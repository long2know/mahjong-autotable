### 2026-05-27T22:00:00Z: Frost — Changsha dealing ceremony rule engine (`Changsha/Dealing/ChangshaDealingCeremony.cs`)

**By:** Frost (parallel backend dev), per Stephen's `copilot-directive-2026-05-27T2127Z-face-down-walls.md` directive on the dealing ceremony being wrong.

**What:** Shipped a pure-function rule engine for the Changsha dealing ceremony as a sibling to Bishop's runtime-side state machine. Lives entirely under `src/backend/src/Mahjong.Autotable.Api/Changsha/Dealing/`. Does NOT touch the runtime, the SignalR endpoint, or the translator.

#### Public API

```csharp
namespace Mahjong.Autotable.Api.Changsha.Dealing;

public enum ChangshaDealingPhase { WaitingForDice, PickingFour, PickingOne, DealerExtra, Complete }

public sealed record ChangshaDealingState(
    int DealerSeat,
    int[]? DiceRoll,        // null until rolled; e.g. [3, 5]
    int? StartingWall,      // 0..3, computed from dealer + diceSum
    int? BreakIndex,        // tile offset (= 2 × diceSum) from right end of starting wall
    int CurrentPickerSeat,
    int TilesTakenThisRound,// 0..4 normal rounds; 0..1 dealer-extra
    int RoundIndex,         // 0..2 = PickingFour rounds; 3 = PickingOne/DealerExtra/Complete
    int[] HandSizes,        // per-seat concealed count
    ChangshaDealingPhase Phase);

public sealed record ChangshaDealingResult(
    bool Valid, string? RejectReason, ChangshaDealingState NewState, int TilesPickedUp);

public static class ChangshaDealingCeremony {
    public static ChangshaDealingState Start(int dealerSeat);                            // phase = WaitingForDice
    public static ChangshaDealingState ApplyDiceRoll(ChangshaDealingState s, int[] dice);// phase → PickingFour
    public static ChangshaDealingResult ValidateAndApplyPickup(ChangshaDealingState s, int seatIndex, int requestedCount);
    public static int ComputeStartingWall(int dealerSeat, int diceSum); // (dealer + (sum-1) % 4) % 4
    public static int ComputeBreakIndex(int diceSum);                   // 2 × diceSum tiles in from right
    public static int ExpectedPickupCount(ChangshaDealingPhase phase);
}
```

#### Invariants

- Pure-function transducer — `state` is never mutated; every method returns a fresh `ChangshaDealingState` or a `ChangshaDealingResult`. The input array passed to `ApplyDiceRoll` is cloned so caller mutation does NOT leak into state.
- Programmer-error inputs (`dealerSeat` out of range, `diceSum` outside [2, 12], wrong dice-array shape, applying a roll twice) throw `ArgumentOutOfRangeException` / `ArgumentException` / `InvalidOperationException`.
- Runtime violations (out-of-turn seat, wrong pickup count, pickup before dice or after completion) are NEVER thrown — they are surfaced as `ChangshaDealingResult { Valid = false, RejectReason = "…" }` with `NewState` aliased back to the input state. This lets Bishop's runtime translate them into wire-level error frames without try/catch overhead.
- Turn order is counter-clockwise from the dealer: `CurrentPickerSeat = (DealerSeat + TilesTakenThisRound) % 4`. The picker resets to the dealer at every round boundary.
- `BreakIndex` is measured in **TILES** from the right end of the chosen wall (= `2 × diceSum`), so the runtime can address per-stack-of-2 wall slots without re-deriving the conversion.
- At `Complete`: `HandSizes[dealer] == 14`, `HandSizes[other] == 13`, total = 53 (out of the 108-tile deck — 55 remain in the live wall, matching `DealService.ExpectedRemainingWall`).

#### Integration contract — what Bishop's runtime must call

1. **Game start (manual mode):** `var state = ChangshaDealingCeremony.Start(dealerSeat);` Persist the state alongside the existing `ChangshaGameState`. The wall does NOT need to be shuffled yet — the ceremony only commits to a starting wall + break index after the dice roll.

2. **Dice roll event:** When `RollDice` or `BeginManualDeal` fires, call `state = ChangshaDealingCeremony.ApplyDiceRoll(state, new[] { roll.Die1, roll.Die2 });`. After this returns, `state.StartingWall` and `state.BreakIndex` are populated. Bishop's existing `BreakPointService.ComputeBreakPoint` is equivalent on the wall index but returns *absolute* stack index across the flattened wall — these two layers are compatible because the ceremony engine never references the flattened wall directly; the runtime owns that translation via its existing `ApplyBreakPointToWall` helper.

3. **Per-pickup:** On each `TakeTilesFromWall` command from the wire (or bot policy), call:
   ```csharp
   var result = ChangshaDealingCeremony.ValidateAndApplyPickup(state, seatIndex, requestedCount);
   if (!result.Valid) {
       // emit error frame with result.RejectReason; do NOT mutate game state
       return;
   }
   state = result.NewState;
   // Bishop's runtime then slices result.TilesPickedUp tile-ids off the front
   // of the wall (state.Wall.GetRange(0, result.TilesPickedUp)) and attaches
   // them to the player's concealed hand. Phase + cursor are already advanced.
   ```

4. **Runtime owns tile-id assignment.** This engine is wall-storage-agnostic. It tells you WHEN to pick, WHO is picking, and HOW MANY tiles to slice; the runtime tells you WHICH tile-ids those are.

#### Parity with existing implementation

Bishop's `ChangshaStateMachine.BeginManualDeal` + `TakeTilesFromWall` + `AdvancePickupCursor` already implement an equivalent state machine in the runtime layer. The ceremony engine is intentionally a clean-room reimplementation as a pure-function library: it is the canonical specification of the rules, decoupled from `ChangshaGameState` so future refactors can collapse the duplication without rewriting the rule logic. Both implementations agree on:
- Turn order (CCW from dealer): `(dealer + i) % 4`
- Wall index from dice sum: `(dealer + (sum-1) % 4) % 4`
- Phase progression: PickingFour ×3 → PickingOne → DealerExtra → Complete
- Final hand sizes: 14 / 13 / 13 / 13

#### Suggested follow-up (Bishop's lane)

When Bishop's runtime is ready to consolidate, replace the body of `BeginManualDeal` and `TakeTilesFromWall` with calls into `ChangshaDealingCeremony` and store the returned `ChangshaDealingState` alongside (or in place of) the pickup fields on `ChangshaGameState`. The runtime keeps ownership of:
- Wall shuffling + `ApplyBreakPointToWall` slot translation
- Tile-id assignment + persistence
- Event emission (`dice-rolled`, `tiles-picked-up`, `tiles-dealt`)
- Phase F's auto-deal one-shot path (DealMode.Auto) — unchanged.

#### Tests

`tests/.../Changsha/Dealing/ChangshaDealingCeremonyTests.cs` — 28 distinct test methods that expand under xunit Theories to **76 individual test cases**, all green:
- Start / WaitingForDice initial state
- ApplyDiceRoll happy path, immutability, dice-array cloning, out-of-range dice, wrong dice count, out-of-phase throw
- ComputeStartingWall — 15 cases covering all dice sums (2..12) and dealer rotations (0..3)
- ComputeBreakIndex — 11 cases covering all dice sums (2..12)
- ValidateAndApplyPickup — happy path, out-of-turn, wrong count, before-dice, after-complete, out-of-range seat
- PickingOne phase — count-1 vs count-4 enforcement
- Round-completion rotation (picker resets to dealer)
- Full deal sequence + every-dealer parametrisation + combinatorial smoke (6 dealer/sum pairs)
- Phase transition log (12 PickingFour + 4 PickingOne + 1 DealerExtra = 17 total pickups)
- Dealer-extra "only dealer may pick" enforcement
- Purity assertion on ValidateAndApplyPickup
- ExpectedPickupCount table-driven coverage of all 5 phases

#### Verification

`dotnet test src/backend/Mahjong.Autotable.slnx --nologo` — 5219 pass, 1 pre-existing W9 cron-schedule fail (Vasquez's known flaky). My 76 new tests all green.

#### Lane discipline

- Touched: `src/backend/src/Mahjong.Autotable.Api/Changsha/Dealing/**` (NEW), `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Dealing/**` (NEW)
- Did NOT touch: `Changsha/Runtime/**`, `AutotableWsEndpoint.cs`, `ChangshaToAutotableTranslator.cs`, `ChangshaDomain.cs`, `Changsha/Scoring/**`, persistence, migrations, frontend.
