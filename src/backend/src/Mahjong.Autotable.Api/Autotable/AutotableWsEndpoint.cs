using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Players;

namespace Mahjong.Autotable.Api.Autotable;

/// <summary>
/// ASP.NET Core WebSocket endpoint that speaks the upstream pwmarcz/autotable
/// <c>NEW</c> / <c>JOIN</c> / <c>JOINED</c> / <c>UPDATE</c> protocol verbatim.
/// The byte-identical vendored bundle (under <c>src/frontend/autotable-src/</c>)
/// connects here unchanged.
///
/// <para><b>Path:</b> <c>/autotable/ws</c> — verified against upstream
/// <c>client-ui.ts:getUrl()</c>:
/// <c>path.substring(1, path.lastIndexOf('/')+1) + 'ws'</c> with
/// <c>window.location.pathname = '/autotable/'</c> resolves to
/// <c>autotable/ws</c> (Default #7, Stephen accepted).</para>
///
/// <para><b>Phase D-backend (this layer, current):</b> the Changsha rules engine
/// drives the autotable scene end-to-end. Each runtime <c>StateChanged</c>
/// translates to a delta of autotable collection entries (<c>match</c>,
/// <c>seats</c>, <c>nicks</c>, <c>dice</c>, <c>things</c>, <c>claim</c>,
/// <c>result</c>) which is stored in the per-game <see cref="AutotableGameState"/>
/// with <see cref="UpdateSource.Runtime"/> attribution, then broadcast to every
/// connection (per-viewer privacy filter applied). Client UPDATEs continue to relay
/// as in Phase C, but cannot overwrite runtime-owned entries
/// (see <see cref="AutotableGameState.ApplyUpdate(System.Collections.Generic.IEnumerable{CollectionEntry}, UpdateSource)"/>).</para>
///
/// <para><b>Multi-game routing (Phase I Wave 3):</b> connections route to the
/// gameId supplied via <c>?gameId=X</c> or in the JOIN message. Empty / absent
/// ids fall back to <see cref="AutotableWsEndpoint.DefaultGameId"/> so the
/// legacy bundle (which omits the query) keeps working. Ids are validated
/// (trim, length cap <see cref="AutotableWsEndpoint.MaxGameIdLength"/>,
/// reject control chars) at handshake and JOIN time; failures close the
/// socket with <see cref="System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation"/>.
/// Hicks's Phase D-frontend "Take Seat" click sends a <c>seats</c> UPDATE
/// that this endpoint routes to <see cref="IChangshaGameRuntime.TakeSeatAsync"/>,
/// optionally followed by <see cref="IChangshaGameRuntime.FillEmptySeatsWithBotsAsync"/>
/// for solo play (query param <c>?bots=true</c>, default ON).</para>
/// </summary>
public static class AutotableWsEndpoint
{
    public const string Path = "/autotable/ws";
    /// <summary>
    /// Fallback relay gameId used when a connection does not supply <c>?gameId=</c>
    /// in the WS handshake and JOIN messages omit the field. Phase I Wave 3 lifted
    /// the single-game-per-instance coercion: <see cref="AutotableConnectionManager"/>
    /// honors per-connection ids and falls back here only for legacy clients.
    /// </summary>
    public const string DefaultGameId = "changsha-default";

    /// <summary>
    /// Phase I Wave 3 — upper bound on accepted gameId length. Connections that
    /// supply a longer value (after trim) are closed with
    /// <see cref="System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation"/>.
    /// 64 chars covers GUIDs (32 hex), the legacy <see cref="DefaultGameId"/>
    /// (15 chars), and any human-readable lobby code we plausibly ship while
    /// keeping in-memory dictionaries and log lines tidy.
    /// </summary>
    public const int MaxGameIdLength = 64;

    /// <summary>Maps the autotable WS handler onto the application pipeline.</summary>
    public static IEndpointConventionBuilder MapAutotableWs(this IEndpointRouteBuilder endpoints) =>
        endpoints.Map(Path, async (HttpContext context, AutotableConnectionManager manager, PlayerIdentityService identity) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected a WebSocket request.");
                return;
            }

            // Phase J Wave 6 — resolve the persistent mahjong_pid cookie
            // BEFORE accepting the WS upgrade so we can append a Set-Cookie
            // if the client doesn't have one yet (response headers are
            // immutable once the WS handshake completes). Mint+write gives
            // first-time visitors a one-year sliding cookie without forcing
            // the frontend to call POST /api/identity first.
            var playerId = identity.ResolveFromCookie(context);
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = identity.Mint();
                try { identity.WriteCookie(context, playerId); }
                catch { /* response headers may already be flushed in test harnesses */ }
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await manager.HandleConnectionAsync(ws, context.Request.Query, playerId, context.RequestAborted);
        });
}

/// <summary>
/// Tracks live WS connections, holds per-game collaborative state, and routes
/// both bundle-originated UPDATE relays and Changsha runtime state-change
/// events to the right bundles. Registered as a singleton.
///
/// <para><b>Phase C-relay layering:</b></para>
/// <list type="bullet">
///   <item><b>Bundle → Server → Other bundles:</b> <c>UPDATE</c> is stored in
///   <see cref="AutotableGameState"/> for the gameId and broadcast to every
///   other connection in that gameId (sender is NOT echoed — already applied
///   locally).</item>
///   <item><b>Changsha runtime → Server → All bundles (legacy / Phase D):</b>
///   <see cref="IChangshaGameRuntime.StateChanged"/> still fires a full
///   translator snapshot to every connection in the affected gameId. Phase
///   D-backend will fold this into the per-game store so the two paths stay
///   consistent.</item>
/// </list>
/// </summary>
public sealed class AutotableConnectionManager : IDisposable
{
    private readonly IChangshaGameRuntime _runtime;
    private readonly ILogger<AutotableConnectionManager> _logger;
    private readonly ConcurrentDictionary<Guid, AutotableConnection> _connections = new();
    private readonly ConcurrentDictionary<string, AutotableGameState> _games = new(StringComparer.Ordinal);
    // Relay gameId → Changsha runtime gameId. Lazily populated on first seat take.
    private readonly ConcurrentDictionary<string, string> _runtimeBinding = new(StringComparer.Ordinal);
    // Reverse map for OnStateChanged → relayGameId lookup.
    private readonly ConcurrentDictionary<string, string> _relayBinding = new(StringComparer.Ordinal);
    // Lock to serialise lazy runtime-game creation per relay gameId.
    private readonly SemaphoreSlim _bindingLock = new(1, 1);
    private readonly Action<string> _stateChangedHandler;

    public AutotableConnectionManager(
        IChangshaGameRuntime runtime,
        ILogger<AutotableConnectionManager> logger)
    {
        _runtime = runtime;
        _logger = logger;
        _stateChangedHandler = OnStateChanged;
        _runtime.StateChanged += _stateChangedHandler;
    }

    public int ConnectionCount => _connections.Count;
    public int GameCount => _games.Count;

    /// <summary>
    /// Test/diagnostic hook: returns the number of stored entries across all
    /// non-ephemeral collections for the given game. Useful for waiting on
    /// the async UPDATE → store pipeline in integration tests.
    /// </summary>
    public int GetStoredEntryCount(string gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return 0;
        return _games.TryGetValue(gameId, out var state) ? state.Snapshot().Count : 0;
    }

    /// <summary>
    /// Test/diagnostic hook: returns the number of stored entries for a specific
    /// collection <paramref name="kind"/> in the given <paramref name="gameId"/>.
    /// Deterministic counterpart to <see cref="GetStoredEntryCount(string)"/> for
    /// tests that need to wait on a particular collection (e.g. <c>"things"</c>)
    /// rather than the aggregate count, which may already be satisfied by
    /// translator-emitted initial state.
    /// </summary>
    public int GetStoredEntryCount(string gameId, string kind)
    {
        if (string.IsNullOrEmpty(gameId)) return 0;
        return _games.TryGetValue(gameId, out var state) ? state.CountFor(kind) : 0;
    }

    /// <summary>Test hook: reports the runtime gameId bound to a relay gameId, if any.</summary>
    public string? GetRuntimeGameIdBoundTo(string relayGameId)
        => _runtimeBinding.TryGetValue(relayGameId ?? string.Empty, out var rid) ? rid : null;

    /// <summary>
    /// Test hook: injects a relay→runtime gameId binding without going through
    /// the WS seat-take flow. Used by Phase 5a/Phase C-relay tests that pre-create
    /// a runtime game via <see cref="IChangshaGameRuntime.CreateGameAsync"/> and
    /// need that game to drive snapshots delivered through the WS endpoint.
    /// </summary>
    public void BindRuntimeGameForTest(string relayGameId, string runtimeGameId)
    {
        _runtimeBinding[relayGameId] = runtimeGameId;
        _relayBinding[runtimeGameId] = relayGameId;
    }

    public async Task HandleConnectionAsync(WebSocket ws, IQueryCollection query, string playerId, CancellationToken serverShutdown)
    {
        // Phase I Wave 3 — honor ?gameId=X. Empty / absent ids fall back to the
        // legacy DefaultGameId so the upstream pwmarcz bundle (which omits the
        // query) keeps working. Bots default to ON for solo MVP play —
        // Stephen can disable via ?bots=false.
        var queryGameIdRaw = query.TryGetValue("gameId", out var g) ? g.ToString() : null;
        if (!TryNormalizeGameId(queryGameIdRaw, out var queryGameId, out var queryGameIdReject))
        {
            _logger.LogInformation(
                "Autotable WS rejecting connection due to invalid ?gameId= ({Reason})",
                queryGameIdReject);
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, queryGameIdReject!, serverShutdown);
            }
            catch { /* socket may already be torn down */ }
            return;
        }

        int? viewerSeat = null;
        var isSpectator = false;
        var seatExplicitlyProvided = false;
        if (query.TryGetValue("seat", out var s) && int.TryParse(s.ToString(), out var parsedSeat) && parsedSeat is >= -1 and <= 3)
        {
            seatExplicitlyProvided = true;
            // Phase I Wave 4 — seat=-1 is the "spectator" sentinel. The connection
            // joins the game (receives snapshots / updates), but ViewerSeat stays
            // null so the per-viewer privacy filter treats it as a spectator (all
            // foreign-seat tiles render face-down) and the connection is never
            // routed into a seat slot. Players still pass 0..3 as before.
            if (parsedSeat == -1)
            {
                isSpectator = true;
            }
            else
            {
                viewerSeat = parsedSeat;
            }
        }
        var autoBotFill = !query.TryGetValue("bots", out var b) || !string.Equals(b.ToString(), "false", StringComparison.OrdinalIgnoreCase);

        // Phase F §1.4 — variant / dealMode / botCount / botDifficulty.
        // Empty-string values fall back to the default (matches the "" query
        // case in VariantSwitchAcceptanceTests where no params yield Changsha).
        var variant = "changsha";
        if (query.TryGetValue("variant", out var v) && !string.IsNullOrEmpty(v.ToString()))
            variant = v.ToString();

        var dealMode = "manual";
        if (query.TryGetValue("dealMode", out var dm) && !string.IsNullOrEmpty(dm.ToString()))
            dealMode = dm.ToString();

        // Phase K (bot-autoplay) — `?botCount=4` without an explicit `?seat=`
        // means "I want four bots", which has only one viable interpretation:
        // a spectator watching an all-bots match. Auto-promote to spectator so
        // the auto-deal hook (TryAutoDealForSpectatorAsync) fires and the
        // table self-plays. Callers that explicitly send `seat=0..3` with
        // `botCount=4` still hit the existing cap-to-3 clamp below (the
        // Seat0_BotCount_StillCapsAt3 acceptance test pins that behaviour).
        if (!seatExplicitlyProvided
            && query.TryGetValue("botCount", out var bcSpec)
            && int.TryParse(bcSpec.ToString(), out var bcSpecParsed)
            && bcSpecParsed == 4)
        {
            isSpectator = true;
            viewerSeat = null;
        }

        // Phase I Wave 4 — spectators (seat=-1) can fill all four seats with bots
        // to watch a fully-bot table; player connections keep the existing 0..3 cap.
        var botCountCap = isSpectator ? 4 : 3;
        var botCount = 3;
        if (query.TryGetValue("botCount", out var bc) && int.TryParse(bc.ToString(), out var parsedBotCount) && parsedBotCount >= 0 && parsedBotCount <= botCountCap)
            botCount = parsedBotCount;

        var botDifficulty = "Medium";
        if (query.TryGetValue("botDifficulty", out var bd) && !string.IsNullOrEmpty(bd.ToString()))
            botDifficulty = bd.ToString();

        var connection = new AutotableConnection(ws, queryGameId, viewerSeat)
        {
            AutoBotFill = autoBotFill,
            Variant = variant,
            DealMode = dealMode,
            BotCount = botCount,
            BotDifficulty = botDifficulty,
            IsSpectator = isSpectator,
            // Phase J Wave 6 — persistent cookie-derived player id (resolved
            // by AutotableWsEndpoint.MapAutotableWs before the WS upgrade).
            // Replaces the previous random per-connection token so career
            // stats and host-promotion key off the same id across reconnects.
            PlayerId = playerId,
        };
        _connections[connection.Id] = connection;
        _logger.LogInformation(
            "Autotable WS connected (connectionId={ConnectionId}, gameId={GameId}, seat={Seat}, spectator={Spectator}, bots={Bots}, variant={Variant}, dealMode={DealMode}, botCount={BotCount}, botDifficulty={BotDifficulty}, runtimeMode={RuntimeMode})",
            connection.Id, queryGameId, viewerSeat, isSpectator, autoBotFill,
            variant, dealMode, botCount, botDifficulty, connection.RuntimeMode);

        try
        {
            await RunReadLoopAsync(connection, serverShutdown);
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Autotable WS closed abnormally (connectionId={ConnectionId})", connection.Id);
        }
        finally
        {
            await HandleDisconnectAsync(connection);
        }
    }

    private async Task RunReadLoopAsync(AutotableConnection connection, CancellationToken serverShutdown)
    {
        var buffer = new byte[8 * 1024];
        var assembler = new StringBuilder();

        while (connection.Socket.State == WebSocketState.Open && !serverShutdown.IsCancellationRequested)
        {
            assembler.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await connection.Socket.ReceiveAsync(buffer, serverShutdown);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connection.Socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "client closed",
                        CancellationToken.None);
                    return;
                }
                assembler.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            var payload = assembler.ToString();
            await HandleInboundAsync(connection, payload, serverShutdown);
        }
    }

    private async Task HandleInboundAsync(AutotableConnection connection, string payload, CancellationToken ct)
    {
        AutotableInboundMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<AutotableInboundMessage>(payload, AutotableJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse autotable WS payload: {Payload}", payload);
            return;
        }

        if (message is null || string.IsNullOrEmpty(message.Type)) return;

        switch (message.Type)
        {
            case "NEW":
                await HandleNewAsync(connection, ct);
                break;
            case "JOIN":
                await HandleJoinAsync(connection, message.GameId, ct);
                break;
            case "UPDATE":
                await HandleUpdateAsync(connection, message.Entries, ct);
                break;
            default:
                _logger.LogDebug("Unknown autotable message type {Type}", message.Type);
                break;
        }
    }

    private async Task HandleNewAsync(AutotableConnection connection, CancellationToken ct)
    {
        // Phase I Wave 3 — honor the connection's gameId (set from ?gameId=) for
        // multi-game routing; fall back to DefaultGameId for legacy clients that
        // don't supply one. NEW carries no gameId of its own.
        var gameId = !string.IsNullOrWhiteSpace(connection.GameId)
            ? connection.GameId!
            : AutotableWsEndpoint.DefaultGameId;
        var state = _games.GetOrAdd(gameId, id => new AutotableGameState(id));
        connection.GameId = gameId;

        var isFirst = ReferenceEquals(state, _games[gameId])
            && ConnectionsInGame(gameId, except: connection.Id) == 0
            && state.Snapshot().Count == 0;
        await SendJoinedAsync(connection, gameId, isFirst, ct);
        await SendFullSnapshotAsync(connection, gameId, ct);
        await TryAutoDealForSpectatorAsync(connection, ct);
    }

    private async Task HandleJoinAsync(AutotableConnection connection, string? gameId, CancellationToken ct)
    {
        // Phase I Wave 3 — JOIN carries its own gameId; validate + honor it.
        // Source priority: validated JOIN.gameId → connection's pre-validated
        // ?gameId= → DefaultGameId. Invalid JOIN ids close the socket with
        // PolicyViolation so the client is informed of the misuse rather than
        // silently rerouted.
        if (!TryNormalizeGameId(gameId, out var messageGameId, out var rejectReason))
        {
            _logger.LogInformation(
                "Autotable WS closing connection {ConnectionId} due to invalid JOIN.gameId ({Reason})",
                connection.Id, rejectReason);
            try
            {
                await connection.Socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    rejectReason!,
                    ct);
            }
            catch { /* socket may already be torn down */ }
            return;
        }

        var resolved = !string.IsNullOrWhiteSpace(messageGameId)
            ? messageGameId!
            : !string.IsNullOrWhiteSpace(connection.GameId)
                ? connection.GameId!
                : AutotableWsEndpoint.DefaultGameId;

        var existedBefore = _games.ContainsKey(resolved);
        var state = _games.GetOrAdd(resolved, id => new AutotableGameState(id));
        connection.GameId = resolved;

        var others = ConnectionsInGame(resolved, except: connection.Id);
        var isFirst = !existedBefore || (others == 0 && state.Snapshot().Count == 0);
        await SendJoinedAsync(connection, resolved, isFirst, ct);
        await SendFullSnapshotAsync(connection, resolved, ct);
        await TryAutoDealForSpectatorAsync(connection, ct);
    }

    /// <summary>
    /// Phase I Wave 4 — when a spectator connection (<c>?seat=-1</c>) asks for a
    /// fully-bot table (<c>?botCount=4</c>), kick off the deal automatically so the
    /// "fun to watch" all-bots mode doesn't require a human to press Deal. Idempotent:
    /// only fires when the runtime is bindable and the game is still in
    /// <see cref="ChangshaPhase.Seating"/>. Mirrors the seat-take + deal flow used
    /// by <see cref="TryHandleMatchActionAsync"/> for player-initiated games.
    /// </summary>
    private async Task TryAutoDealForSpectatorAsync(AutotableConnection connection, CancellationToken ct)
    {
        if (!connection.IsSpectator) return;
        if (connection.BotCount != 4) return;
        if (connection.RuntimeMode != AutotableRuntimeMode.ChangshaRuntime) return;
        if (string.IsNullOrEmpty(connection.GameId)) return;

        try
        {
            // Spectator auto-deal binds the runtime game with no host id —
            // there is no human player at the table to act as host.
            var runtimeGameId = await EnsureRuntimeBoundAsync(connection.GameId!, hostPlayerId: null, ct);
            if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return;
            if (snap.Phase != ChangshaPhase.Seating) return;

            // Fill every seat with a bot (the spectator never occupies one) then
            // start the game; the runtime drives the deal from there.
            await _runtime.FillEmptySeatsWithBotsAsync(runtimeGameId, ct);
            await _runtime.StartGameAsync(runtimeGameId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Auto-deal for spectator connection {ConnectionId} failed", connection.Id);
        }
    }

    private async Task HandleUpdateAsync(
        AutotableConnection connection,
        List<CollectionEntry>? entries,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(connection.GameId))
        {
            // Pre-JOIN UPDATE — no game to route to. Drop quietly; the bundle
            // sends JOIN immediately after connect so this is rare.
            _logger.LogDebug(
                "Discarded UPDATE from connection {ConnectionId} — no gameId yet (entries={Count})",
                connection.Id, entries?.Count ?? 0);
            return;
        }

        if (entries is null || entries.Count == 0) return;

        var state = _games.GetOrAdd(connection.GameId, id => new AutotableGameState(id));

        // Phase F §1.2 — Relay-mode connections forward every entry verbatim. The
        // backend never routes their UPDATEs into the Changsha runtime, and never
        // strips runtime-only kinds (the upstream variants don't emit them anyway).
        if (connection.RuntimeMode != AutotableRuntimeMode.ChangshaRuntime)
        {
            var relayApplied = state.ApplyUpdate(entries, UpdateSource.Client);
            if (relayApplied.Count == 0) return;
            await BroadcastToOthersAsync(connection, relayApplied, full: false, ct);
            return;
        }

        // Phase D-backend §4 — branch by collection name. Game-affecting kinds
        // route to the Changsha runtime (their authoritative effect comes back
        // through the StateChanged → translator → ApplyUpdate(Runtime) loop).
        // Cosmetic kinds (mouse, sound, dice, things) pass through as Client.
        var passthroughEntries = new List<CollectionEntry>(entries.Count);
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case "seats":
                    // Hicks's "Take Seat" click — route to runtime.TakeSeatAsync,
                    // optionally auto-fill remaining seats with bots for solo play.
                    await TryHandleSeatTakeAsync(connection, entry, ct);
                    // Mirror upstream's perPlayer semantics so the seat shows up
                    // immediately for other clients; runtime will reconfirm on its
                    // next StateChanged push.
                    passthroughEntries.Add(entry);
                    break;

                case ChangshaCollectionKinds.Claim:
                    // Hicks's 碰/吃/杠/胡 click. Route to ClaimAsync / PassAsync.
                    await TryHandleClaimActionAsync(connection, entry, ct);
                    // Don't relay — runtime will re-broadcast claim state.
                    break;

                case ChangshaCollectionKinds.Pickup:
                    // Phase F §3 — manual pickup. Routed to RollDiceAsync /
                    // TakeTilesFromWallAsync on the runtime; the runtime then emits
                    // a fresh translator snapshot containing the updated pickup
                    // entry. Don't relay — the runtime owns this kind.
                    await TryHandlePickupActionAsync(connection, entry, ct);
                    break;

                case ChangshaCollectionKinds.Discard:
                    // Hicks playability iter2 — human player click-to-discard.
                    // The runtime validates phase + active seat; invalid clicks
                    // are no-ops. The resulting tile move comes back through the
                    // standard things-collection broadcast so we don't relay
                    // this entry to other clients.
                    await TryHandleDiscardActionAsync(connection, entry, ct);
                    break;

                case "match":
                    // Match update from bundle. If the game hasn't started yet,
                    // treat any client-driven match push as a "Deal" command.
                    await TryHandleMatchActionAsync(connection, entry, ct);
                    passthroughEntries.Add(entry);
                    break;

                case ChangshaCollectionKinds.Result:
                    // Result is server-emitted only — ignore client pushes.
                    break;

                default:
                    // mouse, sound, dice, things, nicks, ephemeral, unique, perPlayer
                    // — pure cosmetic / meta. Pass through to the relay store.
                    passthroughEntries.Add(entry);
                    break;
            }
        }

        if (passthroughEntries.Count == 0) return;

        var applied = state.ApplyUpdate(passthroughEntries, UpdateSource.Client);
        if (applied.Count == 0) return;

        await BroadcastToOthersAsync(connection, applied, full: false, ct);
    }

    // ── Inbound action routing (seats / claim / match) ───────────────

    private async Task TryHandleSeatTakeAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        // value shape: { seat: int } (per upstream Player.svelte). null = leave.
        if (entry.Value is null) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;
        if (!je.TryGetProperty("seat", out var seatEl)) return;
        if (seatEl.ValueKind != JsonValueKind.Number) return;
        if (!seatEl.TryGetInt32(out var seatIndex)) return;
        if (seatIndex is < 0 or > 3) return;

        // Phase F §1.2 — Relay-mode connections do NOT bind a Changsha runtime.
        // The bundle's local Setup drives the deal; the backend only relays.
        if (connection.RuntimeMode != AutotableRuntimeMode.ChangshaRuntime) return;

        try
        {
            var runtimeGameId = await EnsureRuntimeBoundAsync(connection.GameId!, connection.PlayerId, ct);
            // Phase J Wave 6 — pass the persistent player id alongside the
            // per-connection transport id (the AutotableConnection.Id GUID
            // serves as the connection-level routing key inside the runtime).
            await _runtime.TakeSeatAsync(runtimeGameId, connection.PlayerId, connection.Id.ToString("N"), seatIndex, ct);

            if (connection.AutoBotFill)
            {
                await _runtime.FillEmptySeatsWithBotsAsync(runtimeGameId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Seat take failed for connection {ConnectionId} seat {Seat}", connection.Id, seatIndex);
        }
    }

    private async Task TryHandleClaimActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        var seatIndex = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            string s when int.TryParse(s, out var p) => p,
            _ => -1
        };
        if (seatIndex is < 0 or > 3) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;

        // Client format: { action: "Pung"|"Chow"|"Kong"|"Hu"|"Pass", tileIds?: int[] }
        if (!je.TryGetProperty("action", out var actionEl) || actionEl.ValueKind != JsonValueKind.String) return;
        var action = actionEl.GetString() ?? string.Empty;

        int[]? tileIds = null;
        if (je.TryGetProperty("tileIds", out var tileIdsEl) && tileIdsEl.ValueKind == JsonValueKind.Array)
        {
            tileIds = new int[tileIdsEl.GetArrayLength()];
            for (var i = 0; i < tileIds.Length; i++) tileIds[i] = tileIdsEl[i].GetInt32();
        }

        try
        {
            if (!_runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId)) return;
            if (string.Equals(action, "Pass", StringComparison.OrdinalIgnoreCase))
            {
                await _runtime.PassAsync(runtimeGameId, seatIndex, ct);
            }
            else
            {
                await _runtime.ClaimAsync(runtimeGameId, seatIndex, action, tileIds, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Claim {Action} failed for seat {Seat}", action, seatIndex);
        }
    }

    /// <summary>
    /// Phase F §3 — manual-pickup routing. Pickup entries from the bundle carry
    /// the player's intent for the current step of the deal:
    /// <list type="bullet">
    ///   <item><c>{ action: "rollDice" }</c> — dealer's dice click (transition
    ///   <see cref="ChangshaPhase.RollingDice"/> → <see cref="ChangshaPhase.BreakPointMarked"/>).</item>
    ///   <item><c>{ action: "take", count: N }</c> — current pickup seat takes
    ///   <c>N</c> tiles from the wall front. <c>count</c> may also be supplied
    ///   as <c>wallTileIds: int[]</c> (server picks the first N from the wall).</item>
    /// </list>
    /// The seat is taken from the entry key (the bundle keys pickup by seat) or
    /// inferred from <see cref="ChangshaGameState.PickupSeatIndex"/> when absent.
    /// </summary>
    private async Task TryHandlePickupActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;
        if (!_runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId)) return;

        // Seat: prefer explicit key (an int seat), fall back to "seatIndex" prop,
        // else use the runtime's current pickup cursor.
        var seatFromKey = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            string s when int.TryParse(s, out var p) => p,
            _ => -1
        };
        int seatIndex = seatFromKey;
        if (seatIndex is < 0 or > 3)
        {
            if (je.TryGetProperty("seatIndex", out var seatEl) && seatEl.ValueKind == JsonValueKind.Number && seatEl.TryGetInt32(out var s))
                seatIndex = s;
        }

        var action = string.Empty;
        if (je.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.String)
            action = actionEl.GetString() ?? string.Empty;

        try
        {
            if (string.Equals(action, "rollDice", StringComparison.OrdinalIgnoreCase))
            {
                if (seatIndex is < 0 or > 3)
                {
                    if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return;
                    seatIndex = snap.DealerSeatIndex;
                }
                await _runtime.RollDiceAsync(runtimeGameId, seatIndex, ct);
                return;
            }

            if (string.Equals(action, "take", StringComparison.OrdinalIgnoreCase))
            {
                if (seatIndex is < 0 or > 3)
                {
                    if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null || snap.PickupSeatIndex is null) return;
                    seatIndex = snap.PickupSeatIndex.Value;
                }
                int count = 0;
                if (je.TryGetProperty("count", out var countEl) && countEl.ValueKind == JsonValueKind.Number && countEl.TryGetInt32(out var c))
                    count = c;
                else if (je.TryGetProperty("wallTileIds", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                    count = idsEl.GetArrayLength();
                if (count <= 0) return;
                await _runtime.TakeTilesFromWallAsync(runtimeGameId, seatIndex, count, ct);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Pickup action {Action} failed for seat {Seat}", action, seatIndex);
        }
    }

    /// <summary>
    /// Hicks playability iter2 — human click-to-discard. The bundle emits a
    /// <see cref="ChangshaCollectionKinds.Discard"/> entry keyed by seat with
    /// value <c>{ tileId: int }</c>. We route to
    /// <see cref="IChangshaGameRuntime.DiscardAsync"/> which validates phase
    /// (must be <c>AwaitingDiscard</c>), active seat, and tile ownership.
    /// Invalid clicks are silently swallowed — the bundle's hand state is
    /// already authoritative on the server side so the next <c>things</c>
    /// push will re-snap any optimistic UI to the truth.
    /// </summary>
    private async Task TryHandleDiscardActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;
        if (!_runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId)) return;

        // Seat: prefer explicit key (an int seat), fall back to "seatIndex" prop.
        var seatIndex = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            string s when int.TryParse(s, out var p) => p,
            _ => -1
        };
        if (seatIndex is < 0 or > 3)
        {
            if (je.TryGetProperty("seatIndex", out var seatEl) && seatEl.ValueKind == JsonValueKind.Number && seatEl.TryGetInt32(out var s))
                seatIndex = s;
        }
        if (seatIndex is < 0 or > 3) return;

        if (!je.TryGetProperty("tileId", out var tileEl) || tileEl.ValueKind != JsonValueKind.Number) return;
        if (!tileEl.TryGetInt32(out var tileId)) return;

        try
        {
            await _runtime.DiscardAsync(runtimeGameId, seatIndex, tileId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Discard failed for seat {Seat} tile {Tile}", seatIndex, tileId);
        }
    }

    private async Task TryHandleMatchActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;

        // A match[0] push with `dealCommand: "start"` is Hicks's "Deal" button.
        // We also fall back to "any match push with dealer field while seating"
        // for compatibility with the upstream bundle's vanilla Deal control.
        var isDealCommand = false;
        if (je.TryGetProperty("dealCommand", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.String)
        {
            isDealCommand = string.Equals(cmdEl.GetString(), "start", StringComparison.OrdinalIgnoreCase);
        }

        if (!isDealCommand) return;

        try
        {
            if (!_runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId)) return;
            if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return;
            if (snap.Phase != ChangshaPhase.Seating) return;

            // Ensure all four seats are filled (auto-fill bots before starting).
            if (connection.AutoBotFill)
            {
                await _runtime.FillEmptySeatsWithBotsAsync(runtimeGameId, ct);
            }
            await _runtime.StartGameAsync(runtimeGameId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deal command failed for connection {ConnectionId}", connection.Id);
        }
    }

    /// <summary>
    /// Lazily binds <paramref name="relayGameId"/> to a Changsha runtime
    /// game. Idempotent: subsequent calls return the same runtime gameId.
    /// Phase J Wave 6 — accepts a <paramref name="hostPlayerId"/> so the
    /// runtime can record a persistent <c>CreatorPlayerId</c> on the new
    /// game; this is what unblocks autotable-WS games being toggled
    /// public via the matchmaking service (closes Vasquez's Wave-5 blind
    /// spot #4 where autotable games carried a null host id).
    /// </summary>
    private async Task<string> EnsureRuntimeBoundAsync(string relayGameId, string? hostPlayerId, CancellationToken ct)
    {
        if (_runtimeBinding.TryGetValue(relayGameId, out var existing)) return existing;

        await _bindingLock.WaitAsync(ct);
        try
        {
            if (_runtimeBinding.TryGetValue(relayGameId, out existing)) return existing;
            // botSeatIndexes = empty so the runtime starts with all-human seats;
            // we'll convert seats to bots on demand via FillEmptySeatsWithBotsAsync.
            var runtimeGameId = await _runtime.CreateGameAsync(
                seed: null,
                botSeatIndexes: Array.Empty<int>(),
                hostPlayerId: hostPlayerId,
                hostConnectionId: null,
                ct);
            _runtimeBinding[relayGameId] = runtimeGameId;
            _relayBinding[runtimeGameId] = relayGameId;
            return runtimeGameId;
        }
        finally
        {
            _bindingLock.Release();
        }
    }

    private async Task HandleDisconnectAsync(AutotableConnection connection)
    {
        _connections.TryRemove(connection.Id, out _);

        var gameId = connection.GameId;
        if (!string.IsNullOrEmpty(gameId))
        {
            // Phase J Wave 2 — release the runtime seat binding so an
            // autotable client that drops mid-game frees its seat (so peers
            // can claim it, or auto-fill can drop a bot in). Mirrors the
            // SignalR-side cleanup in <see cref="ChangshaHub.OnDisconnectedAsync"/>
            // which calls the same runtime hook. Only Changsha-mode, non-spectator
            // connections may have taken a runtime seat; relay-mode and spectator
            // connections short-circuit. Idempotent at the runtime layer — a
            // connectionId with no matching SeatConnections entry is a no-op. The
            // runtime clears <c>SeatConnections[seat]</c> but preserves
            // <c>seat.PlayerId</c> so the Phase J Wave 1 hot-seat swap (reconnect
            // by seat index) keeps working.
            await ReleaseRuntimeSeatAsync(connection, gameId!);

            // Upstream parity (server/game.ts:leave) — null out per-player
            // collection entries owned by this player and broadcast the
            // tombstones to remaining peers, so their seat/nick disappears.
            if (_games.TryGetValue(gameId, out var state))
            {
                var tombstones = state.RemovePlayerEntries(connection.PlayerId);
                if (tombstones.Count > 0)
                {
                    try
                    {
                        await BroadcastToOthersAsync(connection, tombstones, full: false, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to broadcast per-player tombstones for {ConnectionId}", connection.Id);
                    }
                }
            }

            // Ref-count cleanup: if no connections remain in this game, drop
            // its state. Mirrors upstream's expiry behaviour without the 2h
            // grace window (Phase C-relay is per-session sandbox semantics —
            // if everyone leaves, the game is gone).
            if (ConnectionsInGame(gameId, except: null) == 0)
            {
                _games.TryRemove(gameId, out _);
            }
        }

        _logger.LogInformation("Autotable WS disconnected (connectionId={ConnectionId})", connection.Id);
    }

    /// <summary>
    /// Phase J Wave 2 — releases the runtime seat binding held by
    /// <paramref name="connection"/> on its game's runtime instance. Idempotent
    /// (no-op if no binding exists) and safe for spectators (which never take a
    /// runtime seat). Used by <see cref="HandleDisconnectAsync(AutotableConnection)"/>
    /// to close the parity gap with <see cref="ChangshaHub.OnDisconnectedAsync"/>:
    /// before this wave only the SignalR path released seats on disconnect,
    /// leaving WS-disconnected players' seats orphaned in
    /// <c>ChangshaGameInstance.SeatConnections</c>.
    /// </summary>
    private async Task ReleaseRuntimeSeatAsync(AutotableConnection connection, string gameId)
    {
        if (connection.IsSpectator) return;
        if (connection.RuntimeMode != AutotableRuntimeMode.ChangshaRuntime) return;
        if (!_runtimeBinding.ContainsKey(gameId)) return;

        try
        {
            // Phase J Wave 6 — pass both the persistent playerId and the
            // per-connection transport id. The runtime matches
            // SeatConnections by transport id (so other tabs holding the
            // same playerId aren't dropped) and uses playerId for host-
            // promotion / stats keying. The AutotableConnection.Id GUID
            // serves as the transport key, matching the value used at
            // TakeSeat time.
            await _runtime.HandleDisconnectAsync(connection.PlayerId, connection.Id.ToString("N"));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Failed to release runtime seat for connection {ConnectionId} on game {GameId}",
                connection.Id, gameId);
        }
    }

    private async Task SendJoinedAsync(AutotableConnection connection, string gameId, bool isFirst, CancellationToken ct)
    {
        var msg = new JoinedMessage
        {
            GameId = gameId,
            PlayerId = connection.PlayerId,
            IsFirst = isFirst
        };
        await SendJsonAsync(connection, msg, ct);
    }

    private async Task SendFullSnapshotAsync(AutotableConnection connection, string? gameId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            // No game bound — ship only the translator's match[0] override
            // so the bundle creates tiles with fives='000'.
            var translatorEntriesNoGame = ChangshaToAutotableTranslator.Translate(state: null,
                viewerSeat: connection.ViewerSeat, viewerPlayerId: connection.PlayerId);
            var msg = new UpdateMessage { Entries = translatorEntriesNoGame.ToList(), Full = true };
            await SendJsonAsync(connection, msg, ct);
            return;
        }

        // Look up the bound runtime game (may be null if no seat has been
        // taken yet). The translator absorbs nulls and degrades to match-only.
        ChangshaGameState? runtimeState = null;
        if (_runtimeBinding.TryGetValue(gameId, out var runtimeGameId))
        {
            _runtime.TryGetSnapshot(runtimeGameId, out runtimeState);
        }

        var translatorEntries = ChangshaToAutotableTranslator.Translate(
            runtimeState,
            viewerSeat: connection.ViewerSeat,
            viewerPlayerId: connection.PlayerId);

        var gameState = _games.GetOrAdd(gameId, id => new AutotableGameState(id));

        // When a runtime game is backing the relay gameId, apply the translator
        // output with Runtime source so it OVERWRITES any client-pushed entries
        // for the same (collection, key). The viewer snapshot returned to this
        // connection is the merged result, then filtered for privacy.
        IReadOnlyList<CollectionEntry> snapshot;
        if (runtimeState is not null)
        {
            gameState.ApplyUpdate(translatorEntries, UpdateSource.Runtime);
            snapshot = gameState.Snapshot();
        }
        else
        {
            var stored = gameState.Snapshot();
            snapshot = MergeSnapshots(translatorEntries, stored);
        }

        var filtered = FilterEntriesForViewer(snapshot, connection.ViewerSeat);
        var update = new UpdateMessage { Entries = filtered.ToList(), Full = true };
        await SendJsonAsync(connection, update, ct);
    }

    /// <summary>
    /// Translator entries come first so that, on collision (same kind + key),
    /// the stored bundle value wins (it's the most recent). This is the
    /// no-Changsha-game path — when no runtime is backing the gameId the
    /// translator only contributes the <c>match[0]</c> fives override, and
    /// any bundle that has uploaded its own match wins.
    /// </summary>
    private static IReadOnlyList<CollectionEntry> MergeSnapshots(
        IReadOnlyList<CollectionEntry> translatorEntries,
        IReadOnlyList<CollectionEntry> storedEntries)
    {
        var index = new Dictionary<(string, string), int>();
        var merged = new List<CollectionEntry>(translatorEntries.Count + storedEntries.Count);
        foreach (var e in translatorEntries)
        {
            index[(e.Kind, e.Key.ToString() ?? string.Empty)] = merged.Count;
            merged.Add(e);
        }
        foreach (var e in storedEntries)
        {
            var key = (e.Kind, e.Key.ToString() ?? string.Empty);
            if (index.TryGetValue(key, out var existingPos))
            {
                merged[existingPos] = e;
            }
            else
            {
                index[key] = merged.Count;
                merged.Add(e);
            }
        }
        return merged;
    }

    /// <summary>
    /// Per-viewer privacy filter (Phase D-backend §3 / Ripley pivot decision #6 /
    /// Phase G slot-parse cleanup).
    ///
    /// <para><b>Rule:</b> for every <c>things</c> entry whose <c>slotName</c> ends in
    /// <c>@{seat}</c>, the entry "belongs" to that seat. If the owning seat is not the
    /// viewer (or the viewer is a spectator), the <c>face</c> field is stripped and
    /// — for hand slots only — the <c>rotationIndex</c> is forced face-down (2,
    /// per upstream <c>setup-slots.ts</c> hand rotations). Wall / discard / meld slots
    /// keep their translator-supplied rotation because those slots are publicly visible
    /// (discards face-up, melds face-up except concealed kong, walls face-down).
    /// The face strip is the only privacy mutation those entries see.</para>
    ///
    /// <para><b>Slot-suffix convention</b> (per <see cref="AutotableSlotMap.HandSlot"/>):
    /// hand slots are formatted <c>hand.{handIdx}@{seat}</c>. The owning seat is the
    /// integer AFTER the last <c>@</c> — <em>not</em> the digit between <c>.</c> and
    /// <c>@</c>, which is the per-seat hand index. Wall / discard / meld slots follow
    /// the same <c>{kind}…@{seat}</c> suffix convention. Slots without <c>@</c> carry
    /// no per-seat privacy semantics and pass through untouched. Slots with an
    /// unparseable suffix (<c>trailing@</c>, <c>garbled@abc</c>) also pass through
    /// — privacy fails open on malformed input so a parse glitch never silently
    /// hides the table.</para>
    ///
    /// <para>Note: in v1 the bundle's thing-index encodes typeIndex (face) intrinsically
    /// because we lock conditions.fives='000' for a clean 1:1 mapping. The filter
    /// strips the explicit face field so any future bundle that respects it renders
    /// only the back even when looking from the viewer's angle. v2 will shuffle
    /// physical tile-ids so the index itself reveals nothing.</para>
    /// </summary>
    private static IReadOnlyList<CollectionEntry> FilterEntriesForViewer(
        IReadOnlyList<CollectionEntry> entries,
        int? viewerSeat)
    {
        if (entries.Count == 0) return entries;

        var filtered = new List<CollectionEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Kind != "things" || entry.Value is null)
            {
                filtered.Add(entry);
                continue;
            }

            if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object)
            {
                filtered.Add(entry);
                continue;
            }

            if (!je.TryGetProperty("slotName", out var slotNameEl) || slotNameEl.ValueKind != JsonValueKind.String)
            {
                filtered.Add(entry);
                continue;
            }

            var slotName = slotNameEl.GetString() ?? string.Empty;

            // Parse owning seat from after the LAST '@'. No '@' or unparseable
            // suffix → not seat-scoped → pass through.
            var at = slotName.LastIndexOf('@');
            if (at < 0 || at == slotName.Length - 1)
            {
                filtered.Add(entry);
                continue;
            }
            if (!int.TryParse(slotName.AsSpan(at + 1), out var slotSeat))
            {
                filtered.Add(entry);
                continue;
            }

            if (viewerSeat.HasValue && slotSeat == viewerSeat.Value)
            {
                // Viewer's own slot — keep face-up.
                filtered.Add(entry);
                continue;
            }

            // Foreign seat (or spectator) — strip the face. For hand slots only,
            // also force the rotation to face-down so the bundle renderer flips the
            // tile back. Discard / meld / wall slots keep their public rotation.
            var forceHandFaceDown = slotName.StartsWith("hand.", StringComparison.Ordinal);
            filtered.Add(new CollectionEntry(entry.Kind, entry.Key,
                StripFace(je, forceHandFaceDown)));
        }
        return filtered;
    }

    /// <summary>
    /// Strips the <c>face</c> field and, when <paramref name="forceHandFaceDown"/>
    /// is true, overrides <c>rotationIndex</c> to 2 (upstream HandRotFaceDown).
    /// For non-hand slots the original rotation is preserved so discards stay
    /// face-up and concealed-kong melds keep their authored face-down rotation.
    /// </summary>
    private static JsonElement StripFace(JsonElement original, bool forceHandFaceDown)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.NameEquals("face")) continue;
                if (forceHandFaceDown && prop.NameEquals("rotationIndex"))
                {
                    // HandRotFaceDown = 2 (per upstream setup-slots.ts hand rotations).
                    w.WriteNumber("rotationIndex", 2);
                    continue;
                }
                prop.WriteTo(w);
            }
            // Explicitly write face=null so any future bundle that respects the
            // field renders only the back even when looking from the viewer's angle.
            w.WriteNull("face");
            w.WriteEndObject();
        }
        ms.Position = 0;
        return JsonDocument.Parse(ms).RootElement.Clone();
    }

    private async Task BroadcastToOthersAsync(
        AutotableConnection sender,
        IReadOnlyList<CollectionEntry> entries,
        bool full,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sender.GameId) || entries.Count == 0) return;

        foreach (var peer in _connections.Values)
        {
            if (peer.Id == sender.Id) continue;
            if (!string.Equals(peer.GameId, sender.GameId, StringComparison.Ordinal)) continue;
            try
            {
                var perViewer = FilterEntriesForViewer(entries, peer.ViewerSeat);
                if (perViewer.Count == 0) continue;
                var message = new UpdateMessage { Entries = perViewer.ToList(), Full = full };
                await SendJsonAsync(peer, message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast UPDATE to peer {PeerId}", peer.Id);
            }
        }
    }

    /// <summary>
    /// Broadcasts <paramref name="entries"/> to every connection in
    /// <paramref name="relayGameId"/> (no sender to skip — runtime is the source).
    /// Per-viewer privacy filter applied.
    /// </summary>
    private async Task BroadcastToAllAsync(
        string relayGameId,
        IReadOnlyList<CollectionEntry> entries,
        bool full,
        CancellationToken ct)
    {
        if (entries.Count == 0) return;
        foreach (var peer in _connections.Values)
        {
            if (!string.Equals(peer.GameId, relayGameId, StringComparison.Ordinal)) continue;
            try
            {
                var perViewer = FilterEntriesForViewer(entries, peer.ViewerSeat);
                if (perViewer.Count == 0) continue;
                var message = new UpdateMessage { Entries = perViewer.ToList(), Full = full };
                await SendJsonAsync(peer, message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast runtime UPDATE to peer {PeerId}", peer.Id);
            }
        }
    }

    private async Task SendJsonAsync(AutotableConnection connection, object payload, CancellationToken ct)
    {
        if (connection.Socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(payload, payload.GetType(), AutotableJson.Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        await connection.SendLock.WaitAsync(ct);
        try
        {
            await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private int ConnectionsInGame(string? gameId, Guid? except)
    {
        if (string.IsNullOrEmpty(gameId)) return 0;
        var count = 0;
        foreach (var c in _connections.Values)
        {
            if (!string.Equals(c.GameId, gameId, StringComparison.Ordinal)) continue;
            if (except.HasValue && c.Id == except.Value) continue;
            count++;
        }
        return count;
    }

    /// <summary>
    /// Phase I Wave 3 — gameId validation for ?gameId= and JOIN.gameId.
    /// </summary>
    /// <remarks>
    /// <para>Accepts null / empty / whitespace-only as "client did not pick" —
    /// returns <c>true</c> with <paramref name="normalized"/> = <c>null</c>;
    /// callers fall back to <see cref="AutotableWsEndpoint.DefaultGameId"/>.</para>
    /// <para>Rejects values that, after <see cref="string.Trim()"/>, exceed
    /// <see cref="AutotableWsEndpoint.MaxGameIdLength"/> or contain any
    /// <see cref="char.IsControl(char)"/> character. On reject, returns
    /// <c>false</c> with a <paramref name="closeReason"/> suitable for the WS
    /// close-frame reason string and leaves <paramref name="normalized"/> as
    /// <c>null</c>.</para>
    /// <para>Case is preserved (ordinal comparison is used everywhere in the
    /// manager). Interior whitespace is preserved; only leading / trailing
    /// whitespace is stripped.</para>
    /// </remarks>
    internal static bool TryNormalizeGameId(string? raw, out string? normalized, out string? closeReason)
    {
        normalized = null;
        closeReason = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return true;
        if (trimmed.Length > AutotableWsEndpoint.MaxGameIdLength)
        {
            closeReason = "gameId too long";
            return false;
        }
        foreach (var c in trimmed)
        {
            if (char.IsControl(c))
            {
                closeReason = "gameId contains control characters";
                return false;
            }
        }
        normalized = trimmed;
        return true;
    }

    /// <summary>
    /// Phase D-backend hook (PRESERVED, not deleted in Phase C-relay): when
    /// the Changsha runtime emits a state-change, we broadcast a full
    /// translator snapshot to every connection bound to that gameId. The
    /// snapshot is per-viewer so different seats may see different face-up
    /// orientations of the same hand.
    /// <para>Currently the runtime is not actively pushing for in-progress
    /// bundle games — Phase D-backend will own the merge between runtime
    /// authority and the bundle-driven relay state.</para>
    /// </summary>
    private void OnStateChanged(string runtimeGameId)
    {
        // Fire-and-forget broadcast. State events arrive on runtime worker
        // threads — we don't want to block them on WS sends.
        if (!_relayBinding.TryGetValue(runtimeGameId, out var relayGameId)) return;
        foreach (var connection in _connections.Values)
        {
            if (!string.Equals(connection.GameId, relayGameId, StringComparison.Ordinal)) continue;
            _ = BroadcastSnapshotAsync(connection, relayGameId);
        }
    }

    private async Task BroadcastSnapshotAsync(AutotableConnection connection, string gameId)
    {
        try
        {
            await SendFullSnapshotAsync(connection, gameId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast snapshot to connection {ConnectionId}", connection.Id);
        }
    }

    public void Dispose()
    {
        _runtime.StateChanged -= _stateChangedHandler;
        _bindingLock.Dispose();
    }
}

/// <summary>
/// Phase F §1 — the WS endpoint runs in one of two modes per connection, decided
/// by the <c>?variant=</c> query param at handshake. Other connections sharing the
/// same gameId may run in a different mode (a Relay-mode bundle observing a
/// Changsha-bound game) but the per-connection routing decisions are local —
/// runtime binding is owned by Changsha-mode connections only.
/// </summary>
public enum AutotableRuntimeMode
{
    /// <summary>
    /// Pure relay (Phase C behaviour): the backend forwards bundle UPDATEs verbatim
    /// to other connections in the same gameId and does NOT bind a Changsha runtime.
    /// Used for upstream's pure-bundle variants (four_player, three_player, bamboo,
    /// minefield) where the bundle's local Setup drives the deal.
    /// </summary>
    Relay = 0,

    /// <summary>
    /// Changsha rules engine drives the game (Phase D-backend behaviour): the backend
    /// lazily binds a <see cref="IChangshaGameRuntime"/> game on the first seat-take,
    /// and runtime <c>StateChanged</c> events drive <c>things</c> / <c>seats</c> /
    /// <c>match</c> / <c>claim</c> / <c>result</c> / <c>pickup</c> via the translator.
    /// </summary>
    ChangshaRuntime = 1,
}

/// <summary>Single bundle connection — one per (WebSocket, gameId, viewerSeat).</summary>
public sealed class AutotableConnection
{
    public Guid Id { get; } = Guid.NewGuid();
    public WebSocket Socket { get; }
    public string? GameId { get; set; }
    public int? ViewerSeat { get; }
    /// <summary>
    /// Phase J Wave 6 — persistent player identity (cookie-derived) for stats
    /// and host-promotion keying. <see cref="AutotableConnectionManager"/>
    /// resolves the <c>mahjong_pid</c> cookie at WS-upgrade time and sets
    /// this via the object initializer; if no cookie is supplied a fresh
    /// 8-hex token is used as a session-scoped fallback so legacy clients
    /// without identity-cookie support still get a unique value within the
    /// connection's lifetime.
    /// </summary>
    public string PlayerId { get; init; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    /// <summary>
    /// When true, taking a seat triggers auto-fill of remaining seats with bots
    /// (Phase D-backend §7). Bundle clients default to true via the <c>?bots=true</c>
    /// query param; the E2E test can disable it for deterministic seat-take tests.
    /// </summary>
    public bool AutoBotFill { get; init; } = true;

    /// <summary>
    /// Phase F §1.4 — the upstream variant requested by the connection. Defaults to
    /// <c>"changsha"</c>; other accepted values are <c>"four_player"</c>,
    /// <c>"three_player"</c>, <c>"bamboo"</c>, <c>"minefield"</c>. The value is
    /// preserved verbatim from the query string (not normalised) so the bundle's
    /// "set up Game" picker round-trips its label.
    /// </summary>
    public string Variant { get; init; } = "changsha";

    /// <summary>
    /// Phase F §1.4 — the deal mode the bundle is asking for. <c>"manual"</c> drives
    /// the post-Phase-F pickup state machine (default for Changsha); <c>"auto"</c>
    /// keeps the legacy single-shot <see cref="ChangshaGameStateMachine.Deal"/> path.
    /// </summary>
    public string DealMode { get; init; } = "manual";

    /// <summary>
    /// Phase F §1.4 — desired number of bot opponents for solo play (0..3). The
    /// runtime fills empty seats up to this count after the first seat-take.
    /// </summary>
    public int BotCount { get; init; } = 3;

    /// <summary>
    /// Phase F §1.4 — bot difficulty (case-insensitive: <c>"Easy"</c> / <c>"Medium"</c> /
    /// <c>"Hard"</c>). Routed through <see cref="ChangshaBotEngine.Resolve"/> at decision
    /// time; defaults to Medium for parity with the legacy <see cref="ChangshaBotPolicy"/>.
    /// </summary>
    public string BotDifficulty { get; init; } = "Medium";

    /// <summary>
    /// Phase I Wave 4 — true when the connection joined with <c>?seat=-1</c> (spectator
    /// mode). Spectator connections receive game snapshots and broadcasts but are never
    /// routed into a seat slot; their <see cref="ViewerSeat"/> stays <c>null</c> so the
    /// per-viewer privacy filter strips every foreign-seat face. Widens the
    /// <c>?botCount=</c> cap from 3 to 4 and, when paired with <c>botCount=4</c>, triggers
    /// the all-bots auto-deal flow in <see cref="AutotableConnectionManager"/>.
    /// </summary>
    public bool IsSpectator { get; init; }

    /// <summary>
    /// Phase F §1.4 — derived from <see cref="Variant"/>. <c>changsha</c> ⇒
    /// <see cref="AutotableRuntimeMode.ChangshaRuntime"/>; every other variant ⇒
    /// <see cref="AutotableRuntimeMode.Relay"/>.
    /// </summary>
    public AutotableRuntimeMode RuntimeMode =>
        string.Equals(Variant, "changsha", StringComparison.OrdinalIgnoreCase)
            ? AutotableRuntimeMode.ChangshaRuntime
            : AutotableRuntimeMode.Relay;

    public AutotableConnection(WebSocket socket, string? gameId, int? viewerSeat)
    {
        Socket = socket;
        GameId = gameId;
        ViewerSeat = viewerSeat;
    }
}
