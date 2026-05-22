namespace Mahjong.Autotable.Api.Changsha.Runtime;

/// <summary>
/// Tunable timings for the Changsha runtime. Overridable from configuration or test harnesses
/// (E2E tests collapse all delays to zero for speed).
/// </summary>
public sealed class ChangshaRuntimeOptions
{
    /// <summary>Delay before a bot acts on its own turn (after TurnStarted).</summary>
    public int BotTurnDelayMs { get; set; } = 350;

    /// <summary>Delay before a bot responds during a claim window.</summary>
    public int BotClaimDelayMs { get; set; } = 250;

    /// <summary>
    /// Phase F §3 — delay before a bot takes its wall tiles during a manual-deal
    /// pickup phase. Gives clients time to render the dice roll and break-point
    /// animations before tiles start flowing into the bot's hand.
    /// </summary>
    public int BotPickupDelayMs { get; set; } = 500;

    /// <summary>Total claim-window timeout. Clients see this as a countdown hint.</summary>
    public int ClaimWindowTimeoutMs { get; set; } = 5000;

    /// <summary>Delay between TilesDealt batch events to allow client animations.</summary>
    public int DealBatchDelayMs { get; set; } = 0;

    /// <summary>Whether to persist snapshots to the database after each transition.</summary>
    public bool PersistSnapshots { get; set; } = true;

    /// <summary>
    /// Phase H Wave 1 — maximum time a bot strategy may spend in
    /// <see cref="Bot.IChangshaBotStrategy.DecideAction"/> before the runtime
    /// abandons the result and falls back to a safe-default action (turn:
    /// cheapest deterministic discard; claim: Pass). Set to <c>0</c> or a
    /// negative value to disable the timeout (legacy behaviour). The slow
    /// strategy task is allowed to complete in the background and its result
    /// is discarded — bot strategies must remain pure / side-effect free.
    /// </summary>
    public int BotDecisionTimeoutMs { get; set; } = 2000;
}
