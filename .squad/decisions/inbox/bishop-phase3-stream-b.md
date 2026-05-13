# Bishop — Phase 3 Stream B (Changsha v1 Backend Fixes)

**Date:** 2026-05-13
**By:** Bishop (Backend Dev)
**Branch:** `stlong/changsha-v1-phase3`
**Status:** SHIPPED — all five surgical fixes landed, 0 failed tests, build clean.

---

## What shipped

Five surgical backend fixes addressing the silent bugs and not-enforced rules surfaced in `bishop-changsha-backend-audit.md`, plus Vasquez's `vasquez-banker-rotation-lock.md` (v1.2) canonical ruling.

### FIX-1 — Banker rotation canonical (winner-becomes-dealer)
- `ChangshaStateMachine.cs` `RotateBanker` (~line 460): rewrote to make the winner the next dealer; washout keeps the dealer's seat; `HandNumber++` in both cases. Reasons: `"dealerRetained"` (winner == old dealer), `"winnerBecomesDealer"`, `"washoutDealerRetained"`. Existing 16-hand → `GameEnded` termination logic unchanged.
- Tests: `BankerRotationTests.cs` fully rewritten — adds `BankerRotation_WinnerBecomesDealer_NotPlusOne`, `BankerRotation_Washout_DealerKeepsSeat`, `BankerRotation_FullGame_16Hands_EmitsGameEnded`, `BankerRotation_CanonicalSequence_MatchesSpec62Example`, `Washout_FromSeat3_DealerStaysOnSeat3`, `HandNumber_IncrementsOnWinnerAndWashout`.
- `StateMachineServiceTests.cs` updated: `BankerRotation_NonDealerWins_RotatesCounterClockwise` → `BankerRotation_NonDealerWins_WinnerBecomesDealer`.

### FIX-2 — Kong/Pung same-tier priority + extract priority helper
- New file `Changsha/ChangshaClaimPriority.cs` — single source of truth. `TierOf(TableClaimType)` returns Hu=3, Kong=Pung=2, Chow=1. `CounterClockwiseDistance(from, to) = (to - from + 4) % 4`.
- `ClaimAdjudicator.cs` and `Runtime/ChangshaGameRuntime.cs` (~line 622) both call `ChangshaClaimPriority.TierOf`. No drift possible — the duplicate inline priority table in the runtime is gone.
- Tests: `ClaimAdjudicatorTests.cs` — replaced the old `Kong_TakesPriorityOverPung` (which hard-asserted the bug). New tests: `KongAndPung_SameTier_CCWClosestSeatWins_KongCloserCCW`, counterexample `KongAndPung_SameTier_PungCloserCCW_BeatsKong`, `[Theory]` `ClaimPriority_PungAndKong_SameTier_CCWProximityTiebreak`, and `ClaimPriority_PriorityTablesAgree_NoDrift`.
- `PungKongChowTests.Kong_OpportunityDetected_WhenSeatHoldsTriplet` — line ~49 fixed from `Priority == 3` to `Priority == TierOf(Kong)`.

### FIX-3 — Per-hand wall seed mixing
- `ChangshaStateMachine.cs` `Deal` (~line 80): `new Random(state.Seed)` → `new Random(HashCode.Combine(state.Seed, state.HandNumber))`. Same `(Seed, HandNumber)` still gives an identical wall; different hands of the same game now produce different shuffles.
- Tests: new file `WallSeedTests.cs` — `WallSeed_Determinism_SameGameSeedAndHandNumberProduceIdenticalShuffle`, `WallSeed_DifferentHands_DifferentShuffles`, `WallSeed_DifferentGameSeeds_DifferentShuffles`, `WallSeed_HandNumber_NotZeroIndexed`.
- **Deferred:** `DiceService(state.Seed + state.HandNumber)` in `StartNextHandOrEndAsync` still uses raw addition rather than `HashCode.Combine`. Out of scope for this stream — dice has minor visual-only impact compared to the wall.

### FIX-4 — Honor `claim.tileIds` in chow resolution
- `Tables/TableActionErrorCodes.cs`: added `ChowTilesInvalid = "CHOW_TILES_INVALID"`.
- `ChangshaStateMachine.cs`: added `ResolveClaim` overload accepting `int[]? chosenTileIds`. Split `RemoveChowTiles` into `RemoveChowTilesByChoice` (validates: exactly 2 distinct tiles, both in concealed hand, single-suit, 3 consecutive ranks — throws `TableRuleException` with `CHOW_TILES_INVALID` on any failure) and `RemoveChowTilesByLowestPattern` (legacy fallback for null/empty tileIds).
- `Runtime/ChangshaGameInstance.cs`: added `LoggedLegacyChowWarning` flag.
- `Runtime/ChangshaGameRuntime.cs` (~line 608): projects `TileIds` from `PendingClaims` through to `ResolveClaim`; once-per-game `LogWarning` if a chow arrives without tileIds.
- Tests: new file `ChowTileIdsTests.cs` — `Chow_TileIdsRespected_WhenClaimantHasMultipleValidPatterns`, `Chow_EmptyTileIds_FallsBackToLowestPattern`, `Chow_InvalidTileIds_ReturnsContractError_NotInHand`, `Chow_InvalidTileIds_ReturnsContractError_NotSequential`, `Chow_InvalidTileIds_ReturnsContractError_DifferentSuits`, `Chow_TileIds_WrongCount_ReturnsContractError`.

### FIX-5 — Enforce missed-win (过胡) rule §3.6
- `ChangshaDomain.cs`: added `HashSet<int> MissedWinSeats` to `ChangshaGameState`. System.Text.Json deserializes missing field as default — back-compat safe for in-flight games.
- `ChangshaStateMachine.cs`:
  - `Deal` (~line 117): `state.MissedWinSeats.Clear()` so the flag resets per hand.
  - `Discard` (~line 167): filters Hu opportunities for seats in `MissedWinSeats` before opening the claim window. If a flagged seat had ONLY a Hu opportunity, no opportunity is added.
  - `ResolveClaim` Hu branch: calls `FlagMissedWinSeats(state, claimWindow, declaringHuSeat: claimingSeatIndex)` so other Hu-capable seats that didn't declare get flagged.
  - `ResolveClaim` non-Hu branch + `PassClaim`: call `FlagMissedWinSeats(state, claimWindow, declaringHuSeat: -1)` so every seat that had Hu in the window is flagged.
  - New `FlagMissedWinSeats` helper (~line below `ResolveHuClaim`): iterates `claimWindow.Opportunities`, adds every `Hu` opportunity owner (except the declarer) to `state.MissedWinSeats`.
- **Self-draw is NOT affected** — `DeclareSelfDrawWin` bypasses the claim window so a flagged seat can still self-draw.
- Tests: new file `MissedWinTests.cs` — `MissedWin_DeclinesWinningDiscard_BlockedFromLaterDiscardWins`, `MissedWin_DoesNotBlockSelfDraw`, `MissedWin_ResetsOnNewHand`, `MissedWin_PungOrKongStillAllowedAfterMissedWin`, `MissedWin_TwoSeatsHadHu_OneWins_OtherFlagged`.

---

## Test count delta

- Baseline: **179 passed**, 7 skipped (v2-deferred), 0 failed.
- After: **203 passed**, 7 skipped, 0 failed.
- Net: +24 tests (banker rewritten = net +5; wall seed +4; claim priority +5; chow tileIds +6; missed-win +5). One existing test (`Kong_TakesPriorityOverPung`) was rewritten in place to assert the corrected rule, two existing tests (one banker, one Pung/Kong/Chow tier assertion) were tightened.

Build: 0 warnings, 0 errors.

---

## Confidence: 16-hand championship now completes correctly?

**High.** Specifically:
- The `RotateBanker` rewrite is exhaustively covered by the §6.2 worked-example test and by a 16-hand drive that asserts `GameEnded` after exactly 16 increments of `HandNumber`. Winner-becomes-dealer plus washout-keeps-seat is symmetric with the locked v1.2 spec.
- Per-hand seed mixing means a 16-hand bot game now plays 16 *different* walls — fairness restored.
- Same-tier Kong/Pung adjudication with CCW proximity matches §3.3.

What I did NOT touch and is still worth tracking:
- `DiceService` seed mixing (still raw addition).
- Persistence hydration on process restart (`_games` is not reloaded from `ChangshaGame.StateJson`).
- E2E coverage for a full 16-hand bot game — `ChangshaHubE2ETests.E2E1_AllBots_PlaysAtLeastOneHandAndCompletes` only proves one hand. A 16-hand E2E remains the cleanest regression guard for FIX-1 + FIX-3 at the hub layer.

These are the same three deferred items called out in `bishop-changsha-backend-audit.md` last week; they are now the largest remaining v1-correctness gaps.

---

## Frontend contract impact

- **New error code** `CHOW_TILES_INVALID` is surfaced via `TableRuleException`. Hub will propagate as a hub error to clients. Hicks may want to map this to a user-facing toast when a chow submission is rejected.
- `ClaimMade` events now reflect the seat that won by CCW proximity for same-tier Kong/Pung — previously Kong always won. No wire schema change, just different seats winning in close-claim scenarios.
- `BankerRotated.reason` now uses `"winnerBecomesDealer"` / `"dealerRetained"` / `"washoutDealerRetained"` (the latter two pre-existed; the first is new). No schema break.

---

## Files touched

**Source:**
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ClaimAdjudicator.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaDomain.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaClaimPriority.cs` *(new)*
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameInstance.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs`
- `src/backend/src/Mahjong.Autotable.Api/Tables/TableActionErrorCodes.cs`

**Tests:**
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/BankerRotationTests.cs` (rewritten)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/PungKongChowTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/WallSeedTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/ChowTileIdsTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/MissedWinTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/ChangshaServices/ClaimAdjudicatorTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/ChangshaServices/StateMachineServiceTests.cs`
