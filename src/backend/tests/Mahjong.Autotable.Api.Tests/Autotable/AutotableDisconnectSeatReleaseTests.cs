using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Autotable;

/// <summary>
/// Phase J Wave 2 — autotable disconnect seat-release tests (Vasquez).
///
/// <para>Bishop's Phase J Wave 2 task pins the bug surfaced in the Wave 1
/// hot-seat swap memo: <see cref="AutotableConnectionManager.HandleConnectionAsync"/>
/// terminates a connection via <c>HandleDisconnectAsync</c> which clears the
/// per-game relay store entries but does NOT call
/// <see cref="IChangshaGameRuntime.HandleDisconnectAsync"/>, so the runtime's
/// <c>SeatConnections</c> binding for that connection survives orphaned.
/// Effect: a fresh WS connection cannot retake the seat the disconnected
/// player held, even though the bundle UI presents the seat as "available".</para>
///
/// <para><b>What these tests pin</b> (post-fix):
/// <list type="bullet">
///   <item>An active-seat WS close releases the runtime seat binding so a new
///     connection can take that exact seat.</item>
///   <item>A spectator (<c>?seat=-1</c>) close performs no runtime mutation —
///     no exception, no spurious bot fill, no disturbance to other seat
///     bindings.</item>
///   <item>A disconnect → reconnect on the same seat by a fresh connection
///     ends with the seat bound to the new connection.</item>
/// </list>
/// </para>
///
/// <para><b>Observability strategy.</b> The runtime's <c>SeatConnections</c>
/// dictionary is not exposed on <see cref="IChangshaGameRuntime"/>, so we
/// observe seat release indirectly: after a disconnect, an independent
/// <see cref="IChangshaGameRuntime.TakeSeatAsync"/> call with a fresh
/// connectionId must succeed (current behaviour throws "Seat N is already
/// taken"). The <see cref="ChangshaGameState.Seats"/> binding's
/// <c>PlayerId</c> is observed via <see cref="IChangshaGameRuntime.TryGetSnapshot"/>
/// to confirm the new connection now owns the seat.</para>
/// </summary>
public class AutotableDisconnectSeatReleaseTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-disconnect-{Guid.NewGuid():N}.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
            b.ConfigureServices(s =>
            {
                s.Configure<ChangshaRuntimeOptions>(o =>
                {
                    o.BotTurnDelayMs = 1;
                    o.BotClaimDelayMs = 1;
                    o.ClaimWindowTimeoutMs = 50;
                    o.DealBatchDelayMs = 0;
                    o.PersistSnapshots = false;
                });
            });
        });
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        try { if (_tempDb is not null && File.Exists(_tempDb)) File.Delete(_tempDb); } catch { }
        return Task.CompletedTask;
    }

    // ────────────────────────────────────────────────────────────────────
    //  1. Active-seat disconnect releases the runtime binding
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Phase-J-2"), Trait("Wave", "Phase-J-2")]
    public async Task Disconnect_OfActiveSeat_ReleasesRuntimeBinding()
    {
        const string gameId = "DISCONNECT-RELEASE";
        var manager = _factory!.Services.GetRequiredService<AutotableConnectionManager>();
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();

        // Alice joins as seat 0 and takes seat 0 (binds the runtime).
        var alice = await OpenAndJoinAsync(seat: 0, gameId: gameId);
        await alice.TakeSeatAsync(0);

        var aliceSeated = await WaitForAsync(() =>
        {
            var rid = manager.GetRuntimeGameIdBoundTo(gameId);
            if (string.IsNullOrEmpty(rid)) return false;
            if (!runtime.TryGetSnapshot(rid!, out var s) || s is null) return false;
            return string.Equals(s.Seats[0].PlayerId, alice.PlayerId, StringComparison.Ordinal)
                && !s.Seats[0].IsBot;
        }, timeoutMs: 2000);
        Assert.True(aliceSeated,
            "Alice's seat-take should bind state.Seats[0].PlayerId to her connectionId.");

        var runtimeGameId = manager.GetRuntimeGameIdBoundTo(gameId)!;

        // Close Alice's WS — server's disconnect handler must propagate the
        // release into the Changsha runtime under Bishop's Phase J Wave 2 fix.
        await alice.DisposeAsync();

        // Probe release indirectly: a fresh connectionId tries to take seat 0
        // directly through the runtime API. Currently throws "Seat 0 is
        // already taken"; post-fix this should succeed because the seat
        // binding for Alice's connectionId has been removed.
        var probeConnectionId = $"probe-{Guid.NewGuid():N}".Substring(0, 12);

        var releaseObserved = await WaitForAsync(() =>
        {
            try
            {
                runtime.TakeSeatAsync(runtimeGameId, probeConnectionId, 0, CancellationToken.None)
                    .GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                return false;
            }
        }, timeoutMs: 3000);

        Assert.True(releaseObserved,
            "Bishop's Phase J Wave 2 fix should release the runtime seat-0 binding when " +
            "Alice's autotable WS closes, so a fresh connectionId can take seat 0. " +
            "If this assertion fails the seat-release wiring from " +
            "AutotableConnectionManager.HandleDisconnectAsync into " +
            "IChangshaGameRuntime.HandleDisconnectAsync is missing or incorrect.");

        // And the new connectionId is now the seat-0 owner in the snapshot.
        Assert.True(runtime.TryGetSnapshot(runtimeGameId, out var postState));
        Assert.NotNull(postState);
        Assert.Equal(probeConnectionId, postState!.Seats[0].PlayerId);
        Assert.False(postState.Seats[0].IsBot);
    }

    // ────────────────────────────────────────────────────────────────────
    //  2. Spectator disconnect is a no-op
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Phase-J-2"), Trait("Wave", "Phase-J-2")]
    public async Task Disconnect_OfSpectator_IsNoOp()
    {
        const string gameId = "DISCONNECT-SPECTATOR";
        var manager = _factory!.Services.GetRequiredService<AutotableConnectionManager>();
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();

        // Alice claims seat 0 first so we have a known-bound seat to verify
        // is left undisturbed by the spectator's later disconnect.
        var alice = await OpenAndJoinAsync(seat: 0, gameId: gameId);
        await alice.TakeSeatAsync(0);
        var aliceSeated = await WaitForAsync(() =>
        {
            var rid = manager.GetRuntimeGameIdBoundTo(gameId);
            return !string.IsNullOrEmpty(rid)
                && runtime.TryGetSnapshot(rid!, out var s) && s is not null
                && string.Equals(s.Seats[0].PlayerId, alice.PlayerId, StringComparison.Ordinal);
        }, timeoutMs: 2000);
        Assert.True(aliceSeated, "Alice's seat-0 take should be observable in runtime state.");
        var runtimeGameId = manager.GetRuntimeGameIdBoundTo(gameId)!;

        // Capture the pre-disconnect snapshot of seat 0 to compare later.
        Assert.True(runtime.TryGetSnapshot(runtimeGameId, out var preState));
        var preAlicePlayerId = preState!.Seats[0].PlayerId;
        var preAliceIsBot = preState.Seats[0].IsBot;

        // Watcher joins as spectator (?seat=-1). Spectators never bind a seat
        // and never call TakeSeatAsync; the bundle's spectator path omits the
        // seats UPDATE entirely.
        var watcher = await OpenAndJoinAsync(seat: -1, gameId: gameId);

        // Force-close the spectator's socket. Under Bishop's Wave 2 fix the
        // backend should fast-path the disconnect (no seat-release attempt
        // because no seat was ever bound), throw nothing, and leave the
        // runtime state untouched.
        await watcher.DisposeAsync();

        // Give the disconnect handler enough time to run. We intentionally
        // probe AFTER a delay rather than waiting for a release signal —
        // there shouldn't be one.
        await Task.Delay(250);

        // Alice's seat-0 binding survives the spectator's close, byte-for-byte.
        Assert.True(runtime.TryGetSnapshot(runtimeGameId, out var postState));
        Assert.NotNull(postState);
        Assert.Equal(preAlicePlayerId, postState!.Seats[0].PlayerId);
        Assert.Equal(preAliceIsBot, postState.Seats[0].IsBot);
        Assert.Equal(alice.PlayerId, postState.Seats[0].PlayerId);

        // The spectator's playerId never appears in any seat slot.
        for (var i = 0; i < 4; i++)
            Assert.NotEqual(watcher.PlayerId, postState.Seats[i].PlayerId);

        // No spurious bot fill — the other 3 seats are still in whatever state
        // Alice's seat-take + AutoBotFill left them. We don't assert a specific
        // shape (bot vs empty) because the AutoBotFill behaviour is not the
        // contract under test; we just assert seat-0 is undisturbed.

        // Sanity that Alice's WS is still alive — the spectator's disconnect
        // must not have triggered a cascading teardown.
        Assert.Equal(WebSocketState.Open, alice.SocketState);

        await alice.DisposeAsync();
    }

    // ────────────────────────────────────────────────────────────────────
    //  3. Disconnect → reconnect on same seat rebinds
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Phase-J-2"), Trait("Wave", "Phase-J-2")]
    public async Task Disconnect_ThenReconnect_SameSeat_Rebinds()
    {
        const string gameId = "DISCONNECT-REBIND";
        var manager = _factory!.Services.GetRequiredService<AutotableConnectionManager>();
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();

        // First connection takes seat 0 and binds the runtime.
        var alice1 = await OpenAndJoinAsync(seat: 0, gameId: gameId);
        await alice1.TakeSeatAsync(0);
        var alice1Seated = await WaitForAsync(() =>
        {
            var rid = manager.GetRuntimeGameIdBoundTo(gameId);
            return !string.IsNullOrEmpty(rid)
                && runtime.TryGetSnapshot(rid!, out var s) && s is not null
                && string.Equals(s.Seats[0].PlayerId, alice1.PlayerId, StringComparison.Ordinal);
        }, timeoutMs: 2000);
        Assert.True(alice1Seated, "Alice1's seat-0 take should bind state.Seats[0].");

        var runtimeGameId = manager.GetRuntimeGameIdBoundTo(gameId)!;

        // Disconnect Alice1 — under Bishop's fix, this releases the seat-0
        // binding in the Changsha runtime.
        await alice1.DisposeAsync();

        // Wait for the release to be observable (poll the snapshot — under
        // the fix, seat 0 either becomes free for re-take or still holds
        // alice1's id transiently before HandleDisconnectAsync runs; we just
        // need to observe that the next seat-take succeeds).
        await using var alice2 = await OpenAndJoinAsync(seat: 0, gameId: gameId);

        // Retry the seat-take — under the current bug this throws inside the
        // backend (logged at Debug, not surfaced to the client) so the seat
        // stays unbound for alice2. The wait loop polls for the post-fix
        // success state where state.Seats[0] holds alice2's playerId.
        await alice2.TakeSeatAsync(0);

        var rebound = await WaitForAsync(() =>
        {
            if (!runtime.TryGetSnapshot(runtimeGameId, out var s) || s is null) return false;
            return string.Equals(s.Seats[0].PlayerId, alice2.PlayerId, StringComparison.Ordinal)
                && !s.Seats[0].IsBot;
        }, timeoutMs: 3000);

        Assert.True(rebound,
            "After Alice1's disconnect, Alice2's seat-0 take should succeed and the runtime " +
            "snapshot should report state.Seats[0].PlayerId == alice2.PlayerId. " +
            "If the release-on-disconnect wiring is missing, the inner TakeSeatAsync throws " +
            "'Seat 0 is already taken' and state.Seats[0].PlayerId remains alice1.PlayerId.");

        // Runtime binding survives across the swap — same runtimeGameId.
        Assert.Equal(runtimeGameId, manager.GetRuntimeGameIdBoundTo(gameId));
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────

    private async Task<WsSession> OpenAndJoinAsync(int seat, string gameId)
    {
        var server = _factory!.Server;
        var wsClient = server.CreateWebSocketClient();
        var path = $"autotable/ws?seat={seat}&gameId={Uri.EscapeDataString(gameId)}";
        var uri = new Uri(server.BaseAddress, path);
        var ws = await wsClient.ConnectAsync(uri, CancellationToken.None);
        var session = new WsSession(ws);
        await session.SendJoinAsync(gameId);
        var joined = await session.ReadEnvelopeAsync();
        Assert.Equal("JOINED", joined.GetProperty("type").GetString());
        session.PlayerId = joined.GetProperty("playerId").GetString()
            ?? throw new InvalidOperationException("JOINED envelope missing playerId.");
        var snapshot = await session.ReadEnvelopeAsync();
        Assert.Equal("UPDATE", snapshot.GetProperty("type").GetString());
        session.LastSnapshot = snapshot;
        return session;
    }

    private static async Task<bool> WaitForAsync(Func<bool> predicate, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(25);
        }
        return false;
    }

    private sealed class WsSession : IAsyncDisposable
    {
        private readonly WebSocket _ws;
        public string PlayerId { get; set; } = string.Empty;
        public JsonElement? LastSnapshot { get; set; }
        public WebSocketState SocketState => _ws.State;

        public WsSession(WebSocket ws) { _ws = ws; }

        public async Task SendJoinAsync(string gameId)
        {
            var msg = JsonSerializer.Serialize(new { type = "JOIN", gameId });
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task TakeSeatAsync(int seatIndex)
        {
            using var ms = new MemoryStream();
            await using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WriteString("type", "UPDATE");
                writer.WritePropertyName("entries");
                writer.WriteStartArray();
                writer.WriteStartArray();
                writer.WriteStringValue("seats");
                writer.WriteStringValue(PlayerId);
                writer.WriteStartObject();
                writer.WriteNumber("seat", seatIndex);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndArray();
                writer.WriteBoolean("full", false);
                writer.WriteEndObject();
            }
            var payload = Encoding.UTF8.GetString(ms.ToArray());
            var bytes = Encoding.UTF8.GetBytes(payload);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<JsonElement> ReadEnvelopeAsync(int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(buffer, cts.Token);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);
            return JsonDocument.Parse(sb.ToString()).RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            if (_ws.State == WebSocketState.Open)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "test done", CancellationToken.None); }
                catch { }
            }
            _ws.Dispose();
        }
    }
}
