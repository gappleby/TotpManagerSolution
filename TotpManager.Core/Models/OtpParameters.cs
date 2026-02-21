namespace TotpManager.Core.Models;

public enum Algorithm
{
    Unspecified = 0,
    SHA1 = 1,
    SHA256 = 2,
    SHA512 = 3,
    MD5 = 4,
}

public enum DigitCount
{
    Unspecified = 0,
    Six = 1,
    Eight = 2,
}

public enum OtpType
{
    Unspecified = 0,
    HOTP = 1,
    TOTP = 2,
}

public class OtpParameters
{
    public byte[] Secret { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public Algorithm Algorithm { get; set; } = Algorithm.Unspecified;
    public DigitCount Digits { get; set; } = DigitCount.Unspecified;
    public OtpType Type { get; set; } = OtpType.Unspecified;
    public long Counter { get; set; }
    public int Id { get; set; }
}

public class MigrationPayload
{
    public List<OtpParameters> OtpParameters { get; set; } = [];
    public int Version { get; set; }
    public int BatchSize { get; set; }
    public int BatchIndex { get; set; }
    public int BatchId { get; set; }
}
