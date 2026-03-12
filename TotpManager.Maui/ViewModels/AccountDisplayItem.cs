using CommunityToolkit.Mvvm.ComponentModel;
using TotpManager.Maui.Models;

namespace TotpManager.Maui.ViewModels;

/// <summary>
/// Wraps an AccountRecord with live TOTP display state (code, countdown, progress).
/// </summary>
public partial class AccountDisplayItem : ObservableObject
{
    public AccountRecord Account { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedCode))]
    private string _code = "------";

    [ObservableProperty]
    private int _secondsRemaining;

    [ObservableProperty]
    private double _progress = 1.0;

    public string FormattedCode => Code.Length switch
    {
        6 => $"{Code[..3]} {Code[3..]}",
        8 => $"{Code[..4]} {Code[4..]}",
        _ => Code,
    };

    public AccountDisplayItem(AccountRecord account)
    {
        Account = account;
    }

    /// <summary>Notifies the UI that Account display properties (name/issuer) have changed.</summary>
    public void NotifyAccountChanged() => OnPropertyChanged(nameof(Account));
}
