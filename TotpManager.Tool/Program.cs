using TotpManager.Core;

// ─── Argument parsing ────────────────────────────────────────────────────────

string? inputUrl = null;
string? imagePath = null;
string outputDir = "output";
bool noQr = false;
bool quiet = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url" when i + 1 < args.Length:
            inputUrl = args[++i];
            break;
        case "--image" when i + 1 < args.Length:
            imagePath = args[++i];
            break;
        case "--output-dir" when i + 1 < args.Length:
            outputDir = args[++i];
            break;
        case "--no-qr":
            noQr = true;
            break;
        case "--quiet":
            quiet = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (inputUrl is null && imagePath is null)
{
    Console.Error.WriteLine("Error: provide --url or --image.");
    PrintUsage();
    return 1;
}

if (inputUrl is not null && imagePath is not null)
{
    Console.Error.WriteLine("Error: --url and --image are mutually exclusive.");
    PrintUsage();
    return 1;
}

// ─── Resolve migration URL ───────────────────────────────────────────────────

string migrationUrl;
try
{
    if (imagePath is not null)
    {
        if (!quiet) Console.WriteLine($"Decoding QR image: {imagePath}");
        migrationUrl = QrCodeDecoder.DecodeQrFromFile(imagePath);
        if (!quiet) Console.WriteLine($"Decoded URL: {migrationUrl}\n");
    }
    else
    {
        migrationUrl = inputUrl!;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to read QR image: {ex.Message}");
    return 2;
}

// ─── Parse migration payload ─────────────────────────────────────────────────

TotpManager.Core.Models.MigrationPayload payload;
try
{
    payload = MigrationParser.Parse(migrationUrl);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to parse migration URL: {ex.Message}");
    return 3;
}

if (payload.OtpParameters.Count == 0)
{
    Console.Error.WriteLine("No OTP accounts found in migration data.");
    return 0;
}

// ─── Output directory ────────────────────────────────────────────────────────

if (!noQr)
    Directory.CreateDirectory(outputDir);

// ─── Process each account ────────────────────────────────────────────────────

int index = 0;
foreach (var otp in payload.OtpParameters)
{
    index++;
    string uri = OtpAuthBuilder.BuildUri(otp);

    // Always print the otpauth:// URI; --quiet only suppresses informational messages
    Console.WriteLine(uri);

    if (!noQr)
    {
        try
        {
            byte[] png = QrCodeGenerator.GenerateQrPng(uri);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(otp.Name) ? $"account_{index}" : otp.Name);
            string filePath = Path.Combine(outputDir, $"{safeName}.png");
            // Avoid overwriting if duplicate names
            filePath = MakeUnique(filePath);
            File.WriteAllBytes(filePath, png);
            if (!quiet)
                Console.WriteLine($"  -> Saved QR: {filePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Warning: failed to generate QR for '{otp.Name}': {ex.Message}");
        }
    }
}

Console.WriteLine($"\n{index} account(s) exported.");
return 0;

// ─── Helpers ─────────────────────────────────────────────────────────────────

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var chars = name.ToCharArray();
    for (int i = 0; i < chars.Length; i++)
        if (Array.IndexOf(invalid, chars[i]) >= 0)
            chars[i] = '_';
    return new string(chars);
}

static string MakeUnique(string filePath)
{
    if (!File.Exists(filePath)) return filePath;
    string dir = Path.GetDirectoryName(filePath)!;
    string nameNoExt = Path.GetFileNameWithoutExtension(filePath);
    string ext = Path.GetExtension(filePath);
    int n = 2;
    string candidate;
    do { candidate = Path.Combine(dir, $"{nameNoExt}_{n++}{ext}"); }
    while (File.Exists(candidate));
    return candidate;
}

static void PrintUsage()
{
    Console.Error.WriteLine("""

    Usage:
      TotpManager.Tool --url "<otpauth-migration://offline?data=...>"
      TotpManager.Tool --image <path-to-migration-qr.png>

    Options:
      --output-dir <dir>   Directory for individual QR PNG files (default: ./output)
      --no-qr              Skip generating QR image files
      --quiet              Suppress informational output; only print otpauth:// URIs
    """);
}
