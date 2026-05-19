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
/// <para><b>Phase C-relay (this layer):</b> bidirectional bundle ↔ bundle
/// multiplayer pipe. <c>UPDATE</c> messages from one connection are stored in
/// the per-game <see cref="AutotableGameState"/> and broadcast to every
/// <i>other</i> connection sharing the same <c>gameId</c>. A late joiner
/// receives the accumulated snapshot on <c>JOINED</c>. No rules enforcement
/// — that's Phase D-backend.</para>
///
/// <para><b>Phase D-backend (next, not in this file yet):</b> the Changsha
/// runtime will drive authoritative state changes that get merged into the
/// per-game collections and broadcast to every connection. The translator-fed
/// <see cref="ChangshaToAutotableTranslator"/> snapshot path is preserved
/// for that integration — currently it contributes the <c>match[0]</c> entry
/// (forcing <c>fives='000'</c>) plus, when a Changsha game is bound, the full
/// table snapshot, which is merged into the relay state on <c>JOIN</c>.</para>
///
/// <para><b>Always-available pattern (spike §3.6):</b> if a JOIN names a
/// gameId not bound to any Changsha game, the endpoint still responds with
/// <c>JOINED</c> + an empty <c>UPDATE</c> so the bundle's 15× auto-reconnect
/// loop stays quiet. Phase C-relay extends this by creating an empty per-game
/// state on demand, allowing subsequent UPDATE relays to proceed normally.</para>
/// </summary>
public static class AutotableWsEndpoint
{
    public const string Path = "/autotable/ws";

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

    public async Task HandleConnectionAsync(WebSocket ws, IQueryCollection query, CancellationToken serverShutdown)
    {
        var queryGameId = query.TryGetValue("gameId", out var g) ? g.ToString() : null;
        int? viewerSeat = null;
        if (query.TryGetValue("seat", out var s) && int.TryParse(s.ToString(), out var parsedSeat) && parsedSeat is >= 0 and <= 3)
        {
            viewerSeat = parsedSeat;
        }

        var connection = new AutotableConnection(ws, queryGameId, viewerSeat);
        _connections[connection.Id] = connection;
        _logger.LogInformation(
            "Autotable WS connected (connectionId={ConnectionId}, gameId={GameId}, seat={Seat})",
            connection.Id, queryGameId, viewerSeat);

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
        // Upstream behavior (server.ts): allocate a fresh gameId not already
        // in use and join the client to a brand new Game. Phase C-relay
        // mirrors that — we own the gameId namespace and the game is empty
        // until the first bundle uploads its sendOnConnect entries.
        string gameId;
        do
        {
            gameId = RandomGameId();
        } while (_games.ContainsKey(gameId));

        var state = _games.GetOrAdd(gameId, id => new AutotableGameState(id));
        connection.GameId = gameId;

        // NEW always makes this connection the first joiner of a fresh game,
        // which triggers the bundle's sendOnConnect path (uploads the initial
        // wall of 136 things + the match conditions).
        var isFirst = ReferenceEquals(state, _games[gameId]) && ConnectionsInGame(gameId, except: connection.Id) == 0;
        await SendJoinedAsync(connection, gameId, isFirst, ct);
        await SendFullSnapshotAsync(connection, gameId, ct);
    }

    private async Task HandleJoinAsync(AutotableConnection connection, string? gameId, CancellationToken ct)
    {
        // Bundle's gameId from JOIN takes precedence over the query param so
        // a stale URL doesn't override a fresh React-driven JOIN.
        var resolved = !string.IsNullOrEmpty(gameId) ? gameId : connection.GameId;
        if (string.IsNullOrEmpty(resolved))
        {
            resolved = RandomGameId();
        }

        // Track whether the game state existed before we touched it. If it
        // did not, this connection is the first joiner and gets isFirst=true
        // — that's the signal upstream <c>Collection.onConnect</c> uses to
        // upload meta-collections (unique / ephemeral / perPlayer) and the
        // initial sendOnConnect payload. Without it, late joiners would see
        // an empty table forever.
        var existedBefore = _games.ContainsKey(resolved);
        var state = _games.GetOrAdd(resolved, id => new AutotableGameState(id));
        connection.GameId = resolved;

        // First-joiner check is "no other connections bound to this gameId."
        // This is more robust than a one-shot "starting" flag because if a
        // game was created by HandleNewAsync but its sole connection dropped
        // before any state was uploaded, the next JOIN should still be
        // treated as the first joiner (state is empty).
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
        var applied = state.ApplyUpdate(entries);
        if (applied.Count == 0) return;

        await BroadcastToOthersAsync(connection, applied, full: false, ct);
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
        // Translator output is always emitted (even for "no Changsha game" —
        // it still ships the match[0] entry that forces fives='000'). When a
        // Changsha game IS bound, the translator's per-viewer perspective
        // wins over any stored bundle state by being applied to the game
        // store first; the resulting merged snapshot is what we send.
        ChangshaGameState? state = null;
        if (!string.IsNullOrEmpty(gameId))
        {
            _runtime.TryGetSnapshot(gameId, out state);
        }

        var translatorEntries = ChangshaToAutotableTranslator.Translate(
            state,
            viewerSeat: connection.ViewerSeat,
            viewerPlayerId: connection.PlayerId);

        // Merge translator output INTO the per-game store so it's visible to
        // every future joiner, then dump the full merged snapshot.
        IReadOnlyList<CollectionEntry> snapshot;
        if (!string.IsNullOrEmpty(gameId))
        {
            var gameState = _games.GetOrAdd(gameId, id => new AutotableGameState(id));
            // Only apply translator entries when there's a backing Changsha
            // game — otherwise we'd persist the match[0] override into every
            // ad-hoc bundle game and clobber the bundle's own match entry on
            // late joins. The match[0] from translator IS still sent in the
            // outbound update via the union below.
            if (state is not null)
            {
                gameState.ApplyUpdate(translatorEntries);
                snapshot = gameState.Snapshot();
            }
            else
            {
                var stored = gameState.Snapshot();
                snapshot = MergeSnapshots(translatorEntries, stored);
            }
        }
        else
        {
            snapshot = translatorEntries;
        }

        var update = new UpdateMessage
        {
            Entries = snapshot.ToList(),
            Full = true
        };
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

    private async Task BroadcastToOthersAsync(
        AutotableConnection sender,
        IReadOnlyList<CollectionEntry> entries,
        bool full,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sender.GameId) || entries.Count == 0) return;

        var message = new UpdateMessage
        {
            Entries = entries.ToList(),
            Full = full
        };

        foreach (var peer in _connections.Values)
        {
            if (peer.Id == sender.Id) continue;
            if (!string.Equals(peer.GameId, sender.GameId, StringComparison.Ordinal)) continue;
            try
            {
                await SendJsonAsync(peer, message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast UPDATE to peer {PeerId}", peer.Id);
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
    private void OnStateChanged(string gameId)
    {
        // Fire-and-forget broadcast. State events arrive on runtime worker
        // threads — we don't want to block them on WS sends.
        foreach (var connection in _connections.Values)
        {
            if (!string.Equals(connection.GameId, gameId, StringComparison.Ordinal)) continue;
            _ = BroadcastSnapshotAsync(connection, gameId);
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

    private static readonly char[] GameIdChars = "0123456789ABCDEFGJKLMNPQRSTUVWXYZ".ToCharArray();

    private static string RandomGameId()
    {
        var chars = new char[5];
        for (var i = 0; i < 5; i++)
        {
            chars[i] = GameIdChars[Random.Shared.Next(GameIdChars.Length)];
        }
        return new string(chars);
    }

    public void Dispose()
    {
        _runtime.StateChanged -= _stateChangedHandler;
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

    public AutotableConnection(WebSocket socket, string? gameId, int? viewerSeat)
    {
        Socket = socket;
        GameId = gameId;
        ViewerSeat = viewerSeat;
    }
}
