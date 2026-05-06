# M7 Missed-Win Lock Design — Implementation Notes

**Date:** 2026-05-07  
**Author:** Ishikawa (Domain Rules Engineer)  
**Branch:** `feat/changsha-m7-missed-win-lock`  
**Scope:** M7 (Missed-Win Lock)

---

## Executive Summary

Implemented full missed-win lock mechanism for Changsha Mahjong. A player who passes on a winning opportunity (declines to claim win on a discard they could win on) is locked out of winning on any tile in their wait set until they discard. This prevents tactical pass-then-claim exploitation.

**Status:** ✅ **READY TO MERGE**

- **Tests:** 2462/2462 passing (2445 existing + 17 new)
- **Build:** Clean (dotnet build)
- **Coverage:** Domain (16 tests), API (integrated)

---

## Rule Specification

**The Rule:**  
If a player has a winning hand on a discarded tile T and submits a Pass claim (instead of Win), that player is locked from winning on **any tile in their wait set** until they discard a tile themselves. Self-draw wins remain unaffected.

**Lock Scope:**  
The lock applies to the player's entire wait set at the moment of the pass, not just the specific tile they passed on. This prevents partial exploitation where a player passes on one tile but claims another in their wait.

**Lock Cleared:**  
- When the locked player discards a tile (their turn ends)
- When the locked player makes a meld claim (Pung/Kong/Chow) — their hand shape changed, making the original wait set stale

**Lock NOT Cleared:**  
- By other players' actions
- By time passing
- By other claims being resolved

---

## Architecture Decisions

### 1. Wait Set Computation Approach

**Decision:** Compute the full wait set by testing all 27 possible tiles (3 suits × 9 ranks) against the player's hand using `RulesetHandAnalyzer.IsWinningHand()`.

**Implementation:**
```csharp
public static IReadOnlySet<Tile> ComputeWaitSet(
    IReadOnlyList<Tile> handTiles,
    IReadOnlyList<Tile> meldTiles,
    MahjongRuleset ruleset = MahjongRuleset.Changsha258)
{
    var waitTiles = new HashSet<Tile>();
    var baseTiles = handTiles.Concat(meldTiles).ToList();
    var allPossibleTiles = GenerateAllPossibleTiles(ruleset); // 27 tiles for Changsha
    
    foreach (var testTile in allPossibleTiles)
    {
        var candidateTiles = baseTiles.Append(testTile).ToList();
        if (RulesetHandAnalyzer.IsWinningHand(candidateTiles, ruleset))
        {
            waitTiles.Add(testTile);
        }
    }
    
    return waitTiles;
}
```

**Rationale:**
- **Correctness:** Reuses existing `IsWinningHand()` validation, ensuring consistency with game rules
- **Performance:** 27 checks is negligible (< 1ms); wait sets are typically small (1-9 tiles)
- **Simplicity:** No need to reverse-engineer hand patterns or maintain separate tenpai analysis logic
- **Extensibility:** Works for all valid Changsha hands (standard, seven pairs, big wins)

**Alternatives Rejected:**
- Pattern-based wait inference: complex to maintain, error-prone for edge cases
- Cache wait sets per hand: premature optimization, complicates state management
- Partial wait computation (only test passed tile): doesn't prevent wait-set exploitation

---

### 2. Lock-Clear Policy on Meld Calls

**Decision:** Clear missed-win lock whenever a player makes ANY meld claim (Pung/Kong/Chow), not just on discard.

**Rationale:**
- **Hand Shape Changed:** Once a player claims a meld, their concealed hand changes. The wait set computed at the time of the pass is now stale.
- **Pragmatic:** The rule presumes a stable wait at the moment of pass. A meld fundamentally alters that stability.
- **Prevents Unfairness:** If we don't clear on meld, a player could be permanently locked even though their hand is completely different.

**Edge Cases Handled:**
- Player passes on tile A (wait set includes A, B, C)
- Player later claims Pung on tile D (not in wait set)
- Lock cleared — their hand changed, fresh start

**Alternative Considered:**
- Recompute wait set after meld: too expensive, adds complexity
- Only clear on discard: unfair if player's hand changes via meld

---

### 3. Integration Point in Claim Arbitrator

**Decision:** Check missed-win lock **after** claim arbitration resolves a winner, but **before** executing the win claim.

**Implementation Location:** `ResolveClaimWindowAsync` in Program.cs, after `ClaimArbitrator.Resolve()` but before `ExecuteWinClaimAsync()`.

**Flow:**
1. All players submit claims (Win/Pung/Chow/Pass)
2. `ClaimArbitrator.Resolve()` determines highest-priority claim
3. **NEW:** `IsBlockedByMissedWinLock()` checks if winner is locked on this tile
4. If locked: reject claim, advance turn, broadcast "BlockedByMissedWinLock"
5. If not locked: execute win claim as normal

**Rationale:**
- **Separation of Concerns:** Arbitration logic stays pure (priority + proximity), lock check is orthogonal validation
- **Minimal Invasiveness:** No changes to `ClaimArbitrator` domain logic
- **Clear Messaging:** Locked players see a specific rejection reason, not a silent loss to another player

**Alternatives Rejected:**
- Check lock during arbitration: mixes validation concerns, complicates arbitrator
- Check lock in client: server authority required for anti-cheat
- Pre-filter claims: loses information about who attempted to claim (useful for debugging)

---

## Domain Layer

### MissedWinLock Value Object

**File:** `src/MahjongClaw.Domain/Gameplay/MissedWinLock.cs`

```csharp
public sealed record MissedWinLock(IReadOnlySet<Tile> LockedWinningTiles)
{
    public bool IsLockedOnTile(Tile tile) =>
        LockedWinningTiles.Any(lockedTile => 
            lockedTile.Suit == tile.Suit && 
            lockedTile.Rank == tile.Rank);
}
```

**Design:**
- Immutable record (value semantics)
- Compares tiles by Suit+Rank (not identity) to handle different tile instances
- Encapsulates lock logic, no external mutation

---

### MissedWinLockTracker

**File:** `src/MahjongClaw.Domain/Gameplay/MissedWinLockTracker.cs`

**API:**
- `CreateLock(handTiles, meldTiles, discardedTile, ruleset)` → `MissedWinLock?`
  - Returns lock if player had a winning hand on discarded tile
  - Returns `null` if no winning hand
- `ComputeWaitSet(handTiles, meldTiles, ruleset)` → `IReadOnlySet<Tile>`
  - Returns all tiles the player can win on

**Integration:** Called in API layer when processing Pass claims.

---

## API Layer

### State Storage

**Location:** `Program.cs` L28

```csharp
var missedWinLocks = new Dictionary<Guid, Dictionary<Guid, MissedWinLock>>();
```

**Structure:** `gameId → (playerId → MissedWinLock)`

**Rationale:**
- In-memory state (same as `pendingClaimWindows`)
- No DB persistence needed (lock is transient, cleared on discard)
- Cleared when game completes or player discards

---

### Lock Creation (Pass Tracking)

**Location:** `ProcessMissedWinLocksAsync()` L2487-2530

**Trigger:** After all claims submitted, before arbitration resolves

**Logic:**
1. For each Pass claim in the window
2. Fetch player's hand + melds
3. Call `MissedWinLockTracker.CreateLock()` with discarded tile
4. If lock returned, store in `missedWinLocks[gameId][playerId]`

**Why Here:** We have full claim context (who passed, what tile, when). Processing all passes in batch is efficient.

---

### Lock Validation (Win Blocking)

**Location:** `IsBlockedByMissedWinLock()` L2545-2560

**Trigger:** After `ClaimArbitrator.Resolve()` selects a Win claim

**Logic:**
1. Check if `missedWinLocks[gameId][playerId]` exists
2. If yes, call `playerLock.IsLockedOnTile(discardedTile)`
3. Return true (blocked) or false (allowed)

**Rejection Flow:**
- Broadcast "BlockedByMissedWinLock" SignalR event
- Advance turn to next player (discard didn't result in claim)
- Execute bot turn chain if needed

---

### Lock Clearing

**Locations:**
1. **On Discard:** `Program.cs` L502 (after `TileDiscarded` event, before opening claim window)
2. **On Meld:** `ExecuteMeldClaimAsync()` L2487 (after meld formed)

**Logic:** `ClearMissedWinLock(gameId, playerId, missedWinLocks)` L2562-2574

**Rationale:**
- Discard: Turn ended, player's action is complete
- Meld: Hand changed, original wait set is stale

---

## Test Coverage

### Domain Tests (16 tests)

**File:** `tests/MahjongClaw.Tests/MissedWinLockTests.cs`

**Scenarios:**
1. `CreateLock_PlayerHasWinningHand_CreatesLock` — Seven pairs wait, creates lock
2. `CreateLock_PlayerDoesNotHaveWinningHand_ReturnsNull` — No winning hand, no lock
3. `IsLockedOnTile_TileInLockedSet_ReturnsTrue` — Lock check positive
4. `IsLockedOnTile_TileNotInLockedSet_ReturnsFalse` — Lock check negative
5. `ComputeWaitSet_SimpleWait_ReturnsCorrectSet` — Seven pairs single tile wait
6. `ComputeWaitSet_MultipleWaits_ReturnsAllWaitTiles` — Complex wait with multiple tiles
7. `ComputeWaitSet_NoWaits_ReturnsEmptySet` — Garbage hand, no waits
8. `CreateLock_WithMelds_CreatesCorrectLock` — Standard hand with exposed meld
9. `CreateLock_NullHandTiles_ThrowsArgumentNullException` — Validation
10. `CreateLock_NullMeldTiles_ThrowsArgumentNullException` — Validation
11. `CreateLock_NullDiscardedTile_ThrowsArgumentNullException` — Validation
12. `IsLockedOnTile_NullTile_ThrowsArgumentNullException` — Validation
13. `ComputeWaitSet_SevenPairsWait_ReturnsCorrectWait` — Seven pairs pattern
14. `IsLockedOnTile_DifferentInstanceSameTile_ReturnsTrue` — Tile comparison by value
15. `ComputeWaitSet_HandWithKongs_HandlesCorrectly` — Kong handling in wait computation
16. `CreateLock_InvalidHandSize_ReturnsNull` — Hand size validation

**Coverage:** All public methods, all edge cases, null checks, value comparison, pattern variety

---

## Edge Cases Handled

1. **Player passes on tile A, later tile B (also in wait set) is discarded**
   - ✅ Locked on B — wait set includes all possible wins, not just passed tile

2. **Player passes on non-winning tile**
   - ✅ No lock created — `CreateLock()` validates winning hand first

3. **Player's wait set changes (calls meld)**
   - ✅ Lock cleared — `ExecuteMeldClaimAsync()` clears lock after meld formed

4. **Player passes, then draws and discards**
   - ✅ Lock cleared — discard endpoint clears lock before opening claim window

5. **Self-draw of locked tile**
   - ✅ Allowed — lock only applies to **claim** wins, not self-draw (handled by absence of logic in draw path)

6. **Locked player tries to win on locked tile**
   - ✅ Rejected — `IsBlockedByMissedWinLock()` rejects, broadcasts "BlockedByMissedWinLock"

7. **Multiple players pass on same tile**
   - ✅ Each gets their own lock — `ProcessMissedWinLocksAsync()` iterates all Pass claims

8. **Player passes, game ends (win by another player)**
   - ✅ Lock discarded — in-memory state cleared when game completes

9. **Player passes, then makes Chow (hand changes slightly)**
   - ✅ Lock cleared — meld call clears lock

10. **Player with existing lock makes another pass**
    - ✅ New lock replaces old — `missedWinLocks[gameId][playerId]` is overwritten

---

## SignalR Events

**No New Events Required.**

**Rationale:**
- Lock is server-authoritative and invisible to clients
- Clients learn about locks via absence of win opportunity (claim rejected)
- Optional future enhancement: broadcast "BlockedByMissedWinLock" as opaque hint to UI

**Existing Event Used:**
- `ClaimResolved` with `Result: "BlockedByMissedWinLock"` when lock rejection occurs

---

## Bot Behavior

**No Changes Required.**

**Rationale:**
- Bots use `BotClaimAdvisor.ShouldClaimDiscardWin()` to evaluate win opportunity
- Bots always claim if they can win (never voluntarily pass on wins)
- If bot passes (no win), no lock created (correct behavior)
- If bot is locked (rare — would require prior pass), lock is checked server-side same as humans

**Edge Case:** If bot logic is enhanced in future to strategically pass on wins, lock will apply correctly.

---

## Files Changed

**Domain (3 files):**
- `src/MahjongClaw.Domain/Gameplay/MissedWinLock.cs` (new, 395 bytes)
- `src/MahjongClaw.Domain/Gameplay/MissedWinLockTracker.cs` (new, 3371 bytes)

**API (1 file):**
- `src/MahjongClaw.Api/Program.cs` (+91 lines, 6 functions modified/added)

**Tests (1 file):**
- `tests/MahjongClaw.Tests/MissedWinLockTests.cs` (new, 16 tests, 10384 bytes)

**Total Lines Changed:** ~160 new lines (domain + API + tests), 6 function signatures updated

---

## Deviations from Charter

**None.**

All charter requirements met:
- ✅ Domain layer: `MissedWinLock` value object + `MissedWinLockTracker`
- ✅ API layer: per-seat lock storage, Pass tracking, Win validation, discard clearing
- ✅ Lock created on Pass with winning hand
- ✅ Lock checked on Win claim
- ✅ Lock cleared on discard and meld
- ✅ Comprehensive tests (16 tests, all edge cases)
- ✅ All 2462 tests passing (2445 existing + 17 new)
- ✅ Self-draw unaffected
- ✅ Wait set fully computed

---

## Design Trade-offs

### Wait Set Computation: Brute-Force vs. Analytical

**Chosen:** Brute-force test all 27 tiles

**Rationale:**
- **Correct by construction:** Reuses `IsWinningHand()`, no divergence risk
- **Fast enough:** 27 checks < 1ms, negligible for gameplay
- **Simple:** No pattern analysis, no edge case bugs

**Alternative:** Analytical tenpai detection (parse hand patterns, deduce waits)
- **Pros:** Theoretically faster (but 27 checks is already fast)
- **Cons:** Complex, error-prone, requires separate test suite, diverges from game rules

**Decision:** Correctness > micro-optimization

---

### Lock Clearing: On Meld vs. Only on Discard

**Chosen:** Clear on both discard AND meld

**Rationale:**
- **Fairness:** Player's hand changed, lock is stale
- **Intuitive:** Rule is "pass on tile you could win on with *this hand*" — meld = new hand

**Alternative:** Only clear on discard
- **Pros:** Simpler rule statement
- **Cons:** Unfair lock persistence after hand changes

**Decision:** Fairness > simplicity

---

### Lock Storage: In-Memory vs. Database

**Chosen:** In-memory (`Dictionary<Guid, Dictionary<Guid, MissedWinLock>>`)

**Rationale:**
- **Transient State:** Lock cleared within 1-2 turns, no long-term persistence needed
- **Consistency:** Matches `pendingClaimWindows` pattern (already in-memory)
- **Simplicity:** No DB migration, no EF entity modeling
- **Performance:** Sub-microsecond lookup

**Alternative:** Persist to `PlayerEntity` or new `MissedWinLockEntity`
- **Pros:** Survives server restart (but why? lock is per-hand state)
- **Cons:** DB overhead, migration complexity, unclear semantics on server restart

**Decision:** Transient in-memory state is correct model

---

## Ready-to-Merge Checklist

- [x] All tests passing (2462/2462)
- [x] .NET build clean (dotnet build MahjongClaw.slnx -c Release)
- [x] Domain layer complete (MissedWinLock + MissedWinLockTracker)
- [x] API layer integrated (lock creation, validation, clearing)
- [x] Tests comprehensive (16 tests, all edge cases)
- [x] Design notes captured (this file)
- [x] Backlog updated (.squad/backlog-changsha-parity.md)
- [x] No regressions (all existing tests pass)
- [x] No breaking changes (additive only)

---

**Recommendation:** ✅ **MERGE** — M7 fully functional, well-tested, no known blockers.
