using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TotpManager.Maui.Models;
using TotpManager.Maui.Pages;
using TotpManager.Maui.Services;

namespace TotpManager.Maui.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly AccountStorageService _storage;
    private readonly TotpService _totp;
    private readonly BackupService _backup;
    private Timer? _ticker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredAccounts))]
    private ObservableCollection<AccountDisplayItem> _accounts = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredAccounts))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccounts))]
    private bool _isEmpty = true;

    public bool HasAccounts => !IsEmpty;

    public IEnumerable<AccountDisplayItem> FilteredAccounts =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Accounts
            : Accounts.Where(a =>
                a.Account.Issuer.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Account.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public AccountsViewModel(AccountStorageService storage, TotpService totp, BackupService backup)
    {
        _storage = storage;
        _totp = totp;
        _backup = backup;
    }

    public async Task LoadAsync()
    {
        var records = await _storage.LoadAsync();
        Accounts.Clear();
        foreach (var rec in records)
            Accounts.Add(new AccountDisplayItem(rec));
        IsEmpty = Accounts.Count == 0;
        UpdateCodes();
    }

    public void StartTicker()
    {
        _ticker?.Dispose();
        _ticker = new Timer(
            _ => MainThread.BeginInvokeOnMainThread(UpdateCodes),
            null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public void StopTicker() => _ticker?.Dispose();

    private void UpdateCodes()
    {
        foreach (var item in Accounts)
        {
            if (!item.Account.IsTotp) continue;
            item.Code = _totp.Generate(item.Account);
            item.SecondsRemaining = _totp.SecondsRemaining(item.Account);
            item.Progress = (double)item.SecondsRemaining / item.Account.Period;
        }
        OnPropertyChanged(nameof(FilteredAccounts));
    }

    /// <summary>Merges incoming accounts, deduplicating by URI.</summary>
    public void AddAccounts(IEnumerable<AccountRecord> incoming)
    {
        var existingUris = Accounts.Select(a => a.Account.OtpUri).ToHashSet();
        foreach (var rec in incoming.Where(r => !existingUris.Contains(r.OtpUri)))
            Accounts.Add(new AccountDisplayItem(rec));

        IsEmpty = Accounts.Count == 0;
        UpdateCodes();
        _ = _storage.SaveAsync(Accounts.Select(a => a.Account));
    }

    [RelayCommand]
    private async Task Delete(AccountDisplayItem item)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Account", $"Remove {item.Account.DisplayName}?", "Delete", "Cancel");
        if (!confirm) return;

        Accounts.Remove(item);
        IsEmpty = Accounts.Count == 0;
        OnPropertyChanged(nameof(FilteredAccounts));
        await _storage.SaveAsync(Accounts.Select(a => a.Account));
    }

    [RelayCommand]
    private static async Task CopyCode(AccountDisplayItem item)
    {
        await Clipboard.SetTextAsync(item.Code);
        await Shell.Current.DisplayAlertAsync("Copied", $"Code {item.FormattedCode} copied to clipboard.", "OK");
    }

    [RelayCommand]
    private static async Task OpenDetail(AccountDisplayItem item)
    {
        await Shell.Current.GoToAsync(nameof(AccountDetailPage),
            new Dictionary<string, object> { ["Account"] = item.Account });
    }

    [RelayCommand]
    private static async Task OpenAdd() =>
        await Shell.Current.GoToAsync(nameof(AddAccountPage));

    [RelayCommand]
    private async Task Backup()
    {
        if (IsEmpty) return;

        string? password = await Shell.Current.DisplayPromptAsync(
            "Backup", "Encryption password (leave blank for unencrypted):",
            "Backup", "Cancel", placeholder: "Optional password");
        if (password == null) return; // user cancelled

        try
        {
            var zipPath = await _backup.CreateBackupZipAsync(
                Accounts.Select(a => a.Account),
                string.IsNullOrEmpty(password) ? null : password);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "TOTP Backup",
                File = new ShareFile(zipPath),
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Backup Failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task Restore()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select TOTP backup ZIP",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, ["public.zip-archive"] },
                    { DevicePlatform.Android, ["application/zip"] },
                }),
            });
            if (result == null) return;

            string? password = null;
            if (result.FileName.Contains("-enc", StringComparison.OrdinalIgnoreCase))
            {
                password = await Shell.Current.DisplayPromptAsync(
                    "Restore", "This backup is encrypted. Enter the password:",
                    "Restore", "Cancel");
                if (password == null) return;
            }

            using var stream = await result.OpenReadAsync();
            var (accounts, _) = await _backup.RestoreFromZipAsync(stream, password);

            if (accounts.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Restore", "No accounts found in the backup.", "OK");
                return;
            }

            bool replace = await Shell.Current.DisplayAlertAsync(
                "Restore",
                $"Found {accounts.Count} account(s). Replace all current accounts or merge?",
                "Replace All", "Merge");

            if (replace)
            {
                Accounts.Clear();
                foreach (var acc in accounts)
                    Accounts.Add(new AccountDisplayItem(acc));
                IsEmpty = Accounts.Count == 0;
                await _storage.SaveAsync(Accounts.Select(a => a.Account));
            }
            else
            {
                AddAccounts(accounts);
            }

            UpdateCodes();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Restore Failed", ex.Message, "OK");
        }
    }
}
