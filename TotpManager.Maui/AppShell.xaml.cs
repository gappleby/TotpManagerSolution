using TotpManager.Maui.Pages;

namespace TotpManager.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AddAccountPage), typeof(AddAccountPage));
        Routing.RegisterRoute(nameof(AccountDetailPage), typeof(AccountDetailPage));
    }
}
