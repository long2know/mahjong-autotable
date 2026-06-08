# Stephen first-play audit — stephen-final-verify-2026-06-08T22-29-31-348Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T22:29:31.349Z
* Ended:   2026-06-08T22:33:46.225Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

❌ **NOT PLAYABLE FROM BARE URL** — 1 blocker(s) + 0 confusion(s).

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4740 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1289 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3182 ms
* ✅ Phase D: Apply & Start (navigate) — 4784 ms
* ✅ Phase D2: Click Connect (manual) — 16 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 2795 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10111 ms
* ✅ Phase G: My hand populated — 1780 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3270 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 71 ms
* ✅ Phase H3: Install autoplay driver (banner-driven loop) — 556 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30305 ms
* ✅ Phase J: Claim window visibility — 20322 ms
* ✅ Phase K: Sustained play observation (60s) — 60272 ms
* ✅ Phase L: Final UI inventory — 1191 ms
* ✅ Phase N: Continuous play loop (90s, banner-driven) — 90593 ms
* ✅ Phase O: Banner + cursor proof (Hicks turn indicator) — 202 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19302 ms

## Phase N — Continuous play loop (90s, banner-driven)

* Loop window: 90539 ms
* Autoplay total lifetime (since H3): 203182 ms
* Seat-0 emit_discard — window: **0**, cumulative: **11** (threshold ≥ 5)
* Total discards across all seats — window: **0**, cumulative: **45** (threshold ≥ 25)
* Cumulative successful emits: discards=11, take_pickups=0, claim_passes=6
* Cumulative silent failures: 0
* Banner sightings (cumulative): discard=0, pickup=0, claim=66
* Per-seat discards cumulative: {"0":11,"1":12,"2":11,"3":11}
* Per-seat emits cumulative:    {"0":11,"1":0,"2":0,"3":0}
* Game completed during autoplay lifetime: yes
* Passed seat-0 threshold (window OR cumulative): yes
* Passed total threshold (window OR cumulative):  yes
* Autoplay ticks: 812
* Timeline: `phase-N-timeline.jsonl`

## Phase O — Banner + cursor proof

_(no Phase O proof was captured — banner never showed discard cue while we watched)_

## P0 — Blockers (user cannot play)

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
