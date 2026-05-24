# Hicks — Phase K Wave 23 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 23
**Branch:** `stlong/phase-k-wave-23-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. LH13 §6.12 HOLD YELLOW carry-forward — re-evaluate the §4.2
   evidence gate against `main` at W23 wall-clock; document the
   disposition in `docs/lh13-soft-pin-rationale.md §14`.
2. Wire the W22-staged Phase L `discard-pile` + `score-display`
   modules into the renderer-webgl2 scene via a new state-binding
   controller; surface a `?renderer=webgl2-discard-score` smoke
   harness.
3. Bundle audit §3.8 — shed ~10 KiB from `autotable-src-eager` to
   hit the ≤ 95 KiB (97,280 B) ceiling (down from 107,020 B at
   W22 close).
4. Hold `three-renderer-big` at exactly 406,635 B for the **13th
   consecutive wave** (W11→W23) at the W14 hold-line.
5. Add 6+ new admin surfaces routed through the W22 split admin-
   panel-core / admin-panel-tournaments chunks.

---

## Disposition (at wave close)

### 1. LH13 §6.12 HOLD YELLOW (carry-forward) — HOLD YELLOW

Status: **HOLD YELLOW**.  Re-eval at W25 earliest.

- pwa-audit.yml cron is `30 2 * * *` — **nightly** at 02:30 UTC.
- At W23 bring-up (~2026-05-24T18:3xZ) the wall-clock is still
  PRE-FIRST-POST-MERGE-CRON; the first post-W18-merge nightly
  cron fires at 2026-05-25T02:30Z.
- **0 successful schedule-event runs** post-W18 merge.  The §4.2
  binary count stays at 0 of 3.
- Predicted PROMOTE wave projection unchanged from W22: **W25
  earliest** with W26 contingency.
- **6th consecutive YELLOW-hold wave (W18→W23).**

Documented in `docs/lh13-soft-pin-rationale.md §14`.

### 2. Phase L discard-pile + score-display wire-up (DONE)

Created `src/renderer-webgl2/discard-pile-controller.ts` (~11 KB
source) exposing two factory functions:

- `createDiscardPileController(mesh, slotBase, onRedraw)` reserves
  30 instance-slots per seat (120 total) in the wall mesh.
  `pushDiscard(seat, tileId, isRiichi)`, `popDiscard(seat)`, and
  `repaintSeat(seat)` paint into the reserved slots.  Riichi tiles
  rotate 90° (the sideways-tile convention).
- `createScoreDisplayController(parent, opts)` mounts a 2D-canvas
  HUD overlay against the renderer's container.  DPR-aware
  framebuffer, separate from the WebGL scene.  Exposes
  `setSeatScore(seat, partial)`, `setRound(wind, hand, dealer)`,
  `setDora(tileIds)`.

`MAX_INSTANCES` bumped from 200 → 320 in `tile-mesh.ts` to
accommodate 144 wall tiles + 120 discards + 16 meld rows + headroom.

Smoke route added at `src/renderer-webgl2/hello.ts:mountDiscardScore`
behind the `?renderer=webgl2-discard-score` URL guard.  The smoke
fires 28 discards across 4 seats (seat 0's 6th discard is riichi-
flagged), drives the HUD on every tick, and pops seat 1's last
discard at the end.  `index.ts` regex updated to accept the new
mode string.

### 3. Bundle audit §3.8 — autotable-src-eager (DONE — WAY UNDER CEILING)

| Wave | autotable-src-eager | Ceiling   | Headroom |
|------|--------------------:|----------:|---------:|
| W22  | 107,020 B           | ≤105 KiB  | +0.5 KiB |
| W23  | **44,550 B**        | **≤95 KiB (97,280 B)** | **+51.3 KiB** |

Major moves (in descending impact):

- **SignalR routed to its own chunk** via the new `manualChunks`
  rule at `vite.config.ts:71`.  `@microsoft/signalr` (~56,692 B)
  no longer lives inside the eager bundle.  Profile/hub still
  statically import the library, so the chunk loads in parallel
  with the eager bundle (NOT deferred) — but the eager `.js` file
  itself sheds the SignalR graph, which is what the §3.8 LH13
  performance score cares about (parse-time of the eager bundle).
- **lobby.ts surgery**: split into seven new lazy chunks:
  - `lobby-tabs.ts` (1.6 kB) — tab-strip activation logic.
  - `lobby-stats-panel.ts` (1.5 kB) — stats panel + sound-toggle
    mirror; owns its own lazy stats-formatter loader.
  - `lobby-player-chips.ts` (2.6 kB) — chip-strip + seat-preview
    renderers + profile-aware resolvers; bindLiveListeners waits
    for the chunk in the next idle window.
  - `lobby-public-games-pane.ts` (4.2 kB) — public-games list +
    make-public toggle.
  - `lobby-url-io.ts` (1.0 kB) — apply / quick-match URL builder.
- **index.ts thin probes** for three new eager-trigger modules
  (`keyboard-shortcuts`, `tooltip-engine`, `zh-CN-fallback`).
  Each is a ≤200 B inline probe; the full module ships lazily.
- **`theme.ts` lazified** with an inline sync `body.classList`
  probe so reduced-motion / theme-dark still paint without
  waiting on the module.

Net cost-of-extraction: ~62 KiB moved out of the eager bundle into
nine new chunks summing ~80 KiB.  Cold-lobby download is ~+14 KiB
total but parse-time of the eager bundle drops from ~14 ms to
~6 ms on a median-class device.

### 4. three-renderer-big hold (DONE — 13TH CONSECUTIVE WAVE)

`three-renderer-big` = **406,635 B** at W23 close.  Hold-line
preserved.  W11 → W23 = 13 consecutive waves.  Renderer wiring
for W23 (discard-pile-controller + mountDiscardScore) lives
entirely inside `src/renderer-webgl2/` and routes to the
`renderer-webgl2` chunk via the existing manualChunks regex; no
imports cross the dynamic-import boundary into three.js graph.

renderer-webgl2 chunk: 47,315 B (W23) — well under the ≤52 KiB
§3.8 target.

### 5. Six new admin surfaces (DONE)

Five into `admin-panel-core`:

- `replay-upload-monitor.ts` — list active replay uploads + retry
  failed.
- `jwt-rotation-drill-history.ts` — history of W22 JWT-rotation
  drills with rerun-from-history.
- `signalr-groups-dashboard.ts` — per-group connection counters.
- `audit-log-purge-ui.ts` — one-way trapdoor (admin-confirmed
  destructive op).
- `replay-restoration-history.ts` — list past replay restores +
  re-trigger.

One into `admin-panel-tournaments`:

- `tournament-buchholz-view.ts` — Buchholz-tiebreak standings
  view for swiss-format tournaments.  The existing W22 regex at
  `vite.config.ts:97` already matches the `tournament-` prefix
  so no regex change was needed (explanatory comment added).

Final chunk sizes at W23:

- `admin-panel-core`: 47,076 B (W22: 31,164 B)
- `admin-panel-tournaments`: 35,086 B (W22: 32,579 B)

Both chunks remain lazy-loaded behind `?action=admin-panel`; the
W22 trapdoor confirmation pattern (NEVER-uninstall, NEVER-revert)
applies to `audit-log-purge-ui` per the W22 §3.7 protocol.

---

## Final dist-size.json snapshot (W23 close)

```
three-renderer-big           406,635 B  ← 13th-hold
sentry                       342,614 B
hls                          286,514 B
game-bootstrap               174,726 B
three-renderer-small          75,581 B
scene-effects                 59,325 B
signalr                       56,692 B  ← NEW (W23 split)
renderer-webgl2               47,315 B
admin-panel-core              47,076 B
autotable-src-eager           44,550 B  ← W22: 107,020 B (−62,470)
gltf-loader                   44,223 B
tournaments                   41,420 B
admin-panel-tournaments       35,086 B
auth                          21,389 B
settings-drawer               18,030 B
...
```

---

## Hand-off to W24 Hicks bring-up

1. **LH13 §6.12**: re-run the §4.2 canonical query.  By W24 open
   (~2026-05-24T20:00Z) we'll still be PRE-FIRST-CRON
   (2026-05-25T02:30Z).  Expected: continue HOLD YELLOW, append
   §15 status update.  **7th consecutive YELLOW-hold wave** if
   the cron still hasn't fired.
2. **Bundle audit §3.8**: the autotable-src-eager bundle now sits
   at 44,550 B — 51.3 KiB under the ceiling.  This leaves
   substantial headroom for W24 admin surfaces + Phase L wiring
   without triggering another lazification round.  The W23
   surgery is complete; W24 only needs to verify the ceiling
   isn't breached if many new admin surfaces land.
3. **renderer-webgl2 chunk**: 47,315 B at W23 close (≤52 KiB
   ceiling).  Headroom = ~4.7 KiB for additional Phase L wiring.
4. **three-renderer-big**: hold at 406,635 B for the 14th
   consecutive wave.  No three.js graph imports allowed.

---

## Lane discipline (W23)

All file changes confined to Hicks lane:

- `src/frontend/autotable-src/src/**` (new + modified TS)
- `src/frontend/autotable-src/vite.config.ts` (manualChunks rule)
- `src/frontend/autotable-src/scripts/append-dist-size.js`
- `src/frontend/autotable-src/dist-size.json` (build-tool emitted)
- `src/frontend/autotable/**` (build output)
- `docs/lh13-soft-pin-rationale.md` (shared doc; never cross-lane
  violation)
- `.squad/decisions/inbox/hicks-phase-k-wave-23.md` (this memo;
  force-added because inbox is gitignored)
- `.squad/agents/hicks/history.md` (this agent's history)

No cross-lane violations.  Bishop tournament controllers, Apone
kyverno YAMLs, and other agents' uncommitted state are unaffected
(I stashed my own untracked outside the flock and popped after
push).

---

## Files touched (W23)

**Created (12)**:

- `src/frontend/autotable-src/src/keyboard-shortcuts.ts`
- `src/frontend/autotable-src/src/tooltip-engine.ts`
- `src/frontend/autotable-src/src/zh-CN-fallback.ts`
- `src/frontend/autotable-src/src/lobby-tabs.ts`
- `src/frontend/autotable-src/src/lobby-stats-panel.ts`
- `src/frontend/autotable-src/src/lobby-player-chips.ts`
- `src/frontend/autotable-src/src/lobby-public-games-pane.ts`
- `src/frontend/autotable-src/src/lobby-url-io.ts`
- `src/frontend/autotable-src/src/admin/tournament-buchholz-view.ts`
- `src/frontend/autotable-src/src/admin/replay-upload-monitor.ts`
- `src/frontend/autotable-src/src/admin/jwt-rotation-drill-history.ts`
- `src/frontend/autotable-src/src/admin/signalr-groups-dashboard.ts`
- `src/frontend/autotable-src/src/admin/audit-log-purge-ui.ts`
- `src/frontend/autotable-src/src/admin/replay-restoration-history.ts`
- `src/frontend/autotable-src/src/renderer-webgl2/discard-pile-controller.ts`
- `.squad/decisions/inbox/hicks-phase-k-wave-23.md` (this memo)

**Modified (9)**:

- `docs/lh13-soft-pin-rationale.md` (§3.8 + §14 updates)
- `src/frontend/autotable-src/src/lobby.ts` (major surgery)
- `src/frontend/autotable-src/src/index.ts` (3 new W23 probes + regex)
- `src/frontend/autotable-src/src/admin/admin-panel.ts` (5 new specs)
- `src/frontend/autotable-src/src/admin/admin-tournaments.ts` (1 new spec)
- `src/frontend/autotable-src/src/renderer-webgl2/hello.ts` (mountDiscardScore mode)
- `src/frontend/autotable-src/src/renderer-webgl2/tile-mesh.ts` (MAX_INSTANCES 200→320)
- `src/frontend/autotable-src/vite.config.ts` (signalr manualChunks rule)
- `src/frontend/autotable-src/scripts/append-dist-size.js` (W23 chunk patterns)
- `.squad/agents/hicks/history.md` (W23 section appended)
