using System.Collections.Concurrent;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Runtime;

/// <summary>
/// In-memory representation of one active Changsha game. Owns the state machine state,
/// per-seat connection bindings, claim-window pending responses, and a SemaphoreSlim that
/// serializes commands. Intentionally not thread-safe outside the semaphore — the runtime
/// always acquires the lock before mutating.
/// </summary>
internal sealed class ChangshaGameInstance : IAsyncDisposable
{
    public string GameId { get; }
    public ChangshaGameState State { get; }
    public SemaphoreSlim Lock { get; } = new(1, 1);
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    /// <summary>SeatIndex → connectionId. A seat without a connection is a bot or disconnected human.</summary>
    public ConcurrentDictionary<int, string> SeatConnections { get; } = new();

    /// <summary>Seats that have acknowledged the deal (gates progression to TurnStarted).</summary>
    public HashSet<int> DealAcks { get; } = new();

    /// <summary>Pending claim-window responses by seatIndex. null = "not yet responded".</summary>
    public Dictionary<int, ClaimResponse?> PendingClaims { get; } = new();

    /// <summary>Cancellation source for the active claim-window timer (null when no window).</summary>
    public CancellationTokenSource? ClaimWindowCts { get; set; }

    /// <summary>Bot driver loop cancellation (cancelled when game is removed).</summary>
    public CancellationTokenSource LifecycleCts { get; } = new();

    /// <summary>Set true the first time a chow claim arrives without explicit tileIds.</summary>
    public bool LoggedLegacyChowWarning { get; set; }

    /// <summary>
    /// Phase J Wave 10 — most recent <see cref="BotDecision"/> per seat. The
    /// runtime updates this on every bot decision tick (turn-start /
    /// claim window / pickup). The replay-persistence path serialises
    /// the seat's last decision into the v2 envelope's per-event
    /// <c>debugScore</c> field for bot-source events, enabling Hicks's
    /// audit-replay admin tab to drill down into "why did bot 2
    /// discard 5-tiao here". Per-event accuracy is approximate (multiple
    /// state-machine events can flow from one bot decision); operators
    /// get the seat's most-recent reasoning at event time, which is
    /// sufficient for debugging strategy regressions.
    /// </summary>
    public ConcurrentDictionary<int, BotDecision> LastBotDecisions { get; } = new();

    /// <summary>
    /// Per-game bot strategy override (Bishop W25 — `?botDifficulty=` URL
    /// plumbing). When non-null, supersedes the runtime-wide default
    /// (<see cref="ChangshaBotEngine.Default"/>) for every bot decision
    /// dispatched on this instance. Null means "fall back to the runtime
    /// default" so existing call sites that never set a per-game strategy
    /// keep their pre-W25 behaviour. Volatile to publish the assignment
    /// across worker threads; the field is otherwise written once during
    /// game setup and read on every bot turn / claim window.
    /// </summary>
    private IChangshaBotStrategy? _botStrategy;
    public IChangshaBotStrategy? BotStrategy
    {
        get => Volatile.Read(ref _botStrategy);
        set => Volatile.Write(ref _botStrategy, value);
    }

    /// <summary>
    /// #116 (P2) — idempotency guard for fire-and-forget bot scheduling. Holds the set of
    /// <c>(kind, seat)</c> bot actions that are currently scheduled/in-flight so a re-entrant
    /// ceremony transition can't double-dispatch the same actor. <see cref="TryBeginBotSchedule"/>
    /// claims a slot at dispatch time (in the runtime's <c>ScheduleBotIfNeededAsync</c>); the
    /// dispatched task releases it via <see cref="EndBotSchedule"/> in a finally once it has run
    /// (or no-opped). Correctness never depends on this guard alone — every bot task still
    /// re-validates phase/seat under <see cref="Lock"/> — it only suppresses redundant duplicates.
    /// </summary>
    private readonly ConcurrentDictionary<(BotScheduleKind Kind, int Seat), byte> _scheduledBots = new();

    /// <summary>Claims the <c>(kind, seat)</c> bot-schedule slot. Returns false if an identical
    /// bot action is already scheduled/in-flight, in which case the caller must NOT dispatch.</summary>
    public bool TryBeginBotSchedule(BotScheduleKind kind, int seat) => _scheduledBots.TryAdd((kind, seat), 0);

    /// <summary>Releases a previously-claimed <c>(kind, seat)</c> slot. Idempotent.</summary>
    public void EndBotSchedule(BotScheduleKind kind, int seat) => _scheduledBots.TryRemove((kind, seat), out _);

    public ChangshaGameInstance(string gameId, ChangshaGameState state)
    {
        GameId = gameId;
        State = state;
    }

    public async ValueTask DisposeAsync()
    {
        try { LifecycleCts.Cancel(); } catch { }
        ClaimWindowCts?.Cancel();
        ClaimWindowCts?.Dispose();
        LifecycleCts.Dispose();
        Lock.Dispose();
        await Task.CompletedTask;
    }
}

internal sealed record ClaimResponse(TableClaimType? ClaimType, int[]? TileIds);

/// <summary>
/// #116 — the kinds of fire-and-forget bot action the runtime schedules. Used as the key
/// (with the seat index) for the per-instance bot-schedule idempotency guard so that a
/// dealer's manual-deal dice roll, a pickup tick, and a turn decision are tracked separately.
/// </summary>
internal enum BotScheduleKind
{
    /// <summary>Manual-deal dealer dice roll (Phase <c>RollingDice</c>, bot dealer).</summary>
    DealerRoll,
    /// <summary>Manual-deal pickup tick (any <c>IsPickupPhase</c>, bot pickup seat).</summary>
    Pickup,
    /// <summary>Bot turn decision (Phase <c>AwaitingDiscard</c>, bot active seat).</summary>
    Turn
}
