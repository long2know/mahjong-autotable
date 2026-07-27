using System.Collections.Concurrent;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Mahjong.Autotable.Api.Changsha.Runtime;

/// <summary>
/// Drives the full Changsha hub lifecycle. Singleton-scoped: holds in-memory game instances,
/// dispatches state-machine commands, broadcasts wire-shaped events, schedules bot decisions,
/// manages claim-window timers, and persists snapshots after each transition.
/// See `.squad/decisions/inbox/bishop-changsha-v2-runtime.md` for the architecture rationale.
/// </summary>
public interface IChangshaGameRuntime
{
    /// <summary>
    /// Creates a new game. <paramref name="hostPlayerId"/> is the persistent
    /// player identity (cookie-derived in v1, stored on
    /// <see cref="ChangshaGameState.CreatorPlayerId"/>); <paramref name="hostConnectionId"/>
    /// is the SignalR connection id that should be added to the per-game
    /// group on creation. Both are nullable for non-SignalR transports (e.g.
    /// the autotable WS bridge) — Phase J Wave 6 wires the autotable
    /// connection's cookie-derived player id through here so even WS-created
    /// games carry a non-null <c>CreatorPlayerId</c> (unblocks public-lobby
    /// support for WS games).
    /// </summary>
    Task<string> CreateGameAsync(int? seed, int[]? botSeatIndexes, string? hostPlayerId, string? hostConnectionId, CancellationToken ct = default, int? maxHands = null);

    Task JoinTableAsync(string gameId, string connectionId, CancellationToken ct = default);
    /// <summary>
    /// Seats a player. <paramref name="playerId"/> is the persistent
    /// identity (stored on <see cref="ChangshaSeatState.PlayerId"/>);
    /// <paramref name="connectionId"/> is the transport connection id
    /// stored in <see cref="ChangshaGameInstance.SeatConnections"/> for
    /// per-seat private routing. Phase J Wave 6 split (was previously
    /// a single conflated <c>connectionId</c> argument).
    /// </summary>
    Task<int> TakeSeatAsync(string gameId, string playerId, string connectionId, int? seatIndex, CancellationToken ct = default);
    Task FillEmptySeatsWithBotsAsync(string gameId, CancellationToken ct = default);

    /// <summary>
    /// Explicit seat release initiated from the client (e.g. the autotable Player
    /// drawer's "Leave" action which pushes <c>["seats", N, { seat: null }]</c>).
    /// Distinct from <see cref="HandleDisconnectAsync"/>: this is intentional, the
    /// transport stays open, and we clear the persistent
    /// <see cref="ChangshaSeatState.PlayerId"/> in addition to the per-tab
    /// transport binding so the seat actually becomes free for other players.
    ///
    /// <para>No-op when no seat is bound to this connection / playerId — repeat
    /// calls from a flaky client are safe.</para>
    /// </summary>
    Task ReleaseSeatAsync(string gameId, string playerId, string connectionId, CancellationToken ct = default);

    /// <summary>
    /// Propagates a transport-layer deal-mode hint (typically the autotable
    /// WS <c>?dealMode=</c> query param) onto <see cref="ChangshaGameState.DealMode"/>.
    /// Only applies when the game is still in <see cref="ChangshaPhase.Seating"/>:
    /// once the deal (auto or manual) has begun, the mode is locked. Returns
    /// <c>true</c> when the value was applied (including no-op writes of the same
    /// value), <c>false</c> when the game had already started or no game is bound.
    ///
    /// <para>Wave-23 follow-up to W22 (Bishop): closes the gap where the autotable
    /// WS endpoint read <c>?dealMode=manual</c> into <see cref="AutotableConnection.DealMode"/>
    /// but never forwarded it to the runtime, so manual deals silently became
    /// auto deals. The accessor is intentionally idempotent and phase-guarded so
    /// reconnects against a mid-hand game can't flip the mode under us.</para>
    /// </summary>
    Task<bool> ApplyDealModeAsync(string gameId, DealMode mode, CancellationToken ct = default);

    /// <summary>
    /// Bishop W25 — bind a per-game bot strategy override resolved from
    /// the autotable WS endpoint's <c>?botDifficulty=</c> query param
    /// (Easy / Medium / Hard / Master, case-insensitive; unknown values
    /// fall back to Medium via <see cref="ChangshaBotEngine.Resolve"/>).
    /// Before this hook the runtime always dispatched on a single
    /// process-scoped <see cref="ChangshaBotEngine.Default"/> (Medium)
    /// regardless of URL difficulty — the W25 audit memo
    /// (<c>bishop-bots-multigame-audit.md</c>) documents the gap.
    /// Idempotent: re-binding the same difficulty is a no-op; rebinding
    /// to a different difficulty silently replaces the current strategy
    /// (Stephen wanted "hot swap" semantics so a test harness or admin
    /// console can flip difficulty between hands without a restart).
    /// Returns true when applied, false when the game is unknown.
    /// </summary>
    Task<bool> SetBotStrategyAsync(string gameId, string difficulty, CancellationToken ct = default);

    /// <summary>
    /// Bishop W25 — diagnostic accessor reporting the lowercase
    /// difficulty discriminator of the strategy that will be dispatched
    /// for <paramref name="gameId"/> the next time a bot ticks. Returns
    /// the per-game override when one is bound, otherwise the runtime
    /// default's discriminator. Null when the gameId is unknown. Used
    /// by the W25 acceptance tests to assert URL-difficulty plumbing
    /// without exposing the strategy instance itself (the strategies
    /// are stateless singletons but the test harness only needs the
    /// discriminator).
    /// </summary>
    string? GetActiveBotDifficulty(string gameId);

    Task StartGameAsync(string gameId, CancellationToken ct = default, int? expectedVersion = null);

    /// <summary>
    /// Marks a seat as having acknowledged the deal-time TilesDealt payload.
    /// Idempotent: a HashSet under the instance lock dedupes repeat calls, and
    /// the downstream <c>TryAdvanceAfterDealAsync</c> uses a sentinel to prevent
    /// double-starting the turn loop. Safe to invoke implicitly from non-SignalR
    /// transports (e.g. the autotable WS bootstrap) where the bundle has no
    /// explicit ack wiring.
    /// </summary>
    Task AcknowledgeDealAsync(string gameId, int seatIndex, CancellationToken ct = default);

    /// <summary>
    /// Returns the seat index currently bound to <paramref name="connectionId"/>
    /// for <paramref name="gameId"/>, or <c>null</c> if the connection doesn't
    /// own a seat (or the game doesn't exist). Used by the autotable WS endpoint
    /// to implicit-ack deals on behalf of seated human connections, since the
    /// bundle has no AckDeal route of its own.
    /// </summary>
    int? TryGetSeatForConnection(string gameId, string connectionId);
    Task DiscardAsync(string gameId, int seatIndex, int tileId, CancellationToken ct = default, int? expectedVersion = null);
    Task ClaimAsync(string gameId, int seatIndex, string claimType, int[]? tileIds, CancellationToken ct = default, int? expectedVersion = null);
    Task PassAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null);
    Task DeclareKongAsync(string gameId, int seatIndex, int[] tileIds, CancellationToken ct = default, int? expectedVersion = null);
    Task DeclareWinAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null);
    /// <summary>
    /// Rebinds <paramref name="seatIndex"/> to <paramref name="playerId"/> +
    /// <paramref name="connectionId"/>. Phase J Wave 6 splits the previous
    /// single-string argument so the persistent identity (stored on
    /// <see cref="ChangshaSeatState.PlayerId"/>) survives even when the
    /// SignalR transport connection cycles.
    /// </summary>
    Task<bool> ReconnectAsync(string gameId, int seatIndex, string playerId, string connectionId, CancellationToken ct = default);
    /// <summary>
    /// Handles a transport disconnect. <paramref name="playerId"/> is the
    /// persistent identity (used for the host-transfer comparison against
    /// <see cref="ChangshaGameState.CreatorPlayerId"/>);
    /// <paramref name="connectionId"/> identifies the specific transport
    /// connection being released — only seats whose
    /// <see cref="ChangshaGameInstance.SeatConnections"/> entry matches
    /// this connection id are freed (so a player with multiple active
    /// browser tabs doesn't lose their seat when one tab closes).
    /// </summary>
    Task HandleDisconnectAsync(string playerId, string connectionId, CancellationToken ct = default);

    /// <summary>
    /// Phase F §3 — dealer-driven dice roll for manual deal. Transitions the
    /// state from <see cref="ChangshaPhase.RollingDice"/> to
    /// <see cref="ChangshaPhase.BreakPointMarked"/>; the dealer's first 4-tile
    /// pickup follows via <see cref="TakeTilesFromWallAsync"/>. Auto deal mode
    /// (Phase D-backend default) does NOT call this method.
    /// </summary>
    Task RollDiceAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null);

    /// <summary>
    /// Phase F §3 — runtime-driven pickup advance. The seat at the current
    /// pickup cursor takes <paramref name="count"/> tiles from the wall front
    /// (validated by <see cref="ChangshaGameStateMachine.TakeTilesFromWall"/>:
    /// <paramref name="seatIndex"/> must equal <see cref="ChangshaGameState.PickupSeatIndex"/>,
    /// <paramref name="count"/> must equal <see cref="ChangshaGameStateMachine.ExpectedPickupCount"/>).
    /// </summary>
    Task TakeTilesFromWallAsync(string gameId, int seatIndex, int count, CancellationToken ct = default, int? expectedVersion = null);

    /// <summary>Test/diagnostic accessor: returns true if the game exists in memory.</summary>
    bool TryGetSnapshot(string gameId, out ChangshaGameState? state);

    /// <summary>
    /// Lock-protected deep-clone snapshot — guarantees the caller iterates a
    /// stable <see cref="ChangshaGameState"/> graph that cannot be mutated
    /// underneath them by a concurrent runtime operation (DrawTile, Discard,
    /// claim resolution, …).
    ///
    /// <para>Use this instead of <see cref="TryGetSnapshot"/> on any path that
    /// will iterate <c>state.Hands</c>, <c>state.DiscardPile</c>, or
    /// <c>state.Wall</c> outside the runtime's instance lock — most notably the
    /// autotable WS broadcast pipeline. <see cref="TryGetSnapshot"/> returns a
    /// live reference and is safe only for read-once scalar field access.</para>
    ///
    /// <para>The clone is produced by a JSON round-trip under the instance lock
    /// (same serializer the runtime uses for persistence), so all reference-typed
    /// collections on the returned state are independent of the live state.</para>
    /// </summary>
    Task<ChangshaGameState?> TryGetSnapshotCopyAsync(string gameId, CancellationToken ct = default);

    /// <summary>
    /// Test/diagnostic accessor: number of active in-memory games. Used by the
    /// Phase I Wave 2 hydration acceptance tests to assert that a process restart
    /// re-populates the runtime from <c>ChangshaGames.StateJson</c>.
    /// </summary>
    int GameCount { get; }

    /// <summary>
    /// Phase I Wave 2 — replay every non-terminal <c>ChangshaGames</c> row from
    /// persistence into <c>_games</c>. Idempotent: a key that already exists
    /// (e.g. a game that was created on the freshly-booted host before hydration
    /// ran) is left untouched. Safe-fail: a per-row deserialize exception is
    /// swallowed with a warning so one corrupt row cannot prevent the runtime
    /// from coming up. Intended to be called once from <c>Program.cs</c>
    /// immediately after <c>DatabaseBootstrapper.InitializeAsync</c>.
    /// </summary>
    Task HydrateAsync(IServiceProvider services, CancellationToken ct = default);

    /// <summary>
    /// Raised after every applied state mutation, with the affected <c>gameId</c>.
    /// Subscribers (e.g. the autotable WS endpoint) read the current snapshot via
    /// <see cref="TryGetSnapshot"/> and broadcast it. Handlers must not throw; the
    /// event is intentionally fire-and-forget (synchronous invocation).
    /// </summary>
    event Action<string, ChangshaGameState>? StateChanged;

    // ── Phase J Wave 5 — Public matchmaking lobby ────────────────────

    /// <summary>
    /// Phase J Wave 5 — snapshot of currently public, currently
    /// <see cref="ChangshaPhase.Seating"/>-phase games. Sorted newest-first,
    /// capped at <paramref name="max"/> entries. Lock-free read (matches
    /// <see cref="TryGetSnapshot"/> semantics — callers should treat results
    /// as a hint; a game may have started by the time the caller acts).
    /// </summary>
    IReadOnlyList<LobbyGameSnapshot> SnapshotLobbyGames(int max = 50);

    /// <summary>
    /// Phase J Wave 5 — toggle a game's public-listing flag. Only the original
    /// creator (matched by <c>state.CreatorPlayerId == callerPlayerId</c>) may
    /// flip the bit; any other caller throws <see cref="Microsoft.AspNetCore.SignalR.HubException"/>.
    /// </summary>
    Task SetGamePublicAsync(string gameId, string callerPlayerId, bool isPublic, string? publicName, CancellationToken ct = default);

    /// <summary>
    /// Phase J Wave 5 — picks a public, lobby-phase game with at least one
    /// free non-bot seat and seats the caller into it. Phase J Wave 6 split
    /// the single conflated <c>connectionId</c> into a persistent
    /// <paramref name="playerId"/> and a transport
    /// <paramref name="connectionId"/> (passes through to
    /// <see cref="TakeSeatAsync(string,string,string,int?,CancellationToken)"/>).
    /// Returns the chosen <c>(gameId, seatIndex)</c> tuple, or <c>null</c> if
    /// no candidate exists.
    /// </summary>
    Task<(string GameId, int SeatIndex)?> JoinRandomAsync(string playerId, string connectionId, string? variant, CancellationToken ct = default);

    /// <summary>
    /// Phase J Wave 5 — destroys an in-memory game and disposes its
    /// <see cref="ChangshaGameInstance"/>. Used by host-disconnect cleanup
    /// when a public lobby empties out. No-op if the game id is unknown.
    /// </summary>
    Task RemoveGameAsync(string gameId, CancellationToken ct = default);
}

/// <summary>
/// Phase J Wave 5 — denormalised lobby-list row returned by
/// <see cref="IChangshaGameRuntime.SnapshotLobbyGames"/>. Captured under the
/// instance read so the caller doesn't have to re-walk the runtime to project
/// to a wire DTO. <c>CreatorPlayerId</c> is the raw player id; the matchmaking
/// service substitutes a display name via <c>PlayerProfileService</c>.
/// </summary>
public sealed record LobbyGameSnapshot(
    string GameId,
    string? PublicName,
    string? CreatorPlayerId,
    int SeatedCount,
    int MaxSeats,
    string Variant,
    DateTime CreatedAt);

public sealed class ChangshaGameRuntime : IChangshaGameRuntime
{
    private readonly IHubContext<ChangshaHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChangshaRuntimeOptions _options;
    private readonly ILogger<ChangshaGameRuntime> _logger;
    private readonly Players.PlayerProfileService? _profileService;
    private readonly ConcurrentDictionary<string, ChangshaGameInstance> _games = new();
    // Phase H Wave 1 — typed as IChangshaBotStrategy (not the legacy ChangshaBotPolicy
    // facade) so test harnesses can swap in a slow / scripted strategy to exercise the
    // BotDecisionTimeoutMs fallback. Default is the Medium strategy (matches the
    // pre-Phase-H behaviour where ChangshaBotPolicy delegated to ChangshaBotEngine.Resolve("medium")).
    private IChangshaBotStrategy _strategy = ChangshaBotEngine.Default;

    public event Action<string, ChangshaGameState>? StateChanged;

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChangshaGameRuntime(
        IHubContext<ChangshaHub> hub,
        IServiceScopeFactory scopeFactory,
        IOptions<ChangshaRuntimeOptions> options,
        ILogger<ChangshaGameRuntime> logger,
        Players.PlayerProfileService? profileService = null)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        // Phase J Wave 5 — optional so existing test harnesses that construct
        // the runtime directly (without DI) keep compiling. Production wiring
        // in Program.cs injects the service; absence skips stats updates.
        _profileService = profileService;
    }

    public bool TryGetSnapshot(string gameId, out ChangshaGameState? state)
    {
        if (_games.TryGetValue(gameId, out var instance))
        {
            state = instance.State;
            return true;
        }
        state = null;
        return false;
    }

    /// <summary>
    /// Vasquez integration audit A2/A3 — defensive snapshot copy used by the
    /// autotable WS broadcast pipeline. The broadcast translator iterates the
    /// state's List&lt;T&gt; collections (DiscardPile, Hands[i].ConcealedTiles,
    /// Wall) outside the instance lock, so a concurrent DrawTile / Discard /
    /// claim resolution can race the iteration and produce a snapshot that
    /// omits or duplicates entries. The serialised AutotableGameState then
    /// retains stale entries (it stores by tile-id), which surfaces as the
    /// "dealer pile never grew" drift Vasquez observed: the discard was
    /// authoritative in the runtime but missing from the wire snapshot.
    ///
    /// <para>JSON round-trip is intentional — it produces an isolated graph
    /// without any reference sharing back to the live state, so the caller can
    /// iterate without lock guards. Cost is paid only on broadcast, not on
    /// hot-path runtime mutations.</para>
    /// </summary>
    public async Task<ChangshaGameState?> TryGetSnapshotCopyAsync(string gameId, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return null;
        await instance.Lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(instance.State, SnapshotJson);
            return JsonSerializer.Deserialize<ChangshaGameState>(json, SnapshotJson);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    public int? TryGetSeatForConnection(string gameId, string connectionId)
    {
        if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(connectionId)) return null;
        if (!_games.TryGetValue(gameId, out var instance)) return null;
        foreach (var kvp in instance.SeatConnections)
        {
            if (string.Equals(kvp.Value, connectionId, StringComparison.Ordinal))
            {
                return kvp.Key;
            }
        }
        return null;
    }

    public int GameCount => _games.Count;

    // ── Hydration (Phase I Wave 2) ────────────────────────────────────

    public async Task HydrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<ChangshaGame> rows;
        try
        {
            // Pull every persisted snapshot — finished-game filtering is done in
            // memory after deserialization (the entity has no IsFinished column,
            // and Phase I Wave 2 explicitly defers a schema migration).
            rows = await db.ChangshaGames
                .AsNoTracking()
                .Where(g => g.StateJson != null && g.StateJson != "")
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hydration query failed; runtime starting with zero in-memory games.");
            return;
        }

        var hydrated = 0;
        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) break;

            ChangshaGameState? state;
            try
            {
                state = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson, SnapshotJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize snapshot for game {GameId}; skipping.", row.Id);
                continue;
            }

            if (state is null)
            {
                _logger.LogWarning("Snapshot for game {GameId} deserialized to null; skipping.", row.Id);
                continue;
            }

            // Terminal phases: nothing to resume. Phase I Wave 3 widens this to
            // also skip WallExhausted (draw-terminal) — a hand whose wall ran
            // out is functionally finished; the runtime's scoring loop only
            // drains it forward via HandleWallExhaustedAsync when actively
            // playing, which a freshly-hydrated row will never be. Phase J Wave 2
            // added <see cref="ChangshaPhase.GameComplete"/> (N-hand cap terminal),
            // and Phase J Wave 4 merged <see cref="ChangshaPhase.EndGame"/> into
            // <c>GameComplete</c> as a deprecated alias (same underlying int);
            // checking either name is equivalent. We additionally normalize any
            // legacy persisted Phase ordinal that fell outside the named-value
            // set — pre-merger snapshots stored the Wave-2 GameComplete at
            // int 18 (one slot after the old EndGame=17). After the merger
            // both names live at int 17; a stale int-18 snapshot is also a
            // terminal record and rewritten to <c>GameComplete</c> defensively
            // so any downstream comparison (or re-persist) sees a named value.
            if ((int)state.Phase == 18)
            {
                state.Phase = ChangshaPhase.GameComplete;
                state.IsGameComplete = true;
            }
            if (state.Phase == ChangshaPhase.GameComplete ||
                state.Phase == ChangshaPhase.WallExhausted) continue;

            // Authoritative key is the row GUID — guard against a hypothetical
            // drift between the row PK and the embedded state.GameId.
            var gameId = row.Id.ToString();
            if (!string.Equals(state.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Snapshot GameId {EmbeddedGameId} disagrees with row {RowId}; using row id.",
                    state.GameId, gameId);
                state.GameId = gameId;
            }

            var instance = new ChangshaGameInstance(gameId, state);
            if (_games.TryAdd(gameId, instance))
            {
                hydrated++;
            }
            else
            {
                _logger.LogDebug("Game {GameId} already present in runtime; hydration skipped this row.", gameId);
                await instance.DisposeAsync().ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Hydrated {Count} Changsha game(s) from persistence.", hydrated);
    }

    // ── CreateGame ────────────────────────────────────────────────────

    public async Task<string> CreateGameAsync(int? seed, int[]? botSeatIndexes, string? hostPlayerId, string? hostConnectionId, CancellationToken ct = default, int? maxHands = null)
    {
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        var (state, _) = ChangshaGameStateMachine.CreateGame(resolvedSeed, botSeatIndexes);
        // #121/C-2 (Lead decision) — optional match hand cap from the lobby `?handCount=`.
        // null ⇒ keep CreateGame's default (4). Persisted with the rest of the state
        // (full-state JSON snapshot), so it round-trips through hydration unchanged.
        if (maxHands is int mh)
        {
            state.MaxHands = mh;
        }
        // Phase H Wave 1 — StateVersion starts at 0 on a freshly created game; the
        // "game-created" event emitted inside CreateGame is treated as setup, not as
        // a mutation that consumes a version slot. First real mutation advances to 1.
        state.StateVersion = 0;
        // Phase J Wave 6 — record the creator's persistent player id as the
        // host identity. Used by MatchmakingService.SetGamePublic for the
        // only-host-may-toggle check and by HandleDisconnectAsync for
        // host-transfer / auto-destroy on public games. Wave-6 split the
        // previous single-id parameter so non-SignalR transports (autotable
        // WS bridge) can pass through their cookie-derived player id even
        // when no SignalR connection exists — this is what unblocks
        // public-lobby support for autotable-WS games (Vasquez's Wave-5
        // blind spot #4).
        state.CreatorPlayerId = string.IsNullOrEmpty(hostPlayerId) ? null : hostPlayerId;
        var instance = new ChangshaGameInstance(state.GameId, state);
        _games[state.GameId] = instance;

        if (!string.IsNullOrEmpty(hostConnectionId))
        {
            await _hub.Groups.AddToGroupAsync(hostConnectionId, state.GameId, ct);
        }

        await PersistSnapshotAsync(instance, ct);

        await _hub.Clients.Group(state.GameId).SendAsync("GameCreated", new
        {
            gameId = state.GameId,
            ruleSet = "changsha-v1",
            seats = state.Seats.Select(SeatToWire).ToList()
        }, ct);

        return state.GameId;
    }

    // ── JoinTable ─────────────────────────────────────────────────────

    public async Task JoinTableAsync(string gameId, string connectionId, CancellationToken ct = default)
    {
        var instance = Require(gameId);
        await _hub.Groups.AddToGroupAsync(connectionId, gameId, ct);

        // Replay current state to the joining client so they can render.
        await SendFullStateAsync(instance, connectionId, seatIndex: null, ct);
    }

    // ── TakeSeat ──────────────────────────────────────────────────────

    public async Task<int> TakeSeatAsync(string gameId, string playerId, string connectionId, int? seatIndex, CancellationToken ct = default)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            int chosenSeat;
            if (seatIndex.HasValue)
            {
                chosenSeat = seatIndex.Value;
                if (chosenSeat is < 0 or > 3)
                    throw new HubException($"Seat {chosenSeat} is out of range.");

                if (instance.SeatConnections.TryGetValue(chosenSeat, out var existing) && existing != connectionId)
                    throw new HubException($"Seat {chosenSeat} is already taken.");
            }
            else
            {
                chosenSeat = Enumerable.Range(0, 4)
                    .FirstOrDefault(i => !instance.SeatConnections.ContainsKey(i) && !instance.State.Seats[i].IsBot, -1);
                if (chosenSeat < 0)
                    throw new HubException("No free seats available.");
            }

            var seat = instance.State.Seats[chosenSeat];
            seat.IsBot = false;
            // Phase J Wave 6 — persistent player identity (cookie-derived in
            // v1). Survives reconnects, drives career-stats keying.
            seat.PlayerId = playerId;
            // Transport connection id for per-seat private routing (e.g.
            // TilesDealt private payload). Cleared on disconnect; rebound on
            // reconnect.
            instance.SeatConnections[chosenSeat] = connectionId;
            instance.LastActivityUtc = DateTime.UtcNow;

            await _hub.Groups.AddToGroupAsync(connectionId, gameId, ct);
            await _hub.Clients.Group(gameId).SendAsync("PlayerSeated", new
            {
                gameId,
                seatIndex = chosenSeat,
                playerId,
                isBot = false
            }, ct);

            await PersistSnapshotAsync(instance, ct);
            return chosenSeat;
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    public async Task FillEmptySeatsWithBotsAsync(string gameId, CancellationToken ct = default)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            for (var i = 0; i < 4; i++)
            {
                if (instance.SeatConnections.ContainsKey(i)) continue;
                var seat = instance.State.Seats[i];
                if (seat.IsBot) continue;
                seat.IsBot = true;
                seat.PlayerId = $"bot-{i}";
                await _hub.Clients.Group(gameId).SendAsync("PlayerSeated", new
                {
                    gameId,
                    seatIndex = i,
                    playerId = seat.PlayerId,
                    isBot = true
                }, ct);
            }
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    /// <summary>
    /// Ripley L-10 audit fix — explicit seat release driven by the autotable
    /// client's "Leave" action (<c>["seats", N, { seat: null }]</c>). Unlike
    /// <see cref="HandleDisconnectAsync"/> (which keeps the persistent
    /// <see cref="ChangshaSeatState.PlayerId"/> in place so a reconnect can
    /// reclaim the seat), this clears the persistent identity too — the
    /// player has voluntarily vacated, the seat is free for anyone.
    ///
    /// <para>Phase guard: only releases while the game is still in
    /// <see cref="ChangshaPhase.Seating"/>. Mid-hand leaves are routed via
    /// disconnect/forfeit paths instead, so we don't blow up an active hand
    /// by clearing a seat that owes a discard.</para>
    ///
    /// <para>No-op when no seat matches the supplied
    /// <paramref name="connectionId"/> / <paramref name="playerId"/>.</para>
    /// </summary>
    public async Task ReleaseSeatAsync(string gameId, string playerId, string connectionId, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return;
        await instance.Lock.WaitAsync(ct);
        try
        {
            // Mid-hand leave is out of scope here — disconnect path handles it.
            if (instance.State.Phase != ChangshaPhase.Seating) return;

            // Find seats this connection (or player) owns. Connection match
            // wins so a stale call from a different tab doesn't kick the
            // active tab. Fall back to playerId so a connection that lost
            // its routing entry (e.g. reconnect mid-leave) still releases.
            var releasedSeats = new List<int>();
            foreach (var kvp in instance.SeatConnections)
            {
                if (!string.Equals(kvp.Value, connectionId, StringComparison.Ordinal)) continue;
                if (instance.SeatConnections.TryRemove(kvp.Key, out _))
                    releasedSeats.Add(kvp.Key);
            }

            if (releasedSeats.Count == 0 && !string.IsNullOrEmpty(playerId))
            {
                for (var i = 0; i < instance.State.Seats.Count; i++)
                {
                    var seat = instance.State.Seats[i];
                    if (!seat.IsBot
                        && string.Equals(seat.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        instance.SeatConnections.TryRemove(i, out _);
                        releasedSeats.Add(i);
                    }
                }
            }

            if (releasedSeats.Count == 0) return;

            foreach (var idx in releasedSeats)
            {
                var seat = instance.State.Seats[idx];
                seat.IsBot = false;
                seat.PlayerId = string.Empty;
                await _hub.Clients.Group(gameId).SendAsync("PlayerSeated", new
                {
                    gameId,
                    seatIndex = idx,
                    playerId = (string?)null,
                    isBot = false
                }, ct);
            }
            instance.LastActivityUtc = DateTime.UtcNow;
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    // ── StartGame ─────────────────────────────────────────────────────

    public async Task<bool> ApplyDealModeAsync(string gameId, DealMode mode, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return false;

        await instance.Lock.WaitAsync(ct);
        try
        {
            // Phase-guard: once StartGameAsync has fired (or a manual deal is in
            // mid-flight), DealMode is locked. Returning false here lets WS
            // callers safely re-invoke on reconnect without flipping the mode
            // mid-hand. Seating is the only legal moment to override.
            if (instance.State.Phase != ChangshaPhase.Seating) return false;
            instance.State.DealMode = mode;
            return true;
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetBotStrategyAsync(string gameId, string difficulty, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return false;

        // ChangshaBotEngine.Resolve returns Medium for null / whitespace /
        // unknown — the W25 audit explicitly preserves this UX rule so a
        // typo in the URL doesn't crash the table.
        var strategy = ChangshaBotEngine.Resolve(difficulty);

        // Volatile setter on the instance — no need to hold the per-game
        // lock; bot decisions read the same volatile field and naturally
        // pick up the new strategy on the next tick. Synchronous mutation
        // matches the unit-test expectation that the accessor below
        // reflects the new value immediately.
        instance.BotStrategy = strategy;

        _logger.LogInformation(
            "Bot strategy for game {GameId} bound to '{Difficulty}' (requested='{Requested}').",
            gameId, strategy.Difficulty, difficulty);

        await Task.CompletedTask;
        return true;
    }

    /// <inheritdoc />
    public string? GetActiveBotDifficulty(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return null;
        var perGame = instance.BotStrategy;
        return (perGame ?? _strategy).Difficulty;
    }

    public async Task StartGameAsync(string gameId, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureExpectedVersion(instance, expectedVersion);
            ChangshaGameStateMachine.StartGame(instance.State);
            await BroadcastGameStartedAsync(instance, ct);

            // Phase F §3 — branch on DealMode. Manual deal stops at RollingDice;
            // the dealer (or auto-ack-on-bot) drives RollDice → pickup loop.
            if (instance.State.DealMode == DealMode.Manual)
            {
                await PersistSnapshotAsync(instance, ct);
                return;
            }

            // Auto deal (default Phase D-backend): drive RollDice → Deal in one shot.
            var diceService = new DiceService(instance.State.Seed);
            ChangshaGameStateMachine.RollDice(instance.State, diceService);
            await BroadcastDiceAsync(instance, ct);

            ChangshaGameStateMachine.Deal(instance.State);
            await BroadcastDealAsync(instance, ct);

            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        if (instance.State.DealMode != DealMode.Manual)
        {
            // After auto deal, await client AckDeal (if humans) or auto-ack and start the turn.
            await TryAdvanceAfterDealAsync(instance, ct);
        }
    }

    // ── Phase F §3 — Manual deal: RollDice + TakeTilesFromWall ────────

    public async Task RollDiceAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureExpectedVersion(instance, expectedVersion);
            // Validate: only the dealer rolls.
            if (seatIndex != instance.State.DealerSeatIndex)
            {
                throw new InvalidOperationException(
                    $"Only dealer seat {instance.State.DealerSeatIndex} may roll dice in manual mode (got {seatIndex}).");
            }

            var diceService = new DiceService(instance.State.Seed + instance.State.HandNumber);
            var roll = diceService.Roll();
            ChangshaGameStateMachine.BeginManualDeal(instance.State, roll);
            await BroadcastDiceAsync(instance, ct);
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        // Phase G §1 — kick off the bot pickup chain. BeginManualDeal lands in
        // BreakPointMarked with PickupSeatIndex == DealerSeatIndex; if the dealer
        // (or any seat reached down the CCW chain) is a bot, this fires the tick.
        await ScheduleBotIfNeededAsync(instance, ct);
    }

    public async Task TakeTilesFromWallAsync(string gameId, int seatIndex, int count, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureExpectedVersion(instance, expectedVersion);
            ChangshaGameStateMachine.TakeTilesFromWall(instance.State, seatIndex, count);
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        // If the deal just completed (DealerExtra advanced into AwaitingDiscard),
        // engage the standard post-deal acknowledgement / turn-loop path so bots
        // and humans handle the first turn the same way as auto-deal.
        if (instance.State.Phase == ChangshaPhase.AwaitingDiscard)
        {
            await TryAdvanceAfterDealAsync(instance, ct);
        }
        else
        {
            // Phase G §1 — still in pickup phase. Keep the bot chain marching CCW;
            // if the next PickupSeatIndex is a human, ScheduleBotIfNeededAsync no-ops
            // and the runtime blocks waiting for that seat's `take` action.
            await ScheduleBotIfNeededAsync(instance, ct);
        }
    }

    public async Task AcknowledgeDealAsync(string gameId, int seatIndex, CancellationToken ct = default)
    {
        var instance = Require(gameId);
        bool ready;
        await instance.Lock.WaitAsync(ct);
        try
        {
            instance.DealAcks.Add(seatIndex);
            ready = HasAllHumanAcks(instance);
        }
        finally
        {
            instance.Lock.Release();
        }
        if (ready) await TryAdvanceAfterDealAsync(instance, ct);
    }

    private static bool HasAllHumanAcks(ChangshaGameInstance instance)
    {
        for (var i = 0; i < 4; i++)
        {
            if (instance.State.Seats[i].IsBot) continue;
            if (!instance.SeatConnections.ContainsKey(i)) continue;
            if (!instance.DealAcks.Contains(i)) return false;
        }
        return true;
    }

    private async Task TryAdvanceAfterDealAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await instance.Lock.WaitAsync(ct);
        bool started;
        try
        {
            started = !instance.DealAcks.Contains(-1) && instance.State.Phase == ChangshaPhase.AwaitingDiscard;
            if (started)
            {
                instance.DealAcks.Add(-1); // sentinel: turn-loop started
            }
        }
        finally
        {
            instance.Lock.Release();
        }
        if (!started) return;

        await EmitTurnStartedAsync(instance, ct);
        await ScheduleBotIfNeededAsync(instance, ct);
    }

    // ── Discard ───────────────────────────────────────────────────────

    public async Task DiscardAsync(string gameId, int seatIndex, int tileId, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        bool openedClaim;
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureSeatOwner(instance, seatIndex);
            EnsureExpectedVersion(instance, expectedVersion);
            ChangshaGameStateMachine.Discard(instance.State, seatIndex, tileId);
            await EmitDiscardAsync(instance, seatIndex, tileId, ct);
            openedClaim = instance.State.Phase == ChangshaPhase.AwaitingClaim;
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        if (openedClaim)
        {
            await OpenClaimWindowAsync(instance, ct);
        }
        else
        {
            await DriveAfterAdvanceAsync(instance, ct);
        }
    }

    public async Task ClaimAsync(string gameId, int seatIndex, string claimType, int[]? tileIds, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        var parsed = ParseClaimType(claimType);

        bool resolveNow;
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureSeatOwner(instance, seatIndex);
            EnsureExpectedVersion(instance, expectedVersion);
            if (instance.State.Phase != ChangshaPhase.AwaitingClaim || instance.State.ClaimWindow is null)
                throw new HubException("No claim window is open.");

            // Validate the seat actually has an opportunity for this type.
            var window = instance.State.ClaimWindow;
            var hasOpp = window.Opportunities.Any(o => o.SeatIndex == seatIndex && o.ClaimType == parsed);
            if (!hasOpp)
                throw new HubException($"Seat {seatIndex} cannot claim {claimType} on this discard.");

            instance.PendingClaims[seatIndex] = new ClaimResponse(parsed, tileIds);
            resolveNow = AllClaimsIn(instance) || CanResolveEarly(instance);
        }
        finally
        {
            instance.Lock.Release();
        }

        if (resolveNow) await ResolveClaimWindowAsync(instance, ct);
    }

    public async Task PassAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        bool resolveNow;
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureSeatOwner(instance, seatIndex);
            EnsureExpectedVersion(instance, expectedVersion);
            if (instance.State.Phase != ChangshaPhase.AwaitingClaim || instance.State.ClaimWindow is null)
                return; // late pass — ignore quietly
            instance.PendingClaims[seatIndex] = new ClaimResponse(null, null);
            resolveNow = AllClaimsIn(instance) || CanResolveEarly(instance);
        }
        finally
        {
            instance.Lock.Release();
        }

        if (resolveNow) await ResolveClaimWindowAsync(instance, ct);
    }

    private static bool AllClaimsIn(ChangshaGameInstance instance)
    {
        if (instance.State.ClaimWindow is null) return false;
        var eligible = instance.State.ClaimWindow.Opportunities.Select(o => o.SeatIndex).Distinct();
        return eligible.All(s => instance.PendingClaims.ContainsKey(s));
    }

    /// <summary>
    /// Bishop W26 — claim-expiry stall fix. Returns true when the claim window can
    /// resolve immediately because no possible response from any unresponsive seat
    /// could change the winner.
    ///
    /// <para>The runtime auto-passes unresponsive seats at <see cref="ChangshaRuntimeOptions.ClaimWindowTimeoutMs"/>
    /// (default 5s). When a bot responds quickly with a high-priority claim
    /// (Pung/Kong/Hu) the table sits idle for the full timeout waiting on humans whose
    /// strongest possible opportunity (e.g. Chow) cannot beat the bot's claim under
    /// the priority + CCW tiebreak rules. This is pure latency — the unresponsive
    /// seat's hypothetical claim would lose regardless of whether they responded
    /// in time.</para>
    ///
    /// <para>For each unresponded eligible seat S, compute the strongest opportunity
    /// S could declare. If that hypothetical claim would beat the current best
    /// responder claim under <see cref="ChangshaClaimPriority"/>, we MUST keep
    /// waiting. Otherwise, the window can resolve now. Hu opportunities for
    /// unresponded seats always force a wait (Hu beats every other tier).</para>
    ///
    /// <para>Returns false for kong-robbing windows: those only contain Hu
    /// opportunities, so an unresponsive seat always has Hu-tier potential — no
    /// early resolution is ever safe.</para>
    ///
    /// <para>Caller MUST hold <see cref="ChangshaGameInstance.Lock"/>.</para>
    /// </summary>
    internal static bool CanResolveEarly(ChangshaGameInstance instance)
    {
        var window = instance.State.ClaimWindow;
        if (window is null) return false;
        if (window.IsKongRobbing) return false;

        var eligibleSeats = window.Opportunities.Select(o => o.SeatIndex).Distinct().ToList();
        var unresponded = eligibleSeats.Where(s => !instance.PendingClaims.ContainsKey(s)).ToList();

        if (unresponded.Count == 0) return false; // AllClaimsIn already covers this

        // Current best claim across already-responded seats (ignoring passes).
        (int Seat, TableClaimType ClaimType)? currentBest = null;
        foreach (var kvp in instance.PendingClaims)
        {
            if (!eligibleSeats.Contains(kvp.Key)) continue;
            var claimType = kvp.Value?.ClaimType;
            if (claimType is null) continue;
            var contender = (kvp.Key, claimType.Value);
            if (currentBest is null || BeatsCurrentBest(contender, currentBest.Value, window.DiscardSeatIndex))
                currentBest = contender;
        }

        // If nobody has actually claimed yet, an unresponded seat's potential claim
        // would automatically be the winner — must keep waiting.
        if (currentBest is null) return false;

        foreach (var seat in unresponded)
        {
            var bestOpp = window.Opportunities
                .Where(o => o.SeatIndex == seat)
                .OrderByDescending(o => ChangshaClaimPriority.TierOf(o.ClaimType))
                .First();
            var hypothetical = (seat, bestOpp.ClaimType);
            if (BeatsCurrentBest(hypothetical, currentBest.Value, window.DiscardSeatIndex))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Bishop W26 — claim-priority comparator. Returns true when <paramref name="contender"/>
    /// would beat <paramref name="best"/> under the same ordering used by
    /// <see cref="ResolveClaimWindowAsync"/>: higher tier wins, then closer CCW
    /// distance to the discarder, then lower seat index (impossible tie — distinct
    /// seats always have distinct CCW distances).
    /// </summary>
    private static bool BeatsCurrentBest(
        (int Seat, TableClaimType ClaimType) contender,
        (int Seat, TableClaimType ClaimType) best,
        int discardSeat)
    {
        var contenderTier = ChangshaClaimPriority.TierOf(contender.ClaimType);
        var bestTier = ChangshaClaimPriority.TierOf(best.ClaimType);
        if (contenderTier != bestTier) return contenderTier > bestTier;

        var contenderDist = ChangshaClaimPriority.CounterClockwiseDistance(discardSeat, contender.Seat);
        var bestDist = ChangshaClaimPriority.CounterClockwiseDistance(discardSeat, best.Seat);
        return contenderDist < bestDist;
    }

    private bool ShouldResolveClaimWindow(ChangshaGameInstance instance)
    {
        instance.Lock.Wait();
        try { return AllClaimsIn(instance) || CanResolveEarly(instance); }
        finally { instance.Lock.Release(); }
    }

    // ── DeclareKong / DeclareWin ──────────────────────────────────────

    public async Task DeclareKongAsync(string gameId, int seatIndex, int[] tileIds, CancellationToken ct = default, int? expectedVersion = null)
    {
        if (tileIds is null || tileIds.Length == 0)
            throw new HubException("DeclareKong requires at least one tile id.");

        var instance = Require(gameId);
        bool openKongRobbingWindow = false;
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureSeatOwner(instance, seatIndex);
            EnsureExpectedVersion(instance, expectedVersion);
            var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);

            // Decide concealed vs added
            var logicalCounts = hand.ConcealedTiles
                .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
                .ToDictionary(g => g.Key, g => g.Count());
            var firstLogical = ChangshaDeckBuilder.GetLogicalTile(tileIds[0]);

            if (logicalCounts.TryGetValue(firstLogical, out var c) && c >= 4)
            {
                ChangshaGameStateMachine.DeclareConcealedKong(instance.State, seatIndex, firstLogical);
                await EmitConcealedKongAsync(instance, seatIndex, firstLogical, ct);
            }
            else
            {
                ChangshaGameStateMachine.DeclareAddedKong(instance.State, seatIndex, tileIds[0]);

                // Phase H Wave 2 §2.2 — when an added kong opens a robbing-the-added-kong
                // window (any other seat can Hu on the kong-target tile), the state-machine
                // leaves the phase in AwaitingClaim with state.ClaimWindow.IsKongRobbing=true.
                // We must NOT emit AddedKong yet — the kong isn't committed until the window
                // resolves with no Hu. Instead, broadcast a Hu-only claim window so clients
                // and bots can decide.
                if (instance.State.Phase == ChangshaPhase.AwaitingClaim)
                {
                    openKongRobbingWindow = true;
                }
                else
                {
                    await EmitAddedKongAsync(instance, seatIndex, tileIds[0], ct);
                }
            }
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        // Phase H Wave 2 §2.2 — broadcast the robbing-the-added-kong claim window
        // outside the instance lock (OpenClaimWindowAsync re-acquires the lock for
        // its own bookkeeping). Mirrors the post-Discard claim-window broadcast.
        if (openKongRobbingWindow)
        {
            await OpenClaimWindowAsync(instance, ct);
        }
    }

    public async Task DeclareWinAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null)
    {
        var instance = Require(gameId);
        bool scored = false;
        await instance.Lock.WaitAsync(ct);
        try
        {
            EnsureSeatOwner(instance, seatIndex);
            EnsureExpectedVersion(instance, expectedVersion);
            ChangshaGameStateMachine.DeclareSelfDrawWin(instance.State, seatIndex);
            await EmitWinDeclaredAsync(instance, ct);
            ChangshaGameStateMachine.Score(instance.State);
            scored = true;
            await EmitScoringAndHandFinishedAsync(instance, ct);
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }

        if (scored) await StartNextHandOrEndAsync(instance, ct);
    }

    // ── Reconnect / Disconnect ────────────────────────────────────────

    public async Task<bool> ReconnectAsync(string gameId, int seatIndex, string playerId, string connectionId, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return false;

        await instance.Lock.WaitAsync(ct);
        try
        {
            instance.SeatConnections[seatIndex] = connectionId;
            var seat = instance.State.Seats[seatIndex];
            seat.IsBot = false;
            // Phase J Wave 6 — persistent identity rebind. Wave-5 code stored
            // the connection id here; Wave-6 stores the cookie-derived player
            // id so career-stats persistence keys off the same identifier
            // across reconnects.
            seat.PlayerId = playerId;
        }
        finally { instance.Lock.Release(); }

        // Phase K Wave 1 — clear any pending forfeit timer for this
        // (game, player) pair so the BackgroundService doesn't auto-
        // forfeit a player who reconnected within the grace window.
        try
        {
            var forfeit = _scopeFactory.CreateScope().ServiceProvider
                .GetService<Tournament.TournamentForfeitService>();
            forfeit?.NoteReconnect(gameId, playerId);
        }
        catch { /* best-effort; never block reconnect on telemetry */ }

        await _hub.Groups.AddToGroupAsync(connectionId, gameId, ct);
        await SendFullStateAsync(instance, connectionId, seatIndex, ct);
        return true;
    }

    public async Task HandleDisconnectAsync(string playerId, string connectionId, CancellationToken ct = default)
    {
        // Phase J Wave 5 — collect games to destroy outside the per-instance
        // lock so we don't try to await a Task that re-enters the same lock.
        var toDestroy = new List<string>();
        // Phase K Wave 1 — also collect (gameId, playerId) tuples to feed
        // into the TournamentForfeitService once we drop the per-instance
        // lock. The forfeit BackgroundService filters by tournament
        // ownership at sweep time, so we don't need to repeat that here.
        var disconnectsToNote = new List<string>();
        foreach (var (gameId, instance) in _games)
        {
            await instance.Lock.WaitAsync(ct);
            try
            {
                // Phase J Wave 6 — release seats whose transport binding
                // matches the dropped connection id only. A player holding
                // the same seat from another tab (different connectionId,
                // same playerId) is unaffected — the SeatConnections value
                // differs so the entry is left in place.
                var matched = instance.SeatConnections
                    .Where(kvp => kvp.Value == connectionId)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var seat in matched)
                    instance.SeatConnections.TryRemove(seat, out _);

                // Phase K Wave 1 — capture for forfeit tracking. We only
                // care when the dropped connection actually held a seat
                // (matched.Count > 0) so a stray "I never sat down"
                // disconnect doesn't pollute the tracker.
                if (matched.Count > 0 && !string.IsNullOrEmpty(playerId))
                {
                    disconnectsToNote.Add(gameId);
                }

                // Phase J Wave 6 — host transfer / auto-destroy for public
                // games still in the Seating lobby phase. Compare against
                // the persistent <paramref name="playerId"/> (Wave-5 used
                // the connection id, which is now decoupled from identity).
                if (instance.State.IsPublic &&
                    instance.State.Phase == ChangshaPhase.Seating &&
                    !string.IsNullOrEmpty(instance.State.CreatorPlayerId) &&
                    !string.IsNullOrEmpty(playerId) &&
                    string.Equals(instance.State.CreatorPlayerId, playerId, StringComparison.Ordinal))
                {
                    // Pick the lowest-indexed seat that still has a live
                    // human connection — its persistent player id becomes
                    // the new host. Bots can't authorise SetGamePublic so
                    // they're never viable hosts. No candidate ⇒ the game
                    // is empty and is queued for destruction.
                    var newHost = instance.SeatConnections
                        .Where(kvp => !instance.State.Seats[kvp.Key].IsBot)
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => (string?)instance.State.Seats[kvp.Key].PlayerId)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p));

                    if (newHost is null)
                    {
                        toDestroy.Add(gameId);
                    }
                    else
                    {
                        instance.State.CreatorPlayerId = newHost;
                    }
                }
            }
            finally { instance.Lock.Release(); }
        }

        // Phase K Wave 1 — notify the forfeit BackgroundService outside
        // the per-instance lock. Resolved via scope so we don't pin the
        // singleton to a request scope; best-effort.
        if (disconnectsToNote.Count > 0)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var forfeit = scope.ServiceProvider.GetService<Tournament.TournamentForfeitService>();
                if (forfeit is not null)
                {
                    foreach (var gid in disconnectsToNote)
                    {
                        forfeit.NoteDisconnect(gid, playerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Forfeit disconnect notification failed for {PlayerId}.", playerId);
            }
        }

        foreach (var gameId in toDestroy)
        {
            await RemoveGameAsync(gameId, ct);
        }
    }

    // ── Drive loop after discard / pass-claim ────────────────────────

    private async Task DriveAfterAdvanceAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await instance.Lock.WaitAsync(ct);
        bool needTurn = false;
        bool exhausted = false;
        try
        {
            if (instance.State.Phase == ChangshaPhase.AwaitingDiscard)
            {
                // Active seat needs to draw before discarding.
                ChangshaGameStateMachine.DrawTile(instance.State);
                if (instance.State.Phase == ChangshaPhase.WallExhausted)
                {
                    exhausted = true;
                }
                else
                {
                    await EmitTileDrawnAsync(instance, instance.State.ActiveSeatIndex, ct);
                    needTurn = true;
                }
                await PersistSnapshotAsync(instance, ct);
            }
            else if (instance.State.Phase == ChangshaPhase.WallExhausted)
            {
                exhausted = true;
            }
        }
        finally { instance.Lock.Release(); }

        if (exhausted)
        {
            await HandleWallExhaustedAsync(instance, ct);
            return;
        }
        if (needTurn)
        {
            await EmitTurnStartedAsync(instance, ct);
            await ScheduleBotIfNeededAsync(instance, ct);
        }
    }

    // ── Claim-window orchestration ────────────────────────────────────

    private async Task OpenClaimWindowAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await instance.Lock.WaitAsync(ct);
        ChangshaClaimWindow window;
        try
        {
            window = instance.State.ClaimWindow!;
            instance.PendingClaims.Clear();
            instance.ClaimWindowCts?.Cancel();
            instance.ClaimWindowCts?.Dispose();
            instance.ClaimWindowCts = CancellationTokenSource.CreateLinkedTokenSource(instance.LifecycleCts.Token);
        }
        finally { instance.Lock.Release(); }

        await _hub.Clients.Group(instance.GameId).SendAsync("ClaimWindowOpen", new
        {
            gameId = instance.GameId,
            discardSeatIndex = window.DiscardSeatIndex,
            discardTileId = window.DiscardTileId,
            opportunities = window.Opportunities.Select(o => new
            {
                seatIndex = o.SeatIndex,
                claimType = ClaimToWire(o.ClaimType),
                priority = o.Priority,
            }).ToList(),
            timeoutMs = _options.ClaimWindowTimeoutMs
        }, ct);

        // Schedule timeout
        _ = ClaimTimeoutAsync(instance, instance.ClaimWindowCts.Token);

        // Schedule bot decisions
        foreach (var opp in window.Opportunities.GroupBy(o => o.SeatIndex))
        {
            var seatIdx = opp.Key;
            if (instance.State.Seats[seatIdx].IsBot)
                _ = BotClaimAsync(instance, seatIdx, instance.ClaimWindowCts.Token);
        }
    }

    private async Task ClaimTimeoutAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_options.ClaimWindowTimeoutMs, ct);
        }
        catch (OperationCanceledException) { return; }

        // Auto-pass anyone who hasn't responded
        await instance.Lock.WaitAsync(CancellationToken.None);
        try
        {
            if (instance.State.ClaimWindow is null) return;
            foreach (var seat in instance.State.ClaimWindow.Opportunities.Select(o => o.SeatIndex).Distinct())
            {
                if (!instance.PendingClaims.ContainsKey(seat))
                    instance.PendingClaims[seat] = new ClaimResponse(null, null);
            }
        }
        finally { instance.Lock.Release(); }
        await ResolveClaimWindowAsync(instance, CancellationToken.None);
    }

    private async Task BotClaimAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)
    {
        try { await Task.Delay(_options.BotClaimDelayMs, ct); }
        catch (OperationCanceledException) { return; }

        TableClaimType? decided;
        await instance.Lock.WaitAsync(CancellationToken.None);
        try
        {
            if (instance.State.ClaimWindow is null) return;
            if (instance.PendingClaims.ContainsKey(seatIndex)) return;
            // Phase H Wave 1 — race the strategy against BotDecisionTimeoutMs. A hung
            // strategy yields BotAction.Pass so the claim window can still resolve.
            // Phase J Wave 10 — DecideWithReasoning surfaces the strategy's
            // tiered explanation; the BotDecision is stashed on the
            // instance for replay-debugScore enrichment.
            // Bishop W25 — per-game strategy override (URL `?botDifficulty=`)
            // takes precedence; null falls back to the runtime default.
            var state = instance.State;
            var strategy = instance.BotStrategy ?? _strategy;
            var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
                () => strategy.DecideWithReasoning(state, seatIndex),
                _options.BotDecisionTimeoutMs,
                () => BotDecision.FromAction(BotAction.Pass()),
                _logger,
                ct).ConfigureAwait(false);
            instance.LastBotDecisions[seatIndex] = decision;
            var action = decision.Action;
            decided = action.Type == BotActionType.Claim ? action.ClaimType : null;
            instance.PendingClaims[seatIndex] = new ClaimResponse(decided, null);
        }
        finally { instance.Lock.Release(); }

        if (ShouldResolveClaimWindow(instance))
            await ResolveClaimWindowAsync(instance, CancellationToken.None);
    }

    private bool AllClaimsInChecked(ChangshaGameInstance instance)
    {
        instance.Lock.Wait();
        try { return AllClaimsIn(instance); }
        finally { instance.Lock.Release(); }
    }

    private async Task ResolveClaimWindowAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await instance.Lock.WaitAsync(ct);
        bool huScored = false;
        bool advance = false;
        bool didTakeClaim = false;
        bool kongRobbingPassed = false;
        int kongRobbingDeclarerSeat = -1;
        int kongRobbingTileId = -1;
        try
        {
            if (instance.State.ClaimWindow is null) return; // already resolved
            instance.ClaimWindowCts?.Cancel();

            // Pick winner across responded seats.
            var window = instance.State.ClaimWindow;
            // Phase H Wave 2 §2.2 — capture kong-robbing context BEFORE PassClaim/ResolveClaim
            // (both clear state.ClaimWindow). Used post-resolution to emit the added-kong
            // completion events when every Hu opportunity passed.
            var isKongRobbingWindow = window.IsKongRobbing;
            if (isKongRobbingWindow)
            {
                kongRobbingDeclarerSeat = window.KongDeclarerSeatIndex ?? window.DiscardSeatIndex;
                kongRobbingTileId = window.DiscardTileId;
            }
            var responded = instance.PendingClaims
                .Where(kvp => kvp.Value?.ClaimType is not null)
                .Select(kvp => new { Seat = kvp.Key, kvp.Value!.ClaimType, kvp.Value!.TileIds })
                .ToList();

            if (responded.Count == 0)
            {
                ChangshaGameStateMachine.PassClaim(instance.State);
                if (isKongRobbingWindow)
                {
                    // PassClaim dispatched to ResolveAddedKongPassed: the kong meld was
                    // upgraded to AddedKong and the replacement was drawn from the back
                    // of the wall (or Phase → WallExhausted). The declarer's turn
                    // resumes — no opponent advance, no DrawTile.
                    kongRobbingPassed = true;
                }
                else
                {
                    advance = true;
                }
            }
            else
            {
                // Single source of truth for priority — see ChangshaClaimPriority.
                // Hu > {Kong, Pung} > Chow, then CCW distance from discarder.
                var winner = responded
                    .OrderByDescending(r => ChangshaClaimPriority.TierOf(r.ClaimType!.Value))
                    .ThenBy(r => ChangshaClaimPriority.CounterClockwiseDistance(window.DiscardSeatIndex, r.Seat))
                    .ThenBy(r => r.Seat)
                    .First();

                // Once-per-game legacy-client warning: a chow claim with no explicit tileIds
                // means we'll fall back to lowest-rank pattern selection. Log so we know stale
                // clients are in the wild.
                if (winner.ClaimType == TableClaimType.Chow
                    && (winner.TileIds is null || winner.TileIds.Length == 0)
                    && !instance.LoggedLegacyChowWarning)
                {
                    _logger.LogWarning(
                        "Changsha game {GameId} seat {Seat} sent a Chow claim with no tileIds; "
                        + "falling back to lowest-rank pattern. (Logged once per game.)",
                        instance.GameId, winner.Seat);
                    instance.LoggedLegacyChowWarning = true;
                }

                ChangshaGameStateMachine.ResolveClaim(
                    instance.State, winner.Seat, winner.ClaimType!.Value, winner.TileIds);
                await EmitClaimMadeAsync(instance, winner.Seat, winner.ClaimType.Value,
                    window.DiscardTileId, ct);

                didTakeClaim = true;
                if (winner.ClaimType == TableClaimType.Hu)
                {
                    // Discard win OR robbing-the-added-kong win — same scoring path
                    // (state.CurrentWin.Method is RobbingKong vs Discard internally;
                    // EmitScoringAndHandFinishedAsync threads Method to clients).
                    ChangshaGameStateMachine.Score(instance.State);
                    await EmitScoringAndHandFinishedAsync(instance, ct);
                    huScored = true;
                }
                else if (winner.ClaimType == TableClaimType.Kong)
                {
                    // Replacement was drawn inside ResolveClaim
                    var hand = instance.State.Hands.Single(h => h.SeatIndex == winner.Seat);
                    if (hand.ConcealedTiles.Count > 0)
                    {
                        var replacementTile = hand.ConcealedTiles[^1];
                        await EmitKongReplacementAsync(instance, winner.Seat, replacementTile, ct);
                    }
                }
                // After non-Hu claim, claimer is now active and must discard. No DrawTile.
            }

            instance.PendingClaims.Clear();
            await PersistSnapshotAsync(instance, ct);
        }
        finally { instance.Lock.Release(); }

        if (huScored)
        {
            await StartNextHandOrEndAsync(instance, ct);
            return;
        }
        if (kongRobbingPassed)
        {
            // §2.2 — kong completed on the declarer's behalf after every opponent passed.
            // Emit the added-kong meld + replacement events and re-schedule the declarer's
            // turn (no DrawTile — the back-of-wall replacement is already in their hand).
            await EmitAddedKongAsync(instance, kongRobbingDeclarerSeat, kongRobbingTileId, ct);
            if (instance.State.Phase == ChangshaPhase.WallExhausted)
            {
                await HandleWallExhaustedAsync(instance, ct);
                return;
            }
            await ScheduleBotIfNeededAsync(instance, ct);
            return;
        }
        if (advance)
        {
            await DriveAfterAdvanceAsync(instance, ct);
            return;
        }
        if (didTakeClaim)
        {
            // claimer to discard — emit TurnStarted and schedule bot
            await EmitTurnStartedAsync(instance, ct);
            await ScheduleBotIfNeededAsync(instance, ct);
        }
    }

    // ── Bot scheduling for own turn ───────────────────────────────────

    private Task ScheduleBotIfNeededAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        // #116 (P1-3/P1-4) — manual deal per-hand ceremony. After RotateBanker parks a new
        // hand in RollingDice, a bot dealer must auto-roll to drive the wall-break + batch
        // pickup ritual (a human dealer rolls via the WS client). This only fires for the
        // per-hand re-entry path — StartGameAsync's manual branch never calls
        // ScheduleBotIfNeededAsync, so hand 1 stays client/test-driven exactly as before.
        if (instance.State.Phase == ChangshaPhase.RollingDice)
        {
            if (instance.State.DealMode != DealMode.Manual) return Task.CompletedTask;
            var dealerSeat = instance.State.DealerSeatIndex;
            if (dealerSeat < 0 || dealerSeat >= instance.State.Seats.Count) return Task.CompletedTask;
            if (!instance.State.Seats[dealerSeat].IsBot) return Task.CompletedTask;
            // #116 (P2) — idempotent dispatch: skip if a dealer roll for this seat is already pending.
            if (!instance.TryBeginBotSchedule(BotScheduleKind.DealerRoll, dealerSeat)) return Task.CompletedTask;

            _ = RunBotDealerRollAsync(instance, dealerSeat, instance.LifecycleCts.Token);
            return Task.CompletedTask;
        }

        // Phase G §1 — manual-deal pickup chain. While IsPickupPhase the active
        // actor is PickupSeatIndex (NOT ActiveSeatIndex — that's the dealer until
        // AwaitingDiscard). Schedule a pickup tick only if that seat is a bot;
        // a human pickup seat stalls the chain until the UI sends `take`.
        if (ChangshaGameStateMachine.IsPickupPhase(instance.State.Phase))
        {
            var pickupSeatNullable = instance.State.PickupSeatIndex;
            if (pickupSeatNullable is not int pickupSeat) return Task.CompletedTask;
            if (pickupSeat < 0 || pickupSeat >= instance.State.Seats.Count) return Task.CompletedTask;
            if (!instance.State.Seats[pickupSeat].IsBot) return Task.CompletedTask;
            // #116 (P2) — idempotent dispatch: skip if a pickup tick for this seat is already pending.
            if (!instance.TryBeginBotSchedule(BotScheduleKind.Pickup, pickupSeat)) return Task.CompletedTask;

            _ = RunBotPickupAsync(instance, pickupSeat, instance.LifecycleCts.Token);
            return Task.CompletedTask;
        }

        var seat = instance.State.ActiveSeatIndex;
        if (instance.State.Phase != ChangshaPhase.AwaitingDiscard) return Task.CompletedTask;
        if (!instance.State.Seats[seat].IsBot) return Task.CompletedTask;
        // #116 (P2) — idempotent dispatch: skip if a turn decision for this seat is already pending.
        if (!instance.TryBeginBotSchedule(BotScheduleKind.Turn, seat)) return Task.CompletedTask;

        _ = RunBotTurnAsync(instance, seat, instance.LifecycleCts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// #116 (P1-3) — manual-deal bot-dealer dice roll. After a hand rotates the banker and
    /// <see cref="StartNextHandOrEndAsync"/> parks the new hand in <see cref="ChangshaPhase.RollingDice"/>
    /// under <see cref="DealMode.Manual"/>, a bot dealer has no human to press "roll". This tick
    /// sleeps <see cref="ChangshaRuntimeOptions.BotPickupDelayMs"/>, re-validates the roll invariants
    /// under the instance lock (phase may have changed, the seat may have been claimed by a
    /// reconnecting human, the instance may be disposing), then calls <see cref="RollDiceAsync"/>,
    /// which begins the manual deal and hands off to <see cref="RunBotPickupAsync"/> via the pickup
    /// chain. The <see cref="BotScheduleKind.DealerRoll"/> guard slot is always released in the
    /// finally so a later hand can re-schedule the (possibly same) dealer seat.
    /// </summary>
    private async Task RunBotDealerRollAsync(ChangshaGameInstance instance, int dealerSeat, CancellationToken ct)
    {
        try
        {
            try { await Task.Delay(_options.BotPickupDelayMs, ct); }
            catch (OperationCanceledException) { return; }

            bool shouldRoll;
            await instance.Lock.WaitAsync(ct);
            try
            {
                shouldRoll = instance.State.Phase == ChangshaPhase.RollingDice
                    && instance.State.DealMode == DealMode.Manual
                    && instance.State.DealerSeatIndex == dealerSeat
                    && dealerSeat >= 0 && dealerSeat < instance.State.Seats.Count
                    && instance.State.Seats[dealerSeat].IsBot;
            }
            finally { instance.Lock.Release(); }

            if (!shouldRoll) return;

            await RollDiceAsync(instance.GameId, dealerSeat, ct);
        }
        catch (OperationCanceledException) { /* lifecycle teardown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot dealer dice-roll failed for game {GameId} seat {Seat}", instance.GameId, dealerSeat);
        }
        finally
        {
            instance.EndBotSchedule(BotScheduleKind.DealerRoll, dealerSeat);
        }
    }

    /// <summary>
    /// Phase G §1 — bot pickup tick. Mirrors <see cref="RunBotTurnAsync"/> for
    /// manual-deal pickup phases. Sleeps <see cref="ChangshaRuntimeOptions.BotPickupDelayMs"/>,
    /// re-validates the pickup invariants under the instance lock (phase may have
    /// changed, seat may have been claimed by a reconnecting human, instance may be
    /// disposing), then calls <see cref="TakeTilesFromWallAsync"/> — which itself
    /// re-invokes <see cref="ScheduleBotIfNeededAsync"/> so the chain continues.
    /// </summary>
    private async Task RunBotPickupAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)
    {
        try
        {
            try { await Task.Delay(_options.BotPickupDelayMs, ct); }
            catch (OperationCanceledException) { return; }

            int expected;
            await instance.Lock.WaitAsync(ct);
            try
            {
                if (!ChangshaGameStateMachine.IsPickupPhase(instance.State.Phase)) return;
                if (instance.State.PickupSeatIndex is not int currentPicker || currentPicker != seatIndex) return;
                if (!instance.State.Seats[seatIndex].IsBot) return;
                expected = ChangshaGameStateMachine.ExpectedPickupCount(instance.State.Phase);
            }
            finally { instance.Lock.Release(); }

            await TakeTilesFromWallAsync(instance.GameId, seatIndex, expected, ct);
        }
        catch (OperationCanceledException) { /* lifecycle teardown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot pickup failed for game {GameId} seat {Seat}", instance.GameId, seatIndex);
        }
        finally
        {
            // #116 (P2) — release the pickup slot. TakeTilesFromWallAsync advances the cursor
            // to a different seat (or the turn loop), so this never clobbers a re-scheduled slot.
            instance.EndBotSchedule(BotScheduleKind.Pickup, seatIndex);
        }
    }

    private async Task RunBotTurnAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)
    {
        // #116 (P2) — the turn slot is released once the decision is captured (before we act),
        // because the concealed/added-kong branches re-schedule THIS same seat's follow-up
        // discard. guardHeld tracks whether the finally still owes a release for early exits.
        bool guardHeld = true;
        try
        {
            try { await Task.Delay(_options.BotTurnDelayMs, ct); }
            catch (OperationCanceledException) { return; }

            BotAction action;
            await instance.Lock.WaitAsync(ct);
            try
            {
                if (instance.State.Phase != ChangshaPhase.AwaitingDiscard
                    || instance.State.ActiveSeatIndex != seatIndex)
                    return;
                // Phase H Wave 1 — race the strategy against BotDecisionTimeoutMs. A hung
                // strategy yields the deterministic Medium-tier discard so the turn loop
                // makes progress instead of blocking the table indefinitely.
                // Phase J Wave 10 — capture the decision (with reasoning)
                // and stash on the instance for replay-debugScore enrichment.
                // Bishop W25 — per-game strategy override (URL `?botDifficulty=`)
                // takes precedence; null falls back to the runtime default.
                var state = instance.State;
                var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
                var strategy = instance.BotStrategy ?? _strategy;
                var decision = await ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync(
                    () => strategy.DecideWithReasoning(state, seatIndex),
                    _options.BotDecisionTimeoutMs,
                    () => BotDecision.FromAction(BotAction.Discard(ChangshaBotPolicy.SelectDiscardTile(hand))),
                    _logger,
                    ct).ConfigureAwait(false);
                instance.LastBotDecisions[seatIndex] = decision;
                action = decision.Action;
            }
            finally { instance.Lock.Release(); }

            // Decision captured — hand the turn slot back so a kong follow-up (same seat) can
            // re-claim it. Correctness is still guarded by the phase/seat re-validation above.
            instance.EndBotSchedule(BotScheduleKind.Turn, seatIndex);
            guardHeld = false;

            switch (action.Type)
            {
                case BotActionType.DeclareWin:
                    await DeclareWinAsync(instance.GameId, seatIndex, ct);
                    break;
                case BotActionType.DeclareConcealedKong:
                    {
                        // We need a tileId of that logical to pass through DeclareKongAsync.
                        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
                        var tileIds = hand.ConcealedTiles
                            .Where(t => ChangshaDeckBuilder.GetLogicalTile(t) == action.LogicalTile!.Value)
                            .Take(4).ToArray();
                        await DeclareKongAsync(instance.GameId, seatIndex, tileIds, ct);
                        // After kong replacement draw, schedule another bot decision
                        await ScheduleBotIfNeededAsync(instance, ct);
                        break;
                    }
                case BotActionType.DeclareAddedKong:
                    await DeclareKongAsync(instance.GameId, seatIndex, new[] { action.TileId!.Value }, ct);
                    await ScheduleBotIfNeededAsync(instance, ct);
                    break;
                case BotActionType.Discard:
                    await DiscardAsync(instance.GameId, seatIndex, action.TileId!.Value, ct);
                    break;
                default:
                    // Fallback safety: discard the highest tile
                    {
                        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
                        if (hand.ConcealedTiles.Count > 0)
                            await DiscardAsync(instance.GameId, seatIndex, hand.ConcealedTiles[^1], ct);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot turn failed for game {GameId} seat {Seat}", instance.GameId, seatIndex);
        }
        finally
        {
            // Safety net for the delay-cancel / validation-fail early returns (idempotent).
            if (guardHeld) instance.EndBotSchedule(BotScheduleKind.Turn, seatIndex);
        }
    }

    // ── Hand finished / next hand ─────────────────────────────────────

    private async Task HandleWallExhaustedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await instance.Lock.WaitAsync(ct);
        try
        {
            ChangshaGameStateMachine.HandleWallExhausted(instance.State);
            await EmitHandFinishedDrawAsync(instance, ct);
            await PersistSnapshotAsync(instance, ct);
        }
        finally { instance.Lock.Release(); }
        await StartNextHandOrEndAsync(instance, ct);
    }

    private async Task StartNextHandOrEndAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        bool ended;
        // #116 (P1-3) — whether the next hand re-enters the manual deal ceremony
        // (RollingDice → dice roll → wall-break → batch pickups) instead of auto-dealing.
        bool manualReentry = false;
        await instance.Lock.WaitAsync(ct);
        try
        {
            ChangshaGameStateMachine.RotateBanker(instance.State);
            await EmitBankerRotatedAsync(instance, ct);
            // Phase J Wave 4 — <see cref="ChangshaPhase.EndGame"/> is a
            // deprecated alias of <see cref="ChangshaPhase.GameComplete"/>
            // (same underlying int value). A single equality check covers
            // both legacy 16-hand-rotation and new N-hand-cap terminations;
            // either branch in <see cref="ChangshaGameStateMachine.RotateBanker"/>
            // also flips <see cref="ChangshaGameState.IsGameComplete"/>.
            ended = instance.State.Phase == ChangshaPhase.GameComplete;
            if (ended)
            {
                await EmitGameEndedAsync(instance, ct);
                if (instance.State.IsGameComplete)
                {
                    await EmitGameCompletedAsync(instance, ct);
                }
            }
            else if (instance.State.DealMode == DealMode.Manual)
            {
                // #116 (P1-3) — per-hand manual deal ceremony. RotateBanker leaves the new
                // hand parked in RollingDice (identical to StartGameAsync's manual branch for
                // hand 1). Do NOT auto-deal: the dealer drives the 6-state ceremony
                // (BreakPointMarked → PickupRound1..3 → SingleTilePickup → DealerExtra) — a
                // human dealer rolls via the WS client, a bot dealer is auto-driven by
                // ScheduleBotIfNeededAsync below. Reset the deal-ack tracking so the
                // post-deal turn-start sentinel is fresh for the new hand.
                instance.DealAcks.Clear();
                manualReentry = true;
            }
            else
            {
                // Auto deal — roll dice + deal atomically (unchanged Phase D-backend path).
                var dice = new DiceService(instance.State.Seed + instance.State.HandNumber);
                ChangshaGameStateMachine.RollDice(instance.State, dice);
                await BroadcastDiceAsync(instance, ct);
                ChangshaGameStateMachine.Deal(instance.State);
                await BroadcastDealAsync(instance, ct);
                instance.DealAcks.Clear();
            }
            await PersistSnapshotAsync(instance, ct);
        }
        finally { instance.Lock.Release(); }

        if (ended) return;

        if (manualReentry)
        {
            // Kick the ceremony: schedules the bot dealer's dice roll when the (rotated)
            // dealer seat is a bot; a human dealer instead rolls via the WS client. The
            // pickup chain then flows through the standard ScheduleBotIfNeededAsync path.
            await ScheduleBotIfNeededAsync(instance, ct);
            return;
        }

        await TryAdvanceAfterDealAsync(instance, ct);
    }

    // ── Event emitters (wire-shape per docs/rules/changsha-signalr-contract.md) ──

    private static object SeatToWire(ChangshaSeatState s) => new
    {
        seatIndex = s.SeatIndex,
        wind = s.Wind.ToString().ToLowerInvariant(),
        playerId = s.PlayerId,
        isBot = s.IsBot,
        isDealer = s.IsDealer,
        tileCount = 0,
        melds = Array.Empty<object>(),
        discards = Array.Empty<int>()
    };

    private async Task BroadcastGameStartedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await _hub.Clients.Group(instance.GameId).SendAsync("GameStarted", new
        {
            gameId = instance.GameId,
            dealerSeatIndex = instance.State.DealerSeatIndex,
            roundWind = instance.State.RoundWind.ToString().ToLowerInvariant(),
            handNumber = instance.State.HandNumber
        }, ct);
    }

    private async Task BroadcastDiceAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var roll = instance.State.LastDiceRoll!.Value;
        var bp = instance.State.BreakPoint!.Value;
        await _hub.Clients.Group(instance.GameId).SendAsync("DiceRolled", new
        {
            gameId = instance.GameId,
            rollerSeatIndex = instance.State.DealerSeatIndex,
            dice = new { die1 = roll.Die1, die2 = roll.Die2, sum = roll.Sum }
        }, ct);
        await _hub.Clients.Group(instance.GameId).SendAsync("BreakPointSet", new
        {
            gameId = instance.GameId,
            breakPoint = new { wallIndex = bp.WallIndex, stackIndex = bp.StackIndex, tileIndex = bp.TileIndex }
        }, ct);
    }

    private async Task BroadcastDealAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        // Emit 4 batches × 4 seats. Batches 1–3: 4 tiles each per seat; batch 4: remainder (1 + dealer's extra).
        const int batches = 4;
        for (var b = 1; b <= batches; b++)
        {
            for (var i = 0; i < 4; i++)
            {
                var seatIdx = (instance.State.DealerSeatIndex + i) % 4;
                var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIdx);
                int from = b switch
                {
                    1 => 0, 2 => 4, 3 => 8, 4 => 12, _ => 0
                };
                int take = b == 4
                    ? hand.ConcealedTiles.Count - from
                    : Math.Min(4, Math.Max(0, hand.ConcealedTiles.Count - from));
                if (take <= 0) continue;
                var slice = hand.ConcealedTiles.Skip(from).Take(take).ToArray();
                var totalCount = from + take;
                var isComplete = b == 4 && i == 3;

                // Send full payload to seat owner (private), public payload to group (no tileIds).
                if (instance.SeatConnections.TryGetValue(seatIdx, out var connId))
                {
                    await _hub.Clients.Client(connId).SendAsync("TilesDealt", new
                    {
                        gameId = instance.GameId,
                        seatIndex = seatIdx,
                        tileIds = slice,
                        tileCount = totalCount,
                        batchNumber = b,
                        isComplete
                    }, ct);
                    await _hub.Clients.GroupExcept(instance.GameId, connId).SendAsync("TilesDealt", new
                    {
                        gameId = instance.GameId,
                        seatIndex = seatIdx,
                        tileIds = Array.Empty<int>(),
                        tileCount = totalCount,
                        batchNumber = b,
                        isComplete
                    }, ct);
                }
                else
                {
                    await _hub.Clients.Group(instance.GameId).SendAsync("TilesDealt", new
                    {
                        gameId = instance.GameId,
                        seatIndex = seatIdx,
                        tileIds = Array.Empty<int>(),
                        tileCount = totalCount,
                        batchNumber = b,
                        isComplete
                    }, ct);
                }

                if (_options.DealBatchDelayMs > 0) await Task.Delay(_options.DealBatchDelayMs, ct);
            }
        }
    }

    private async Task EmitTurnStartedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        await _hub.Clients.Group(instance.GameId).SendAsync("TurnStarted", new
        {
            gameId = instance.GameId,
            seatIndex = instance.State.ActiveSeatIndex,
            turnNumber = instance.State.TurnNumber,
            wallRemaining = instance.State.Wall.Count,
            phase = instance.State.Phase.ToString()
        }, ct);
    }

    private async Task EmitTileDrawnAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)
    {
        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
        var tileId = hand.ConcealedTiles[^1];
        if (instance.SeatConnections.TryGetValue(seatIndex, out var connId))
        {
            await _hub.Clients.Client(connId).SendAsync("TileDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)tileId,
                wallRemaining = instance.State.Wall.Count,
                isReplacementDraw = false
            }, ct);
            await _hub.Clients.GroupExcept(instance.GameId, connId).SendAsync("TileDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)null,
                wallRemaining = instance.State.Wall.Count,
                isReplacementDraw = false
            }, ct);
        }
        else
        {
            await _hub.Clients.Group(instance.GameId).SendAsync("TileDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)null,
                wallRemaining = instance.State.Wall.Count,
                isReplacementDraw = false
            }, ct);
        }
    }

    private async Task EmitKongReplacementAsync(ChangshaGameInstance instance, int seatIndex, int tileId, CancellationToken ct)
    {
        if (instance.SeatConnections.TryGetValue(seatIndex, out var connId))
        {
            await _hub.Clients.Client(connId).SendAsync("KongReplacementDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)tileId,
                wallRemaining = instance.State.Wall.Count
            }, ct);
            await _hub.Clients.GroupExcept(instance.GameId, connId).SendAsync("KongReplacementDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)null,
                wallRemaining = instance.State.Wall.Count
            }, ct);
        }
        else
        {
            await _hub.Clients.Group(instance.GameId).SendAsync("KongReplacementDrawn", new
            {
                gameId = instance.GameId,
                seatIndex,
                tileId = (int?)null,
                wallRemaining = instance.State.Wall.Count
            }, ct);
        }
    }

    private async Task EmitDiscardAsync(ChangshaGameInstance instance, int seatIndex, int tileId, CancellationToken ct)
    {
        await _hub.Clients.Group(instance.GameId).SendAsync("TileDiscarded", new
        {
            gameId = instance.GameId,
            seatIndex,
            tileId,
            turnNumber = instance.State.TurnNumber
        }, ct);
    }

    private async Task EmitClaimMadeAsync(ChangshaGameInstance instance, int seatIndex, TableClaimType type, int tileId, CancellationToken ct)
    {
        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
        var meld = hand.Melds.LastOrDefault();
        await _hub.Clients.Group(instance.GameId).SendAsync("ClaimMade", new
        {
            gameId = instance.GameId,
            claimingSeatIndex = seatIndex,
            claimType = ClaimToWire(type),
            tileId,
            meld = meld is null ? null : new
            {
                type = MeldKindToWire(meld.Kind),
                tileIds = meld.TileIds.ToArray(),
                claimedFrom = meld.ClaimedFromSeatIndex
            }
        }, ct);
    }

    private async Task EmitConcealedKongAsync(ChangshaGameInstance instance, int seatIndex, int logicalTile, CancellationToken ct)
    {
        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
        var meld = hand.Melds.LastOrDefault(m => m.Kind == MeldKind.ConcealedKong);
        await _hub.Clients.Group(instance.GameId).SendAsync("ClaimMade", new
        {
            gameId = instance.GameId,
            claimingSeatIndex = seatIndex,
            claimType = "kong",
            tileId = meld?.TileIds[0] ?? 0,
            meld = meld is null ? null : new
            {
                type = "concealedKong",
                tileIds = meld.TileIds.ToArray(),
                claimedFrom = (int?)null
            }
        }, ct);

        if (hand.ConcealedTiles.Count > 0)
            await EmitKongReplacementAsync(instance, seatIndex, hand.ConcealedTiles[^1], ct);
    }

    private async Task EmitAddedKongAsync(ChangshaGameInstance instance, int seatIndex, int tileId, CancellationToken ct)
    {
        var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
        var meld = hand.Melds.LastOrDefault(m => m.Kind == MeldKind.AddedKong);
        await _hub.Clients.Group(instance.GameId).SendAsync("ClaimMade", new
        {
            gameId = instance.GameId,
            claimingSeatIndex = seatIndex,
            claimType = "kong",
            tileId,
            meld = meld is null ? null : new
            {
                type = "addedKong",
                tileIds = meld.TileIds.ToArray(),
                claimedFrom = meld.ClaimedFromSeatIndex
            }
        }, ct);

        if (hand.ConcealedTiles.Count > 0)
            await EmitKongReplacementAsync(instance, seatIndex, hand.ConcealedTiles[^1], ct);
    }

    private async Task EmitWinDeclaredAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var win = instance.State.CurrentWin!;
        var hand = instance.State.Hands.Single(h => h.SeatIndex == win.WinningSeatIndex);
        await _hub.Clients.Group(instance.GameId).SendAsync("WinDeclared", new
        {
            gameId = instance.GameId,
            winResult = new
            {
                winningSeatIndex = win.WinningSeatIndex,
                winType = WinMethodToWire(win.Method),
                winPattern = WinPatternToWire(win.Pattern),
                winningTileId = win.WinningTileId,
                sourceSeatIndex = win.SourceSeatIndex,
                allPatterns = win.AllPatterns.Select(WinPatternToWire).ToArray(),
                isRobbedKong = win.IsRobbedKong,
                // Phase J Wave 3 — explicit axes for Hicks's UI (banner copy) so
                // the frontend doesn't infer self-draw / kong-replacement from
                // winType + allPatterns. Field names mirror WinResult auto-property
                // names (camelCased by the default SignalR JSON contract).
                isSelfDraw = win.IsSelfDraw,
                isKongReplacement = win.IsKongReplacement
            },
            hand = new
            {
                concealedTiles = hand.ConcealedTiles.ToArray(),
                melds = hand.Melds.Select(m => new
                {
                    type = MeldKindToWire(m.Kind),
                    tileIds = m.TileIds.ToArray(),
                    claimedFrom = m.ClaimedFromSeatIndex
                }).ToArray()
            }
        }, ct);
    }

    private async Task EmitScoringAndHandFinishedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var win = instance.State.CurrentWin;
        var score = instance.State.CurrentScore!;
        var hs = new
        {
            handNumber = instance.State.HandNumber,
            roundWind = instance.State.RoundWind.ToString().ToLowerInvariant(),
            dealerSeatIndex = instance.State.DealerSeatIndex,
            winResult = win is null ? null : new
            {
                winningSeatIndex = win.WinningSeatIndex,
                winType = WinMethodToWire(win.Method),
                winPattern = WinPatternToWire(win.Pattern),
                winningTileId = win.WinningTileId,
                sourceSeatIndex = win.SourceSeatIndex,
                allPatterns = win.AllPatterns.Select(WinPatternToWire).ToArray(),
                isRobbedKong = win.IsRobbedKong,
                // Phase J Wave 3 — explicit axes (same as the WinDeclared payload).
                isSelfDraw = win.IsSelfDraw,
                isKongReplacement = win.IsKongReplacement
            },
            scoreResult = new
            {
                category = score.Category.ToString().ToLowerInvariant() switch
                {
                    "smallwin" => "smallWin",
                    "bigwin" => "bigWin",
                    var s => s
                },
                basePoints = score.BasePoints,
                payments = score.Payments.Select(p => new
                {
                    fromSeatIndex = p.FromSeatIndex,
                    toSeatIndex = p.ToSeatIndex,
                    amount = p.Amount,
                    reason = p.Reason
                }).ToArray(),
                // Post-W23 — fan-catalog breakdown surfaced on the SignalR transport
                // (parity with the bundle WS ScoreResultEntry). Empty array = no fans.
                fans = score.Fans.Select(f =>
                {
                    var info = Mahjong.Autotable.Api.Changsha.Scoring.FanCatalog.Get(f.Fan);
                    return new
                    {
                        fan = ChangshaGameStateMachine.FanWireName(f.Fan),
                        points = f.Points,
                        chinese = info.Chinese,
                        pinyin = info.Pinyin,
                        english = info.English,
                    };
                }).ToArray(),
                fanPoints = score.FanPoints,
            },
            isDraw = false
        };
        var gs = BuildGameSummary(instance);

        await _hub.Clients.Group(instance.GameId).SendAsync("ScoringComplete", new
        {
            gameId = instance.GameId,
            handSummary = hs,
            gameSummary = gs
        }, ct);
    }

    private async Task EmitHandFinishedDrawAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var hs = new
        {
            handNumber = instance.State.HandNumber,
            roundWind = instance.State.RoundWind.ToString().ToLowerInvariant(),
            dealerSeatIndex = instance.State.DealerSeatIndex,
            winResult = (object?)null,
            scoreResult = (object?)null,
            isDraw = true
        };
        await _hub.Clients.Group(instance.GameId).SendAsync("ScoringComplete", new
        {
            gameId = instance.GameId,
            handSummary = hs,
            gameSummary = BuildGameSummary(instance)
        }, ct);
    }

    private async Task EmitBankerRotatedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        // Reason isn't tracked explicitly — derive from CurrentWin (already cleared by RotateBanker).
        // Use the most recent banker-rotated event from the log for detail parsing.
        var bankerEvt = instance.State.EventLog.LastOrDefault(e => e.EventType == "banker-rotated");
        string reason = "drawRotation";
        int previous = instance.State.DealerSeatIndex;
        if (bankerEvt is not null)
        {
            // detail format: "previous:N,reason:..."
            foreach (var part in bankerEvt.Detail.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length != 2) continue;
                if (kv[0] == "previous" && int.TryParse(kv[1], out var p)) previous = p;
                if (kv[0] == "reason") reason = kv[1] switch
                {
                    "winnerRotation" => "winnerBecomesDealer",
                    "dealerRetained" => "dealerRetained",
                    "drawRotation" => "drawRotation",
                    _ => kv[1]
                };
            }
        }
        await _hub.Clients.Group(instance.GameId).SendAsync("BankerRotated", new
        {
            gameId = instance.GameId,
            previousDealerSeatIndex = previous,
            newDealerSeatIndex = instance.State.DealerSeatIndex,
            reason
        }, ct);
    }

    private async Task EmitGameEndedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var gs = BuildGameSummary(instance);
        var winnerKvp = instance.State.CumulativeScores.OrderByDescending(kvp => kvp.Value).First();
        await _hub.Clients.Group(instance.GameId).SendAsync("GameEnded", new
        {
            gameId = instance.GameId,
            gameSummary = gs,
            finalScores = instance.State.CumulativeScores,
            winner = new { seatIndex = winnerKvp.Key, score = winnerKvp.Value }
        }, ct);
    }

    /// <summary>
    /// Phase J Wave 2 — emits the <c>GameCompleted</c> SignalR event whenever
    /// <see cref="ChangshaGameState.IsGameComplete"/> is true. Fired alongside
    /// the legacy <c>GameEnded</c> event so existing subscribers keep working;
    /// new clients (Hicks's end-of-game summary modal) subscribe to
    /// <c>GameCompleted</c> for the dedicated N-hand-cap payload. Payload
    /// shape: <c>{ gameId, hand: int, maxHands: int, finalScores: Dictionary,
    /// winner: { seatIndex, score }, phase: "GameComplete" }</c>. Phase J Wave 4
    /// note: <see cref="ChangshaPhase.EndGame"/> is now a deprecated alias of
    /// <see cref="ChangshaPhase.GameComplete"/>; the wire <c>phase</c> field
    /// always serialises as <c>"GameComplete"</c> because that value is
    /// declared first in <see cref="ChangshaPhase"/> and shared int values
    /// resolve to the first-declared name in <c>Enum.ToString()</c>.
    /// </summary>
    private async Task EmitGameCompletedAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        var state = instance.State;
        var winnerKvp = state.CumulativeScores.OrderByDescending(kvp => kvp.Value).First();
        await _hub.Clients.Group(instance.GameId).SendAsync("GameCompleted", new
        {
            gameId = instance.GameId,
            hand = state.HandNumber - 1,
            maxHands = state.MaxHands,
            finalScores = state.CumulativeScores,
            winner = new { seatIndex = winnerKvp.Key, score = winnerKvp.Value },
            phase = state.Phase.ToString()
        }, ct);

        // Phase J Wave 5 — career-stats hookup. Project the per-seat
        // CumulativeScores to per-PlayerId scores, identify the winners (all
        // seats tied at the top score — handles 2-way splits cleanly), then
        // delegate to PlayerProfileService.RecordGameCompletedAsync for a
        // single SaveChangesAsync transaction. Bots are filtered there
        // (PlayerId starts with "bot-"). The service swallows its own DB
        // exceptions so a stats failure can never break the game-completion
        // hot path; we still wrap defensively for the projection.
        if (_profileService is not null)
        {
            try
            {
                var topScore = state.CumulativeScores.Count == 0
                    ? 0
                    : state.CumulativeScores.Values.Max();

                var finalScores = new Dictionary<string, int>(StringComparer.Ordinal);
                var winners = new HashSet<string>(StringComparer.Ordinal);

                foreach (var seat in state.Seats)
                {
                    if (string.IsNullOrEmpty(seat.PlayerId)) continue;
                    if (!state.CumulativeScores.TryGetValue(seat.SeatIndex, out var score)) continue;
                    if (finalScores.TryGetValue(seat.PlayerId, out var existing))
                        finalScores[seat.PlayerId] = existing + score;
                    else
                        finalScores[seat.PlayerId] = score;

                    if (score == topScore) winners.Add(seat.PlayerId);
                }

                await _profileService.RecordGameCompletedAsync(finalScores, winners, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Recording completed-game stats for {GameId} failed.", instance.GameId);
            }
        }

        // Phase J Wave 7 — persist canonical play-by-play snapshot for the
        // completed game. Single best-effort write into ChangshaGameReplays;
        // failures are logged + swallowed so a replay-persist hiccup can
        // never break the game-completion hot path. The upsert keyed on
        // GameId keeps re-completion idempotent (e.g. a hydrated game that
        // re-emits GameCompleted simply refreshes its replay row).
        await PersistReplayAsync(instance, ct);

        // Phase K Wave 1 — per-player match-history denormalization
        // (Bishop). Writes one PlayerGameHistory row per human seat so
        // GET /api/games?playerId=… can list a player's completed games
        // without a JSON scan of ChangshaGame.StateJson. Best-effort:
        // failures never break the completion hot path.
        await PersistPlayerGameHistoryAsync(instance, ct);

        // Phase J Wave 10 — tournament-match advancement. If the
        // completed game is bound to any TournamentMatch row, flip
        // that match to "complete" with the top-score player as the
        // winner. Best-effort: a tournament-service hiccup never
        // breaks the game-completion hot path.
        await AdvanceTournamentMatchAsync(instance, ct);
    }

    /// <summary>
    /// Phase K Wave 1 — projects the completed game's seats into one
    /// <see cref="PlayerGameHistory"/> row per human player via
    /// <see cref="Players.PlayerGameHistoryService"/>. Best-effort:
    /// missing service registration (legacy test harnesses) or DB
    /// exceptions are logged + swallowed.
    /// </summary>
    private async Task PersistPlayerGameHistoryAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(instance.GameId, out var gameGuid)) return;

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetService<Players.PlayerGameHistoryService>();
            if (svc is null) return;

            // Best-effort RulePresetId resolution: the column exists on
            // the ChangshaGames row (Wave 8) but legacy callers may not
            // populate it. Null is the safe fallback.
            Guid? rulePresetId = null;
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var row = await db.ChangshaGames.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == gameGuid, ct);
                rulePresetId = row?.RulePresetId;
            }
            catch { /* fall through with null */ }

            await svc.RecordAsync(gameGuid, instance.CreatedUtc, instance.State, rulePresetId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlayerGameHistory write failed for {GameId}; surface will be stale.", instance.GameId);
        }
    }

    private async Task AdvanceTournamentMatchAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(instance.GameId, out var gameGuid)) return;
            var state = instance.State;
            if (state.CumulativeScores.Count == 0) return;
            var topScore = state.CumulativeScores.Values.Max();
            var winnerSeat = state.Seats.FirstOrDefault(s =>
                state.CumulativeScores.TryGetValue(s.SeatIndex, out var sc) && sc == topScore);
            if (winnerSeat is null || string.IsNullOrEmpty(winnerSeat.PlayerId)) return;

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetService<Tournament.TournamentService>();
            if (svc is null) return;
            var match = await svc.AdvanceMatchAsync(gameGuid, winnerSeat.PlayerId, ct);

            // Phase K Wave 1 — Elo rating update. Only fires when the
            // completed game actually mapped to a tournament match
            // (AdvanceMatchAsync returned a non-null match); ad-hoc /
            // public games skip the rating delta. Best-effort.
            if (match is not null)
            {
                try
                {
                    var ratings = scope.ServiceProvider.GetService<Tournament.PlayerRatingService>();
                    if (ratings is not null)
                    {
                        var participants = new List<string> { match.Player1Id, match.Player2Id };
                        if (!string.IsNullOrWhiteSpace(match.Player3Id)) participants.Add(match.Player3Id!);
                        if (!string.IsNullOrWhiteSpace(match.Player4Id)) participants.Add(match.Player4Id!);
                        await ratings.RecordMatchOutcomeAsync(participants, winnerSeat.PlayerId, ct: ct);
                    }
                }
                catch (Exception rex)
                {
                    _logger.LogWarning(rex, "Rating update for tournament match {MatchId} failed; swallowing.", match.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tournament-advance failed for {GameId}; swallowing.", instance.GameId);
        }
    }

    /// <summary>
    /// Phase J Wave 7 — projects <see cref="ChangshaGameState.EventLog"/> to
    /// the canonical Wave-7 replay wire shape
    /// <c>{ turn, phase, actor, action, tilesJson, timestampUtc }[]</c>
    /// and upserts it into <see cref="ChangshaGameReplay"/>. <c>phase</c> is
    /// the deal/discard/claim/hu bucket derived from the runtime event type;
    /// <c>actor</c> is the seat index (<c>-1</c> = system); <c>tilesJson</c>
    /// is a JSON-encoded <c>int[]</c> of the runtime tile ids touched by the
    /// event (single-element for tile-scoped events, empty array otherwise).
    /// Bucket mapping is deliberately exhaustive so a new EventType added
    /// upstream defaults to <c>"Other"</c> rather than dropping out of the
    /// replay. Best-effort: DB exceptions are logged + swallowed; the
    /// completion hot path never fails because the replay snapshot couldn't
    /// be persisted.
    /// </summary>
    private async Task PersistReplayAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        try
        {
            if (!Guid.TryParse(instance.GameId, out var gameGuid)) return;

            var state = instance.State;
            var events = new List<object>(state.EventLog.Count);
            // Phase J Wave 9 — durationMs is the gap between consecutive
            // OccurredUtc timestamps. For the very first event we fall
            // back to 0 (no predecessor to measure against).
            DateTime? prevTs = null;
            foreach (var evt in state.EventLog)
            {
                var tileIds = evt.TileId.HasValue ? new[] { evt.TileId.Value } : Array.Empty<int>();
                var duration = prevTs is null ? 0 : Math.Max(0, (int)(evt.OccurredUtc - prevTs.Value).TotalMilliseconds);
                // Phase J Wave 10 — for bot-source events, attach the most
                // recent BotDecision for the seat as `debugScore`. Per-event
                // accuracy is approximate (a single decision can spawn
                // multiple state-machine events); operators get the seat's
                // most-recent reasoning at event time, which is sufficient
                // for debugging strategy regressions. Non-bot events leave
                // the field null so the wire shape is uniform.
                object? debugScore = null;
                if (evt.SeatIndex >= 0
                    && evt.SeatIndex < state.Seats.Count
                    && state.Seats[evt.SeatIndex].IsBot
                    && instance.LastBotDecisions.TryGetValue(evt.SeatIndex, out var dec))
                {
                    debugScore = new
                    {
                        score = dec.Score,
                        tile = dec.Tile,
                        actionType = dec.Action.Type.ToString(),
                        reasoning = dec.Reasoning,
                    };
                }
                events.Add(new
                {
                    turn = evt.TurnNumber,
                    phase = ReplayPhaseBucket(evt.EventType),
                    actor = evt.SeatIndex,
                    action = evt.EventType,
                    tilesJson = JsonSerializer.Serialize(tileIds, SnapshotJson),
                    timestampUtc = evt.OccurredUtc,
                    // Phase J Wave 9 — per-event metadata for the v2 envelope.
                    source = ResolveReplayEventSource(instance, state, evt),
                    durationMs = duration,
                    // Phase J Wave 10 — per-bot-event reasoning + score
                    // (Hicks's admin audit drilldown).
                    debugScore,
                });
                prevTs = evt.OccurredUtc;
            }
            // Phase J Wave 9 — v2 envelope. v1 was a bare events array;
            // v2 wraps in { schemaVersion, events } so consumers can
            // branch on shape without inspecting the array.
            var envelope = new { schemaVersion = ChangshaGameReplay.CurrentSchemaVersion, events };
            var eventsJson = JsonSerializer.Serialize(envelope, SnapshotJson);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.ChangshaGameReplays.FirstOrDefaultAsync(r => r.GameId == gameGuid, ct);
            var now = DateTime.UtcNow;
            if (existing is null)
            {
                db.ChangshaGameReplays.Add(new ChangshaGameReplay
                {
                    Id = Guid.NewGuid(),
                    GameId = gameGuid,
                    CreatedAt = now,
                    EventsJson = eventsJson,
                    SchemaVersion = ChangshaGameReplay.CurrentSchemaVersion,
                });
            }
            else
            {
                existing.CreatedAt = now;
                existing.EventsJson = eventsJson;
                existing.SchemaVersion = ChangshaGameReplay.CurrentSchemaVersion;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persisting replay snapshot for {GameId} failed.", instance.GameId);
        }
    }

    /// <summary>
    /// Phase J Wave 9 — classify a replay event by source for the v2
    /// envelope. Returns <c>"system"</c> for engine-emitted events
    /// (no seat actor), <c>"human"</c> for human seats, and
    /// <c>"bot:&lt;difficulty&gt;"</c> for bots.
    ///
    /// <para>Phase J Wave 10 — bot difficulty now reflects the runtime's
    /// active <see cref="IChangshaBotStrategy.Difficulty"/> instead of the
    /// "unknown" placeholder. The runtime owns one strategy at a time
    /// (singleton lifetime), so this is necessarily uniform across all
    /// bot seats in the same game — sufficient for the audit drilldown.</para>
    ///
    /// <para>Bishop W25 — when a per-game strategy override is bound
    /// (URL <c>?botDifficulty=</c>), report THAT difficulty instead of
    /// the runtime default. Pre-W25 the replay always logged
    /// <c>bot:medium</c> regardless of URL difficulty, masking
    /// difficulty regressions and confusing the audit replay.</para>
    /// </summary>
    private string ResolveReplayEventSource(ChangshaGameInstance instance, ChangshaGameState state, ChangshaEvent evt)
    {
        if (evt.SeatIndex < 0 || evt.SeatIndex >= state.Seats.Count) return "system";
        var seat = state.Seats[evt.SeatIndex];
        var strategy = instance.BotStrategy ?? _strategy;
        return seat.IsBot ? $"bot:{strategy.Difficulty}" : "human";
    }

    /// <summary>
    /// Phase J Wave 7 — bucketises a runtime <c>EventType</c> string into
    /// the Wave-7 replay <c>phase</c> wire vocabulary. Buckets follow the
    /// Hicks-facing taxonomy in the inbox memo: <c>Setup</c> (game lifecycle),
    /// <c>Deal</c> (wall/draw/dealing), <c>Discard</c>, <c>Claim</c> (pung /
    /// kong / chow / pass / added-kong), <c>Hu</c> (win / draw-hand / scoring
    /// / false-hu). Unknown types fall back to <c>"Other"</c> rather than
    /// disappearing — keeps the replay surface forward-compatible with
    /// future state-machine event additions.
    ///
    /// <para>Public so Vasquez's contract suite can pin every documented
    /// runtime event type against the bucket taxonomy without having to
    /// thread <c>InternalsVisibleTo</c>.</para>
    /// </summary>
    public static string ReplayPhaseBucket(string eventType) => eventType switch
    {
        "game-created" or "game-started" or "banker-rotated" => "Setup",
        "dice-rolled" or "manual-deal-begun" or "tiles-dealt" or "tiles-picked-up"
            or "tile-drawn" or "kong-replacement-drawn" or "wall-exhausted" => "Deal",
        "tile-discarded" => "Discard",
        "claim-window-open" or "claim-resolved" or "claim-passed"
            or "concealed-kong" or "added-kong-declared" or "added-kong" => "Claim",
        "win-declared" or "scoring-complete" or "draw-hand" or "false-hu-penalty" => "Hu",
        _ => "Other",
    };

    private static object BuildGameSummary(ChangshaGameInstance instance) => new
    {
        gameId = instance.GameId,
        totalHands = instance.State.HandNumber,
        currentRound = instance.State.RoundNumber,
        roundWind = instance.State.RoundWind.ToString().ToLowerInvariant(),
        handInRound = instance.State.HandInRound,
        dealerSeatIndex = instance.State.DealerSeatIndex,
        scores = instance.State.CumulativeScores
    };

    private async Task SendFullStateAsync(ChangshaGameInstance instance, string connectionId, int? seatIndex, CancellationToken ct)
    {
        var state = instance.State;
        var seats = state.Seats.Select(s =>
        {
            var hand = state.Hands.FirstOrDefault(h => h.SeatIndex == s.SeatIndex);
            var concealed = (seatIndex == s.SeatIndex && hand is not null) ? hand.ConcealedTiles.ToArray() : null;
            var melds = hand?.Melds.Select(m => new
            {
                type = MeldKindToWire(m.Kind),
                tileIds = m.TileIds.ToArray(),
                claimedFrom = m.ClaimedFromSeatIndex
            }).ToArray() ?? Array.Empty<object>();
            var discards = state.DiscardPile
                .Where(d => d.SeatIndex == s.SeatIndex)
                .Select(d => d.TileId).ToArray();
            return new
            {
                seatIndex = s.SeatIndex,
                wind = s.Wind.ToString().ToLowerInvariant(),
                playerId = s.PlayerId,
                isBot = s.IsBot,
                isDealer = s.IsDealer,
                tileCount = hand?.ConcealedTiles.Count ?? 0,
                concealedTiles = concealed,
                melds,
                discards
            };
        }).ToArray();

        var payload = new
        {
            gameId = instance.GameId,
            phase = state.Phase.ToString(),
            roundWind = state.RoundWind.ToString().ToLowerInvariant(),
            roundNumber = state.RoundNumber,
            handNumber = state.HandNumber,
            handInRound = state.HandInRound,
            dealerSeatIndex = state.DealerSeatIndex,
            activeSeatIndex = state.ActiveSeatIndex,
            wallRemaining = state.Wall.Count,
            seats,
            discardPile = state.DiscardPile.Select(d => new { seatIndex = d.SeatIndex, tileId = d.TileId, turnNumber = d.TurnNumber }).ToArray(),
            claimWindow = state.ClaimWindow is null ? null : new
            {
                discardSeatIndex = state.ClaimWindow.DiscardSeatIndex,
                discardTileId = state.ClaimWindow.DiscardTileId,
                opportunities = state.ClaimWindow.Opportunities.Select(o => new
                {
                    seatIndex = o.SeatIndex,
                    claimType = ClaimToWire(o.ClaimType),
                    priority = o.Priority,
                }).ToArray()
            },
            scores = state.CumulativeScores
        };

        await _hub.Clients.Client(connectionId).SendAsync("FullState", payload, ct);
    }

    // ── Persistence (singleton-safe via scope factory) ────────────────

    private async Task PersistSnapshotAsync(ChangshaGameInstance instance, CancellationToken ct)
    {
        // Notify subscribers (e.g. AutotableWsEndpoint) on every state mutation.
        // Done before the DB write so a slow disk doesn't gate the broadcast.
        // Handler exceptions are swallowed to keep the runtime resilient.
        var handler = StateChanged;
        if (handler is not null)
        {
            // #137 — capture a deep-clone snapshot of the state AS IT IS RIGHT NOW,
            // under the instance lock (PersistSnapshotAsync is always invoked while the
            // caller holds it), and hand THAT to subscribers. The broadcast pipeline must
            // NOT re-read the live state later: a transient phase — e.g. EndHand, whose
            // result['current'] is tombstoned the instant the runtime rotates the banker
            // into the next hand's RollingDice — would otherwise already be gone by the
            // time a fire-and-forget broadcast task acquires the lock, silently dropping
            // the per-hand result the autotable client's hand-end observer latches on
            // (handEnds under-counts and the 4-hand gate fails even though the game
            // completed). Capturing here freezes every transition exactly as it occurred.
            ChangshaGameState? snapshot = null;
            try
            {
                var snapshotJson = JsonSerializer.Serialize(instance.State, SnapshotJson);
                snapshot = JsonSerializer.Deserialize<ChangshaGameState>(snapshotJson, SnapshotJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StateChanged snapshot capture failed for game {GameId}", instance.GameId);
            }
            if (snapshot is not null)
            {
                try { handler.Invoke(instance.GameId, snapshot); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "StateChanged handler threw for game {GameId}", instance.GameId);
                }
            }
        }

        if (!_options.PersistSnapshots) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var json = JsonSerializer.Serialize(instance.State, SnapshotJson);
            var gameGuid = Guid.Parse(instance.GameId);
            var entity = await db.ChangshaGames.FirstOrDefaultAsync(g => g.Id == gameGuid, ct);
            if (entity is null)
            {
                entity = new ChangshaGame
                {
                    Id = gameGuid,
                    Seed = instance.State.Seed,
                    StateJson = json,
                    StateVersion = instance.State.StateVersion,
                    CurrentHandNumber = instance.State.HandNumber,
                    CurrentRoundNumber = instance.State.RoundNumber,
                    CreatedUtc = instance.CreatedUtc,
                    UpdatedUtc = DateTime.UtcNow,
                    // Phase K Wave 3 — Bishop. Mirror the live state's
                    // creator id to the persistent column so settings
                    // endpoints can authorize without spinning the
                    // runtime up.
                    OwnerPlayerId = string.IsNullOrEmpty(instance.State.CreatorPlayerId)
                        ? null
                        : instance.State.CreatorPlayerId,
                };
                db.ChangshaGames.Add(entity);
            }
            else
            {
                entity.StateJson = json;
                entity.StateVersion = instance.State.StateVersion;
                entity.CurrentHandNumber = instance.State.HandNumber;
                entity.CurrentRoundNumber = instance.State.RoundNumber;
                entity.UpdatedUtc = DateTime.UtcNow;
                // Phase K Wave 3 — Bishop. Keep the persisted owner
                // column in sync with the live state's host (reassigned
                // when the original creator disconnects past grace).
                if (!string.IsNullOrEmpty(instance.State.CreatorPlayerId))
                {
                    entity.OwnerPlayerId = instance.State.CreatorPlayerId;
                }
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist snapshot for game {GameId}", instance.GameId);
        }
    }

    // ── Phase J Wave 5 — Public matchmaking lobby ─────────────────────

    /// <inheritdoc />
    public IReadOnlyList<LobbyGameSnapshot> SnapshotLobbyGames(int max = 50)
    {
        // Lock-free scan — accepts an inconsistent read by design (the matchmaking
        // lobby is a hint surface; SetGamePublic / StartGame are the real source of
        // truth). Same pattern as TryGetSnapshot.
        if (max < 1) max = 1;
        var list = new List<LobbyGameSnapshot>(Math.Min(max, _games.Count));
        foreach (var (gameId, instance) in _games)
        {
            var state = instance.State;
            if (!state.IsPublic) continue;
            if (state.Phase != ChangshaPhase.Seating) continue;

            // SeatedCount counts seats with a live connection. Bots don't count
            // as "seated humans" but they do occupy a seat — they're reflected
            // in the (MaxSeats - SeatedCount) gap implicitly.
            var seated = instance.SeatConnections.Count;
            list.Add(new LobbyGameSnapshot(
                GameId: gameId,
                PublicName: state.PublicName,
                CreatorPlayerId: state.CreatorPlayerId,
                SeatedCount: seated,
                MaxSeats: state.Seats.Count,
                Variant: "Changsha",
                CreatedAt: instance.CreatedUtc));
        }

        list.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return list.Count > max ? list.Take(max).ToList() : list;
    }

    /// <inheritdoc />
    public async Task SetGamePublicAsync(string gameId, string callerPlayerId, bool isPublic, string? publicName, CancellationToken ct = default)
    {
        var instance = Require(gameId);
        await instance.Lock.WaitAsync(ct);
        try
        {
            var state = instance.State;
            if (string.IsNullOrEmpty(state.CreatorPlayerId) ||
                !string.Equals(state.CreatorPlayerId, callerPlayerId, StringComparison.Ordinal))
            {
                throw new HubException("Only the game host may change the public-listing flag.");
            }
            if (state.Phase != ChangshaPhase.Seating)
            {
                throw new HubException("Public-listing flag may only change while the game is in the Seating phase.");
            }

            state.IsPublic = isPublic;
            if (isPublic)
            {
                if (publicName is not null)
                {
                    var trimmed = publicName.Trim();
                    if (trimmed.Length == 0) trimmed = null!;
                    if (trimmed is { Length: > 64 }) trimmed = trimmed[..64];
                    state.PublicName = trimmed;
                }
            }
            else
            {
                state.PublicName = null;
            }
            await PersistSnapshotAsync(instance, ct);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(string GameId, int SeatIndex)?> JoinRandomAsync(string playerId, string connectionId, string? variant, CancellationToken ct = default)
    {
        // Variant is a hint — only Changsha is supported in this codebase so
        // an explicit non-match returns "no candidate" rather than a hard error
        // (lets the frontend gracefully fall back to "create a game").
        if (!string.IsNullOrEmpty(variant) &&
            !string.Equals(variant, "Changsha", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidates = new List<(string GameId, ChangshaGameInstance Instance)>();
        foreach (var (gameId, instance) in _games)
        {
            var state = instance.State;
            if (!state.IsPublic) continue;
            if (state.Phase != ChangshaPhase.Seating) continue;
            // Must have at least one free non-bot seat.
            var hasFreeSeat = false;
            for (var i = 0; i < state.Seats.Count; i++)
            {
                if (state.Seats[i].IsBot) continue;
                if (instance.SeatConnections.ContainsKey(i)) continue;
                hasFreeSeat = true;
                break;
            }
            if (hasFreeSeat) candidates.Add((gameId, instance));
        }

        if (candidates.Count == 0) return null;

        var pick = candidates[Random.Shared.Next(candidates.Count)];
        try
        {
            // Phase J Wave 6 — forward both ids so the seat records its
            // persistent player id and the transport connection id is wired
            // for private-payload routing.
            var seat = await TakeSeatAsync(pick.GameId, playerId, connectionId, seatIndex: null, ct);
            return (pick.GameId, seat);
        }
        catch (HubException)
        {
            // Race: another caller took the last seat between candidate-pick
            // and TakeSeatAsync. Caller can retry with a fresh JoinRandom.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RemoveGameAsync(string gameId, CancellationToken ct = default)
    {
        if (!_games.TryRemove(gameId, out var instance)) return;
        try
        {
            await instance.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disposing removed game {GameId} threw.", gameId);
        }

        // Best-effort persistence cleanup — mark the row as terminal so a
        // restart's HydrateAsync skips it. We don't hard-delete the row
        // because the event log references it via FK and Apone's CI / audit
        // pipelines may want post-hoc replay. Marking phase=GameComplete +
        // IsGameComplete=true is the existing terminal signal used by the
        // hydration filter.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!Guid.TryParse(gameId, out var gameGuid)) return;
            var entity = await db.ChangshaGames.FirstOrDefaultAsync(g => g.Id == gameGuid, ct);
            if (entity is null) return;
            // Re-serialise the (now-disposed) snapshot so the terminal flag is
            // recorded. Keep the state-version monotonic.
            var terminalState = instance.State;
            terminalState.Phase = ChangshaPhase.GameComplete;
            terminalState.IsGameComplete = true;
            entity.StateJson = JsonSerializer.Serialize(terminalState, SnapshotJson);
            entity.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persisting removal-terminal snapshot for {GameId} failed.", gameId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private ChangshaGameInstance Require(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var instance))
            throw new HubException($"Unknown gameId {gameId}.");
        return instance;
    }

    private static void EnsureSeatOwner(ChangshaGameInstance instance, int seatIndex)
    {
        if (seatIndex is < 0 or > 3)
            throw new HubException($"Seat {seatIndex} is out of range.");
    }

    /// <summary>
    /// Phase H Wave 1 — optimistic concurrency guard. When <paramref name="expectedVersion"/>
    /// is non-null and does not match <see cref="ChangshaGameState.StateVersion"/>, throws
    /// <see cref="ChangshaConcurrencyException"/> BEFORE any mutation. Must be invoked
    /// inside the instance lock so the version cannot move between check and mutation.
    /// Server-internal callers (bot scheduler, claim-window timeout) pass null and bypass
    /// the check.
    /// </summary>
    private static void EnsureExpectedVersion(ChangshaGameInstance instance, int? expectedVersion)
    {
        if (expectedVersion is null) return;
        var actual = instance.State.StateVersion;
        if (expectedVersion.Value != actual)
            throw new ChangshaConcurrencyException(expectedVersion.Value, actual);
    }

    private static TableClaimType ParseClaimType(string s) => s.ToLowerInvariant() switch
    {
        "hu" => TableClaimType.Hu,
        "kong" => TableClaimType.Kong,
        "pung" => TableClaimType.Pung,
        "chow" => TableClaimType.Chow,
        _ => throw new HubException($"Unknown claim type {s}.")
    };

    private static string ClaimToWire(TableClaimType t) => t switch
    {
        TableClaimType.Hu => "hu",
        TableClaimType.Kong => "kong",
        TableClaimType.Pung => "pung",
        TableClaimType.Chow => "chow",
        _ => "pass"
    };

    private static string MeldKindToWire(MeldKind k) => k switch
    {
        MeldKind.Chow => "chow",
        MeldKind.Pung => "pung",
        MeldKind.ExposedKong => "exposedKong",
        MeldKind.ConcealedKong => "concealedKong",
        MeldKind.AddedKong => "addedKong",
        _ => "pung"
    };

    private static string WinMethodToWire(WinMethod m) => m switch
    {
        WinMethod.SelfDraw => "selfDraw",
        WinMethod.Discard => "discard",
        WinMethod.RobbingKong => "robbingKong",
        _ => "selfDraw"
    };

    private static string WinPatternToWire(WinPattern p) => p switch
    {
        WinPattern.Standard => "standard",
        WinPattern.SevenPairs => "sevenPairs",
        WinPattern.AllPungs => "allPungs",
        WinPattern.FullFlush => "fullFlush",
        WinPattern.NineTerminals => "nineTerminals",
        // Phase I Wave 1 — contextual Big Win patterns. Wire names mirror the enum-case
        // identifiers in camelCase so frontend result-modal + move-log mappers can drive
        // i18n / iconography off a stable string key (Hicks's UI lane consumes these
        // identically to the structural patterns above).
        WinPattern.HeavenlyHand => "heavenlyHand",
        WinPattern.EarthlyHand => "earthlyHand",
        WinPattern.LastTileFromWall => "lastTileFromWall",
        WinPattern.LastDiscardCatch => "lastDiscardCatch",
        WinPattern.KongReplacementWin => "kongReplacementWin",
        _ => "standard"
    };
}
