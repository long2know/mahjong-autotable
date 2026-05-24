# Hicks — Phase K Wave 19 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 19
**Branch:** `stlong/phase-k-wave-19-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. Re-evaluate LH13 §6.8 evidence gate against `main` post-W18-merge.
   Promote to hard-pin GREEN if ≥3 consecutive successful schedule-
   event runs are observed; otherwise hold YELLOW with explicit
   blocker.
2. Phase L renderer — W4 canonical wall geometry (4 × 18 × 2 layout
   + dora indicator slot) wired into the `renderer-webgl2` chunk's
   `TileMesh` path, plus the three camera modes (orbital, isometric-
   flat, perspective-three-quarter) the production renderer needs
   to switch between.
3. Bundle audit §3.4 — shed bytes from `autotable-src-eager` to hit
   the ≤145 KB ceiling (down from 156,577 B at W18 close).
4. Hold `three-renderer-big` at ≤ 406,635 B (W19 ceiling = W18 close).
5. Admin UI — wire the three Bishop W19 surfaces:
   `rotation-policy-bulk`, `replay-integrity-audit`,
   `swiss-pairing-audit` (read-only).

---

## Bundle outcomes (W19 final, vs W18 baseline)

| Chunk | W18 baseline | W19 target | W19 result | Δ vs W18 | Status |
|---|---|---|---|---|---|
| `autotable-src-eager` | 156,577 B | ≤145,000 B | **144,192 B** | −12,385 B | ✅ under ceiling by 808 B |
| `three-renderer-big` | 406,635 B | ≤406,635 B | **406,635 B** | 0 B | ✅ held exactly |
| `renderer-webgl2` | 25,666 B | ≤45,000 B | **30,174 B** | +4,508 B | ✅ wall-geometry + camera modes |
| `admin-panel` | 18,411 B | ≤32,000 B | **26,701 B** | +8,290 B | ✅ 3 new W19 surfaces |
| `matchmaking` *(new lazy)* | (in eager) | new lazy | **2,642 B** | extracted | ✅ |
| `rule-presets` *(new lazy)* | (in eager) | new lazy | **9,712 B** | extracted | ✅ |
| `stats-module` *(new lazy)* | (in eager) | new lazy | **3,227 B** | extracted | ✅ |

All targets met. `dist-size.json` carries the W19 row under wave-name
`K19` (recorded by `scripts/append-dist-size.js` automatically on
build).

---

## Deliverable #1 — LH13 §6.8 evidence-gate re-evaluation

**Decision: HOLD §6.7 YELLOW.  Do NOT promote to hard-pin GREEN.**

**Evidence collected** (via `gh run list -w pwa-audit.yml -L 30
--json conclusion,event,headBranch,headSha,createdAt,databaseId`):

| Event | Branch | Conclusion | Count | Notes |
|---|---|---|---|---|
| `schedule` | `main` post-W18-merge | success | **0** | the sample window remains empty for schedule-event ticks against the post-W18 `main` tree |
| `workflow_dispatch` | `main` post-W18-merge | success | 1 | SHA `7832f498`, 10:46:31Z — proves workflow file is healthy |
| `pull_request` | `stlong/phase-k-wave-18-bringup` | success | 1 | pre-merge PR run |
| `push` / `schedule` | `stlong/phase-k-wave-18-bringup` | success | 2 | pre-merge branch runs (frozen) |

**Convergence criterion** (LH13 §4.2): "≥3 consecutive successful
schedule-event runs against the candidate workflow tree".  Required
sample is 3; observed sample is 0.  Criterion **not met**.

**Rationale** — manual `workflow_dispatch` runs prove the workflow
file works but do not satisfy §4.2 because the §4 audit chain is
specifically about the GitHub-Actions cron *scheduler* producing
green builds.  We need actual scheduled ticks against the post-W18
`main` tree.  At hourly cron cadence we should have a fair sample
window by W20 bring-up (~14 ticks).

**YELLOW indicator** is documented at `docs/lh13-soft-pin-rationale.md`
§10 (newly added in this wave).  The §6.7 row reads YELLOW because
the W18 remediation is *on `main`* (improvement vs W17/W18 RED) but
the sample window has not yet opened — a strict GREEN flip would be
premature.

**Re-check trigger** — next-wave (W20) bring-up agent re-runs §4.2
with a wider sample window.  No earlier action required.

---

## Deliverable #2 — Phase L renderer wall geometry + camera modes

**Files touched (Hicks lane only — `src/frontend/autotable-src/`
*excluding* `tests/` per `tests/ci/lane-map.json`):**

* `src/renderer-webgl2/wall-geometry.ts` (NEW, 311 lines) — canonical
  4-wall × 18-stack × 2-tile Changsha layout + dora indicator
  slot.  Exports:
  - `populateCanonicalWall(mesh, tileIds)` — writes 144 instances to
    a `TileMesh` (see `tile-mesh.ts`); the per-instance face index is
    the `tileFaceForId()` mapping from W18's face-catalogue.
  - `populateWallWithDora(mesh, seed)` — convenience entry that
    deterministically shuffles a 144-tile set (W17 + flowers/seasons
    optional) and places one dora indicator on top of the east wall.
  - `wallTileMatrix(side, stack, layer)` — model-matrix lookup so
    the picker can recover the world-space centre of any slot.
  - `iterateWallSlots()`, `wallSlotCentre(side, stack, layer)` —
    helpers used by `hello.ts` mountWall smoke + future picking.
  - `canonicalTileIds()`, `shuffleTileIds(seed)` — deterministic
    tile-id sources (mahjong canon: 27 suits × 4 + 7 honours × 4 +
    8 flowers/seasons × 1 = 144).
  - `CANONICAL_WALL_TILE_COUNT = 144`.

* `src/renderer-webgl2/camera.ts` (MODIFIED) — three new W19 surfaces:
  - `CameraMode` type — `'orbital' | 'isometric-flat' |
    'perspective-three-quarter'`.
  - `CameraProjection` type — `'perspective' | 'orthographic'`.
  - `CAMERA_MODE_PRESETS` — per-mode `{ projection, fovYRadians,
    orthoSize, elevation, azimuth, distance }` defaults.
  - `applyCameraMode(cam, mode)` — switches all four state fields in
    one call (no UI ping-pong on the picker).
  - `orthographic4(out, halfWidth, halfHeight, near, far)` — adds
    the orthographic projection matrix path the iso-flat mode needs.
  - `OrbitCamera` interface extended with `mode`, `projection`,
    `fovYRadians`, `orthoSize` fields.  `createOrbitCamera()` now
    defaults to `'orbital'` + `'perspective'` so existing W15/W16/W17
    smoke entry points keep their current look-and-feel.
  - `projectionMatrix()` signature changed from `(canvas, fovY, near,
    far)` to `(cam, canvas, near, far)` — branches on
    `cam.projection`.  No external callers in the eager bundle
    (the `projectionMatrix` reference in `src/render/custom-outline.ts`
    is a GLSL identifier string, not a JS call); only `hello.ts`
    internal callsite updated.

* `src/renderer-webgl2/hello.ts` (MODIFIED) — added the W19 wall smoke:
  - New mount mode `'wall'` (triggered by `?renderer=webgl2-wall`).
  - `mountWall()` allocates a `TileMesh`, calls `populateWallWithDora`,
    and wires a per-canvas camera-mode picker (`installCameraMode
    Picker`) so a developer can switch between orbital / iso-flat /
    3/4 perspective from the URL-loaded smoke without recompiling.

* `src/index.ts` (MODIFIED, 1-line regex change) — added `wall` to the
  `webgl2-(hello|tile-mesh|scene|wall)` URL pattern so the renderer-
  webgl2 chunk is only loaded when a smoke URL is asked for.  Lobby
  cold path stays unchanged.

**Deferred to Vasquez** — the brief mentions an e2e spec stub at
`src/frontend/autotable-src/tests/e2e/phase-l-wall-w19.spec.ts`, but
`tests/ci/lane-map.json` assigns `^src/frontend/autotable-src/tests/`
to the Vasquez lane.  The W18 retrospective tightened the
cross-lane-bundling check after Apone scooped Hicks tree edits;
following the tightened lane discipline, this stub is left to
Vasquez to author in the same wave or W20.  See §6.6 of the W18
retrospective for the rationale.

---

## Deliverable #3 — Bundle audit §3.4 surgery (`autotable-src-eager` ≤ 145 KB)

**Three lazifications** in `src/lobby.ts`:

1. **`matchmaking`** (was eager, now ~2.6 KB lazy chunk) —
   `installPublicGamesPane` and `installMakePublicToggle` now accept
   the matchmaking module as a parameter; new schedulers
   `schedulePublicGamesPaneLazyMount()` and
   `scheduleMakePublicToggleLazyMount()` wire mouseenter/focus/click
   listeners on the public-games tab + the make-public toggle.
   First activation triggers `loadMatchmaking()` (a memoised
   `import('./matchmaking')`).  The tab-activate handler in
   `installLobbyTabs.activate()` mirrors the W17 leaderboard pattern:
   `if (isPub) { void loadMatchmaking().then(start) }` on activate;
   `else if (_matchmakingMod !== null) _matchmakingMod.stopPolling()`
   on deactivate (skip the import if the module was never loaded).

2. **`rule-presets`** (was eager, now ~9.7 KB lazy chunk) — replaced
   `installRulePresetsUi()` boot call with
   `scheduleRulePresetsUiLazyMount()`, which uses
   `requestIdleCallback` (fallback `setTimeout(0)` for Safari) to
   defer the import.  The URL-builder's `getSelectedPresetId()` call
   is replaced by an inline LS read
   (`readSelectedPresetIdInline()` — same key
   `mahjong.rule-preset.selected.v1`, same default
   `'classic-changsha'`, same try/catch guard) so the URL still emits
   `?rulePreset=` for non-default selections without dragging in
   the editor surface.

3. **`stats`** (was eager, now ~3.2 KB lazy chunk) — `formatStats`
   import lazified through `loadStatsModule()`.
   `renderLobbyStatsPanel()` paints the displayName heading
   immediately on populated profiles, then dynamic-imports the
   formatter and replaces the panel content in place once the
   chunk lands.  Empty/loading state (no profile yet) never pulls
   the chunk.

**Outcome:** eager dropped from 156,577 B (W18 close) to 144,192 B
(W19) — a 12,385 B reduction, 808 B under the §3.4 ≤145,000 B ceiling.

**No CDN behaviour changes.**  Each new lazy chunk is hash-named by
the Vite `chunkFileNames` template; service-worker precache picks
them up via `manifest-precache.json`.  No URL contracts change.

---

## Deliverable #4 — `three-renderer-big` hold

W18 close: 406,635 B.  W19 result: **406,635 B (held exactly).**

Hicks made no edits to `src/render/`, `src/scene/`, or any module
routed into the `three-renderer-big` chunk by
`vite.config.ts:manualChunks`.  The bit-exact hold was
verified by re-reading the K19 row in `dist-size.json`.

---

## Deliverable #5 — Admin UI for 3 W19 Bishop surfaces

All three surfaces follow the `AdminSurfaceSpec<TRow,TBody>` pattern
established in W17 (`admin-shared.ts`).  Each lands in the
`admin-panel` chunk via `manualChunks` (`src/admin/` → admin-panel).

* `src/admin/rotation-policy-bulk.ts` (NEW, ~150 lines) —
  `ROTATION_POLICY_BULK_SPEC`.  Per-tenant bulk update of dealer +
  wind rotation policies (`DealerRotation: 'east' | 'winner' |
  'random'`; `WindRotation: 'tournament' | 'four-rounds' |
  'eight-rounds' | 'unlimited'`).  REST: `POST
  /api/admin/rotation-policy/bulk-update`.

* `src/admin/replay-integrity-audit.ts` (NEW, ~180 lines) —
  `REPLAY_INTEGRITY_AUDIT_SPEC`.  Replay-store integrity sweep:
  per-replay status badges (green: hash-match, yellow: missing
  upload, red: hash-mismatch), per-tenant rollup.  REST: `POST
  /api/admin/replay-integrity-audit/sweep`,
  `GET /api/admin/replay-integrity-audit/{tenantId}`.

* `src/admin/swiss-pairing-audit.ts` (NEW, ~170 lines) —
  `SWISS_PAIRING_AUDIT_SPEC`.  Read-only audit log of Swiss tournament
  pairings (per round / per pairing seed).  Composite row-key
  `${tournamentId}:${round}:${pairingKey}`.  Read-only is signalled
  via `fields: []` — `admin-shared.ts:renderAdminListHtml` was
  extended in this wave to suppress the per-row Edit/Delete buttons
  and the Actions column header when `spec.fields.length === 0`;
  `admin-panel.ts:renderSurfaceFrame` was extended to suppress the
  Create button on the same gate.

* `src/admin/admin-panel.ts` (MODIFIED) — imports the three new
  SPECs; the `SURFACES` registry now has 6 entries (W17 = 3 +
  W19 = 3).  Read-only surface gating added per the bullet above.

* `src/admin/admin-shared.ts` (MODIFIED) — `renderAdminListHtml`
  read-only path (see above).

---

## W18 → W19 changes summary

| Path | Action |
|---|---|
| `src/renderer-webgl2/wall-geometry.ts` | created (NEW) |
| `src/renderer-webgl2/camera.ts` | extended (W19 camera modes) |
| `src/renderer-webgl2/hello.ts` | extended (wall smoke + picker) |
| `src/index.ts` | regex updated (1-line) |
| `src/lobby.ts` | bundle §3.4 surgery (3 lazifications) |
| `src/admin/admin-panel.ts` | 3 new surfaces + read-only gate |
| `src/admin/admin-shared.ts` | read-only render gate |
| `src/admin/rotation-policy-bulk.ts` | created (NEW) |
| `src/admin/replay-integrity-audit.ts` | created (NEW) |
| `src/admin/swiss-pairing-audit.ts` | created (NEW) |
| `dist-size.json` | K19 row appended (automatic) |
| `docs/lh13-soft-pin-rationale.md` | §10 W19 HOLD added |

---

## E2E spec deferral note → Vasquez

Vasquez owns `^src/frontend/autotable-src/tests/`.  The
`tests/e2e/phase-l-wall-w19.spec.ts` stub the brief mentions should
land in Vasquez's W19 commit, not Hicks's.  Suggested coverage:
mount `?renderer=webgl2-wall`, assert canvas non-empty, switch
camera modes via the picker, assert pixel-region change between
modes.  Hicks's `mountWall()` and `installCameraModePicker()`
helpers expose stable `data-testid` hooks
(`webgl2-wall-canvas`, `webgl2-camera-mode-picker`,
`webgl2-camera-mode-orbital` etc.) for those assertions.

---

## Lane discipline statement

All Hicks files in this wave live strictly under
`src/frontend/autotable-src/` *excluding* `tests/`.  The
cross-lane-bundling check is expected to pass; if Apone's
W19 staged tree (visible in the working tree at bring-up but
*not* in any of my edits) crosses into my lane, that's an
Apone-side concern surfacing through the same check — not a
Hicks regression.
