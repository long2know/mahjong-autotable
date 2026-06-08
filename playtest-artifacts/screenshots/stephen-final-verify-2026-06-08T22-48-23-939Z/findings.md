# Stephen first-play audit — stephen-final-verify-2026-06-08T22-48-23-939Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T22:48:23.940Z
* Ended:   2026-06-08T22:52:38.790Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

✅ **PLAYABLE FROM BARE URL** — no blockers or confusions observed.

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4712 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1273 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3182 ms
* ✅ Phase D: Apply & Start (navigate) — 4771 ms
* ✅ Phase D2: Click Connect (manual) — 14 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 2895 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10122 ms
* ✅ Phase G: My hand populated — 1766 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3233 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 33 ms
* ✅ Phase H3: Install autoplay driver (banner-driven loop) — 522 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30312 ms
* ✅ Phase J: Claim window visibility — 20319 ms
* ✅ Phase K: Sustained play observation (60s) — 60277 ms
* ✅ Phase L: Final UI inventory — 1240 ms
* ✅ Phase N: Continuous play loop (90s, banner-driven) — 90679 ms
* ✅ Phase O: Banner + cursor proof (Hicks turn indicator) — 185 ms
* ✅ Phase M: #deal single-click + disabled tooltip (FIX 4) — 19229 ms

## Phase N — Continuous play loop (90s, banner-driven)

* Loop window: 90639 ms
* Autoplay total lifetime (since H3): 203326 ms
* Seat-0 emit_discard — window: **0**, cumulative: **15** (threshold ≥ 5)
* Total discards across all seats — window: **0**, cumulative: **61** (threshold ≥ 25)
* Cumulative successful emits: discards=15, take_pickups=0, claim_passes=8
* Cumulative silent failures: 0
* Banner sightings (cumulative): discard=4, pickup=0, claim=58
* Per-seat discards cumulative: {"0":15,"1":15,"2":16,"3":15}
* Per-seat emits cumulative:    {"0":15,"1":0,"2":0,"3":0}
* Game completed during autoplay lifetime: yes
* Passed seat-0 threshold (window OR cumulative): yes
* Passed total threshold (window OR cumulative):  yes
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
