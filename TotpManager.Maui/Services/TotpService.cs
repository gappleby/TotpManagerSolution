using TotpManager.Core;
using TotpManager.Core.Models;
using TotpManager.Maui.Models;

namespace TotpManager.Maui.Services;

public class TotpService
{
    public string Generate(AccountRecord account)
    {
        var algorithm = account.Algorithm switch
        {
            "SHA256" => Algorithm.SHA256,
            "SHA512" => Algorithm.SHA512,
            _ => Algorithm.SHA1
        };
        return TotpGenerator.Generate(account.Secret, account.Digits, account.Period, algorithm);
    }

    public int SecondsRemaining(AccountRecord account) =>
        TotpGenerator.SecondsRemaining(account.Period);
}
