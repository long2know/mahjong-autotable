# Stephen first-play audit — stephen-first-play-2026-06-08T20-31-50-566Z

* Base URL: http://127.0.0.1:8088/autotable/  (NO query parameters)
* Started: 2026-06-08T20:31:50.568Z
* Ended:   2026-06-08T20:34:18.285Z
* Page errors: 0
* Console errors: 3
* Console warnings (non-NaN): 4
* Network failures (≥400): 2

## Verdict

❌ **NOT PLAYABLE FROM BARE URL** — 4 blocker(s) + 1 confusion(s).

## Phase summary

* ✅ Phase A: Landing (bare URL, no query params) — 4719 ms
* ✅ Phase B: Dismissals (Skip Tour + Skip Onboarding) — 1712 ms
* ✅ Phase C: Lobby picks (Changsha + 3 bots + seat 0) — 3188 ms
* ✅ Phase D: Apply & Start (navigate) — 4985 ms
* ✅ Phase D2: Click Connect (manual) — 4152 ms
* ✅ Phase E: Take seat (manual fallback if auto-seat skipped) — 55 ms
* ✅ Phase F: Deal — hold-to-confirm 700ms on #deal — 10502 ms
* ✅ Phase G: My hand populated — 1856 ms
* ✅ Phase H: Discard — human-style click on hand tile — 3604 ms
* ✅ Phase H2: Click #pickup-take-btn → does pickup phase advance? — 69 ms
* ✅ Phase I: Bot draw + discard cadence (30s) — 30489 ms
* ✅ Phase J: Claim window visibility — 20360 ms
* ✅ Phase K: Sustained play observation (60s) — 60472 ms
* ✅ Phase L: Final UI inventory — 1465 ms

## P0 — Blockers (user cannot play)

### [P0] (D) "Apply & Start" navigated to http://127.0.0.1:8088/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&seat=0 BUT did NOT auto-connect to the WebSocket. The lobby's buildUrl() omits ?gameId=, and client-ui.ts:start() guards auto-connect on getUrlState() (the gameId query param), so the user lands on an EMPTY 3D table with a "Connect" button and must click it manually. This contradicts the button label "Apply & Start".

* **Steps to reproduce:** 1) Open http://127.0.0.1:8088/autotable/ bare. 2) Dismiss tour + onboarding. 3) Pick Changsha + 3 bots + Seat 0 (defaults). 4) Click Apply & Start. 5) Observe: page reloads with ?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&seat=0 (NO gameId), the 3D table renders empty, and #connect button is still shown.
* **Suspect file/owner:** src/frontend/autotable-src/src/lobby.ts buildUrl() at line 448 (does not add gameId) + src/frontend/autotable-src/src/client-ui.ts start() at line 490 (only auto-connects when getUrlState() returns non-null gameId). Fix: lobby.buildUrl should mint a fresh gameId (e.g. crypto.randomUUID slice) when none exists, OR client-ui.ts:start() should auto-connect on ANY lobby-built URL.
* **Screenshot:** `phase-D-after-apply.png`

### [P0] (H) User-style click on a hand tile did NOT register a discard. The user is staring at a hand of 14 tiles and tap-clicking has zero effect — silent rejection from world.emitDiscard. pickupCurrent=null (no take pending), myPickupTurn=false, hasExtra=false, before=3, after=3. The runtime is NOT obviously in a pickup phase that would explain the rejection; play has stalled with no UI feedback explaining why. This is the "the game won't let me play" complaint.

* **Steps to reproduce:** 1) Bare URL → lobby defaults → Apply & Start → Connect → Take Seat 0 → hold #deal 700ms → wait 8s. 2) Click any tile in your hand (or call world.emitDiscard via console). 3) Observe: silent no-op, discards collection does not grow, the game just sits there.
* **Suspect file/owner:** world.ts emitDiscard at line 461 — needs at MINIMUM a toast/console.warn explaining WHY the discard was rejected (wrong phase, not your turn, no pickup pending, etc.). Currently the failure path is a bare `return false` with zero user feedback. Backend ChangshaStateMachine.Discard should also surface rejection to the client.
* **Screenshot:** `phase-H-after-discard.png`

### [P0] (I) Over 30s, NO bot at any other seat made a discard (delta=0). No pickup state present — bots may be misconfigured. moveLog entries=6.

* **Steps to reproduce:** After deal, wait 30s without any further user action. Expect bots to draw + discard; observed: zero progress.
* **Suspect file/owner:** BotEngine.cs in src/backend / bot-turn loop / turn rotation
* **Screenshot:** `phase-I-after-30s.png`

### [P0] (K) Over 60s of observation, total discards grew by 0 (from 3 to 3). Play has stalled — no bot is taking turns, no human discard registered, no win condition hit. The user is staring at a frozen 3D table. This is the "I waited and nothing happened" complaint and is likely a CASCADE from Phase H (the dealer-extra/discard-rejection issue) blocking the entire turn rotation.

* **Steps to reproduce:** 1) Bare URL → complete the lobby + connect + seat + deal flow. 2) Wait 60+ seconds without any further user action. 3) Observe: discards count does not advance, no bot draws or discards. The game is dead in the water.
* **Suspect file/owner:** Cascade from Phase H — fix the dealer-extra / silent-discard-rejection issue and the turn rotation should resume. Also recommend defaulting the lobby to Auto deal mode for first-time users (lobby.ts initLobby).
* **Screenshot:** `phase-K-final-60s.png`

## P1 — Confusions (user struggles)

### [P1] (B) Tour overlay appears before lobby and intercepts pointer events on Apply & Start — user MUST click Skip Tour first

* **Steps to reproduce:** Open bare URL.  Tour overlay covers the lobby.  User must locate + click "Skip tour" before proceeding.
* **Suspect file/owner:** src/frontend/autotable-src/src/tour.ts ensureRoot — consider not painting modal overlay until lobby is dismissed, or moving tour to a non-blocking corner.
* **Screenshot:** `phase-A-landing.png`

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
