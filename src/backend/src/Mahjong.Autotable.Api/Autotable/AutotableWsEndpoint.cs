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
/// The byte-identical <c>autotable.9519e86d.js</c> bundle connects here unchanged.
///
/// <para><b>Path:</b> <c>/autotable/ws</c> — verified against upstream
/// <c>client-ui.ts:getUrl()</c>:
/// <c>path.substring(1, path.lastIndexOf('/')+1) + 'ws'</c> with
/// <c>window.location.pathname = '/autotable/'</c> resolves to
/// <c>autotable/ws</c> (Default #7, Stephen accepted).</para>
///
/// <para><b>Phase 5a scope:</b> server → bundle only. Bundle-initiated UPDATE
/// messages (mouse moves, drag events) are logged at Debug and discarded.
/// Phase 5b will translate those into Changsha hub commands.</para>
///
/// <para><b>Always-available pattern (spike §3.6):</b> if a JOIN names a
/// gameId not bound to any Changsha game, the endpoint still responds with
/// <c>JOINED</c> + an empty <c>UPDATE</c> so the bundle's 15× auto-reconnect
/// loop stays quiet.</para>
///
/// <para><b>Single-game-per-instance (Default #8):</b> connection routing
/// uses the WS query string <c>?gameId=X&amp;seat=N</c>. <c>seat</c> is
/// optional and selects the viewer perspective for hand visibility.</para>
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
/// Tracks live WS connections and routes Changsha runtime state-change events
/// to the right bundles. Registered as a singleton.
/// </summary>
public sealed class AutotableConnectionManager : IDisposable
{
    private readonly IChangshaGameRuntime _runtime;
    private readonly ILogger<AutotableConnectionManager> _logger;
    private readonly ConcurrentDictionary<Guid, AutotableConnection> _connections = new();
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
            _connections.TryRemove(connection.Id, out _);
            _logger.LogInformation("Autotable WS disconnected (connectionId={ConnectionId})", connection.Id);
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
                // Phase 5a is one-way (server → bundle). Bundle-initiated
                // canvas mutations (drags, mouse moves, take-seat clicks)
                // are discarded; Phase 5b will translate them into Changsha
                // hub commands. Log only at Debug so the channel stays clean.
                _logger.LogDebug(
                    "Discarded bundle UPDATE (connectionId={ConnectionId}, entries={Count})",
                    connection.Id, message.Entries?.Count ?? 0);
                break;
            default:
                _logger.LogDebug("Unknown autotable message type {Type}", message.Type);
                break;
        }
    }

    private async Task HandleNewAsync(AutotableConnection connection, CancellationToken ct)
    {
        // Upstream behavior: server allocates a new 5-char gameId and joins
        // the client to it. For Phase 5a we don't create a Changsha game on
        // demand — we just allocate a synthetic gameId and respond with an
        // empty snapshot (always-available pattern). The bundle will then
        // sit at the empty-table state until JOIN is re-issued with our
        // real gameId from the React parent.
        var gameId = RandomGameId();
        connection.GameId = gameId;
        await SendJoinedAsync(connection, gameId, isFirst: false, ct);
        await SendFullSnapshotAsync(connection, gameId: null, ct);
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

        connection.GameId = resolved;
        await SendJoinedAsync(connection, resolved, isFirst: false, ct);
        await SendFullSnapshotAsync(connection, resolved, ct);
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
        ChangshaGameState? state = null;
        if (!string.IsNullOrEmpty(gameId))
        {
            _runtime.TryGetSnapshot(gameId, out state);
        }

        var entries = ChangshaToAutotableTranslator.Translate(
            state,
            viewerSeat: connection.ViewerSeat,
            viewerPlayerId: connection.PlayerId);

        var update = new UpdateMessage
        {
            Entries = entries.ToList(),
            Full = true
        };
        await SendJsonAsync(connection, update, ct);
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
