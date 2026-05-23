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
    Task<string> CreateGameAsync(int? seed, int[]? botSeatIndexes, string? hostConnectionId, CancellationToken ct = default);

    Task JoinTableAsync(string gameId, string connectionId, CancellationToken ct = default);
    Task<int> TakeSeatAsync(string gameId, string connectionId, int? seatIndex, CancellationToken ct = default);
    Task FillEmptySeatsWithBotsAsync(string gameId, CancellationToken ct = default);
    Task StartGameAsync(string gameId, CancellationToken ct = default, int? expectedVersion = null);
    Task AcknowledgeDealAsync(string gameId, int seatIndex, CancellationToken ct = default);
    Task DiscardAsync(string gameId, int seatIndex, int tileId, CancellationToken ct = default, int? expectedVersion = null);
    Task ClaimAsync(string gameId, int seatIndex, string claimType, int[]? tileIds, CancellationToken ct = default, int? expectedVersion = null);
    Task PassAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null);
    Task DeclareKongAsync(string gameId, int seatIndex, int[] tileIds, CancellationToken ct = default, int? expectedVersion = null);
    Task DeclareWinAsync(string gameId, int seatIndex, CancellationToken ct = default, int? expectedVersion = null);
    Task<bool> ReconnectAsync(string gameId, int seatIndex, string connectionId, CancellationToken ct = default);
    Task HandleDisconnectAsync(string connectionId, CancellationToken ct = default);

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
    event Action<string>? StateChanged;

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
    /// free non-bot seat and seats <paramref name="connectionId"/> into it.
    /// Returns the chosen <c>(gameId, seatIndex)</c> tuple, or <c>null</c> if
    /// no candidate exists.
    /// </summary>
    Task<(string GameId, int SeatIndex)?> JoinRandomAsync(string connectionId, string? variant, CancellationToken ct = default);

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

    public event Action<string>? StateChanged;

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

    public async Task<string> CreateGameAsync(int? seed, int[]? botSeatIndexes, string? hostConnectionId, CancellationToken ct = default)
    {
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        var (state, _) = ChangshaGameStateMachine.CreateGame(resolvedSeed, botSeatIndexes);
        // Phase H Wave 1 — StateVersion starts at 0 on a freshly created game; the
        // "game-created" event emitted inside CreateGame is treated as setup, not as
        // a mutation that consumes a version slot. First real mutation advances to 1.
        state.StateVersion = 0;
        // Phase J Wave 5 — record the creator's connection id as the host
        // identity. Used by MatchmakingService.SetGamePublic for the only-host-
        // may-toggle check and by HandleDisconnectAsync for host-transfer /
        // auto-destroy on public games. Null when the runtime is bootstrapping
        // a game from a non-SignalR transport (autotable WS) that doesn't
        // surface a host id at create time.
        state.CreatorPlayerId = string.IsNullOrEmpty(hostConnectionId) ? null : hostConnectionId;
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

    public async Task<int> TakeSeatAsync(string gameId, string connectionId, int? seatIndex, CancellationToken ct = default)
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
            seat.PlayerId = connectionId;
            instance.SeatConnections[chosenSeat] = connectionId;
            instance.LastActivityUtc = DateTime.UtcNow;

            await _hub.Groups.AddToGroupAsync(connectionId, gameId, ct);
            await _hub.Clients.Group(gameId).SendAsync("PlayerSeated", new
            {
                gameId,
                seatIndex = chosenSeat,
                playerId = connectionId,
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

    // ── StartGame ─────────────────────────────────────────────────────

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
                StateChanged?.Invoke(instance.GameId);
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
        StateChanged?.Invoke(instance.GameId);

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
        StateChanged?.Invoke(instance.GameId);

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
            resolveNow = AllClaimsIn(instance);
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
            resolveNow = AllClaimsIn(instance);
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

    public async Task<bool> ReconnectAsync(string gameId, int seatIndex, string connectionId, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var instance)) return false;

        await instance.Lock.WaitAsync(ct);
        try
        {
            instance.SeatConnections[seatIndex] = connectionId;
            var seat = instance.State.Seats[seatIndex];
            seat.IsBot = false;
            seat.PlayerId = connectionId;
        }
        finally { instance.Lock.Release(); }

        await _hub.Groups.AddToGroupAsync(connectionId, gameId, ct);
        await SendFullStateAsync(instance, connectionId, seatIndex, ct);
        return true;
    }

    public async Task HandleDisconnectAsync(string connectionId, CancellationToken ct = default)
    {
        // Phase J Wave 5 — collect games to destroy outside the per-instance
        // lock so we don't try to await a Task that re-enters the same lock.
        var toDestroy = new List<string>();
        foreach (var (gameId, instance) in _games)
        {
            await instance.Lock.WaitAsync(ct);
            try
            {
                var matched = instance.SeatConnections
                    .Where(kvp => kvp.Value == connectionId)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var seat in matched)
                    instance.SeatConnections.TryRemove(seat, out _);

                // Phase J Wave 5 — host transfer / auto-destroy for public games
                // still in the Seating lobby phase. Private games and games that
                // have already started keep the existing semantics (orphaned
                // SeatConnections only, state is preserved for reconnect).
                if (instance.State.IsPublic &&
                    instance.State.Phase == ChangshaPhase.Seating &&
                    !string.IsNullOrEmpty(instance.State.CreatorPlayerId) &&
                    string.Equals(instance.State.CreatorPlayerId, connectionId, StringComparison.Ordinal))
                {
                    // Pick the lowest-indexed seat that still has a live human
                    // connection — that connection becomes the new host. A bot
                    // seat is never a viable host (bots can't authorise
                    // SetGamePublic). If no candidate is found the game is
                    // empty and queued for destruction.
                    var newHost = instance.SeatConnections
                        .Where(kvp => !instance.State.Seats[kvp.Key].IsBot)
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => (string?)kvp.Value)
                        .FirstOrDefault();

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
            var state = instance.State;
            var action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
                () => _strategy.DecideAction(state, seatIndex),
                _options.BotDecisionTimeoutMs,
                BotAction.Pass,
                _logger,
                ct).ConfigureAwait(false);
            decided = action.Type == BotActionType.Claim ? action.ClaimType : null;
            instance.PendingClaims[seatIndex] = new ClaimResponse(decided, null);
        }
        finally { instance.Lock.Release(); }

        if (AllClaimsInChecked(instance))
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

            _ = RunBotPickupAsync(instance, pickupSeat, instance.LifecycleCts.Token);
            return Task.CompletedTask;
        }

        var seat = instance.State.ActiveSeatIndex;
        if (instance.State.Phase != ChangshaPhase.AwaitingDiscard) return Task.CompletedTask;
        if (!instance.State.Seats[seat].IsBot) return Task.CompletedTask;

        _ = RunBotTurnAsync(instance, seat, instance.LifecycleCts.Token);
        return Task.CompletedTask;
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
        try { await Task.Delay(_options.BotPickupDelayMs, ct); }
        catch (OperationCanceledException) { return; }

        int expected;
        try
        {
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
    }

    private async Task RunBotTurnAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)
    {
        try { await Task.Delay(_options.BotTurnDelayMs, ct); }
        catch (OperationCanceledException) { return; }

        try
        {
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
                var state = instance.State;
                var hand = instance.State.Hands.Single(h => h.SeatIndex == seatIndex);
                action = await ChangshaBotEngine.DecideActionWithTimeoutAsync(
                    () => _strategy.DecideAction(state, seatIndex),
                    _options.BotDecisionTimeoutMs,
                    () => BotAction.Discard(ChangshaBotPolicy.SelectDiscardTile(hand)),
                    _logger,
                    ct).ConfigureAwait(false);
            }
            finally { instance.Lock.Release(); }

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
            else
            {
                // Start next hand
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

        if (!ended) await TryAdvanceAfterDealAsync(instance, ct);
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
                }).ToArray()
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
    }

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
            try { handler.Invoke(instance.GameId); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StateChanged handler threw for game {GameId}", instance.GameId);
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
                    UpdatedUtc = DateTime.UtcNow
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
    public async Task<(string GameId, int SeatIndex)?> JoinRandomAsync(string connectionId, string? variant, CancellationToken ct = default)
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
            var seat = await TakeSeatAsync(pick.GameId, connectionId, seatIndex: null, ct);
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
