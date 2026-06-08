# Stephen first-play audit — stephen-first-play-2026-06-08T20-59-30-805Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T20:59:30.806Z
* Ended:   2026-06-08T21:02:17.251Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

❌ **NOT PLAYABLE FROM BARE URL** — 1 blocker(s) + 0 confusion(s).

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4700 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1285 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3186 ms
* ✅ Phase D: Apply & Start (navigate) — 4933 ms
* ✅ Phase D2: Click Connect (manual) — 35 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 3133 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10473 ms
* ✅ Phase G: My hand populated — 1871 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3666 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 89 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30549 ms
* ✅ Phase J: Claim window visibility — 20496 ms
* ✅ Phase K: Sustained play observation (60s) — 60483 ms
* ✅ Phase L: Final UI inventory — 1562 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19886 ms

## P0 — Blockers (user cannot play)

### [P0] (K) Over 60s of observation, total discards grew by 0 (from 6 to 6). Play has stalled — no bot is taking turns, no human discard registered, no win condition hit. The user is staring at a frozen 3D table. This is the "I waited and nothing happened" complaint and is likely a CASCADE from Phase H (the dealer-extra/discard-rejection issue) blocking the entire turn rotation.

* **Steps to reproduce:** 1) Bare URL → complete the lobby + connect + seat + deal flow. 2) Wait 60+ seconds without any further user action. 3) Observe: discards count does not advance, no bot draws or discards. The game is dead in the water.
* **Suspect file/owner:** Cascade from Phase H — fix the dealer-extra / silent-discard-rejection issue and the turn rotation should resume. Also recommend defaulting the lobby to Auto deal mode for first-time users (lobby.ts initLobby).
* **Screenshot:** `phase-K-final-60s.png`

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
