# Frost — Scoring End-to-End Audit (Wave-K)

**Author:** Frost (mahjong-autotable squad)
**Date:** 2026-05-29
**Scope:** End-to-end Hu → `FanCalculator` → `ScoreResult` → `PlayerStats` pipeline +
bot-strength sanity check.
**Status:** ✅ Complete — pipeline is intact, no rule regressions found.

## TL;DR

- The Hu → score → persistence pipeline is **wired correctly**. Both self-draw
  Hu (SevenPairs) and discard-Hu (SevenPairs) produce a non-zero `BasePoints`,
  emit the expected fans, preserve zero-sum, and write a row to `PlayerStats`
  with `LastGameAt` set.
- A 3-game streak run correctly increments `GamesPlayed`, `GamesWon`,
  `LongestWinStreak`, `CurrentWinStreak`, and accumulates `TotalScore`.
- The bot-vs-bot 30-hand simulation produces non-zero Hu counts for both
  4×Master and 4×Easy, and the strategy abstraction is observable (swapping
  Master in at seat 0 measurably shifts the per-seat score profile).
- **Two fans are unreachable in the current pure-Changsha 108-tile variant**
  (`MixedOneSuit`, `BigThreeDragons`) — both are correctly variant-gated to
  `FanVariant.ExpandedChinese` and remain as future work behind the deck
  switcher.

## 1. Fan catalog coverage matrix

`FanCalculator` (`Changsha/Scoring/FanCalculator.cs:125`) and the static
`FanCatalog` (`Changsha/Scoring/Fan.cs:156`) define the full 14-fan set. Every
enum member has a `FanCatalog` entry; `FanCalculatorTests.Catalog_HasEntryForEveryFan`
guards drift.

| Fan | Chinese | Points | Variant | Reachable in pure Changsha | Notes |
| --- | --- | --: | --- | :-: | --- |
| `SelfDraw` | 自摸 | 1 | Changsha | ✅ | Asserted by `BotContextualHuTests` + new `SevenPairsSelfDrawHu_ScoresAndPersistsWinnerStats` |
| `KongReplacement` | 杠上开花 | 2 | Changsha | ✅ | Covered by `FanCatalogIntegrationTests` |
| `LastTileFromWall` | 海底捞月 | 2 | Changsha | ✅ | Covered by `FanCatalogIntegrationTests` |
| `LastDiscardCatch` | 河底捞鱼 | 2 | Changsha | ✅ | Covered by `FanCatalogIntegrationTests` |
| `RobbingKong` | 抢杠 | 2 | Changsha | ✅ | Covered by `FanCatalogIntegrationTests` |
| `FullFlush` | 清一色 | 6 | Changsha | ✅ | Covered by `FanCalculatorTests` + `BotContextualHuTests` |
| `MixedOneSuit` | 混一色 | 3 | ExpandedChinese | ❌ (variant-gated) | Requires honor tiles; not present in 108-tile Changsha deck |
| `SevenPairs` | 七对 | 4 | Changsha | ✅ | Asserted by new `HuToScoreToPersistenceTests` (both self-draw and discard paths) |
| `AllPungs` | 碰碰胡 | 4 | Changsha | ✅ | Covered by `FanCalculatorTests` |
| `ConcealedHand` | 门清 | 1 | Changsha | ✅ | Asserted by new `HuToScoreToPersistenceTests` (both paths) |
| `BigThreeDragons` | 大三元 | 8 | ExpandedChinese | ❌ (variant-gated) | Requires dragon tiles; not present in 108-tile Changsha deck |
| `HeavenlyHand` | 天和 | 8 | Changsha | ✅ | Covered by `BotContextualHuTests` (stacks with `FullFlush`) |
| `EarthlyHand` | 地和 | 8 | Changsha | ✅ | Covered by `FanCatalogIntegrationTests` |
| `NineTerminals` | 九幺 | 6 | Changsha | ✅ | Covered by `FanCalculatorTests` |

**Coverage: 12 / 14 reachable, 12 / 12 exercised. The 2 variant-gated fans
behave correctly (`FanContext.Variant = Changsha` suppresses them) and have
unit tests that prove they emit when `Variant = ExpandedChinese`.**

## 2. End-to-end persistence tests (`HuToScoreToPersistenceTests`)

Drives the **real** `ChangshaGameStateMachine` from a post-deal state through
every phase transition (`AwaitingDiscard` → `AwaitingClaim` → `Scoring` →
`EndHand`), then invokes `PlayerProfileService.RecordGameCompletedAsync` against
an in-test SQLite-backed `AppDbContext` and asserts the row landed.

| Test | Path | Outcome | Notes |
| --- | --- | --- | --- |
| `SevenPairsSelfDrawHu_ScoresAndPersistsWinnerStats` | Self-draw Hu via `DeclareSelfDrawWin` | ✅ Pass | BasePoints = 30 (12 base + 18 fan: SevenPairs=4 + SelfDraw=1 + ConcealedHand=1 × 3 payments). Row persisted with `GamesWon=1`, `LastGameAt != null`. |
| `DiscardHu_ScoresAndPersistsWinnerStats` | Discard-Hu via `ResolveClaim(Hu)` | ✅ Pass | SevenPairs-shape on discard (7-pair completing on dealer's Wan-1). Category=`BigWin`, `SelfDraw` fan absent, `ConcealedHand` fan present. |
| `RepeatedHu_AccumulatesWinsAndStreak` | 3 successive self-draws | ✅ Pass | `GamesPlayed=3`, `GamesWon=3`, `LongestWinStreak=3`, `CurrentWinStreak=3`. Score accumulates across `RecordGameCompletedAsync` calls. |

**Gotcha surfaced during construction:** `PlayerProfileService.cs:256` skips
any `PlayerId` starting with `bot-`. The state machine's `CreateGame` seeds
the bot seats with `bot-N` IDs, so tests **must overwrite the seat IDs**
(I use `frost-{label}-seat-{N}`) before invoking the persistence hook —
otherwise no row is written and the test silently asserts nothing.

## 3. Bot-strength simulation (`BotStrengthSimulationTests`)

Permanent unskipped 30-hand simulation with deterministic seeds. Numbers below
are from the audit run (seed family `9000 + i*17`):

```
[4×Master]               hands=30 hu=20 draws=10
                         winsPerSeat=[7,4,5,4]
                         scoreSumPerSeat=[-6,6,-8,8]
                         avgWinnerScore=4.85

[4×Easy]                 hands=30 hu=18 draws=12
                         winsPerSeat=[4,4,6,4]
                         scoreSumPerSeat=[26,5,-11,-20]
                         avgWinnerScore=6.39

[Master@seat0 vs 3×Easy] hands=30 hu=16 draws=14
                         winsPerSeat=[9,3,2,2]
                         scoreSumPerSeat=[38,17,-31,-24]
                         avgWinnerScore=8.56

Score deltas (Master@seat0 minus 4×Easy):
  seat0: +12   seat1: +12   seat2: -20   seat3: -4
```

**Observations**

- **4×Master completes more hands** (20 vs 18 Hu) than 4×Easy, consistent with
  Master's tighter discard heuristics letting hands resolve faster.
- **Master@seat0 banks +9 wins vs 4 for the same seat** under 4×Easy — a
  strong indication the Master heuristic translates to a real seat advantage
  when surrounded by Easy bots.
- **Average winner score is higher in mixed (8.56) than uniform Easy (6.39)
  or uniform Master (4.85)**. The all-Master case has lower per-win value
  because Masters defend better, suppressing big-fan setups that the Easy
  bots can't disrupt.

The test does not pin "Master beats Easy" as a hard equality because
bot-vs-bot variance at 30 hands is real. It DOES pin that swapping a single
strategy must produce SOME observable delta (`PerSeatScoreSum`, `HuCount`, or
`PerSeatHuWins`). If a future regression nullifies the strategy abstraction,
this test fails immediately.

## 4. Gaps / follow-up for future waves

1. **Variant switcher.** `MixedOneSuit` and `BigThreeDragons` are correctly
   suppressed in pure Changsha but there is no user-facing toggle to enable
   `FanVariant.ExpandedChinese`. A future wave should expose this through
   game options and add a 144-tile `ExpandedChinese` deck builder.
2. **Robbing-Kong end-to-end.** `FanCatalogIntegrationTests` covers the fan
   detection in isolation; an end-to-end test that drives an added-kong
   declaration and a robbing-Hu through the state machine + persistence
   would close that loop the way `DiscardHu_ScoresAndPersistsWinnerStats`
   does for the standard discard path.
3. **Stacked fan persistence.** The `BotContextualHuTests` battery already
   asserts in-memory fan stacking (HeavenlyHand + FullFlush + SelfDraw +
   ConcealedHand → BasePoints=72). Adding a persistence-side equivalent that
   confirms `TotalScore` and `HighestSingleGameScore` increment by the
   correct stacked amount would be a useful 1-test addition.
4. **Long-run bot tournament.** The skipped `BotSimulationLog.cs` 100-hand
   tests remain available for on-demand deep analysis. Consider promoting a
   ≤50-hand variant to the always-on suite once CI budget tolerates it.

## 5. Test inventory delta

```
src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/HuToScoreToPersistenceTests.cs   (+3 tests, +1 fixture file)
src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Bots/BotStrengthSimulationTests.cs          (+3 tests)
```

Filter to run the audit suite locally:

```
dotnet test --filter "FullyQualifiedName~HuToScoreToPersistenceTests|FullyQualifiedName~BotStrengthSimulationTests"
```

Filter to run the full scoring-adjacent regression:

```
dotnet test --filter "FullyQualifiedName~FanCalculator|FullyQualifiedName~FanCatalogIntegration|FullyQualifiedName~HuToScoreToPersistence|FullyQualifiedName~BotStrengthSimulation|FullyQualifiedName~BotContextualHu|FullyQualifiedName~ScoringService"
```

→ 61 / 61 pass at audit close.

## 6. Lane discipline

Touched only test files under `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/`
plus the two squad-state files (`.squad/decisions/inbox/frost-scoring-audit.md`,
`.squad/agents/frost/history.md`). No production code under `Changsha/Scoring/**`,
`PlayerProfileService.cs`, `ChangshaGameStateMachine.cs`, or runtime files was
modified.
