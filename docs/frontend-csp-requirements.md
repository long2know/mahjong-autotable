# Frontend Content-Security-Policy requirements — Phase K Wave 7

Owner: Hicks (Frontend Engineer)
Status: **Stable as of Phase K Wave 7.** HLS.js vendoring eliminated
the last third-party `script-src` allowance.

## Required policy

```
Content-Security-Policy:
  default-src 'self';
  script-src  'self';
  style-src   'self' 'unsafe-inline';
  img-src     'self' data: blob:;
  media-src   'self' blob: https://*.cloudfront.net;
  connect-src 'self' wss: https://*.sentry.io;
  worker-src  'self' blob:;
  font-src    'self';
  object-src  'none';
  base-uri    'self';
  frame-ancestors 'none';
```

Everything above is what the production deploy ships. Any
relaxation must be reviewed by Hicks + Bishop and recorded in a
W-numbered memo.

## Directive-by-directive rationale

### `script-src 'self'` — **no CDN allowlist needed**

W6 shipped the spectator HLS viewer with a CDN script tag for
HLS.js (`<script src="https://cdn.jsdelivr.net/...">`). That
required `script-src 'self' https://cdn.jsdelivr.net`, which is
a meaningful supply-chain attack surface (any compromise of
jsdelivr's CDN immediately becomes JS execution on
mahjong-autotable.com).

**Wave 7 vendors HLS.js** as a dynamic-imported chunk
(`hls.<hash>.js`, ~286 kB, loaded on user gesture only when
they hit `#spectator-livestream`). The bundle now contains:

```ts
const HlsModule = await import('hls.js/dist/hls.light.mjs');
```

This is a chunk emitted by our own build, served from
`mahjong-autotable.com/autotable/hls.<hash>.js`, content-hashed
and subresource-integrity-friendly. The CSP no longer needs to
allowlist jsdelivr.

The `hls.light.mjs` build is the streaming-only variant
(no DRM / no progressive playback fallback); we don't use either,
so the light build saves ~80 kB vs the full HLS.js bundle.

### `style-src 'self' 'unsafe-inline'`

`'unsafe-inline'` is regrettable but currently required because:

- Sentry's SDK injects inline `<style>` tags for its error-boundary
  overlay in dev. (Doesn't run in prod, but the SDK still emits
  the styles.)
- Vite's HMR runtime injects inline styles for the error overlay
  in dev. (Also dev-only.)

Removing `'unsafe-inline'` is a Phase L scope item — requires
nonce-based CSP per-request. Not blocked on bundler.

### `media-src 'self' blob: https://*.cloudfront.net`

HLS.js fetches `.m3u8` playlists + `.ts` segments. Our HLS
origin is the CloudFront distribution fronting MediaConvert
output. `blob:` is required because HLS.js's `BUFFER_APPENDING`
event path uses `URL.createObjectURL(blob)` for the MediaSource
hand-off.

### `connect-src 'self' wss: https://*.sentry.io`

- `'self'` covers the SignalR hub (same-origin).
- `wss:` covers SignalR's WebSocket upgrade (already same-origin
  but the WebSocket scheme is matched separately by some browsers).
- `*.sentry.io` is the error-reporting endpoint. Inlining /
  self-hosting Sentry's relay is a Phase L+ item.

### `worker-src 'self' blob:`

HLS.js spawns a Web Worker for transmux work using a Blob URL
(its `enableWorker` config — on by default). Removing `blob:`
would force `enableWorker: false`, which doubles transmux latency.

### `object-src 'none'` + `base-uri 'self'` + `frame-ancestors 'none'`

Belt-and-braces hardening. No PDF embeds, no `<base>` overrides,
no clickjacking iframes.

## What W7 removed from the CSP

W6 was tracking a draft addition of `https://cdn.jsdelivr.net` to
`script-src` to support the CDN-loaded HLS.js. **W7's vendoring
work means that addition is no longer needed.** The W7 commit
drops the draft from the CSP rollout plan.

This is a real security win, not just a cosmetic one — every
external `script-src` URL is a separate supply-chain dependency.
Vendoring 286 kB of compiled JS into our own pipeline is a
small bundle cost for a meaningful narrowing of the trust
boundary.

## How to verify a deploy

```bash
curl -sI https://mahjong-autotable.com/ | grep -i content-security
```

Compare against the policy block at the top of this document.
Mismatches should fail-fast on a deploy gate (Apone owns the
nginx config under `src/infra/nginx/`).

## Future tightening (Phase L+ scope)

| Directive | Current | Future target | Blocker |
|-----------|---------|---------------|---------|
| `style-src` | `'self' 'unsafe-inline'` | `'self' 'nonce-<random>'` | Sentry SDK + Vite dev-overlay inline styles |
| `connect-src` | `* sentry.io` | `'self'` only | Self-host Sentry relay |
| `script-src` | `'self'` | `'self' 'strict-dynamic' 'nonce-<random>'` | Per-request nonce injection in nginx |

None of these block W7 shipping. They're enumerated here so
future-Hicks doesn't have to re-derive the path.
