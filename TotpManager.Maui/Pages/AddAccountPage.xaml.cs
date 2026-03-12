using TotpManager.Maui.ViewModels;
using ZXing.Net.Maui;

namespace TotpManager.Maui.Pages;

public partial class AddAccountPage : ContentPage
{
    private readonly AddAccountViewModel _vm;

    public AddAccountPage(AddAccountViewModel vm, AccountsViewModel accountsVm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // Wire the callback so imported accounts are added to the main list
        vm.OnAccountsImported = accountsVm.AddAccounts;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(value))
            _vm.HandleScannedBarcode(value);
    }

    private async void OnCancelClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
