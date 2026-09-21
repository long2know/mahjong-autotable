# Reverse-proxy deployment

Phase J Wave 6 (Apone, DevOps). Operator-facing guide to fronting the
`mahjong-autotable` container with a TLS-terminating reverse proxy on
a Linux host.

The container listens on plain HTTP at port `8080` by design — TLS is a
deployment concern, not an application concern, so the same image can
sit behind nginx, Caddy, Traefik, an AWS ALB, or whatever else the
operator already runs. This document documents the two most common
setups (nginx + Caddy) with ready-to-copy sample configs in
[`infra/`](../infra/).

## Why a reverse proxy

The 11-item launch checklist needs three things from the deployment
front:

1. **TLS termination + automatic certificate renewal.** A self-hosted
   Linux box doesn't get certificates for free — nginx with certbot,
   or Caddy with its native ACME client, solves this without bolting
   anything into the .NET runtime.
2. **WebSocket upgrade fidelity for SignalR + the raw `/autotable/ws`
   transport.** Both transports need `Connection: Upgrade /
   Upgrade: websocket` to flow through and a multi-hour
   `proxy_read_timeout` so the proxy doesn't tear down idle long-poll
   connections.
3. **Real client IPs.** The Phase J Wave 6 rate limiter partitions by
   IP (see `RateLimitingExtensions.ResolvePartitionKey` and
   [`docs/deployment.md` § "Rate limiting"](deployment.md#rate-limiting)).
   When a proxy is in front, Kestrel sees the proxy's IP unless
   `X-Forwarded-For` is forwarded — `ResolvePartitionKey` honors XFF
   when present, so as long as the proxy sets it correctly the partition
   key matches reality.

## Sample configs

| Proxy   | Sample file                                                 | Notes                              |
| ------- | ----------------------------------------------------------- | ---------------------------------- |
| nginx   | [`infra/nginx/mahjong.conf.example`](../infra/nginx/mahjong.conf.example)   | TLS + WebSocket upgrade locations + 24-hour read timeout |
| Caddy   | [`infra/caddy/Caddyfile.example`](../infra/caddy/Caddyfile.example)         | Automatic TLS via Let's Encrypt; WS upgrade is implicit  |

The Nginx samples target the default Compose host publication
`127.0.0.1:8950`; container-internal8080 does not change. The existing
Caddy example uses an explicit8080 upstream: change it to8950 (or your
chosen `MAHJONG_HOST_PORT`). Adjust the public hostname for your deployment.

For an existing Nginx/TLS installation, start with the
[HTTP/WebSocket location example](../infra/nginx/mahjong-http.conf.example)
and place its location inside your existing server. Keep its map at
http{} scope, preserve paths by omitting a URI suffix from `proxy_pass`,
and leave your certificates untouched. The edge example overwrites XFF;
only retain an incoming proxy chain when every preceding proxy is trusted.
Set `MAHJONG_HOST_PORT=8951` for a separate local instance and point that
instance's upstream at8951, not at the primary8950 server.

## nginx — quick start

```bash
# Copy the sample.
sudo install -m 0644 infra/nginx/mahjong.conf.example \
    /etc/nginx/sites-available/mahjong.conf

# Edit the server_name, ssl_certificate, and proxy_pass values.
sudo $EDITOR /etc/nginx/sites-available/mahjong.conf

# Enable + reload.
sudo ln -s /etc/nginx/sites-available/mahjong.conf \
          /etc/nginx/sites-enabled/mahjong.conf
sudo nginx -t
sudo systemctl reload nginx
```

The sample wires three `location` blocks:

| Location          | Purpose                              | Read timeout |
| ----------------- | ------------------------------------ | ------------ |
| `/`               | Static assets + REST API             | 60s          |
| `/hubs/`          | SignalR (long-lived)                 | 24 h         |
| `/autotable/ws`   | Raw WebSocket (long-lived)           | 24 h         |

Set up TLS with certbot:

```bash
sudo certbot --nginx -d mahjong.example.com
```

Certbot updates the `ssl_certificate*` lines and wires a renewal timer
into systemd automatically.

## Caddy — quick start

```bash
sudo install -m 0644 infra/caddy/Caddyfile.example /etc/caddy/Caddyfile
sudo $EDITOR /etc/caddy/Caddyfile          # update the hostname
sudo systemctl reload caddy
```

Caddy auto-provisions the certificate at first request — no manual
ACME setup. The sample also writes a rotated JSON access log under
`/var/log/caddy/`.

## Forwarded headers in the app

The .NET app does not yet enable general
`Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions`.
The Phase J Wave 6 rate-limiter falls back to `X-Forwarded-For` on its
own (see `RateLimitingExtensions.ResolvePartitionKey`), so the
partition key matches the real client even without that middleware
enabled.

This does not make `Request.Scheme` or every `RemoteIpAddress` consumer
proxy-aware. HTTPS authentication redirects/cookies and trusted-proxy
configuration require a separately reviewed application change and
deployment-specific trusted proxy addresses. HTTP/static/WebSocket proxy
smoke results must not be presented as proof of that wider TLS/auth surface.
Do not enable unrestricted forwarded-header trust as a workaround.

## Related docs

- [`docs/deployment.md`](deployment.md) — base Docker deploy + env vars
- [`docs/systemd.md`](systemd.md) — wrap the `docker run` line in a unit
- [`docs/log-rotation.md`](log-rotation.md) — keep `docker logs` bounded
- [`docs/secrets.md`](secrets.md) — `Cors__AllowedOrigins__0` belongs in the secret store
- [`docs/observability.md`](observability.md) — gating `/metrics` behind proxy auth
