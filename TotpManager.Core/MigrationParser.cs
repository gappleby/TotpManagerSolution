using TotpManager.Core.Models;

namespace TotpManager.Core;

public static class MigrationParser
{
    /// <summary>
    /// Parses a Google Authenticator migration URL into a MigrationPayload.
    /// Expected format: otpauth-migration://offline?data=BASE64_PROTOBUF
    /// </summary>
    public static MigrationPayload Parse(string migrationUrl)
    {
        var uri = new Uri(migrationUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var data = query["data"] ?? throw new ArgumentException("Migration URL has no 'data' query parameter.");

        // URL-encoded '+' becomes ' ' after ParseQueryString; restore it
        data = data.Replace(' ', '+');

        var bytes = Convert.FromBase64String(data);
        return ProtobufDecoder.DecodePayload(bytes);
    }
}
