# Stephen first-play — POST-FIX VERIFICATION

* **Verifier:** Vasquez (mahjong-autotable squad)
* **Verified at:** 2026-06-08T21:09Z
* **Against bundle:** `autotable-src.25dc5f79.js` (Hicks fix-wave squash sha `554749a`)
* **Backend:** `http://127.0.0.1:8088` (already running, NOT restarted)
* **Runs:** 3 independent runs of `playtest-artifacts/playtest-stephen-first-play.spec.mjs`
* **Spec mod:** ONLY added Phase M (FIX-4 verification) at end; no behavioral changes to A–L

## Verdict

**❌ FAIL — 1 P0 still fires (P0-K).**  Four of Hicks's five fixes verified end-to-end (FIX 1, 2, 3, 4, 5).  P0-D, P0-H, P1-B fully cleared in all 3 runs.  P0-I cleared in run 2 (5 bot discards in 30s) but flaky in runs 1 & 3 (only 2 bot discards in 30s — bots stall after one partial round).  **P0-K fires in all 3 runs (0 discard growth in the 60s observation window)** — but the ROOT CAUSE is different from pre-fix.  Pre-fix: silent discard rejection blocked all play.  Post-fix: the cascade is broken (user CAN discard, bots DO take turns), but sustained play stalls because after one rotation the ball comes back to seat 0 and the user must discard again — the spec only emits one Phase-H discard.  This is no longer a "click is silently swallowed" bug; it's now a "passive user causes table to freeze with no UI prompt" bug.

## Per-phase result table (3-run aggregate)

| Phase | Task assertion | Run 1 | Run 2 | Run 3 | Verdict |
|-------|---------------|-------|-------|-------|---------|
| A | No tour overlay; lobby interactive | ✅ tour absent | ✅ | ✅ | **PASS** (FIX 3 — P1-B cleared) |
| B | Skip dismisses onboarding only (no tour to skip) | ✅ onboarding only | ✅ | ✅ | **PASS** |
| C | Lobby picks accepted; auto deal default | ✅ auto selected | ✅ | ✅ | **PASS** (FIX 2) |
| D | URL has `?gameId=changsha-<8hex>`; auto-connect ≤5s; Connect hidden | ✅ `gameId=changsha-5a34ae24`, `clientConnected=true`, Disconnect visible | ✅ `gameId=changsha-3cd4564c` | ✅ | **PASS** (FIX 1 — P0-D cleared) |
| D2 | already connected, no manual click | ✅ `alreadyConnected` | ✅ | ✅ | **PASS** |
| E | Seat 0 occupied | ✅ `seatNow=0` | ✅ | ✅ | **PASS** |
| F | Deal fires; 14 tiles in hand | ✅ `mySeatHand=14` | ✅ | ✅ | **PASS** |
| G | Hand face-up to seat 0 | ✅ 14 tiles `hand.*@0` | ✅ | ✅ | **PASS** |
| H | Discard succeeds OR toast appears (no silent fail) | ✅ `discardAttempt.ok=true`, `discardCount` 0→2 | ✅ 0→4 | ✅ 0→2 | **PASS** (P0-H cleared) |
| H2 | `#pickup-take-btn` not stranded user | ✅ hudVisible=false (no DealerExtra stall) | ✅ | ✅ | **PASS** |
| I | ≥3 bot discards across seats 1/2/3 in 30s | ❌ 2 bot discards | ✅ 5 bot discards | ❌ 2 bot discards | **FLAKY 1/3** |
| J | Claim window check | ✅ no-op (window times out cleanly) | ✅ | ✅ | **PASS** |
| K | ≥8 discards growth t=10s→t=70s | ❌ 0 growth (2→2) | ❌ 0 growth (6→6) | ❌ 0 growth (2→2) | **FAIL 3/3** |
| L | Quick Match + Deal buttons present, no broken UI | ✅ 14 visible buttons incl. Deal/Disconnect/Leave seat | ✅ | ✅ | **PASS** |
| **M (NEW)** | `#deal` disabled tooltip + single-click deal | ✅ disabled-tip="Take a seat first", enabled-tip="Deal a new hand", single-click → 14 hand tiles | ✅ identical | ✅ identical | **PASS** (FIX 4 verified) |

## Fix-by-fix verification

| Fix | Description | Verified | Evidence |
|-----|-------------|----------|----------|
| 1 | `lobby.buildUrl()` mints `changsha-<8hex>` | ✅ | Phase D URL contains `gameId=changsha-5a34ae24` (run 1), `changsha-3cd4564c` (run 2), `changsha-c8...` (run 3); auto-connect succeeded in all 3 runs (`clientConnected=true`) |
| 2 | Default `dealMode=auto` | ✅ | Phase C confirms Auto radio is the lobby default in screenshot; Phase D URL has `dealMode=auto` in all 3 runs; Phase F dealer hand reaches 14 (auto dealer-extra) |
| 3 | Tour is opt-in only | ✅ | Phase A `tourOverlay.present=false` in all 3 runs; lobby Apply button is hit-clickable; `dismissOverlaysIfPresent` reports only "onboarding" dismissed, no "tour" |
| 4 | `#deal` is single-click + tooltip | ✅ | Phase M before-seat: `disabled=true, title="Take a seat first", aria-label="Take a seat first"`; after-seat: `disabled=false, title="Deal a new hand"`; `page.click('#deal')` (no `mouse.down`/`up`) dealt 14 tiles in 6.5s; `singleClickMs` ~7000ms (includes server round-trip + auto-flip) |
| 5 | `emitDiscard()` rejections show toast | ⚠️ Indirectly | Phase H discard succeeded in all 3 runs (no rejection to test the toast path); the secondary `onDragStart` widened toast was NOT exercised because hand-tile clicks landed in legal state.  The toast pathway exists per Hicks's source diff and `#toast-region` is in the DOM; live toast-on-rejection wasn't observable in the green-path test |

## P0-K analysis (the regression that's not a regression)

### Before Hicks's fixes
- Phase K trajectory: `totalDiscards = 0, 0, 0, 0, 0, 0` (across t=10..60s)
- Cause: Phase H discard SILENTLY rejected (DealerExtra/null pickup state); bots couldn't continue because dealer never discarded.

### After Hicks's fixes (run 2, the best run)
- Phase K trajectory: `totalDiscards = 6, 6, 6, 6, 6, 6` (across t=10..60s)
- The 6 discards happened BEFORE Phase K began (during Phase H + Phase I).  Move log from run 2:
  - `14:00:01 Seat 0: discarded 3万`
  - `14:00:01 Seat 1 (Bot): discarded 7条`
  - `14:00:02 Seat 2 (Bot): formed a meld with 8筒 / discarded 3条`
  - `14:00:02 Seat 3 (Bot): discarded 6条`
  - `14:00:02 Seat 0: claim window — Chow on 6条 (from Seat 3)` (user did not act → auto-pass)
  - `14:00:02 Seat 2 (Bot): claim window — Pung on 6条 (from Seat 3)` (bot did not claim)
  - `14:00:08 Seat 2 (Bot): discarded 3条`
  - `14:00:08 Seat 0: claim window — Pung on 3条 (from Seat 2)` (user did not act → auto-pass)
  - `14:00:13 Seat 3 (Bot): discarded 8筒`
  - `14:00:13 Seat 1 (Bot): claim window — Pung on 8筒 (from Seat 3)` (bot did not claim)
  - `14:00:14 Seat 1 (Bot): discarded 1条`
  - `14:00:14 Seat 2 (Bot): discarded 2筒`
  - `14:00:14 Seat 3 (Bot): claim window — Chow on 2筒 (from Seat 2)` (bot did not claim)
  - `14:00:15 Seat 3 (Bot): discarded 4筒`
  - **[stall — no more entries for the next 60s]**

### Diagnosis
After Seat 3 discards 4筒 at t=14:00:15, the turn returns to Seat 0 (the user).  Seat 0's `totalHandTiles=41 / mySeatHand=14` (likely +1 from a Pung claim or pickup).  The user must either claim or discard, but the spec only emits ONE Phase-H discard then sits idle for 60s.  Bots cannot drive the table on their own when it's a human seat's turn to act.

This is **not** a cascade from P0-H anymore — the cascade is broken and the user IS now in the game.  The remaining issue is that there's no "It's your turn — discard a tile" UI prompt highlighted prominently, so a passive observer (Stephen) sees a frozen table.

## Recommendations for next fix wave

(In priority order; these are NEW issues, not regressions of Hicks's fixes.)

1. **[P0-NEW] Turn indicator for seat 0 when it's their turn-to-discard.** When the user holds the 14th tile (drew or claimed) and the table is waiting on their discard, surface a HIGH-VISIBILITY "Your turn — discard a tile" banner (similar to the existing pickup HUD).  Currently the user sees a normal-looking hand with no indication that the game is waiting on them.
2. **[P1-NEW] Claim window indicator for seat 0.** When seat 0 has an available Pung/Chow/Kong/Hu claim, the claim window opens silently — no visual cue beyond a move-log entry that scrolls past.  Consider a center-screen modal or pulsing badge.
3. **[P1-NEW] Bot stochastic stall (runs 1 & 3).** In runs 1 and 3, bots produced only 2 total discards in 30s instead of 5+.  In both cases the move-log entries cut off at the same pattern (Seat 2 discard → Seat 0 claim window → Seat 3 discard → stall).  Investigate whether bot-side claim windows are sometimes blocking the next bot turn even when the seat doesn't claim.
4. **[P2-NEW] Spec design gap.** The test contract assumes "play sustains itself after one user discard."  If the design is "user must keep discarding", the spec should loop on hand>=14 → click discard.  Otherwise the P0-K threshold is unachievable by design.  Either the spec or the design needs to converge.

## Soft assertion regression watch (PASS)

- **Browser console:** 0 page-errors in all 3 runs.  Console-errors are 3 in each run, all from `THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN` (the known cosmetic warning) + 2 × 404 (favicons, pre-existing).  No new errors introduced.
- **Visible buttons (Phase L):** 14 buttons including `#deal` (text "Deal", disabled=false), `#disconnect`, `#leave-seat`, `#toggle-setup`, `#toggle-dealer`.  Quick Match is in the lobby panel (not on the table). No broken/orphaned buttons.

## Artifact map

- `green-state/01..11` — chronological green-path screenshots (run 2 representative)
- `stall-state/run{1,2,3}-phase-K-stuck-at-N-discards.png` — three runs frozen at K
- `per-run/run{1,2,3}-summary.json` — full machine-readable findings per run
- `per-run/run{1,2,3}-findings.md` — spec's own findings.md per run

## How to reproduce

```bash
cd /data/source/mahjong-autotable/src/frontend/autotable-src
E2E_BASE_URL=http://127.0.0.1:8088 \
  node /data/source/mahjong-autotable/playtest-artifacts/playtest-stephen-first-play.spec.mjs
```

Spec exits with code 2 when blockers > 0 (matches FAIL).  Auto deal mode is the lobby default (FIX 2) so a fresh user gets `dealMode=auto` automatically.
