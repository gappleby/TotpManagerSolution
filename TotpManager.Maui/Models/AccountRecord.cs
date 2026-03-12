namespace TotpManager.Maui.Models;

public class AccountRecord
{
    public string OtpUri { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte[] Secret { get; set; } = [];
    public string Algorithm { get; set; } = "SHA1";
    public int Digits { get; set; } = 6;
    public int Period { get; set; } = 30;
    public bool IsTotp { get; set; } = true;
    public long Counter { get; set; }

    /// <summary>Primary display label — the issuer if present, otherwise the account name.</summary>
    public string DisplayName => string.IsNullOrEmpty(Issuer) ? Name : Issuer;

    /// <summary>Secondary label shown under DisplayName — the account name when an issuer is present.</summary>
    public string SubtitleName => string.IsNullOrEmpty(Issuer) ? string.Empty : Name;
}
