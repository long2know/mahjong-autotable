using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Tables;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;

namespace Mahjong.Autotable.Api.Tests.Changsha.Bots;

/// <summary>
/// #124 — regression coverage for <see cref="ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync"/>
/// (and its non-reasoning sibling). The bot-decision timeout is a safety net: when a strategy
/// exceeds <c>BotDecisionTimeoutMs</c> the runtime MUST use the caller's safe-default (Pass in a
/// claim window) and NEVER surface the strategy's late answer — otherwise a slow "Claim Hu" leaks
/// through as a false Hu.
///
/// <para>The prior implementation decided this purely from <see cref="Task.WhenAny(Task[])"/>
/// ordering (<c>winner == decisionTask</c>). That is not authoritative under thread-pool
/// starvation: a synchronous (<see cref="Thread.Sleep(int)"/>-style) strategy hogs a pool thread
/// while the <see cref="Task.Delay(int)"/> timeout's own completion is starved past the (slower)
/// strategy, so <c>WhenAny</c> surfaces the late strategy result as if it were in-budget. That is
/// exactly the <c>Test (Postgres)</c> matrix flake
/// (<c>BotBehaviorTests.Bot_Timeout_DuringClaim_PassesNotFalseHu</c>: 1/5337, rerun-green). The fix
/// makes the timeout authoritative on the wall clock (a <see cref="System.Diagnostics.Stopwatch"/>),
/// which these tests pin.</para>
/// </summary>
public sealed class BotDecisionTimeoutWrapperTests
{
    private static BotAction PassAction() => BotAction.Pass();
    private static BotDecision Pass() => BotDecision.FromAction(BotAction.Pass());
    private static BotDecision ClaimHu() => BotDecision.FromAction(BotAction.Claim(TableClaimType.Hu));

    [Fact, Trait("Category", "Changsha")]
    public async Task SlowStrategy_ExceedingBudget_YieldsSafeDefault_NeverItsLateResult()
    {
        // 500ms strategy vs 30ms budget — the safe-default Pass must win; the strategy's Claim(Hu)
        // must never surface. Deterministic: the gap is far larger than any scheduling jitter.
        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(500); return ClaimHu(); },
            timeoutMs: 30,
            Pass);

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task FastStrategy_WithinBudget_ReturnsStrategyResult()
    {
        // Opposite guard: an in-budget answer IS honoured — the wall-clock guard must not
        // spuriously fall back for a strategy that answered well within the timeout.
        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            ClaimHu, // no delay
            timeoutMs: 5_000,
            Pass);

        Assert.Equal(BotActionType.Claim, result.Action.Type);
        Assert.Equal(TableClaimType.Hu, result.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task FaultedStrategy_ReturnsSafeDefaultAction_InsteadOfThrowing()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => throw new InvalidOperationException("test: simulated strategy fault"),
            timeoutMs: 500,
            safeDefault: PassAction,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task FaultedStrategy_ReturnsSafeDefaultDecision_InsteadOfThrowing()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => throw new InvalidOperationException("test: simulated strategy fault"),
            timeoutMs: 500,
            safeDefault: Pass,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W115")]
    public async Task FaultedSafeDefaultActionFactory_FallsBackToHardPass()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => throw new InvalidOperationException("test: simulated safe-default fault"),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W115")]
    public async Task FaultedSafeDefaultDecisionFactory_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => throw new InvalidOperationException("test: simulated safe-default fault"),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W116")]
    public async Task NullStrategyAction_UsesSafeDefaultAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => null!,
            timeoutMs: 500,
            safeDefault: () => BotAction.Claim(TableClaimType.Pung),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W116")]
    public async Task NullSafeDefaultAction_FallsBackToHardPass()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => null!,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W116")]
    public async Task NullStrategyDecisionAction_UsesSafeDefaultDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => new BotDecision(null!, null, 7, Array.Empty<string>()),
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, decision.Action.Type);
        Assert.Equal(TableClaimType.Hu, decision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W122")]
    public async Task DefaultStrategyDecision_UsesSafeDefaultDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => default,
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, decision.Action.Type);
        Assert.Equal(TableClaimType.Hu, decision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W116")]
    public async Task NullSafeDefaultDecisionAction_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => new BotDecision(null!, null, 0, Array.Empty<string>()),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W122")]
    public async Task DefaultSafeDefaultDecision_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => default,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W123")]
    public async Task MalformedStrategyAction_MissingClaimType_UsesSafeDefaultAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => new BotAction { Type = BotActionType.Claim },
            timeoutMs: 500,
            safeDefault: () => BotAction.Claim(TableClaimType.Pung),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W123")]
    public async Task MalformedSafeDefaultAction_MissingTileId_FallsBackToHardPassAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => new BotAction { Type = BotActionType.Discard },
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W131")]
    public async Task MalformedStrategyAction_OutOfRangeDiscardTile_UsesSafeDefaultAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.Discard(tileId: -1),
            timeoutMs: 500,
            safeDefault: () => BotAction.Claim(TableClaimType.Pung),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W131")]
    public async Task MalformedSafeDefaultAction_OutOfRangeAddedKongTile_FallsBackToHardPassAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => BotAction.DeclareAddedKong(tileId: -1),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W134")]
    public async Task StrategyAction_PhysicalTilePayloadInConcealedKong_RemapsToLogicalTile()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.DeclareConcealedKong(Tiao(9, 2)),
            timeoutMs: 500,
            safeDefault: () => BotAction.Claim(TableClaimType.Pung),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.DeclareConcealedKong, action.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 2)), action.LogicalTile);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W126")]
    public async Task MalformedStrategyAction_UnknownActionType_UsesSafeDefaultAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => new BotAction { Type = (BotActionType)999 },
            timeoutMs: 500,
            safeDefault: () => BotAction.Claim(TableClaimType.Pung),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W126")]
    public async Task MalformedSafeDefaultAction_UnknownClaimType_FallsBackToHardPassAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => new BotAction { Type = BotActionType.Claim, ClaimType = (TableClaimType)999 },
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W127")]
    public async Task MalformedSafeDefaultAction_UnknownActionType_FallsBackToHardPassAction()
    {
        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => { Thread.Sleep(100); return BotAction.Claim(TableClaimType.Hu); },
            timeoutMs: 1,
            safeDefault: () => new BotAction { Type = (BotActionType)999 },
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W123")]
    public async Task MalformedStrategyDecisionAction_MissingDiscardTile_UsesSafeDefaultDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => new BotDecision(new BotAction { Type = BotActionType.Discard }, null, 0, Array.Empty<string>()),
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, decision.Action.Type);
        Assert.Equal(TableClaimType.Hu, decision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W123")]
    public async Task MalformedSafeDefaultDecisionAction_MissingClaimType_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => new BotDecision(new BotAction { Type = BotActionType.Claim }, null, 0, Array.Empty<string>()),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W134")]
    public async Task StrategyDecision_PhysicalTilePayloadInConcealedKong_RemapsToLogicalTile()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => BotDecision.FromAction(BotAction.DeclareConcealedKong(Tiao(9, 2))),
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.DeclareConcealedKong, decision.Action.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 2)), decision.Action.LogicalTile);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W131")]
    public async Task MalformedSafeDefaultDecisionAction_OutOfRangeClaimTileId_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => BotDecision.FromAction(BotAction.Claim(TableClaimType.Pung, new[] { -1 })),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W126")]
    public async Task MalformedStrategyDecisionAction_UnknownActionType_UsesSafeDefaultDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => new BotDecision(new BotAction { Type = (BotActionType)999 }, null, 0, Array.Empty<string>()),
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Claim, decision.Action.Type);
        Assert.Equal(TableClaimType.Hu, decision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W126")]
    public async Task MalformedSafeDefaultDecisionAction_UnknownClaimType_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => new BotDecision(new BotAction { Type = BotActionType.Claim, ClaimType = (TableClaimType)999 }, null, 0, Array.Empty<string>()),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W127")]
    public async Task MalformedSafeDefaultDecisionAction_UnknownActionType_FallsBackToHardPassDecision()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(100); return ClaimHu(); },
            timeoutMs: 1,
            safeDefault: () => new BotDecision(new BotAction { Type = (BotActionType)999 }, null, 0, Array.Empty<string>()),
            logger: null,
            ct: CancellationToken.None);

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W116")]
    public async Task NullReasoning_IsNormalizedToEmptyList()
    {
        var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => new BotDecision(BotAction.Pass(), null, 11, null!),
            timeoutMs: 500,
            safeDefault: ClaimHu,
            logger: null,
            ct: CancellationToken.None);

        Assert.NotNull(decision.Reasoning);
        Assert.Empty(decision.Reasoning);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task CancellationTokenCancelled_ReturnsSafeDefaultAction_InsteadOfThrowing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.Claim(TableClaimType.Hu),
            timeoutMs: 500,
            safeDefault: PassAction,
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W111")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_UsesSafeDefaultAction()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.Claim(TableClaimType.Hu),
            timeoutMs: 0,
            safeDefault: PassAction,
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W125")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_AndMalformedSafeDefaultAction_FallsBackToHardPass()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.Claim(TableClaimType.Hu),
            timeoutMs: 0,
            safeDefault: () => new BotAction { Type = BotActionType.Discard },
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W128")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_AndUnknownSafeDefaultClaimType_FallsBackToHardPassAction()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
            () => BotAction.Claim(TableClaimType.Hu),
            timeoutMs: 0,
            safeDefault: () => new BotAction { Type = BotActionType.Claim, ClaimType = (TableClaimType)999 },
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W113")]
    public async Task InFlightCancellation_WithTimeoutDisabled_UsesSafeDefaultAction()
    {
        using var cts = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var decisionTask = Task.Run(() =>
            ChangshaBotEngine.DecideActionWithTimeoutAsync(
                () =>
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(5));
                    return BotAction.Claim(TableClaimType.Hu);
                },
                timeoutMs: 0,
                safeDefault: PassAction,
                logger: null,
                ct: cts.Token));

        Assert.True(entered.Wait(TimeSpan.FromSeconds(1)), "Strategy did not start in time.");
        cts.Cancel();

        BotAction action;
        try
        {
            action = await decisionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            release.Set();
        }

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task InFlightCancellation_ReturnsSafeDefaultDecision_InsteadOfLateResult()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            () => { Thread.Sleep(250); return ClaimHu(); },
            timeoutMs: 5_000,
            Pass,
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W112")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_UsesSafeDefaultDecision()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            ClaimHu,
            timeoutMs: 0,
            Pass,
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W125")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_AndMalformedSafeDefaultDecision_FallsBackToHardPass()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            ClaimHu,
            timeoutMs: 0,
            safeDefault: () => new BotDecision(new BotAction { Type = BotActionType.Claim }, null, 0, Array.Empty<string>()),
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W128")]
    public async Task CancellationTokenCancelled_WithTimeoutDisabled_AndUnknownSafeDefaultDecisionClaimType_FallsBackToHardPass()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
            ClaimHu,
            timeoutMs: 0,
            safeDefault: () => new BotDecision(new BotAction { Type = BotActionType.Claim, ClaimType = (TableClaimType)999 }, null, 0, Array.Empty<string>()),
            logger: null,
            ct: cts.Token);

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W114")]
    public async Task InFlightCancellation_WithTimeoutDisabled_UsesSafeDefaultDecision()
    {
        using var cts = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var decisionTask = Task.Run(() =>
            ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
                () =>
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(5));
                    return ClaimHu();
                },
                timeoutMs: 0,
                Pass,
                logger: null,
                ct: cts.Token));

        Assert.True(entered.Wait(TimeSpan.FromSeconds(1)), "Strategy did not start in time.");
        cts.Cancel();

        BotDecision result;
        try
        {
            result = await decisionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            release.Set();
        }

        Assert.Equal(BotActionType.Pass, result.Action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task UnderThreadPoolStarvation_LateStrategyResult_NeverSurfaces()
    {
        // Reproduce the #124 flake mechanism deterministically-enough to guard against regressions:
        // fire many concurrent timeout calls whose synchronous strategy hogs a pool thread for far
        // longer than the tiny budget. The concurrent sleepers starve the Task.Delay timeout
        // continuations, so Task.WhenAny ordering ALONE would occasionally surface a late Claim(Hu).
        // The wall-clock guard makes the safe-default Pass win EVERY time — a single Hu is a leak.
        const int iterations = 300;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var calls = Enumerable.Range(0, iterations).Select(_ =>
            ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
                () => { Thread.Sleep(60); return ClaimHu(); },
                timeoutMs: 10,
                Pass,
                logger: null,
                ct: cts.Token));

        var results = await Task.WhenAll(calls);

        var leaks = results.Count(r => r.Action.Type != BotActionType.Pass);
        Assert.True(leaks == 0,
            $"{leaks}/{iterations} timeout calls surfaced a late strategy result instead of the " +
            $"safe-default Pass — the #124 thread-pool-starvation false-Hu leak.");
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W114")]
    public async Task UnderThreadPoolStarvation_ActionWrapper_LateStrategyResult_NeverSurfaces()
    {
        // Mirror the #124 starvation guard for DecideActionWithTimeoutAsync so both wrapper
        // surfaces stay pinned to timeout-safe defaults under pool pressure.
        const int iterations = 300;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var calls = Enumerable.Range(0, iterations).Select(_ =>
            ChangshaBotEngine.DecideActionWithTimeoutAsync(
                () => { Thread.Sleep(60); return BotAction.Claim(TableClaimType.Hu); },
                timeoutMs: 10,
                safeDefault: PassAction,
                logger: null,
                ct: cts.Token));

        var results = await Task.WhenAll(calls);

        var leaks = results.Count(r => r.Type != BotActionType.Pass);
        Assert.True(leaks == 0,
            $"{leaks}/{iterations} action-wrapper timeout calls surfaced a late strategy result " +
            $"instead of the safe-default Pass.");
    }

    [Fact, Trait("Category", "Changsha")]
    public void ClaimFallback_PrioritizesOfferedHu()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 17, botSeats: new[] { 1 });
        var window = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = ChangshaTestHelpers.Tid(Suit.Wan, 1, 0),
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Hu, Priority = 4 },
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Pung, Priority = 2 }
            }
        };
        state.ClaimWindow = window;
        state.Phase = ChangshaPhase.AwaitingClaim;

        var action = ChangshaGameRuntime.BuildMediumClaimFallbackAction(window, state, seatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Hu, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha")]
    public void ClaimFallback_Passes_WhenSeatHasNoOfferedClaims()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 19, botSeats: new[] { 1 });
        var window = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 2, ClaimType = TableClaimType.Pung, Priority = 2 }
            }
        };
        state.ClaimWindow = window;
        state.Phase = ChangshaPhase.AwaitingClaim;

        var action = ChangshaGameRuntime.BuildMediumClaimFallbackAction(window, state, seatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W128")]
    public void ClaimFallback_UsesBestOfferedNonHuClaim_WhenDecisionStateIsOutOfPhase()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 23, botSeats: new[] { 1 });
        var window = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = ChangshaTestHelpers.Tid(Suit.Wan, 7, 0),
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Chow, Priority = 1 },
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Pung, Priority = 2 }
            }
        };

        // Intentionally out-of-phase snapshot: Medium would emit Wait/Discard here.
        // Fallback must stay within expected claim-window legality.
        state.ClaimWindow = null;
        state.Phase = ChangshaPhase.AwaitingDiscard;

        var action = ChangshaGameRuntime.BuildMediumClaimFallbackAction(window, state, seatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W104")]
    public void NonHuHuOverflowFallback_PreservesMediumPass_WhenOnlyShapeRegressiveNonHuClaimsExist()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(6, 0), Wan(8, 0),
                Tong(2, 0), Tong(3, 0), Tong(9, 0), Tong(9, 1),
                Tiao(3, 0), Tiao(4, 0), Tiao(5, 0), Tiao(5, 1), Tiao(5, 2), Tiao(8, 0), Tiao(9, 0)
            }
        };

        var window = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = Tiao(5, 3),
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Hu, Priority = ChangshaClaimPriority.TierOf(TableClaimType.Hu) },
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Pung, Priority = ChangshaClaimPriority.TierOf(TableClaimType.Pung) }
            }
        };

        var decisionState = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ActiveSeatIndex = 0,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            ClaimWindow = window
        };

        var action = ChangshaGameRuntime.BuildBestNonHuFallbackClaimAction(window, decisionState, seatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W106")]
    public void OwnTurnFallback_IllegalNonWinAction_PrefersLegalConcealedKongBeforeDiscard()
    {
        var concealedTiles = ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 2), (Suit.Wan, 3), (Suit.Wan, 4),
            (Suit.Tong, 2), (Suit.Tong, 3), (Suit.Tong, 4),
            (Suit.Tiao, 6), (Suit.Tiao, 7), (Suit.Tiao, 8), (Suit.Tiao, 9));

        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = concealedTiles
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 1,
            LastDrawSeatIndex = 1,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: -1),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.DeclareConcealedKong, decision.Action.Type);
        Assert.Equal(ChangshaTestHelpers.Logical(Suit.Wan, 1), decision.Action.LogicalTile);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W107")]
    public void OwnTurnFallback_IllegalNonWinAction_PrefersLegalAddedKongBeforeDiscard_WhenNoConcealedKongExists()
    {
        var addedKongTile = Wan(1, 3);
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                addedKongTile,
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new()
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new() { Wan(1, 0), Wan(1, 1), Wan(1, 2) },
                    ClaimedFromSeatIndex = 0
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 1,
            LastDrawSeatIndex = 1,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: -1),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.DeclareAddedKong, decision.Action.Type);
        Assert.Equal(addedKongTile, decision.Action.TileId);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W108")]
    public void OwnTurnFallback_IllegalNonWinAction_PrefersLegalSelfDrawWinBeforeKongOrDiscard()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 27, botSeats: new[] { 1 });
        state.Phase = ChangshaPhase.AwaitingDiscard;
        state.ActiveSeatIndex = 1;
        state.LastDrawSeatIndex = 1;

        var hand = state.Hands.Single(h => h.SeatIndex == 1);
        hand.ConcealedTiles.Clear();
        hand.Melds.Clear();
        hand.ConcealedTiles.AddRange(new[]
        {
            Wan(1, 0), Wan(2, 0), Wan(3, 0),
            Wan(4, 0), Wan(5, 0), Wan(6, 0),
            Tong(1, 0), Tong(2, 0), Tong(3, 0),
            Tiao(4, 0), Tiao(5, 0), Tiao(6, 0),
            Tong(5, 1), Tong(5, 2)
        });

        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, seatIndex: 1));

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: -1),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.DeclareWin, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W109")]
    public void OwnTurnFallback_LegalNonWinAction_StillPromotesLegalSelfDrawWin()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 31, botSeats: new[] { 1 });
        state.Phase = ChangshaPhase.AwaitingDiscard;
        state.ActiveSeatIndex = 1;
        state.LastDrawSeatIndex = 1;

        var hand = state.Hands.Single(h => h.SeatIndex == 1);
        hand.ConcealedTiles.Clear();
        hand.Melds.Clear();
        hand.ConcealedTiles.AddRange(new[]
        {
            Wan(1, 0), Wan(2, 0), Wan(3, 0),
            Wan(4, 0), Wan(5, 0), Wan(6, 0),
            Tong(1, 0), Tong(2, 0), Tong(3, 0),
            Tiao(4, 0), Tiao(5, 0), Tiao(6, 0),
            Tong(5, 1), Tong(5, 2)
        });

        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, seatIndex: 1));

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: Tong(5, 1)),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.DeclareWin, decision.Action.Type);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W110")]
    public void OwnTurnFallback_IllegalNonWinAction_UsesStateAwareMediumDiscardSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0),
                Wan(1, 0),
                Tong(4, 0),
                Tong(7, 0)
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new()
                {
                    SeatIndex = 1,
                    Melds = new List<Meld>
                    {
                        new()
                        {
                            Kind = MeldKind.Pung,
                            TileIds = new List<int>
                            {
                                Wan(2, 0), Wan(2, 1), Wan(2, 2)
                            }
                        },
                        new()
                        {
                            Kind = MeldKind.Pung,
                            TileIds = new List<int>
                            {
                                Tong(6, 0), Tong(6, 1), Tong(6, 2)
                            }
                        },
                        new()
                        {
                            Kind = MeldKind.Pung,
                            TileIds = new List<int>
                            {
                                Tiao(9, 0), Tiao(9, 1), Tiao(9, 2)
                            }
                        }
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tiao(2, 1), TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: -1),
            state,
            hand,
            seatIndex: 0,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.Discard, decision.Action.Type);
        Assert.Equal(Wan(1, 0), decision.Action.TileId);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W132")]
    public void OwnTurnFallback_StaleDiscardCopyId_RemapsBeforeLegalityFallbackUpgrade()
    {
        var concealedTiles = ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 2), (Suit.Wan, 3), (Suit.Wan, 4),
            (Suit.Tong, 2), (Suit.Tong, 3), (Suit.Tong, 4),
            (Suit.Tiao, 6), (Suit.Tiao, 7), (Suit.Tiao, 8), (Suit.Tiao, 9));

        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = concealedTiles
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 1,
            LastDrawSeatIndex = 1,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: Tong(2, 1)),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.Discard, decision.Action.Type);
        Assert.Equal(Tong(2, 0), decision.Action.TileId);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W133")]
    public void OwnTurnFallback_StaleAddedKongCopyId_RemapsBeforeLegalityFallbackUpgrade()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new List<int>
            {
                Wan(1, 3),
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(1, 0), Wan(1, 1), Wan(1, 2) },
                    ClaimedFromSeatIndex = 2
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 1,
            LastDrawSeatIndex = 1,
            Hands = new List<ChangshaHandState>
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.DeclareAddedKong(tileId: Wan(1, 0)),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: fallback");

        Assert.Equal(BotActionType.DeclareAddedKong, decision.Action.Type);
        Assert.Equal(Wan(1, 3), decision.Action.TileId);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W130")]
    public void SelectSafeAddedKongTile_UsesCandidate_WhenTileIsLegal()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 3),
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(1, 0), Wan(1, 1), Wan(1, 2) },
                    ClaimedFromSeatIndex = 2
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var selected = ChangshaGameRuntime.SelectSafeAddedKongTile(state, seatIndex: 0, candidateTileId: Wan(1, 3));

        Assert.Equal(Wan(1, 3), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W130")]
    public void SelectSafeAddedKongTile_RemapsToMatchingLogicalTile_WhenCandidateCopyIsMissing()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 3),
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(1, 0), Wan(1, 1), Wan(1, 2) },
                    ClaimedFromSeatIndex = 2
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var selected = ChangshaGameRuntime.SelectSafeAddedKongTile(state, seatIndex: 0, candidateTileId: Wan(1, 0));

        Assert.Equal(Wan(1, 3), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W130")]
    public void SelectSafeAddedKongTile_ReturnsNull_WhenNoMatchingLegalTileExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 3),
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(1, 0), Wan(1, 1), Wan(1, 2) },
                    ClaimedFromSeatIndex = 2
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var selected = ChangshaGameRuntime.SelectSafeAddedKongTile(state, seatIndex: 0, candidateTileId: Tong(9, 0));

        Assert.Null(selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W137")]
    public void SelectSafeAddedKongTile_LowRangePayload_UsesLogicalInterpretation_WhenPhysicalInterpretationHasNoCandidate()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(6, 3),
                Wan(2, 0), Wan(3, 0), Wan(4, 0),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0), Tiao(9, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(6, 0), Wan(6, 1), Wan(6, 2) },
                    ClaimedFromSeatIndex = 2
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        // 5 is ambiguous in low-range: physical interpretation => logical Wan2,
        // direct logical interpretation => Wan6. Only Wan6 is legal here.
        var selected = ChangshaGameRuntime.SelectSafeAddedKongTile(state, seatIndex: 0, candidateTileId: 5);

        Assert.Equal(Wan(6, 3), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W137")]
    public void SelectSafeAddedKongTile_LowRangePayload_ReturnsNull_WhenPhysicalAndLogicalInterpretationsAreBothLegal()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(2, 3),
                Wan(6, 3),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(2, 0), Wan(2, 1), Wan(2, 2) },
                    ClaimedFromSeatIndex = 2
                },
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(6, 0), Wan(6, 1), Wan(6, 2) },
                    ClaimedFromSeatIndex = 1
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        // 5 is ambiguous in low-range and both interpretations are legal:
        // physical interpretation => Wan2, direct logical interpretation => Wan6.
        // Runtime must not guess.
        var selected = ChangshaGameRuntime.SelectSafeAddedKongTile(state, seatIndex: 0, candidateTileId: 5);

        Assert.Null(selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W138")]
    public void OwnTurnFallback_AmbiguousLowAddedKongPayload_UsesLegalMediumAction()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(2, 3),
                Wan(6, 3),
                Tong(2, 0), Tong(3, 0), Tong(4, 0),
                Tiao(6, 0), Tiao(7, 0), Tiao(8, 0)
            },
            Melds = new List<Meld>
            {
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(2, 0), Wan(2, 1), Wan(2, 2) },
                    ClaimedFromSeatIndex = 2
                },
                new()
                {
                    Kind = MeldKind.Pung,
                    TileIds = new List<int> { Wan(6, 0), Wan(6, 1), Wan(6, 2) },
                    ClaimedFromSeatIndex = 1
                }
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        // Ambiguous low-range payload: physical interpretation => Wan2, direct
        // logical interpretation => Wan6. Runtime remap must refuse guessing and
        // route through a legal medium-policy fallback action.
        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.DeclareAddedKong(tileId: 5),
            state,
            hand,
            seatIndex: 0,
            reasoningLine: "test: ambiguous low added-kong payload fallback");

        Assert.NotEqual(BotActionType.Pass, decision.Action.Type);

        var actionIsLegal = decision.Action.Type switch
        {
            BotActionType.DeclareAddedKong => decision.Action.TileId is int tileId
                && ChangshaGameStateMachine.CanDeclareAddedKong(state, seatIndex: 0, tileId),
            BotActionType.DeclareConcealedKong => decision.Action.LogicalTile is int logicalTile
                && ChangshaGameStateMachine.CanDeclareConcealedKong(state, seatIndex: 0, logicalTile),
            BotActionType.Discard => decision.Action.TileId is int discardTileId
                && hand.ConcealedTiles.Contains(discardTileId),
            BotActionType.DeclareWin => ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, seatIndex: 0),
            _ => false,
        };

        Assert.True(actionIsLegal, "fallback produced an illegal own-turn action");
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W134")]
    public void SelectSafeConcealedKongLogical_UsesCandidate_WhenLogicalTileIsLegal()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(9, 0), Tiao(9, 1), Tiao(9, 2), Tiao(9, 3),
                Wan(1, 0), Wan(2, 0), Wan(3, 0), Wan(4, 0), Wan(5, 0),
                Tong(4, 0), Tong(5, 0), Tong(6, 0), Tong(7, 0), Tong(8, 0)
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var legalLogical = ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0));
        var selected = ChangshaGameRuntime.SelectSafeConcealedKongLogical(state, seatIndex: 0, candidateLogicalTile: legalLogical);

        Assert.Equal(legalLogical, selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W134")]
    public void SelectSafeConcealedKongLogical_RemapsPhysicalTilePayloadToLegalLogicalTile()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(9, 0), Tiao(9, 1), Tiao(9, 2), Tiao(9, 3),
                Wan(1, 0), Wan(2, 0), Wan(3, 0), Wan(4, 0), Wan(5, 0),
                Tong(4, 0), Tong(5, 0), Tong(6, 0), Tong(7, 0), Tong(8, 0)
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var stalePhysicalTilePayload = Tiao(9, 2);
        var selected = ChangshaGameRuntime.SelectSafeConcealedKongLogical(state, seatIndex: 0, candidateLogicalTile: stalePhysicalTilePayload);

        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W136")]
    public void SelectSafeConcealedKongLogical_DoesNotRemapAmbiguousLowPayload()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0), Wan(1, 1), Wan(1, 2), Wan(1, 3),
                Tong(2, 0), Tong(3, 0), Tong(4, 0), Tong(5, 0), Tong(6, 0),
                Tiao(2, 0), Tiao(3, 0), Tiao(4, 0), Tiao(5, 0), Tiao(6, 0)
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var ambiguousLowPayload = Wan(1, 2);
        var selected = ChangshaGameRuntime.SelectSafeConcealedKongLogical(state, seatIndex: 0, candidateLogicalTile: ambiguousLowPayload);

        Assert.Null(selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W136")]
    public void OwnTurnFallback_AmbiguousLowConcealedKongPayload_UsesLegalMediumAction()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0), Wan(1, 1), Wan(1, 2), Wan(1, 3),
                Tong(2, 0), Tong(3, 0), Tong(4, 0), Tong(5, 0), Tong(6, 0),
                Tiao(2, 0), Tiao(3, 0), Tiao(4, 0), Tiao(5, 0), Tiao(6, 0)
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            LastDrawSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.DeclareConcealedKong(Wan(1, 2)),
            state,
            hand,
            seatIndex: 0,
            reasoningLine: "test: ambiguous low concealed-kong payload fallback");

        Assert.Equal(BotActionType.DeclareConcealedKong, decision.Action.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Wan(1, 0)), decision.Action.LogicalTile);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W124")]
    public void SelectSafeDiscardTile_UsesCandidate_WhenTileIsInHand()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(7, 0), Tong(3, 0), Tiao(5, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTile(hand, Tong(3, 0));

        Assert.Equal(Tong(3, 0), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W129")]
    public void SelectSafeDiscardTile_RemapsToMatchingLogicalTile_WhenCandidateCopyIsMissing()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(7, 1), Wan(7, 2), Tong(3, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTile(hand, Wan(7, 0));

        Assert.Equal(Wan(7, 1), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W138")]
    public void SelectSafeDiscardTileOrNull_RemapsLowRangeLogicalPayload_WhenPhysicalInterpretationHasNoLegalCopy()
    {
        // 5 is ambiguous: physical tile-id interpretation => Wan-2 copy 1 (logical 1),
        // direct logical interpretation => Wan-6 (logical 5). When no Wan-2 copy is in
        // hand, runtime should salvage stale logical payloads by remapping to Wan-6.
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(6, 0), Wan(6, 2), Tong(3, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTileOrNull(hand, candidateTileId: 5);

        Assert.Equal(Wan(6, 0), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W138")]
    public void SelectSafeDiscardTileOrNull_KeepsPhysicalInterpretation_WhenBothPhysicalAndLogicalInterpretationsAreLegal()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(2, 2), Wan(6, 0), Tong(3, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTileOrNull(hand, candidateTileId: 5);

        Assert.Equal(Wan(2, 2), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W131")]
    public void SelectSafeDiscardTileOrNull_ReturnsNull_WhenNoLegalMatchingTileExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(7, 1), Wan(7, 2), Tong(3, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTileOrNull(hand, candidateTileId: Tong(9, 0));

        Assert.Null(selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W124")]
    public void SelectSafeDiscardTile_FallsBackToLowestTile_WhenCandidateIsIllegal()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { Wan(7, 0), Tong(3, 0), Tiao(5, 0) }
        };

        var selected = ChangshaGameRuntime.SelectSafeDiscardTile(hand, candidateTileId: -1);

        Assert.Equal(Wan(7, 0), selected);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W121")]
    public void OwnTurnFallback_EmptyConcealedHand_DoesNotThrow_AndReturnsPass()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new List<int>(),
            Melds = new List<Meld>()
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 1,
            LastDrawSeatIndex = 1,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            }
        };

        var decision = ChangshaGameRuntime.BuildMediumOwnTurnFallbackDecision(
            BotAction.Discard(tileId: -1),
            state,
            hand,
            seatIndex: 1,
            reasoningLine: "test: empty concealed hand fallback");

        Assert.Equal(BotActionType.Pass, decision.Action.Type);
    }

    private static int Wan(int rank, int copy = 0) => ((int)Suit.Wan * 9 + (rank - 1)) * 4 + copy;
    private static int Tong(int rank, int copy = 0) => ((int)Suit.Tong * 9 + (rank - 1)) * 4 + copy;
    private static int Tiao(int rank, int copy = 0) => ((int)Suit.Tiao * 9 + (rank - 1)) * 4 + copy;
}
