# Bishop — Fan-catalog integration into the production score path

**Author:** Bishop (Backend Dev)
**Date:** 2026-07-25 (post-W23)
**Branch / PR:** `feat/fan-catalog-integration`
**Hand-off origin:** `.squad/decisions/inbox/frost-fan-catalog.md`

## Decision

Wire Frost's pure-function `Mahjong.Autotable.Api.Changsha.Scoring.FanCalculator`
into `ChangshaGameStateMachine.Score` as an **ADDITIVE** layer on top of the
existing 258-pair small/big-win tier. Fans are detected from the runtime's
authoritative win state, distributed across the existing per-payment
structure (so zero-sum holds without special casing), and surfaced on both
wire transports (bundle WS `ScoreResultEntry` + SignalR `HandFinished.scoreResult`)
with localised Chinese / Pinyin / English labels for the frontend win-screen
modal.

## What shipped

### Schema (additive)

- `ScoreResult` (in `Changsha/ChangshaDomain.cs`) gained:
  - `IReadOnlyList<Scoring.DetectedFan> Fans` — defaults to empty.
  - `int FanPoints` — defaults to 0 (sum of per-payment fan points).
- `ScoreResultEntry` (in `Autotable/AutotableProtocol.cs`) gained:
  - `List<FanEntry> Fans` (camelCase wire id + Chinese/Pinyin/English labels +
    per-payment points).
  - `int FanPoints`.
- New `FanEntry` wire shape — fields `fan` (camelCase Fan enum), `points`,
  `chinese`, `pinyin`, `english`.

**Backward compatibility:** Every existing field on `ScoreResult` /
`ScoreResultEntry` / `ScorePaymentEntry` is preserved. Legacy clients that
deserialize only `category` / `basePoints` / `payments` continue to work
unchanged — the new fields are optional and default to empty.

### Behavior

- `ChangshaGameStateMachine.Score`:
  1. Calls `ScoringService.CalculateScore(..., bigWinPatternCount)` exactly
     as before to produce the BASE score (`Category`, `BasePoints`,
     `Payments`).
  2. Composes `FanContext` from `state.CurrentWin.IsSelfDraw /
     IsKongReplacement / IsRobbedKong / AllPatterns` (HeavenlyHand /
     EarthlyHand / LastTileFromWall / LastDiscardCatch flags) + seat / round
     wind + `FanVariant.Changsha`.
  3. Composes `WinningHand` from `GetHand(state, win.WinningSeatIndex)`.
  4. Runs `FanCalculator.EvaluateHand(hand, ctx)`.
  5. For each detected fan, appends one `PaymentEntry` per existing base
     payment row, with `Amount = fan.Points`, `Reason = "fan:<camelCase>"`,
     mirroring the base row's `(FromSeatIndex, ToSeatIndex)`.
  6. Recomputes `BasePoints` as `Payments.Sum(p => p.Amount)` — the same
     invariant the pre-Wave-2 base score honored.
  7. Stores the full `FanResult.Detected` list + `TotalPoints` on
     `state.CurrentScore`.
  8. Applies all payments (base + fan) to `CumulativeScores` via the same
     pre-existing loop. Zero-sum is preserved by construction.

### Distribution shape (why per-payment-multiplied, not per-fan-flat)

Each fan adds `fan.Points` to EACH existing base payment row. For a
self-draw win (3 base payments — opp→winner × 3 opponents), a `SelfDraw`
fan (1 pt) contributes `1 × 3 = 3` total points distributed as 1 from each
opponent. For a discard win (1 base payment — discarder→winner), the same
fan contributes `1 × 1 = 1` total point. This:

- mirrors how the 258-pair base scaling already differentiates by method
  (`BigWinSelfDrawBase=3` per opp vs `BigWinDiscardBase=6` from discarder);
- keeps zero-sum trivial — every fan-bonus row is a `(from, to, amount)`
  triple just like the base;
- preserves `BasePoints == Payments.Sum(p => p.Amount)`;
- makes per-opponent accounting transparent to auditors / replay.

If a future ruleset wants flat-bonus distribution instead (one fan-bonus
row per detected fan, paid from a single virtual pool), swap
`ApplyFanBonusesToPayments` for a different helper — the rest of the
pipeline doesn't care.

## Pre-existing tests updated

| Test | File:line | Old → New | Reason |
|---|---|---|---|
| `Bot_AllPatterns_StacksContextual` | `Changsha/Acceptance/BotContextualHuTests.cs:458` | `BasePoints == 24` → `BasePoints == 72`; +4 `Assert.Contains` for `Fan.SelfDraw / FullFlush / HeavenlyHand / ConcealedHand` | Dealer self-draw with HeavenlyHand+FullFlush now picks up `SelfDraw(1)+FullFlush(6)+HeavenlyHand(8)+ConcealedHand(1)=16` fan points per payment × 3 base payments = +48 fan bonus on top of the 24-point base. |

This is the **sole** pre-existing test whose hard `BasePoints` expectation
flowed through `ChangshaGameStateMachine.Score`. Every other state-machine
score test either:

- uses `ScoringService.CalculateScore` directly (untouched by my changes —
  the fan layer wraps the service, doesn't modify it). Affected suites:
  `ScoringServiceTests`, `ScoringTests`, `StackedBigWinScoringTests`.
- uses an inequality / ratio assertion that the additive fan layer
  preserves: `EdgeCaseTests.MultipleBigWinPatterns_ScoresStack_DeferredToV2`
  asserts `stacked.BasePoints >= 2 * single.BasePoints` — both sides gain
  the same per-payment fan delta (SelfDraw+AllPungs+ConcealedHand on each;
  stacked additionally gains FullFlush on both sides via the multiplier
  channel, not the fan channel), so the inequality still holds.
- doesn't pin numeric totals: `EndToEndPlayableTests` only asserts
  non-empty payments + zero-sum; `MissedWinPenaltyTests.Player_FalseHu*`
  goes through `RecordFalseHu`, which is independent of the `Score` path.

## New tests

New file `Changsha/Acceptance/FanCatalogIntegrationTests.cs` — 3 tests:

1. **`SelfDrawHu_AddsSelfDrawFanBonusOnTopOfBaseScore`** — dealer
   self-draws a Standard 258-compliant hand. Asserts:
   - `Fans` contains `SelfDraw` + `ConcealedHand`.
   - `FanPoints == 2`.
   - Base payment rows still total `2 × 3 = 6` (SmallWin dealer self-draw).
   - 6 fan-bonus rows (3 base × 2 fans), each `Amount == 1`.
   - `BasePoints == 6 + 6 = 12`.
   - `CumulativeScores.Values.Sum() == 0`.
   - Wire id reasons: `"fan:selfDraw"`, `"fan:concealedHand"`.

2. **`KongReplacementSelfDraw_AddsKongReplacementFanBonus`** — dealer
   declares concealed kong of Tiao-9, draws planted Tong-5 replacement
   off the back of the wall, completes the hand on a 258 Tong-5 pair,
   declares self-draw Hu. Asserts:
   - `state.CurrentWin.IsKongReplacement == true` and `IsSelfDraw == true`
     (pre-Score sanity check on Frost's runtime flags).
   - `Fans` contains `SelfDraw` + `KongReplacement` + `ConcealedHand`
     (concealed kong satisfies 门清 per
     `FanCalculator.IsConcealedHand`).
   - `FanPoints == 4` (1 + 2 + 1).
   - 9 fan-bonus rows (3 fans × 3 base payments), one with
     `Reason == "fan:kongReplacement"` and `Amount == 2`.
   - Zero-sum.

3. **`ScoreResult_FanBreakdown_RoundTripsThroughBundleTranslator`** —
   non-dealer self-draws AllPungs (multi-fan payload), then runs
   `ChangshaToAutotableTranslator.BuildHandResult`. Asserts the
   `HandResultEntry.ScoreResult.Fans` wire shape:
   - non-empty list, `FanPoints > 0`.
   - `selfDraw` entry has `Chinese == "自摸"`, `Pinyin == "zì mō"`,
     `English == "Self-draw"`, `Points == 1`.
   - `allPungs` entry has `Chinese == "碰碰胡"`, `Points == 4`.
   - `concealedHand` entry has `Chinese == "门清"`.
   - Backward-compat: `Category == "bigWin"`, non-zero `BasePoints`,
     non-empty `Payments` — legacy clients keep working.
   - Some `Payments` rows carry `Reason` prefixed `fan:` so a UI that
     prefers the flat-payment list (over the structured `Fans`
     breakdown) can render the same information.

## Test gate

- **5125 backend tests; 5124 pass; 1 fails** (the pre-existing W9
  `^\s*schedule:` regex test on Vasquez's nightly cron workflow,
  documented as unrelated in `bishop-manual-deal-plumb.md` and Frost's W23
  memo). Baseline was 5121 + 1 fail; +3 new tests → 5124 + 1 fail. No new
  regressions.

## Wire surface changes

The new `ScoreResultEntry.fans` field arrives over BOTH the bundle WS
transport (via `ChangshaToAutotableTranslator.BuildHandResult` →
`HandResultEntry.ScoreResult`) AND the SignalR transport (via
`ChangshaGameRuntime.EmitScoringAndHandFinishedAsync` →
`ScoringComplete.handSummary.scoreResult`). Each `FanEntry` is:

```json
{
  "fan": "selfDraw",
  "points": 1,
  "chinese": "自摸",
  "pinyin": "zì mō",
  "english": "Self-draw"
}
```

Order is deterministic enum-declaration order (matches
`FanResult.Detected`). Ferro's win-screen modal can render each entry
as a chip — `points` is the per-payment contribution (multiplied
across the existing base payment count for the displayed total).

## Follow-ups

- **Ferro:** wire `scoreResult.fans` into the win-screen modal chip
  strip. The existing multiplier breakdown stays as-is; fans render
  alongside as their own row. Use the catalog labels directly OR
  look up `fan` against your own i18n catalog if you want a different
  language path.
- **Vasquez:** confirm fan point weights against your reading of Baidu
  §计分. Current values are Frost's first-draft conservative tuning. If
  flat-multipliers (×N rather than +N pts) are preferred, the
  `ApplyFanBonusesToPayments` helper in `ChangshaGameStateMachine.cs` is
  the SOLE pipeline seam — swap it without touching the catalog or the
  detector.
- **Future variant switch:** `FanContext.Variant` is currently hard-coded
  to `FanVariant.Changsha` inside `EvaluateFanBonuses`. When a per-game
  `RuleOptions.Variant` flag lands, thread it through `state` →
  `FanContext.Variant`. `FanCalculator.EvaluateHand` already filters
  variant-gated fans (`MixedOneSuit`, `BigThreeDragons`) correctly — only
  the seam needs widening.

## Status

- [x] Schema additions on `ScoreResult` + `ScoreResultEntry` + new `FanEntry`.
- [x] `ChangshaGameStateMachine.Score` wires `FanCalculator.EvaluateHand`.
- [x] Bundle WS translator surfaces `fans` + `fanPoints`.
- [x] SignalR `HandFinished` payload surfaces `fans` + `fanPoints`.
- [x] One pre-existing test updated (`Bot_AllPatterns_StacksContextual`).
- [x] Three new integration tests in `FanCatalogIntegrationTests`.
- [x] 5124 pass / 1 pre-existing fail.
- [ ] PR opened + admin-merged (in flight).
