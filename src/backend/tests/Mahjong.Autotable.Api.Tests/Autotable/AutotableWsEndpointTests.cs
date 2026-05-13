using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Autotable;

/// <summary>
/// CAT-PHASE5A: End-to-end WS endpoint tests using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Verifies JOIN → JOINED + full UPDATE, the always-available pattern for unknown gameIds,
/// runtime state-change → UPDATE broadcast, and the one-way Phase 5a discard policy.
/// </summary>
public class AutotableWsEndpointTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-ws-{Guid.NewGuid():N}.db");

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

    // ── unknown gameId → JOINED + empty-but-valid UPDATE (always-available) ──

    [Fact, Trait("Category", "Phase5a")]
    public async Task Join_UnknownGameId_ReturnsJoinedAndEmptySnapshot()
    {
        await using var session = await OpenAsync(seat: 0);
        await session.SendJoinAsync("DOES-NOT-EXIST");

        var joined = await session.ReadEnvelopeAsync();
        Assert.Equal("JOINED", joined.GetProperty("type").GetString());
        Assert.Equal("DOES-NOT-EXIST", joined.GetProperty("gameId").GetString());

        var update = await session.ReadEnvelopeAsync();
        Assert.Equal("UPDATE", update.GetProperty("type").GetString());
        Assert.True(update.GetProperty("full").GetBoolean());

        // Always-available: only the match entry — no game data.
        var entries = update.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("match", entries[0][0].GetString());
    }

    // ── known gameId → JOINED + full snapshot ─────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public async Task Join_KnownGameId_ReturnsFullSnapshot()
    {
        var runtime = _factory!.Services.GetRequiredService<IChangshaGameRuntime>();
        var gameId = await runtime.CreateGameAsync(seed: 11, botSeatIndexes: new[] { 1, 2, 3 }, hostConnectionId: null);
        await runtime.StartGameAsync(gameId);
        await Task.Delay(50); // let any deal-batch fanout settle

        await using var session = await OpenAsync(seat: 0);
        await session.SendJoinAsync(gameId);

        var joined = await session.ReadEnvelopeAsync();
        Assert.Equal("JOINED", joined.GetProperty("type").GetString());

        var update = await session.ReadEnvelopeAsync();
        Assert.Equal("UPDATE", update.GetProperty("type").GetString());
        Assert.True(update.GetProperty("full").GetBoolean());

        var entries = update.GetProperty("entries");
        var things = CountEntriesOfKind(entries, "things");
        var seats = CountEntriesOfKind(entries, "seats");
        var matches = CountEntriesOfKind(entries, "match");

        Assert.Equal(108, things);
        Assert.Equal(4, seats);
        Assert.Equal(1, matches);
    }

    // ── state mutation broadcasts to bound connection ─────────────────

    [Fact, Trait("Category", "Phase5a")]
    public async Task StateChange_BroadcastsUpdate_ToBoundConnection()
    {
        var runtime = _factory!.Services.GetRequiredService<IChangshaGameRuntime>();
        var gameId = await runtime.CreateGameAsync(seed: 23, botSeatIndexes: new[] { 1, 2, 3 }, hostConnectionId: null);
        await runtime.StartGameAsync(gameId);
        await Task.Delay(50);

        await using var session = await OpenAsync(seat: 0);
        await session.SendJoinAsync(gameId);

        // Consume initial JOINED + first UPDATE.
        _ = await session.ReadEnvelopeAsync();
        _ = await session.ReadEnvelopeAsync();

        // Trigger a state mutation — fill empty seats with bots toggles state and persists.
        await runtime.FillEmptySeatsWithBotsAsync(gameId);

        // Expect another UPDATE within a reasonable window.
        var followUp = await session.ReadEnvelopeAsync(timeoutMs: 2000);
        Assert.Equal("UPDATE", followUp.GetProperty("type").GetString());
        Assert.True(followUp.GetProperty("full").GetBoolean());
    }

    // ── bundle-initiated UPDATE is silently discarded ─────────────────

    [Fact, Trait("Category", "Phase5a")]
    public async Task BundleInitiatedUpdate_IsDiscardedQuietly()
    {
        await using var session = await OpenAsync(seat: 0);

        // Send a synthetic bundle UPDATE before any JOIN — endpoint must not crash.
        var bundleUpdate = "{\"type\":\"UPDATE\",\"entries\":[[\"things\",0,{\"slotName\":\"hand.0@0\",\"rotationIndex\":1,\"claimedBy\":null,\"heldRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"shiftSlotName\":null}]],\"full\":false}";
        await session.SendRawAsync(bundleUpdate);

        // Now JOIN should still succeed.
        await session.SendJoinAsync("ANY");
        var joined = await session.ReadEnvelopeAsync();
        Assert.Equal("JOINED", joined.GetProperty("type").GetString());

        var update = await session.ReadEnvelopeAsync();
        Assert.Equal("UPDATE", update.GetProperty("type").GetString());
    }

    // ── helpers ───────────────────────────────────────────────────────

    private async Task<WsSession> OpenAsync(int seat)
    {
        var server = _factory!.Server;
        var wsClient = server.CreateWebSocketClient();
        var uri = new Uri(server.BaseAddress, $"autotable/ws?seat={seat}");
        var ws = await wsClient.ConnectAsync(uri, CancellationToken.None);
        return new WsSession(ws);
    }

    private static int CountEntriesOfKind(JsonElement entriesArray, string kind)
    {
        var count = 0;
        for (var i = 0; i < entriesArray.GetArrayLength(); i++)
        {
            if (entriesArray[i][0].GetString() == kind) count++;
        }
        return count;
    }

    private sealed class WsSession : IAsyncDisposable
    {
        private readonly WebSocket _ws;
        public WsSession(WebSocket ws) { _ws = ws; }

        public async Task SendJoinAsync(string gameId)
        {
            var msg = JsonSerializer.Serialize(new { type = "JOIN", gameId });
            await SendRawAsync(msg);
        }

        public async Task SendRawAsync(string payload)
        {
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
            var json = sb.ToString();
            return JsonDocument.Parse(json).RootElement.Clone();
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
