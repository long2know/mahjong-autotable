# Hicks — Phase K Wave 8 memo

**Branch:** `stlong/phase-k-wave-8-bringup`
**Date:** 2026-05-23
**Author:** Hicks (Frontend Engineer) `<hicks@squad.mahjong>`
**Scope:** Three.js renderer chunk under 540 KB; double-elim losers-
bracket UI with reset-match row; commentary tile-ref → board highlight
event flow; Lighthouse PWA audit (≥ 0.95 target); Vite dev-server
SignalR + WebSocket proxy; trend-ledger K8 entry.
**Build gate:** `npm run build:vite` clean (~8 s wall);
`tsc --noEmit --strict --target es6 --module esnext --moduleResolution
bundler --types vite/client` zero errors.

---

## Headline — every W8 target met, PWA score 1.00

| Item | W8 target | W8 result | Status |
|------|-----------|-----------|--------|
| `three-renderer.<hash>.js` (big) | < 540 KB | **531.86 KB** | ✅ +8.14 KB headroom |
| Losers-bracket UI (with reset-match row) | testids + wire shape | shipped (`bracket-renderer.ts`) | ✅ |
| Commentary tile-ref → board highlight | < 500 ms latency | event chain wired, < 1 ms in handler | ✅ |
| Lighthouse PWA score | ≥ 0.95 | **1.00** | ✅ |
| Vite SignalR + WS proxy | `/hubs/*`, `/autotable/ws` forwarded | shipped (`vite.config.ts:server.proxy`) | ✅ |

`dist-size.json` K8 entry recorded; Vasquez's monotonic-decrease
invariant on `three-renderer-big` holds (`740 → 579 → 531.86 KB`).

---

## 1. Three.js renderer chunk: 578.72 → 531.86 KB (−46.86 KB)

### The lever that worked — GLTFLoader chunk peel (−44.22 KB)

The `asset-loader.ts` dynamic-import has been lazy-loading
`three/examples/jsm/loaders/GLTFLoader.js` since W3, but Vite/Rollup
silently collapsed the import back into `three-renderer` because both
matched the catchall `node_modules/three/` rule in
`vite.config.ts:manualChunks`. Adding an explicit pre-check **before**
the catchall:

```ts
function manualChunks(id: string): string | undefined {
  if (id.includes('node_modules/hls.js/')) return 'hls';
  if (id.includes('node_modules/@sentry/')) return 'sentry';
  // W8 ADDITION:
  if (id.includes('node_modules/three/examples/jsm/loaders/GLTFLoader')) {
    return 'gltf-loader';
  }
  if (id.includes('node_modules/three/')) return 'three-renderer';
  return undefined;
}
```

splits the loader into its own 44.22 KB chunk that `AssetLoader.loadAll()`
fetches in parallel with the texture downloads. Net first-paint cost is
unchanged (the awaits were already concurrent), the renderer chunk just
sheds the loader's weight. The new chunk gets picked up by the SW manifest
generator automatically — `THREE_RENDERER_RE` already covers the
`three/examples/jsm/` graph; the GLTFLoader chunk is now a peer that the
SW pre-caches via the existing `chunk-*.<hash>.js` regex set.

### The second lever — hand-rolled `mergeSimpleGeometries` (−3.83 KB)

`object-view.ts` used `mergeGeometries` from
`three/examples/jsm/utils/BufferGeometryUtils.js` to consolidate 24 static
tile-tray geometries into a single draw call. That single call pulled the
entire 1435-line `BufferGeometryUtils.js` into the renderer chunk. The
24 inputs all share attribute layout (position + normal + uv, `itemSize` 3
and 2) and are all non-indexed, so a 36-line hand-rolled helper covers
the contract exactly:

```ts
function mergeSimpleGeometries(geometries: BufferGeometry[]): BufferGeometry {
  // Contract: all inputs share attribute names + itemSize,
  // all are non-indexed. The single caller is addStatic's
  // tile-tray pass; any new caller must verify this.
  ...
}
```

If we ever need indexed-geometry merging or differing layouts, revert
those callers to `mergeGeometries`. The helper is callee-specific by
design — keeping it general would just reinvent `BufferGeometryUtils.js`.

### What didn't work — deep imports made the bundle BIGGER

The W8 directive hinted that per-class deep imports
(`from 'three/src/math/Vector3.js'`) should help. **Tested; they made the
bundle ~150 KB larger.**

| Approach | Big chunk | Δ vs W7 |
|----------|-----------|---------|
| W7 baseline (`from 'three'`) | 578.72 KB | — |
| Bulk swap `from 'three/src/Three.js'` | 729.4 KB | **+150.7 KB** ❌ |
| Per-class deep imports (38 symbols) | 725.5 KB | **+146.8 KB** ❌ |

Root cause: three's bundled `build/three.module.js` is **more**
tree-shake-friendly than its `src/` tree because the
`moduleSideEffects: false` Rollup config can dead-strip private helpers
inside a single bundled file but conservatively preserves them across
file boundaries. The W8 directive's "deep imports help" hint was wrong
for three.js 0.179. **Don't retry.** The `scripts/three-deep-imports.js`
and `scripts/three-collapse-imports.js` rewriter pair are kept in-tree
as reference / safety net in case a future major three.js release flips
the calculus, but they should NOT be applied to the source by default.

Full experiment write-up: `docs/frontend-three-budget.md §4`.

### Aggressive treeshake levers (no measurable impact)

Added `propertyReadSideEffects: false`, `tryCatchDeoptimization: false`,
`unknownGlobalSideEffects: false` to `rollupOptions.treeshake`. **No
measurable size change** in the current import topology — kept for
future-proofing.

### Remaining dead-weight (~80 KB) inside the chunk

Material classes pulled by `WebGLRenderer`'s internal `material.type`
string switches: `MeshStandardMaterial` (×13 references in three's
renderer code paths), `MeshPhongMaterial`, `MeshPhysicalMaterial`,
`MeshToonMaterial`, `MorphTarget`, `Skeleton`, `SkinnedMesh`,
`VideoTexture`, `CompressedTexture`, `Sprite`, `Points`, `LOD`,
`GLBufferAttribute`. These **cannot be tree-shaken without forking
three's build** — Rollup conservatively keeps them because the
runtime dispatcher references them by string.

Future waves that want more savings should patch three.js's
`WebGLRenderer.js` (we only use `MeshLambertMaterial` +
`MeshBasicMaterial`) and ship as a `pnpm` patch or `package.json`
resolution. Estimated savings: ~15–20 KB. Defer to W10+.

---

## 2. Double-elim losers-bracket UI + reset-match row

### Wire-shape contract (Bishop's W8 endpoint)

The W6 double-elim renderer used a client-side heuristic
(`partitionDoubleElim(matches)`) that walked the flat `matches[]` array
and inferred winners/losers/grand-final by `round` + `matchIndex`. W8
adds an optional server-authored partition that the renderer prefers
when present:

```ts
interface DoubleElimLayout {
  winnersBracket: BracketMatch[];
  losersBracket: BracketMatch[];
  grandFinal: { match: BracketMatch | null; resetMatch: BracketMatch | null };
}
```

`tournaments.ts:normalizeDoubleElimLayout` tolerates three wire spellings
(`layout`, `doubleElimLayout`, `bracketLayout`) and Bishop-side
`grandFinal.match` / `grandFinal.resetMatch` (snake-case fallbacks
accepted). When the wire still ships only `matches[]` (mid-deploy where
the controller change hasn't landed yet), the renderer falls back to the
W6 heuristic and emits `resetMatch: null`.

### Reset-match render gating

The reset row only renders when the bracket actually needs the second
game:

```ts
function shouldRenderResetMatch(grandFinal, resetMatch): boolean {
  if (resetMatch === null) return false;
  if (grandFinal === null) return false;
  // Pre-decided cases: reset match in-progress or complete →
  // render regardless (the bracket clearly entered reset state).
  const rs = (resetMatch.status ?? '').toLowerCase();
  if (rs === 'in-progress' || rs === 'complete') return true;
  // Otherwise: render iff the grand-final is complete AND the
  // losers-bracket champion (player2 by Bishop's convention) won.
  const fs = (grandFinal.status ?? '').toLowerCase();
  if (fs !== 'complete') return false;
  return grandFinal.winnerPlayerId === grandFinal.player2?.playerId;
}
```

This is belt-and-braces — Bishop only emits `resetMatch` populated when
it's actually needed, but a stale cache shouldn't surface a row that
should be hidden.

### Testid migration (W6 → W8)

| W6 testid | W8 testid | Reason |
|-----------|-----------|--------|
| `bracket-double-elim-winners` | `winners-bracket` | Vasquez's W8 spec name |
| `bracket-double-elim-losers` | `losers-bracket` | Vasquez's W8 spec name |
| `bracket-match-{round}-{matchIndex}` | `bracket-match` (with `data-match-round` / `data-match-index` siblings) | `getAllByTestId('bracket-match')` count assert |
| `tournament-grand-final` | `bracket-grand-final` (legacy kept on same element via `data-testid-legacy`) | W8 spec name |
| n/a | `grand-final-reset` | W8 reset row |
| n/a | `losers-bracket-round-{n}` (group), `losers-bracket-round` (label) | W8 round group + label |
| n/a | `bracket-live-update` (hidden anchor) | W8 mutation-observer target |

Verified no test code referenced the renamed testids
(`grep -rn 'bracket-double-elim-losers'` returned only `selectors.md`
and decision memos). selectors.md updated to document the rename;
docs are authoritative source of truth.

### Live-update path

`tournaments.ts:ensureHubSubscription` already wired
`TournamentMatchCompleted` / `TournamentMatchCompletedV1`. W8 adds:

- `conn.on('TournamentBracketUpdated', handler)` — Bishop's W8
  finer-grained event that fires on ANY bracket cell mutation (seed
  shuffle, dispatch, score, reset spawn).
- `window.__publishTournamentBracketUpdate(payload)` — installed
  on first activation, calls the same refresh path so Vasquez's
  `bracket-live-update.spec.ts` can simulate the push without
  spinning up a real hub.

`bracket-renderer.ts:DoubleElimRenderer.render` adds an invisible
`<div data-testid="bracket-live-update" data-update-id={Date.now()}>`
to the wrap on every render — the spec mutation-observes
`data-update-id` to detect the re-render.

---

## 3. Commentary tile-ref → board highlight (item 3)

### Event flow

1. User clicks a tile-ref chip in the commentary panel.
2. `commentary-panel.ts:renderTileRef` dispatches **two** events
   synchronously:
   - `commentary:tile-ref` — kept for back-compat with Hank's
     analyst overlay (W7 contract).
   - `mahjong:highlight-tile` — new W8 event consumed by
     `MainView`.
3. `MainView.setupHighlightOverlay` listens for the new event and
   calls `pulseHighlight(tileId)`.
4. `pulseHighlight`:
   - Sets `data-highlight-tile-id={id}` on `#main` and the overlay.
   - Sets `data-highlight-active="true"` on the overlay (triggers
     the 2 s CSS pulse animation — yellow halo over the canvas).
   - Writes `window.__lastHighlightedTile` + `window.__highlightTimestampMs`
     (Vasquez's observability hooks, written synchronously before
     event dispatch so latency measurements are accurate).
   - Dispatches `tile-highlight` (CustomEvent with `{ tileId, timestamp }`)
     — Vasquez's latency spec listens for this as the
     "highlight has landed" confirmation.
   - Sets a 2000 ms timer that clears all the data attributes.
5. Re-entrant: a second click before the first pulse expires
   resets the timer (most-recent click wins).

### Why a CSS overlay and not a 3D mesh outline

The tile-ref format ("S2-Z7", "M1", "Z7") is the wire-format that
Bishop's commentary generator emits — it's not directly mapped to
`World.things[]` (the runtime tile dictionary) without a parser that
doesn't yet exist. Even if the parser were trivial, `MainView`'s
outline gets overwritten every frame from `objectView.selectedObjects`
(see `Game.update()` line 130), so a direct `outline.setSelected([mesh])`
call would get clobbered on the next frame.

The CSS overlay approach:
- **Zero coupling to the world layer.** No mesh lookup, no per-frame
  override fight.
- **Hard-deterministic latency.** Browser CSS animation start = same
  event-loop turn as the click handler — well under 500 ms.
- **Playwright-friendly.** `[data-highlight-tile-id]` selector works
  without touching the renderer pipeline.
- **`prefers-reduced-motion: reduce` honoured** (the CSS animation
  collapses to a static highlight).

The actual tile-id → 3D mesh mapping is deferred to **Phase L**
(probably W9 or later — needs a tile-id parser + a `World.findThingByFace`
API). When that lands, `pulseHighlight` can ALSO call
`outline.setHighlight([mesh])` for an in-3D pulse; the CSS overlay
stays as a fallback and as the Playwright observability layer.

---

## 4. Lighthouse PWA audit — 0.75 → 1.00

### Baseline (regression from W7)

`lighthouse@11.7.1 --only-categories=pwa` on the W8 production
build initially scored **0.75**. The single failing audit was
`installable-manifest`:

```
✗ No supplied icon is at least 144 px square in PNG, SVG or WebP
  format, with the purpose attribute unset or set to "any".
```

### Root cause — Vite hashed the icons, manifest kept source paths

The manifest's `icons[].src` entries reference the source-tree paths
(`img/icon-192.auto.png`). Vite's HTML processor moves all
HTML-referenced icons to the build root with content-hashed names
(`icon-192.auto.88edf577.png`). The manifest is emitted as a static
copy via `vite.config.ts:copyStaticAssets`, so its `src` values
NEVER get rewritten. Result: every manifest icon 404'd, and Lighthouse
couldn't find a single icon ≥ 144 px to satisfy the install rule.

This had been broken since W7 (the Parcel → Vite swap); W7 didn't
re-run the PWA audit so the regression went unnoticed for a full wave.

### Fix

`vite.config.ts:copyStaticAssets` now also copies the un-hashed PWA
icons to `out/img/icon-NNN.auto.png` so the manifest's `src` paths
resolve. The hashed root-level copies remain (referenced from
`index.html` via Vite's HTML processor) — they live at different paths
and don't conflict.

```ts
const iconNames = [
  'icon-16.auto.png', 'icon-32.auto.png', 'icon-96.auto.png',
  'icon-192.auto.png', 'icon-512.auto.png', 'icon-maskable-512.auto.png',
];
if (!existsSync(`${out}/img`)) mkdirSync(`${out}/img`, { recursive: true });
for (const name of iconNames) {
  copyFileSync(`${root}/img/${name}`, `${out}/img/${name}`);
}
```

Post-fix score: **1.00** (all six binary audits ✓).

### Lighthouse 13 note — PWA category dropped

`lighthouse@13.x` (released 2026) **removed the PWA category entirely**.
The audit recipe in `docs/frontend-pwa-audit.md §3` pins
`lighthouse@11` for repeatable scoring. When the team moves to a
PWA-Builder-based audit (Microsoft's replacement tooling), the recipe
will need updating — flagged in W9 hand-off notes.

---

## 5. Vite SignalR + WebSocket dev proxy

Before W8, the dev workflow against Bishop's ASP.NET Core backend was
clunky:

- Changsha hub needed `?hub=http://localhost:5000/hubs/changsha` URL
  override on every page load.
- Voice hub had **no override path** — voice testing required a full
  prod build co-located with the backend.
- Commentary livestream WS (`/autotable/ws`) was in the same boat.

W8 adds a `server.proxy` block to `vite.config.ts`:

```ts
server: {
  proxy: {
    '/hubs': {
      target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
      ws: true, changeOrigin: true,
    },
    '/autotable/ws': {
      target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
      ws: true, changeOrigin: true,
    },
    '/api': {
      target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
      changeOrigin: true,
    },
  },
}
```

`ws: true` enables the HTTP → WebSocket upgrade dance so SignalR's
`wss://` transport survives the hop. `hub.ts:hubUrl()` simplified to
return `/hubs/changsha` (same-origin) in every mode — the dev proxy
makes this work, production co-locates hub + bundle at the same origin.
The legacy `?hub=<url>` override is kept for contributors pointing at
a remote backend.

---

## Identity + commit hygiene

Per W6/W7 proven pattern:

- Per-command git env, never `git config user.name`.
- Flock-wrapped commit at `/tmp/squad-git-lock`.
- Only lane-allowed paths staged: `src/frontend/`, `docs/frontend-*`,
  `.squad/agents/hicks/`, `.squad/decisions/inbox/hicks-*`,
  `src/frontend/autotable-src/tests/selectors.md`.
- `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
  trailer included.

---

## Hand-off to W9 / Vasquez

- **Vasquez:** W8 specs (`losers-bracket-render.spec.ts`,
  `commentary-tile-ref-latency.spec.ts`, `three-renderer-540-hard.spec.ts`,
  `pwa-lighthouse-score.spec.ts`, `vite-signalr-proxy.spec.ts`,
  `bracket-live-update.spec.ts`) should pass against this build:
  - Testids documented in `tests/selectors.md` W8 footer.
  - `dist-size.json` K8 entry recorded; `three-renderer-big = 531862`.
  - `window.__lastHighlightedTile`, `window.__highlightTimestampMs`,
    `window.__publishTournamentBracketUpdate` all installed.
- **Bishop:** `tournaments.ts:normalizeDoubleElimLayout` tolerates three
  wire spellings (`layout` / `doubleElimLayout` / `bracketLayout`) for
  the partition. Pick one canonically (recommendation: `layout`) and
  drop the others in W9.
- **Phase L candidate work:**
  - Tile-id → 3D mesh mapping (currently CSS-overlay only; once
    `World.findThingByFace` exists, `pulseHighlight` can ALSO call
    `outline.setHighlight([mesh])` for an in-3D pulse).
  - `WebGLRenderer.js` patch to strip unused material types (~15-20 KB
    estimated savings on the renderer chunk).
  - Manifest gap-fills: `screenshots[]`, `id`, `lang`, `dir`,
    `iarc_rating_id` (PWA Builder flags but not Lighthouse 11 blockers).
  - Lighthouse 13+ migration (PWA category dropped — recipe needs
    rewriting around individual audits).
- **Parcel removal:** Plan to delete `build:parcel` from
  `package.json` at end of W9 if no regressions surface (W7 + W8 both
  Vite-only deploys clean).

---

## Files touched (lane-conformant)

```
src/frontend/autotable-src/vite.config.ts            (modified)
src/frontend/autotable-src/src/bracket-renderer.ts   (modified)
src/frontend/autotable-src/src/tournaments.ts        (modified)
src/frontend/autotable-src/src/main-view.ts          (modified)
src/frontend/autotable-src/src/commentary-panel.ts   (modified)
src/frontend/autotable-src/src/object-view.ts        (modified)
src/frontend/autotable-src/src/asset-loader.ts       (modified)
src/frontend/autotable-src/src/hub.ts                (modified)
src/frontend/autotable-src/src/style.css             (modified)
src/frontend/autotable-src/src/main.css              (modified)
src/frontend/autotable-src/.gitignore                (modified)
src/frontend/autotable-src/scripts/append-dist-size.js   (modified)
src/frontend/autotable-src/scripts/three-deep-imports.js (new, NOT applied — reference only)
src/frontend/autotable-src/scripts/three-collapse-imports.js (new, NOT applied — reference only)
src/frontend/autotable-src/dist-size.json            (K8 entry appended)
src/frontend/autotable-src/tests/selectors.md        (W8 footer)
src/frontend/autotable/**                            (build artefacts — Vite rebuild)
docs/frontend-three-budget.md                        (modified — W8 §4)
docs/frontend-build-tooling.md                       (modified — K8 row + §3 proxy + §4 PWA fix)
docs/frontend-pwa-audit.md                           (new — W8 PWA audit doc)
.squad/agents/hicks/history.md                       (modified — W8 entry appended)
.squad/decisions/inbox/hicks-phase-k-wave-8.md       (new — this memo)
```
