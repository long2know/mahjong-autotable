# Frontend build tooling — Phase K Wave 8

Owner: Hicks (Frontend Engineer)
Status: **Vite is the production bundler (since Phase K Wave 7).**
Wave 8 keeps the `build:parcel` fallback in `package.json` for one
more wave — no W7 deploy regressions surfaced, but the W8 changes
(GLTFLoader chunk peel, SignalR dev proxy) are Vite-only and merit
a second wave of confidence before pruning the fallback. See
`docs/frontend-three-budget.md §4` for the W8 chunking changes.

## TL;DR

```bash
cd src/frontend/autotable-src
npm run build              # alias for build:vite — production build
npm run build:vite         # explicit Vite invocation
npm run build:parcel       # one-wave Parcel fallback (Wave 8 only)
npm run build:post         # SW manifest regen (chained automatically)
```

Outputs land under `src/frontend/autotable/` (one level up from
`autotable-src/`) under the canonical `[name].[hash:8].[ext]`
pattern — identical to Parcel's W6 layout, so the
`scripts/generate-sw-manifest.js` regex set continues to match.

## Why we swapped (Wave 7 rationale)

Wave 6 left `three-renderer.<hash>.js (big)` at **739.72 kB** with
the chunk topology fully optimised within Parcel's tree-shaker
constraints. The blocker was structural:

- `three.module.js` (three.js's public entry) is a giant
  re-export of `three.core.js` — one `export { ... } from './three.core.js'`
  line — and three's `package.json` declares
  `"sideEffects": ["build/three.module.js"]`.
- Parcel honours that side-effect annotation and **disables
  tree-shake on the re-export module**. The whole core is pulled
  even when only ~40 symbols are statically referenced.
- Modest cuts (wildcard `import * as three` removal, `Stats`
  gated behind `?stats=1`, `GLTFLoader` peeled into its own
  chunk) saved ~45 kB on the small sub-chunk but the big chunk
  did not budge. W6 ended at the same 740 kB floor.

Rollup (the bundler that powers Vite) **does** tree-shake the
re-export when you override `sideEffects` for the three module
graph. In W7 we set:

```ts
treeshake: {
  moduleSideEffects: (id) => !id.includes('node_modules/three/'),
}
```

This drops the big chunk from 739.72 kB → **578.72 kB**
(-21.8%, **-160.8 kB**) on the same source graph, plus
**-29.7 kB** on the small sub-chunk (99.1 → 69.3 kB), for a
combined **-22.6%** renderer payload reduction.

Vite also brings:

- Sub-10-second cold builds (Parcel was ~9s; Vite is ~7-8s
  even with terser, ~10s with esbuild).
- Native ESM dev-server (`npm run dev` is now instant — useful
  but not on the production critical path).
- First-class TypeScript handling without Parcel's per-package
  babel hop.
- `manualChunks` API for explicit chunk-name control (Parcel's
  chunk-name policy was opaque; we used `dynamic-import` boundaries
  as the only steering tool).

## Decision matrix considered

| Option | Risk | Expected ∆ | Decision |
|--------|------|-----------|----------|
| **A. Vite swap** | Medium — new tool, learn-curve | -150 to -200 kB on big chunk | **Chosen** |
| B. esbuild swap | Medium-high — fewer plugin escape hatches | -100 to -150 kB | Rejected (no Vite asset-import polish) |
| C. Stay on Parcel + custom plugin | High — Parcel plugin API is unstable | <-50 kB | Rejected (chasing a moving target) |
| D. Hand-roll `three/src/*` imports | Very high — would touch every renderer file | -200 to -300 kB | Rejected (Phase L scope) |

Option A wins on risk/reward: medium risk, large savings, keeps
all renderer source files untouched, and inherits rollup's
mature tree-shake.

## How Vite is configured

The config lives at `src/frontend/autotable-src/vite.config.ts`.
Key decisions:

### 1. Entry + output layout

```ts
build: {
  outDir: '../autotable',
  emptyOutDir: true,
  assetsDir: '.',
  rollupOptions: {
    input: resolve(__dirname, 'index.html'),
    output: {
      entryFileNames: 'autotable-src.[hash:8].js',
      chunkFileNames: chunkFileNamesFn,
      assetFileNames: '[name].[hash:8].[ext]',
      hashCharacters: 'hex',
    },
  },
}
```

`hashCharacters: 'hex'` (Rollup 4+) restores Parcel's
lowercase-hex hash convention — the SW manifest regex set in
`scripts/generate-sw-manifest.js` is built around `[a-f0-9]+`.

### 2. manualChunks (deliberately minimal)

Wave 7 routes **only** `node_modules/*` packages through
`manualChunks`:

```ts
function manualChunks(id: string): string | undefined {
  if (id.includes('node_modules/hls.js/')) return 'hls';
  if (id.includes('node_modules/@sentry/')) return 'sentry';
  if (id.includes('node_modules/three/')) return 'three-renderer';
  return undefined;
}
```

A first-pass version of this config tried to **also** route source
files (`src/world.ts`, `src/types.ts`, `src/asset-loader.ts`,
~25 files) into `three-renderer`. **That broke the W5 lazy-render
split**: any chunk that statically imported (say) `src/types.ts`
would then transitively depend on `three-renderer` at import
time, pulling 580 kB of three.js into the lobby cold path.

Lesson — let rollup walk dynamic-import boundaries on its own.
The `scene-shell.ts` → `import('./three-renderer')` boundary is
the right cut; rollup collapses the static graph behind it into
the renderer chunk naturally.

### 3. Three.js side-effects override

```ts
treeshake: {
  moduleSideEffects: (id) => !id.includes('node_modules/three/'),
}
```

This is the single biggest lever. Three's `sideEffects: ["build/three.module.js"]`
declaration disables tree-shake on the re-export module under
the standard ESM contract; we explicitly tell rollup to ignore
that annotation. **No three.js code is now reachable from the
renderer chunk except the symbols statically imported across
`src/*.ts`.**

(The override is safe because three.js's actual module-load side
effects are confined to small additions to a couple of static
caches — none of the renderer code paths exercise them.)

### 4. Chunk-name disambiguation

Rollup names chunks by their entry module. Several internal
graphs would collide on the name `index` (the `@sentry/browser`
entry, `@microsoft/signalr` entry, etc.). The
`chunkFileNamesFn` callback inspects `facadeModuleId` and remaps:

- `@sentry/browser` → `sentry.<hash>.js`
- `@microsoft/signalr` → `signalr.<hash>.js`
- anything else with a generic `index` name → `chunk.<hash>.js`

### 5. Build-time plugins

Three lightweight plugins compose the post-build pipeline:

1. **`copyStaticAssets()`** — mirrors Parcel's "copy non-imported
   public assets" behaviour for the `manifest.webmanifest` +
   `sw.js` files that the SW pre-cache expects at the dist root.
2. **`runSwManifestScript()`** — runs `scripts/generate-sw-manifest.js`
   on `closeBundle`. Identical to W6's `build:post` chain.
3. **`appendDistSize()`** — runs `scripts/append-dist-size.js` on
   `closeBundle`. Updates the `dist-size.json` ledger Vasquez's
   W7 spec reads.

## Asset import migration (one-time, completed in W7)

Parcel's `url:./img/foo.png` import scheme is non-standard. Vite
uses the `?url` query suffix:

```diff
- import tableUrl from "url:../img/table.jpg";
+ import tableUrl from "../img/table.jpg?url";
```

All asset imports in `src/asset-loader.ts` were migrated; the
behaviour is identical (resolves to the hashed dist URL at build,
to the dev-server URL in dev).

## Service worker integration

The SW pre-cache generator (`scripts/generate-sw-manifest.js`)
runs unchanged from W6 — Vite's output layout matches Parcel's
exactly. The script prunes stale chunks, copies `sw.js`, and
writes `manifest-precache.json` listing the 14-ish stable assets
to pre-warm at install time.

If the chunk-name set changes in a future wave, update the regex
constants near the top of `generate-sw-manifest.js`:
`THREE_RENDERER_RE`, `COMMENTARY_RE`, `HLS_RE`, etc.

## TypeScript strict check

Run on CI + locally:

```bash
cd src/frontend/autotable-src
npx tsc --noEmit --strict --target es6 --module esnext \
  --moduleResolution bundler --types vite/client \
  --lib DOM,DOM.Iterable,es6,es2017 src/*.ts
```

The `--types vite/client` flag pulls Vite's `ImportMeta.env` /
asset-suffix declarations. `--module esnext` is required for
dynamic `import(...)` syntax (TS1323 otherwise).

## Fallback path: `npm run build:parcel` (removed in Wave 10)

Parcel was retained behind `npm run build:parcel` for three waves
(W7–W9) as a safety hatch in case a Vite-induced deploy regression
surfaced. Three consecutive wave bring-ups (gates 1506 / 1706 /
1880) shipped on Vite without invoking the fallback, so Phase K
Wave 10 retires it:

- `package.json#scripts.build:parcel` — **deleted.**
- `devDependencies` — Parcel + its 4 transformers / packagers
  (`parcel`, `@parcel/packager-raw-url`,
  `@parcel/transformer-image`, `@parcel/transformer-webmanifest`)
  **deleted.** `npm ci` no longer downloads ~640 packages (most of
  the saved install time on cold CI runs).
- `package-lock.json` regenerated. The dependency tree is now Vite
  + Lighthouse only.
- `.parcel-cache/` remains in `.gitignore` for one wave (covers a
  contributor who runs the W9-tagged build locally).

If a future wave needs a non-Vite production bundler, the recipe
lives in git history at
`f518196:src/frontend/autotable-src/package.json` (build:parcel
script) and `f518196:src/frontend/autotable-src/Makefile`
(parcel-based release targets, which were already legacy at W7).

`partitionDoubleElim` — the W6 bracket-renderer heuristic that was
retained alongside Bishop's canonical `layout` shape across W7–W9
— is removed in W10 as well. The function was unreferenced by
production code since W9; the dead-code prune ships in
`src/frontend/autotable-src/src/bracket-renderer.ts` (search
"Wave 10 — `partitionDoubleElim` removal" for the rationale
comment).

## §5 — Wave 10: Build cache strategy

`npm run build:vite` was ~28-32 s on a cold cache (full
node_modules walk, dep pre-bundle, three.js source transforms).
Wave 10 wires Vite's disk-persisted cache:

```ts
// vite.config.ts
export default defineConfig({
  root: __dirname,
  cacheDir: resolve(__dirname, '.vite'),
  // …
});
```

- The cache lives at `src/frontend/autotable-src/.vite/`. It
  holds (a) the dep pre-bundle (`node_modules/.vite` equivalent
  pinned next to the source tree) and (b) Vite's transform cache.
- `.gitignore` excludes `.vite/` so nothing leaks into commits.
- The `pwa-audit` GitHub Actions workflow restores the cache via
  `actions/cache@v4` with the key
  `vite-${{ runner.os }}-${{ hashFiles('package-lock.json',
  'vite.config.ts') }}`. Either file changing busts the cache;
  source-only PRs hit a warm cache.

### Measured warmth

| Run                     | Cold cache | Warm cache | Notes                               |
|-------------------------|------------|------------|--------------------------------------|
| `npm run build:vite`    | ~28-32 s   | ~8-12 s    | Local dev machine (M1 Pro reference) |
| GitHub Actions CI       | ~50-65 s   | ~18-25 s   | ubuntu-latest, 4-core / 16 GB        |

The warm-cache speedup is ~3× — comfortable for the `pwa-audit`
workflow's 20-minute budget. If a Lighthouse upgrade or a three.js
bump produces a stale-transform symptom (most often: a build that
succeeds but the renderer chunk is 50+ KB larger than the trend
ledger), wipe with:

```bash
rm -rf src/frontend/autotable-src/.vite
```

…and re-run the build. CI handles this automatically when
`vite.config.ts` or `package-lock.json` change (the hash-of-files
cache key invalidates).

### Build hygiene checklist (Wave 10)

- [ ] `npm ci --no-audit --no-fund` succeeds with no warnings.
- [ ] `npm run build:vite` writes `../autotable/` and exits 0.
- [ ] `../autotable/manifest.webmanifest` exists.
- [ ] `dist-size.json` has a K10 row.
- [ ] `three-renderer-big` shrinks vs the previous wave (W9 was
      507.47 kB; W10 closes at **497.44 kB**).
- [ ] `npm exec lighthouse http://127.0.0.1:4173/` returns five
      LH13 categories.
- [ ] `node scripts/manifest-lint.js --manifest manifest.webmanifest
      --out .pwa-score.json` reports `pwaScore=1.000`.

## Trend ledger (W10 update)

`dist-size.json` is the source-of-truth for chunk-size trend.
Vasquez's W7 Playwright spec reads it and asserts
`three-renderer-big` is monotonically decreasing wave-over-wave.

| Wave | Bundler | three-renderer-big | three-renderer-small | gltf-loader | Total renderer |
|------|---------|-------------------|---------------------|-------------|----------------|
| K5   | Parcel  | 724.7 kB          | 144.9 kB            | (in big)    | 869.6 kB       |
| K6   | Parcel  | 739.72 kB         | 99.1 kB             | (in big)    | 838.8 kB       |
| K7   | Vite    | 578.72 kB ✅      | 69.35 kB ✅         | (in big)    | 648.07 kB ✅   |
| K8   | Vite    | 531.86 kB ✅      | 71.00 kB            | 44.22 kB    | 602.86 kB ✅   |
| K9   | Vite    | 507.47 kB ✅      | 75.38 kB            | 44.22 kB    | 582.85 kB ✅   |
| **K10** | **Vite** | **497.44 kB** ✅ | **75.38 kB**       | **44.22 kB** | **572.82 kB** ✅ |

K7 → K6 delta: **-21.8% on the big chunk, -22.7% renderer total.**
K8 → K7 delta: **-8.1% on the big chunk** via GLTFLoader chunk peel.
K9 → K8 delta: **-4.6% on the big chunk** via three.js feature strip
(see `docs/frontend-three-budget.md §5`).
K10 → K9 delta: **-2.0% on the big chunk** via PMREMGenerator strip
(see `docs/frontend-three-budget.md §6` for the partial-win autopsy
and the W11 candidate list for the remaining shader-string
strippers).

## §3 — Dev-server SignalR + WebSocket proxy (Wave 8)

Before W8, the dev workflow against Bishop's ASP.NET Core backend
was clunky:

- The Changsha SignalR hub needed a URL override
  (`?hub=http://localhost:5000/hubs/changsha`) re-typed on every
  page load (see `hub.ts` pre-W8).
- The Voice hub had no override path — voice testing required a
  full production build served from the same origin as the backend.
- The commentary livestream WebSocket (`/autotable/ws`) was in the
  same boat as voice.

Wave 8 adds a `server.proxy` block to `vite.config.ts` that routes
same-origin requests under `/hubs/*`, `/autotable/ws`, and `/api/*`
from the Vite dev server (default port 5173) to Bishop's backend
at `http://localhost:5000`. `ws: true` enables the HTTP→WebSocket
upgrade dance so SignalR's `wss://` transport survives the hop.

The backend host can be overridden via the `AUTOTABLE_BACKEND`
env var:

```bash
# Default — points at http://localhost:5000:
npm run dev

# Override — point at a remote preview environment:
AUTOTABLE_BACKEND=https://preview-7.autotable.internal npm run dev
```

`hub.ts:hubUrl()` was simplified to always return `/hubs/changsha`
(same-origin) — the dev proxy makes this work without any URL
gymnastics, and the production build co-locates hub + bundle at
the same origin. The legacy `?hub=<url>` override is kept for
contributors pointing at a remote backend without spinning up the
proxy.

## §4 — Wave 8: PWA icon manifest fix (incidental bundler change)

Hidden bug found by the W8 Lighthouse audit: the manifest emitted
by `copyStaticAssets` references the source-tree icon paths
(`img/icon-NNN.auto.png`), but Vite's HTML processor moves all
HTML-referenced icons to the build root with content-hashed names.
The manifest icons all 404'd, breaking the `installable-manifest`
Lighthouse audit (Wave 7 missed this — the audit wasn't re-run
after the Parcel→Vite swap).

Fix: `copyStaticAssets` now also copies the un-hashed PWA icons to
`out/img/icon-NNN.auto.png`. See `docs/frontend-pwa-audit.md §1`
for the audit progression (0.75 → 1.00).

Wave 10 extends `copyStaticAssets` further to copy three new
PWA screenshot placeholders
(`img/screenshot-{lobby,table,mobile}.auto.png`) referenced by
the W10 manifest gap-fills. See `docs/frontend-pwa-audit.md §4`
for the manifest schema delta.
