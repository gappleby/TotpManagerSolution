# TotpManagerSolution

A self-hosted .NET 10 tool for importing, managing, and backing up TOTP/HOTP accounts from Google Authenticator migration QR codes and standard `otpauth://` URIs. Deployed as a Docker container behind an nginx reverse proxy.

---

## Solution Structure

```
TotpManagerSolution/
├── TotpManager.Core/           # net10.0 class library — shared business logic
├── TotpManager.Tool/           # net10.0-windows console app — batch CLI
├── TotpManager.Web/            # net10.0 ASP.NET Core Razor Pages — web UI
├── Dockerfile                  # multi-stage build for TotpManager.Web
├── TotpManagerSolution.slnx    # modern solution format
├── Description.md              # end-user guide
└── README.md                   # this file
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, ASP.NET Core |
| UI | Razor Pages, Bootstrap 5.3.3 (CDN + SRI), vanilla JS |
| QR decode (server) | ZXing.Net 0.16.9 + System.Drawing.Common 9.0.2 (Windows only) |
| QR decode (browser) | Native `BarcodeDetector` API → jsQR 1.4.0 |
| QR encode | QRCoder 1.6.0 |
| ZIP backup | SharpZipLib 1.4.2 (AES-256) |
| TOTP generation (browser) | Web Crypto API (secure context) → jsSHA 3.3.1 (fallback) |
| Container | Docker — `mcr.microsoft.com/dotnet/aspnet:10.0` |

---

## TotpManager.Core

The shared library both the web app and CLI depend on. No ASP.NET references — pure .NET 10.

### Classes

#### `ProtobufDecoder`
Hand-rolled proto3 wire-format decoder. Uses a `ref struct ProtoReader` for stack-allocated, zero-GC parsing. Written this way because `Grpc.Tools` proto codegen doesn't work reliably in non-SDK-style projects.

Decodes the binary payload from a Google Authenticator migration URL into a `MigrationPayload` containing a list of `OtpParameters`.

#### `MigrationParser`
Accepts `otpauth-migration://offline?data=<base64>` URLs. Extracts and base64-decodes the `data` query parameter, restoring `+` characters that were URL-encoded as spaces, then delegates to `ProtobufDecoder`.

#### `OtpAuthBuilder`
Converts an `OtpParameters` object into a standard `otpauth://[totp|hotp]/[issuer:]name?secret=...` URI. Implements RFC 4648 base32 encoding without padding. Omits default values (`SHA1`, `6 digits`) to produce clean URIs.

#### `QrCodeDecoder`
`[SupportedOSPlatform("windows")]` — depends on `System.Drawing.Common`.

Wraps ZXing.Net's `BarcodeReaderGeneric`. Uses `BitmapFormat.BGRA32` with direct `LockBits` pixel extraction. Tries five scales (0.5×, 0.75×, 1.0×, 1.5×, 2.0×) and two binarizers (`HybridBinarizer` for photos, `GlobalHistogramBinarizer` for clean/digital images) to handle high-DPI screenshots and low-resolution source images.

> **Why multi-scale?** ZXing.Net 0.16.x fails on high-version (dense) QR codes (v25+) such as those produced by Google Authenticator's multi-account export. Scaling up can bring these within the decoder's capability.

#### `QrCodeGenerator`
Thin wrapper around QRCoder. Produces raw PNG bytes at ECC level Q (25% damage tolerance), 10 pixels per module.

### Models

```
MigrationPayload
  └── OtpParameters[]
        ├── secret: byte[]
        ├── name, issuer: string
        ├── algorithm: Algorithm  (SHA1 | SHA256 | SHA512 | MD5 | Unspecified)
        ├── digits: DigitCount    (Six | Eight | Unspecified)
        ├── type: OtpType         (TOTP | HOTP | Unspecified)
        └── counter: long         (HOTP only)
```

---

## TotpManager.Web

### Architecture — Stateless Page Model

The web app has **no database and no server-side session**. All account state is held by the browser as a newline-delimited list of `otpauth://` URIs in a hidden form field (`ExistingOtpUris`). Every POST handler receives this field, operates on it, and re-renders the page with an updated list.

This means:
- No persistence layer to manage
- No multi-user concerns
- Closing the tab without backing up = data loss (by design — it's a personal tool)
- A container restart has no effect on backed-up data

### QR Decode Chain

Client-side decoding is preferred because:
1. The browser's native `BarcodeDetector` (Chromium ≥ 83, Edge) handles dense QR codes better than ZXing.Net
2. Images are decoded locally — they never leave the browser
3. Server-side decoding only works on Windows (System.Drawing dependency)

```
Browser receives image file
    │
    ├─ 1. BarcodeDetector API (if available — Chrome/Edge only)
    │       Handles high-density codes well. Uses OS QR engine.
    │
    ├─ 2. jsQR at 5 scales (1.0×, 0.5×, 0.75×, 1.5×, 2.0×)
    │       Pure JS. inversionAttempts: 'attemptBoth'.
    │
    └─ 3. Server-side ZXing (fallback, Windows host only)
            Multi-scale + dual binarizer. Image written to temp file,
            decoded, then deleted.
```

### Client-Side TOTP Generation

OTP secrets are never sent back to the server after the initial decode. The TOTP computation runs entirely in the browser:

- **Secure context** (HTTPS / localhost): native Web Crypto API (`crypto.subtle.importKey` + `crypto.subtle.sign`)
- **Non-secure context** (plain HTTP): jsSHA library fallback

Both use the same RFC 6238 algorithm: HMAC-SHA over an 8-byte big-endian counter (`floor(unix_time / period)`), dynamic truncation to the required digit count.

### ZIP Backup Format

```
otp-qr-codes-YYYYMMDD[-enc].zip
├── accounts.txt        ← newline-delimited otpauth:// URIs (UTF-8)
├── Google - you.png    ← individual QR code PNGs
├── GitHub - you.png
└── ...
```

`accounts.txt` is the primary restore path and works on any platform. QR image decoding during restore is a legacy fallback (Windows only, for ZIPs created before `accounts.txt` was introduced).

When a password is provided, both the PNGs and `accounts.txt` are encrypted with AES-256 (SharpZipLib's `AESKeySize = 256`). The `-enc` suffix on the filename signals that the archive is encrypted.

### Security Headers

Applied by middleware in `Program.cs` to every response:

| Header | Value |
|---|---|
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `no-referrer` |
| `Content-Security-Policy` | `default-src 'self'`; scripts/styles from `cdn.jsdelivr.net`; images from `data:` and `blob:` |

All CDN resources (Bootstrap, jsQR, jsSHA) have `integrity=sha384-...` SRI hashes to guard against CDN compromise.

### `Program.cs` Middleware Order

```
UseForwardedHeaders      ← reads X-Forwarded-For / X-Forwarded-Proto from nginx
Security headers middleware
[UseHsts / UseHttpsRedirection — Development only]
UseRouting
UseAuthorization
MapStaticAssets
MapRazorPages
```

`ForwardedHeaders` has `KnownIPNetworks` and `KnownProxies` cleared to trust any upstream proxy on the local network. Tighten this if the app is ever exposed beyond a trusted LAN.

### Page Handlers

| Handler | Method | Description |
|---|---|---|
| `OnGet` | GET | Initialises empty page |
| `OnPostAddAsync` | POST | Processes url / image / camera / zip input; merges with existing accounts |
| `OnPostRestoreAsync` | POST | Replaces all accounts from uploaded ZIP |
| `OnPostDownloadZip` | POST | Generates and returns ZIP file |
| `OnPostDelete` | POST | Removes one account by URI |
| `OnPostEdit` | POST | Rebuilds URI with new name/issuer |

---

## TotpManager.Tool (CLI)

Windows-only (`net10.0-windows`) batch processor. Useful for one-off exports from a migration URL to individual QR PNG files.

```
TotpManager.Tool [options]

Options:
  --url <migration-url>     otpauth-migration:// or otpauth:// URI
  --image <file>            Image file containing a migration QR code
  --output-dir <dir>        Output directory for PNG files (default: ./output)
  --no-qr                   Print otpauth:// URIs only, skip PNG generation
  --quiet                   Suppress informational output

Exit codes:
  0  Success
  1  Bad arguments
  2  QR decode failed
  3  URL parse failed
```

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0 |
| Docker | Any recent version |
| OS (local run) | Windows (for QR image decode); Linux fine for web-only use |

---

## Building

### Local development

```bash
# Build everything
dotnet build TotpManagerSolution.slnx

# Run the web app (Development mode — hot reload available)
dotnet run --project TotpManager.Web/TotpManager.Web.csproj

# Run the CLI tool
dotnet run --project TotpManager.Tool/TotpManager.Tool.csproj -- --url "otpauth-migration://..."

# Publish the web app (Release)
dotnet publish TotpManager.Web/TotpManager.Web.csproj -c Release -o ./publish
```

The web app binds to `http://localhost:5216` in Development (from `launchSettings.json`).

### Docker image

The Dockerfile is in the solution root. **Build context must be the solution root** so both `TotpManager.Core` and `TotpManager.Web` are in scope.

```bash
# From TotpManagerSolution/
docker build -t totpmanager:latest .

# Run (exposes port 8080)
docker run -d --name totpmanager -p 8080:8080 totpmanager:latest
```

The container runs as `Production` environment. Server-side QR decode is disabled on Linux (the runtime image is Linux-based). Restore via `accounts.txt` in the ZIP still works on Linux.

---

## Deployment — nginx Reverse Proxy

The app is designed to sit behind nginx at a sub-path (e.g. `/totp`). nginx strips the path prefix before forwarding, so no `UsePathBase` is needed in the app.

### nginx location block

```nginx
location /totp/ {
    proxy_pass         http://127.0.0.1:8080/;   # trailing slash strips /totp prefix
    proxy_http_version 1.1;
    proxy_set_header   Host              $host;
    proxy_set_header   X-Forwarded-For  $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_set_header   X-Real-IP        $remote_addr;

    # Allow ZIP uploads up to 20 MB (must match FormOptions limit in Program.cs)
    client_max_body_size 20m;
}
```

> **Note on trailing slashes:** Both `location /totp/` and `proxy_pass http://.../` must have trailing slashes. nginx uses the trailing slash to rewrite the URI, dropping the `/totp` prefix before forwarding. Without them, the full path (including `/totp`) reaches the app and all routes break.

### TLS

TLS is terminated at nginx. The app receives plain HTTP from the proxy. `ASPNETCORE_ENVIRONMENT=Production` skips the in-app HTTPS redirect. The `X-Forwarded-Proto: https` header is trusted (via `UseForwardedHeaders`) so ASP.NET Core knows the original request was HTTPS.

### QNAP NAS deployment

On QNAP, set up the container via Container Station or `docker run` and configure the Virtual Host / reverse proxy in the QNAP web UI to forward `/totp/` to `http://localhost:8080/`. Ensure the QNAP nginx config includes `client_max_body_size 20m` for the upload limit.

---

## Known Platform Differences

| Feature | Windows host | Linux host (Docker) |
|---|---|---|
| Server-side QR image decode | ✅ ZXing.Net + System.Drawing | ❌ Not available |
| Browser-side QR decode | ✅ BarcodeDetector / jsQR | ✅ BarcodeDetector / jsQR |
| ZIP restore via `accounts.txt` | ✅ | ✅ |
| ZIP restore via QR images (legacy) | ✅ | ❌ |
| TOTP generation | ✅ Web Crypto / jsSHA | ✅ Web Crypto / jsSHA |
| TotpManager.Tool | ✅ | ❌ (net10.0-windows) |

Server-side QR decode for image uploads is the only scenario that requires Windows. In practice, browser-side decode (BarcodeDetector / jsQR) handles the majority of cases, including high-density Google Authenticator migration codes.

---

## DataProtection

ASP.NET Core Razor Pages requires DataProtection for antiforgery token signing. Keys are **ephemeral (in-memory)** — no volume mount or key directory is configured. A container restart invalidates any open browser form tokens; the user just needs to refresh the page and resubmit. This is acceptable for a personal tool with no persistent login session.

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("TotpManager");
```

If you need tokens to survive restarts (e.g. shared/team use), add:

```csharp
.PersistKeysToFileSystem(new DirectoryInfo("/app/dp-keys"))
```

and mount a host volume at `/app/dp-keys` in your Docker run command.
