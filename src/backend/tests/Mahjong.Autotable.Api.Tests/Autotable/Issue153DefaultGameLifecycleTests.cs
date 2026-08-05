using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Autotable;

/// <summary>
/// #153 — backend default-game lifecycle contract (real WebSocket, no state injection / test hooks).
///
/// <para>Hudson's independent live diagnosis (issue #153, comment 5096546486) proved the Changsha
/// engine and bot scheduler are healthy: a fresh game deals 14/13/13/13, bots take their own pickups
/// and turns, and hands roll over. The reproducible backend defect is <b>persistence/attach UX</b>:
/// the persistent <see cref="AutotableWsEndpoint.DefaultGameId"/> ("changsha-default") — the fallback
/// binding for a bare <c>?variant=changsha</c> connection that carries no explicit gameId — is reused
/// forever. A fresh browser opening the bare URL after a previous match was dealt / stalled / left
/// silently JOINs that leftover game (seat 0 still owned by the departed player, hand frozen
/// mid-ceremony) → the user's "restart → Take Seat → nothing progresses" report.</para>
///
/// <para>The fix makes stale/default attach <b>explicit and safe</b>: a seat-seeking newcomer whose
/// persistent identity does not already own a seat, arriving at an <em>abandoned</em> default game
/// that has advanced past <see cref="ChangshaPhase.Seating"/>, is given a fresh table instead of the
/// stale one. Two invariants are preserved verbatim and pinned below: a <b>deliberate reconnect</b>
/// (same cookie-derived playerId returning to its seat) reattaches, and every <b>explicit
/// <c>?gameId=</c></b> game keeps first-creator-wins + multi-human join. A live default game with
/// another player still connected is never torn out from under them.</para>
/// </summary>
public sealed class Issue153DefaultGameLifecycleTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    private const string DefaultGameId = AutotableWsEndpoint.DefaultGameId;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"issue153-lifecycle-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
            b.ConfigureServices(s => s.Configure<ChangshaRuntimeOptions>(o =>
            {
                o.BotTurnDelayMs = 1;
                o.BotClaimDelayMs = 1;
                o.BotPickupDelayMs = 1;
                o.ClaimWindowTimeoutMs = 50;
                o.DealBatchDelayMs = 0;
                o.PersistSnapshots = false;
            }));
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

    private IChangshaGameRuntime Runtime => _factory!.Services.GetRequiredService<IChangshaGameRuntime>();
    private AutotableConnectionManager Manager => _factory!.Services.GetRequiredService<AutotableConnectionManager>();

    // ── PRIMARY: fresh browser must NOT inherit a stale, already-dealt default game ──────────────

    [Fact]
    public async Task FreshBrowser_AfterDefaultGameDealt_GetsFreshTable_NotStaleState()
    {
        // Browser A: bare default URL, auto deal, seat 0, plays into a dealt hand, then leaves.
        string ridA;
        await using (var a = await OpenAsync("?variant=changsha&dealMode=auto"))
        {
            await a.JoinAndReadAsync(DefaultGameId);
            await a.TakeSeatAsync(0);
            var bound = await WaitForBindingAsync();
            Assert.NotNull(bound);
            ridA = bound!;
            await a.SendDealAsync();
            var dealt = await WaitForAsync(() => Runtime.TryGetSnapshot(ridA, out var s) && s is not null
                && s.Phase >= ChangshaPhase.AwaitingDiscard);
            Assert.True(dealt, "precondition: browser A's default game must reach a dealt hand.");
        }

        // A has left: wait until the server has fully processed the disconnect (abandoned game).
        Assert.True(await WaitForAsync(() => Manager.ConnectionCount == 0),
            "browser A's disconnect must be processed before browser B connects.");

        // Browser B: a DIFFERENT fresh identity (no cookie) opens the same bare default URL.
        await using var b = await OpenAsync("?variant=changsha&dealMode=auto");
        var joinedB = await b.JoinAndReadAsync(DefaultGameId);
        var pidB = joinedB.GetProperty("playerId").GetString();
        await b.TakeSeatAsync(0);
        var ridB = await WaitForBindingChangeAsync(ridA);

        Assert.NotNull(ridB);
        Assert.NotEqual(ridA, ridB);

        // B sits at a fresh, clean table — Seating phase, seat 0 owned by B, no inherited hand.
        Assert.True(Runtime.TryGetSnapshot(ridB!, out var snapB) && snapB is not null);
        Assert.Equal(ChangshaPhase.Seating, snapB!.Phase);
        Assert.Equal(pidB, snapB.Seats[0].PlayerId);
        Assert.All(snapB.Hands, h => Assert.Empty(h.ConcealedTiles));

        // The stale game A left behind was retired, not handed to B.
        Assert.False(Runtime.TryGetSnapshot(ridA, out var staleSnap) && staleSnap is not null
            && !staleSnap.IsGameComplete,
            "the stale default game must be retired (removed / terminal), never reattached to a newcomer.");
    }

    // ── PRESERVE: a deliberate reconnect (same playerId) reattaches to its own game ──────────────

    [Fact]
    public async Task DeliberateReconnect_SamePlayer_ReattachesToStartedGame_NotReset()
    {
        var pid = $"reconnect-{Guid.NewGuid():N}";
        string ridA;
        await using (var a = await OpenAsync("?variant=changsha&dealMode=auto", cookiePlayerId: pid))
        {
            await a.JoinAndReadAsync(DefaultGameId);
            await a.TakeSeatAsync(0);
            ridA = (await WaitForBindingAsync())!;
            await a.SendDealAsync();
            Assert.True(await WaitForAsync(() => Runtime.TryGetSnapshot(ridA, out var s) && s is not null
                && s.Phase >= ChangshaPhase.AwaitingDiscard), "precondition: reconnect game must be dealt.");
        }

        Assert.True(await WaitForAsync(() => Manager.ConnectionCount == 0),
            "the first connection's disconnect must be processed before the reconnect.");

        // SAME persistent identity returns (cookie carried) and re-takes its seat.
        await using var a2 = await OpenAsync("?variant=changsha&dealMode=auto", cookiePlayerId: pid);
        await a2.JoinAndReadAsync(DefaultGameId);
        await a2.TakeSeatAsync(0);
        await Task.Delay(150);
        var ridA2 = Manager.GetRuntimeGameIdBoundTo(DefaultGameId);

        Assert.Equal(ridA, ridA2); // reattached, NOT reset
        Assert.True(Runtime.TryGetSnapshot(ridA2!, out var snap) && snap is not null);
        Assert.Equal(pid, snap!.Seats[0].PlayerId);
        Assert.True(snap.Phase >= ChangshaPhase.AwaitingDiscard,
            "a deliberate reconnect must preserve the in-progress hand, not restart it.");
    }

    // ── PRESERVE: explicit ?gameId= games keep first-creator-wins + join (never reset) ──────────

    [Fact]
    public async Task ExplicitGameId_NewPlayer_JoinsExistingGame_NotReset()
    {
        var gameId = $"shared-153-{Guid.NewGuid():N}";
        await using var a = await OpenAsync($"?variant=changsha&dealMode=auto&gameId={gameId}");
        await a.JoinAndReadAsync(gameId);
        await a.TakeSeatAsync(0);
        var ridA = await WaitForBindingAsync(gameId);
        Assert.NotNull(ridA);
        await a.SendDealAsync();
        Assert.True(await WaitForAsync(() => Runtime.TryGetSnapshot(ridA!, out var s) && s is not null
            && s.Phase >= ChangshaPhase.AwaitingDiscard), "precondition: explicit game must be dealt.");

        // A DIFFERENT player joins the SAME explicit gameId while A is still playing.
        await using var b = await OpenAsync($"?variant=changsha&gameId={gameId}");
        await b.JoinAndReadAsync(gameId);
        await b.TakeSeatAsync(1);
        await Task.Delay(150);

        // Explicit games are never subject to the default-game reset: the binding is unchanged.
        Assert.Equal(ridA, Manager.GetRuntimeGameIdBoundTo(gameId));
        Assert.True(Runtime.TryGetSnapshot(ridA!, out var snap) && snap is not null);
        Assert.True(snap!.Phase >= ChangshaPhase.AwaitingDiscard,
            "first-creator-wins: the joiner attaches to the creator's in-progress game unchanged.");
    }

    // ── SAFETY: a live default game with another player connected is not torn out ────────────────

    [Fact]
    public async Task LiveDefaultGame_WithConnectedPlayer_NotRetiredByConcurrentNewcomer()
    {
        await using var a = await OpenAsync("?variant=changsha&dealMode=auto");
        await a.JoinAndReadAsync(DefaultGameId);
        await a.TakeSeatAsync(0);
        var ridA = await WaitForBindingAsync();
        Assert.NotNull(ridA);
        await a.SendDealAsync();
        Assert.True(await WaitForAsync(() => Runtime.TryGetSnapshot(ridA!, out var s) && s is not null
            && s.Phase >= ChangshaPhase.AwaitingDiscard), "precondition: live default game must be dealt.");

        // A stays connected. A newcomer arrives at the default game and tries to sit.
        await using var b = await OpenAsync("?variant=changsha&dealMode=auto");
        await b.JoinAndReadAsync(DefaultGameId);
        await b.TakeSeatAsync(1);
        await Task.Delay(200);

        // A's live game must survive — the newcomer must not retire a game with active co-players.
        Assert.Equal(ridA, Manager.GetRuntimeGameIdBoundTo(DefaultGameId));
        Assert.True(Runtime.TryGetSnapshot(ridA!, out var snap) && snap is not null
            && !snap.IsGameComplete,
            "a default game with another connected player must not be retired by a newcomer.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task<string?> WaitForBindingAsync(string relayGameId = DefaultGameId)
    {
        await WaitForAsync(() => Manager.GetRuntimeGameIdBoundTo(relayGameId) is not null);
        return Manager.GetRuntimeGameIdBoundTo(relayGameId);
    }

    private async Task<string?> WaitForBindingChangeAsync(string previous, string relayGameId = DefaultGameId)
    {
        await WaitForAsync(() =>
        {
            var rid = Manager.GetRuntimeGameIdBoundTo(relayGameId);
            return rid is not null && !string.Equals(rid, previous, StringComparison.Ordinal);
        });
        return Manager.GetRuntimeGameIdBoundTo(relayGameId);
    }

    private async Task<bool> WaitForAsync(Func<bool> predicate, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(20);
        }
        return predicate();
    }

    private async Task<WsSession> OpenAsync(string queryString, string? cookiePlayerId = null)
    {
        var server = _factory!.Server;
        var wsClient = server.CreateWebSocketClient();
        if (cookiePlayerId is not null)
        {
            wsClient.ConfigureRequest = req =>
                req.Headers["Cookie"] = $"mahjong_pid={cookiePlayerId}";
        }
        var uri = new Uri(server.BaseAddress, $"autotable/ws{queryString}");
        var ws = await wsClient.ConnectAsync(uri, CancellationToken.None);
        return new WsSession(ws);
    }

    private sealed class WsSession : IAsyncDisposable
    {
        private readonly WebSocket _ws;
        public string PlayerId { get; private set; } = string.Empty;
        public WsSession(WebSocket ws) { _ws = ws; }

        public async Task<JsonElement> JoinAndReadAsync(string gameId)
        {
            await SendRawAsync(JsonSerializer.Serialize(new { type = "JOIN", gameId }));
            var joined = await ReadEnvelopeAsync();
            PlayerId = joined.GetProperty("playerId").GetString() ?? string.Empty;
            _ = await ReadEnvelopeAsync(); // initial full UPDATE
            return joined;
        }

        public async Task TakeSeatAsync(int seatIndex) => await SendUpdateAsync(new object[]
        {
            new object[] { "seats", seatIndex, new { seat = seatIndex } }
        });

        public async Task SendDealAsync() => await SendUpdateAsync(new object[]
        {
            new object[] { "match", 0, new { dealCommand = "start" } }
        });

        public async Task SendUpdateAsync(object[] entries) =>
            await SendRawAsync(JsonSerializer.Serialize(new { type = "UPDATE", entries, full = false }));

        private async Task SendRawAsync(string payload) => await _ws.SendAsync(
            Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, CancellationToken.None);

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
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
                catch { }
            }
            _ws.Dispose();
        }
    }
}
