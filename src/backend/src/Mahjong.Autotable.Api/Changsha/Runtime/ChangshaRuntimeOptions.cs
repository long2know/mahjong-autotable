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

    /// <summary>Total claim-window timeout. Clients see this as a countdown hint.</summary>
    public int ClaimWindowTimeoutMs { get; set; } = 5000;

    /// <summary>Delay between TilesDealt batch events to allow client animations.</summary>
    public int DealBatchDelayMs { get; set; } = 0;

    /// <summary>Whether to persist snapshots to the database after each transition.</summary>
    public bool PersistSnapshots { get; set; } = true;
}
