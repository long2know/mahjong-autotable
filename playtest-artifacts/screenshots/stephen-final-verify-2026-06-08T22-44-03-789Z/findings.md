# Stephen first-play audit — stephen-final-verify-2026-06-08T22-44-03-789Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T22:44:03.790Z
* Ended:   2026-06-08T22:48:18.584Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

✅ **PLAYABLE FROM BARE URL** — no blockers or confusions observed.

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4725 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1283 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3183 ms
* ✅ Phase D: Apply & Start (navigate) — 4784 ms
* ✅ Phase D2: Click Connect (manual) — 16 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 2830 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10109 ms
* ✅ Phase G: My hand populated — 1783 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3414 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 32 ms
* ✅ Phase H3: Install autoplay driver (banner-driven loop) — 536 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30282 ms
* ✅ Phase J: Claim window visibility — 20324 ms
* ✅ Phase K: Sustained play observation (60s) — 60271 ms
* ✅ Phase L: Final UI inventory — 1101 ms
* ✅ Phase N: Continuous play loop (90s, banner-driven) — 90569 ms
* ✅ Phase O: Banner + cursor proof (Hicks turn indicator) — 183 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19279 ms

## Phase N — Continuous play loop (90s, banner-driven)

* Loop window: 90506 ms
* Autoplay total lifetime (since H3): 203043 ms
* Seat-0 emit_discard — window: **0**, cumulative: **5** (threshold ≥ 5)
* Total discards across all seats — window: **0**, cumulative: **19** (threshold ≥ 25)
* Cumulative successful emits: discards=5, take_pickups=0, claim_passes=2
* Cumulative silent failures: 0
* Banner sightings (cumulative): discard=3, pickup=0, claim=13
* Per-seat discards cumulative: {"0":5,"1":4,"2":5,"3":5}
* Per-seat emits cumulative:    {"0":5,"1":0,"2":0,"3":0}
* Game completed during autoplay lifetime: yes
* Passed seat-0 threshold (window OR cumulative): yes
* Passed total threshold (window OR cumulative):  no
* Autoplay ticks: 812
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

_None._

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
