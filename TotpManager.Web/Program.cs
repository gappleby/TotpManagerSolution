using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Cap upload size at 20 MB — enforced at the Kestrel/middleware layer before handlers run.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 20 * 1024 * 1024);

// DataProtection is required by Razor Pages for antiforgery token signing.
// Keys are ephemeral (in-memory); a container restart invalidates open form tokens
// (harmless — users just re-submit). No volume mount needed.
builder.Services.AddDataProtection()
    .SetApplicationName("TotpManager");

// Trust X-Forwarded-For / X-Forwarded-Proto headers from QNAP's reverse proxy.
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Allow any proxy (local network); restrict in production if needed.
    opts.KnownIPNetworks.Clear();
    opts.KnownProxies.Clear();
});

var app = builder.Build();

// Must be first so subsequent middleware sees the real scheme/host.
app.UseForwardedHeaders();

// Security headers — applied to every response.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"] = "DENY";  // prevents embedding in iframe
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";

    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +   // <-- add blob:
        "connect-src 'none'; " +
        "frame-ancestors 'none';";

    await next();
});

// HTTPS redirect is handled by QNAP's reverse proxy — skip it in the container.
if (app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
