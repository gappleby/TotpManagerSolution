using System.Text;
using TotpManager.Maui.Models;

namespace TotpManager.Maui.Services;

/// <summary>
/// Persists accounts as a newline-delimited otpauth:// URI list — same format as the ZIP backup's accounts.txt.
/// </summary>
public class AccountStorageService
{
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "accounts.txt");
    private readonly QrImportService _importer;

    public AccountStorageService(QrImportService importer)
    {
        _importer = importer;
    }

    public async Task<IReadOnlyList<AccountRecord>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return [];
        var content = await File.ReadAllTextAsync(_filePath, Encoding.UTF8);
        return _importer.ParseInput(content);
    }

    public async Task SaveAsync(IEnumerable<AccountRecord> accounts)
    {
        var uris = string.Join('\n', accounts.Select(a => a.OtpUri));
        await File.WriteAllTextAsync(_filePath, uris, Encoding.UTF8);
    }
}
