using TotpManager.Core.Models;
using System.Text;

namespace TotpManager.Core;

public static class OtpAuthBuilder
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string BuildUri(OtpParameters op)
    {
        var host = op.Type == OtpType.HOTP ? "hotp" : "totp";
        var path = "/" + Uri.EscapeDataString(op.Name);
        var secret = Base32Encode(op.Secret);

        var sb = new StringBuilder();
        sb.Append($"otpauth://{host}{path}?secret={secret}");

        if (!string.IsNullOrEmpty(op.Issuer))
            sb.Append($"&issuer={Uri.EscapeDataString(op.Issuer)}");

        // Only include algorithm if it's not SHA1 / unspecified (SHA1 is the default)
        if (op.Algorithm is not Algorithm.Unspecified and not Algorithm.SHA1)
            sb.Append($"&algorithm={AlgorithmName(op.Algorithm)}");

        // Only include digits if it's not 6 / unspecified (6 is the default)
        if (op.Digits == DigitCount.Eight)
            sb.Append("&digits=8");

        if (op.Type == OtpType.TOTP)
            sb.Append("&period=30");
        else if (op.Type == OtpType.HOTP)
            sb.Append($"&counter={op.Counter}");

        return sb.ToString();
    }

    private static string AlgorithmName(Algorithm algorithm) => algorithm switch
    {
        Algorithm.SHA1   => "SHA1",
        Algorithm.SHA256 => "SHA256",
        Algorithm.SHA512 => "SHA512",
        Algorithm.MD5    => "MD5",
        _                => "SHA1",
    };

    /// <summary>Base32 encoding without padding, per RFC 4648.</summary>
    private static string Base32Encode(byte[] data)
    {
        if (data.Length == 0) return string.Empty;

        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
            result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);

        return result.ToString();
    }
}
