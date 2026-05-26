# Frost — Changsha Fan Catalog (beyond 258-pair)

**Author:** Frost (Backend Dev, parallel)
**Date:** 2026-07-25
**Branch / PR:** `feat/changsha-fan-catalog`
**First ship.** Onboarded via PR #83.

## Decision

Ship a standalone, opt-in **fan catalog** layer that expands Changsha
scoring beyond the basic 258-pair small-/big-win tier in
`ScoringService.cs`. The new layer is a pure function and is NOT yet
wired into the default scoring path — wiring is left to Bishop's next
pass to avoid breaking ~dozens of state-machine-driven score
regression tests.

## What shipped

### New files (additive only)

- **`src/backend/src/Mahjong.Autotable.Api/Changsha/Scoring/Fan.cs`**
  — Canonical fan catalog with Pinyin + Chinese + English + base points
  + variant gate per fan.

- **`src/backend/src/Mahjong.Autotable.Api/Changsha/Scoring/FanCalculator.cs`**
  — `FanCalculator.EvaluateHand(WinningHand, FanContext) → FanResult`.
  Stateless, side-effect-free. Variant-gated fans filtered at emission
  time via `ctx.Variant`.

- **`src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Scoring/FanCalculatorTests.cs`**
  — 39 unit tests (positive + negative per fan, catalog integrity,
  deterministic ordering, combinatorial smoke).

### Fans now catalogued

| Fan | Pinyin | Chinese | English | Pts | Variant |
|---|---|---|---|---|---|
| `SelfDraw` | zì mō | 自摸 | Self-draw | 1 | Changsha |
| `KongReplacement` | gàng shàng kāi huā | 杠上开花 | Win on Kong Replacement | 2 | Changsha |
| `LastTileFromWall` | hǎi dǐ lāo yuè | 海底捞月 | Last Tile from the Wall | 2 | Changsha |
| `LastDiscardCatch` | hé dǐ lāo yú | 河底捞鱼 | Last Discard Catch | 2 | Changsha |
| `RobbingKong` | qiǎng gàng | 抢杠 | Robbing the Kong | 2 | Changsha |
| `FullFlush` | qīng yī sè | 清一色 | Pure Suit | 6 | Changsha |
| `MixedOneSuit` | hùn yī sè | 混一色 | Mixed One Suit | 3 | **ExpandedChinese** |
| `SevenPairs` | qī duì | 七对 | Seven Pairs | 4 | Changsha |
| `AllPungs` | pèng pèng hú | 碰碰胡 | All Pungs | 4 | Changsha |
| `ConcealedHand` | mén qīng | 门清 | Concealed Hand | 1 | Changsha |
| `BigThreeDragons` | dà sān yuán | 大三元 | Big Three Dragons | 8 | **ExpandedChinese** |
| `HeavenlyHand` | tiān hé | 天和 | Heavenly Hand | 8 | Changsha |
| `EarthlyHand` | dì hé | 地和 | Earthly Hand | 8 | Changsha |
| `NineTerminals` | jiǔ yāo | 九幺 | Nine Terminals | 6 | Changsha |

### Variant gating

`FanVariant.Changsha` (default) emits only Changsha-tagged fans (no
honors, no dragons — matches the 108-tile deck). `FanVariant.ExpandedChinese`
unlocks `MixedOneSuit` and `BigThreeDragons`. Pure Changsha hands drawn
exclusively from tile-id range `[0,107]` cannot fire either gated fan
even under ExpandedChinese — verified by the negative test
`MixedOneSuit_ExpandedChinese_NoHonorsInPureChangshaDeck_StillSuppressed`.

The expanded-deck encoding the calculator anticipates is documented in
`FanCalculator.TryGetDragon`:
- `108..111` = 中 (red dragon)
- `112..115` = 發 (green dragon)
- `116..119` = 白 (white dragon)

Future expanded-Chinese deck builders MUST follow this convention so
the calculator's dragon detection lights up without code changes.

## Architecture: ADDITIVE, no trunk touched

- **Touched:** None of the no-touch files. Strictly NEW files added.
  - `ChangshaGameRuntime.cs` — untouched (Bishop's trunk)
  - `AutotableWsEndpoint.cs` — untouched (Bishop's trunk)
  - `ChangshaDomain.cs` — untouched (Bishop's trunk)
  - `ScoringService.cs` — untouched (no opt-in wiring this PR)
  - `ChangshaStateMachine.cs` — untouched (no wiring this PR)
  - `WinDetector.cs` — untouched (calculator delegates to it for
    structural patterns)

- **Sibling, not reuse:** `FanContext` is a fresh record in the new
  `Scoring` namespace, deliberately separate from the existing
  `WinContext` used by `ChangshaWinDetector`. Reason: the detector
  context gates STRUCTURAL pattern detection; the fan context layers
  ADDITIVE bonuses. Keeping them apart prevents downstream callers
  from accidentally coupling the layers when one ruleset evolves.

## Integration status: **query-only (option b)**

The task spec preferred option (a) — wire into `ChangshaStateMachine.Score`.
After auditing the existing test surface I went with (b) because:

1. `ChangshaStateMachine.Score` writes to `state.CurrentScore.BasePoints`
   through `ScoringService.CalculateScore`. Existing tests (e.g.
   `EdgeCaseTests`, `Acceptance/MissedWinPenaltyTests`,
   `Acceptance/BotContextualHuTests`,
   `Acceptance/EndToEndPlayableTests`) assert exact `BasePoints` values
   driven through the state machine. Adding fan bonuses additively
   would silently break ~dozens of those tests, even though the
   assertions are arguably correct under the new model.
2. The integration also needs a wire-surface decision: should the fan
   breakdown appear as new `PaymentEntry` rows (with
   `Reason="fan-bonus:selfDraw"` etc.), or as a new `FanResult` field
   on `ScoreResult`? The latter requires touching `ChangshaDomain.cs`
   — Bishop's trunk.

Recommended path for Bishop's next pass:

```csharp
// In ChangshaStateMachine.Score, after the existing CalculateScore call:
var fanCtx = new FanContext
{
    IsSelfDraw = state.CurrentWin.Method == WinMethod.SelfDraw,
    IsKongReplacement = state.LastDrawWasKongReplacement,
    IsLastTileFromWall = state.CurrentWin.AllPatterns.Contains(WinPattern.LastTileFromWall),
    IsLastDiscardCatch = state.CurrentWin.AllPatterns.Contains(WinPattern.LastDiscardCatch),
    IsRobbingKong = state.CurrentWin.IsRobbedKong,
    IsHeavenlyHand = state.CurrentWin.AllPatterns.Contains(WinPattern.HeavenlyHand),
    IsEarthlyHand = state.CurrentWin.AllPatterns.Contains(WinPattern.EarthlyHand),
    SeatWind = state.Seats[state.CurrentWin.WinningSeatIndex].Wind,
    RoundWind = state.RoundWind,
    Variant = FanVariant.Changsha,
};
var winningHand = new WinningHand
{
    ConcealedTileIds = GetHand(state, state.CurrentWin.WinningSeatIndex).ConcealedTiles,
    Melds = GetHand(state, state.CurrentWin.WinningSeatIndex).Melds,
    WinningTileId = state.CurrentWin.WinningTileId,
};
var fanResult = FanCalculator.EvaluateHand(winningHand, fanCtx);
// Add fanResult.TotalPoints as a payment entry, OR mutate state.CurrentScore
// after extending ScoreResult with a FanBreakdown field in ChangshaDomain.cs.
```

The exact wire-surface choice is Bishop's call.

## Risk

Very low — strictly additive code, no existing tests modified. The
calculator is dead code from the runtime's perspective until Bishop
wires it in. Frontend or replay can call it directly today via the
public static `FanCalculator.EvaluateHand`.

## Tests

39 new tests in `FanCalculatorTests`:

- **Catalog integrity (2):** every `Fan` member has a `FanInfo`;
  variant-gated set is exactly `{MixedOneSuit, BigThreeDragons}`.
- **One positive + one negative per fan (≈24):** SelfDraw,
  KongReplacement, LastTileFromWall, LastDiscardCatch, RobbingKong,
  FullFlush, MixedOneSuit (×3 — pure-Changsha suppression, no-honor
  variant suppression, honor-bearing emission), SevenPairs, AllPungs,
  ConcealedHand (×3 — no melds, concealed kong, claimed pung,
  claimed chow), BigThreeDragons (×3 — Changsha suppression, all-three
  dragons emission, two-dragons suppression), HeavenlyHand, EarthlyHand,
  NineTerminals.
- **Combinatorial (4):** Standard + SelfDraw + ConcealedHand stacks;
  discard win with chow → empty; FullFlush + SelfDraw + ConcealedHand
  stacks; deterministic enum-order ordering of emitted fans.
- **Empty result is reusable + idempotent.**

**Full backend suite:** 5121 passed / 1 failed. The 1 failure is the
pre-existing W9 `^\s*schedule:` regex test (Vasquez's nightly cron
workflow self-lane test) — documented as unrelated in
`.squad/decisions/inbox/bishop-manual-deal-plumb.md`.

## Follow-ups (for the inbox)

- **Bishop:** wire `FanCalculator.EvaluateHand` into
  `ChangshaStateMachine.Score`. Recommended payload shape above.
  Likely requires extending `ScoreResult` in `ChangshaDomain.cs` with a
  `FanBreakdown` field (or appending fan-bonus payments to
  `Payments`). Update the existing state-machine score tests for the
  new totals at the same time.

- **Hicks / Ferro:** new `FanResult` is a queryable structure suitable
  for the future fan-breakdown HUD panel. Each `DetectedFan` carries
  the `Fan` enum value — pair it with `FanCatalog.Get(fan)` to render
  Chinese / Pinyin / English / Points / Description.

- **Vasquez:** confirm flat-bonus point weights for each fan match
  your reading of Baidu §计分. Current values are a first-draft
  conservative tuning (situational fans 1-2 pts, suit-purity 3-6 pts,
  hand-shape 4 pts, concealment 1 pt, prestige 6-8 pts). If you'd
  prefer fan-multipliers (×1 / ×2 / ×3) over flat bonuses, ping Frost
  for a refactor — the `FanInfo` record can accommodate either shape.

- **Future variant switch:** wire `FanContext.Variant` through a
  per-game `RuleOptions.Variant` flag so tournament configurations can
  flip between pure Changsha and expanded-Chinese without code edits.

## Status

- [x] Fan enum + catalog
- [x] FanCalculator pure function
- [x] 39 tests green
- [x] No regressions (5121 pass; 1 unrelated pre-existing failure)
- [ ] PR opened + admin-merged (in flight)
- [ ] Bishop integration follow-up (separate PR)
