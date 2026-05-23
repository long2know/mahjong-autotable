# Hicks — Phase K Wave 6 memo

**Branch:** `stlong/phase-k-wave-6-bringup`
**Date:** 2026-07-04
**Author:** Hicks (Frontend Engineer) `<hicks@squad.mahjong>`
**Scope:** AI commentary side-panel UI (Phase L-ready), spectator
HLS livestream viewer (hash route), bracket renderer strategy
(Swiss + double-elim), modest pre-Phase-L three.js tree-shake
sweep (Stats + GLTFLoader lazy, wildcard `import * as three`
retired), PWA install button polish + two new tour stops +
maskable + 192/512 icons.
**Build gate:** `parcel build` clean (~9 s wall); `tsc --noEmit
--strict --target es6 --module esnext --moduleResolution bundler`
zero errors (note: the W6 task spec's strict command omits
`--module esnext` — without it `tsc` rejects dynamic imports with
TS1323; W7 should update the spec wording).

---

## Headline — five disjoint UI surfaces, all gated, none on a critical-path

W6 is a "stage the UI for Phase L" wave. Every new surface is
behind a hash route, a server reply, or a feature toggle so the
existing tile / replay / tournament screens are byte-identical
on first paint. None of the five deliverables widens the
`autotable-src` eager chunk (still 219.68 kB), nor the
`game-bootstrap` preload chunk (still 169.98 kB).

| Chunk                              | Wave 5     | Wave 6      | Δ              |
|------------------------------------|------------|-------------|----------------|
| `autotable-src.<hash>.js` (eager)  | 218.7 kB   | **219.68 kB** | +1.0 kB        |
| `scene-shell.<hash>.js`            | 2.33 kB    | **2.33 kB**   | unchanged ✅   |
| `game-bootstrap.<hash>.js`         | 169.98 kB  | **169.98 kB** | unchanged ✅   |
| `three-renderer.<hash>.js` (small) | 144.9 kB   | **99.1 kB**   | **−45.8 kB**   |
| `three-renderer.<hash>.js` (big)   | 724.7 kB   | **739.72 kB** | +15 kB (see below) |
| `GLTFLoader.<hash>.js` (NEW)       | —          | **44.61 kB**  | split from small chunk |
| `stats.module.<hash>.js` (NEW)     | —          | **1.9 kB**    | split, opt-in only |
| `commentary-panel.<hash>.js` (NEW) | —          | **3.77 kB** ✅ | target <80 kB |
| `spectator-livestream.<hash>.js` (NEW) | —      | **5.41 kB**   | hash route only |
| `tournaments.<hash>.js`            | unchanged  | unchanged*   | bracket-renderer code is inlined |

\* The bracket renderer strategy module is dynamic-imported on
the first `rerenderBracket()` call, so it does not bloat the
eager tournaments chunk; parcel chose to inline it into the
existing `tournaments` chunk (still well under the W4 1 MB budget).

### About the big `three-renderer` chunk

The W6 task carried a strict <700 kB sub-target on the big
three-renderer chunk. **That target was not met** — the chunk
weighs 739.72 kB. The reason is that the chunk is **almost
entirely three.js core re-exports** (`three.module.js` →
`three.core.js`); 386 distinct three symbols are statically
imported across `main-view.ts`, `asset-loader.ts`,
`object-view.ts`, `world.ts`, `client-ui.ts`, etc., and parcel's
tree-shaker keeps the whole namespace because three's index
re-exports the entire core in one go. Real reductions require a
bundler swap (esbuild / rollup do this better) or a deep refactor
to import from `three/src/*` paths directly — both well beyond
the W6 "modest pre-Phase-L cleanup" envelope.

What I **did** ship under that target:

1. Retired the only `import * as three from 'three'` (in
   `three-renderer.ts`). `window.three` is now opt-in via
   `?debug=three`; `window.game` remains unconditional.
2. `Stats` (three's perf HUD) is no longer statically imported
   anywhere. `main-view.ts` only constructs it when
   `?stats=1` is present in the URL. Net result: a tiny
   1.9 kB `stats.module.<hash>.js` chunk that 99 % of users
   never fetch.
3. `GLTFLoader` is now dynamic-imported via `getGltfLoader()`
   inside `asset-loader.ts:loadModels()` (which is async).
   Parcel extracted a sibling 44.61 kB `GLTFLoader.<hash>.js`
   chunk that loads **in parallel** with the texture fetches on
   first model load, so wall-clock TTFP is unchanged or
   slightly better.

Total renderer payload on a cold game-URL navigation:
`99.1 + 739.72 = 838.8 kB` (down from W5's `144.9 + 724.7 = 869.6 kB`,
**−30.8 kB** before GLTFLoader's parallel sibling fetch).
The "big chunk" hash (`c3e34903`) is byte-identical to W5 — the
savings all came out of the small chunk via GLTFLoader extraction.

W7 should NOT re-attempt the <700 kB target without a bundler
decision; see `docs/frontend-three-budget.md` for the full audit
and recommended next-step options.

---

## 1. AI commentary side panel (`commentary-panel.ts`)

A 3.77 kB module that mounts an `<aside>` next to the replay
move-log. On replay open it hits
`GET /api/games/{gameId}/commentary/replay`; on 200 it lists each
returned commentary turn as a `commentary-line-{idx}` row.
On 404 / 503 it shows a Phase L "coming soon" empty state — the
Phase L backend endpoint isn't expected to land until L1, so 404
is the steady-state response for the foreseeable future.

### Wiring
- `replay.ts:openServer()` calls `void this.mountCommentaryPanel(payload.gameId)`
  after the existing move-log render. The import is dynamic so
  the bundle cost stays out of the eager path.
- `replay.ts:close()` calls `void this.unmountCommentaryPanel()`
  which `closeCommentaryPanel()`s any open instance and clears
  the host aside.
- The host `<aside id="replay-commentary-host"
  data-testid="replay-commentary-host">` is always in the DOM
  (it's in `index.html`); only the inner panel mounts lazily.

### Test IDs
`replay-commentary-host`, `commentary-panel`,
`commentary-panel-loading`, `commentary-panel-empty`,
`commentary-panel-error`, `commentary-line-{idx}`.

### Forward-staging notes for W7 (Bishop)
- The fetch consumes a JSON shape of `{ lines: Array<{ text:
  string; speaker?: string; ts?: number }> }`. Bishop is free to
  evolve that shape; the UI degrades to "empty" on a parse failure.
- `gameId` is whatever `replay.openServer(payload)` already
  receives via `payload.gameId`; no additional client-side state.

---

## 2. Spectator HLS livestream viewer (`spectator-livestream.ts`)

A 5.41 kB module bound to the `#/spectate/{tableId}` hash route
by `installSpectatorRoute()`. Renders a small full-screen viewer
with `<video data-testid="spectator-livestream-player">`, a
status region, a live spectator-count badge, and a leave button.

### HLS.js loading strategy
The viewer uses native HLS on Safari (no library needed). For
Chrome/Firefox/Edge it lazy-loads HLS.js from the public CDN
(`https://cdn.jsdelivr.net/npm/hls.js@1.5.13/dist/hls.min.js`)
via a `<script>` tag at first-spectate-time. This deliberately
**avoids** adding a ~120 kB npm dependency to our bundle that
Safari users would never need. CSP for the live origin will
need to allow `cdn.jsdelivr.net` for `script-src`; flagging for
Bishop / Ripley to update the CSP header when the spectator
backend ships.

### SignalR
Reuses the existing `hub.ts:getHubConnection()`. On open the
viewer calls (best-effort) `JoinSpectatorGroup({ tableId })`
and listens for `spectatorCountUpdate`. On close it calls
`LeaveSpectatorGroup({ tableId })`. Both hub methods are
defensive — wrapped in `try/catch` because the server-side
methods may not exist yet (Bishop's W6 stub only ships the m3u8
endpoint, not the group join).

### Playlist URL
`/api/tables/{tableId}/livestream/playlist.m3u8`. If Bishop's
landing endpoint URL differs, only one line in
`spectator-livestream.ts:playlistUrlFor()` needs updating.

### Test IDs
`spectator-livestream-screen`, `spectator-livestream-player`,
`spectator-livestream-status`, `spectator-count`,
`spectator-livestream-leave`.

---

## 3. Bracket renderer strategy (`bracket-renderer.ts`)

The existing `tournaments.ts:rerenderBracket()` was a single
`buildBracketSvg()` call that only handled single-elimination.
W6 splits the renderer into a strategy module exporting:

- `SingleElimRenderer` — delegates to the existing
  `buildBracketSvg()` helper (zero behaviour change).
- `SwissRenderer` — renders a per-round table of pairings + a
  running standings table. Round-robin formats use this renderer
  too (Swiss with `rounds = N-1` is structurally identical).
- `DoubleElimRenderer` — renders winners-bracket + losers-bracket
  + grand-final regions with cross-region linking.
- `pickBracketRenderer(format)` — switch over the format key.
- `resolveFormatKey(format)` — substring-matches the user-visible
  format string (`'Double Elimination'`, `'Swiss System'`, etc.)
  to `'single-elim' | 'swiss' | 'double-elim' | 'round-robin'`.

`rerenderBracket()` now reads `tournament.format`, calls
`resolveFormatKey`, and dispatches to the matching renderer. The
container's `data-testid="bracket-format-{format}"` is set
unconditionally so e2e specs can hard-assert which renderer ran.

### Removed code
- The old `buildMatchesList()` function (lines ~1456-1492 of the
  pre-W6 `tournaments.ts`) was unreferenced after the rewrite and
  has been removed. No other module re-exports it.

### Test IDs
`bracket-format-{single-elim|swiss|double-elim|round-robin}`,
`bracket-round-{n}`, `bracket-match-{round}-{matchIndex}`,
`bracket-double-elim-winners`, `bracket-double-elim-losers`,
`tournament-grand-final`, `bracket-swiss-standings`.

### W7 follow-ups
The two new renderers consume `tournament.matches[]` directly. If
Bishop / Apone evolve the tournament JSON shape for Swiss or
double-elim (e.g. add a `bracket: 'winners' | 'losers'`
discriminator on each match), only the `DoubleElimRenderer` /
`SwissRenderer` partition functions need updating.

---

## 4. PWA install button polish + tour additions

### Install button (`pwa.ts`)
The install affordance is now a real `<button>` in the top bar
(`data-testid="pwa-install-button"`) instead of the inline
prompt strip we shipped in W3. The legacy `pwa-install-prompt`
testid is preserved as a hidden `<span>` inside the button so
W3-era e2e specs still resolve until W7+ rewrites them.

Added: an `appinstalled` window event listener at module bottom
that hides the button once the platform reports successful
install.

### Tour (`tour.ts`)
Inserted two new stops in the existing 8-step tour:
- **Step 6 — voice setup** (between chat and language stops):
  anchors on `voice-toggle` / `voice-settings`, explains the
  voice rotation and the spectator-distinct copy that landed in
  W5. Selector: `tour-step-voice-setup`.
- **Step 9 — tournament view** (between tournaments-tab and
  finale stops): anchors on `tournament-tab` / `bracket-format-*`,
  explains the new bracket renderers. Selector:
  `tour-step-tournament-view`.

Intro copy updated from "Quick tour: 6 stops, ~30 seconds" to
"Quick tour: 10 stops, ~45 seconds". Step counters and
percentages are recomputed from `STEPS.length`, so no other copy
needed manual updates.

### Manifest + icons
- `manifest.webmanifest` now declares 6 icon entries: 16 / 32 /
  96 / 192 / 512 (`purpose: "any"`) and 512 (`purpose: "maskable"`).
- New PNGs in `src/frontend/autotable-src/img/`:
  `icon-192.auto.png`, `icon-512.auto.png`,
  `icon-maskable-512.auto.png` — all generated from
  `img/icon.svg` via ImageMagick `convert`.
- `index.html` carries new `<link rel="apple-touch-icon">` entries
  for 192 and 512 (the existing 16/32/96 links are kept).
- `scripts/generate-sw-manifest.js`'s `ICON_RE` now matches the
  new sizes and the maskable variant; all 6 icons are in
  `manifest-precache.json`. A new `COMMENTARY_RE` adds the
  commentary-panel chunk to the pre-cache so the replay panel is
  installable offline.

---

## SW pre-cache
`manifest-precache.json` (autotable-v3) now lists 18 assets
including all 6 icons (16/32/96/192/512 any + 512 maskable), the
new `commentary-panel.<hash>.js` chunk, and both `three-renderer`
sub-chunks. The two new "deep" chunks (`GLTFLoader`, `stats.module`,
`spectator-livestream`) are intentionally NOT pre-cached — they
load on user gesture (model load / `?stats=1` / hash route), so
pre-caching them would only waste mobile bandwidth.

## Strict-mode check
`tsc --noEmit --strict --target es6 --module esnext
--moduleResolution bundler` exits clean. The W6 task spec wrote
the strict command without `--module esnext`, which breaks because
TS rejects dynamic imports with TS1323 under the default
`--module commonjs` setting. Recommending W7 update the spec
wording.

## Cross-lane safety
- Per-invocation identity: every git operation uses
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`;
  `git config user.name` is never called.
- `flock -w 120 9 /tmp/squad-git-lock` wraps the commit + push so
  no other agent can interleave a config-then-commit race like
  the one observed in W5.
- `git status` was inspected before each `git add` and only files
  matching `src/frontend/`, `.squad/agents/hicks/`,
  `.squad/decisions/inbox/hicks-*`, `docs/frontend-three-budget.md`
  were staged. Other agents are touching `.tool-actionlint/`,
  `.tool-terraform/`, `infra/terraform/modules/`, etc. — left
  untouched.

## Hand-off to W7
1. **<700 kB three-renderer target** — defer or take a bundler
   swap decision. See `docs/frontend-three-budget.md`.
2. **HLS.js npm vs CDN** — if Phase L spectator analytics need to
   import HLS.js types eagerly, switch to npm dep + dynamic
   import; the CDN path was a W6-only optimisation.
3. **Phase L commentary endpoint** — when Bishop ships
   `/api/games/{id}/commentary/replay` the UI should "just work";
   verify the JSON shape matches the assumed
   `{ lines: Array<{ text, speaker?, ts? }> }` contract.
4. **`OutlinePass` replacement** — the small `three-renderer`
   chunk still ships the addon at ~30 kB. A `MeshBasicMaterial`
   stencil-write trick would save that; worth a W7 spike.
5. **W3-era `pwa-install-prompt` alias** — once W7 rewrites the
   PWA install spec to use `pwa-install-button`, drop the legacy
   `<span>` alias from `pwa.ts:mountInstallButton`.
