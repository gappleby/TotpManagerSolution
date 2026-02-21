using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

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
