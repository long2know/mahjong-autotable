using System.Collections.Concurrent;
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
