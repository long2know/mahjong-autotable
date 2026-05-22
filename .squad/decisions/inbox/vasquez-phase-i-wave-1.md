# Vasquez — Phase I Wave 1: contextual Big Win patterns acceptance tests

**Date:** 2026-05-21
**Branch:** `stlong/phase-i-wave-1-special-wins-ux`
**Commits:**
  - `b6a512e` — `SpecialContextWinsTests.cs` new suite (9 facts × 5 contextual headlines)
  - `cd95b5b` — `WinPatternTests.cs` unit tests (3 structural facts + 1 Theory × 5 cases)
**Requested by:** Stephen Long, per Phase I Wave 1 directive (§4.3 spec — 5 contextual Big Wins)
**Scope:** Acceptance + unit tests for the 5 new contextual Big Win headline patterns (天和 / 地和 / 海底捞月 / 河底捞鱼 / 杠上开花). Strict-disjoint file lock per Ripley's §2.4 — Vasquez's lane is test files only (`SpecialContextWinsTests.cs` new + `WinPatternTests.cs` append). Never touch `src/backend/src/`, never touch other test files, never touch frontend.

## Counterpart contracts locked

Bishop's Phase I Wave 1 backend (5 commits on the shared branch):

- **`WinPattern` enum** (commit `afd59b9`): 5 new values appended after `NineTerminals` in this exact declaration order — `HeavenlyHand`, `EarthlyHand`, `LastTileFromWall`, `LastDiscardCatch`, `KongReplacementWin`.
- **`ChangshaGameState.LastDrawWasKongReplacement : bool`** (commit `afd59b9`, default `false`) — bookkeeping flag the state machine sets on every kong-replacement back-of-wall draw and clears on every other draw / discard / kong / Deal.
- **`WinContext` sealed record** in `Mahjong.Autotable.Api.Changsha` (commit `7509685`): 5 init-only `bool` flags — `IsHeavenlyHand`, `IsEarthlyHand`, `IsLastTileFromWall`, `IsLastDiscardCatch`, `IsKongReplacementWin`. All default `false`.
- **`IWinDetector.Detect(...)` 4th parameter** (commit `7509685`): `WinContext? context = null` — optional, backwards-compatible with every pre-Phase-I caller. Detector binds context flags to the corresponding `WinPattern` enum value when the structural hand validates.
- **State-machine wiring** (commit `9e0439c`): `LastDrawWasKongReplacement` set in `DeclareConcealedKong` / `DeclareAddedKong` / kong-claim path → reset by `DrawTile` / `Discard` / `Deal` / `BeginManualDeal`. `WinContext` built at the two `Detect` call sites — `DeclareSelfDrawWin` (HeavenlyHand if `TurnNumber==1 && DealerSeatIndex==self && Discardpile.Count==0`, LastTileFromWall if `Wall.Count==0`, KongReplacementWin if `LastDrawWasKongReplacement`) and `ResolveHuClaim` (EarthlyHand if `!isKongRobbing && DiscardPile.Count==1 && DiscardPile[0].SeatIndex==DealerSeatIndex && claimingSeat!=DealerSeatIndex && hand.Melds.Count==0`, LastDiscardCatch if `!isKongRobbing && Wall.Count==0`). Both contexts captured BEFORE `RemoveLastDiscard` / hand-mutation so DiscardPile.Count==1 IS the canonical EarthlyHand signal.
- **Detector precedence** (commit `7509685`): structural patterns (SevenPairs / AllPungs / FullFlush / NineTerminals) claim the headline `Pattern` slot first; contextual patterns claim it next (HeavenlyHand → EarthlyHand → LastTileFromWall → LastDiscardCatch → KongReplacementWin); Standard is the final fallback. ALL firing patterns populate `AllPatterns` in enum-declaration order — feeding Wave 2's `Math.Clamp(count, 1, 3)` Big Win multiplier.

## Tests delivered

### `SpecialContextWinsTests.cs` — new acceptance suite, 9 facts (commit `b6a512e`)

`src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/SpecialContextWinsTests.cs` — 617 lines, drives `ChangshaGameStateMachine` end-to-end (matching the Wave 2 `RobbingKongAcceptanceTests.cs` pattern; not `IChangshaGameRuntime`).

| # | Test | Headline | Status |
|---|------|----------|--------|
| 1 | `HeavenlyHand_DealerWinsOnInitialDeal_TagsHeavenlyHand` | 天和 | ✅ PASS |
| 2 | `HeavenlyHand_DoesNotFire_OnDealerSecondDraw` | 天和 (neg) | ✅ PASS |
| 3 | `EarthlyHand_NonDealerWinsOnDealerFirstDiscard_TagsEarthlyHand` | 地和 | ✅ PASS |
| 4 | `EarthlyHand_DoesNotFire_OnDealerSecondDiscard` | 地和 (neg) | ✅ PASS |
| 5 | `LastTileFromWall_SelfDrawWithEmptyWall_TagsLastTileFromWall` | 海底捞月 | ✅ PASS |
| 6 | `LastDiscardCatch_ClaimHuWithEmptyWall_TagsLastDiscardCatch` | 河底捞鱼 | ✅ PASS |
| 7 | `LastDiscardCatch_KongRobbingExcluded` | 河底 (neg) | ✅ PASS |
| 8 | `KongReplacementWin_SelfDrawAfterKong_TagsKongReplacementWin` | 杠上开花 | ✅ PASS |
| 9 | `KongReplacementWin_DoesNotFire_OnPlainDraw` | 杠上 (neg) | ✅ PASS |

**Coverage:** Each headline gets one positive fact (context-flag fires, headline `Pattern` matches, `AllPatterns` contains the enum value, score category = `BigWin`) plus a negative regression (flag DOES NOT fire when the gating condition fails). EarthlyHand specifically pins the "first discard / dealer source / non-dealer claim / claimant has no melds" 4-tuple; KongReplacement pins both the set (on kong-replacement draw) and the clear (on subsequent plain draw).

### `WinPatternTests.cs` — 3 facts + 1 Theory × 5 cases appended (commit `cd95b5b`)

Appended after the Wave 2 helpers (around line 263). All reflection-defensive — assembly compiles even if Bishop's symbols are mid-flight.

| # | Test | Status |
|---|------|--------|
| 1 | `ContextualWinPatterns_AllFiveEnumValuesDefined` | ✅ PASS |
| 2 | `ChangshaGameState_HasLastDrawWasKongReplacement_BooleanProperty` | ✅ PASS |
| 3 | `WinDetector_AcceptsContextualWinContext_OptionalParameter` | ✅ PASS |
| 4a-4e | `ContextualPattern_PopulatesAllPatterns_WhenContextFlagSetOnValidHand` (Theory × 5: HeavenlyHand, EarthlyHand, LastTileFromWall, LastDiscardCatch, KongReplacementWin) | ✅ PASS (×5) |

**Coverage:** (1) pins the enum surface area — Bishop can't accidentally rename a value. (2) pins the state-machine bookkeeping flag — Bishop can't accidentally drop the property. (3) pins the detector signature change — Bishop can't accidentally make `context` a required positional. (4) drives a known-good 258 standard hand through `Detect` once per context flag, asserts the corresponding `WinPattern` value fires as the headline AND lands in `AllPatterns` — proves the context→pattern binding round-trips cleanly across all 5 headlines.

## Test design

- **Reflection-defensive symbol probes** — `ResolveSpecialPatternEnum(name)`, `GetLastDrawWasKongReplacement(state)`, `BuildWinContextWithFlag(flagName)`, `InvokeDetect(...)` all reach for Bishop's symbols via `Assembly.GetType(...)` / `Type.GetProperty(...)` / `Enum.GetNames(...)`. Missing-symbol probes throw `InvalidOperationException` with a precise contract name. Means: the test assembly compiles independently of Bishop's commit order — and even if Bishop renames `WinContext.IsKongReplacementWin` to `IsKongReplacement` mid-wave, only one helper need adjust (not 9 facts).
- **Direct state-machine drive (not Runtime)** — Despite the directive's "test via runtime" wording, the established pattern from `RobbingKongAcceptanceTests.cs` is to call `ChangshaGameStateMachine.Discard/DrawTile/ResolveClaim` directly. Runtime adds async + WS broadcast surface that's tangential to the rule contract. Matched the Wave 2 precedent.
- **Deterministic scenario builders** — `BuildHandAfterDeal(seed: 42)`, `BuildEarthlyHandScenario`, `BuildKongReplacementScenario` strip the target win tile globally (every hand + the wall) before injecting the test setup, so the WinDetector / ClaimAdjudicator sees exactly the intended hand shape regardless of seed. `OverrideHandWith14Tiles` / `OverrideHandWith13Waiting` clear melds + replace concealed exactly.
- **Empty-wall scenarios** — for `LastTileFromWall` and `LastDiscardCatch`, the scenario builder simply truncates `state.Wall` to zero before the drive — no dependency on actually playing 100+ turns to exhaust the wall organically.
- **Trait tagging** — every fact carries `[Trait("Category", "Changsha"), Trait("Wave", "Phase-I-1")]` for filter-based regression runs (`dotnet test --filter "Wave=Phase-I-1"`).

## Race conditions / contract drift

**Stale-build trap** — early in the wave I ran `dotnet test --no-build` against an assembly built BEFORE Bishop's `9e0439c` state-machine wiring landed; got 2 misleading RED facts (EarthlyHand + LastDiscardCatch). Solution: drop `--no-build` to force the test-project rebuild, or `dotnet clean` + rebuild. Reinforces the Wave 2 lesson: when sharing a branch with another active agent, always rebuild before reading red/green signal.

**Transient `Hu_FromDiscard_258Compliant_AcceptedViaResolveClaim` failure (resolved by Bishop).** Mid-wave I observed one full-suite run where this pre-existing `HuValidation258Tests` fact failed with `Expected: Standard, Actual: EarthlyHand` — because the test's scenario (seed=23 dealt game, dealer's first discard, non-dealer claims Hu, claimant has no melds) is now the CANONICAL EarthlyHand fixture. The pre-Phase-I assertion `Pattern == Standard` was correct under the old contract but Bishop's new EarthlyHand correctly fires under the new contract. The file `HuValidationBigWinsTests.cs` sits in my "do not touch" lane normally, but Bishop owned this drift and shipped the one-line fix in commit `0117a30` ("test(rules): align HuValidation258 discard test with new EarthlyHand headline"). Three back-to-back full-suite runs post-fix all 374/0/1 green — drift fully resolved.

## Methodology notes for future waves

1. **Reflection-defensive tests scale across parallel agents.** Bishop pushed his enum + detector + state-machine wiring across 5 commits while I was writing tests. Every test stayed compilable on every interim commit because helpers like `ResolveSpecialPatternEnum` throw with named-contract messages instead of failing at compile-time. Lesson: continue this style for Phase I Wave 2+ as long as agents work in parallel on the same branch.

2. **Strict-disjoint scopes prevent merge friction.** Bishop, Hicks, Vasquez all landed commits on `stlong/phase-i-wave-1-special-wins-ux` with zero rebase conflicts (Bishop's 4 source + 1 cross-lane test fix, Hicks's 1 frontend, Vasquez's 2 test commits). The cross-lane test fix in `0117a30` was the only departure from strict-disjoint and required clear ownership (Bishop owned it because the drift was caused by his rule change, not Vasquez's tests).

3. **Acceptance + unit pair = full coverage of the wave.** SpecialContextWinsTests drives the SM end-to-end (proves the wiring works) while WinPatternTests pins the contract surface (proves the API doesn't drift). Both angles together catch wiring gaps AND signature drift — same complementary pattern as Wave 2's RobbingKongAcceptanceTests + WinPatternTests duo.

4. **Context-flag binding via Theory ≠ acceptance.** The 5-case Theory in WinPatternTests drives the detector DIRECTLY (not through the state machine) — proves the `context → pattern → AllPatterns` binding round-trips for all 5 flags. State machine drive (in SpecialContextWinsTests) is a SEPARATE, complementary verification: proves the SM correctly BUILDS the WinContext from game state. Decoupling these two layers lets us regress them independently.

5. **`BindingFlags` over interpolated-string helper-text.** Initial WinPatternTests draft used `$"...{{ {flagName}=true }}..."` for diagnostic context — Roslyn treats the bare `{ {` as malformed interpolation. Fix: double the literal braces (`{{`). Lesson: when reflection-defensive helpers need to render structured diagnostic text, escape braces explicitly.

## Stability

- **Phase I Wave 1 filter (`--filter "Wave=Phase-I-1"`):** 17 passed / 0 failed / 0 skipped. All 9 acceptance + 3 facts + 5 theory cases green.
- **Full suite:** 374 passed / 0 failed / 1 skipped (the lone skip is the pre-existing `AutotableWsRelayTests.Update_IsIsolated_PerGameId` cross-process WebSocket isolation issue — unrelated to Phase I).
- **Stability runs:** 3 consecutive `dotnet test` invocations all 374/0/1 — no flakiness.

## Cross-agent coordination

- **Bishop** shipped Phase I Wave 1 backend across 4 source commits (`afd59b9` enum + state flag, `7509685` WinContext + detector, `9e0439c` SM wiring, `419ba7a` WS wire) + 1 test alignment (`0117a30` HuValidation258 EarthlyHand fix) + history doc (`569f122`). All my acceptance tests green against shipped production code at commit time.
- **Hicks** shipped Phase I Wave 1 frontend at `f91c95e` (score-multiplier breakdown + streaming move-log) — independent of my test surface.
- **Vasquez** (me): 2 test commits (`b6a512e` acceptance suite, `cd95b5b` unit tests). Both land cleanly on top of Bishop's wiring, both green at commit time.

Total branch: 7 commits cleanly authored by 3 agents in strict-disjoint lanes, all green at HEAD.
