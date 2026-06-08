# Stephen first-play audit — stephen-final-verify-2026-06-08T22-15-05-118Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T22:15:05.118Z
* Ended:   2026-06-08T22:19:21.344Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

❌ **NOT PLAYABLE FROM BARE URL** — 5 blocker(s) + 0 confusion(s).

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4711 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1290 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3178 ms
* ✅ Phase D: Apply & Start (navigate) — 4788 ms
* ✅ Phase D2: Click Connect (manual) — 33 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 2875 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10077 ms
* ✅ Phase G: My hand populated — 1768 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3299 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 64 ms
* ✅ Phase H3: Install autoplay driver (banner-driven loop) — 537 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30365 ms
* ✅ Phase J: Claim window visibility — 20369 ms
* ✅ Phase K: Sustained play observation (60s) — 60417 ms
* ✅ Phase L: Final UI inventory — 1815 ms
* ✅ Phase N: Continuous play loop (90s, banner-driven) — 90970 ms
* ✅ Phase O: Banner + cursor proof (Hicks turn indicator) — 316 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19265 ms

## Phase N — Continuous play loop (90s, banner-driven)

* Loop window: 90896 ms
* Seat-0 emit_discard events: **0** (threshold ≥ 5)
* Total discards across all seats: **0** (threshold ≥ 25)
* Silent emit failures: 0
* Banner sightings (cumulative): discard=0, pickup=0, claim=2
* Per-seat discards in window: {"0":0,"1":0,"2":0,"3":0}
* Per-seat emits in window:   {"0":0,"1":0,"2":0,"3":0}
* Per-seat emits cumulative:  {"0":0,"1":0,"2":0,"3":0}
* Game completed during window: no
* Autoplay ticks: 817
* Timeline: `phase-N-timeline.jsonl`

## Phase O — Banner + cursor proof

_(no Phase O proof was captured — banner never showed discard cue while we watched)_

## P0 — Blockers (user cannot play)

### [P0] (I) Over 30s, NO bot at any other seat made a discard (delta=0). No pickup state present — bots may be misconfigured. moveLog entries=8.

* **Steps to reproduce:** After deal, wait 30s without any further user action. Expect bots to draw + discard; observed: zero progress.
* **Suspect file/owner:** BotEngine.cs in src/backend / bot-turn loop / turn rotation
* **Screenshot:** `phase-I-after-30s.png`

### [P0] (K) Over 60s of observation, total discards grew by 0 (from 4 to 4). Play has stalled — no bot is taking turns, no human discard registered, no win condition hit. The user is staring at a frozen 3D table. This is the "I waited and nothing happened" complaint and is likely a CASCADE from Phase H (the dealer-extra/discard-rejection issue) blocking the entire turn rotation.

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

### [P0] (O) No banner-discard proof was captured during the Phase N loop window — `window.__autoplay.discardProofCaptured` was never set, meaning the `#turn-banner` never showed the discard cue while we were watching.

* **Steps to reproduce:** After Phase N completes, read window.__autoplay.discardProofCaptured.
* **Suspect file/owner:** src/frontend/autotable-src/src/game-ui.ts refreshTurnBanner — commit 7a50257
* **Screenshot:** `phase-O-live-state.png`

## P1 — Confusions (user struggles)

_None._

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
