# TotpManagerSpa

A browser-only TOTP/OTP manager. All cryptographic operations run in the browser — the server does nothing except serve static files. Accounts are held in memory only; they are never sent to the server.

## Features

- Import via migration URL, QR image, live camera, or encrypted ZIP
- Live TOTP codes with countdown bar
- Backup and restore as an encrypted (AES-256) or plain ZIP
- Edit and delete accounts
- Search / filter

---

## Building the Docker image

```bash
# From the solution root
docker build -t totp-spa ./TotpManagerSpa

# Or from inside the TotpManagerSpa folder
docker build -t totp-spa .
```

The image listens on **port 8080** (HTTP only — TLS is terminated by nginx upstream).

---

## Running standalone (no proxy)

```bash
docker run --rm -p 8080:8080 totp-spa
```

Open `http://localhost:8080` in your browser.

---

## Deploying behind nginx at `/totp/`

### 1. Start the container

Set the `PATHBASE` environment variable to match the nginx sub-path:

```bash
docker run -d \
  --name totp-spa \
  --restart unless-stopped \
  -e PATHBASE=/totp \
  -p 127.0.0.1:8080:8080 \
  totp-spa
```

Binding to `127.0.0.1:8080` keeps the container off the public network — only nginx can reach it.

### 2. Docker Compose equivalent

```yaml
services:
  totp-spa:
    image: totp-spa
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    environment:
      - PATHBASE=/totp
```

### 3. nginx configuration

```nginx
server {
    listen 443 ssl;
    server_name example.com;

    ssl_certificate     /etc/ssl/certs/example.com.crt;
    ssl_certificate_key /etc/ssl/private/example.com.key;

    # ── TOTP Manager SPA ──────────────────────────────────────────────────
    location /totp/ {
        proxy_pass         http://127.0.0.1:8080/totp/;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

> **Trailing slash matters.** Both `location /totp/` and `proxy_pass .../totp/` must end with `/` so nginx strips and re-adds the prefix correctly.

After reloading nginx (`nginx -s reload`), the app is available at `https://example.com/totp/`.

---

## Automatic rebuild with Docker Compose Watch

During development, `docker compose watch` rebuilds and restarts the container whenever a source file changes (`.md` files are ignored):

```bash
# From the TotpManagerSpa folder
docker compose watch
```

---

## GitHub Actions CI/CD

The workflow at `.github/workflows/docker-spa-build.yaml` triggers on any push to `main` that modifies a non-`.md` file under `TotpManagerSpa/`:

- **Pull request** — builds the image to verify it compiles; does not push.
- **Push to main** — builds and pushes to GitHub Container Registry:

```
ghcr.io/<your-org>/totp-spa:latest
ghcr.io/<your-org>/totp-spa:sha-<commit>
```

To pull and run the published image:

```bash
docker pull ghcr.io/<your-org>/totp-spa:latest

docker run -d \
  --name totp-spa \
  --restart unless-stopped \
  -e PATHBASE=/totp \
  -p 127.0.0.1:8080:8080 \
  ghcr.io/<your-org>/totp-spa:latest
```

---

## Configuration reference

| Environment variable | Default | Description |
|---|---|---|
| `PATHBASE` | *(empty)* | Sub-path prefix when deployed under a reverse proxy, e.g. `/totp` |
| `ASPNETCORE_URLS` | `http://+:8080` | Kestrel listen address (set in the Dockerfile; rarely needs changing) |
