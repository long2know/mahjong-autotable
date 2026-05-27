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

namespace Mahjong.Autotable.Api.Tests.Changsha.Acceptance;

/// <summary>
/// Regression: Vasquez tile-interaction playtest gate G4 — discard round-trip from the
/// local dealer (seat 0) was silently dropped in manual-deal mode. Root cause memo:
/// <c>.squad/decisions/inbox/vasquez-tile-interaction.md</c>.
///
/// <para>The Changsha state machine and runtime correctly advance
/// <see cref="ChangshaPhase.DealerExtra"/> → <see cref="ChangshaPhase.AwaitingDiscard"/>
/// when the dealer takes the +1 tile. The issue surfaced in the
/// <see cref="AutotableConnectionManager"/> WS endpoint, which routes the bundle's
/// <c>discard</c> push to <c>TryHandleDiscardActionAsync</c> → <c>IChangshaGameRuntime.DiscardAsync</c>.
/// The state-machine `DealerExtra → AwaitingDiscard` transition was already firing
/// (pinned by <see cref="ManualPickupAcceptanceTests"/> and
/// <see cref="BotPickupSchedulerAcceptanceTests"/>); the missing piece was that the
/// runtime broadcast emitted by <c>StateChanged</c> after the dealer's +1 take was
/// observed BEFORE the post-take advance ran. That left the wire pickup tombstone in
/// place but no <c>turnstart</c> signal for the dealer, and any client-driven
/// <c>discard</c> push raced ahead of <see cref="ChangshaGameRuntime.TryAdvanceAfterDealAsync"/>.</para>
///
/// <para>This test pins:</para>
/// <list type="number">
///   <item>After the dealer's DealerExtra take, the runtime snapshot is
///   <see cref="ChangshaPhase.AwaitingDiscard"/> with the dealer holding 14 tiles.</item>
///   <item>An immediate <c>DiscardAsync</c> call from the dealer (no other intervening
///   ack/turnstart) succeeds: the tile leaves the dealer's hand and lands in the
///   discard pile.</item>
///   <item>The next seat (CCW from dealer) becomes the active seat — the round-robin
///   continues. (Implicit because <see cref="ChangshaGameStateMachine.Discard"/> hands
///   off to <c>AwaitingDraw</c>/<c>AwaitingClaim</c> per Changsha rules.)</item>
///   <item>The autotable WS endpoint round-trips the bundle's
///   <c>["discard", 0, {tileId}]</c> push end-to-end: it lands the tile in the
///   <c>discard.*@0</c> slot on the next broadcast.</item>
/// </list>
/// </summary>
public sealed class DealerExtraTransitionsToAwaitingDiscardTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"dealerextra-{Guid.NewGuid():N}.db");

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
                    o.BotPickupDelayMs = 25;
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

    // ── Runtime-level: DealerExtra → AwaitingDiscard → Discard works ────────

    [Fact, Trait("Category", "TileInteraction"), Trait("Gate", "G4")]
    public async Task Runtime_AfterDealerExtraTake_PhaseIsAwaitingDiscard_AndDiscardSucceeds()
    {
        var runtime = _factory!.Services.GetRequiredService<IChangshaGameRuntime>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Manual game, dealer = seat 0 (human), seats 1/2/3 bots.
        var gameId = await runtime.CreateGameAsync(seed: 4242, botSeatIndexes: new[] { 1, 2, 3 },
            hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var pre));
        pre!.DealerSeatIndex = 0;
        foreach (var seat in pre.Seats) seat.IsDealer = seat.SeatIndex == 0;

        Assert.True(await runtime.ApplyDealModeAsync(gameId, DealMode.Manual, cts.Token));

        // Drive the manual ceremony from RollingDice through every pickup the human owns.
        // The bot scheduler auto-advances seats 1/2/3 between human picks.
        await runtime.StartGameAsync(gameId, cts.Token);
        await runtime.RollDiceAsync(gameId, seatIndex: 0, cts.Token);

        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token); // R1
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.PickupRound2,
            TimeSpan.FromSeconds(3),
            "cursor did not return to dealer for round 2");

        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token); // R2
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.PickupRound3,
            TimeSpan.FromSeconds(3),
            "cursor did not return to dealer for round 3");

        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token); // R3
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.SingleTilePickup,
            TimeSpan.FromSeconds(3),
            "cursor did not return to dealer for single-tile round");

        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 1, cts.Token); // single
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.DealerExtra,
            TimeSpan.FromSeconds(3),
            "phase did not advance to DealerExtra with cursor at dealer");

        // The dealer takes the +1 tile.
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 1, cts.Token);

        // Bishop's contract: the runtime MUST transition to AwaitingDiscard with the
        // dealer's 14th tile in hand. PickupSeatIndex is cleared (manual deal complete).
        var post = Snapshot(runtime, gameId);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, post.Phase);
        Assert.Equal(0, post.ActiveSeatIndex);
        Assert.Null(post.PickupSeatIndex);
        Assert.Equal(14, post.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles.Count);
        Assert.Equal(55, post.Wall.Count);

        // Pick the dealer's last-picked tile (the 14th) and discard it. This is the
        // direct repro of Vasquez gate G4 — the discard MUST be accepted without an
        // explicit AcknowledgeDealAsync (manual-deal autotable bundle has no ack route).
        var dealerHand = post.Hands.Single(h => h.SeatIndex == 0);
        var tileToDiscard = dealerHand.ConcealedTiles[^1];

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: tileToDiscard, cts.Token);

        // Verify the discard landed: tile is in the discard pile, dealer is back to 13.
        var afterDiscard = Snapshot(runtime, gameId);
        Assert.Contains(afterDiscard.DiscardPile,
            d => d.SeatIndex == 0 && d.TileId == tileToDiscard);
        Assert.Equal(13, afterDiscard.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles.Count);
        Assert.DoesNotContain(tileToDiscard,
            afterDiscard.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles);
    }

    // ── Translator: post-take broadcast surfaces the new phase ──────────────

    [Fact, Trait("Category", "TileInteraction"), Trait("Gate", "G4")]
    public async Task Runtime_AfterDealerExtraTake_TranslatorEmits_NoPickupEntry_AndDiscardAccepted()
    {
        // Mirror Vasquez's observation: after DealerExtra, the wire `pickup` entry
        // transitions to null (translator no longer emits it). The discard handler
        // MUST nevertheless accept the dealer's discard. This pins the relationship
        // between the translator's tombstone and the discard-phase gate.
        var runtime = _factory!.Services.GetRequiredService<IChangshaGameRuntime>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var gameId = await runtime.CreateGameAsync(seed: 4343, botSeatIndexes: new[] { 1, 2, 3 },
            hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var pre));
        pre!.DealerSeatIndex = 0;
        foreach (var seat in pre.Seats) seat.IsDealer = seat.SeatIndex == 0;
        Assert.True(await runtime.ApplyDealModeAsync(gameId, DealMode.Manual, cts.Token));

        await runtime.StartGameAsync(gameId, cts.Token);
        await runtime.RollDiceAsync(gameId, seatIndex: 0, cts.Token);

        // Mid-ceremony: translator must surface the pickup entry while in a pickup phase.
        var mid = Snapshot(runtime, gameId);
        var midEntries = ChangshaToAutotableTranslator.Translate(mid, viewerSeat: 0);
        Assert.Contains(midEntries, e => e.Kind == "pickup" && (e.Key as string) == "current"
                                       && e.Value is not null);

        // Drive ceremony through DealerExtra.
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token);
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.PickupRound2,
            TimeSpan.FromSeconds(3), "round 1 did not complete");
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token);
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.PickupRound3,
            TimeSpan.FromSeconds(3), "round 2 did not complete");
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 4, cts.Token);
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.SingleTilePickup,
            TimeSpan.FromSeconds(3), "round 3 did not complete");
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 1, cts.Token);
        await WaitForAsync(() => Snapshot(runtime, gameId).PickupSeatIndex == 0
                              && Snapshot(runtime, gameId).Phase == ChangshaPhase.DealerExtra,
            TimeSpan.FromSeconds(3), "DealerExtra not reached");
        await runtime.TakeTilesFromWallAsync(gameId, seatIndex: 0, count: 1, cts.Token);

        var post = Snapshot(runtime, gameId);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, post.Phase);

        // After the dealer-extra take, the translator must no longer emit a pickup
        // entry (manual deal complete). The bundle's `client.pickup.get('current')`
        // observes this as a transition to undefined/null — matching Vasquez's note.
        var postEntries = ChangshaToAutotableTranslator.Translate(post, viewerSeat: 0);
        Assert.DoesNotContain(postEntries, e => e.Kind == "pickup" && e.Value is not null);

        // And the runtime must accept a discard from the dealer right now — no other
        // call is required. This is the regression gate for G4.
        var tileToDiscard = post.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles[^1];
        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: tileToDiscard, cts.Token);

        var afterDiscard = Snapshot(runtime, gameId);
        Assert.Contains(afterDiscard.DiscardPile,
            d => d.SeatIndex == 0 && d.TileId == tileToDiscard);
    }

    // ── End-to-end via the autotable WS endpoint ────────────────────────────

    [Fact, Trait("Category", "TileInteraction"), Trait("Gate", "G4")]
    public async Task WsEndpoint_DealerDiscardAfterDealerExtra_LandsInDiscardSlot()
    {
        // Mirror the wire path Vasquez exercised in the Playwright playtest:
        // bundle sends `["discard", 0, {tileId}]` after the runtime advances to
        // AwaitingDiscard. The endpoint must echo a `things` UPDATE moving the
        // tile to a `discard.*@0` slot.
        var gameId = $"dealerextra-ws-{Guid.NewGuid():N}";
        await using var session = await OpenAsync(seat: 0, gameId: gameId, dealMode: "manual");
        await session.SendJoinAsync(gameId);
        _ = await session.ReadEnvelopeAsync(); // JOINED
        _ = await session.ReadEnvelopeAsync(); // initial UPDATE

        // Take seat 0 (binds runtime).
        await session.SendUpdateAsync(new object[]
        {
            new object[] { "seats", 0, new { seat = 0 } }
        });

        var manager = _factory!.Services.GetRequiredService<AutotableConnectionManager>();
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();
        var runtimeGameId = await WaitForBindingAsync(manager, gameId, timeoutMs: 3000);
        Assert.NotNull(runtimeGameId);

        await DrainAsync(session, timeoutMs: 300);

        // Bundle's Deal click → match[0] = { dealCommand: "start" } (or vanilla
        // world.deal() push). Then dice roll. Then the take-chain.
        await session.SendUpdateAsync(new object[]
        {
            new object[] { "match", 0, new { dealCommand = "start" } }
        });

        Assert.True(await WaitForAsync(() =>
        {
            if (!runtime.TryGetSnapshot(runtimeGameId!, out var s) || s is null) return false;
            return s.DealMode == DealMode.Manual && s.Phase == ChangshaPhase.RollingDice;
        }, timeoutMs: 3000), "manual deal did not reach RollingDice");

        await DrainAsync(session, timeoutMs: 200);

        // Roll dice via the action-in-key wire shape (per W23 tests).
        await session.SendUpdateAsync(new object[]
        {
            new object[] { "pickup", "rollDice", new { seatIndex = 0 } }
        });
        Assert.True(await WaitForAsync(() =>
        {
            if (!runtime.TryGetSnapshot(runtimeGameId!, out var s) || s is null) return false;
            return s.Phase == ChangshaPhase.BreakPointMarked
                || s.Phase == ChangshaPhase.PickupRound1;
        }, timeoutMs: 3000), "dice roll did not park runtime at BreakPointMarked");

        // Drive the 5 takes (4/4/4/1/1) via the action-in-key wire shape.
        await DriveTakeAsync(session, runtime, runtimeGameId!, count: 4,
            untilPhase: ChangshaPhase.PickupRound2);
        await DriveTakeAsync(session, runtime, runtimeGameId!, count: 4,
            untilPhase: ChangshaPhase.PickupRound3);
        await DriveTakeAsync(session, runtime, runtimeGameId!, count: 4,
            untilPhase: ChangshaPhase.SingleTilePickup);
        await DriveTakeAsync(session, runtime, runtimeGameId!, count: 1,
            untilPhase: ChangshaPhase.DealerExtra);
        await DriveTakeAsync(session, runtime, runtimeGameId!, count: 1,
            untilPhase: ChangshaPhase.AwaitingDiscard);

        // Phase guard: by here the runtime has transitioned to AwaitingDiscard with
        // the dealer holding 14 tiles. This is the post-condition Vasquez expected.
        Assert.True(runtime.TryGetSnapshot(runtimeGameId!, out var dealerReady));
        Assert.Equal(ChangshaPhase.AwaitingDiscard, dealerReady!.Phase);
        Assert.Equal(14, dealerReady.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles.Count);

        var dealerHand = dealerReady.Hands.Single(h => h.SeatIndex == 0);
        var discardTileId = dealerHand.ConcealedTiles[^1];

        await DrainAsync(session, timeoutMs: 200);

        // The bundle's emitDiscard pushes ["discard", 0, {tileId}]. This MUST round-trip
        // through the WS endpoint and land the tile in the discard pile.
        await session.SendUpdateAsync(new object[]
        {
            new object[] { "discard", 0, new { tileId = discardTileId } }
        });

        var discardLanded = await WaitForAsync(() =>
        {
            if (!runtime.TryGetSnapshot(runtimeGameId!, out var s) || s is null) return false;
            return s.DiscardPile.Any(d => d.SeatIndex == 0 && d.TileId == discardTileId)
                && !s.Hands.Single(h => h.SeatIndex == 0).ConcealedTiles.Contains(discardTileId);
        }, timeoutMs: 3000);

        Assert.True(discardLanded,
            "Vasquez G4 regression: bundle's discard push for the dealer's 14th tile " +
            "must round-trip and land in the discard pile. The autotable WS endpoint " +
            "is silently dropping it.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task DriveTakeAsync(
        WsSession session,
        IChangshaGameRuntime runtime,
        string runtimeGameId,
        int count,
        ChangshaPhase untilPhase)
    {
        await session.SendUpdateAsync(new object[]
        {
            new object[] { "pickup", "take", new { seatIndex = 0, count } }
        });

        var advanced = await WaitForAsync(() =>
        {
            if (!runtime.TryGetSnapshot(runtimeGameId, out var s) || s is null) return false;
            return s.Phase == untilPhase;
        }, timeoutMs: 4000);

        Assert.True(advanced, $"runtime did not reach {untilPhase} after take({count})");
    }

    private static ChangshaGameState Snapshot(IChangshaGameRuntime runtime, string gameId)
    {
        Assert.True(runtime.TryGetSnapshot(gameId, out var s));
        return s!;
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(25);
        }
        if (!predicate())
            throw new TimeoutException($"DealerExtra→AwaitingDiscard contract violated: {description}");
    }

    private static async Task<bool> WaitForAsync(Func<bool> predicate, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(25);
        }
        return predicate();
    }

    private async Task<WsSession> OpenAsync(int seat, string gameId, string dealMode)
    {
        var server = _factory!.Server;
        var wsClient = server.CreateWebSocketClient();
        var path = $"autotable/ws?seat={seat}&gameId={Uri.EscapeDataString(gameId)}&dealMode={dealMode}&botCount=3";
        var uri = new Uri(server.BaseAddress, path);
        var ws = await wsClient.ConnectAsync(uri, CancellationToken.None);
        return new WsSession(ws);
    }

    private static async Task<string?> WaitForBindingAsync(
        AutotableConnectionManager manager, string relayGameId, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var rid = manager.GetRuntimeGameIdBoundTo(relayGameId);
            if (rid is not null) return rid;
            await Task.Delay(25);
        }
        return manager.GetRuntimeGameIdBoundTo(relayGameId);
    }

    private static async Task DrainAsync(WsSession session, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try { _ = await session.ReadEnvelopeAsync(timeoutMs: 50); }
            catch { return; }
        }
    }

    private sealed class WsSession : IAsyncDisposable
    {
        private readonly WebSocket _ws;
        public WsSession(WebSocket ws) { _ws = ws; }

        public async Task SendJoinAsync(string gameId)
        {
            var msg = JsonSerializer.Serialize(new { type = "JOIN", gameId });
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendUpdateAsync(object[] entries)
        {
            var msg = JsonSerializer.Serialize(new { type = "UPDATE", entries, full = false });
            var bytes = Encoding.UTF8.GetBytes(msg);
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
