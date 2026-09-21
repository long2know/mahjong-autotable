using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonBaseUnitIntegrationTests(ITestOutputHelper output)
{
    private const int MaximumBaseUnit = 11_184_810;

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("", 1)]
    [InlineData("&baseUnit=1", 1)]
    [InlineData("&baseUnit=7", 7)]
    [InlineData("&baseUnit=11184810", MaximumBaseUnit)]
    public async Task AuthenticatedWsCreation_PublishesNumericDefaultOrConfiguredUnit(string query, int expected)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(extraQuery: query);
        Assert.Equal(MaximumBaseUnit, ChangshaBaseUnit.MaxValue);
        Assert.Equal(expected, table.State.BaseUnit);
        Assert.Equal(expected, WireBaseUnit(await table.Peer.BarrierAsync()));
        Assert.Equal(ChangshaPhase.Seating, table.State.Phase);
        Assert.Equal(table.Peer.PlayerId, table.State.CreatorPlayerId);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(MaximumBaseUnit)]
    public async Task AuthenticatedWsSelfHu_ScalesEveryCanonicalPaymentAndFourSeatTotal(int baseUnit)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(
            extraQuery: $"&baseUnit={baseUnit.ToString(CultureInfo.InvariantCulture)}", ephemeralKinds: ["ownTurn"]);
        await host.AdvanceToDrawAsync(table, ArrangeBeforeDraw(table, "hu-14"));
        var before = await table.Peer.BarrierAsync();
        Assert.Equal(baseUnit, WireBaseUnit(before));
        var action = Entry(before, "ownTurn", "0");
        Assert.True(action.GetProperty("hu").GetBoolean());

        await table.Peer.UpdateAsync([new object[] { "ownTurn", 0, new
        {
            action = "hu",
            gameId = action.GetProperty("gameId").GetString(),
            expectedVersion = action.GetProperty("stateVersion").GetInt32()
        } }]);
        var after = await table.Peer.BarrierAsync();
        Assert.Equal(ChangshaPhase.GameComplete, table.State.Phase);
        Assert.True(table.State.IsGameComplete);
        Assert.Equal(baseUnit, WireBaseUnit(after));
        Assert.Equal(WinPattern.Standard, table.State.CurrentWin!.Pattern);
        Assert.Equal(WinMethod.SelfDraw, table.State.CurrentWin.Method);
        Assert.Equal(ScoreCategory.SmallWin, table.State.CurrentScore!.Category);
        Assert.Equal(6 * baseUnit, table.State.CurrentScore.BasePoints);
        Assert.Equal(3, table.State.CurrentScore.Payments.Count);
        Assert.Equal(new[] { 1, 2, 3 }, table.State.CurrentScore.Payments.Select(payment => payment.FromSeatIndex).OrderBy(seat => seat));
        Assert.All(table.State.CurrentScore.Payments, payment =>
        {
            Assert.Equal(0, payment.ToSeatIndex);
            Assert.Equal(2 * baseUnit, payment.Amount);
        });
        Assert.Equal(6 * baseUnit, table.State.CumulativeScores[0]);
        Assert.All(new[] { 1, 2, 3 }, seat => Assert.Equal(-2 * baseUnit, table.State.CumulativeScores[seat]));
        Assert.Equal(0L, table.State.CumulativeScores.Values.Sum(value => (long)value));
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnTurnSettlementOverflow_IsExplicitlyRejectedWithoutPartialMutation(bool winnerOverflow)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var spectator = await host.ConnectAsync(table.RoomId);
        var draw = ArrangeBeforeDraw(table, "hu-14");
        table.State.CumulativeScores = winnerOverflow
            ? new Dictionary<int, int> { [0] = int.MaxValue, [1] = -int.MaxValue, [2] = 0, [3] = 0 }
            : new Dictionary<int, int> { [0] = 1, [1] = int.MinValue, [2] = int.MaxValue, [3] = 0 };
        await host.AdvanceToDrawAsync(table, draw);
        var available = Entry(await table.Peer.BarrierAsync(), "ownTurn", "0");
        Assert.True(available.GetProperty("hu").GetBoolean());
        Assert.Equal(0L, table.State.CumulativeScores.Values.Sum(value => (long)value));
        await using var observer = await host.ConnectHubAsync(table.Peer.PlayerId);
        var successEvents = new ConcurrentQueue<JsonElement>();
        var discards = new ConcurrentQueue<JsonElement>();
        var fullStates = Channel.CreateUnbounded<JsonElement>();
        using var fullSubscription = observer.On<JsonElement>("FullState", payload => fullStates.Writer.TryWrite(payload));
        using var discardSubscription = observer.On<JsonElement>("TileDiscarded", payload => discards.Enqueue(payload));
        using var winSubscription = observer.On<JsonElement>("WinDeclared", payload => RecordSuccess("WinDeclared", payload));
        using var scoreSubscription = observer.On<JsonElement>("ScoringComplete", payload => RecordSuccess("ScoringComplete", payload));
        using var handSubscription = observer.On<JsonElement>("HandFinished", payload => RecordSuccess("HandFinished", payload));
        using var completeSubscription = observer.On<JsonElement>("GameCompleted", payload => RecordSuccess("GameCompleted", payload));
        using var endedSubscription = observer.On<JsonElement>("GameEnded", payload => RecordSuccess("GameEnded", payload));
        await SignalRFenceAsync();
        Assert.Empty(successEvents);
        Assert.Empty(discards);
        var before = await host.SnapshotAsync(table);
        var persistedBefore = await ReadPersistedAsync();
        Assert.Equal(before.StateVersion, persistedBefore.StateVersion);
        var persistedState = JsonSerializer.Deserialize<ChangshaGameState>(persistedBefore.StateJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(persistedState);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(persistedState));
        Assert.Equal(table.GameId, available.GetProperty("gameId").GetString());
        Assert.Equal(before.StateVersion, available.GetProperty("stateVersion").GetInt32());
        await spectator.BarrierAsync();
        var mark = table.Peer.Frames.Count;
        var spectatorMark = spectator.Frames.Count;
        await table.Peer.UpdateAsync([new object[] { "ownTurn", 0, new
        {
            action = "hu", gameId = table.GameId, expectedVersion = before.StateVersion
        } }]);
        await table.Peer.BarrierAsync();
        var delivered = table.Peer.Frames.Skip(mark).ToArray();
        var errorFrame = Assert.Single(delivered, frame => Entries(frame)
            .Any(entry => entry[0].GetString() == "actionRejected"));
        Assert.False(errorFrame.GetProperty("full").GetBoolean());
        var error = Assert.Single(Entries(errorFrame));
        Assert.Equal("actionRejected", error[0].GetString());
        Assert.Equal("current", error[1].GetString());
        Assert.Equal(new[] { "action", "ownedSeat", "reason", "requestedSeat" },
            error[2].EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal("hu", error[2].GetProperty("action").GetString());
        Assert.Equal("score-overflow", error[2].GetProperty("reason").GetString());
        Assert.Equal(0, error[2].GetProperty("requestedSeat").GetInt32());
        Assert.Equal(0, error[2].GetProperty("ownedSeat").GetInt32());
        var rejectionIndex = Array.FindIndex(delivered, frame => Entries(frame)
            .Any(entry => entry[0].GetString() == "actionRejected"));
        var joinedIndex = Array.FindIndex(delivered, frame => frame.GetProperty("type").GetString() == "JOINED");
        Assert.True(joinedIndex > rejectionIndex);
        var corrective = delivered.Skip(rejectionIndex + 1).Take(joinedIndex - rejectionIndex - 1)
            .Where(frame => frame.GetProperty("type").GetString() == "UPDATE"
                && frame.TryGetProperty("full", out var full) && full.GetBoolean()).ToArray();
        Assert.NotEmpty(corrective);
        var correctedAction = Entry(corrective[^1], "ownTurn", "0");
        Assert.Equal(table.GameId, correctedAction.GetProperty("gameId").GetString());
        Assert.Equal(before.StateVersion, correctedAction.GetProperty("stateVersion").GetInt32());
        Assert.DoesNotContain(delivered.SelectMany(Entries), entry =>
            entry[0].GetString() is "result" or "gameComplete" && entry[2].ValueKind != JsonValueKind.Null);
        await spectator.BarrierAsync();
        Assert.DoesNotContain(spectator.Frames.Skip(spectatorMark).SelectMany(Entries),
            entry => entry[0].GetString() == "actionRejected");
        await SignalRFenceAsync();
        Assert.Empty(successEvents);
        Assert.Empty(discards);
        var after = await host.SnapshotAsync(table);
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.Null(after.CurrentWin);
        Assert.Null(after.CurrentScore);
        Assert.False(after.IsGameComplete);
        Assert.Equal(0L, after.CumulativeScores.Values.Sum(value => (long)value));
        AssertInventory(after);
        var persistedAfter = await ReadPersistedAsync();
        Assert.Equal(persistedBefore, persistedAfter);
        output.WriteLine($"Atomic Hu rejection game={table.GameId} winnerOverflow={winnerOverflow} version={after.StateVersion}; senderOnly=true successEvents=0 persistedUnchanged=true");

        const int continuedDiscard = 44;
        Assert.Contains(continuedDiscard, after.Hands[0].ConcealedTiles);
        Assert.Empty(new ClaimAdjudicator().GetOpportunities(0, continuedDiscard, after.Hands));
        await table.Peer.UpdateAsync([new object[] { "discard", 0, new { tileId = continuedDiscard } }]);
        await table.Peer.BarrierAsync();
        await SignalRFenceAsync();
        var discarded = Assert.Single(discards);
        Assert.Equal(table.GameId, discarded.GetProperty("gameId").GetString());
        Assert.Equal(0, discarded.GetProperty("seatIndex").GetInt32());
        Assert.Equal(continuedDiscard, discarded.GetProperty("tileId").GetInt32());
        Assert.Empty(successEvents);
        var continued = await host.SnapshotAsync(table);
        Assert.True(continued.StateVersion > after.StateVersion);
        Assert.Equal(1, continued.ActiveSeatIndex);
        Assert.Equal(1, continued.LastDrawSeatIndex);
        Assert.Equal(after.Wall.Count - 1, continued.Wall.Count);
        Assert.Equal(after.Wall[0], continued.Hands[1].ConcealedTiles[^1]);
        Assert.DoesNotContain(continuedDiscard, continued.Hands[0].ConcealedTiles);
        Assert.Contains(continued.DiscardPile, discard => discard.SeatIndex == 0 && discard.TileId == continuedDiscard);
        Assert.Equal(after.CumulativeScores.OrderBy(pair => pair.Key), continued.CumulativeScores.OrderBy(pair => pair.Key));
        AssertInventory(continued);
        output.WriteLine($"Socket continued with real discard={continuedDiscard}; nextSeat=1 nextDraw={after.Wall[0]} version={continued.StateVersion}; groupObserverReceivedDiscard=true");

        void RecordSuccess(string eventType, JsonElement payload) =>
            successEvents.Enqueue(JsonSerializer.SerializeToElement(new { eventType, payload }));

        async Task SignalRFenceAsync()
        {
            // FullState fences earlier group messages on the same observer connection.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await observer.InvokeCoreAsync<JsonElement>("JoinTable", [table.GameId], timeout.Token);
            var full = await fullStates.Reader.ReadAsync(timeout.Token);
            Assert.Equal(table.GameId, full.GetProperty("gameId").GetString());
        }

        async Task<(string StateJson, int StateVersion, DateTime UpdatedUtc)> ReadPersistedAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameId = Guid.Parse(table.GameId);
            var row = await db.ChangshaGames.AsNoTracking().SingleAsync(row => row.Id == gameId, timeout.Token);
            return (row.StateJson, row.StateVersion, row.UpdatedUtc);
        }

        static IEnumerable<JsonElement> Entries(JsonElement frame) =>
            frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task FirstCreatorUnit_IsLatchedAcrossLaterSeatsReconnectAndClientConfigWrites()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(extraQuery: "&baseUnit=7", ephemeralKinds: ["ownTurn"]);
        await using var later = await host.ConnectAsync(table.RoomId, extraQuery: "&baseUnit=3");
        Assert.Equal(7, WireBaseUnit(await later.BarrierAsync()));
        await later.UpdateAsync([new object[] { "seats", later.PlayerId, new { seat = 1 } }]);
        await later.BarrierAsync();
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(table.GameId, later.PlayerId));
        Assert.Equal(7, table.State.BaseUnit);
        await host.AdvanceToDrawAsync(table, ArrangeBeforeDraw(table, "hu-14"));
        var stateVersion = table.State.StateVersion;
        await table.Peer.UpdateAsync([new object[]
        {
            "match", 0, new { dealer = 0, conditions = new { variant = "changsha", baseUnit = 99 } }
        }]);
        Assert.Equal(7, WireBaseUnit(await table.Peer.BarrierAsync()));
        Assert.Equal(stateVersion, table.State.StateVersion);
        await table.Peer.DisposeAsync();
        await using var reconnect = await host.ConnectAsync(
            table.RoomId, table.Peer.PlayerId, extraQuery: "&baseUnit=11", ephemeralKinds: ["ownTurn"]);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(table.GameId, reconnect.PlayerId));
        Assert.Equal(table.GameId, host.Manager.GetRuntimeGameIdBoundTo(table.RoomId));
        Assert.Equal(7, table.State.BaseUnit);
        Assert.Equal(7, WireBaseUnit(await reconnect.BarrierAsync()));
        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(1, host.Runtime.GameCount);
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("baseUnit=0")]
    [InlineData("baseUnit=-1")]
    [InlineData("baseUnit=11184811")]
    [InlineData("baseUnit=2147483647")]
    [InlineData("baseUnit=2147483648")]
    [InlineData("baseUnit=1.5")]
    [InlineData("baseUnit=1e2")]
    [InlineData("baseUnit=NaN")]
    [InlineData("baseUnit=")]
    [InlineData("baseUnit=%202")]
    [InlineData("baseUnit=%2B2")]
    [InlineData("baseUnit=1&baseUnit=2")]
    public async Task InvalidWsUnit_IsExplicitlyClosedBeforeAnyGameIsCreated(string query)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        var room = $"hudson-invalid-unit-{Guid.NewGuid():N}";
        var before = host.Runtime.GameCount;
        using var socket = await host.OpenSocketAsync(
            $"variant=changsha&bots=false&botCount=0&gameId={room}&{query}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[4096]), timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, response.MessageType);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, response.CloseStatus);
        Assert.Equal($"baseUnit must be an integer from 1 to {MaximumBaseUnit}.", response.CloseStatusDescription);
        output.WriteLine($"WS rejected {query}: {response.CloseStatus} / {response.CloseStatusDescription}");
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "observed-policy-rejection", timeout.Token);
        Assert.Equal(before, host.Runtime.GameCount);
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(room));
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task SignedSignalRCreation_PreservesThreeArgumentLegacyRpcAndAddsExplicitConfigRpc()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        var player = $"hudson-unit-rpc-{Guid.NewGuid():N}";
        await using var connection = await host.ConnectHubAsync(player);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var legacy = await connection.InvokeCoreAsync<JsonElement>(
            "CreateGame", ["changsha-v1", Array.Empty<int>(), 20260912], timeout.Token);
        var configured = await connection.InvokeCoreAsync<JsonElement>("CreateGameWithConfig",
            [new { ruleSet = "changsha-v1", botSeatIndexes = Array.Empty<int>(), seed = 20260912, baseUnit = 7 }], timeout.Token);
        var defaultConfig = await connection.InvokeCoreAsync<JsonElement>("CreateGameWithConfig",
            [new { ruleSet = "changsha-v1", botSeatIndexes = Array.Empty<int>(), seed = 20260912 }], timeout.Token);
        foreach (var (result, expected) in new[] { (legacy, 1), (configured, 7), (defaultConfig, 1) })
        {
            var game = result.GetProperty("gameId").GetString();
            Assert.False(string.IsNullOrEmpty(game));
            Assert.Equal(expected, result.GetProperty("baseUnit").GetInt32());
            Assert.True(host.Runtime.TryGetSnapshot(game!, out var snapshot));
            Assert.Equal(expected, snapshot!.BaseUnit);
            Assert.Equal(player, snapshot.CreatorPlayerId);
            Assert.All(snapshot.Seats, seat => Assert.False(seat.IsBot));
            output.WriteLine($"SignalR created gameId={game} baseUnit={expected} owner={player}");
        }
        Assert.Equal(3, host.Runtime.GameCount);
        Assert.Equal(3, new[] { legacy, configured, defaultConfig }.Select(result => result.GetProperty("gameId").GetString()).Distinct().Count());
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MaximumBaseUnit + 1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task InvalidSignalRUnit_FailsWithoutCreatingGameAndConnectionRemainsUsable(int baseUnit)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var connection = await host.ConnectHubAsync($"hudson-unit-rpc-{Guid.NewGuid():N}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var before = host.Runtime.GameCount;
        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeCoreAsync<JsonElement>(
            "CreateGameWithConfig", [new { baseUnit, botSeatIndexes = Array.Empty<int>() }], timeout.Token));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Equal(before, host.Runtime.GameCount);
        var valid = await connection.InvokeCoreAsync<JsonElement>("CreateGameWithConfig",
            [new { baseUnit = 1, botSeatIndexes = Array.Empty<int>() }], timeout.Token);
        Assert.Equal(1, valid.GetProperty("baseUnit").GetInt32());
        Assert.Equal(before + 1, host.Runtime.GameCount);
        output.WriteLine($"SignalR rejected baseUnit={baseUnit}: {error.Message}");
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(MaximumBaseUnit)]
    public async Task PersistedWsCreatedUnit_SurvivesFactoryRestartAndSignedRuntimeReconnect(int baseUnit)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        // This case needs its own seed; the shared OpenHumanTableAsync helper pins 20260912.
        var room = $"base-unit-restart-{Guid.NewGuid():N}";
        var owner = $"test-owner-{Guid.NewGuid():N}";
        await using var peer = new WsPeer(await host.OpenSocketAsync(
            $"variant=changsha&bots=false&botCount=0&dealMode=manual&handCount=1&seed=59716&baseUnit={baseUnit.ToString(CultureInfo.InvariantCulture)}",
            owner), room, owner, output);
        await peer.BarrierAsync();
        await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "gameComplete", "ownTurn" }
            .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
        await peer.BarrierAsync();
        await peer.UpdateAsync([new object[] { "seats", owner, new { seat = 2 } }]);
        await peer.BarrierAsync();
        var gameId = host.Manager.GetRuntimeGameIdBoundTo(room)
            ?? throw new InvalidOperationException("Normal authenticated seat did not bind a runtime.");
        Assert.Equal(2, host.Runtime.TryGetSeatForPlayer(gameId, owner));
        Assert.True(host.Runtime.TryGetSnapshot(gameId, out var state));
        Assert.NotNull(state);
        Assert.All(state.Seats, seat => Assert.False(seat.IsBot));
        var table = new HumanTable(room, gameId, 2, state, peer);
        await PrepareRecordedRestartHandAsync(host.Runtime, table.GameId);
        var ready = await table.Peer.BarrierAsync();
        Assert.Equal(baseUnit, WireBaseUnit(ready));
        Assert.True(Entry(ready, "ownTurn", "2").GetProperty("hu").GetBoolean());
        Assert.Equal(table.Peer.PlayerId, table.State.CreatorPlayerId);
        int[] expectedTiles = [87, 85, 67, 78, 66, 81, 86, 75];
        Assert.Equal(expectedTiles, table.State.Hands[2].ConcealedTiles);
        Assert.Equal(new[] { 12, 16, 20 }, table.State.Hands[2].Melds[0].TileIds);
        Assert.Equal(new[] { 21, 27, 30 }, table.State.Hands[2].Melds[1].TileIds);
        Assert.All(table.State.Hands[2].Melds, meld =>
        {
            Assert.Equal(MeldKind.Chow, meld.Kind);
            Assert.Equal(1, meld.ClaimedFromSeatIndex);
        });
        var expectedState = JsonSerializer.Serialize(table.State);
        var game = Guid.Parse(table.GameId);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.ChangshaGames.AsNoTracking().SingleAsync(row => row.Id == game);
            using var persisted = JsonDocument.Parse(row.StateJson);
            Assert.Equal(baseUnit, persisted.RootElement.GetProperty("baseUnit").GetInt32());
            Assert.Equal(table.State.StateVersion, row.StateVersion);
            Assert.Equal(table.Peer.PlayerId, row.OwnerPlayerId);
            var stored = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.NotNull(stored);
            Assert.Equal(expectedState, JsonSerializer.Serialize(stored));
            var binding = await db.AutotableRoomBindings.AsNoTracking()
                .SingleAsync(binding => binding.RoomId == table.RoomId);
            Assert.Equal(game, binding.RuntimeGameId);
        }
        await table.Peer.DisposeAsync();
        await host.RestartAsync();
        Assert.True(host.Runtime.TryGetSnapshot(table.GameId, out var hydrated));
        Assert.NotNull(hydrated);
        Assert.Equal(baseUnit, hydrated.BaseUnit);
        Assert.Equal(table.Peer.PlayerId, hydrated.CreatorPlayerId);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, hydrated.Phase);
        Assert.Equal(2, hydrated.ActiveSeatIndex);
        Assert.Equal(2, hydrated.LastDrawSeatIndex);
        Assert.Equal(expectedTiles, hydrated.Hands[2].ConcealedTiles);
        Assert.Equal(2, hydrated.Hands[2].Melds.Count);
        Assert.Equal(table.Peer.PlayerId, hydrated.Seats[2].PlayerId);
        Assert.Equal(expectedState, JsonSerializer.Serialize(hydrated));
        Assert.True(ChangshaOwnTurnActions.Available(hydrated, 2)?.Hu);
        AssertInventory(hydrated);

        // The room alias is durable; signed runtime reconnect separately restores the seat connection.
        await using var reconnect = await host.ConnectHubAsync(table.Peer.PlayerId);
        var full = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = reconnect.On<JsonElement>("FullState", state => full.TrySetResult(state));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await reconnect.InvokeCoreAsync<JsonElement>(
            "ReconnectGame", [table.GameId, 2], timeout.Token);
        Assert.True(result.GetProperty("success").GetBoolean());
        var received = await full.Task.WaitAsync(timeout.Token);
        Assert.Equal(table.GameId, received.GetProperty("gameId").GetString());
        Assert.Equal(baseUnit, received.GetProperty("baseUnit").GetInt32());
        Assert.Equal(2, host.Runtime.TryGetSeatForPlayer(table.GameId, table.Peer.PlayerId));
        output.WriteLine($"Persisted/hydrated/reconnected gameId={table.GameId} baseUnit={baseUnit} ownerSeat=2");
    }

    internal static async Task PrepareRecordedRestartHandAsync(IChangshaGameRuntime runtime, string gameId)
    {
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        Assert.NotNull(state);
        Assert.Equal(59716, state.Seed);
        Assert.Equal(DealMode.Manual, state.DealMode);
        Assert.Equal(1, state.MaxHands);
        using var setup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await runtime.StartGameAsync(gameId, setup.Token);
        await runtime.RollDiceAsync(gameId, state.DealerSeatIndex, setup.Token);
        for (var pickup = 0; pickup < 17; pickup++)
        {
            Assert.True(ChangshaGameStateMachine.IsPickupPhase(state.Phase));
            await runtime.TakeTilesFromWallAsync(gameId, state.PickupSeatIndex!.Value,
                ChangshaGameStateMachine.ExpectedPickupCount(state.Phase), setup.Token);
        }
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        await runtime.AcknowledgeDealAsync(gameId, 2, setup.Token);

        // Seed 59716 reaches two actual Chows and an own-draw Hu in 10 discards,
        // reducing this preparation from 49 to 39 recorded persistence calls.
        int[] discards =
        [
            5, 12, 47, 2, 11, 21, 74, 39, 8, 0
        ];
        for (var index = 0; index < discards.Length; index++)
        {
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
            Assert.Equal(index % 4, state.ActiveSeatIndex);
            await runtime.DiscardAsync(gameId, index % 4, discards[index], setup.Token);
            var window = state.ClaimWindow;
            if (window is null) continue;
            int[]? chowPartners = discards[index] switch
            {
                12 => [16, 20],
                21 => [27, 30],
                _ => null
            };
            if (chowPartners is not null)
                Assert.Contains(window.Opportunities,
                    opportunity => opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Chow);
            var passingSeats = window.Opportunities.Select(opportunity => opportunity.SeatIndex)
                .Distinct().Where(seat => chowPartners is null || seat != 2).OrderBy(seat => seat).ToArray();
            foreach (var seat in passingSeats)
            {
                Assert.Same(window, state.ClaimWindow);
                await runtime.PassAsync(gameId, seat, setup.Token);
            }
            if (chowPartners is not null)
            {
                Assert.Same(window, state.ClaimWindow);
                await runtime.ClaimAsync(gameId, 2, "Chow", chowPartners, setup.Token);
            }
            Assert.Null(state.ClaimWindow);
        }
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(2, state.ActiveSeatIndex);
        Assert.Equal(2, state.LastDrawSeatIndex);
        Assert.Equal(8, state.Hands[2].ConcealedTiles.Count);
        Assert.Equal(2, state.Hands[2].Melds.Count);
        Assert.True(ChangshaOwnTurnActions.Available(state, 2)?.Hu);
        AssertInventory(state);
    }

    private static int WireBaseUnit(JsonElement snapshot)
    {
        var value = Entry(snapshot, "match", "0").GetProperty("conditions").GetProperty("baseUnit");
        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        return value.GetInt32();
    }

    private static JsonElement Entry(JsonElement snapshot, string kind, string key)
    {
        var matches = snapshot.GetProperty("entries").EnumerateArray()
            .Where(entry => entry[0].GetString() == kind && entry[1].ToString() == key).ToArray();
        Assert.NotEmpty(matches);
        return matches[^1][2].Clone();
    }
}
