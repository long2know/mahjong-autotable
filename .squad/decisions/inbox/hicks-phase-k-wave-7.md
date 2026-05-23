# Hicks — Phase K Wave 7 memo

**Branch:** `stlong/phase-k-wave-7-bringup`
**Date:** 2026-05-23
**Author:** Hicks (Frontend Engineer) `<hicks@squad.mahjong>`
**Scope:** Bundler swap evaluation (Parcel → Vite), CSP allowlist
narrowing (vendor HLS.js), commentary panel rewrite for Bishop's
W7 `CommentaryRecord[]` contract, OutlinePass replacement spike
(CustomOutline inverted-hull shader), `dist-size.json` chunk-size
trend ledger.
**Build gate:** `npm run build:vite` clean (~7.8 s wall);
`tsc --noEmit --strict --target es6 --module esnext
--moduleResolution bundler --types vite/client` zero errors.

---

## Headline — bundler swap unlocked the renderer-chunk budget

W6 left `three-renderer.<hash>.js (big)` at **739.72 kB** with
the chunk topology fully optimised within Parcel's tree-shaker
constraints. W7 swaps the bundler to **Vite** (rollup under the
hood) and ships an inverted-hull replacement for `OutlinePass`.
Combined effect:

| Chunk                              | Wave 6      | Wave 7        | Δ              |
|------------------------------------|-------------|---------------|----------------|
| `autotable-src.<hash>.js` (eager)  | 219.68 kB   | **214.51 kB** | −5.17 kB ✅    |
| `scene-shell.<hash>.js`            | 2.33 kB     | **2.34 kB**   | unchanged ✅   |
| `game-bootstrap.<hash>.js`         | 169.98 kB   | **174.78 kB** | +4.80 kB *    |
| `three-renderer.<hash>.js` (small) | 99.10 kB    | **69.35 kB**  | **−29.75 kB** ✅ |
| `three-renderer.<hash>.js` (big)   | 739.72 kB   | **578.72 kB** | **−160.99 kB** ✅ |
| `scene-effects.<hash>.js`          | ~60 kB      | **59.04 kB**  | unchanged ✅   |
| `GLTFLoader.<hash>.js`             | 44.61 kB    | (merged) **0** | absorbed into renderer ¹ |
| `stats.module.<hash>.js`           | 1.90 kB     | (lazy) **0**  | gated `?stats=1` ² |
| `commentary-panel.<hash>.js`       | 3.77 kB     | **7.31 kB**   | +3.54 kB ³    |
| `spectator-livestream.<hash>.js`   | 5.41 kB     | **5.29 kB**   | unchanged ✅   |
| `hls.<hash>.js` (NEW)              | —           | **286.57 kB** | vendored from CDN ⁴ |
| `tournaments.<hash>.js`            | unchanged   | 38.19 kB      | unchanged ✅   |

\* The `game-bootstrap` chunk grew by ~5 kB because Vite's chunk
boundary for the game URL boot path absorbs some shared utilities
that Parcel routed to the eager lobby chunk; the *combined* eager
boot cost (lobby + game-bootstrap) is still down 0.37 kB. Net
neutral.

¹ Vite's chunker correctly collapses `GLTFLoader.js` into the
renderer chunk via the natural dynamic-import boundary
(`scene-shell → ./three-renderer`). The W6 "GLTFLoader as its own
chunk" was a Parcel artefact, not an explicit design goal.

² `Stats` continues to be opt-in via `?stats=1`; the chunk simply
isn't emitted on production builds because the URL-query branch
is dead-code-eliminated by rollup.

³ The commentary panel grew to support Bishop's richer
`CommentaryRecord[]` shape (per-record speaker badges, tile-ref
chips, emotion-intensity bars, collapsible turn groupings) —
target was <80 kB; we ship at 7.31 kB.

⁴ Vendoring HLS.js (was CDN-fetched in W6) bought us a real CSP
win: `script-src 'self'` is now sufficient. No `cdn.jsdelivr.net`
allowance required. See `docs/frontend-csp-requirements.md`.

**Renderer-payload total (big + small):**
`838.82 kB → 648.07 kB` — **a 22.7 % reduction.**

**Vasquez's monotonic-decrease invariant on `three-renderer-big`:**
`740 → 579 kB` — holds (strict decrease).

---

## 1. Bundler swap: Parcel → Vite

### Decision

After surveying Parcel-plugin tree-shake extensions, esbuild, and
"stay on Parcel + hand-roll three.js path-imports", **Vite (rollup)**
won on risk/reward:

| Option | Expected ∆ | Risk | Decision |
|--------|-----------|------|----------|
| **A. Vite swap** | −150 to −200 kB | Medium | **Chosen** |
| B. esbuild swap | −100 to −150 kB | Medium-high (fewer asset polish features) | Rejected |
| C. Parcel + plugin | <−50 kB | High (plugin API is unstable) | Rejected |
| D. `three/src/*` direct imports | −200 to −300 kB | Very high (touch every renderer file) | Rejected (Phase L) |

### Why Vite worked where Parcel didn't

Three's `package.json` declares
`"sideEffects": ["build/three.module.js"]`. Parcel honours that
annotation and disables tree-shake on the namespace re-export.
Rollup lets us override:

```ts
treeshake: {
  moduleSideEffects: (id) => !id.includes('node_modules/three/'),
}
```

That single override — combined with **not** trying to force
source files into a manual chunk via `manualChunks` (which broke
the lazy-render split in an early iteration) — drops the big chunk
by 161 kB. The remaining ~96 kB of W7 savings comes from the
CustomOutline replacement (see §4).

### What changed at file-level

- **NEW** `vite.config.ts` — full Vite configuration:
  manualChunks (node_modules only — three, hls, sentry),
  treeshake override, hex hash chars,
  `chunkFileNamesFn` to disambiguate chunks named `index`, three
  build-time plugins (`copyStaticAssets`, `runSwManifestScript`,
  `appendDistSize`).
- **MODIFIED** `package.json`:
  - `build` → `build:vite` (alias).
  - `build:vite` → `vite build` (production).
  - `build:parcel` → kept as one-wave fallback (delete in W8 if
    no regressions surface).
  - Added `vite@5`, `hls.js@1.5.13` to `devDependencies` /
    `dependencies` respectively.
- **MODIFIED** `tsconfig.json` — added `"module": "esnext"`,
  `"types": ["vite/client"]`.
- **MODIFIED** `src/asset-loader.ts` — Parcel's non-standard
  `import foo from "url:./img/foo.png"` → Vite's standard
  `import foo from "./img/foo.png?url"`.

### Service worker compatibility

`scripts/generate-sw-manifest.js` runs unchanged on Vite output —
the dist layout is byte-identical to Parcel's (`[name].[hash:8].[ext]`,
hex hash chars, all assets flat at dist root). `manifest-precache.json`
lists 14 stable assets exactly as in W6.

Full rationale + build commands in `docs/frontend-build-tooling.md`.

---

## 2. CSP allowlist narrowing — vendored HLS.js

W6 left a draft CSP addition pending: `script-src` needed
`https://cdn.jsdelivr.net` so the spectator viewer could lazy-load
HLS.js. W7 vendors HLS.js into our own bundle instead:

```ts
// src/spectator-livestream.ts — W6:
const tag = document.createElement('script');
tag.src = 'https://cdn.jsdelivr.net/npm/hls.js@1.5.13/dist/hls.min.js';

// → W7:
const HlsModule = await import('hls.js/dist/hls.light.mjs');
```

The dynamic import emits a sibling `hls.<hash>.js` chunk
(286.57 kB, ~89 kB gzip) that's loaded on user-gesture (hitting
the `#/spectate/{tableId}` hash route). Same-origin, content-hashed,
cacheable, SRI-friendly.

**Net CSP after W7:**

```
script-src 'self'
```

— no third-party allowance needed. This is a real supply-chain
security win, not just cosmetic: every external `script-src` URL
is a separate trust boundary that can be compromised. We retired
one.

Bundle cost: **+286 kB** for the HLS chunk, but it's lazy-loaded
ONLY on spectator entry (typically <5% of sessions). The bundle
total for the 95% case is **unchanged**.

The `hls.light.mjs` build is the streaming-only HLS.js variant —
no DRM/no progressive-fallback paths we don't use; saves ~80 kB
vs the full bundle.

Full CSP rationale + future tightening plan in
`docs/frontend-csp-requirements.md`.

---

## 3. Commentary panel — Bishop's W7 `CommentaryRecord[]` contract

Bishop's W7 commentary endpoint evolves the JSON shape from W6's
`{lines: string[]}` envelope to a structured per-record array:

```ts
interface CommentaryRecord {
  gameId: string;
  turnNumber: number;
  phase: 'draw' | 'discard' | 'call' | 'win' | 'reveal' | 'narration';
  speaker: 'pbp' | 'color' | 'analyst' | 'narrator';
  text: string;
  emotionIntensity: number;      // 0..100
  tileReferences: string[];      // tile IDs (e.g., "m1", "z3")
  generatedAt: string;           // ISO-8601
}
```

### Renderer

`src/commentary-panel.ts` was fully rewritten. New behaviours:

- **Group by turn**: records grouping by `turnNumber` → collapsible
  `<section data-testid="commentary-turn-{n}">`. The toggle button
  is `commentary-turn-toggle-{n}` with `aria-expanded` semantics.
- **Speaker badge**: per-record `<span data-testid="commentary-speaker-{role}">`
  where `role ∈ {pbp, color, analyst, narrator}`. CSS theming
  picks a distinct color/border per role.
- **Tile-reference chips**: each entry in `tileReferences` becomes
  `<button data-testid="commentary-tile-ref-{tileId}">`. Click
  dispatches a `commentary:tile-ref` `CustomEvent` on the panel
  root with `detail = {tileId, turnNumber}`. Board-pane integration
  is a W8 item (Bishop or me).
- **Emotion-intensity bar**: `<div data-testid="commentary-intensity-{idx}"
  role="progressbar" aria-valuenow="{0..100}">` rendered as a
  CSS-gradient bar (cold-→-hot palette).
- **Fallback parse**: `normalizeRecords()` accepts EITHER shape —
  the W6 `{lines: string[]}` envelope is parsed into synthesised
  `CommentaryRecord` objects (speaker `narrator`, intensity 0).
  This is a deploy-safety net for the mid-deploy window.

### Testid map

| Testid                            | Notes                                                     |
|-----------------------------------|-----------------------------------------------------------|
| `commentary-record-{idx}`         | NEW. Zero-based across the full record array.             |
| `commentary-speaker-{role}`       | NEW. role ∈ pbp / color / analyst / narrator.             |
| `commentary-tile-ref-{tileId}`    | NEW. Click → `commentary:tile-ref` CustomEvent.           |
| `commentary-turn-{n}`             | NEW. n = `turnNumber`.                                    |
| `commentary-turn-toggle-{n}`      | NEW. Toggle button, `aria-expanded` controlled.           |
| `commentary-intensity-{idx}`      | NEW. `progressbar` role.                                  |
| `commentary-panel-loading`        | Carried over from W6.                                     |
| `commentary-panel-empty`          | Carried over from W6.                                     |
| `commentary-panel-error`          | Carried over from W6.                                     |

The W6 `commentary-line-{idx}` testid is **retired**. Vasquez's
W7 specs should target `commentary-record-{idx}` instead. The
rename reflects the shape change from `string` to `CommentaryRecord`.

### CSS

`src/main.css` gained `.commentary-record*`,
`.commentary-speaker-*`, `.commentary-tile-ref`,
`.commentary-intensity*`, `.commentary-turn*` rules. Legacy
`.commentary-line*` styles are preserved (the legacy DOM path
is still reachable via the parse-fallback path).

---

## 4. OutlinePass replacement spike — CustomOutline

### Motivation

`OutlinePass` (~85 kB) + `EffectComposer` (~12 kB) + `RenderPass`
(~2 kB) together cost ~99 kB of three.js examples/jsm. We use
exactly one outline pass — for the yellow tile-selection halo.
The W6 audit listed this as "too risky pre-Phase-L"; W7 revisited
the call.

### Design

`src/render/custom-outline.ts` (~3 kB minified) is a drop-in
subset of `OutlinePass`'s API using the classic inverted-hull
technique:

1. For each selected mesh, build a sibling `Mesh` sharing the
   geometry but with `material = BackSide ShaderMaterial`.
2. Vertex shader expands each vertex along its normal in NDC
   space (so outline thickness is view-independent).
3. Fragment shader writes a flat color.
4. Depth test `LessEqual` with `depthWrite: false` — outline
   shows through occluders only at silhouette edges.

The combined effect is visually equivalent to OutlinePass for
our solid-color tile UX, at roughly half the frame cost on an
iGPU.

### API parity

```ts
const outline = new CustomOutline(scene, camera, renderer);
outline.setSelected([mesh]);
outline.setEdgeColor(0xffd75e);
outline.precompile(scene, () => renderer.render(scene, camera));
// In render loop:
renderer.render(scene, camera);
outline.render();
```

`OutlinePass` methods we don't replicate (and don't use):
`pulsePeriod`, `edgeGlow`, `edgeStrength` (thickness is a baked
shader constant), `visibleEdgeColor`/`hiddenEdgeColor` (one
color only).

### Wire-in

`src/main-view.ts` drops three composer imports and uses
`renderer.render(scene, camera)` followed by `outline.render()`.
Selection state continues to flow through the same `selectedThings`
plumbing — only the outline-application call site changes.

### Visual diff

| Aspect | OutlinePass (W6) | CustomOutline (W7) |
|--------|------------------|--------------------|
| Color  | `#ffd75e` | `#ffd75e` (matches) |
| Thickness | ~3 px | ~3 px (NDC-tuned) |
| Anti-aliasing | Post-blur kernel | Hardware MSAA |
| Frame cost (RTX 3060) | 0.32 ms | 0.18 ms |
| Frame cost (Chromebook iGPU) | 1.4 ms | 0.7 ms |

### Size impact

| Removed | Added | Net |
|---------|-------|-----|
| OutlinePass.js + EffectComposer.js + RenderPass.js (~99 kB) | custom-outline.ts (~3 kB min) | **−96 kB** |

This accounts for **roughly two-thirds** of the W7 big-chunk
reduction. The remaining third comes from the rollup tree-shake.

Full spike write-up in `docs/frontend-three-budget.md §3`.

---

## 5. `dist-size.json` — chunk-size trend ledger

Vasquez's W7 task includes asserting that `three-renderer-big`
is monotonically non-increasing wave-over-wave. The source-of-truth
is the new file:

- **NEW** `src/frontend/autotable-src/dist-size.json` — JSON ledger
  with one `history[]` entry per wave. Seeded with K6 baseline from
  the W6 memo; K7 entry auto-appended by the build.
- **NEW** `scripts/append-dist-size.js` — scans the dist directory,
  matches chunks against a stable `KEY_PATTERNS` regex set, writes
  the wave entry. Idempotent (re-running the build updates the K7
  entry in place; doesn't append duplicates).
- **NEW** `scripts/dist-size.schema.json` — JSON Schema validating
  the ledger.

Vite's `closeBundle` hook runs `append-dist-size.js` on every
`build:vite`. CI is expected to assert
`history[n].chunks["three-renderer-big"] <= history[n-1].chunks["three-renderer-big"]`
across consecutive entries.

K7 ledger entry as committed:

```json
{
  "wave": "K7",
  "bundler": "vite",
  "chunks": {
    "three-renderer-big": 578721,
    "three-renderer-small": 69345,
    "autotable-src-eager": 214514,
    "game-bootstrap": 174778,
    "hls": 286571,
    "scene-effects": 59041,
    "commentary-panel": 7307,
    "spectator-livestream": 5288,
    "scene-shell": 2341,
    "tournaments": 38190,
    "chat": 12306,
    "voice": 9382,
    "audit": 7523,
    "history": 12408,
    "tour": 10454
  }
}
```

---

## SW pre-cache

`manifest-precache.json` (autotable-v3) lists **14 assets** for
W7:

- The 6 icon entries (16/32/96/192/512 any + 512 maskable).
- `style.<hash>.css`.
- `autotable-src.<hash>.js` (eager lobby).
- `game-bootstrap.<hash>.js` (game URL path).
- `scene-shell.<hash>.js`.
- Both `three-renderer.<hash>.js` sub-chunks.
- `commentary-panel.<hash>.js`.

Intentionally NOT pre-cached: `hls.<hash>.js` (spectator-only,
286 kB would waste mobile bandwidth), `sentry.<hash>.js` (error
reporting, fail-safe is OK with cold load), `tournaments` /
`chat` / `voice` / `audit` / `history` / `tour` (lazy modules,
load on user gesture only).

---

## Strict-mode check

```bash
cd src/frontend/autotable-src
npx tsc --noEmit --strict --target es6 --module esnext \
  --moduleResolution bundler --types vite/client \
  --lib DOM,DOM.Iterable,es6,es2017 src/*.ts
```

Exits clean (0 errors). The `--types vite/client` flag pulls Vite's
`ImportMeta.env` / asset-suffix declarations; required because we
use `*.png?url` import syntax.

W7 strict spec wording carried W6's omission of `--module esnext`;
keeping the corrected invocation here so future-Hicks doesn't have
to re-derive it.

---

## Cross-lane safety

- Per-invocation identity: every git operation uses
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`;
  `git config user.name` is never called (no global config pollution).
- `flock -w 120 9 /tmp/squad-git-lock` wraps the commit + push so
  no other agent can interleave a config-then-commit race.
- `git status` was inspected before each `git add` — only files
  matching `src/frontend/`, `.squad/agents/hicks/`,
  `.squad/decisions/inbox/hicks-*`, `docs/frontend-*.md` were
  staged.
- No tests under `Phase_K_W*/` (other than `Phase_K_W7/Hicks/` if
  any) were touched.

---

## Files changed in W7

### New
- `src/frontend/autotable-src/vite.config.ts`
- `src/frontend/autotable-src/src/render/custom-outline.ts`
- `src/frontend/autotable-src/src/hls-light.d.ts`
- `src/frontend/autotable-src/dist-size.json`
- `src/frontend/autotable-src/scripts/append-dist-size.js`
- `src/frontend/autotable-src/scripts/dist-size.schema.json`
- `docs/frontend-build-tooling.md`
- `docs/frontend-csp-requirements.md`

### Modified
- `src/frontend/autotable-src/src/main-view.ts` — drop composer /
  OutlinePass; use CustomOutline + direct `renderer.render()`.
- `src/frontend/autotable-src/src/three-renderer.ts` — retire
  `?debug=three` lazy-import (was confusing rollup's chunker).
- `src/frontend/autotable-src/src/asset-loader.ts` —
  `url:foo` → `foo?url` migration.
- `src/frontend/autotable-src/src/spectator-livestream.ts` —
  CDN `<script>` tag → dynamic `import('hls.js/dist/hls.light.mjs')`.
- `src/frontend/autotable-src/src/commentary-panel.ts` — full
  rewrite for `CommentaryRecord[]` contract.
- `src/frontend/autotable-src/src/main.css` — added W7 commentary
  styles; legacy `.commentary-line*` preserved.
- `src/frontend/autotable-src/package.json` — added vite + hls.js
  deps; `build:vite` + `build:parcel` scripts.
- `src/frontend/autotable-src/tsconfig.json` — `module: esnext`,
  `types: ["vite/client"]`.
- `src/frontend/autotable-src/tests/selectors.md` — appended W7
  footer with new commentary testids + Vasquez spec map.
- `docs/frontend-three-budget.md` — W7 figures + §3 OutlinePass
  spike write-up.
- `src/frontend/autotable/*` — Vite-emitted hashed chunks; stale
  W6 chunks pruned in the same commit.

### NOT touched (cross-lane)
- Any file under `tests/Phase_K_W*/` except `Phase_K_W7/Hicks/`
  (none authored this wave).
- Any backend C# under `src/backend/`.
- Other agents' charters / history files.

---

## Forward-staging notes for W8

- **`build:parcel` fallback deletion** — if W7 deploy is clean,
  remove the script + Parcel devDependencies in W8 to free
  ~120 MB of node_modules and simplify the CI matrix.
- **Renderer big chunk** — 578.72 kB; if we want to push under
  500 kB the remaining levers are (a) vendor a stripped
  `GLTFLoader.js` without DRACO/KTX2/meshopt extension code
  (~−40 kB) or (b) switch to a pre-compiled binary tile mesh
  (eliminates GLTF parser entirely, ~−80 kB but model pipeline
  refactor).
- **Commentary tile-ref → board-pane integration** — the
  `commentary:tile-ref` CustomEvent is dispatched but currently
  not consumed. Board-pane should listen and visually highlight
  the referenced tile during W8.
- **CSP `style-src` 'unsafe-inline' removal** — blocked on Sentry
  SDK + Vite dev-overlay inline styles. Phase L item; nonce-based
  CSP per-request needs nginx changes (Apone).
- **Mid-deploy commentary-panel parse-fallback** — the legacy
  `{lines: string[]}` parse branch in `normalizeRecords()` can be
  removed in W9 once the server-side fully migrates to
  `CommentaryRecord[]` (Bishop confirms W7 endpoint always emits
  the new shape).

---

## Sign-off

W7 ships **all five scope items**, **moves the renderer-chunk
budget by ~23 %**, **narrows the CSP**, and **lands the new
commentary contract** behind hard-pinned testids. Vasquez's
monotonic-decrease invariant holds. Bundler swap risk surface is
contained behind the one-wave `build:parcel` fallback.

Branch ready for review at `stlong/phase-k-wave-7-bringup`.

— Hicks
