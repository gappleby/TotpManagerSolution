// TotpManagerSpa — serves the static SPA; all OTP logic runs in the browser.
//
// PathBase support — set when deployed under a sub-path (e.g. nginx at /totp/):
//   Environment variable : PATHBASE=/totp
//   Command-line arg     : --PathBase /totp
//   appsettings.json     : { "PathBase": "/totp" }
//
// When running standalone (dotnet run) leave PathBase unset; the app serves at /.

var builder = WebApplication.CreateBuilder(args);
var app     = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? string.Empty;
if (!string.IsNullOrWhiteSpace(pathBase))
    app.UsePathBase(pathBase);

app.UseDefaultFiles();   // serves index.html for /  (or /totp/ when PathBase is set)
app.UseStaticFiles();
app.Run();
