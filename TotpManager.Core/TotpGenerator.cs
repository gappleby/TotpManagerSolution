using System.Security.Cryptography;
using TotpManager.Core.Models;

namespace TotpManager.Core;

public static class TotpGenerator
{
    public static string Generate(byte[] secret, int digits = 6, int period = 30, Algorithm algorithm = Algorithm.SHA1)
    {
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;
        return ComputeHotp(secret, counter, digits, algorithm);
    }

    public static int SecondsRemaining(int period = 30)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return period - (int)(now % period);
    }

    private static string ComputeHotp(byte[] secret, long counter, int digits, Algorithm algorithm)
    {
        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        byte[] hash = algorithm switch
        {
            Algorithm.SHA256 => HMACSHA256.HashData(secret, counterBytes),
            Algorithm.SHA512 => HMACSHA512.HashData(secret, counterBytes),
            _ => HMACSHA1.HashData(secret, counterBytes)
        };

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset]     & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   |  (hash[offset + 3] & 0xFF);

        int divisor = (int)Math.Pow(10, digits);
        return (binary % divisor).ToString().PadLeft(digits, '0');
    }
}
