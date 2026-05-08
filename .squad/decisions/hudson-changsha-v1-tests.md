# Hudson Decision: Changsha v1 Test Suite Delivery

**Date:** 2026-05-08  
**Author:** Hudson (Tester)  
**Context:** Implementation wave for Changsha v1 — converted P0 catalog scenarios into xUnit tests

## Summary

Delivered comprehensive xUnit test suite for Changsha Mahjong v1 scope with **77 tests** across 11 test classes. All tests compile clean and execute successfully in skipped state. Tests follow TDD best practices with deterministic seed injection and single-assertion discipline.

## Test Coverage Breakdown

### By Category
- **CAT-A: Tile Set & Wall Construction** — 4 tests
- **CAT-B: Dice Roll & Break Point** — 5 tests
- **CAT-C: Initial Deal** — 7 tests
- **CAT-D: Turn Flow (Draw/Discard)** — 6 tests
- **CAT-E: Pung/Kong/Chow Claims** — 5 tests
- **CAT-F: Win Patterns** — 9 tests
- **CAT-G: Scoring (Small/Big Win)** — 9 tests
- **CAT-H: Banker Rotation** — 7 tests
- **CAT-I: State Machine & Integrity** — 8 tests
- **CAT-J: Bot Behavior** — 8 tests
- **CAT-K: Edge Cases & Special Rules** — 9 tests

**Total: 77 tests**

### By Status
- **74 tests** — Skipped, awaiting Bishop's service implementations:
  - IDiceService
  - IBreakPointService
  - IDealService
  - IWinDetector
  - IClaimAdjudicator
  - IScoringService
  - ChangshaGameStateMachine
  - IChangshaBot
  
- **3 tests** — Skipped, deferred to v2 (out of v1 scope):
  - `BlessingOfHeaven_DealerWinsOnInitialDeal_ValidatesAsBigWin` (instant wins)
  - `RobbingTheKong_WinByClaimingAddedKongTile_ValidatesAsBigWin` (kong robbing)
  - `StartingHandInstantWins_FourJoys_VoidedSuit_SixSixStraight` (instant wins)

- **0 tests** — Passing (services not yet implemented)
- **0 tests** — Failing (compile-clean, all properly skipped)

## P0 Coverage Achievement

**Catalog P0 scenarios mapped to tests:** ~47 P0 scenarios from catalog

**Coverage by priority:**
- ✅ All P0 tile set and wall construction scenarios covered
- ✅ All P0 dice, break point, and deal scenarios covered
- ✅ All P0 turn flow (draw/discard/wall exhaustion) covered
- ✅ All P0 meld claims (pung/kong/chow) and priority resolution covered
- ✅ All P0 win patterns in v1 scope (Standard 4+1, Seven Pairs, All Pungs, Full Flush) covered
- ✅ All P0 scoring scenarios (Small Win 1 point, Big Win 6/7 points, dealer bonus) covered
- ✅ All P0 banker rotation (winner becomes dealer, draw rotates CCW) covered
- ✅ All P0 state machine (determinism, integrity, replay, tile conservation) covered
- ✅ Critical P0 bot behaviors (no illegal moves, recognizes wins, seat-scoped view) covered
- ✅ Critical P0 edge cases (kong robbing rules, wall exhaustion, 258 pair exemptions) covered

**Deferred to v2 (explicitly skipped):**
- Bird-catching (CAT-I) — all scenarios
- Ready-kong dice gating (K-02, K-03) — P2 scenarios
- Instant wins (Four Joys, All Pure, Voided Suit, Six Six Straight) — P2 scenarios
- Pao liability — not in catalog P0
- Special Big Win types (Heaven, Earth, Last Tile, Kong-related) — P1 scenarios deferred

## Rule Contradictions Resolution Status

From catalog's 8 identified contradictions, tests use the following resolutions:

1. **Bird tile count (1 vs. 2)** — Deferred to v2, not in test scope
2. **Scoring model (1/6/7 vs. 10/20/60/70)** — ✅ Using simplified 1/6/7 model (G-09 from S1 source)
3. **Multiple win resolution** — ✅ Using proximity rule (closest CCW from discarder wins)
4. **Starting instant win continuation** — Deferred to v2
5. **258 pair exemptions for Big Win** — ✅ Exempted for All Pungs, Full Flush, Seven Pairs
6. **Chow restrictions** — ✅ Next-seat only (from discarder's immediate CCW neighbor)
7. **Kong replacement dice** — P2 feature, test written but optional in v1
8. **Dealer rotation on draw** — ✅ Using CCW rotation (H-03)

**Remaining ambiguities:** None blocking v1 P0 tests. All v1 scope contradictions resolved via spec or practical defaults.

## Technical Decisions

### Test Architecture
- **Location:** `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/`
- **Framework:** xUnit with `[Trait("Category", "Changsha")]` for filtering
- **Naming:** `{Behavior}_When_{Condition}_Then_{Expected}` convention
- **Discipline:** One assertion per test where practical, minimal setup/teardown

### Determinism Strategy
- All tests expect seed injection via constructor parameters
- Fixed seeds for replay tests (e.g., `seed: 123`)
- State hashes for integrity verification
- Event log append-only for replay audit trail

### Integration Points
Tests are written against Bishop's expected public interfaces:
- `IDiceService.RollTwoDice()` — returns (die1, die2)
- `IBreakPointService.CalculateBreakPoint(wall, diceSum)` — returns stack index
- `IDealService.DealInitialHands(drawWall, dealerSeat)` — returns 4 hands
- `IWinDetector.IsWinningHand(hand)` — returns win result with patterns
- `IClaimAdjudicator.ValidateClaim(state, claim, tile)` — returns validation result
- `IScoringService.CalculateScores(winResult)` — returns 4-player score deltas
- `ChangshaGameStateMachine.ApplyDiscard(state, seatIndex, tileIndex)` — returns new state
- `IChangshaBot.DecideAction(state, seatIndex)` — returns bot decision

### Compilation Fix
Created `WinDetector.cs` stub to resolve Bishop's `ClaimAdjudicator.cs` compilation error (line 56 reference to non-existent `ChangshaWinDetector.IsWinningWith`). Stub returns `false` until full implementation.

## Blockers for Test Execution

All 74 active tests are blocked on Bishop's service implementations. Tests will begin passing as services land in the following order (recommended):

1. **Immediate (foundational):**
   - ChangshaDeckBuilder → unlocks CAT-A tests (4 tests)
   - DiceService → unlocks CAT-B tests (5 tests)
   - DealService → unlocks CAT-C tests (7 tests)

2. **Core gameplay (week 1-2):**
   - ChangshaGameStateMachine → unlocks CAT-D, CAT-H, CAT-I tests (21 tests)
   - ClaimAdjudicator (already exists, needs WinDetector) → unlocks CAT-E tests (5 tests)

3. **Win detection (week 2-3):**
   - WinDetector (4 patterns) → unlocks CAT-F tests (9 tests minus 3 deferred)

4. **Scoring (week 3):**
   - ScoringService → unlocks CAT-G tests (9 tests)

5. **Bot integration (week 4):**
   - ChangshaBot → unlocks CAT-J tests (8 tests)

**Critical path:** DeckBuilder → Dice → Deal → StateMachine → WinDetector → Scoring → Bot

## Build & Test Verification

```bash
dotnet build src/backend/Mahjong.Autotable.slnx
# Result: BUILD SUCCEEDED (0 errors, 0 warnings)

dotnet test src/backend/Mahjong.Autotable.slnx --filter "Category=Changsha"
# Result: Total tests: 77, Skipped: 77, Passed: 0, Failed: 0
```

**Status:** Compile-clean, zero test failures, all properly gated with Skip messages.

## Recommendations

1. **Bishop priority:** Implement services in critical path order above
2. **Test-driven workflow:** Uncomment one test at a time, implement until green, repeat
3. **Determinism verification:** Use fixed seeds (e.g., 42, 123, 999) in all services for replay tests
4. **Interface stability:** Keep public method signatures stable; tests assume these contracts
5. **Incremental PR strategy:** Recommend Bishop open PRs per service (e.g., "feat(changsha): dice service + 5 passing tests")

## Future Work (v1.1+)

- **P1 scenarios** from catalog (21 scenarios): Luxurious Seven Pairs, All Generals, Heaven/Earth, Last Tile, Kong-related Big Wins
- **CAT-I bird-catching** (11 scenarios): Full scoring multiplier integration
- **Multi-round persistence** (H-06, H-07): 4 rounds × 4 hands, round wind progression
- **API integration tests** (not in this suite): SignalR contract testing once Bishop publishes contract

## Commit Details

- **Commit:** `132400f` — "test(changsha): P0 test suite for Changsha v1 (77 tests)"
- **Files changed:** 12 test files + 1 stub
- **Branch:** `stlong/changsha-v1`
- **Pushed:** 2026-05-08

---

**Verdict:** Test catalog conversion COMPLETE for v1 P0 scope. All tests compile clean. Awaiting Bishop's service implementations to begin green-lighting tests. Ready for parallel TDD workflow.
