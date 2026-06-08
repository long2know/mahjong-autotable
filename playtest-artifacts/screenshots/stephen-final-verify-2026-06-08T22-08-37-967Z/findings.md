# Stephen first-play audit — stephen-final-verify-2026-06-08T22-08-37-967Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T22:08:37.967Z
* Ended:   2026-06-08T22:12:52.787Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

❌ **NOT PLAYABLE FROM BARE URL** — 4 blocker(s) + 1 confusion(s).

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4710 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1274 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3182 ms
* ✅ Phase D: Apply & Start (navigate) — 4766 ms
* ✅ Phase D2: Click Connect (manual) — 3 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 2889 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 9998 ms
* ✅ Phase G: My hand populated — 1762 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3233 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 50 ms
* ✅ Phase H3: Install autoplay driver (banner-driven loop) — 538 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30344 ms
* ✅ Phase J: Claim window visibility — 20343 ms
* ✅ Phase K: Sustained play observation (60s) — 60329 ms
* ✅ Phase L: Final UI inventory — 1048 ms
* ✅ Phase N: Continuous play loop (90s, banner-driven) — 90735 ms
* ✅ Phase O: Banner + cursor proof (Hicks turn indicator) — 317 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19211 ms

## Phase N — Continuous play loop (90s, banner-driven)

* Loop window: 90665 ms
* Seat-0 emit_discard events: **0** (threshold ≥ 5)
* Total discards across all seats: **0** (threshold ≥ 25)
* Silent emit failures: 363
* Banner sightings (cumulative): discard=812, pickup=0, claim=1
* Per-seat discards in window: {"0":0,"1":0,"2":0,"3":0}
* Per-seat emits in window:   {"0":0,"1":0,"2":0,"3":0}
* Per-seat emits cumulative:  {"0":0,"1":0,"2":0,"3":0}
* Game completed during window: no
* Autoplay ticks: 813
* Timeline: `phase-N-timeline.jsonl`

## Phase O — Banner + cursor proof

* Captured snapshot (at first banner-discard sighting during Phase N):
  - bannerText: "Your turn — click a tile to discard"
  - bannerVisible: true
  - bannerClasses: ["turn-banner-discard","visible"]
  - bodyHasMyTurnDiscard: true
  - canvasCursor: "pointer"
  - selfSeat: 0

## P0 — Blockers (user cannot play)

### [P0] (I) Over 30s, NO bot at any other seat made a discard (delta=0). No pickup state present — bots may be misconfigured. moveLog entries=8.

* **Steps to reproduce:** After deal, wait 30s without any further user action. Expect bots to draw + discard; observed: zero progress.
* **Suspect file/owner:** BotEngine.cs in src/backend / bot-turn loop / turn rotation
* **Screenshot:** `phase-I-after-30s.png`

### [P0] (K) Over 60s of observation, total discards grew by 0 (from 3 to 3). Play has stalled — no bot is taking turns, no human discard registered, no win condition hit. The user is staring at a frozen 3D table. This is the "I waited and nothing happened" complaint and is likely a CASCADE from Phase H (the dealer-extra/discard-rejection issue) blocking the entire turn rotation.

* **Steps to reproduce:** 1) Bare URL → complete the lobby + connect + seat + deal flow. 2) Wait 60+ seconds without any further user action. 3) Observe: discards count does not advance, no bot draws or discards. The game is dead in the water.
* **Suspect file/owner:** Cascade from Phase H — fix the dealer-extra / silent-discard-rejection issue and the turn rotation should resume. Also recommend defaulting the lobby to Auto deal mode for first-time users (lobby.ts initLobby).
* **Screenshot:** `phase-K-final-60s.png`

### [P0] (N) Only 0 seat-0 emit_discard events in 90s (mission threshold ≥ 5). Banner-driven loop is not advancing the human turn reliably.

* **Steps to reproduce:** After H3 installs autoplay, drain window.__autoplay.events over a 90s window and count emit_discard events with seat===0.
* **Suspect file/owner:** src/frontend/autotable-src/src/game-ui.ts refreshTurnBanner / world.ts emitDiscard
* **Screenshot:** `phase-N-end.png`

### [P0] (N) Only 0 total discards across all seats in 90s (mission threshold ≥ 25, Bishop's 4-bot smoke saw 34–51). Bot cadence is degraded in mixed human/bot mode even with autoplay driving the human.

* **Steps to reproduce:** Count discard events (delta from cli.things hand→discard transitions) over the Phase N 90s window.
* **Suspect file/owner:** ChangshaGameRuntime.cs CanResolveEarly / bot-turn loop / claim window expiry
* **Screenshot:** `phase-N-end.png`

## P1 — Confusions (user struggles)

### [P1] (N) Autoplay encountered 363 emit failures in 90s (emitDiscard returned false, emitTakePickup returned false, or no claim-pass button).

* **Steps to reproduce:** Inspect window.__autoplay.events for type=emit_*_failed.
* **Suspect file/owner:** world.ts emit* surfaces / banner state-machine timing
* **Screenshot:** `phase-N-end.png`

## P2 — Polish

### [P2] (B) Onboarding card (name/avatar) appears in the lobby — Skip is available but it adds friction to first play

* **Steps to reproduce:** Open bare URL.  Onboarding card sits between user and Apply & Start.
* **Suspect file/owner:** src/frontend/autotable-src/src/identity-onboarding.ts
* **Screenshot:** `phase-A-landing.png`

### [P2] (J) No claim window became active during the 20s observation — could be normal (no discard matched our hand)

* **Screenshot:** `phase-I-after-30s.png`

## Console errors (first 20)

* THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN. The "position" attribute is likely to have NaN values. Is
* Failed to load resource: the server responded with a status of 404 (Not Found)
* Failed to load resource: the server responded with a status of 404 (Not Found)
