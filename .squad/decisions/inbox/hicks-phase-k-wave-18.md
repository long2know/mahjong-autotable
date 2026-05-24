# Hicks — Phase K Wave 18 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 18
**Branch:** `stlong/phase-k-wave-18-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. Wire the admin UI for Bishop's W17 CRUD surfaces (replay-retention,
   per-tenant JWKS rotation, SignalR retention).
2. Wire the canonical tile atlas PNG into the Phase L renderer-webgl2
   path (W17 loaded the PNG; W18 needs the suit/value catalogue).
3. §3.3 bundle surgery — shed ~12 KB from `autotable-src-eager` to
   close the gap to the ≤165 KB target.
4. Hold `three-renderer-big` ≤ 406,635 B.
5. LH13 calibration status — re-run §4.2 evidence gate, document.

---

## Bundle outcomes (W18 final, vs W17 baseline)

| Chunk | W17 baseline | W18 target | W18 result | Δ vs W17 | Status |
|---|---|---|---|---|---|
| `autotable-src-eager` | 176,907 B | ≤165,000 B | **156,577 B** | −20,330 B | ✅ shed 20.3 KB (target was 12 KB) |
| `three-renderer-big` | 406,635 B | ≤406,635 B | **406,635 B** | 0 B | ✅ held exactly |
| `renderer-webgl2` | 24,743 B | ≤45,000 B | **25,666 B** | +923 B | ✅ +923 B from face catalogue |
| `admin-panel` | (didn't exist) | ≤40,000 B | **18,411 B** | new chunk | ✅ 54% headroom |
| `spectator-follow` | (in eager) | new lazy | **3,535 B** | extracted | ✅ |
| `reconnect` | (in eager) | new lazy | **3,067 B** | extracted | ✅ |
| `pwa` | (in eager) | new lazy | **2,320 B** | extracted | ✅ |

All targets met or beaten. `dist-size.json` carries the W18 row
under wave-name `K18` (recorded by `scripts/append-dist-size.js`).

---

## Deliverable #1 — Admin UI for 3 W17 CRUD surfaces

### Files added
- `src/frontend/autotable-src/src/admin/admin-shared.ts` — shared
  helpers: `AdminSurfaceSpec` interface, `gateAdminFetch()` auth
  ladder (401→403→503→200), `promptAdminReason()` via
  `window.prompt`, generic list/form/row renderers,
  `injectAdminPanelStyles()` (idempotent stylesheet inject).
- `src/frontend/autotable-src/src/admin/replay-retention.ts` —
  `REPLAY_RETENTION_SPEC` targeting `/api/admin/replays/retention`.
  Writes require `X-Admin-Reason` (mirrors Bishop's W17 controller).
- `src/frontend/autotable-src/src/admin/jwks-rotation.ts` —
  `JWKS_ROTATION_SPEC` targeting
  `/api/admin/jwks-rotation/per-tenant`.  Writes do **not** require
  `X-Admin-Reason` (confirmed by reading the W17 controller).
- `src/frontend/autotable-src/src/admin/signalr-retention.ts` —
  `SIGNALR_RETENTION_SPEC` targeting `/api/admin/signalr/retention`.
  Writes require `X-Admin-Reason`.
- `src/frontend/autotable-src/src/admin/admin-panel.ts` —
  `openAdminPanel()` entry point.  Renders a single overlay with a
  tab strip across the 3 specs; each tab independently runs the
  shared list/form renderer.

### Wiring
- `vite.config.ts:manualChunks` — added rule that bundles
  `src/admin/` into a single `admin-panel` chunk (matches the
  `renderer-webgl2` rule pattern).
- `src/action-router.ts` — added `admin-panel` to
  `SUPPORTED_ACTIONS`; added `dispatchAdminPanel()` +
  `gateAndMountAdminPanel()` helpers (lazy-import + gate ladder);
  added the switch case to route `?action=admin-panel` through
  the lazy mount.
- `scripts/append-dist-size.js` — extended `KEY_PATTERNS` with 4
  new entries (`admin-panel`, `pwa`, `reconnect`, `spectator-follow`)
  so the W18 row tracks the new lazy chunks.

### Auth ladder contract (matches Bishop's W17 controllers)

| Response | Meaning | UI behaviour |
|---|---|---|
| 401 | No session | Toast: "Sign in required."  Close overlay. |
| 403 | Not an admin | Toast: "Admin role required."  Close overlay. |
| 503 | Per-tenant flag disabled | Toast banner inside overlay: "This admin surface is disabled for your tenant."  List still renders empty. |
| 200/201/204 | Success | Refresh list. |

### Selectors for Vasquez's follow-up Playwright spec

The W18 plan asked for `tests/e2e/admin-panel-w18.spec.ts` —
that path is in **Vasquez's lane** (per
`tests/ci/lane-map.json`), so Hicks did NOT author it.  Selectors
are documented here so Vasquez can author the spec in a W19
follow-up without re-reading the source:

| Element | `data-testid` |
|---|---|
| Overlay root | `admin-panel-overlay` |
| Tab strip container | `admin-panel-tabs` |
| Tab button (per surface) | `admin-tab-${spec.id}` (e.g. `admin-tab-replay-retention`) |
| Active panel body | `admin-panel-body` |
| Surface list table | `admin-list-${spec.id}` |
| Row in surface list | `admin-row-${rowKey}` |
| "Add new" button | `admin-add-${spec.id}` |
| Form root (edit/create modal) | `admin-form-${spec.id}` |
| Form field input | `admin-field-${spec.id}-${fieldKey}` |
| Form submit button | `admin-submit-${spec.id}` |
| Reason-prompt overlay | `admin-reason-prompt` (window.prompt — may need stub) |
| Toast container | `admin-toast` |

**Recommended Vasquez test matrix:**
- 401 path: no session cookie → overlay closes + toast asserts.
- 403 path: non-admin session → overlay closes + toast asserts.
- 503 path: per-tenant flag off → overlay stays open, banner asserts.
- Happy path: each of the 3 surfaces — list renders, add row,
  edit row, delete row.  Assert `X-Admin-Reason` was sent on
  replay-retention + signalr-retention writes, NOT on jwks-rotation.

---

## Deliverable #2 — Atlas PNG wired into Phase L renderer

W17 already had `tile-atlas.ts:acquireTileAtlas()` loading
`/img/tiles-atlas-webgl2.auto.png` (192 × 2176 PNG, committed)
and uploading it to a WebGL2 texture; the shader
(`tile-mesh.ts:TILE_INSTANCE_FS`) already sampled by
`(faceCol + localUv) / (gridCols, gridRows)`.  What W17 lacked
was the **suit/value catalogue** — host-side code had no way to
say "row 14 = pin 6" without re-deriving the layout.

### Files added
- `src/frontend/autotable-src/src/renderer-webgl2/tile-faces.ts`
  — exports `TILE_FACES: ReadonlyArray<TileFace>` (34 entries),
  `TILE_FACE_COUNT`, `tileFace(id)`, `atlasUvForTile(...)` UV
  helper (mirrors the fragment shader for host-side consumers
  like picking overlays), `canonicalWallTileIds()` returning a
  136-entry `Uint8Array` for the canonical wall (flowers/seasons
  defer to Phase L W5).

### Files modified
- `src/renderer-webgl2/tile-atlas.ts` — refreshed the doc header
  to reflect W18 status (atlas asset committed + faces catalogue
  bridges shader ↔ host).  Pure comment-only change.
- `src/renderer-webgl2/hello.ts` — `mountTileMesh()` +
  `mountScene()` status text now reports
  `${instances} instances across ${TILE_FACE_COUNT} tile faces`;
  picked-tile status line now reads
  `Picked tile #N [m6 (man-6)] at world (...)` so a smoke test
  visibly confirms the catalogue is wired through.

Net bundle cost: +923 B in `renderer-webgl2`
(24,743 → 25,666; ≤45,000 ceiling has 19.3 KB headroom).

---

## Deliverable #3 — §3.3 bundle surgery

Target: shed ~12 KB from `autotable-src-eager` (176,907 → ≤165,000 B).
Actual shed: **20,330 B** (12 KB target overshot deliberately to
buy back the ~3 KB that the W18 admin-panel router additions
would otherwise have added to the eager chunk).

### Lazified modules

1. **`pwa.ts`** (2,320 B chunk) — `registerServiceWorker()` only
   runs when `'serviceWorker' in navigator`.  New
   `schedulePwaLazyMount()` in `src/index.ts` gates on that
   feature probe + defers the import via `requestIdleCallback`
   (with `setTimeout(0)` fallback).
2. **`reconnect.ts`** (3,067 B chunk) — was eagerly imported only
   so the `?rejoin=` URL probe could call `initRejoin()`.
   Replaced with `scheduleRejoinAndLobbyBoot()` in
   `src/index.ts` that:
   - probes `window.location.search` for `rejoin=`;
   - lazy-imports `./reconnect` only on a positive match;
   - **preserves the existing call-order contract** by chaining
     `initLobby()` after the rejoin import resolves (or
     immediately if no rejoin probe matched).
3. **`spectator-follow.ts`** (3,535 B chunk) — was eagerly
   imported by `src/lobby.ts` and unconditionally installed on
   every load.  Replaced with `scheduleSpectatorFollowLazyMount()`
   that gates on `?seat=-1` OR `?spectate` URL parameters, then
   lazy-imports the module.

### Risk notes
- **Rejoin ordering**: the old eager path imported `reconnect`
  at module top-level so `initRejoin()` could run synchronously
  inside the `?rejoin=` branch.  The new lazy path uses an
  async wrapper so `initLobby()` only runs after the rejoin
  module resolves; this introduces a microtask delay (~1ms) on
  the rejoin path only.  Non-rejoin loads are unaffected.
- **PWA registration**: the old eager path registered the SW
  inside the document-load handler.  The new path registers it
  inside an idle callback gated on the SW capability probe.
  Idle-callback timing on a cold reload may delay SW
  registration by ~50ms; acceptable since SW is for repeat
  loads, not first-paint.

---

## Deliverable #4 — `three-renderer-big` hold-line

**Result: 406,635 B exact — held to the byte.**  No work was
required; the W18 surgery touched `src/index.ts`, `src/lobby.ts`,
`src/admin/*`, and `src/renderer-webgl2/*` only — none of which
land in the `three-renderer-big` graph.  The W17 freeze of the
three-renderer module graph remains intact.

---

## Deliverable #5 — LH13 calibration status

**Action: HOLD soft-flip** (no change from W17).

Doc updated: `docs/lh13-soft-pin-rationale.md` §9 appended,
documenting that:
- The cron scheduler is still alive (≥1 schedule-event tick
  observed since W17) but no successful runs have landed (still
  0 of 3 required for convergence).
- Apone's W18 remediation (`--screenEmulation.mobile=false` in
  the Lighthouse invocation) is **staged in the working tree
  but not yet committed**.  W19 re-check is gated on Apone's
  commit landing + ≥3 scheduled ticks accumulating against the
  new code.
- §3, §4.2, §5, §6 unchanged for W18.

**GH token note:** the W18 agent shell does not carry `GH_TOKEN`,
so a fresh `gh run list --workflow=pwa-audit.yml` poll was not
possible.  The §9 table carries the W17 figure forward rather
than fabricate a count; the Coordinator should refresh from
authenticated CLI at audit time.

---

## Lane-discipline notes

- **`tests/e2e/admin-panel-w18.spec.ts` NOT authored** — `tests/`
  is Vasquez's lane (`tests/ci/lane-map.json`).  Selectors are
  documented above for Vasquez to consume in a W19 follow-up.
- **`.github/workflows/pwa-audit.yml` NOT modified** — workflows
  are Apone's lane.  The working tree shows uncommitted edits
  there (`--screenEmulation.mobile=false` + a batch of action
  SHA pins); these are Apone's W18 work and were deliberately
  excluded from Hicks's commit set.
- **Bishop's W17 backend controllers NOT modified** — Hicks
  consumed Bishop's surfaces but did not alter them.  Auth
  ladder + body shapes were verified by reading
  `src/backend/.../Admin*Controller.cs` files; no writes
  required.
- **All edits live under `src/frontend/autotable-src/src/` +
  `vite.config.ts` + `scripts/append-dist-size.js` +
  `docs/lh13-soft-pin-rationale.md`** plus this inbox memo.

---

## Hand-off checklist for the Coordinator

- [ ] Confirm `dist-size.json` W18 row is present (wave `K18`).
- [ ] Confirm `three-renderer-big` shows 406,635 B in the W18 row.
- [ ] Assign Vasquez the `admin-panel-w18.spec.ts` follow-up
      using the selector table above.
- [ ] Once Apone's pwa-audit.yml commit lands, queue the LH13
      §4.2 re-check for W19 bring-up (still owned by frontend).
- [ ] No outstanding code-review items from Hicks's side.

EOF
