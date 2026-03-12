using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TotpManager.Core;
using TotpManager.Maui.Models;
using TotpManager.Maui.Services;

namespace TotpManager.Maui.ViewModels;

[QueryProperty(nameof(Account), "Account")]
public partial class AccountDetailViewModel : ObservableObject
{
    private readonly AccountStorageService _storage;
    private readonly AccountsViewModel _accountsVm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrImageSource))]
    private AccountRecord? _account;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editIssuer = string.Empty;

    public ImageSource? QrImageSource
    {
        get
        {
            if (Account == null) return null;
            var pngBytes = QrCodeGenerator.GenerateQrPng(Account.OtpUri);
            return ImageSource.FromStream(() => new MemoryStream(pngBytes));
        }
    }

    public AccountDetailViewModel(AccountStorageService storage, AccountsViewModel accountsVm)
    {
        _storage = storage;
        _accountsVm = accountsVm;
    }

    partial void OnAccountChanged(AccountRecord? value)
    {
        if (value == null) return;
        EditName = value.Name;
        EditIssuer = value.Issuer;
    }

    [RelayCommand]
    private async Task CopyUri()
    {
        if (Account == null) return;
        await Clipboard.SetTextAsync(Account.OtpUri);
        await Shell.Current.DisplayAlertAsync("Copied", "Account URI copied to clipboard.", "OK");
    }

    [RelayCommand]
    private void StartEdit() => IsEditing = true;

    [RelayCommand]
    private async Task SaveEdit()
    {
        if (Account == null) return;

        // Rebuild the URI with the updated name/issuer, preserving all other parameters.
        var u = new Uri(Account.OtpUri);
        var query = System.Web.HttpUtility.ParseQueryString(u.Query);
        query.Set("issuer", EditIssuer);

        var newPath = "/" + Uri.EscapeDataString(
            string.IsNullOrEmpty(EditIssuer) ? EditName : $"{EditIssuer}:{EditName}");

        var newUri = $"{u.Scheme}://{u.Host}{newPath}?{query}";

        Account.OtpUri = newUri;
        Account.Name = EditName;
        Account.Issuer = EditIssuer;

        // Sync name in the accounts list display item
        var listItem = _accountsVm.Accounts.FirstOrDefault(a => a.Account == Account);
        listItem?.NotifyAccountChanged();

        await _storage.SaveAsync(_accountsVm.Accounts.Select(a => a.Account));

        IsEditing = false;
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(QrImageSource));
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (Account == null) return;
        EditName = Account.Name;
        EditIssuer = Account.Issuer;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Account == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete", $"Remove {Account.DisplayName}?", "Delete", "Cancel");
        if (!confirm) return;

        var item = _accountsVm.Accounts.FirstOrDefault(a => a.Account.OtpUri == Account.OtpUri);
        if (item != null)
            _accountsVm.Accounts.Remove(item);

        await _storage.SaveAsync(_accountsVm.Accounts.Select(a => a.Account));
        await Shell.Current.GoToAsync("..");
    }
}
