namespace Mahjong.Autotable.Api.Changsha.Bot;

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
            _ => MediumInstance
        };
    }

    /// <summary>The default strategy (Medium) — exposed for unit testing.</summary>
    public static IChangshaBotStrategy Default => MediumInstance;

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
    /// Invoked only on timeout; never throws.
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

        if (timeoutMs <= 0)
        {
            return decision();
        }

        var decisionTask = Task.Run(decision, ct);
        var winner = await Task.WhenAny(decisionTask, Task.Delay(timeoutMs, ct)).ConfigureAwait(false);

        if (winner == decisionTask && decisionTask.IsCompletedSuccessfully)
        {
            return decisionTask.Result;
        }

        if (decisionTask.IsCompleted && !decisionTask.IsCompletedSuccessfully)
        {
            return decisionTask.GetAwaiter().GetResult();
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

        return safeDefault();
    }
}
