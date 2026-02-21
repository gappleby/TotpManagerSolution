# TotpManager — Application Requirements

This document captures the original requirements used to build the TotpManagerSolution in Claude. It is intended to allow the project to be fully reconstructed from scratch if needed.

---

## 1. Overview

Build a self-hosted web application that:
- Imports OTP (One-Time Password) accounts from Google Authenticator migration QR codes and standard `otpauth://` URIs
- Displays live, auto-refreshing TOTP codes for each account
- Allows accounts to be edited, deleted, and searched
- Allows accounts to be exported as a password-protected ZIP backup and restored from that backup
- Runs as a Docker container behind an nginx reverse proxy

---

## 2. Solution Structure

Create a .NET 10 solution (`TotpManagerSolution.slnx`, modern format) containing three projects:

| Project | Type | TFM |
|---|---|---|
| `TotpManager.Core` | Class library | `net10.0` |
| `TotpManager.Tool` | Console application | `net10.0-windows` |
| `TotpManager.Web` | ASP.NET Core Razor Pages | `net10.0` |

---

## 3. TotpManager.Core Requirements

### 3.1 NuGet Packages
- `ZXing.Net 0.16.9` — QR code image decoding
- `QRCoder 1.6.0` — QR code image generation
- `System.Drawing.Common 9.0.2` — bitmap access for ZXing (Windows only)

### 3.2 Models (`OtpParameters.cs`)
Define the following types mirroring the Google Authenticator protobuf schema:

```
enum Algorithm    { Unspecified, SHA1, SHA256, SHA512, MD5 }
enum DigitCount   { Unspecified, Six, Eight }
enum OtpType      { Unspecified, HOTP, TOTP }

class OtpParameters
    byte[]     Secret
    string     Name
    string     Issuer
    Algorithm  Algorithm
    DigitCount Digits
    OtpType    Type
    long       Counter   // HOTP only
    int        Id

class MigrationPayload
    List<OtpParameters>  OtpParameters
    int                  Version
    int                  BatchSize
    int                  BatchIndex
    int                  BatchId
```

### 3.3 `ProtobufDecoder`
- Hand-roll a proto3 wire-format decoder — do NOT use Grpc.Tools (unreliable in non-SDK projects)
- Implement a `ref struct ProtoReader` for zero-allocation, stack-based parsing
- Support wire types: 0 (varint), 1 (64-bit), 2 (length-delimited), 5 (32-bit)
- Expose `DecodePayload(byte[])` → `MigrationPayload`
- Expose `DecodeOtpParameters(byte[])` → `OtpParameters`

### 3.4 `MigrationParser`
- Accept `otpauth-migration://offline?data=<base64>` URLs
- Restore URL-encoded `+` characters (decoded as spaces) before base64 decoding
- Delegate to `ProtobufDecoder`
- Expose `Parse(string migrationUrl)` → `MigrationPayload`

### 3.5 `OtpAuthBuilder`
- Convert `OtpParameters` → standard `otpauth://[totp|hotp]/[issuer:]name?secret=...` URI
- Implement RFC 4648 base32 encoding (no padding)
- Omit default parameter values from the URI (SHA1, 6 digits, period=30)
- Expose `BuildUri(OtpParameters)` → `string`

### 3.6 `QrCodeDecoder`
- Mark `[SupportedOSPlatform("windows")]`
- Use `BarcodeReaderGeneric` (not the non-generic `BarcodeReader`) with `BitmapFormat.BGRA32`
- Extract pixel data via `LockBits`, stripping stride padding row-by-row
- Try five scales: 0.5×, 0.75×, 1.0×, 1.5×, 2.0×
- Try two binarizers per scale: `HybridBinarizer` (photos) and `GlobalHistogramBinarizer` (clean digital images)
- Expose `DecodeQrFromFile(string path)` → `string` (throws on failure)

### 3.7 `QrCodeGenerator`
- Use QRCoder with ECC level Q and 10 pixels per module
- Expose `GenerateQrPng(string uri, int pixelsPerModule = 10)` → `byte[]`

---

## 4. TotpManager.Tool Requirements

### 4.1 Purpose
A Windows-only CLI for batch processing migration URLs or images into individual QR PNG files.

### 4.2 Command-line Interface
```
--url <migration-url>     Accept otpauth-migration:// or otpauth:// URI
--image <file>            Decode migration URL from a QR image file
--output-dir <dir>        Output directory (default: ./output)
--no-qr                   Print otpauth:// URIs only; skip PNG generation
--quiet                   Suppress informational output
```

### 4.3 Exit Codes
- `0` — success
- `1` — bad arguments
- `2` — QR image decode failed
- `3` — migration URL parse failed

### 4.4 Behaviour
- Sanitise filenames (replace invalid characters with `_`)
- Avoid filename collisions by appending ` (n)` suffixes

---

## 5. TotpManager.Web Requirements

### 5.1 NuGet Packages
- `SharpZipLib 1.4.2` — ZIP creation and reading, AES-256 encryption

### 5.2 Design Philosophy
- **Stateless** — no database, no server-side session
- Account state is held entirely in the browser as a newline-delimited list of `otpauth://` URIs in a hidden form field (`ExistingOtpUris`)
- Closing the tab without backing up loses data — this is by design

### 5.3 Input Modes
The Add panel must support four modes:

| Mode | Description |
|---|---|
| `url` | Paste `otpauth-migration://` or one or more `otpauth://` URIs |
| `image` | Upload an image file; decode client-side first, server fallback |
| `camera` | Live camera scan using jsQR at 250 ms intervals |
| `zip` | Upload a previously exported backup ZIP |

### 5.4 QR Decode Priority (image and camera modes)
1. Browser `BarcodeDetector` API (Chrome/Edge — handles dense codes; check `'BarcodeDetector' in window` first)
2. jsQR at multiple scales (`inversionAttempts: 'attemptBoth'`) — scales: 1.0, 0.5, 0.75, 1.5, 2.0
3. Server-side `QrCodeDecoder` (Windows only; Linux must skip with a clear error message)

### 5.5 Account Cards
Each account displays as a Bootstrap card:
- Bold issuer name; smaller account name below
- For TOTP accounts: live 6/8-digit code updated every second, grouped in the middle (e.g. `482 917`)
- Progress bar + seconds countdown: blue → yellow (≤ 10 s) → red (≤ 5 s)
- Copy TOTP code button (📋) — writes current digits to clipboard
- Gear button (⚙️) — shows QR image, type/algorithm/digits info, Copy URI button, PNG download link
- Edit button (✏️) — inline form to rename issuer and account name
- Delete button (🗑️) — requires confirmation

### 5.6 Client-Side TOTP Generation
- Secrets must NOT be sent back to the server after initial decode
- Use `crypto.subtle` (Web Crypto API) in secure contexts (HTTPS / localhost)
- Fall back to jsSHA 3.3.1 in non-secure contexts (plain HTTP)
- Implement RFC 6238: HMAC-SHA over 8-byte big-endian counter, dynamic truncation

### 5.7 Account Management
- **Merge logic** — deduplication key: `type|issuer|name` (case-insensitive); incoming accounts replace existing matches in-place, new ones are appended
- **Edit** — rebuild the `otpauth://` URI with new name/issuer; preserve all other query parameters
- **Delete** — remove by URI equality from the `ExistingOtpUris` list
- **Search** — client-side filter on `issuer + " " + name`; show/hide cards in real time

### 5.8 ZIP Backup Format
```
otp-qr-codes-YYYYMMDD.zip          (unencrypted)
otp-qr-codes-YYYYMMDD-enc.zip      (AES-256 encrypted)
├── accounts.txt                    ← primary restore path; UTF-8; one otpauth:// URI per line
├── <Issuer - Name>.png             ← individual QR code PNGs
└── ...
```
- Filename collisions resolved by appending ` (n)` before the extension
- Both PNGs and `accounts.txt` share the same password and AES-256 key size when encrypted

### 5.9 ZIP Restore
- Prefer `accounts.txt` over QR image decode (cross-platform; Linux containers cannot use System.Drawing)
- Fall back to QR image decode only on Windows, only when `accounts.txt` is absent
- Restore replaces all currently shown accounts

### 5.10 Archive Password UX
- Single shared password field used for both backup and restore
- Password is persisted in browser `localStorage` across sessions
- Server signals JS to clear `localStorage` when a restore succeeds on an unencrypted ZIP (`RestoredWithoutPassword` flag)

### 5.11 Upload Limits
- Maximum file size: **20 MB** for all file inputs (QR image, ZIP upload, restore ZIP)
- Enforced at two levels:
  1. `FormOptions.MultipartBodyLengthLimit` in `Program.cs` (framework layer)
  2. Explicit `IFormFile.Length` check in each handler (application layer)

### 5.12 Security Requirements
- Anti-forgery tokens on all POST forms (ASP.NET Core default — enabled by `AddRazorPages`)
- No `@Html.Raw()` output of user-controllable data; use `data-` attributes for values that need to reach JavaScript
- Validate ZIP entry names before processing: reject entries containing `..`, rooted paths, or null bytes
- Log full exception details server-side (`ILogger<IndexModel>`); return generic messages to the browser
- Security headers on every response: `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Content-Security-Policy`
- SRI `integrity=sha384-...` hashes on all CDN `<script>` and `<link>` tags

### 5.13 `Program.cs` Configuration
- `AddRazorPages()`
- `FormOptions.MultipartBodyLengthLimit = 20 MB`
- `AddDataProtection().SetApplicationName("TotpManager")` — ephemeral in-memory keys (no file persistence needed)
- `ForwardedHeadersOptions` — trust `X-Forwarded-For` and `X-Forwarded-Proto` from any upstream proxy (clear `KnownIPNetworks` and `KnownProxies`)
- Security headers middleware (before routing)
- `UseHsts` / `UseHttpsRedirection` in Development only (TLS terminated at nginx in production)

---

## 6. Docker Requirements

### 6.1 Multi-Stage Dockerfile (in solution root)
```
Stage 1 — build:    mcr.microsoft.com/dotnet/sdk:10.0
Stage 2 — runtime:  mcr.microsoft.com/dotnet/aspnet:10.0
```
- Copy `.csproj` files first (restore layer) then source files (compile layer) to maximise layer cache hits
- Publish in Release mode with `--no-restore`
- Expose port `8080`; set `ASPNETCORE_URLS=http://+:8080` and `ASPNETCORE_ENVIRONMENT=Production`

### 6.2 Build Context
Build context must be the **solution root** (not the Web project folder) so `TotpManager.Core` source is available.

```bash
docker build -t totpmanager:latest .
docker run -d --name totpmanager -p 8080:8080 totpmanager:latest
```

---

## 7. nginx Reverse Proxy Requirements

- App is served under a sub-path, e.g. `/totp/`
- nginx strips the path prefix before forwarding (trailing slash on both `location` and `proxy_pass`)
- nginx must forward `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Real-IP`
- `client_max_body_size` must be set to at least **20m** (matching the app's upload limit)

```nginx
location /totp/ {
    proxy_pass          http://127.0.0.1:8080/;
    proxy_http_version  1.1;
    proxy_set_header    Host              $host;
    proxy_set_header    X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header    X-Forwarded-Proto $scheme;
    proxy_set_header    X-Real-IP         $remote_addr;
    client_max_body_size 20m;
}
```

---

## 8. Frontend Dependencies (CDN with SRI)

| Library | Version | Usage |
|---|---|---|
| Bootstrap | 5.3.3 | Layout, cards, buttons, forms |
| jsQR | 1.4.0 | Client-side QR decoding from image/camera |
| jsSHA | 3.3.1 | HMAC-SHA for TOTP in non-secure HTTP contexts |

All three must be loaded from `https://cdn.jsdelivr.net/npm/` with verified `integrity=sha384-...` SRI hashes and `crossorigin="anonymous"`.

---

## 9. Platform Constraints

| Feature | Windows | Linux (Docker) |
|---|---|---|
| Server-side QR image decode | Required (`System.Drawing.Common`) | Not available — skip gracefully |
| ZIP restore via `accounts.txt` | ✅ | ✅ |
| ZIP restore via QR image decode | ✅ (legacy fallback) | ❌ |
| Browser QR decode | ✅ | ✅ |
| Client-side TOTP | ✅ | ✅ |
| TotpManager.Tool | ✅ | ❌ (`net10.0-windows`) |
