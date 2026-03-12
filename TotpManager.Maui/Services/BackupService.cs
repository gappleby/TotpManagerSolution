using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using TotpManager.Core;
using TotpManager.Maui.Models;

namespace TotpManager.Maui.Services;

/// <summary>
/// Creates and restores ZIP backups in the same format as TotpManager.Web and TotpManagerSpa.
/// </summary>
public class BackupService
{
    private readonly QrImportService _importer;

    public BackupService(QrImportService importer)
    {
        _importer = importer;
    }

    public async Task<string> CreateBackupZipAsync(IEnumerable<AccountRecord> accounts, string? password = null)
    {
        var list = accounts.ToList();
        var encrypted = !string.IsNullOrEmpty(password);
        var timestamp = DateTime.Now.ToString("yyyyMMdd");
        var suffix = encrypted ? "-enc" : string.Empty;
        var fileName = $"otp-qr-codes-{timestamp}{suffix}.zip";
        var destPath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await Task.Run(() =>
        {
            using var fs = File.Create(destPath);
            using var zipStream = new ZipOutputStream(fs);
            zipStream.SetLevel(encrypted ? 0 : 6);
            if (encrypted) zipStream.Password = password;

            // accounts.txt
            var txtBytes = Encoding.UTF8.GetBytes(string.Join('\n', list.Select(a => a.OtpUri)));
            WriteEntry(zipStream, "accounts.txt", txtBytes, encrypted);

            // One QR PNG per account
            foreach (var account in list)
            {
                var pngBytes = QrCodeGenerator.GenerateQrPng(account.OtpUri);
                var entryName = BuildEntryName(account);
                WriteEntry(zipStream, entryName, pngBytes, encrypted);
            }

            zipStream.Finish();
        });

        return destPath;
    }

    public async Task<(IReadOnlyList<AccountRecord> Accounts, bool WasEncrypted)> RestoreFromZipAsync(
        Stream zipStream, string? password = null)
    {
        return await Task.Run(() =>
        {
            bool wasEncrypted = false;
            var uriLines = new List<string>();

            using var zis = new ZipInputStream(zipStream);
            if (!string.IsNullOrEmpty(password)) zis.Password = password;

            ZipEntry? entry;
            while ((entry = zis.GetNextEntry()) != null)
            {
                if (!entry.Name.Equals("accounts.txt", StringComparison.OrdinalIgnoreCase)) continue;
                wasEncrypted = entry.AESKeySize > 0;
                using var reader = new StreamReader(zis, Encoding.UTF8, leaveOpen: true);
                var content = reader.ReadToEnd();
                uriLines.AddRange(content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
                break;
            }

            var accounts = _importer.ParseInput(string.Join('\n', uriLines));
            return (accounts, wasEncrypted);
        });
    }

    private static void WriteEntry(ZipOutputStream zip, string name, byte[] data, bool encrypted)
    {
        var entry = new ZipEntry(name)
        {
            DateTime = DateTime.Now,
            Size = data.Length,
            AESKeySize = encrypted ? 256 : 0,
        };
        zip.PutNextEntry(entry);
        zip.Write(data, 0, data.Length);
        zip.CloseEntry();
    }

    private static string BuildEntryName(AccountRecord account)
    {
        var raw = string.IsNullOrEmpty(account.Issuer)
            ? account.Name
            : $"{account.Issuer} - {account.Name}";
        foreach (var c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c, '_');
        return raw + ".png";
    }
}
