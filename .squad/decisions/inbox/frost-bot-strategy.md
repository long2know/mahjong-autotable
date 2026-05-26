# Frost — Wave-24 Changsha-aware bot strategy heuristics

**Branch:** `feat/bot-strategy-changsha-heuristics`
**Base:** commit `b9b6482` on `main`
**Files (this PR only):**
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/Heuristics/Shanten.cs` (NEW)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/Heuristics/DiscardEfficiency.cs` (NEW)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/Heuristics/SuitCommitment.cs` (NEW)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/Heuristics/TenpaiDetector.cs` (NEW)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/MasterStrategy.cs` (MODIFIED — wired in heuristics + reasoning)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Bots/BotStrategyTests.cs` (NEW, 20 tests)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Bots/BotSimulationLog.cs` (NEW, gated [Skip])

## Why

Stephen's W24 directive: "single-player should be FUN — bots should play
credibly enough that watching a 4-bot game feels like real Changsha mahjong."
Existing bot tiers were already solid (Easy/Medium/Hard/Master), with
a rigorous shanten counter and per-tier strategies. The gaps were:

1. **`MasterStrategy.cs` docstring promised "suit-purity awareness"** for
   清一色 (FullFlush, 4-fan) but the `ComputeHardCompatibleDiscardScore`
   was bit-for-bit identical to Hard's. Promise was unfulfilled.
2. **No tenpai-aware defensive play.** Hard/Master both use a generic
   "any seat discarded this tile" safety bias, but neither escalates
   defense when an opponent has 3+ open melds (a near-winning shape).
3. **No public `Shanten.Calculate` façade.** The counter was buried
   inside `HandEvaluator.MinShantenToHu` under a non-discoverable name.

## What

Four new modules under `Changsha/Bot/Heuristics/`:

### `Shanten.cs` (heuristic #3)
Public façade over `HandEvaluator.MinShantenToHu` with the conventional
"shanten" name. Exposes `Calculate`, `CalculateAfterDiscardingLogical`,
`CalculateAfterAddingLogical`, and `IsTenpai`. Pure delegation — no
algorithmic change.

### `DiscardEfficiency.cs` (heuristic #1)
Pure scorer implementing the directive's exact formula:
```
efficiency(t, hand) = CountSameSuitNeighbours(t, hand) + 2 * CountSameLogicalMatches(t, hand)
```
Higher = keep, lower = discard. `SelectDiscardByEfficiency` picks the
lowest-efficiency tile with tile-id descending tie-breaker. Reference
implementation untainted by 2/5/8 / gap-partial tuning; useful as a
regression surface for the heuristic math itself.

### `SuitCommitment.cs` (heuristic #4)
Detects when the bot has ≥8 tiles in one suit (configurable threshold,
default 8). When committed, `Bias(tile, hand)` returns −1 for tiles
outside the dominant suit and 0 inside. Declared meld tiles count
toward dominance (a Tong-pung is just as committed as 3 concealed Tong
tiles). Delivers on Master's previously-unfulfilled docstring promise.

### `TenpaiDetector.cs` (heuristic #5)
Flags an opponent as "likely tenpai" when their declared-meld count
reaches ≥3 (default threshold). At that point their concealed buffer
is ≤4 tiles and structurally allows tenpai — matches the practical
Changsha rule "watch out for three open melds". Provides:
- `IsLikelyTenpai(hand)` — O(1) check
- `CollectDangerousOpponents(state, botSeatIndex)` — list of seat ids
- `CollectGenbutsuLogicals(state, opponentSeats)` — proven-safe logicals
- `SafetyBias(tile, state, botSeatIndex)` — −1 when the tile is genbutsu
  against at least one dangerous opponent, else 0

### `MasterStrategy.cs` (composition)
`SelectDiscardTile` gets two additional `ThenBy(...)` tier-breakers,
positioned AFTER the existing opponent-discard safety so the existing
contract (shanten primary, keep-score secondary) holds:
```csharp
return hand.ConcealedTiles
    .OrderBy(t => shantenByLogical[...])                         // primary
    .ThenBy(t => ComputeHardCompatibleDiscardScore(...))         // Hard's keep-score
    .ThenBy(t => OpponentSafetyTieBreaker(...))                  // Master's opponent safety
    .ThenBy(t => TenpaiDetector.SafetyBias(t, state, botSeatIndex))  // W24 (Frost)
    .ThenBy(t => SuitCommitment.Bias(t, hand))                   // W24 (Frost)
    .ThenByDescending(t => t)
    .First();
```
`DecideWithReasoning` also surfaces the new tier signals so the audit
replay shows "tenpai defense: opponent 1 likely tenpai; discard is
genbutsu (safe against them)" and "suit-commitment: dominant=Wan
count=9 (discard outside dominant suit — drives toward 清一色)".

## Tests

`Changsha/Bots/BotStrategyTests.cs` — **20 focused unit tests**, all
passing. Coverage:

| Heuristic | Test count | Sample test names |
|-----------|-----------|--------|
| Shanten façade | 4 | `Shanten_Calculate_Returns0_OnTenpaiHand`, `Shanten_Calculate_RisesAfterRemovingShapeTile`, `Shanten_CalculateAfterAddingLogical_DropsForUsefulDraw`, `Shanten_IsTenpai_TruthOnZeroShanten` |
| Discard efficiency | 4 | `Bot_PrefersIsolatedHonorDiscard_WhenMidTileAvailable`, `Bot_KeepsPairOverIsolatedTile_OnEfficiencyMath`, `Bot_NeighbourTilesContributeToEfficiency`, `Bot_CrossSuitNeighbour_DoesNotContributeToEfficiency` |
| Suit commitment | 3 | `SuitCommitment_PrefersNonDominantDiscard_AboveThreshold`, `SuitCommitment_NeutralBelowThreshold`, `SuitCommitment_DeclaredMelds_CountTowardDominance` |
| Tenpai detector | 4 | `TenpaiDetector_FlagsThreeMeldOpponent_AsDangerous`, `TenpaiDetector_DoesNotFlagTwoMeldOpponent`, `TenpaiDetector_SafetyBias_PrefersGenbutsuAgainstDangerousOpponent`, `TenpaiDetector_SafetyBias_ZeroWhenNoDangerousOpponent` |
| Claim priority | 3 | `Bot_PrioritizesHuOverKong_WhenBothAvailable`, `Bot_PrioritizesPungOverChow_WhenBothAvailable_OnMedium`, `Bot_HuFastPath_AlwaysWins_OnHardStrategy` |
| Master-tier composition | 2 | `MasterStrategy_Reasoning_SurfacesSuitCommitment_WhenCommitted`, `MasterStrategy_Reasoning_SurfacesTenpaiDefense_WhenOpponentDangerous` |

**Test count delta: 5125 → 5145 (+20).** All 5144 non-W9 tests pass;
the W9 cron-schedule failure remains the only pre-existing failure
(unchanged from baseline).

## Simulation (optional — `BotSimulationLog.cs`, [Skip] in CI)

**4×Master 100-hand self-play** (seeds 5000 + 31·i):
```
seat0=23  seat1=20  seat2=32  seat3=12  draws=13
```
87 wins / 13 draws — bots reliably reach winning shapes. Distribution
across seats shows real variation (dealer/seat-3 ratio is normal),
not a degenerate "always-seat-0" pattern.

**Master vs 3×Hard 100 hands** (same seed set):
```
masterWins=22  hardWinsTotal=65  hardAvgPerSeat=21.67  draws=13
```
Master matches Hard's per-seat baseline (22 vs 21.67, ratio 1.015) —
the new tenpai-defense and suit-commitment tiers don't measurably
shift raw win-rate, but they don't regress either. The lift is in
reasoning quality / spectator experience (audit replay surfaces
"tenpai defense" lines, bots commit to 清一色 when 8+ in one suit),
which is exactly the directive's stated goal ("FUN to watch").

## Lane discipline

Did NOT touch (per directive):
- `ChangshaGameStateMachine.cs` (Bishop's trunk)
- `ChangshaGameRuntime.cs`, `AutotableWsEndpoint.cs`, `ChangshaDomain.cs`
- `Persistence/Migrations/**` (Vasquez's in-flight test-isolation work)
- Any frontend file

Modified ONLY:
- `MasterStrategy.cs` (within my lane, additional tier-breakers — no
  contract change to Easy/Medium/Hard)
- New files under `Changsha/Bot/Heuristics/`
- New files under `tests/.../Changsha/Bots/`

## Followups (not in this PR)

1. **Wire `botDifficulty` query string through to runtime.** Currently
   `AutotableWsEndpoint.cs` parses `?botDifficulty=master` but
   `ChangshaGameRuntime` always uses `ChangshaBotEngine.Default` (=Medium)
   regardless. Bishop's call.
2. **Tenpai detector refinement.** The 3-meld threshold is a coarse
   proxy; a future tier could probe each opponent's possible concealed
   configurations against the wall. Out-of-scope for W24.
3. **Replay storage** (deferred to wave 4 per directive — Vasquez's
   test-isolation work merges first).
