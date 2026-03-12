using TotpManager.Core;
using TotpManager.Core.Models;
using TotpManager.Maui.Models;

namespace TotpManager.Maui.Services;

/// <summary>
/// Parses otpauth-migration:// and otpauth:// URIs into AccountRecord objects.
/// </summary>
public class QrImportService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public IReadOnlyList<AccountRecord> ParseInput(string input)
    {
        var results = new List<AccountRecord>();

        foreach (var line in input.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var payload = MigrationParser.Parse(trimmed);
                    foreach (var otp in payload.OtpParameters)
                        results.Add(OtpParametersToRecord(otp));
                }
                catch { /* skip malformed lines */ }
            }
            else if (trimmed.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var record = ParseOtpAuthUri(trimmed);
                if (record != null) results.Add(record);
            }
        }

        return results;
    }

    private static AccountRecord OtpParametersToRecord(OtpParameters otp)
    {
        return new AccountRecord
        {
            OtpUri = OtpAuthBuilder.BuildUri(otp),
            Issuer = otp.Issuer,
            Name = otp.Name,
            Secret = otp.Secret,
            Algorithm = otp.Algorithm is Algorithm.Unspecified or Algorithm.SHA1 ? "SHA1"
                      : otp.Algorithm == Algorithm.SHA256 ? "SHA256"
                      : otp.Algorithm == Algorithm.SHA512 ? "SHA512" : "SHA1",
            Digits = otp.Digits == DigitCount.Eight ? 8 : 6,
            Period = 30,
            IsTotp = otp.Type != OtpType.HOTP,
            Counter = otp.Counter,
        };
    }

    private static AccountRecord? ParseOtpAuthUri(string uri)
    {
        try
        {
            var u = new Uri(uri);
            var isHotp = u.Host.Equals("hotp", StringComparison.OrdinalIgnoreCase);
            var path = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/'));

            string issuer, name;
            var colonIdx = path.IndexOf(':');
            if (colonIdx >= 0)
            {
                issuer = path[..colonIdx];
                name = path[(colonIdx + 1)..];
            }
            else
            {
                issuer = string.Empty;
                name = path;
            }

            var query = System.Web.HttpUtility.ParseQueryString(u.Query);
            var secretB32 = query["secret"] ?? string.Empty;
            var issuerParam = query["issuer"];
            if (!string.IsNullOrEmpty(issuerParam)) issuer = issuerParam;

            var algorithmStr = (query["algorithm"] ?? "SHA1").ToUpperInvariant();
            var digitsStr = query["digits"] ?? "6";
            var periodStr = query["period"] ?? "30";
            var counterStr = query["counter"] ?? "0";

            return new AccountRecord
            {
                OtpUri = uri,
                Issuer = issuer,
                Name = name,
                Secret = Base32Decode(secretB32),
                Algorithm = algorithmStr switch { "SHA256" => "SHA256", "SHA512" => "SHA512", _ => "SHA1" },
                Digits = int.TryParse(digitsStr, out var d) ? d : 6,
                Period = int.TryParse(periodStr, out var p) ? p : 30,
                IsTotp = !isHotp,
                Counter = long.TryParse(counterStr, out var c) ? c : 0,
            };
        }
        catch { return null; }
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant();
        var result = new List<byte>();
        int buffer = 0, bitsLeft = 0;

        foreach (char ch in input)
        {
            var idx = Base32Alphabet.IndexOf(ch);
            if (idx < 0) continue;
            buffer = (buffer << 5) | idx;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result.Add((byte)(buffer >> bitsLeft));
            }
        }

        return [.. result];
    }
}
