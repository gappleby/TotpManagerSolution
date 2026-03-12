using TotpManager.Maui.ViewModels;

namespace TotpManager.Maui.Pages;

public partial class AccountDetailPage : ContentPage
{
    public AccountDetailPage(AccountDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
