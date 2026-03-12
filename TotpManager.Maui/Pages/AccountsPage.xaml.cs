using TotpManager.Maui.ViewModels;

namespace TotpManager.Maui.Pages;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _vm;

    public AccountsPage(AccountsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        _vm.StartTicker();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopTicker();
    }
}
