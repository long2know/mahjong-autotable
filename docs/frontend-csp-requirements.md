# Frontend Content-Security-Policy requirements

Owner: Hicks (Frontend Engineer) · Reconciled by Apone (DevOps) in
Phase L WP-G (#121).
Status: **Reconciled to the middleware.** HLS.js vendoring keeps
`script-src` free of any third-party allowance; the optional Sentry
and spectator-HLS features carry narrow `connect-src`/`media-src`
host allowlists.

## Source of truth

The policy is enforced **in-app** by
`src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs`
— there is **no nginx CSP**. The canonical string is the
`SecurityHeadersMiddleware.DefaultCsp` constant. **This document is
reconciled to that constant** — when they disagree, the middleware
wins and this doc is the bug. Production applies two runtime
modifiers on top of the constant:

- `Security:CspStrictStyles=true` (set in `appsettings.Production.json`)
  drops `'unsafe-inline'` from `style-src`.
- `report-uri /api/csp-report` is appended at runtime so browsers POST
  violations to the persisted `CspViolation` sink.

`Security:CspStrict` stays **false** in production — Three.js needs
`'wasm-unsafe-eval'` for its WebAssembly (Draco / KTX) decoders.

## Required policy (effective production)

```
Content-Security-Policy:
  default-src 'self';
  script-src  'self' 'wasm-unsafe-eval';
  style-src   'self';
  img-src     'self' data: blob:;
  media-src   'self' blob: https://*.cloudfront.net;
  font-src    'self' data:;
  connect-src 'self' ws: wss: blob: https://*.sentry.io https://*.cloudfront.net;
  worker-src  'self' blob:;
  frame-ancestors 'none';
  object-src  'none';
  base-uri    'self';
  form-action 'self';
  report-uri  /api/csp-report;
```

Everything above is what the production deploy ships. Any change must
edit `SecurityHeadersMiddleware.DefaultCsp` **first** (reviewed by
Bishop + Hicks), then update this block to match. The
`CspSentryHlsAllowanceTests` + `CspHeaderTests` contracts fail if the
two drift.

## Directive-by-directive rationale

### `script-src 'self' 'wasm-unsafe-eval'` — no CDN allowlist

W6 shipped the spectator HLS viewer with a CDN script tag for
HLS.js (`<script src="https://cdn.jsdelivr.net/...">`). That
required `script-src 'self' https://cdn.jsdelivr.net`, which is
a meaningful supply-chain attack surface (any compromise of
jsdelivr's CDN immediately becomes JS execution on
mahjong-autotable.com).

**Wave 7 vendors HLS.js** as a dynamic-imported chunk
(`hls.<hash>.js`, loaded on user gesture only when they hit
`#/spectate/{tableId}`). The bundle now contains:

```ts
const mod = await import('hls.js/dist/hls.light.mjs');
```

This is a chunk emitted by our own build, served from
`mahjong-autotable.com/autotable/hls.<hash>.js`, content-hashed
and subresource-integrity-friendly. **No external `script-src`
origin is needed.**

`'wasm-unsafe-eval'` is the CSP Level 3 permission that allows
`WebAssembly.compile()` **only** (used by Three.js's optional
Draco / KTX decoders). Per spec it does **not** re-enable
`eval()` / `new Function()`; those remain blocked. It is retained
because `Security:CspStrict` is `false` in production. The shipped
bundle contains zero `eval` / `new Function` callsites (Vasquez's
`CspHeaderTests.DefaultCspConstant_Wave9_HasNoUnsafeEval` locks
`'unsafe-eval'` out).

### `style-src 'self'` (production) / `'self' 'unsafe-inline'` (default)

The `DefaultCsp` constant carries `style-src 'self' 'unsafe-inline'`
because Sentry's SDK and Vite's HMR runtime inject inline `<style>`
tags for their **dev-only** error overlays. Production sets
`Security:CspStrictStyles=true`, which drops `'unsafe-inline'` at
runtime (Hicks's W10 pass migrated every HTML `style="…"` attribute
to a CSS class), so the effective production `style-src` is
`'self'`. Removing the constant's `'unsafe-inline'` entirely is a
future nonce-based item — see the table below.

### `media-src 'self' blob: https://*.cloudfront.net`

The spectator playlist is fetched **same-origin**
(`/api/tables/{tableId}/livestream/playlist.m3u8`, so `'self'`).
Segments live behind the CloudFront distribution fronting the
MediaConvert output:

- **HLS.js (MSE) path** — `blob:` is required for the
  `URL.createObjectURL(MediaSource)` hand-off to the `<audio>`
  element. The `.ts`/`.aac` segment *fetches* go through
  `connect-src` (see below), not `media-src`.
- **Native Safari path** — the browser's own player loads
  segments referenced in the playlist directly, governed by
  `media-src`; hence `https://*.cloudfront.net`.

### `connect-src 'self' ws: wss: blob: https://*.sentry.io https://*.cloudfront.net`

- `'self'` covers the SignalR hub + same-origin `fetch`/XHR.
- `ws:` / `wss:` cover SignalR's WebSocket upgrade — `ws:` for the
  non-TLS local/container origin, `wss:` for the TLS public origin
  (some browsers match the scheme separately).
- `blob:` covers HLS.js worker/`fetch` plumbing that reads from
  Blob URLs.
- `https://*.sentry.io` is the Sentry ingest endpoint
  (`sentry.ts`, DSN-gated — no traffic when no DSN is set). The
  wildcard covers `o###.ingest[.us].sentry.io`. Self-hosting the
  relay to drop this is a future item.
- `https://*.cloudfront.net` is the HLS.js segment origin —
  HLS.js `fetch`es `.m3u8`/`.ts` over `connect-src`.

### `worker-src 'self' blob:`

HLS.js spawns a Web Worker for transmux work using a Blob URL
(its `enableWorker` config — on by default). Removing `blob:`
would force `enableWorker: false`, which doubles transmux latency.

### `object-src 'none'` + `base-uri 'self'` + `frame-ancestors 'none'` + `form-action 'self'`

Belt-and-braces hardening. No PDF embeds, no `<base>` overrides,
no clickjacking iframes, no cross-origin form posts.

## What vendoring removed from the CSP

W6 was tracking a draft addition of `https://cdn.jsdelivr.net` to
`script-src` to support the CDN-loaded HLS.js. **W7's vendoring
work means that addition is no longer needed.** `script-src` carries
no external origin.

This is a real security win, not just a cosmetic one — every
external `script-src` URL is a separate supply-chain dependency.
Vendoring the compiled HLS.js into our own pipeline is a small
bundle cost for a meaningful narrowing of the trust boundary.

## How to verify a deploy

The header is emitted by the .NET app itself (no nginx), so probe
any route the app serves:

```bash
curl -sSI http://127.0.0.1:8080/ | grep -i content-security-policy
```

Compare against the effective-production policy block at the top of
this document. The `CspSentryHlsAllowanceTests` +
`CspHeaderTests` suites fail-fast in CI if the middleware constant
and this doc drift.

## Future tightening (Phase L+ scope)

| Directive | Current (prod) | Future target | Blocker |
|-----------|---------------|---------------|---------|
| `style-src` | `'self'` (constant keeps `'unsafe-inline'` for dev) | `'self' 'nonce-<random>'` | Sentry SDK + Vite dev-overlay inline styles |
| `connect-src` | `'self' ws: wss: blob: *.sentry.io *.cloudfront.net` | drop `*.sentry.io` (self-host relay) | Self-host Sentry relay |
| `script-src` | `'self' 'wasm-unsafe-eval'` | `'self' 'strict-dynamic' 'nonce-<random>'` | Per-request nonce injection + drop WASM decoders |

None of these block shipping. They're enumerated here so
future-Hicks doesn't have to re-derive the path.
