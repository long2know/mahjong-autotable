using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;

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
/// <para><b>Single-game-per-instance (Default #8):</b> all NEW/JOIN messages
/// resolve to the deterministic relay gameId <c>"changsha-default"</c>, which is
/// lazily bound to one Changsha runtime game. Hicks's Phase D-frontend "Take Seat"
/// click sends a <c>seats</c> UPDATE that this endpoint routes to
/// <see cref="IChangshaGameRuntime.TakeSeatAsync"/>, optionally followed by
/// <see cref="IChangshaGameRuntime.FillEmptySeatsWithBotsAsync"/> for solo play
/// (query param <c>?bots=true</c>, default ON).</para>
/// </summary>
public static class AutotableWsEndpoint
{
    public const string Path = "/autotable/ws";
    /// <summary>
    /// Deterministic single-game-per-instance relay gameId (Default #8). All inbound
    /// NEW/JOIN/UPDATE messages resolve to this gameId — Phase E will widen.
    /// </summary>
    public const string DefaultGameId = "changsha-default";

    /// <summary>Maps the autotable WS handler onto the application pipeline.</summary>
    public static IEndpointConventionBuilder MapAutotableWs(this IEndpointRouteBuilder endpoints) =>
        endpoints.Map(Path, async (HttpContext context, AutotableConnectionManager manager) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected a WebSocket request.");
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await manager.HandleConnectionAsync(ws, context.Request.Query, context.RequestAborted);
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

    public async Task HandleConnectionAsync(WebSocket ws, IQueryCollection query, CancellationToken serverShutdown)
    {
        // Phase D-backend: single-game-per-instance. Ignore any client-supplied
        // gameId; everyone joins the same default game. Bots default to ON for
        // solo MVP play — Stephen can disable via ?bots=false.
        var queryGameId = query.TryGetValue("gameId", out var g) ? g.ToString() : null;
        int? viewerSeat = null;
        if (query.TryGetValue("seat", out var s) && int.TryParse(s.ToString(), out var parsedSeat) && parsedSeat is >= 0 and <= 3)
        {
            viewerSeat = parsedSeat;
        }
        var autoBotFill = !query.TryGetValue("bots", out var b) || !string.Equals(b.ToString(), "false", StringComparison.OrdinalIgnoreCase);

        var connection = new AutotableConnection(ws, queryGameId, viewerSeat) { AutoBotFill = autoBotFill };
        _connections[connection.Id] = connection;
        _logger.LogInformation(
            "Autotable WS connected (connectionId={ConnectionId}, gameId={GameId}, seat={Seat}, bots={Bots})",
            connection.Id, queryGameId, viewerSeat, autoBotFill);

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
        // Phase D-backend single-game default: NEW and JOIN both resolve to the
        // single default gameId. Bundle's NEW path triggers sendOnConnect (the
        // meta-collection declarations) which is still useful.
        var gameId = AutotableWsEndpoint.DefaultGameId;
        var state = _games.GetOrAdd(gameId, id => new AutotableGameState(id));
        connection.GameId = gameId;

        var isFirst = ReferenceEquals(state, _games[gameId])
            && ConnectionsInGame(gameId, except: connection.Id) == 0
            && state.Snapshot().Count == 0;
        await SendJoinedAsync(connection, gameId, isFirst, ct);
        await SendFullSnapshotAsync(connection, gameId, ct);
    }

    private async Task HandleJoinAsync(AutotableConnection connection, string? gameId, CancellationToken ct)
    {
        // Phase D-backend: ignore client-supplied gameId; force the single default
        // game. Future Phase E will widen to lobby-allocated multi-game ids.
        var resolved = AutotableWsEndpoint.DefaultGameId;

        var existedBefore = _games.ContainsKey(resolved);
        var state = _games.GetOrAdd(resolved, id => new AutotableGameState(id));
        connection.GameId = resolved;

        var others = ConnectionsInGame(resolved, except: connection.Id);
        var isFirst = !existedBefore || (others == 0 && state.Snapshot().Count == 0);
        await SendJoinedAsync(connection, resolved, isFirst, ct);
        await SendFullSnapshotAsync(connection, resolved, ct);
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

        try
        {
            var runtimeGameId = await EnsureRuntimeBoundAsync(connection.GameId!, ct);
            await _runtime.TakeSeatAsync(runtimeGameId, connection.PlayerId, seatIndex, ct);

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
    /// Lazily binds <paramref name="relayGameId"/> to a Changsha runtime game.
    /// Idempotent: subsequent calls return the same runtime gameId.
    /// </summary>
    private async Task<string> EnsureRuntimeBoundAsync(string relayGameId, CancellationToken ct)
    {
        if (_runtimeBinding.TryGetValue(relayGameId, out var existing)) return existing;

        await _bindingLock.WaitAsync(ct);
        try
        {
            if (_runtimeBinding.TryGetValue(relayGameId, out existing)) return existing;
            // botSeatIndexes = empty so the runtime starts with all-human seats;
            // we'll convert seats to bots on demand via FillEmptySeatsWithBotsAsync.
            var runtimeGameId = await _runtime.CreateGameAsync(seed: null, botSeatIndexes: Array.Empty<int>(), hostConnectionId: null, ct);
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
    /// Per-viewer privacy filter (Phase D-backend §3 / Ripley pivot decision #6).
    /// For every <c>things</c> entry whose <c>slotName</c> places the tile in another
    /// seat's hand, override <c>rotationIndex</c> to face-down and strip face data.
    /// Open melds, discards, and the wall stay as the translator emitted them
    /// (the wall is already face-down; discards/melds are public). The viewer's
    /// own hand is unaffected.
    /// <para>Note: in v1 the bundle's thing-index encodes typeIndex (face) intrinsically
    /// because we lock conditions.fives='000' for a clean 1:1 mapping. The filter
    /// strips the explicit face field and forces face-down rendering; v2 will
    /// shuffle physical tile-ids so the index itself reveals nothing.</para>
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
            // hand.<seat>@<index> — concealed-hand slots are the privacy target.
            // wall.* and discard.* and meld.* are publicly visible.
            if (!slotName.StartsWith("hand.", StringComparison.Ordinal))
            {
                filtered.Add(entry);
                continue;
            }

            // Extract the seat index from "hand.<seat>@<index>".
            var dot = slotName.IndexOf('.');
            var at = slotName.IndexOf('@');
            if (dot < 0 || at <= dot + 1)
            {
                filtered.Add(entry);
                continue;
            }
            if (!int.TryParse(slotName.AsSpan(dot + 1, at - dot - 1), out var slotSeat))
            {
                filtered.Add(entry);
                continue;
            }

            if (viewerSeat.HasValue && slotSeat == viewerSeat.Value)
            {
                // Viewer's own hand — keep face-up.
                filtered.Add(entry);
                continue;
            }

            // Other seat's hand — force face-down rotation + strip face.
            filtered.Add(new CollectionEntry(entry.Kind, entry.Key,
                StripFaceAndForceFaceDown(je)));
        }
        return filtered;
    }

    private static JsonElement StripFaceAndForceFaceDown(JsonElement original)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.NameEquals("face")) continue;
                if (prop.NameEquals("rotationIndex"))
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

/// <summary>Single bundle connection — one per (WebSocket, gameId, viewerSeat).</summary>
public sealed class AutotableConnection
{
    public Guid Id { get; } = Guid.NewGuid();
    public WebSocket Socket { get; }
    public string? GameId { get; set; }
    public int? ViewerSeat { get; }
    public string PlayerId { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    /// <summary>
    /// When true, taking a seat triggers auto-fill of remaining seats with bots
    /// (Phase D-backend §7). Bundle clients default to true via the <c>?bots=true</c>
    /// query param; the E2E test can disable it for deterministic seat-take tests.
    /// </summary>
    public bool AutoBotFill { get; init; } = true;

    public AutotableConnection(WebSocket socket, string? gameId, int? viewerSeat)
    {
        Socket = socket;
        GameId = gameId;
        ViewerSeat = viewerSeat;
    }
}
