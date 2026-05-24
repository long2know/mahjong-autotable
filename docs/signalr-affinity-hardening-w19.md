# SignalR sticky-session affinity hardening (W19)

> Phase K Wave 19 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W19 sticky-
> session hardening for the SignalR `/hubs/changsha` endpoint.
> Companion to
> [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
> (the W12 sequence-SLO observability surface).

The W7-era nginx-ingress affinity wiring on
`infra/k8s/base/ingress.yaml` already cookie-pinned SignalR
clients to a single backend pod, but the W18 retro flagged
two gaps:

1. The 24h cookie TTL was IMPLICIT — `session-cookie-max-age`
   + `session-cookie-expires` were both set to 86400, but no
   in-file comment explained the magic number or what it was
   pinned to. A future operator could "round to 12h" without
   realising the value matched the SignalR re-handshake window.
2. A client whose `mahjong_aff` cookie was stripped (by a
   corporate proxy, an aggressive browser cookie-clear, or a
   `private` window) lost affinity completely — the next
   request round-robined to a fresh pod, breaking the SignalR
   stream mid-frame.

W19 closes both gaps via:

* Explicit 24h cookie TTL with an in-file comment.
* `Secure` + `SameSite=Lax` cookie attributes (defense-in-
  depth alongside W4 HSTS preload + W7 cert-manager TLS).
* An IP-hash UPSTREAM fallback when the cookie is missing,
  wired via nginx-ingress's `configuration-snippet`
  annotation.

## 1. The W19 hardening shape

The full annotation set on `infra/k8s/base/ingress.yaml`
after W19 (delta from W7-era highlighted):

```yaml
annotations:
  # W7-era — unchanged at W19.
  nginx.ingress.kubernetes.io/affinity:                "cookie"
  nginx.ingress.kubernetes.io/affinity-mode:           "persistent"
  nginx.ingress.kubernetes.io/session-cookie-name:     "mahjong_aff"
  nginx.ingress.kubernetes.io/session-cookie-max-age:  "86400"   # 24h
  nginx.ingress.kubernetes.io/session-cookie-expires:  "86400"   # 24h
  # W19 — cookie-attribute hardening (Secure + SameSite=Lax).
  nginx.ingress.kubernetes.io/session-cookie-secure:   "true"
  nginx.ingress.kubernetes.io/session-cookie-samesite: "Lax"
  # W19 — IP-hash fallback when the cookie is missing.
  nginx.ingress.kubernetes.io/configuration-snippet: |
    set $mahjong_hash_key "$cookie_mahjong_aff";
    if ($mahjong_hash_key = "") {
      set $mahjong_hash_key "$proxy_add_x_forwarded_for";
    }
    add_header X-Mahjong-Affinity-Source "$mahjong_hash_key" always;
```

## 2. The cookie-attribute hardening (`Secure` + `SameSite=Lax`)

### 2.1 `session-cookie-secure: "true"`

Equivalent to setting the `Secure` attribute on the
`Set-Cookie` header. The browser will only return the cookie
on HTTPS requests; a downgrade to HTTP (e.g. a misconfigured
intermediary or an active MITM rewriting `https://` to
`http://`) does not leak the affinity cookie.

This is defense-in-depth alongside:

* The W4 HSTS preload header (clients can't be tricked into
  HTTP in the first place).
* The W7 cert-manager TLS cert (HTTPS is the only valid
  scheme for the production hostname).

### 2.2 `session-cookie-samesite: "Lax"`

Equivalent to setting `SameSite=Lax` on the `Set-Cookie`
header. The cookie is sent on:

* Same-origin requests (the autotable SPA → the SignalR hub).
* Top-level navigations (a user clicking a `https://mahjong.example.com/...`
  link from an external page).

The cookie is NOT sent on:

* Cross-origin subresource requests (a hostile site embedding
  the autotable iframe — the cookie wouldn't accompany the
  hub negotiate so the affinity cookie wouldn't help a
  cross-origin attacker pin to a victim's pod).
* Cross-origin XHR / fetch / form-POST without explicit
  credentials.

`Lax` is the correct baseline for a same-origin SignalR
client (which never makes cross-site requests). `Strict`
would block the cookie even on legitimate top-level navigations
from a deep-link share (Discord, Slack); `Lax` is the W19
sweet-spot.

## 3. The IP-hash UPSTREAM fallback (`configuration-snippet`)

### 3.1 Why a fallback is necessary

Without a fallback, a SignalR client whose `mahjong_aff` cookie
is missing (corporate proxy, browser cookie-clear, private
window, re-issued cookie after a 24h+ idle) round-robins to a
fresh pod on every request. SignalR's negotiate → connect
→ reconnect protocol requires pod-affinity for the entire
session; a mid-stream pod swap returns a `WebSocket protocol
error` to the client and kicks the user off the table.

### 3.2 The snippet

```nginx
set $mahjong_hash_key "$cookie_mahjong_aff";
if ($mahjong_hash_key = "") {
  set $mahjong_hash_key "$proxy_add_x_forwarded_for";
}
add_header X-Mahjong-Affinity-Source "$mahjong_hash_key" always;
```

Flow:

1. `$mahjong_hash_key` is initialised from the inbound
   `mahjong_aff` cookie. If present, the variable carries the
   cookie value (which nginx-ingress's cookie-based affinity
   already hashes to pick a backend).
2. If the cookie is empty, `$mahjong_hash_key` is set to
   `$proxy_add_x_forwarded_for` — the request's `X-Forwarded-
   For` header concatenated with `$remote_addr`. This is the
   most-trusted client IP shape nginx can derive (the W7
   ALB forwards XFF; the ALB target group itself sets
   `$remote_addr` to the ALB IP, so the XFF chain is the
   only client-IP signal).
3. The `add_header X-Mahjong-Affinity-Source` line is purely
   informational — it surfaces which source the affinity
   came from in the response header, so the §5 smoke-test
   runbook can verify the fallback fired without grepping
   the nginx-ingress access log.

### 3.3 Interaction with `affinity: cookie` (the W7-era baseline)

The nginx-ingress's cookie-based affinity (`affinity: cookie`
+ `affinity-mode: persistent`) sits BELOW the snippet in the
nginx Lua chain. With the cookie present, nginx-ingress's
own balancer picks a backend using the cookie value. With the
cookie ABSENT, the snippet runs first and `$mahjong_hash_key`
carries the XFF chain into the request scope; nginx-ingress
sets a fresh `mahjong_aff` cookie on the response (per the
W7 `affinity-mode: persistent` annotation), and subsequent
requests use the cookie-based affinity.

In effect: the fallback only fires on the FIRST request of a
new client, OR on requests from a client whose cookie was
stripped (in which case the fallback hashes to the same
backend as long as the client's IP doesn't change). After
the response sets the cookie, the W7-era cookie affinity
takes over.

### 3.4 Why not `upstream-hash-by`?

`nginx.ingress.kubernetes.io/upstream-hash-by` would replace
the cookie-based affinity ENTIRELY with an IP-hash balancer.
The W19 design wants BOTH:

* Cookie-based affinity is the PRIMARY signal (survives a
  client-IP change — mobile-data → wifi, CGN re-assignment).
* IP-hash is the FALLBACK only — fires when the cookie is
  missing.

`upstream-hash-by` is exclusive with `affinity: cookie` and
would break the cookie-based mobile/CGN path. The
`configuration-snippet` shape gives us both.

## 4. The W7 → W19 behaviour matrix

| Scenario                                      | W7 behaviour      | W19 behaviour |
| --------------------------------------------- | ----------------- | ------------- |
| Client with cookie, same IP                   | ✅ Pinned         | ✅ Pinned     |
| Client with cookie, IP changed                | ✅ Pinned         | ✅ Pinned     |
| Client cookie stripped, same IP               | ❌ Round-robin    | ✅ Pinned via IP hash |
| Client cookie stripped, IP changed            | ❌ Round-robin    | ❌ Round-robin (but cookie re-set on response) |
| Hostile site embeds autotable iframe          | ⚠ Cookie sent    | ✅ Cookie blocked (SameSite=Lax) |
| HTTP downgrade attempt                        | ⚠ Cookie sent    | ✅ Cookie blocked (Secure) |

## 5. Validation

### 5.1 `kustomize build` exit code

```bash
kustomize build infra/k8s/overlays/prod/ > /tmp/prod-render.yaml
kustomize build infra/k8s/overlays/staging/ > /tmp/staging-render.yaml
echo "Exit codes: prod=$?, staging=$?"
# Expect both to exit 0.
```

Verified at W19 land: both overlays render exit-0.

### 5.2 `kubectl apply --dry-run=server` (cluster-side)

The W19 brief asks for `kubectl apply --dry-run=server`
against a live cluster. The runbook below assumes the
operator's kubeconfig points at staging:

```bash
# Render the staging overlay and dry-run-apply against the
# staging cluster. Server-side dry-run validates the
# annotations against the running nginx-ingress
# IngressClass admission webhook.
kustomize build infra/k8s/overlays/staging/ \
    | kubectl apply --dry-run=server -f -
```

A successful dry-run prints:

```
ingress.networking.k8s.io/staging-mahjong-autotable configured (server dry run)
```

(plus the other resources the staging overlay materialises).
The W19 hardening surface — the four new annotations — is
validated by the dry-run; nginx-ingress's CRD-side webhook
will reject any malformed `configuration-snippet`.

### 5.3 Smoke test — IP-hash fallback fires when cookie absent

After the W19 hardening lands on staging, issue two requests
to the staging hub from a fresh client (no `mahjong_aff`
cookie set):

```bash
# Request 1 — cookie absent. Expect X-Mahjong-Affinity-Source
# header to surface the XFF chain.
curl -i -H "Host: staging.mahjong.example.com" \
    https://staging.mahjong.example.com/healthz \
    | grep -i 'x-mahjong-affinity-source\|set-cookie'
# Expect:
#   X-Mahjong-Affinity-Source: <client-IP>
#   Set-Cookie: mahjong_aff=<value>; ... Secure; SameSite=Lax

# Request 2 — cookie present. Expect X-Mahjong-Affinity-Source
# header to surface the cookie value.
curl -i -H "Host: staging.mahjong.example.com" \
    -H "Cookie: mahjong_aff=abc123" \
    https://staging.mahjong.example.com/healthz \
    | grep -i 'x-mahjong-affinity-source'
# Expect:
#   X-Mahjong-Affinity-Source: abc123
```

## 6. Rollback

Revert the W19 commit; the W7-era three-annotation cookie
shape is restored. No data path is affected — the cookie's
absence after rollback simply restores the W7 behaviour
(round-robin on cookie-strip) without breaking active
SignalR connections (existing cookies remain valid until
their 86400-second TTL expires).

## 7. Cross-references

- [`infra/k8s/base/ingress.yaml`](../infra/k8s/base/ingress.yaml)
  — W19 ingress source (the file the §1 annotation set
  lands on).
- [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
  — W12 SignalR sequence-SLO observability surface.
- [`docs/hsts-preload.md`](./hsts-preload.md) — W4 HSTS
  preload header (defense-in-depth alongside the W19
  `Secure` cookie attribute).
- nginx-ingress upstream docs §"Sticky-Session" —
  <https://kubernetes.github.io/ingress-nginx/user-guide/nginx-configuration/annotations/#cookie-affinity>
