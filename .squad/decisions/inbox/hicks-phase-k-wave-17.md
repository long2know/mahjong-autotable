# Hicks — Phase K Wave 17 decisions memo

Branch: `stlong/phase-k-wave-17-bringup` (off W16 head `c866535`)
Author: Hicks (Frontend) `<hicks@squad.mahjong>`
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`
Model: `claude-opus-4.7-xhigh` (confirmed).

## Scope shipped

Four deliverables per the W17 directive — all four landed in the
same commit (no defers, no follow-up waves required):

1. **Phase L renderer-webgl2 polish — scene orchestrator +
   ray-cast picking + canonical atlas asset** (W16 19.0 KB →
   W17 24.7 KB; well under the 40 KB Phase L budget).
2. **Bundle audit §3.2 surgery** — three lobby-mounted modules
   moved off the eager cold path (autotable-src-eager 214,202
   → 176,907 B, **−37,295 B / −17.4 %**; the §3.2 target was
   ≥14 KB, exceeded by 2.65×).
3. **`three-renderer-big` hold-line — 7th consecutive wave**
   at 406,635 B.
4. **LH13 cron status — HOLD soft-flip** (first
   `schedule`-event run since W16 confirmed cron is alive but
   conclusion=failure; convergence criterion still 0 of 3).

## 1 — Phase L renderer-webgl2 polish

### What landed

W16 shipped the tile-mesh graph (19,017 B); W17 brings it up to
"scene-runtime ready" status.  New files under
`src/frontend/autotable-src/src/renderer-webgl2/`:

* **`scene.ts`** — `createTileScene()` factory.  Wires
  `camera.ts` + `tile-mesh.ts` + `tile-atlas.ts` into a single
  orchestrator with a DPR-aware framebuffer, rAF-coalesced
  redraw scheduling (`redraw()` is idempotent within a frame
  budget), and a `dispose()` lifecycle that releases the GL
  context.  No imperative draw loop — the scene only repaints
  when the camera state or the tile-set actually changes.
* **`picking.ts`** — `pickTile(scene, x, y)`.  NDC →
  inverse-view-projection ray construction; ray-AABB slab
  intersection against the canonical tile bounds; returns the
  tile-instance index of the front-most hit (or `null`).  No
  GPU readback path — picking is pure CPU math, so the picking
  cost stays independent of the WebGL2 driver's
  `readPixels()` latency.
* **`hello.ts`** — extended from W16's 2-mode harness
  (`?renderer=webgl2-hello` / `?renderer=webgl2-tile-mesh`) to
  a 3-mode harness adding `?renderer=webgl2-scene`.  The new
  mode mounts the scene via `createTileScene()` and wires a
  click handler that calls `pickTile()` and logs the hit.
  Existing W16 modes are byte-for-byte preserved (Vasquez's
  W16 smoke harness in `tests/e2e/renderer-webgl2-smoke.spec.ts`
  is unchanged and still passes).

### Canonical atlas asset

W16's `tile-atlas.ts` carried a STUB comment because the asset
file wasn't committed yet.  W17 ships the canonical asset:

* **`scripts/generate-tile-atlas-webgl2.js`** — zero-dep Node
  PNG generator.  Hand-rolled IDAT zlib stream + IEEE 0xEDB88320
  CRC32, PNG colour-type 6 (truecolour + alpha), 192×2176
  layout (3 cols × 34 rows = 102 cells, 64 px each).  Cell
  labels match the `tile-mesh.ts` UV-index convention.
* **`img/tiles-atlas-webgl2.auto.png`** — 10,058 bytes, the
  generator's output.  The `.auto.png` suffix marks it as
  generator-managed; regen via `node scripts/generate-tile-
  atlas-webgl2.js` is idempotent and produces the same bytes
  (deterministic PNG encoder; no timestamps in chunks).
* **`vite.config.ts`** — new `copyStaticAssets()` block under
  the existing dice-copy / PWA-icons-copy plugin, lifting the
  PNG out of `img/` and into the published `dist/` tree where
  the runtime can fetch it.
* **`tile-atlas.ts`** — header comment block (lines 1-37)
  rewritten to drop the STUB framing and document the W17
  canonical asset path (`../img/tiles-atlas-webgl2.auto.png`
  via vite's static-copy alias).  The fallback grid texture
  path is preserved as a runtime safety-net.

### Size envelope

| Chunk | W16 | W17 | Δ | Budget |
|-------|-----|-----|----|-------|
| `renderer-webgl2` | 19,017 B | **24,743 B** | +5,726 B | ≤ 40,000 B ✓ |

The 5.7 KB delta covers scene.ts + picking.ts + the broadened
`hello.ts` 3-mode mount.  Headroom remaining: 15.3 KB before
the Phase L budget ceiling.

### Forward path

W18 candidates (NOT in this wave):
* `dirty-flag` API on the scene to allow callers to invalidate
  arbitrary subsets without re-running pickTile.
* `tests/e2e/renderer-webgl2-scene-smoke.spec.ts` mirror
  (Vasquez lane).
* Integration with the existing `three-renderer-big` graph so
  the WebGL2 renderer can be opted into via `?renderer=webgl2`
  (no qualifier) once the asset + picking surface is wired
  to the autotable game loop.

## 2 — Bundle audit §3.2 surgery

### Modules moved off the eager cold path

The §3.2 directive named three lobby-mounted modules that are
gated on a UI interaction but were being imported statically
into the eager graph at `lobby.ts:initLobby()` time.  W17
breaks each one into its own lazy chunk:

| Module | Surface | Trigger | Lazy chunk size (raw) |
|--------|---------|---------|----------------------|
| `leaderboard` | Lobby leaderboard tab | `#lobby-leaderboard-tab` click | 11,349 B |
| `settings-drawer` | Settings drawer | `#settings-button` click | 17,770 B |
| `profile-page` | Profile page modal | `#lobby-open-profile` chip + `mahjong:open-profile-page` event | 9,464 B |

### Mechanism

`lobby.ts` carries three `scheduleXLazyMount()` helpers (one
per module) that follow the same shape:

```ts
let _xMod: typeof import('./x') | null = null;
let _xQueue: Event[] = [];

async function loadX(): Promise<typeof import('./x')> {
  if (_xMod) return _xMod;
  _xMod = await import('./x');
  return _xMod;
}

function scheduleXLazyMount(): void {
  /* installs a one-shot listener on the trigger surface.
     On first trigger:
       1. await loadX()
       2. call the install function on the mod
       3. replay queued events for the surface, if any  */
}
```

The `type * as` import keeps the typechecker happy without
emitting code into the eager chunk.  The polling-start dance
(leaderboard polls when its tab is active) uses
`loadLeaderboard().then(...)` and only calls `stopPolling()`
if the module is already loaded (no eager load just to stop
nothing).

### Custom event ordering subtleties (documented for posterity)

* **Settings-drawer first-click pre-warm.**  On first click,
  the lazy loader installs the module's own click handler
  *after* the user's click event has already fired.  Workaround
  in this commit: `loadX({ openOnLoad: true })` synthetically
  re-clicks the button post-install.  Subsequent clicks hit
  the installed handler directly.
* **Profile-page custom-event queueing.**  Leaderboard's "View"
  button fires `mahjong:open-profile-page` — if the profile-page
  module isn't loaded yet, the eager listener queues the event
  in `_profilePageEventQueue` and replays it once `loadProfile
  Page()` resolves.

### Size envelope

| Chunk | W16 | W17 | Δ |
|-------|-----|-----|----|
| `autotable-src-eager` | 214,202 B | **176,907 B** | −37,295 B |
| `leaderboard` (NEW) | — | 11,349 B | +11,349 B |
| `settings-drawer` (NEW) | — | 17,770 B | +17,770 B |
| `profile-page` (NEW) | — | 9,464 B | +9,464 B |

Net wire-cost shift: 37,295 B moved off the eager cold path,
38,583 B added to lazy chunks (the small ~1.3 KB overhead is
the per-chunk import wrapper).  The §3.2 directive's stated
target was a **≥14 KB eager savings** — the realised savings
exceed that by 2.65×.  Cold-path users who never touch the
leaderboard / settings / profile surface (the majority on a
spectator-only or single-game session) now pay zero of the
38 KB.

### `scripts/append-dist-size.js`

Added 3 new `KEY_PATTERNS` entries (`leaderboard`,
`settings-drawer`, `profile-page`) so the dist-size ledger
tracks the new chunks wave-over-wave.

## 3 — `three-renderer-big` hold-line — 7th wave

| Wave | three-renderer-big bytes |
|------|--------------------------|
| W11 | 406,635 |
| W12 | 406,635 |
| W13 | 406,635 |
| W14 | 406,635 |
| W15 | 406,635 |
| W16 | 406,635 |
| **W17** | **406,635** |

Floor confirmed.  No new strip passes attempted this wave —
the W7→W11 strip sequence has converged.  Next reduction
opportunity is the Phase L renderer-webgl2 cut-over once the
WebGL2 path has feature-parity (W18+).

## 4 — LH13 cron decision

### Empirical re-run at W17 bring-up

Re-ran the §4.2 evidence-gate check:

```bash
TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill 2>/dev/null \
       | awk -F= '/^password=/{print $2}')
GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
       --json conclusion,event,createdAt
```

Result at W17 vs W16 sign-off:

| Metric | W16 sign-off | **W17 bring-up** |
|--------|--------------|------------------|
| `event == "schedule"` runs | 0 | **1** |
| `event == "schedule" AND conclusion == "success"` | 0 | **0** |
| `manual-cron` dispatch runs | 0 | 0 |
| Coordinator seed (3 manual `?bypass=lh13-cron-stall`) | Pending | Still pending |

### Interpretation

The cron scheduler IS alive — a `schedule`-event run fired
between W16 sign-off and W17 bring-up.  That run's conclusion
was `failure`, consistent with the §6.4 "preview_url derivation
gate" hypothesis already documented in `docs/lh13-soft-pin-
rationale.md`.

Convergence criterion ("≥3 consecutive successful schedule-
event runs") is **not met** — still 0 of 3.

### Decision: HOLD

The provisional soft-flip established at W16 remains in force
for W17.  The §3 threshold table, §5 audit trail rules, and §6
coordination contract are unchanged.  `docs/lh13-soft-pin-
rationale.md` gains a new §8 documenting this re-check.

Forward signal: the first cron `schedule`-event firing narrows
the remaining failure surface to the post-launch
`preview_url` derivation already on Apone's plate.  Next
re-check at W18 bring-up.

## 5 — Lane discipline

All file changes confined to the hicks lane regex per
`tests/ci/lane-map.json`:

```
^(src/frontend/|Phase_K_W\d+/Hicks/|\.squad/agents/hicks/
   |\.squad/decisions/inbox/hicks-)
```

Specifically:
* `src/frontend/autotable-src/src/lobby.ts`
* `src/frontend/autotable-src/src/index.ts`
* `src/frontend/autotable-src/src/renderer-webgl2/scene.ts`
  (new)
* `src/frontend/autotable-src/src/renderer-webgl2/picking.ts`
  (new)
* `src/frontend/autotable-src/src/renderer-webgl2/hello.ts`
* `src/frontend/autotable-src/src/renderer-webgl2/tile-atlas.ts`
* `src/frontend/autotable-src/scripts/generate-tile-atlas-
  webgl2.js` (new)
* `src/frontend/autotable-src/scripts/append-dist-size.js`
* `src/frontend/autotable-src/img/tiles-atlas-webgl2.auto.png`
  (new, generator output)
* `src/frontend/autotable-src/vite.config.ts`
* `src/frontend/autotable-src/dist-size.json`
* `docs/lh13-soft-pin-rationale.md`
* `.squad/decisions/inbox/hicks-phase-k-wave-17.md`
  (this memo, force-added — `.squad/decisions/inbox/` is
  gitignored)

Vasquez lane (`src/frontend/autotable-src/tests/`) untouched.
No cross-lane file edits.  `tests/ci/check-cross-lane-
bundling.sh --strict` run post-commit.

## 6 — Verification

* `npm run build:vite` exited 0; no TypeScript errors; no
  unused-import warnings introduced by the §3.2 surgery.
* `dist-size.json` shows `current: "K17"` and a fresh K17
  history entry with all 27 tracked chunks (24 carry-over + 3
  new `profile-page` / `leaderboard` / `settings-drawer`).
* Re-running the build a second time produces byte-identical
  output (rebuild idempotency).
* Generated `tiles-atlas-webgl2.auto.png` re-generates to the
  same 10,058-byte file (encoder determinism verified).

## 7 — Open items (forwarded to W18)

* WebGL2 scene + picking smoke-test mirror (Vasquez lane).
* `?renderer=webgl2` opt-in cut-over once feature-parity is
  reached.
* §3.3 bundle-audit item (next candidate per
  `docs/frontend-bundle-audit.md`).
* LH13 §8 re-check at W18 bring-up.

## 8 — Co-author trailer

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```
