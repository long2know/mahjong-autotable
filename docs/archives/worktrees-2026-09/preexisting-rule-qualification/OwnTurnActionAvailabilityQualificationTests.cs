using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class OwnTurnActionAvailabilityQualificationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _database;

    public OwnTurnActionAvailabilityQualificationTests()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(directory);
        _database = Path.Combine(directory, $"own-turn-qualification-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_database}");
            builder.ConfigureServices(services => services.Configure<ChangshaRuntimeOptions>(options =>
            {
                options.PersistSnapshots = false;
                options.BotTurnDelayMs = 30000;
                options.BotClaimDelayMs = 30000;
                options.BotPickupDelayMs = 30000;
                options.ClaimWindowTimeoutMs = 30000;
                options.DealBatchDelayMs = 0;
            }));
        });
    }

    public Task InitializeAsync()
    {
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (File.Exists(_database)) File.Delete(_database);
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "PredrawIsNotReady")]
    [InlineData(0, 13, 14)]
    [InlineData(2, 7, 8)]
    public async Task RuntimeAdvance_EmitsLegitimatePredrawThenActualExtraTile(
        int meldCount, int expectedBefore, int expectedAfter)
    {
        await using var table = await OpenHumanTableAsync();
        var scenario = meldCount == 0 ? "self-hu" : "self-hu-two-chows";
        var front = ArrangeBeforeDraw(table.State, scenario);
        var counts = new ConcurrentQueue<int>();
        void Observe(string id, ChangshaGameState snapshot)
        {
            if (id == table.GameId && snapshot.Phase == ChangshaPhase.AwaitingDiscard
                && snapshot.ActiveSeatIndex == 0)
            {
                counts.Enqueue(snapshot.Hands[0].ConcealedTiles.Count);
            }
        }
        table.Runtime.StateChanged += Observe;
        try
        {
            await AdvanceToHumanDrawAsync(table);
        }
        finally
        {
            table.Runtime.StateChanged -= Observe;
        }

        var observed = counts.ToArray();
        Assert.Contains(expectedBefore, observed);
        Assert.Contains(expectedAfter, observed);
        Assert.True(Array.IndexOf(observed, expectedBefore) < Array.IndexOf(observed, expectedAfter));
        Assert.Equal(13, expectedBefore + 3 * meldCount);
        Assert.Equal(14, expectedAfter + 3 * meldCount);
        Assert.Equal(front, table.State.Hands[0].ConcealedTiles[^1]);
        Assert.True(new ChangshaWinDetector().Detect(table.State.Hands[0]).IsWin);
        Assert.Null(table.State.CurrentWin);
    }

    [Theory, Trait("Category", "RulesQualificationGap"), Trait("Rule", "OwnTurnWsClassification")]
    [InlineData("self-hu", "Hu", 14, 0)]
    [InlineData("self-hu-two-chows", "Hu", 8, 2)]
    [InlineData("concealed-kong", "Kong", 14, 0)]
    [InlineData("added-kong", "Kong", 11, 1)]
    public async Task CurrentWsClaimRoute_DoesNotExpressLegalHumanOwnTurnActions(
        string scenario, string claimType, int expectedConcealed, int expectedMelds)
    {
        await using var table = await OpenHumanTableAsync();
        var front = ArrangeBeforeDraw(table.State, scenario);
        await AdvanceToHumanDrawAsync(table);
        var ready = await table.Runtime.TryGetSnapshotCopyAsync(table.GameId)
            ?? throw new InvalidOperationException("Missing ready snapshot.");
        Assert.Equal(ChangshaPhase.AwaitingDiscard, ready.Phase);
        Assert.Equal(0, ready.ActiveSeatIndex);
        Assert.Equal(expectedConcealed, ready.Hands[0].ConcealedTiles.Count);
        Assert.Equal(expectedMelds, ready.Hands[0].Melds.Count);
        Assert.Equal(14, expectedConcealed + 3 * expectedMelds);
        Assert.Equal(front, ready.Hands[0].ConcealedTiles[^1]);
        Assert.All(ready.Seats, seat => Assert.False(seat.IsBot));
        Assert.Equal(0, table.Runtime.TryGetSeatForPlayer(table.GameId, table.Peer.PlayerId));
        Assert.Null(ready.CurrentWin);
        var version = ready.StateVersion;
        var before = JsonSerializer.Serialize(ready);

        var initialWire = await table.Peer.JoinBarrierAsync();
        AssertOwnClaimClosed(initialWire);
        var noWindow = await Assert.ThrowsAsync<HubException>(() =>
            table.Runtime.ClaimAsync(table.GameId, 0, claimType, null));
        Assert.Equal("No claim window is open.", noWindow.Message);

        // Use the existing claim payload, not an invented own-turn protocol.
        await table.Peer.SendUpdateAsync(
            [new object[] { "claim", 0, new { action = "claim", type = claimType } }]);
        var afterWire = await table.Peer.JoinBarrierAsync();
        AssertOwnClaimClosed(afterWire);
        var after = await table.Runtime.TryGetSnapshotCopyAsync(table.GameId)
            ?? throw new InvalidOperationException("Missing post-command snapshot.");
        Assert.Equal(version, after.StateVersion);
        Assert.Equal(before, JsonSerializer.Serialize(after));

        // Positive alternate-path control: runtime commands really implement the rule.
        if (claimType == "Hu")
        {
            Assert.True(new ChangshaWinDetector().Detect(after.Hands[0]).IsWin);
            await table.Runtime.DeclareWinAsync(table.GameId, 0);
            Assert.Equal(WinMethod.SelfDraw, table.State.CurrentWin!.Method);
            Assert.Equal(ChangshaPhase.GameComplete, table.State.Phase);
        }
        else
        {
            await table.Runtime.DeclareKongAsync(table.GameId, 0, [front]);
            Assert.True(table.State.StateVersion > version);
            if (scenario == "concealed-kong")
            {
                Assert.Equal(MeldKind.ConcealedKong, Assert.Single(table.State.Hands[0].Melds).Kind);
                Assert.Equal(1, table.State.WallBackDrawn);
            }
            else
            {
                Assert.True(table.State.Hands[0].Melds.Any(m => m.Kind == MeldKind.AddedKong)
                    || table.State.ClaimWindow is { IsKongRobbing: true });
            }
        }
    }

    private async Task<HumanTable> OpenHumanTableAsync()
    {
        var relayId = $"own-turn-{Guid.NewGuid():N}";
        var server = _factory.Server;
        var uri = new Uri(server.BaseAddress,
            $"autotable/ws?variant=changsha&dealMode=manual&bots=false&botCount=0&seat=0&handCount=1&gameId={relayId}");
        var socket = await server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None);
        var peer = new WsPeer(socket, relayId);
        await peer.JoinBarrierAsync();
        await peer.SendUpdateAsync(
        [
            new object[] { "ephemeral", "claim", true },
            new object[] { "ephemeral", "pickup", true },
            new object[] { "ephemeral", "turn", true },
            new object[] { "ephemeral", "discard", true },
            new object[] { "ephemeral", "gameComplete", true }
        ]);
        await peer.SendUpdateAsync([new object[] { "seats", peer.PlayerId, new { seat = 0 } }]);
        await peer.JoinBarrierAsync();
        var manager = _factory.Services.GetRequiredService<AutotableConnectionManager>();
        var gameId = manager.GetRuntimeGameIdBoundTo(relayId)
            ?? throw new InvalidOperationException("The legitimate WS seat did not bind a runtime game.");
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();
        Assert.Equal(0, runtime.TryGetSeatForPlayer(gameId, peer.PlayerId));
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        Assert.NotNull(state);
        Assert.All(state.Seats, seat => Assert.False(seat.IsBot));
        return new HumanTable(gameId, runtime, state, peer);
    }

    private static int ArrangeBeforeDraw(ChangshaGameState state, string scenario)
    {
        int[] readyTiles;
        Meld[] ownMelds = [];
        var front = 19;
        if (scenario == "self-hu")
        {
            front = 0;
            readyTiles = [4, 8, 12, 16, 20, 24, 28, 32, 52, 53, 36, 40, 44, front];
        }
        else if (scenario == "self-hu-two-chows")
        {
            front = 20;
            ownMelds =
            [
                new Meld { Kind = MeldKind.Chow, TileIds = [0, 4, 8], ClaimedFromSeatIndex = 3 },
                new Meld { Kind = MeldKind.Chow, TileIds = [48, 52, 56], ClaimedFromSeatIndex = 3 }
            ];
            readyTiles = [12, 16, 36, 40, 44, 88, 89, front];
        }
        else
        {
            int[] filler = [40, 41, 42, 44, 45, 46, 48, 49, 50, 52];
            if (scenario == "concealed-kong")
            {
                readyTiles = new[] { 16, 17, 18 }.Concat(filler).Append(front).ToArray();
            }
            else
            {
                Assert.Equal("added-kong", scenario);
                ownMelds =
                [
                    new Meld { Kind = MeldKind.Pung, TileIds = [16, 17, 18], ClaimedFromSeatIndex = 2 }
                ];
                readyTiles = filler.Append(front).ToArray();
            }
        }
        foreach (var hand in state.Hands)
        {
            hand.ConcealedTiles.Clear();
            hand.Melds.Clear();
        }
        state.Hands[0].ConcealedTiles.AddRange(readyTiles[..^1]);
        state.Hands[0].Melds.AddRange(ownMelds);
        const int unclaimableDiscard = 104;
        var assigned = readyTiles.Concat(ownMelds.SelectMany(m => m.TileIds))
            .Append(unclaimableDiscard).ToArray();
        Assert.Equal(assigned.Length, assigned.Distinct().Count());
        var remaining = new Queue<int>(Enumerable.Range(0, 108).Except(assigned));
        for (var seat = 1; seat < 4; seat++)
        {
            var count = seat == 3 ? 14 : 13;
            if (seat == 3)
                state.Hands[seat].ConcealedTiles.Add(unclaimableDiscard);
            for (var index = state.Hands[seat].ConcealedTiles.Count; index < count; index++)
                state.Hands[seat].ConcealedTiles.Add(remaining.Dequeue());
        }
        state.Wall = new[] { front }.Concat(remaining).ToList();
        state.WallBackDrawn = 0;
        state.WallDrawIndex = 0;
        state.WallBackIndex = state.Wall.Count - 1;
        state.DiscardPile.Clear();
        state.ClaimWindow = null;
        state.CurrentWin = null;
        state.CurrentScore = null;
        state.MissedWinSeats.Clear();
        state.Phase = ChangshaPhase.AwaitingDiscard;
        state.ActiveSeatIndex = 3;
        state.TurnNumber = 3;
        state.MaxHands = 1;
        state.BreakPoint = new BreakPointService().ComputeBreakPoint(5, 0);
        state.LastDrawWasKongReplacement = false;
        Assert.Equal(Enumerable.Range(0, 108),
            state.Wall.Concat(state.Hands.SelectMany(h =>
                h.ConcealedTiles.Concat(h.Melds.SelectMany(m => m.TileIds)))).OrderBy(t => t));
        return front;
    }

    private static async Task AdvanceToHumanDrawAsync(HumanTable table)
    {
        var adjudicator = new ClaimAdjudicator();
        var discard = table.State.Hands[3].ConcealedTiles.FirstOrDefault(
            tile => adjudicator.GetOpportunities(3, tile, table.State.Hands).Count == 0, -1);
        Assert.InRange(discard, 0, 107);
        await table.Runtime.DiscardAsync(table.GameId, 3, discard);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(0, table.State.ActiveSeatIndex);
        Assert.Contains(table.State.EventLog,
            entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 0);
    }

    private static void AssertOwnClaimClosed(JsonElement snapshot)
    {
        var claims = snapshot.GetProperty("entries").EnumerateArray()
            .Where(entry => entry[0].GetString() == "claim" && entry[1].ToString() == "0")
            .ToList();
        Assert.NotEmpty(claims);
        Assert.All(claims, entry => Assert.Equal(JsonValueKind.Null, entry[2].ValueKind));
    }

    private sealed record HumanTable(
        string GameId, IChangshaGameRuntime Runtime, ChangshaGameState State, WsPeer Peer) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Peer.DisposeAsync();
    }

    private sealed class WsPeer(WebSocket socket, string gameId) : IAsyncDisposable
    {
        public string PlayerId { get; private set; } = string.Empty;

        public async Task SendUpdateAsync(object[] entries)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "UPDATE", entries });
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<JsonElement> JoinBarrierAsync()
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "JOIN", gameId });
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var joined = false;
            while (true)
            {
                var envelope = await ReadAsync(deadline.Token);
                var type = envelope.GetProperty("type").GetString();
                if (type == "JOINED")
                {
                    PlayerId = envelope.GetProperty("playerId").GetString()
                        ?? throw new InvalidOperationException("JOINED omitted the signed player identity.");
                    joined = true;
                }
                else if (joined && type == "UPDATE" && envelope.GetProperty("full").GetBoolean())
                {
                    return envelope;
                }
            }
        }

        private async Task<JsonElement> ReadAsync(CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            var text = new StringBuilder();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer, ct);
                Assert.NotEqual(WebSocketMessageType.Close, received.MessageType);
                text.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
            } while (!received.EndOfMessage);
            using var document = JsonDocument.Parse(text.ToString());
            return document.RootElement.Clone();
        }

        public ValueTask DisposeAsync()
        {
            socket.Abort();
            socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
