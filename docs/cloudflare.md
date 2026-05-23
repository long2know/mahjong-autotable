# Cloudflare (CDN + edge security)

Phase J Wave 8 (Apone, DevOps) operator-facing guide for fronting
`mahjong-autotable` with Cloudflare. The backend ships a
**CDN-aware cache + security-header middleware**
(`Mahjong.Autotable.Api.Observability.SecurityHeadersMiddleware`)
that is correct for any reverse proxy, but the recommendations in
this doc are specifically tuned for Cloudflare's free / pro plans.

## Why Cloudflare

The Parcel-built SPA is ~9 MB unminified, ~2.5 MB gzipped. The
backend serves it from `wwwroot` directly on the origin, which is
fine but means every cold tab hits the origin's bandwidth. Cloudflare:

- caches the hashed bundle files (`autotable-src.<hash>.js` etc.)
  indefinitely (`Cache-Control: public, max-age=31536000, immutable`),
- terminates TLS at the edge so the origin can run plain HTTP behind
  a private network (Tailscale, k8s ClusterIP, etc.),
- rate-limits abusive traffic before it touches the .NET runtime,
- gives operators DDoS protection that the application itself does
  not need to implement.

## DNS + origin setup

1. **DNS-only first.** Add an `A` / `AAAA` record for the origin
     **with the orange cloud OFF** (DNS-only). Confirm `dig +short`
     resolves to your origin and that `curl -I https://example.com/health`
     returns 200.
2. **Origin pull.** Issue an origin certificate from Cloudflare
     (Dashboard → SSL/TLS → Origin Server → Create Certificate). Use
     a 15-year validity and ECDSA. Mount the cert on the origin reverse
     proxy (Caddy / nginx / Traefik). The application itself listens
     on plain HTTP (`http://+:8080`); TLS is terminated by the reverse
     proxy.
3. **Flip to "Full (Strict)".** Dashboard → SSL/TLS → Overview →
     **Full (strict)**. Cloudflare will now refuse to talk to the
     origin unless the cert chain validates. Never use "Flexible" —
     it leaves the origin → CF hop in cleartext.
4. **Flip the orange cloud on.** The record is now proxied. Verify
     `curl -I https://example.com/health` still returns 200 and the
     response has a `CF-Ray` header.

## CF-Connecting-IP and rate limiting

When traffic is proxied through Cloudflare, the origin sees
Cloudflare IPs as the remote address. The rate limiter in
`RateLimiting/RateLimitingExtensions.cs` is Cloudflare-aware:

```csharp
private static string ResolvePartitionKey(HttpContext ctx) {
        // Wave 8 — Cloudflare puts the real client IP in this header.
        var cf = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cf)) return cf;
        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff)) return xff.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
```

`CF-Connecting-IP` is *only* set by Cloudflare and *only* when the
request actually traversed Cloudflare's edge. If you need a hard
guarantee, either:

- restrict the origin firewall to Cloudflare's published IP ranges
    (https://www.cloudflare.com/ips/), OR
- add an `Authenticated Origin Pulls` mTLS cert at the reverse proxy.

Without one of these, an attacker can spoof `CF-Connecting-IP` by
hitting the origin directly. **Apply at least one of the two before
trusting `CF-Connecting-IP` in production.**

## Page rules / cache rules

Cloudflare's default cache eligibility ignores `Cache-Control` from
the origin unless the request matches a built-in cacheable extension
(`*.js`, `*.css`, `*.png`, …). Our hashed bundles match those by
filename, so they're cacheable without any extra rule.

For the SPA index page, we want **no edge caching**. The origin
already sets `Cache-Control: no-cache, must-revalidate` on the HTML
shell — Cloudflare honours this and revalidates. No rule needed.

For the API surface (`/api/*`, `/health`, `/metrics`, `/changshaHub`),
we want **bypass cache entirely**. Add a Cache Rule:

| Match | Action |
|---|---|
| `(http.request.uri.path matches "^/api/.*" or http.request.uri.path eq "/health" or http.request.uri.path eq "/metrics" or http.request.uri.path eq "/changshaHub")` | **Bypass cache** |

Why a rule rather than relying on `Cache-Control: no-store`? Because
Cloudflare's free plan respects `no-store` for **GET** but not for
**POST** — and the SignalR negotiate / send endpoints are POST. A
bypass-cache rule is the unambiguous answer.

## Security headers

The origin already stamps:

| Header | Value | Notes |
|---|---|---|
| `X-Frame-Options` | `DENY` | No clickjacking; SPA never iframes itself. |
| `X-Content-Type-Options` | `nosniff` | Standard. |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Avoids leaking auth callback URLs. |
| `Content-Security-Policy` | see below | Permissive but `'unsafe-inline'`-free. |

The CSP is intentionally permissive on `script-src` because Three.js
generates shader programs at runtime via `new Function(...)`:

```
default-src 'self';
script-src 'self' 'unsafe-eval';
style-src 'self' 'unsafe-inline';
img-src 'self' data: blob:;
font-src 'self' data:;
media-src 'self' data: blob:;
connect-src 'self' wss: https:;
worker-src 'self' blob:;
object-src 'none';
base-uri 'self';
frame-ancestors 'none';
form-action 'self';
upgrade-insecure-requests
```

To override (e.g., to allow Sentry's CDN), set `Security:ContentSecurityPolicy`
in `appsettings.json` or via the `Security__ContentSecurityPolicy`
env var. The override **replaces** the whole policy — there is no
merge.

`'unsafe-inline'` on `style-src` is required because Bootstrap 4's
tooltips inject inline styles. Switching to Bootstrap 5 (Wave 9
candidate) will let us drop it.

> **HSTS.** The middleware deliberately does NOT set
> `Strict-Transport-Security`. Toggle it on at the Cloudflare layer
> instead (Dashboard → SSL/TLS → Edge Certificates → HSTS) so the
> setting can be unwound from the dashboard if anything goes wrong;
> a header from the origin is much harder to retract.

## Edge rate limiting

The application rate limiter (`Wave 6`) handles per-IP quotas at the
hot path:

| Policy | Token bucket | Window | Scope |
|---|---|---|---|
| `fixed-window-anonymous` | 10 req / min | per IP | anonymous endpoints |
| `token-bucket-api` | 30 burst, 5 rps replenish | per IP | `/api/*` |

For a public deployment, add a Cloudflare rate limit *in front* of
these so abusive traffic is rejected at the edge before consuming
.NET threads:

| Path | Threshold | Period | Action |
|---|---|---|---|
| `/api/identity` | 30 req | 60 s | Block (1 min) |
| `/api/auth/*` | 20 req | 60 s | Block (5 min) |
| `/changshaHub/negotiate` | 30 req | 60 s | Block (1 min) |

These are upper bounds — well above legitimate per-tab traffic but
low enough to slow a brute force.

## Performance: cache analytics

Once Cloudflare is in front:

- Dashboard → Analytics → Caching → expect **>95% Cache HIT rate
    on bundles** (`/autotable-src.<hash>.js` etc.). If you see <80%,
    the immutable headers are likely being stripped — check page
    rules.
- Dashboard → Analytics → Performance → expect <50 ms TTFB on cached
    requests, ~200 ms on origin-pulls.

## Disaster recovery

- **Origin overload.** Enable "Under Attack Mode" (Security →
    Overview). All traffic gets a 5s JS challenge. Returns to normal
    once removed.
- **Bad config?** Cloudflare's "Pause Cloudflare on Site" link
    (Overview → Advanced Actions) is the kill switch. Traffic goes
    direct to the origin DNS within ~30s of TTL expiry.
- **DNS misroute.** Cloudflare caches CNAME / A records aggressively.
    Force a flush via Dashboard → Caching → Configuration → Purge
    Cache → Purge Everything.

## See also

- `docs/observability.md` — `/health` + `/metrics` semantics
- `docs/sentry.md` — error reporting; Sentry traffic should also be
    bypass-cache
- `docs/secret-management.md` — keeping the Cloudflare API token out
    of the repo
- `RateLimiting/RateLimitingExtensions.cs` — code-level limiter
