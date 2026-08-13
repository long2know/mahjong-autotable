using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Players;
using Microsoft.Extensions.Options;

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
    private readonly Action<string, ChangshaGameState> _stateChangedHandler;
    // Frost 2026-05-29 — surfaced via the translator's claim-entry deadline so the
    // autotable client can render a real countdown.  Snapshot at construction time
    // (singleton) — runtime options are read-only at startup.
    private readonly int _claimWindowTimeoutMs;
    // SC-2 / G19 (Ripley, BINDING) — per-viewer opaque handle projection. Mandatory / default-on for
    // Changsha; the shared HKDF provider is built once from stable server-secret IKM and reused across
    // every connection (it holds only the HKDF-derived key, no per-game/per-connection state).
    private readonly bool _opaqueHiddenHandles;
    private readonly OpaqueTileHandleProvider? _handleProvider;

    public AutotableConnectionManager(
        IChangshaGameRuntime runtime,
        ILogger<AutotableConnectionManager> logger,
        IOptions<ChangshaRuntimeOptions> runtimeOptions,
        Mahjong.Autotable.Api.Auth.JwtSigningKeyProvider jwtSigningKeys)
    {
        _runtime = runtime;
        _logger = logger;
        _claimWindowTimeoutMs = runtimeOptions?.Value?.ClaimWindowTimeoutMs ?? 0;
        _opaqueHiddenHandles = runtimeOptions?.Value?.OpaqueHiddenHandles ?? true;
        if (_opaqueHiddenHandles)
        {
            // IKM priority: a DEDICATED decoded `Privacy:HandleSecret` (base64) when configured,
            // else the STABLE configured JWT signing-key bytes. Never a per-process random secret,
            // and never used as a raw MAC key — OpaqueTileHandleProvider runs HKDF over this IKM.
            byte[]? ikm = null;
            var b64 = runtimeOptions?.Value?.HandleSecretBase64;
            if (!string.IsNullOrWhiteSpace(b64))
            {
                try { ikm = Convert.FromBase64String(b64); } catch { ikm = null; }
            }
            ikm ??= jwtSigningKeys?.ActiveKey?.Material;
            if (ikm is { Length: > 0 })
            {
                try
                {
                    _handleProvider = new OpaqueTileHandleProvider(ikm);
                }
                catch (ArgumentException ex)
                {
                    // Sub-minimum IKM ⇒ cannot derive opaque handles; disable rather than weaken.
                    _logger.LogWarning(ex,
                        "SC-2 opaque tile handles disabled: server-secret IKM is below the provider minimum. " +
                        "Configure Privacy:HandleSecret (base64, >=32 bytes) or a sufficiently long JWT signing key.");
                    _handleProvider = null;
                }
            }
            else
            {
                _logger.LogWarning(
                    "SC-2 opaque tile handles requested but no server-secret IKM is available " +
                    "(neither Privacy:HandleSecret nor a configured JWT signing key). Hidden tiles will emit real ids.");
            }
        }
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
        int? requestedSeat = null;
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
                // SECURITY — seat-ownership hardening (Bishop rev2, Blocker D). The raw
                // `?seat=N` query param is a NON-AUTHORITATIVE preference/hint ONLY; it MUST
                // NOT seed ViewerSeat, which drives per-viewer real-tile-id projection. Before
                // this fix a client could connect with `?seat=2` (never taking the seat) and
                // receive seat 2's real concealed hand — a confirmed leak of the foreign
                // seat's real tile ids. ViewerSeat is now bound EXCLUSIVELY from
                // runtime-confirmed ownership: TryGetSeatForPlayer on (re)connect
                // (TryInferViewerSeatOnConnectAsync) or a successful TakeSeat. An unowned
                // requester stays a spectator/opaque (foreign hands render as anonymous
                // face-down handles). The requested seat is retained only as a hint (never
                // consulted for projection) for telemetry / possible future auto-seat UX.
                requestedSeat = parsedSeat;
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

        // Ferro WP-E/#120 (Hudson C-2 determinism gap) — honor the lobby's
        // `?seed=` so a chosen seed reproduces the same game. Parsed as a
        // non-negative 32-bit int (matches the frontend coerceSeed range
        // 0..int.MaxValue); anything invalid leaves seed null ⇒ the runtime
        // picks a random seed, exactly as before. Threaded to
        // ChangshaGameRuntime.CreateGameAsync at first runtime bind below.
        int? seed = null;
        if (query.TryGetValue("seed", out var sd) && int.TryParse(sd.ToString(), out var parsedSeed) && parsedSeed >= 0)
            seed = parsedSeed;

        // #121/#130/C-2 (Lead + canonical §6.3) — honor the lobby's `?handCount=`. Canonical
        // Changsha caps a match at 16 hands (§6.3), so the accepted create-time values are
        // {1,4,8,16}. The legacy UI's 32 option is misleading — the engine's 4-round terminal
        // (HandsPerRound=4 × 4 rounds) already ends a stored-32 game at hand 16 — so a requested 32
        // is NORMALIZED to the authoritative 16 (surfaced as MaxHands=16) rather than stored. Any
        // other value (or absent) keeps the runtime default of 4. Future >16 designs: issue #130.
        // Threaded to CreateGameAsync at first runtime bind (first-creator-wins).
        var maxHands = 4;
        if (query.TryGetValue("handCount", out var hcRaw)
            && int.TryParse(hcRaw.ToString(), out var parsedHandCount))
        {
            maxHands = parsedHandCount switch
            {
                1 or 4 or 8 or 16 => parsedHandCount,
                32 => 16, // legacy UI option → canonical §6.3 cap
                _ => 4
            };
        }

        var connection = new AutotableConnection(ws, queryGameId, viewerSeat)
        {
            AutoBotFill = autoBotFill,
            Variant = variant,
            DealMode = dealMode,
            BotCount = botCount,
            BotDifficulty = botDifficulty,
            MaxHands = maxHands,
            IsSpectator = isSpectator,
            Seed = seed,
            // Blocker D (Bishop rev2) — non-authoritative `?seat=` hint. Retained for
            // telemetry only; NEVER consulted for per-viewer projection (ViewerSeat is
            // bound solely from confirmed ownership / TakeSeat).
            RequestedSeat = requestedSeat,
            // Phase J Wave 6 — persistent cookie-derived player id (resolved
            // by AutotableWsEndpoint.MapAutotableWs before the WS upgrade).
            // Replaces the previous random per-connection token so career
            // stats and host-promotion key off the same id across reconnects.
            PlayerId = playerId,
        };
        _connections[connection.Id] = connection;
        _logger.LogInformation(
            "Autotable WS connected (connectionId={ConnectionId}, gameId={GameId}, seat={Seat}, spectator={Spectator}, bots={Bots}, variant={Variant}, dealMode={DealMode}, botCount={BotCount}, botDifficulty={BotDifficulty}, seed={Seed}, maxHands={MaxHands}, runtimeMode={RuntimeMode})",
            connection.Id, queryGameId, viewerSeat, isSpectator, autoBotFill,
            variant, dealMode, botCount, botDifficulty, seed, maxHands, connection.RuntimeMode);

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
        await TryInferViewerSeatOnConnectAsync(connection, ct);
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
        await TryInferViewerSeatOnConnectAsync(connection, ct);
        await TryAutoDealForSpectatorAsync(connection, ct);
    }

    /// <summary>
    /// BE-5 (Ripley §9.1/§11.1) — reconnect owner inference. When a connection joins a
    /// game its durable player already owns a seat in (a reconnect / a fresh tab), bind
    /// <see cref="AutotableConnection.ViewerSeat"/> to that seat and re-project so the
    /// owner's own hand renders FACE-UP immediately. Blocker D (Bishop rev2) — this
    /// runtime-ownership lookup (TryGetSeatForPlayer) is now the ONLY connect-time source of
    /// ViewerSeat: the raw <c>?seat=</c> query is a non-authoritative hint that no longer
    /// seeds ViewerSeat, so a requester who owns no seat is a clean no-op (stays a
    /// spectator/opaque viewer) instead of projecting a foreign hand.
    /// </summary>
    private async Task TryInferViewerSeatOnConnectAsync(AutotableConnection connection, CancellationToken ct)
    {
        try
        {
            if (connection.GameId is null || connection.ViewerSeat is not null) return;
            if (!_runtimeBinding.TryGetValue(connection.GameId, out var runtimeGameId)) return;
            var seat = _runtime.TryGetSeatForPlayer(runtimeGameId, connection.PlayerId);
            if (seat is null) return;
            connection.ViewerSeat = seat;
            await SendFullSnapshotAsync(connection, connection.GameId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Reconnect viewer-seat inference failed for {ConnectionId}", connection.Id);
        }
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
            // Bishop W25 — forward the spectator's `?botDifficulty=` so
            // an all-bots watch-mode table honours the URL difficulty.
            var runtimeGameId = await EnsureRuntimeBoundAsync(
                connection.GameId!, hostPlayerId: null, ct,
                botDifficulty: connection.BotDifficulty, seed: connection.Seed, maxHands: connection.MaxHands);
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
        //
        // BE-2 / G18 (Ripley §9.1/§11.3 / RC-2/RC-11) — in ChangshaRuntime mode the
        // runtime is authoritative over the SCENE: inbound client `things`, `match`,
        // and `dice` are DROPPED (no store, no relay, no peer broadcast). This closes
        // the board-spoofing / local-scatter relay (a client drag or local `world.deal`
        // could otherwise mutate peers' boards). When any such push is dropped we send
        // the offender a corrective full authoritative snapshot so its optimistic local
        // scatter is immediately overwritten (BE-3 already owns the deal, so `match` no
        // longer needs to double as a Deal trigger). Legitimate seat/claim/pickup/discard
        // commands are preserved.
        var passthroughEntries = new List<CollectionEntry>(entries.Count);
        var droppedRuntimeOwnedScenePush = false;
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case "seats":
                    // Hicks's "Take Seat" click — route to runtime.TakeSeatAsync,
                    // optionally auto-fill remaining seats with bots for solo play.
                    // The leave-seat path (Ripley L-10) owns its own peer broadcast
                    // (per-player tombstones via RemovePlayerEntries), and the
                    // occupied-seat/take path is runtime-owned too. Both signal
                    // back via the return value so we skip the raw passthrough that
                    // would otherwise re-store a stale `seats[playerId]` entry.
                    var handledBySeatAction = await TryHandleSeatTakeAsync(connection, entry, ct);
                    if (!handledBySeatAction)
                    {
                        // Mirror upstream's perPlayer semantics so the seat shows up
                        // immediately for other clients; runtime will reconfirm on its
                        // next StateChanged push.
                        passthroughEntries.Add(entry);
                    }
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
                    // BE-2 — `match` is runtime-owned. Still invoke the (now-redundant,
                    // phase-guarded) Deal handler so a pre-BE-3 client that races the
                    // seat-fill start can't wedge, but NEVER relay/store the client's
                    // match to peers, and correct the offender's scene below.
                    await TryHandleMatchActionAsync(connection, entry, ct);
                    droppedRuntimeOwnedScenePush = true;
                    break;

                case "things":
                case "dice":
                    // BE-2 — runtime-owned scene collections. The engine owns every tile
                    // position and the dice roll; drop inbound client pushes (no store,
                    // no relay, no peer broadcast) and correct the offender below.
                    droppedRuntimeOwnedScenePush = true;
                    break;

                case ChangshaCollectionKinds.Result:
                    // Result is server-emitted only — ignore client pushes.
                    break;

                case ChangshaCollectionKinds.GameComplete:
                    // #116/#122 — gameComplete is server-emitted only (locked C-1, inbound to
                    // clients). Drop any client push so a buggy/malicious client can't relay a
                    // fake end-of-match signal to peers and spuriously trigger their modals.
                    break;

                case ChangshaCollectionKinds.Turn:
                    // C-1/C-2 — the discard-turn cue is server-emitted only. Drop any client
                    // push so a client can't forge whose turn it is; the runtime re-broadcasts
                    // the authoritative turn on every AwaitingDiscard snapshot.
                    break;

                case ActionRejectedKind:
                    // Server-emitted only; never let a client forge a rejection for a peer.
                    break;

                default:
                    // mouse, sound, nicks, ephemeral, unique, perPlayer — pure cosmetic /
                    // meta (peer cursors, nicknames, collection declarations). Pass through
                    // to the relay store. (things/dice/match are handled above as
                    // runtime-owned per BE-2.)
                    passthroughEntries.Add(entry);
                    break;
            }
        }

        // BE-2 / G18 — a dropped runtime-owned scene push means the offender may have
        // optimistically mutated its local scene (drag / local deal). Re-project the
        // authoritative snapshot to it (UpdateSource.Runtime) so any local scatter is
        // overwritten. Peers are never told about the client's push.
        if (droppedRuntimeOwnedScenePush)
        {
            try { await SendFullSnapshotAsync(connection, connection.GameId, ct); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Corrective snapshot after dropped scene push failed for {ConnectionId}", connection.Id);
            }
        }

        if (passthroughEntries.Count == 0) return;

        var applied = state.ApplyUpdate(passthroughEntries, UpdateSource.Client);
        if (applied.Count == 0) return;

        await BroadcastToOthersAsync(connection, applied, full: false, ct);
    }

    // ── Inbound action routing (seats / claim / match) ───────────────

    private async Task<bool> TryHandleSeatTakeAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        // value shape: { seat: int } (per upstream Player.svelte). null = leave.
        // Returns true whenever the entry has been handled by the runtime seat
        // path (leave or take). Returns false only for guard / no-op exits so the
        // existing passthrough behaviour is preserved verbatim.
        if (entry.Value is null) return false;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return false;
        if (!je.TryGetProperty("seat", out var seatEl)) return false;

        // Phase F §1.2 — Relay-mode connections do NOT bind a Changsha runtime.
        // The bundle's local Setup drives the deal; the backend only relays.
        if (connection.RuntimeMode != AutotableRuntimeMode.ChangshaRuntime) return false;

        // Ripley L-10 audit fix — `{seat: null}` is the upstream Player.svelte
        // shape for an explicit "Leave" action. Previously this returned silently
        // because the handler only accepted JsonValueKind.Number, leaving stale
        // seats around (`nicks` entries never cleared, the lobby seat counter
        // permanently stuck at 4/4). Route through the runtime so the persistent
        // identity AND the per-tab transport binding both clear.
        if (seatEl.ValueKind == JsonValueKind.Null)
        {
            try
            {
                var runtimeGameIdLeave = await EnsureRuntimeBoundAsync(connection.GameId!, connection.PlayerId, ct, seed: connection.Seed, maxHands: connection.MaxHands);
                await _runtime.ReleaseSeatAsync(runtimeGameIdLeave, connection.PlayerId, connection.Id.ToString("N"), ct);

                // Ripley prodready follow-up (L-10 part 2, 2026-06-03) — the
                // runtime's ReleaseSeatAsync only broadcasts on SignalR
                // (`PlayerSeated` event), but the autotable bundle listens on
                // the WS `/autotable/ws` upstream protocol. Without this
                // mirror of the disconnect path (see HandleDisconnectAsync
                // ~30 LOC below), the peer's `seats`/`nicks` collection
                // entries for this playerId stay stored under the persistent
                // key and never tombstone, so the lobby seat-counter and the
                // sidebar nickname remain ghosted as "occupied" until a
                // page refresh re-runs the JOIN flow.
                //
                // RemovePlayerEntries clears (seats, nicks, mouse, …) for
                // this playerId in the per-game relay store; broadcasting the
                // resulting null entries to peers triggers the bundle's
                // standard tombstone path (Collection#set(key, undefined)).
                // The runtime's StateChanged push that fires inside
                // ReleaseSeatAsync above then refills seat 0 with the
                // translator's placeholder identity (`seat-0`) so peers see
                // the seat as available, not occupied by a ghost.
                if (_games.TryGetValue(connection.GameId!, out var state))
                {
                    var tombstones = state.RemovePlayerEntries(connection.PlayerId);
                    if (tombstones.Count > 0)
                    {
                        try
                        {
                            await BroadcastToOthersAsync(connection, tombstones, full: false, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex,
                                "Failed to broadcast leave-seat tombstones for {ConnectionId}",
                                connection.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Seat release failed for connection {ConnectionId}", connection.Id);
            }
            return true;
        }

        if (seatEl.ValueKind != JsonValueKind.Number) return false;
        if (!seatEl.TryGetInt32(out var seatIndex)) return false;
        if (seatIndex is < 0 or > 3) return false;

        try
        {
            // Bishop W25 — forward `?botDifficulty=` on the seat-take path
            // so a human-created game honours the URL difficulty for its
            // bot opponents. EnsureRuntimeBoundAsync only emits the
            // SetBotStrategyAsync call when a non-empty difficulty is
            // supplied; the runtime default (Medium) covers callers that
            // never specified one.
            //
            // #153 — this is the ONLY caller that passes resettingConnection, so the stale
            // default-game guard fires exclusively for a human sitting down to play. A newcomer
            // meeting an abandoned, already-started `changsha-default` gets a fresh table instead of
            // inheriting its frozen seat/turn state; a deliberate reconnect and live co-play games
            // are untouched (see EnsureRuntimeBoundAsync / ShouldRetireStaleDefault).
            var runtimeGameId = await EnsureRuntimeBoundAsync(
                connection.GameId!, connection.PlayerId, ct,
                botDifficulty: connection.BotDifficulty, seed: connection.Seed, maxHands: connection.MaxHands,
                resettingConnection: connection);
            // Phase J Wave 6 — pass the persistent player id alongside the
            // per-connection transport id (the AutotableConnection.Id GUID
            // serves as the connection-level routing key inside the runtime).
            var grantedSeat = await _runtime.TakeSeatAsync(runtimeGameId, connection.PlayerId, connection.Id.ToString("N"), seatIndex, ct);

            // BE-5 (Ripley §9.1/§11.1 / RC-3) + Blocker D hardening (Bishop rev2) — bind the
            // per-viewer projection to the seat the runtime AUTHORITATIVELY granted, never the
            // raw requested index. TakeSeatAsync throws HubException when the seat is owned by a
            // different connection, so reaching this line means this player now owns
            // `grantedSeat`; on the take path with an explicit index the two are equal, but
            // using the return value keeps ViewerSeat provably tied to confirmed ownership
            // (defense-in-depth alongside the removed `?seat=` projection grant). Setting it
            // BEFORE the deal means every subsequent StateChanged broadcast is already correctly
            // faced; the explicit re-projection below covers the transition.
            connection.ViewerSeat = grantedSeat;

            if (connection.AutoBotFill)
            {
                await _runtime.FillEmptySeatsWithBotsAsync(runtimeGameId, ct);
            }

            // BE-3 (Ripley §9.1 / RC-5) — server-driven start. Once the human's seat
            // take plus configured bot-fill leaves every seat occupied, the SERVER
            // starts the game: Auto deals atomically, Manual arms the pickup ceremony
            // (RollingDice). The legacy client `#deal` / `match` Deal trigger is no
            // longer required. Idempotent + phase-guarded (only from Seating with all
            // seats filled; the runtime's StartGame throws past Seating and is caught).
            await TryServerStartOnSeatFillAsync(connection, runtimeGameId, ct);

            // W23 follow-up (Gap 2) — if the seat-take lands on an already-dealt
            // game (e.g. a fresh tab opened against a hand that's already in
            // AwaitingDiscard), implicit-ack so the runtime doesn't stall
            // waiting for a SignalR-style AckDeal that this transport will
            // never send. No-op when the state is still pre-deal.
            await TryAutoAckSeatedConnectionAsync(connection, runtimeGameId, ct);

            // BE-5 — immediately re-project a full per-viewer snapshot so the owner's own
            // hand flips FACE-UP (and foreign hands stay face-down) on the take-seat
            // transition, without waiting for the next mutation or a client reload.
            await SendFullSnapshotAsync(connection, connection.GameId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Seat take failed for connection {ConnectionId} seat {Seat}", connection.Id, seatIndex);
        }

        // Take-seat path is fully owned by the runtime (the StateChanged push
        // re-broadcasts the authoritative seat assignment). Do not passthrough
        // the inbound client entry.
        return true;
    }

    /// <summary>
    /// W23 follow-up (Vasquez Gap 2) — implicit AckDeal for the autotable WS
    /// transport. The bundle has no AckDeal route of its own (only the SignalR
    /// <c>changsha</c> hub does), so the runtime would otherwise wait
    /// indefinitely for a handshake that never arrives.
    ///
    /// <para>Invariants:
    /// <list type="bullet">
    ///   <item>Phase-guarded — we only ack when the runtime is in
    ///   <see cref="ChangshaPhase.AwaitingDiscard"/> or later. Pre-deal phases
    ///   (Seating, RollingDice, manual pickup) are skipped: no hand exists yet
    ///   so no ack is meaningful, and acking pre-deal could cause the turn loop
    ///   to start the instant the deal completes without giving the bundle a
    ///   chance to render. (Idempotent re-entry on the next state change picks
    ///   up the post-deal phase naturally.)</item>
    ///   <item>Idempotent — <see cref="IChangshaGameRuntime.AcknowledgeDealAsync"/>
    ///   uses a HashSet under the instance lock; repeat calls are safe.</item>
    ///   <item>Seat-scoped — only acks the seat actually bound to this
    ///   connection (via <see cref="IChangshaGameRuntime.TryGetSeatForConnection"/>).
    ///   A spectator connection (no bound seat) is a clean no-op.</item>
    /// </list></para>
    /// </summary>
    private async Task TryAutoAckSeatedConnectionAsync(
        AutotableConnection connection,
        string runtimeGameId,
        CancellationToken ct)
    {
        if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return;
        if (!IsPastDealPhase(snap.Phase)) return;

        var seat = _runtime.TryGetSeatForConnection(runtimeGameId, connection.Id.ToString("N"));
        if (seat is null) return;

        // Defensive: a seat reported as a bot shouldn't be auto-acked from a
        // human connection path (would only happen if a bot fill raced this
        // call). HasAllHumanAcks ignores bot seats so this is also harmless.
        if (snap.Seats[seat.Value].IsBot) return;

        try
        {
            await _runtime.AcknowledgeDealAsync(runtimeGameId, seat.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Auto-ack failed (gameId={GameId}, seat={Seat}, connectionId={ConnectionId})",
                runtimeGameId, seat, connection.Id);
        }
    }

    private static bool IsPastDealPhase(ChangshaPhase phase) => phase switch
    {
        ChangshaPhase.AwaitingDiscard => true,
        ChangshaPhase.AwaitingClaim => true,
        ChangshaPhase.DeclaringKong => true,
        ChangshaPhase.DrawingReplacement => true,
        ChangshaPhase.Scoring => true,
        ChangshaPhase.EndHand => true,
        ChangshaPhase.RotatingBanker => true,
        ChangshaPhase.WallExhausted => true,
        _ => false
    };

    internal const string ActionRejectedKind = "actionRejected";

    private readonly record struct SeatAuthorization(int? Seat, string? Failure)
    {
        public bool IsAuthorized => Seat.HasValue && Failure is null;
    }

    private SeatAuthorization AuthorizeSeatAction(
        AutotableConnection connection,
        string? runtimeGameId,
        int? requestedSeat)
    {
        if (string.IsNullOrEmpty(runtimeGameId))
            return new(null, "no-game");

        var ownedSeat = ResolveOwnedSeat(connection, runtimeGameId);
        if (ownedSeat is null)
        {
            return new(null, connection.IsSpectator
                ? "spectator-owns-no-seat"
                : "connection-owns-no-seat");
        }

        return requestedSeat.HasValue && requestedSeat.Value != ownedSeat.Value
            ? new(ownedSeat, "seat-not-owned-by-connection")
            : new(ownedSeat, null);
    }

    private int? ResolveOwnedSeat(AutotableConnection connection, string runtimeGameId)
    {
        var seat = _runtime.TryGetSeatForConnection(runtimeGameId, connection.Id.ToString("N"));
        if (seat is not null)
            return seat;

        // A transport reconnect loses its connection binding but retains the runtime-owned
        // persistent player binding. Creation placeholders are not durable identities.
        if (string.IsNullOrEmpty(connection.PlayerId)
            || connection.PlayerId.StartsWith("human-", StringComparison.OrdinalIgnoreCase)
            || connection.PlayerId.StartsWith("bot-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return _runtime.TryGetSeatForPlayer(runtimeGameId, connection.PlayerId);
    }

    private async Task RejectSeatActionAsync(
        AutotableConnection connection,
        string action,
        int? requestedSeat,
        SeatAuthorization authorization,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Rejected WS action {Action} from connection {ConnectionId} in game {GameId}: "
            + "requestedSeat={RequestedSeat}, ownedSeat={OwnedSeat}, reason={Reason}",
            action, connection.Id, connection.GameId, requestedSeat,
            authorization.Seat, authorization.Failure);

        await SendJsonAsync(connection, new UpdateMessage
        {
            Entries =
            [
                new CollectionEntry(ActionRejectedKind, "current", new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["reason"] = authorization.Failure,
                    ["requestedSeat"] = requestedSeat,
                    ["ownedSeat"] = authorization.Seat
                })
            ],
            Full = false
        }, ct);

        if (authorization.Failure != "no-game")
            await SendFullSnapshotAsync(connection, connection.GameId, ct);
    }

    private async Task TryHandleClaimActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        // Vasquez tile-interaction G4 root-cause defence — also accept `double` keys
        // so a bundle that sends a JS-native numeric seat (always serialised as a
        // JSON number, sometimes routed through the protocol's `double` fallback)
        // is parsed instead of being silently dropped. See
        // `CollectionEntryJsonConverter.Read` for the upstream parse rules.
        var seatIndex = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            string s when int.TryParse(s, out var p) => p,
            _ => -1
        };
        if (seatIndex is < 0 or > 3) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;

        // Wire contract — the shipped bundle (game-ui.ts sendClaim) writes:
        //   claim[seat] = { action: "claim", type: "Pung"|"Chow"|"Kong"|"Hu" }  // a meld / win claim
        //   claim[seat] = { action: "pass",  type: null }                       // decline
        // The legacy pre-bundle form { action: "Pung"|"Chow"|"Kong"|"Hu"|"Pass" } is still accepted.
        // #134 — previously this read `action` AS the claim type, so a real claim passed the literal
        // "claim" to ParseClaimType (throw, swallowed) and every non-Pass human claim stalled.
        if (!je.TryGetProperty("action", out var actionEl) || actionEl.ValueKind != JsonValueKind.String) return;
        var action = actionEl.GetString() ?? string.Empty;

        string? type = null;
        if (je.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            type = typeEl.GetString();

        // The bundle never sends tileIds; accept them if a client does (Chow without tileIds falls
        // back to the runtime's lowest-rank pattern, matching the bot path).
        int[]? tileIds = null;
        if (je.TryGetProperty("tileIds", out var tileIdsEl) && tileIdsEl.ValueKind == JsonValueKind.Array)
        {
            tileIds = new int[tileIdsEl.GetArrayLength()];
            for (var i = 0; i < tileIds.Length; i++) tileIds[i] = tileIdsEl[i].GetInt32();
        }

        // Resolve intent: `action == "pass"` declines; `action == "claim"` carries the meld/win type
        // in `type`; otherwise treat `action` itself as the type (legacy form).
        bool isPass;
        string? claimType;
        if (string.Equals(action, "pass", StringComparison.OrdinalIgnoreCase))
        {
            isPass = true;
            claimType = null;
        }
        else if (string.Equals(action, "claim", StringComparison.OrdinalIgnoreCase))
        {
            isPass = false;
            claimType = type;
        }
        else
        {
            isPass = false;
            claimType = action;
        }

        // Reject malformed / unknown claim types explicitly (visible log) WITHOUT submitting a bogus
        // claim that would throw inside ClaimAsync — the window stays open for retry / server auto-pass.
        if (!isPass && !IsKnownClaimType(claimType))
        {
            _logger.LogWarning(
                "Ignoring malformed human claim from seat {Seat} in game {GameId}: action='{Action}', "
                + "type='{Type}' is not a known claim type (Pung/Chow/Kong/Hu).",
                seatIndex, connection.GameId, action, type ?? "(none)");
            return;
        }

        _runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId);
        var authorization = AuthorizeSeatAction(connection, runtimeGameId, seatIndex);
        if (!authorization.IsAuthorized)
        {
            await RejectSeatActionAsync(connection, isPass ? "pass" : "claim", seatIndex, authorization, ct);
            return;
        }

        try
        {
            if (isPass)
            {
                await _runtime.PassAsync(runtimeGameId!, authorization.Seat!.Value, ct);
            }
            else
            {
                await _runtime.ClaimAsync(runtimeGameId!, authorization.Seat!.Value, claimType!, tileIds, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Claim {Action}/{Type} for seat {Seat} was rejected by the runtime",
                action, claimType ?? "pass", authorization.Seat);
        }
    }

    // #134 — the claim types the runtime's ParseClaimType accepts (case-insensitive). Kept in lock-step
    // with ChangshaGameRuntime.ParseClaimType so a malformed human claim is rejected up-front rather
    // than throwing (and being swallowed) inside the runtime.
    private static bool IsKnownClaimType(string? s) => s is not null && s.ToLowerInvariant() switch
    {
        "pung" or "chow" or "kong" or "hu" => true,
        _ => false,
    };

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
    /// The requested seat comes from the entry key or value; the acting seat is resolved
    /// from the sending connection's server-side runtime ownership.
    /// </summary>
    private async Task TryHandlePickupActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;
        _runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId);

        // Action wire-format (per autotable-src/src/client.ts:90-95):
        //   outbound "rollDice" : ["pickup", "rollDice", { seatIndex }]
        //   outbound "take"     : ["pickup", "take",     { seatIndex, wallTileIds }]
        // The bundle puts the command verb in the ENTRY KEY (string), not in a
        // value field. Value-side `action` is also honoured for forward-compat
        // with any client that prefers the verbose shape.
        var keyAction = entry.Key as string;
        var action = string.Empty;
        if (!string.IsNullOrEmpty(keyAction) && !int.TryParse(keyAction, out _))
        {
            action = keyAction;
        }
        else if (je.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.String)
        {
            action = actionEl.GetString() ?? string.Empty;
        }

        var isRollDice = string.Equals(action, "rollDice", StringComparison.OrdinalIgnoreCase);
        var isTake = string.Equals(action, "take", StringComparison.OrdinalIgnoreCase);
        if (!isRollDice && !isTake) return;

        // The wire seat is only a requested seat. It never selects the runtime actor.
        var seatFromKey = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            string s when int.TryParse(s, out var p) => p,
            _ => -1
        };
        var requestedSeatValue = seatFromKey;
        if (requestedSeatValue is < 0 or > 3)
        {
            if (je.TryGetProperty("seatIndex", out var seatEl)
                && seatEl.ValueKind == JsonValueKind.Number
                && (seatEl.TryGetInt32(out var s)
                    || (seatEl.TryGetDouble(out var sd) && sd >= 0 && sd <= 3 && (s = (int)sd) >= 0)))
            {
                requestedSeatValue = s;
            }
        }
        int? requestedSeat = requestedSeatValue is >= 0 and <= 3 ? requestedSeatValue : null;

        var authorization = AuthorizeSeatAction(connection, runtimeGameId, requestedSeat);
        if (!authorization.IsAuthorized)
        {
            await RejectSeatActionAsync(
                connection,
                isRollDice ? "pickup.rollDice" : "pickup.take",
                requestedSeat,
                authorization,
                ct);
            return;
        }
        var seatIndex = authorization.Seat!.Value;

        try
        {
            if (isRollDice)
            {
                await _runtime.RollDiceAsync(runtimeGameId!, seatIndex, ct);
                return;
            }

            int count = 0;
            if (je.TryGetProperty("count", out var countEl) && countEl.ValueKind == JsonValueKind.Number && countEl.TryGetInt32(out var c))
                count = c;
            else if (je.TryGetProperty("wallTileIds", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                count = idsEl.GetArrayLength();
            if (count <= 0) return;
            await _runtime.TakeTilesFromWallAsync(runtimeGameId!, seatIndex, count, ct);
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
    /// The endpoint first verifies that the sending connection owns the requested seat.
    /// </summary>
    private async Task TryHandleDiscardActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is null) return;
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;
        _runtimeBinding.TryGetValue(connection.GameId!, out var runtimeGameId);

        // Seat: prefer explicit key (an int seat), fall back to "seatIndex" prop.
        // Vasquez tile-interaction G4 root-cause defence — accept `double` keys
        // alongside `long` / `int`. Pre-fix the discard handler only matched
        // integer types and silently dropped pushes whose key was boxed as
        // `double` by the protocol parser (which prior to the W4-followup fix in
        // `CollectionEntryJsonConverter.Read` could happen for any integer-valued
        // key). Without this case the dealer's `["discard", 0, …]` push was
        // rejected as a bad seat and the runtime never saw the discard.
        var seatIndex = entry.Key switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
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

        var authorization = AuthorizeSeatAction(connection, runtimeGameId, seatIndex);
        if (!authorization.IsAuthorized)
        {
            await RejectSeatActionAsync(connection, "discard", seatIndex, authorization, ct);
            return;
        }

        try
        {
            await _runtime.DiscardAsync(runtimeGameId!, authorization.Seat!.Value, tileId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Discard failed for seat {Seat} tile {Tile}", authorization.Seat, tileId);
        }
    }

    /// <summary>
    /// BE-3 (Ripley §9.1 / RC-5) — server-driven start on seat-fill. Fires only from
    /// <see cref="ChangshaPhase.Seating"/> with every seat occupied; Auto deals in one
    /// shot, Manual arms the pickup ceremony (RollingDice). Idempotent: a duplicate
    /// seat-take / re-entry is a no-op (phase guard here + <c>RequirePhase(Seating)</c>
    /// inside the runtime's <c>StartGame</c>, which throws-and-is-caught past Seating).
    /// Replaces the legacy client <c>#deal</c> / <c>match</c> Deal trigger so the
    /// authoritative deal never depends on a client scene push (BE-2 can then drop
    /// inbound <c>match</c>).
    /// </summary>
    private async Task TryServerStartOnSeatFillAsync(
        AutotableConnection connection, string runtimeGameId, CancellationToken ct)
    {
        try
        {
            if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return;
            if (snap.Phase != ChangshaPhase.Seating) return;                       // already started
            if (!_runtime.AreAllSeatsOccupied(runtimeGameId)) return;              // wait for all seats

            var requestedMode = string.Equals(connection.DealMode, "manual", StringComparison.OrdinalIgnoreCase)
                ? DealMode.Manual
                : DealMode.Auto;
            await _runtime.ApplyDealModeAsync(runtimeGameId, requestedMode, ct);
            await _runtime.StartGameAsync(runtimeGameId, ct);
            // Auto: after-deal the runtime awaits an ack the WS transport never sends —
            // ack the caller's bound seat so the turn loop advances. No-op for Manual /
            // pre-AwaitingDiscard and for an already-acked seat.
            await TryAutoAckSeatedConnectionAsync(connection, runtimeGameId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server start-on-seat-fill failed for connection {ConnectionId}", connection.Id);
        }
    }

    private async Task TryHandleMatchActionAsync(AutotableConnection connection, CollectionEntry entry, CancellationToken ct)
    {
        if (entry.Value is not JsonElement je || je.ValueKind != JsonValueKind.Object) return;

        // A match[0] push with `dealCommand: "start"` is Hicks's "Deal" button.
        // We also fall back to "any match push with dealer field while seating"
        // for compatibility with the upstream bundle's vanilla Deal control —
        // `world.deal()` (autotable-src/src/world.ts) emits `{ dealer, honba,
        // conditions }` with no dealCommand field, and that is the ONLY match
        // push that fires from a Seating phase. Vasquez W23 follow-up: without
        // this fallback, the human-led "Deal" click never reaches
        // StartGameAsync (observed in playtest-artifacts/playtest-human-led).
        var isDealCommand = false;
        if (je.TryGetProperty("dealCommand", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.String)
        {
            isDealCommand = string.Equals(cmdEl.GetString(), "start", StringComparison.OrdinalIgnoreCase);
        }

        // Vanilla deal push from upstream `world.deal()` — match[0] with `{ dealer,
        // honba, conditions }` and no `dealCommand` field. The bundle's
        // setupDealButton.onSuccess is exactly this push. Key may arrive as long
        // OR double (the JS bundle serializes `0` as a plain `0`, but the .NET
        // reader sometimes routes it through TryGetDouble for the boxed key).
        var keyIsZero = entry.Key switch
        {
            long l => l == 0,
            int i => i == 0,
            double d => d == 0.0,
            string s => s == "0",
            _ => false
        };
        var isVanillaDealPush = !isDealCommand
            && keyIsZero
            && je.TryGetProperty("dealer", out var dealerEl)
            && dealerEl.ValueKind == JsonValueKind.Number;

        if (!isDealCommand && !isVanillaDealPush) return;

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

            // W23 follow-up — propagate the connection's `?dealMode=` query into
            // the runtime state BEFORE StartGameAsync branches on it. Without
            // this hop, `?dealMode=manual` is silently a no-op (Vasquez's Gap 1
            // — see .squad/decisions/inbox/vasquez-human-led-playtest.md). Phase
            // guard inside ApplyDealModeAsync prevents accidental mid-hand
            // flips when this path is re-entered on a reconnect.
            var requestedMode = string.Equals(connection.DealMode, "manual", StringComparison.OrdinalIgnoreCase)
                ? DealMode.Manual
                : DealMode.Auto;
            await _runtime.ApplyDealModeAsync(runtimeGameId, requestedMode, ct);

            await _runtime.StartGameAsync(runtimeGameId, ct);

            // W23 follow-up — auto-ack on the caller's bound seat (Gap 2). After
            // an auto-deal the runtime sits in AwaitingDiscard; the SignalR
            // contract waits for AcknowledgeDealAsync before broadcasting the
            // private hand-tile payload. The autotable bundle has no ack route,
            // so we ack implicitly here. Idempotent + phase-aware: a no-op if
            // the state hasn't reached AwaitingDiscard (manual deal) or if the
            // seat already acked.
            await TryAutoAckSeatedConnectionAsync(connection, runtimeGameId, ct);
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
    private async Task<string> EnsureRuntimeBoundAsync(string relayGameId, string? hostPlayerId, CancellationToken ct, string? botDifficulty = null, int? seed = null, int? maxHands = null, AutotableConnection? resettingConnection = null)
    {
        if (_runtimeBinding.TryGetValue(relayGameId, out var existing)
            && !(resettingConnection is not null && string.Equals(relayGameId, AutotableWsEndpoint.DefaultGameId, StringComparison.Ordinal)))
        {
            // #121 (Lead decision) — first-creator-wins: a re-bind (late joiner / reconnect) must
            // NOT silently mutate the bound game's config. The bot difficulty (like seed/handCount)
            // is fixed by whoever created the game; a later client's `?botDifficulty=` is ignored.
            // (Pre-#121 this re-applied SetBotStrategyAsync on every re-bind, letting any late
            // joiner re-skin a live game's bots.)
            return existing;
        }

        await _bindingLock.WaitAsync(ct);
        try
        {
            if (_runtimeBinding.TryGetValue(relayGameId, out existing))
            {
                // #153 — stale default-game guard. The persistent DefaultGameId ("changsha-default")
                // is the fallback binding for bare `?variant=changsha` connections that carry no
                // explicit gameId. Once created it is reused forever (and rehydrated across a backend
                // restart), so a fresh browser that opens the bare URL after a previous match was
                // dealt / stalled / completed silently JOINs that leftover state — seat 0 still owned
                // by a departed player, the hand frozen mid-ceremony, zero progression (Hudson's
                // #153 diagnosis). When the caller is a seat-seeking newcomer (resettingConnection
                // set) we retire the stale default game and mint a fresh one so the newcomer gets a
                // clean, playable table. Preserved verbatim: a DELIBERATE reconnect (same persistent
                // playerId still owns a seat) reattaches; a still-joinable (Seating) or live game with
                // other connected players is kept; and every non-default (explicit `?gameId=`) game
                // keeps first-creator-wins + multi-human join semantics untouched.
                if (resettingConnection is not null
                    && string.Equals(relayGameId, AutotableWsEndpoint.DefaultGameId, StringComparison.Ordinal)
                    && ShouldRetireStaleDefault(existing, hostPlayerId, resettingConnection))
                {
                    _runtimeBinding.TryRemove(relayGameId, out _);
                    _relayBinding.TryRemove(existing, out _);
                    try
                    {
                        await _runtime.RemoveGameAsync(existing, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Retiring stale default runtime game {RuntimeGameId} failed; minting a fresh {DefaultGameId} anyway.",
                            existing, relayGameId);
                    }
                    // fall through to create a fresh default game below
                }
                else
                {
                    return existing;
                }
            }
            // botSeatIndexes = empty so the runtime starts with all-human seats;
            // we'll convert seats to bots on demand via FillEmptySeatsWithBotsAsync.
            // Ferro WP-E/#120 — pass the URL-supplied seed (null ⇒ runtime randomizes).
            var runtimeGameId = await _runtime.CreateGameAsync(
                seed: seed,
                botSeatIndexes: Array.Empty<int>(),
                hostPlayerId: hostPlayerId,
                hostConnectionId: null,
                ct,
                maxHands: maxHands);
            _runtimeBinding[relayGameId] = runtimeGameId;
            _relayBinding[runtimeGameId] = relayGameId;

            // Bishop W25 — bind the URL-supplied `?botDifficulty=` to the
            // freshly-created runtime game. Pre-W25 this was a silent
            // drop (the WS endpoint captured BotDifficulty into the
            // AutotableConnection but never forwarded it to the runtime,
            // so every game played at the Medium default regardless of
            // URL). Null / whitespace skips the call so the runtime
            // default (Medium) applies, matching pre-W25 behaviour for
            // clients that don't specify a difficulty. #121: this is now the
            // ONLY place botDifficulty is applied — first-creator-wins.
            if (!string.IsNullOrWhiteSpace(botDifficulty))
            {
                await _runtime.SetBotStrategyAsync(runtimeGameId, botDifficulty!, ct);
            }

            return runtimeGameId;
        }
        finally
        {
            _bindingLock.Release();
        }
    }

    /// <summary>
    /// #153 — decides whether the currently-bound <see cref="DefaultGameId"/> runtime game is stale
    /// and must be retired so a seat-seeking newcomer gets a fresh, playable table instead of silently
    /// inheriting leftover seat/turn state. Returns <c>true</c> only when ALL hold:
    /// <list type="bullet">
    ///   <item>the newcomer has a persistent identity (a seat-take, not a host-less spectator bind);</item>
    ///   <item>the existing default game has advanced past <see cref="ChangshaPhase.Seating"/> (it has
    ///   already dealt / is mid-hand / stalled / terminal — a Seating-phase game is still freshly
    ///   joinable and is never retired);</item>
    ///   <item>the newcomer is NOT already a seated participant — a deliberate reconnect (same
    ///   <see cref="ChangshaSeatState.PlayerId"/>) must reattach, never reset;</item>
    ///   <item>no OTHER live connection is still attached to the default game — a game with active
    ///   co-players is live, not abandoned, and is never torn out from under them.</item>
    /// </list>
    /// </summary>
    private bool ShouldRetireStaleDefault(string runtimeGameId, string? hostPlayerId, AutotableConnection resettingConnection)
    {
        // Only a seat-seeking human (durable identity present) may trigger a reset.
        if (string.IsNullOrEmpty(hostPlayerId)) return false;
        // Never retire a game that other clients are still actively connected to.
        if (ConnectionsInGame(AutotableWsEndpoint.DefaultGameId, except: resettingConnection.Id) > 0) return false;
        if (!_runtime.TryGetSnapshot(runtimeGameId, out var snap) || snap is null) return false;
        // A game still in Seating is freshly joinable — not stale.
        if (snap.Phase == ChangshaPhase.Seating) return false;
        // Deliberate reconnect: the returning player already owns a seat → keep the game.
        if (_runtime.TryGetSeatForPlayer(runtimeGameId, hostPlayerId) is not null) return false;
        // Newcomer meeting an already-started / stalled / terminal, abandoned default game → retire it.
        _logger.LogInformation(
            "Retiring stale default game (runtimeGameId={RuntimeGameId}, phase={Phase}) for new player {PlayerId}; minting a fresh {DefaultGameId}.",
            runtimeGameId, snap.Phase, hostPlayerId, AutotableWsEndpoint.DefaultGameId);
        return true;
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

    private async Task SendFullSnapshotAsync(AutotableConnection connection, string? gameId, CancellationToken ct, ChangshaGameState? capturedState = null)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            // No game bound — ship only the translator's match[0] override
            // so the bundle creates tiles with fives='000'.
            var translatorEntriesNoGame = ChangshaToAutotableTranslator.Translate(state: null,
                viewerSeat: connection.ViewerSeat, viewerPlayerId: connection.PlayerId,
                claimWindowTimeoutMs: _claimWindowTimeoutMs);
            var msg = new UpdateMessage { Entries = translatorEntriesNoGame.ToList(), Full = true };
            await SendJsonAsync(connection, msg, ct);
            return;
        }

        // #137 — StateChanged broadcasts pass the snapshot the runtime froze at the
        // mutation instant (capturedState); use it verbatim so a transient EndHand
        // result isn't lost to a later re-read. Initial-snapshot callers (connect /
        // seat) pass null and fall through to a fresh lock-protected copy.
        //
        // Vasquez integration audit A2/A3 — use TryGetSnapshotCopyAsync (lock-
        // protected JSON deep clone) instead of TryGetSnapshot (live reference)
        // so the translator iterates a stable graph. TryGetSnapshot returns
        // instance.State directly, which can be mutated mid-iteration by a
        // concurrent DrawTile / Discard / claim resolution running on a runtime
        // worker thread — producing a snapshot that drops the most-recent
        // discard from the wire view (the runtime keeps it, but the broadcast
        // does not) and gaslights the client into rendering a stale board.
        ChangshaGameState? runtimeState = capturedState;
        if (runtimeState is null && _runtimeBinding.TryGetValue(gameId, out var runtimeGameId))
        {
            runtimeState = await _runtime.TryGetSnapshotCopyAsync(runtimeGameId, ct);
        }

        var translatorEntries = ChangshaToAutotableTranslator.Translate(
            runtimeState,
            viewerSeat: connection.ViewerSeat,
            viewerPlayerId: connection.PlayerId,
            claimWindowTimeoutMs: _claimWindowTimeoutMs,
            privacy: ChangshaPrivacyProjector.Create(
                _opaqueHiddenHandles ? _handleProvider : null, gameId, connection.PlayerId));

        var gameState = _games.GetOrAdd(gameId, id => new AutotableGameState(id));

        // When a runtime game is backing the relay gameId, apply the translator
        // output with Runtime source so it OVERWRITES any client-pushed entries
        // for the same (collection, key). The viewer snapshot returned to this
        // connection is the merged result, then filtered for privacy.
        IReadOnlyList<CollectionEntry> snapshot;
        if (runtimeState is not null)
        {
            // ── Multi-viewer shared-store privacy (SC-2 endpoint leak fix) ─────────
            // Apone 2026-08-10, independently confirmed (Ripley/Vasquez drafts
            // unverified): `translatorEntries` is a PER-VIEWER projection — this
            // viewer's own concealed hand keeps real numeric tileIds, while every
            // tile HIDDEN from this viewer (foreign concealed hands, ALL wall tiles,
            // foreign concealed kongs) is keyed by an opaque per-viewer `h_` handle.
            // `AutotableGameState` is SHARED across every connection on this gameId
            // and `ApplyUpdate` merges `things` key-by-key (numeric tileId OR opaque
            // handle). Because different viewers key the SAME concealed slot
            // differently, persisting each viewer's projection makes the store
            // accumulate the UNION of everyone's keys (numeric + opaque duplicates),
            // and stale numeric keys never get tombstoned by another viewer. A later
            // viewer's snapshot is built from that store and only FACE-stripped by
            // FilterEntriesForViewer (which preserves the KEY) — so a spectator/foreign
            // seat receives the seated owner's real tileId KEYS, and the key alone
            // reconstructs identity (typeIndex = key/4). That is the :18084 leak.
            //
            // Fix: NEVER persist the per-viewer `things` into the shared store. Store
            // only the viewer-NEUTRAL runtime kinds (match/seats/nicks/dice/claim/
            // pickup/result/turn/gameComplete — none privacy-projected). Inbound client
            // `things` are already dropped in ChangshaRuntime mode (BE-2), so the store
            // legitimately holds no runtime `things`. This viewer's `things` are then
            // sourced straight from its own fresh translation for the outbound snapshot
            // only — so one viewer's keys can never enter another viewer's snapshot, and
            // no stale numeric/opaque key can accumulate across updates or reconnects.
            var neutralForStore = new List<CollectionEntry>(translatorEntries.Count);
            foreach (var entry in translatorEntries)
            {
                if (!string.Equals(entry.Kind, ThingsKind, StringComparison.Ordinal))
                    neutralForStore.Add(entry);
            }
            gameState.ApplyUpdate(neutralForStore, UpdateSource.Runtime);

            var stored = gameState.Snapshot();
            // Ephemeral kinds (claim, pickup, dice, sound, …) are deliberately
            // NOT stored by ApplyUpdate, so they would be missing from the
            // gameState snapshot even though the runtime just produced them.
            // Re-attach the latest translator output for any ephemeral kind so
            // the full snapshot we ship is actually full. `stored` carries the
            // viewer-neutral runtime kinds plus any client-owned relay entries and
            // — by construction — no runtime `things`.
            var withEphemerals = MergeRuntimeEphemerals(stored, translatorEntries, gameState);
            // Re-attach ONLY this viewer's own `things` projection. Because these
            // never touch the shared store, one viewer's keys can never leak into
            // another viewer's snapshot.
            snapshot = AttachViewerThings(withEphemerals, translatorEntries);
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
    /// Phase F gap-fix — when a Changsha runtime is backing the connection,
    /// <see cref="AutotableGameState.Snapshot"/> deliberately omits ephemeral
    /// kinds (claim, pickup, dice, sound, …) because <see cref="AutotableGameState.ApplyUpdate"/>
    /// never stored them. Without this merge the full-snapshot broadcast that
    /// fires on every <c>StateChanged</c> would silently drop the runtime's
    /// pickup affordance, leaving manual-deal clients unable to drive the
    /// take-tiles chain. Re-attach the latest translator output for any
    /// ephemeral kind so the snapshot we ship is actually full.
    ///
    /// <para>#132 — also forward the authoritative <c>result</c> tombstone.
    /// <c>result</c> is the only STORED (non-ephemeral) collection the runtime clears with a null
    /// value (the translator emits <c>result['current']=null</c> once the hand advances past
    /// <see cref="ChangshaPhase.EndHand"/>). <see cref="AutotableGameState.ApplyUpdate"/> already
    /// removed it from <paramref name="storedEntries"/>, but a full snapshot that merely OMITS the
    /// entry does not tell the bundle to hide <c>#result-modal</c> — the bundle hides it ONLY on an
    /// explicit <c>result['current']=null</c>. Re-attach that tombstone so the modal closes for every
    /// client (not just whoever advanced the hand) and does not re-open on hand-2+ broadcasts.</para>
    /// </summary>
    internal static IReadOnlyList<CollectionEntry> MergeRuntimeEphemerals(
        IReadOnlyList<CollectionEntry> storedEntries,
        IReadOnlyList<CollectionEntry> translatorEntries,
        AutotableGameState gameState)
    {
        var merged = new List<CollectionEntry>(storedEntries.Count + translatorEntries.Count);
        merged.AddRange(storedEntries);
        foreach (var entry in translatorEntries)
        {
            var isNull = entry.Value is null
                || (entry.Value is JsonElement je && je.ValueKind == JsonValueKind.Null);

            if (gameState.IsEphemeral(entry.Kind))
            {
                // Ephemeral kinds were never stored; re-attach the live (non-null) value only.
                // BE-4 (Ripley §9.1 / RC-6) + R-1 E3 (Vasquez) exceptions: forward an explicit
                // `claim` OR `pickup` tombstone (null) so the bundle clears the cached claim
                // overlay / the sticky pickup cursor — an omitted slice leaves a stale 碰/吃/杠/胡
                // window or keeps isMyPickupTurn() TRUE (wall wrongly interactive) after the deal.
                if (!isNull) merged.Add(entry);
                else if (entry.Kind == ChangshaCollectionKinds.Claim
                         || entry.Kind == ChangshaCollectionKinds.Pickup) merged.Add(entry);
            }
            else if (isNull && entry.Kind == ChangshaCollectionKinds.Result)
            {
                // #132 — forward the explicit result tombstone (removed from `stored` by
                // ApplyUpdate) so the bundle hides #result-modal. At EndHand `result` is non-null
                // and already present via `stored`, so this never double-emits the populated result.
                merged.Add(entry);
            }
        }
        return merged;
    }

    /// <summary>Upstream collection name for scene tiles. The only translator collection
    /// whose KEYS are per-viewer projected (real tileId when visible to the viewer, opaque
    /// <c>h_</c> handle when hidden) — hence the only one that must never enter the shared
    /// cross-connection store (see <see cref="SendFullSnapshotAsync"/>).</summary>
    private const string ThingsKind = "things";

    /// <summary>
    /// Multi-viewer privacy (SC-2 endpoint leak fix) — builds the outbound snapshot's
    /// <c>things</c> from THIS connection's own per-viewer projection only.
    /// <paramref name="baseEntries"/> (the shared-store snapshot + re-attached runtime
    /// ephemerals) is stripped of any <c>things</c> so a foreign viewer's / the seated
    /// owner's real tileId keys can never ride along; <paramref name="viewerEntries"/>
    /// then contributes exactly this viewer's <c>things</c>. The canonical store never
    /// holds a per-viewer <c>things</c> projection, so no numeric real-id key is ever
    /// shipped to a viewer not entitled to it, and no stale numeric/opaque key can
    /// accumulate across updates or reconnects.
    /// </summary>
    private static IReadOnlyList<CollectionEntry> AttachViewerThings(
        IReadOnlyList<CollectionEntry> baseEntries,
        IReadOnlyList<CollectionEntry> viewerEntries)
    {
        var merged = new List<CollectionEntry>(baseEntries.Count + viewerEntries.Count);
        foreach (var e in baseEntries)
        {
            if (!string.Equals(e.Kind, ThingsKind, StringComparison.Ordinal))
                merged.Add(e);
        }
        foreach (var e in viewerEntries)
        {
            if (string.Equals(e.Kind, ThingsKind, StringComparison.Ordinal))
                merged.Add(e);
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
    private void OnStateChanged(string runtimeGameId, ChangshaGameState snapshot)
    {
        // #137 — deliver the snapshot the runtime captured AT THE MOMENT OF THE
        // MUTATION (see ChangshaGameRuntime.PersistSnapshotAsync), never a later
        // re-read. Enqueue it per-connection and drain FIFO through a single
        // drainer so ordering is preserved: the transient EndHand result must land
        // BEFORE the next hand's RollingDice tombstone, or the client's hand-end
        // observer would collapse two hand-ends into one null→present flip.
        if (!_relayBinding.TryGetValue(runtimeGameId, out var relayGameId)) return;
        foreach (var connection in _connections.Values)
        {
            if (!string.Equals(connection.GameId, relayGameId, StringComparison.Ordinal)) continue;
            connection.BroadcastQueue.Enqueue(snapshot);
            _ = DrainBroadcastQueueAsync(connection, relayGameId);
        }
    }

    /// <summary>
    /// #137 — single-drainer per connection. Broadcasts are fire-and-forget from
    /// the runtime thread, but WS sends for one connection must stay strictly
    /// ordered (and never overlap — a concurrent WebSocket.SendAsync throws). The
    /// <c>Interlocked</c> gate guarantees exactly one active drainer; the FIFO
    /// <see cref="AutotableConnection.BroadcastQueue"/> preserves enqueue order,
    /// which equals the runtime's mutation order (StateChanged fires synchronously
    /// under the per-game lock). The re-check after releasing the gate closes the
    /// enqueue-after-drain race.
    /// </summary>
    private async Task DrainBroadcastQueueAsync(AutotableConnection connection, string gameId)
    {
        if (Interlocked.CompareExchange(ref connection.BroadcastDrainerActive, 1, 0) != 0) return;
        try
        {
            while (connection.BroadcastQueue.TryDequeue(out var snapshot))
            {
                try
                {
                    await SendFullSnapshotAsync(connection, gameId, CancellationToken.None, snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to broadcast snapshot to connection {ConnectionId}", connection.Id);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref connection.BroadcastDrainerActive, 0);
        }
        // An item may have been enqueued between our last dequeue and the reset.
        if (!connection.BroadcastQueue.IsEmpty)
            _ = DrainBroadcastQueueAsync(connection, gameId);
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
    /// <summary>
    /// The seat this connection renders as (own hand face-up, others face-down / opaque).
    /// MUTABLE and bound EXCLUSIVELY from runtime-confirmed ownership — BE-5 (Ripley
    /// §9.1/§11.1) rebinds it on a successful <c>TakeSeat</c> and on reconnect owner
    /// inference. Blocker D (Bishop rev2) — the raw <c>?seat=</c> query no longer seeds this
    /// (that let an unowned requester project a foreign concealed hand); an unseated
    /// connection stays null (spectator/opaque) until it actually owns a seat.
    /// </summary>
    public int? ViewerSeat { get; set; }
    /// <summary>
    /// Blocker D (Bishop rev2) — the raw <c>?seat=N</c> query value captured as a
    /// NON-AUTHORITATIVE hint. Never consulted for per-viewer projection (see
    /// <see cref="ViewerSeat"/>); retained only for telemetry / potential future auto-seat
    /// UX. Null when the connection supplied no numeric seat (or the spectator sentinel).
    /// </summary>
    public int? RequestedSeat { get; init; }
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
    /// #137 — per-connection ordered broadcast queue. The runtime hands the WS
    /// endpoint the snapshot it froze at each mutation (StateChanged); those are
    /// enqueued here and drained FIFO by a single drainer (see
    /// <c>AutotableConnectionManager.DrainBroadcastQueueAsync</c>) so per-hand
    /// results are delivered in mutation order and never overlap on the socket.
    /// </summary>
    public ConcurrentQueue<ChangshaGameState> BroadcastQueue { get; } = new();

    /// <summary>#137 — 0/1 Interlocked gate ensuring exactly one active broadcast drainer.</summary>
    public int BroadcastDrainerActive;

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
    /// #121/#130/C-2 (Lead + canonical §6.3) — optional match hand cap from the lobby
    /// <c>?handCount=</c> WS query param. Canonical Changsha caps at 16 hands (§6.3): accepted
    /// create-time values are {1,4,8,16}; a legacy/requested <c>32</c> is normalized to the
    /// authoritative <c>16</c>; any other/absent value ⇒ the runtime default (4). Threaded to
    /// <see cref="IChangshaGameRuntime.CreateGameAsync"/> at first runtime bind (first-creator-wins),
    /// so a late joiner's differing <c>handCount</c> never re-caps a bound game.
    /// </summary>
    public int MaxHands { get; init; } = 4;

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
    /// Ferro WP-E/#120 — optional deterministic RNG seed from the <c>?seed=</c>
    /// WS query param (0..<see cref="int.MaxValue"/>). Threaded to
    /// <see cref="IChangshaGameRuntime.CreateGameAsync"/> at first runtime bind so
    /// a <c>?seed=</c> URL reproduces the same wall shuffle + dice. <c>null</c> ⇒
    /// the runtime picks a random seed (the default for every game that doesn't
    /// pin one), so seedless games are unchanged.
    /// </summary>
    public int? Seed { get; init; }

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
