using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using TotpManager.Maui.Pages;
using TotpManager.Maui.Services;
using TotpManager.Maui.ViewModels;
using ZXing.Net.Maui.Controls;

namespace TotpManager.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<QrImportService>();
        builder.Services.AddSingleton<AccountStorageService>();
        builder.Services.AddSingleton<TotpService>();
        builder.Services.AddSingleton<BackupService>();

        // ViewModels
        builder.Services.AddSingleton<AccountsViewModel>();
        builder.Services.AddTransient<AddAccountViewModel>();
        builder.Services.AddTransient<AccountDetailViewModel>();

        // Pages
        builder.Services.AddSingleton<AccountsPage>();
        builder.Services.AddTransient<AddAccountPage>();
        builder.Services.AddTransient<AccountDetailPage>();

#if DEBUG
        builder.Logging.AddDebug(); // requires Microsoft.Extensions.Logging.Debug package
#endif

        return builder.Build();
    }
}
