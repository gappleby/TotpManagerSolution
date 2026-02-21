using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TotpManager.Core;
using TotpManager.Core.Models;

namespace TotpManager.Web.Pages;

public class IndexModel : PageModel
{
    // ── Input ──────────────────────────────────────────────────────────────
    [BindProperty] public string InputMode { get; set; } = "url";
    [BindProperty] public string? MigrationUrl { get; set; }
    [BindProperty] public IFormFile? QrImage { get; set; }
    [BindProperty] public IFormFile? ZipUpload { get; set; }
    [BindProperty] public IFormFile? RestoreZip { get; set; }

    // Newline-joined otpauth:// URIs of the accounts currently on screen,
    // submitted as a hidden field so the server can merge/restore them.
    [BindProperty] public string? ExistingOtpUris { get; set; }

    [BindProperty] public string? BackupPassword  { get; set; }
    [BindProperty] public string? RestorePassword { get; set; }

    [BindProperty] public string? UriToDelete { get; set; }
    [BindProperty] public string? OldUri      { get; set; }
    [BindProperty] public string? NewName     { get; set; }
    [BindProperty] public string? NewIssuer   { get; set; }

    // ResolvedMigrationUrl carries either:
    //   • an otpauth-migration:// URL  (url / image / camera modes)
    //   • newline-joined otpauth:// URIs (zip mode, or any append/restore)
    [BindProperty] public string? ResolvedMigrationUrl { get; set; }

    // ── Results ────────────────────────────────────────────────────────────
    public List<AccountResult> Accounts { get; set; } = [];
    public string? ErrorMessage { get; set; }
    // True when restore succeeded and the ZIP contained no encrypted entries.
    // JS uses this to clear the stored backup password from localStorage.
    public bool RestoredWithoutPassword { get; set; }

    // Tells the view to re-open the Add panel (used when a submission fails).
    public bool ShowAddPanel { get; set; }
    public string AddPanelMode { get; set; } = "url";

    public record AccountResult(
        string Name,
        string Issuer,
        string OtpUri,
        string TypeLabel,
        string AlgorithmLabel,
        int Digits,
        string QrPngBase64);

    // ── Handlers ───────────────────────────────────────────────────────────
    public void OnGet() { }

    public async Task<IActionResult> OnPostAddAsync()
    {
        try
        {
            if (InputMode == "zip") await ProcessZipUploadAsync();
            else                    await ProcessMigrationUrlAsync();
        }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }

        // Always merge new accounts into existing
        var existing = ParseUriList(ExistingOtpUris)
            .Select(BuildAccountResultFromOtpUri).OfType<AccountResult>().ToList();
        Accounts = Accounts.Count > 0 ? MergeAccounts(existing, Accounts) : existing;
        ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));

        if (ErrorMessage != null) { ShowAddPanel = true; AddPanelMode = InputMode; }
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync()
    {
        if (RestoreZip is null || RestoreZip.Length == 0)
        {
            ErrorMessage = "Please select a ZIP file.";
            return Page();
        }

        try
        {
            var (accounts, wasEncrypted) = await LoadAccountsFromZipAsync(RestoreZip, RestorePassword);
            Accounts = accounts;
            if (Accounts.Count == 0)
                ErrorMessage = "No valid OTP QR codes found in ZIP.";
            else
                ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));
            RestoredWithoutPassword = Accounts.Count > 0 && !wasEncrypted;
        }
        catch (Exception ex) { ErrorMessage = $"Error restoring: {ex.Message}"; }

        return Page();
    }

    public IActionResult OnPostDownloadZip()
    {
        if (string.IsNullOrWhiteSpace(ResolvedMigrationUrl))
            return BadRequest("No data available.");

        try
        {
            IEnumerable<(string filename, string otpUri)> entries;

            if (ResolvedMigrationUrl.TrimStart().StartsWith("otpauth-migration://"))
            {
                var payload = MigrationParser.Parse(ResolvedMigrationUrl);
                entries = payload.OtpParameters.Select(otp =>
                {
                    var uri = OtpAuthBuilder.BuildUri(otp);
                    var name = string.IsNullOrWhiteSpace(otp.Issuer)
                        ? otp.Name
                        : $"{otp.Issuer} - {otp.Name}";
                    return (name, uri);
                });
            }
            else
            {
                entries = ParseUriList(ResolvedMigrationUrl)
                    .Select(uri => (LabelFromOtpUri(uri), uri));
            }

            var hasPassword = !string.IsNullOrEmpty(BackupPassword);
            var ms = new MemoryStream();
            using (var zipStream = new ZipOutputStream(ms) { IsStreamOwner = false })
            {
                zipStream.SetLevel(5);
                if (hasPassword) zipStream.Password = BackupPassword;

                var seen     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var uriList  = new List<string>();

                foreach (var (filename, uri) in entries)
                {
                    uriList.Add(uri);
                    var pngBytes = QrCodeGenerator.GenerateQrPng(uri);
                    var baseName = SanitizeFilename(filename);
                    if (string.IsNullOrEmpty(baseName)) baseName = "account";

                    seen.TryGetValue(baseName, out var count);
                    seen[baseName] = count + 1;
                    var entryName = count == 0 ? $"{baseName}.png" : $"{baseName} ({count}).png";

                    var entry = new ZipEntry(entryName)
                    {
                        DateTime   = DateTime.UtcNow,
                        Size       = pngBytes.Length,
                        AESKeySize = hasPassword ? 256 : 0,
                    };
                    zipStream.PutNextEntry(entry);
                    zipStream.Write(pngBytes, 0, pngBytes.Length);
                    zipStream.CloseEntry();
                }

                // Embed a plain-text list of OTP URIs so restore works on any platform
                // (Linux containers can't decode QR images via System.Drawing).
                var uriBytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', uriList));
                var txtEntry = new ZipEntry("accounts.txt")
                {
                    DateTime   = DateTime.UtcNow,
                    Size       = uriBytes.Length,
                    AESKeySize = hasPassword ? 256 : 0,
                };
                zipStream.PutNextEntry(txtEntry);
                zipStream.Write(uriBytes, 0, uriBytes.Length);
                zipStream.CloseEntry();
            }

            ms.Position = 0;
            var datePart  = DateTime.UtcNow.ToString("yyyyMMdd");
            var encSuffix = hasPassword ? "-enc" : "";
            return File(ms, "application/zip", $"otp-qr-codes-{datePart}{encSuffix}.zip");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error generating ZIP: {ex.Message}");
        }
    }

    public IActionResult OnPostDelete()
    {
        var existing = ParseUriList(ExistingOtpUris)
            .Where(u => u != UriToDelete).ToList();
        Accounts = existing.Select(BuildAccountResultFromOtpUri)
            .OfType<AccountResult>().ToList();
        ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));
        return Page();
    }

    public IActionResult OnPostEdit()
    {
        var existing = ParseUriList(ExistingOtpUris).ToList();
        if (!string.IsNullOrEmpty(OldUri))
        {
            var rebuilt = RebuildOtpUri(OldUri, NewName?.Trim() ?? "", NewIssuer?.Trim() ?? "");
            var idx = existing.IndexOf(OldUri);
            if (idx >= 0) existing[idx] = rebuilt;
        }
        Accounts = existing.Select(BuildAccountResultFromOtpUri)
            .OfType<AccountResult>().ToList();
        ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));
        return Page();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task ProcessZipUploadAsync()
    {
        if (ZipUpload is null || ZipUpload.Length == 0)
        {
            ErrorMessage = "Please select a ZIP file.";
            return;
        }

        (Accounts, _) = await LoadAccountsFromZipAsync(ZipUpload);

        if (Accounts.Count == 0)
            ErrorMessage = "No valid otpauth QR codes found in the ZIP.";

        ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));
    }

    private async Task ProcessMigrationUrlAsync()
    {
        string? migrationUrl;

        if (InputMode == "image")
        {
            if (QrImage is null || QrImage.Length == 0)
            {
                ErrorMessage = "Please select a QR image file to upload.";
                return;
            }

            var ext = Path.GetExtension(QrImage.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var tmp = Path.ChangeExtension(Path.GetTempFileName(), ext);
            try
            {
                await using (var fs = System.IO.File.Create(tmp))
                    await QrImage.CopyToAsync(fs);
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException(
                        "Server-side QR decoding is not available on this platform. " +
                        "Use the browser-based image upload — it decodes the QR code locally in your browser.");
#pragma warning disable CA1416
                migrationUrl = QrCodeDecoder.DecodeQrFromFile(tmp);
#pragma warning restore CA1416
            }
            finally
            {
                if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(MigrationUrl))
            {
                ErrorMessage = "Please paste a migration URL or otpauth:// URI.";
                return;
            }
            migrationUrl = MigrationUrl.Trim();
        }

        // Direct otpauth:// URI(s) — one per line (single-account QR codes)
        if (migrationUrl.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            var uris = migrationUrl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .Where(u => u.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase));
            foreach (var uri in uris)
            {
                var result = BuildAccountResultFromOtpUri(uri);
                if (result is not null) Accounts.Add(result);
            }
            if (Accounts.Count == 0)
                ErrorMessage = "No valid otpauth:// URIs found.";
            ResolvedMigrationUrl = string.Join('\n', Accounts.Select(a => a.OtpUri));
            return;
        }

        var payload = MigrationParser.Parse(migrationUrl);

        foreach (var otp in payload.OtpParameters)
        {
            var uri = OtpAuthBuilder.BuildUri(otp);
            var pngBytes = QrCodeGenerator.GenerateQrPng(uri);

            var typeLabel = otp.Type switch { OtpType.HOTP => "HOTP", _ => "TOTP" };
            var algLabel  = otp.Algorithm switch
            {
                Algorithm.SHA256 => "SHA256",
                Algorithm.SHA512 => "SHA512",
                Algorithm.MD5    => "MD5",
                _                => "SHA1"
            };
            var digits = otp.Digits == DigitCount.Eight ? 8 : 6;

            Accounts.Add(new AccountResult(
                otp.Name, otp.Issuer, uri,
                typeLabel, algLabel, digits,
                Convert.ToBase64String(pngBytes)));
        }

        ResolvedMigrationUrl = migrationUrl;
    }

    /// <summary>
    /// Opens a ZIP (optionally password-protected) and decodes every image entry as an otpauth QR code.
    /// Returns the decoded accounts and whether the ZIP contained any encrypted entries.
    /// </summary>
    private static async Task<(List<AccountResult> accounts, bool wasEncrypted)>
        LoadAccountsFromZipAsync(IFormFile file, string? password = null)
    {
        var results      = new List<AccountResult>();
        var wasEncrypted = false;

        // ZipFile (random-access) is required for AES-encrypted entries;
        // ZipInputStream only handles legacy PKZIP encryption.
        // Copy to MemoryStream first to guarantee a seekable stream.
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        using var zipFile = new ZipFile(ms) { IsStreamOwner = false };
        if (!string.IsNullOrEmpty(password)) zipFile.Password = password;

        // Single pass: collect accounts.txt content and image entries.
        string? accountsText = null;
        var imageEntries = new List<ZipEntry>();

        foreach (ZipEntry entry in zipFile)
        {
            if (!entry.IsFile) continue;
            if (entry.IsCrypted) wasEncrypted = true;

            if (entry.Name.Equals("accounts.txt", StringComparison.OrdinalIgnoreCase))
            {
                // Platform-independent OTP URI list written by current backup format.
                using var stream = zipFile.GetInputStream(entry);
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
                accountsText = reader.ReadToEnd();
            }
            else
            {
                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                    imageEntries.Add(entry);
            }
        }

        if (accountsText is not null)
        {
            // Prefer accounts.txt — works on every platform, including Linux containers.
            var uris = accountsText.Split('\n',
                           StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Where(u => u.StartsWith("otpauth://"));
            foreach (var uri in uris)
            {
                var result = BuildAccountResultFromOtpUri(uri);
                if (result is not null) results.Add(result);
            }
        }
        else
        {
            // Legacy ZIPs (no accounts.txt): fall back to QR image decode.
            // Only works on Windows (System.Drawing.Common unavailable on Linux).
            foreach (var entry in imageEntries)
            {
                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                var tmp = Path.ChangeExtension(Path.GetTempFileName(), ext);
                try
                {
                    using var entryStream = zipFile.GetInputStream(entry);
                    await using (var fs = System.IO.File.Create(tmp))
                        await entryStream.CopyToAsync(fs);

                    if (!OperatingSystem.IsWindows()) continue;
#pragma warning disable CA1416
                    var uri = QrCodeDecoder.DecodeQrFromFile(tmp);
#pragma warning restore CA1416
                    if (!uri.StartsWith("otpauth://")) continue;

                    var result = BuildAccountResultFromOtpUri(uri);
                    if (result is not null) results.Add(result);
                }
                catch { /* skip unreadable / non-QR entries */ }
                finally
                {
                    if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
                }
            }
        }

        return (results, wasEncrypted);
    }

    /// <summary>
    /// Merges incoming accounts into the existing list.
    /// Accounts matched by (type, issuer, name) are replaced in-place;
    /// unmatched incoming accounts are appended at the end.
    /// </summary>
    private static List<AccountResult> MergeAccounts(
        List<AccountResult> existing, List<AccountResult> incoming)
    {
        var result = existing.ToList();

        foreach (var newAcct in incoming)
        {
            var key = AccountKey(newAcct);
            var idx = result.FindIndex(a => AccountKey(a) == key);
            if (idx >= 0)
                result[idx] = newAcct;
            else
                result.Add(newAcct);
        }

        return result;
    }

    /// <summary>Identity key for deduplication: type + issuer + name (case-insensitive).</summary>
    private static string AccountKey(AccountResult a) =>
        $"{a.TypeLabel}|{a.Issuer.Trim().ToLowerInvariant()}|{a.Name.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Builds an AccountResult by parsing an otpauth:// URI directly.
    /// Used when loading accounts from a ZIP export or restoring from hidden field.
    /// </summary>
    private static AccountResult? BuildAccountResultFromOtpUri(string uri)
    {
        try
        {
            var u = new Uri(uri);
            var typeLabel = u.Host.Equals("hotp", StringComparison.OrdinalIgnoreCase) ? "HOTP" : "TOTP";

            var label = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/'));
            string name, issuer;
            var colon = label.IndexOf(':');
            if (colon >= 0) { issuer = label[..colon]; name = label[(colon + 1)..]; }
            else             { name = label; issuer = string.Empty; }

            var p = System.Web.HttpUtility.ParseQueryString(u.Query);
            if (!string.IsNullOrEmpty(p["issuer"])) issuer = p["issuer"]!;

            var algLabel = (p["algorithm"] ?? "SHA1").ToUpperInvariant();
            var digits = int.TryParse(p["digits"], out var d) ? d : 6;

            var pngBytes = QrCodeGenerator.GenerateQrPng(uri);
            return new AccountResult(name, issuer, uri, typeLabel, algLabel, digits,
                Convert.ToBase64String(pngBytes));
        }
        catch { return null; }
    }

    /// <summary>Extracts a human-readable label from an otpauth:// URI for use as a ZIP filename.</summary>
    private static string LabelFromOtpUri(string uri)
    {
        try
        {
            var u = new Uri(uri);
            var p = System.Web.HttpUtility.ParseQueryString(u.Query);
            var label = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/'));

            var colon = label.IndexOf(':');
            var name   = colon >= 0 ? label[(colon + 1)..] : label;
            var issuer = !string.IsNullOrEmpty(p["issuer"]) ? p["issuer"]!
                       : colon >= 0 ? label[..colon] : string.Empty;

            return string.IsNullOrWhiteSpace(issuer) ? name : $"{issuer} - {name}";
        }
        catch { return "account"; }
    }

    private static IEnumerable<string> ParseUriList(string? raw) =>
        (raw ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Where(u => u.StartsWith("otpauth://"));

    private static string RebuildOtpUri(string originalUri, string newName, string newIssuer)
    {
        try
        {
            var u = new Uri(originalUri);
            var p = System.Web.HttpUtility.ParseQueryString(u.Query);
            var label = string.IsNullOrWhiteSpace(newIssuer)
                ? Uri.EscapeDataString(newName)
                : Uri.EscapeDataString(newIssuer) + ":" + Uri.EscapeDataString(newName);
            if (string.IsNullOrWhiteSpace(newIssuer)) p.Remove("issuer");
            else p["issuer"] = newIssuer;
            return $"otpauth://{u.Host}/{label}?{(p.ToString() ?? "").Replace("+", "%20")}";
        }
        catch { return originalUri; }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
