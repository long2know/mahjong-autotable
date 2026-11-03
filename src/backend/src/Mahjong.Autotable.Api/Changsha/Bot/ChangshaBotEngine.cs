namespace Mahjong.Autotable.Api.Changsha.Bot;

using Mahjong.Autotable.Api.Tables;
using Microsoft.Extensions.Logging;

/// <summary>
/// Phase F resolver for <see cref="IChangshaBotStrategy"/>. The runtime asks
/// <c>ChangshaBotEngine.Resolve("easy"|"medium"|"hard")</c> on every decision point
/// and gets back a singleton strategy instance. Unknown values fall back to Medium —
/// matches Stephen's UX rule that an unrecognised <c>?bots=hard</c> shouldn't break
/// the game.
/// </summary>
/// <remarks>
/// Strategies are stateless across hands, so a single instance per difficulty is
/// reused for the lifetime of the process. This keeps allocations zero on the hot
/// path during a bot's turn.
/// </remarks>
public static class ChangshaBotEngine
{
    private static readonly IChangshaBotStrategy EasyInstance = new EasyStrategy();
    private static readonly IChangshaBotStrategy MediumInstance = new MediumStrategy();
    private static readonly IChangshaBotStrategy HardInstance = new HardStrategy();
    private static readonly IChangshaBotStrategy MasterInstance = new MasterStrategy();

    /// <summary>
    /// Resolves a difficulty string to its strategy. Case-insensitive; whitespace
    /// trimmed. Empty / null / unrecognised strings → Medium (the default).
    /// </summary>
    public static IChangshaBotStrategy Resolve(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            return MediumInstance;

        return difficulty.Trim().ToLowerInvariant() switch
        {
            "easy" => EasyInstance,
            "medium" => MediumInstance,
            "hard" => HardInstance,
            "master" => MasterInstance,
            _ => MediumInstance
        };
    }

    /// <summary>The default strategy (Medium) — exposed for unit testing.</summary>
    public static IChangshaBotStrategy Default => MediumInstance;

    private static bool IsValidActionType(BotActionType actionType)
        => Enum.IsDefined(actionType);

    private static bool IsValidClaimType(TableClaimType? claimType)
        => claimType is { } value && Enum.IsDefined(value);

    private static bool IsMalformedAction(BotAction action)
    {
        if (!IsValidActionType(action.Type))
            return true;

        return action.Type switch
        {
            BotActionType.Discard => action.TileId is null,
            BotActionType.DeclareAddedKong => action.TileId is null,
            BotActionType.DeclareConcealedKong => action.LogicalTile is null,
            BotActionType.Claim => !IsValidClaimType(action.ClaimType),
            _ => false
        };
    }

    private static string? GetMalformedActionReason(BotAction action)
    {
        if (!IsValidActionType(action.Type))
            return "unknown action type";

        return action.Type switch
        {
            BotActionType.Discard when action.TileId is null => "discard missing tile id",
            BotActionType.DeclareAddedKong when action.TileId is null => "added kong missing tile id",
            BotActionType.DeclareConcealedKong when action.LogicalTile is null => "concealed kong missing logical tile",
            BotActionType.Claim when action.ClaimType is null => "claim missing claim type",
            BotActionType.Claim when !IsValidClaimType(action.ClaimType) => "claim has unknown claim type",
            _ => null
        };
    }

    /// <summary>
    /// Phase H Wave 1 — race a synchronous bot decision against a timeout. If the
    /// decision returns within <paramref name="timeoutMs"/> the result is used;
    /// otherwise <paramref name="safeDefault"/> is invoked and the slow task is
    /// allowed to run to completion in the background (its result is discarded —
    /// strategies are pure / side-effect free).
    /// </summary>
    /// <param name="decision">
    /// The bot decision to invoke (typically a closure over
    /// <see cref="IChangshaBotStrategy.DecideAction"/>). Executed on the thread pool.
    /// </param>
    /// <param name="timeoutMs">
    /// Per-decision budget. <c>0</c> or negative disables the timeout and invokes
    /// <paramref name="decision"/> inline on the calling thread (legacy behaviour).
    /// </param>
    /// <param name="safeDefault">
    /// Factory for the fallback action — supplied by the caller because the safe
    /// default depends on the call site (turn → discard, claim window → pass).
    /// Invoked when timeout/cancellation/fault blocks a trustworthy strategy
    /// outcome; never throws.
    /// </param>
    /// <param name="logger">Optional logger; a warning is emitted on timeout.</param>
    /// <param name="ct">
    /// Lifecycle cancellation. If cancelled, the decision task is abandoned and
    /// <paramref name="safeDefault"/> is invoked.
    /// </param>
    public static async Task<BotAction> DecideActionWithTimeoutAsync(
        Func<BotAction> decision,
        int timeoutMs,
        Func<BotAction> safeDefault,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(safeDefault);

        static BotAction HardPassAction() => BotAction.Pass();

        BotAction CoerceAction(BotAction? action, string source, bool allowSafeDefaultFallback)
        {
            if (action is null)
            {
                if (allowSafeDefaultFallback)
                {
                    logger?.LogWarning("Bot {Source} action factory returned null; using safe-default action fallback.", source);
                    return InvokeSafeDefault();
                }

                logger?.LogWarning("Bot {Source} action factory returned null; using hard pass fallback.", source);
                return HardPassAction();
            }

            if (IsMalformedAction(action))
            {
                var reason = GetMalformedActionReason(action) ?? "malformed action payload";
                if (allowSafeDefaultFallback)
                {
                    logger?.LogWarning(
                        "Bot {Source} action factory returned malformed {ActionType} action ({Reason}); using safe-default action fallback.",
                        source,
                        action.Type,
                        reason);
                    return InvokeSafeDefault();
                }

                logger?.LogWarning(
                    "Bot {Source} action factory returned malformed {ActionType} action ({Reason}); using hard pass fallback.",
                    source,
                    action.Type,
                    reason);
                return HardPassAction();
            }

            return action;
        }

        BotAction InvokeSafeDefault()
        {
            try
            {
                return CoerceAction(safeDefault(), "safe-default", allowSafeDefaultFallback: false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Bot safe-default action factory faulted; using hard pass fallback.");
                return HardPassAction();
            }
        }

        if (timeoutMs <= 0)
        {
            if (ct.IsCancellationRequested)
            {
                logger?.LogDebug("Bot decision cancelled before execution; using safe-default action.");
                return InvokeSafeDefault();
            }

            // Timeout-disabled mode still honours lifecycle cancellation: run the decision on a
            // dedicated worker and race it against the caller token so we can return a safe-default
            // promptly even if the strategy blocks synchronously.
            if (ct.CanBeCanceled)
            {
                var inlineDecisionTask = Task.Factory.StartNew(
                    decision,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, ct);
                await Task.WhenAny(inlineDecisionTask, cancellationTask).ConfigureAwait(false);

                if (ct.IsCancellationRequested || !inlineDecisionTask.IsCompleted || inlineDecisionTask.IsCanceled)
                {
                    logger?.LogDebug("Bot decision cancelled during execution; using safe-default action.");

                    _ = inlineDecisionTask.ContinueWith(
                        static t => { _ = t.Exception; },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    return InvokeSafeDefault();
                }

                if (inlineDecisionTask.IsCompletedSuccessfully)
                    return CoerceAction(inlineDecisionTask.Result, "strategy", allowSafeDefaultFallback: true);

                logger?.LogWarning(
                    inlineDecisionTask.Exception?.GetBaseException(),
                    "Bot decision faulted with timeout disabled; using safe-default action.");
                return InvokeSafeDefault();
            }

            try
            {
                return CoerceAction(decision(), "strategy", allowSafeDefaultFallback: true);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Bot decision faulted with timeout disabled; using safe-default action.");
                return InvokeSafeDefault();
            }
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var decisionTask = Task.Factory.StartNew(
            decision,
            ct,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var delayTask = Task.Delay(timeoutMs, ct);
        await Task.WhenAny(decisionTask, delayTask).ConfigureAwait(false);

        // #124 — the strategy's outcome is honoured ONLY when it demonstrably arrived within the
        // wall-clock budget. Task.WhenAny ordering is NOT authoritative under thread-pool
        // starvation: a synchronous (Thread.Sleep-style) strategy hogs a pool thread while the
        // Task.Delay timeout's own completion is starved past the (slower) strategy, so WhenAny
        // can surface a late strategy result as if it were in-budget — leaking e.g. a false Hu
        // where the safe-default is required. The Stopwatch is immune to that scheduling inversion.
        if (!ct.IsCancellationRequested
            && stopwatch.ElapsedMilliseconds <= timeoutMs
            && decisionTask.IsCompleted)
        {
            if (decisionTask.IsCompletedSuccessfully)
            {
                return CoerceAction(decisionTask.Result, "strategy", allowSafeDefaultFallback: true);
            }

            if (decisionTask.IsCanceled)
            {
                return InvokeSafeDefault();
            }

            logger?.LogWarning(
                decisionTask.Exception?.GetBaseException(),
                "Bot decision faulted within timeout budget; using safe-default action.");
            return InvokeSafeDefault();
        }

        // Lifecycle cancellation should not escape to callers; use safe-default and
        // let any late/faulted strategy task drain in the background.
        if (ct.IsCancellationRequested || decisionTask.IsCanceled)
        {
            logger?.LogDebug("Bot decision cancelled; using safe-default action.");

            _ = decisionTask.ContinueWith(
                static t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return InvokeSafeDefault();
        }

        logger?.LogWarning(
            "Bot decision timed out after {TimeoutMs}ms; using safe-default action.",
            timeoutMs);

        // Let the slow task drain in the background; observe its exception so the
        // task is not flagged as unhandled. Strategies are pure so the result is
        // safely discardable.
        _ = decisionTask.ContinueWith(
            static t => { _ = t.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return InvokeSafeDefault();
    }

    /// <summary>
    /// Phase J Wave 10 — explainable variant of
    /// <see cref="DecideActionWithTimeoutAsync"/>. Same timeout / safe-default
    /// semantics, but threads a <see cref="BotDecision"/> all the way through
    /// so the runtime can capture reasoning + score for the audit replay.
    /// On timeout/cancellation/fault the caller's <paramref name="safeDefault"/>
    /// factory is invoked; the safe default typically wraps a stable
    /// <see cref="BotAction"/> with empty reasoning + score 0.
    /// </summary>
    public static async Task<BotDecision> DecideWithReasoningWithTimeoutAsync(
        Func<BotDecision> decision,
        int timeoutMs,
        Func<BotDecision> safeDefault,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(safeDefault);

        static BotDecision HardPassDecision() => BotDecision.FromAction(BotAction.Pass());

        BotDecision CoerceDecision(BotDecision? maybeDecision, string source, bool allowSafeDefaultFallback)
        {
            if (!maybeDecision.HasValue)
            {
                if (allowSafeDefaultFallback)
                {
                    logger?.LogWarning("Bot {Source} decision factory returned null; using safe-default decision fallback.", source);
                    return InvokeSafeDefault();
                }

                logger?.LogWarning("Bot {Source} decision factory returned null; using hard pass fallback.", source);
                return HardPassDecision();
            }

            var decision = maybeDecision.Value;

            if (decision.Action is null)
            {
                if (allowSafeDefaultFallback)
                {
                    logger?.LogWarning("Bot {Source} decision returned null action; using safe-default decision fallback.", source);
                    return InvokeSafeDefault();
                }

                logger?.LogWarning("Bot {Source} decision returned null action; using hard pass fallback.", source);
                return HardPassDecision();
            }

            if (IsMalformedAction(decision.Action))
            {
                var reason = GetMalformedActionReason(decision.Action) ?? "malformed action payload";
                if (allowSafeDefaultFallback)
                {
                    logger?.LogWarning(
                        "Bot {Source} decision returned malformed {ActionType} action ({Reason}); using safe-default decision fallback.",
                        source,
                        decision.Action.Type,
                        reason);
                    return InvokeSafeDefault();
                }

                logger?.LogWarning(
                    "Bot {Source} decision returned malformed {ActionType} action ({Reason}); using hard pass fallback.",
                    source,
                    decision.Action.Type,
                    reason);
                return HardPassDecision();
            }

            if (decision.Reasoning is null)
            {
                logger?.LogWarning("Bot {Source} decision returned null reasoning; normalizing to empty reasoning.", source);
                decision = decision with { Reasoning = Array.Empty<string>() };
            }

            return decision;
        }

        BotDecision InvokeSafeDefault()
        {
            try
            {
                return CoerceDecision(safeDefault(), "safe-default", allowSafeDefaultFallback: false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Bot safe-default decision factory faulted; using hard pass fallback.");
                return HardPassDecision();
            }
        }

        if (timeoutMs <= 0)
        {
            if (ct.IsCancellationRequested)
            {
                logger?.LogDebug("Bot decision cancelled before execution; using safe-default decision.");
                return InvokeSafeDefault();
            }

            // Timeout-disabled mode still honours lifecycle cancellation: run the decision on a
            // dedicated worker and race it against the caller token so we can return a safe-default
            // promptly even if the strategy blocks synchronously.
            if (ct.CanBeCanceled)
            {
                var inlineDecisionTask = Task.Factory.StartNew(
                    decision,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, ct);
                await Task.WhenAny(inlineDecisionTask, cancellationTask).ConfigureAwait(false);

                if (ct.IsCancellationRequested || !inlineDecisionTask.IsCompleted || inlineDecisionTask.IsCanceled)
                {
                    logger?.LogDebug("Bot decision cancelled during execution; using safe-default decision.");

                    _ = inlineDecisionTask.ContinueWith(
                        static t => { _ = t.Exception; },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    return InvokeSafeDefault();
                }

                if (inlineDecisionTask.IsCompletedSuccessfully)
                    return CoerceDecision(inlineDecisionTask.Result, "strategy", allowSafeDefaultFallback: true);

                logger?.LogWarning(
                    inlineDecisionTask.Exception?.GetBaseException(),
                    "Bot decision faulted with timeout disabled; using safe-default decision.");
                return InvokeSafeDefault();
            }

            try
            {
                return CoerceDecision(decision(), "strategy", allowSafeDefaultFallback: true);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Bot decision faulted with timeout disabled; using safe-default decision.");
                return InvokeSafeDefault();
            }
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var decisionTask = Task.Factory.StartNew(
            decision,
            ct,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var delayTask = Task.Delay(timeoutMs, ct);
        await Task.WhenAny(decisionTask, delayTask).ConfigureAwait(false);

        // #124 — see DecideActionWithTimeoutAsync: honour the strategy's outcome ONLY when it
        // demonstrably arrived within the wall-clock budget, so a late (thread-pool-starved)
        // strategy answer can never be surfaced in place of the safe-default (the Postgres-cell
        // false-Hu flake). The Stopwatch is authoritative where Task.WhenAny ordering is not.
        if (!ct.IsCancellationRequested
            && stopwatch.ElapsedMilliseconds <= timeoutMs
            && decisionTask.IsCompleted)
        {
            if (decisionTask.IsCompletedSuccessfully)
            {
                return CoerceDecision(decisionTask.Result, "strategy", allowSafeDefaultFallback: true);
            }

            if (decisionTask.IsCanceled)
            {
                return InvokeSafeDefault();
            }

            logger?.LogWarning(
                decisionTask.Exception?.GetBaseException(),
                "Bot decision faulted within timeout budget; using safe-default decision.");
            return InvokeSafeDefault();
        }

        if (ct.IsCancellationRequested || decisionTask.IsCanceled)
        {
            logger?.LogDebug("Bot decision cancelled; using safe-default decision.");

            _ = decisionTask.ContinueWith(
                static t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return InvokeSafeDefault();
        }

        logger?.LogWarning(
            "Bot decision timed out after {TimeoutMs}ms; using safe-default decision.",
            timeoutMs);

        _ = decisionTask.ContinueWith(
            static t => { _ = t.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return InvokeSafeDefault();
    }
}
