using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonClaimSettlementOverflowIntegrationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ClaimHuOverflow_PreservesWholeCommandAndWindow_ThenPassOrTimeoutContinues(
        bool robbingKong, bool winnerOverflow)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        await using var scene = await OpenClaimAsync(host, robbingKong, winnerOverflow);
        await using var signals = await SuccessSignals.OpenAsync(host, scene);
        var before = await SnapshotAsync(host, scene);
        var beforeStored = await PersistedAsync(host, scene);
        AssertPersistedMatches(before, beforeStored.StateJson);
        var instance = Instance(host, scene.GameId);
        var beforeWindow = await WindowStateAsync(instance);
        Assert.Equal(0, beforeWindow.PendingCount);
        var context = ClaimContext(await scene.Winner.BarrierAsync());
        var sourceMark = scene.Source.Frames.Count;
        var winnerMark = scene.Winner.Frames.Count;
        var continued = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Observe(string game, ChangshaGameState state)
        {
            if (game == scene.GameId && state.Phase == ChangshaPhase.AwaitingDiscard
                && state.ClaimWindow is null && state.CurrentWin is null
                && state.ActiveSeatIndex == (robbingKong ? 0 : 1)
                && state.LastDrawSeatIndex == state.ActiveSeatIndex)
                continued.TrySetResult(state);
        }

        host.Runtime.StateChanged += Observe;
        try
        {
            await SendClaimAsync(scene.Winner, context, pass: false);
            await scene.Winner.BarrierAsync();
            var frames = scene.Winner.Frames.Skip(winnerMark).ToArray();
            var errorFrame = Assert.Single(frames, frame => Entries(frame)
                .Any(entry => entry[0].GetString() == "actionRejected"));
            Assert.False(errorFrame.GetProperty("full").GetBoolean());
            var error = Assert.Single(Entries(errorFrame));
            Assert.Equal("actionRejected", error[0].GetString());
            Assert.Equal("current", error[1].GetString());
            Assert.Equal(new[] { "action", "ownedSeat", "reason", "requestedSeat" },
                error[2].EnumerateObject().Select(property => property.Name).OrderBy(name => name));
            Assert.Equal("claim", error[2].GetProperty("action").GetString());
            Assert.Equal("score-overflow", error[2].GetProperty("reason").GetString());
            Assert.Equal(1, error[2].GetProperty("requestedSeat").GetInt32());
            Assert.Equal(1, error[2].GetProperty("ownedSeat").GetInt32());
            var rejectedAt = Array.FindIndex(frames, frame => Entries(frame)
                .Any(entry => entry[0].GetString() == "actionRejected"));
            var joinedAt = Array.FindIndex(frames, frame => frame.GetProperty("type").GetString() == "JOINED");
            Assert.True(joinedAt > rejectedAt);
            var resync = frames.Skip(rejectedAt + 1).Take(joinedAt - rejectedAt - 1).Where(IsFull).ToArray();
            Assert.NotEmpty(resync);
            Assert.Equal(before.StateVersion, ClaimContext(resync[^1]).GetProperty("stateVersion").GetInt32());
            Assert.DoesNotContain(frames.SelectMany(Entries), entry =>
                entry[0].GetString() is "result" or "gameComplete" && entry[2].ValueKind != JsonValueKind.Null);
            await scene.Source.BarrierAsync();
            Assert.DoesNotContain(scene.Source.Frames.Skip(sourceMark).SelectMany(Entries),
                entry => entry[0].GetString() == "actionRejected");
            await signals.FenceAsync();
            Assert.Empty(signals.Events);
            var after = await SnapshotAsync(host, scene);
            Assert.Equal(before.StateVersion, after.StateVersion);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
            Assert.Null(after.CurrentWin);
            Assert.Null(after.CurrentScore);
            Assert.Empty(after.FalseHuPenalties);
            AssertInventory(after);
            Assert.Equal(beforeStored, await PersistedAsync(host, scene));
            var rejectedWindow = await WindowStateAsync(instance);
            Assert.Same(beforeWindow.Window, rejectedWindow.Window);
            Assert.Same(beforeWindow.Timer, rejectedWindow.Timer);
            Assert.Equal(beforeWindow.PendingJson, rejectedWindow.PendingJson);
            Assert.Equal(0, rejectedWindow.PendingCount);
            output.WriteLine($"Atomic {(robbingKong ? "rob-Kong" : "discard")} Hu {(winnerOverflow ? "winner-overflow" : "payer-underflow")} game={scene.GameId}: exact score-overflow, live/persisted state identical, pending=0, same uncancelled timer, no success events.");

            if (winnerOverflow)
            {
                await SendClaimAsync(scene.Winner, ClaimContext(await scene.Winner.BarrierAsync()), pass: true);
                await scene.Winner.BarrierAsync();
            }
            // The untouched shared fixture uses a real 30-second claim timer.
            await continued.Task.WaitAsync(TimeSpan.FromSeconds(35));
            await scene.Winner.BarrierAsync();
            await signals.FenceAsync();
            var settled = await SnapshotAsync(host, scene);
            Assert.Null(settled.ClaimWindow);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, settled.Phase);
            Assert.True(settled.StateVersion > before.StateVersion);
            Assert.Null(settled.CurrentWin);
            Assert.Null(settled.CurrentScore);
            Assert.False(settled.IsGameComplete);
            Assert.Empty(settled.FalseHuPenalties);
            Assert.Equal(before.CumulativeScores.OrderBy(pair => pair.Key),
                settled.CumulativeScores.OrderBy(pair => pair.Key));
            Assert.Equal(0L, settled.CumulativeScores.Values.Sum(score => (long)score));
            Assert.Empty(signals.Events);
            if (robbingKong)
            {
                Assert.Equal(0, settled.ActiveSeatIndex);
                Assert.Equal(MeldKind.AddedKong, Assert.Single(settled.Hands[0].Melds).Kind);
                Assert.DoesNotContain(19, settled.Hands[0].ConcealedTiles);
                Assert.Equal(new[] { 16, 17, 18, 19 }, settled.Hands[0].Melds[0].TileIds.OrderBy(tile => tile));
                Assert.Equal(before.Wall.Take(before.Wall.Count - 1), settled.Wall);
                Assert.Equal(before.Wall[^1], settled.Hands[0].ConcealedTiles[^1]);
                Assert.Equal(before.WallBackDrawn + 1, settled.WallBackDrawn);
                Assert.Equal(before.WallDrawIndex, settled.WallDrawIndex);
            }
            else
            {
                Assert.Equal(1, settled.ActiveSeatIndex);
                Assert.Equal(before.Wall.Skip(1), settled.Wall);
                Assert.Equal(before.Wall[0], settled.Hands[1].ConcealedTiles[^1]);
                Assert.Equal(before.WallBackDrawn, settled.WallBackDrawn);
            }
            AssertInventory(settled);
            AssertPersistedMatches(settled, (await PersistedAsync(host, scene)).StateJson);
            output.WriteLine($"Rejected {(robbingKong ? "rob-Kong" : "discard")} Hu later {(winnerOverflow ? "normal same-socket pass" : "actual untouched timer expiry")} completed; nextActor={settled.ActiveSeatIndex}, version={settled.StateVersion}, scores unchanged, inventory=108.");
        }
        finally
        {
            host.Runtime.StateChanged -= Observe;
        }
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentClaimHu_SettlesNormallyAndTransfersTheWinningTileExactlyOnce(bool robbingKong)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        await using var scene = await OpenClaimAsync(host, robbingKong, winnerOverflow: null);
        await using var signals = await SuccessSignals.OpenAsync(host, scene);
        var before = await SnapshotAsync(host, scene);
        var mark = scene.Winner.Frames.Count;
        await SendClaimAsync(scene.Winner, ClaimContext(await scene.Winner.BarrierAsync()), pass: false);
        await scene.Winner.BarrierAsync();
        await signals.FenceAsync();
        var after = await SnapshotAsync(host, scene);
        Assert.Equal(ChangshaPhase.GameComplete, after.Phase);
        Assert.True(after.IsGameComplete);
        Assert.True(after.StateVersion > before.StateVersion);
        Assert.Equal(1, after.CurrentWin!.WinningSeatIndex);
        Assert.Equal(0, after.CurrentWin.SourceSeatIndex);
        Assert.Equal(robbingKong ? WinMethod.RobbingKong : WinMethod.Discard, after.CurrentWin.Method);
        Assert.Equal(robbingKong, after.CurrentWin.IsRobbedKong);
        Assert.False(after.CurrentWin.IsSelfDraw);
        Assert.False(after.CurrentWin.IsKongReplacement);
        var tile = robbingKong ? 19 : 0;
        Assert.Equal(tile, after.CurrentWin.WinningTileId);
        Assert.Equal(1, after.Hands[1].ConcealedTiles.Count(candidate => candidate == tile));
        Assert.Equal(14, after.Hands[1].ConcealedTiles.Count);
        Assert.DoesNotContain(tile, after.Hands[0].ConcealedTiles);
        Assert.DoesNotContain(after.DiscardPile, discard => discard.TileId == tile);
        Assert.Equal(before.Wall, after.Wall);
        Assert.Equal(before.WallBackDrawn, after.WallBackDrawn);
        if (robbingKong)
        {
            Assert.Equal(MeldKind.Pung, Assert.Single(after.Hands[0].Melds).Kind);
            Assert.Equal(new[] { 16, 17, 18 }, after.Hands[0].Melds[0].TileIds.OrderBy(value => value));
        }
        var amount = robbingKong ? 7 : 2;
        Assert.Equal(amount, after.CurrentScore!.BasePoints);
        var payment = Assert.Single(after.CurrentScore.Payments);
        Assert.Equal(0, payment.FromSeatIndex);
        Assert.Equal(1, payment.ToSeatIndex);
        Assert.Equal(amount, payment.Amount);
        Assert.Equal(new[] { -amount, amount, 0, 0 }, Enumerable.Range(0, 4).Select(seat => after.CumulativeScores[seat]));
        Assert.Equal(0, after.CumulativeScores.Values.Sum());
        AssertSingleHuClaimMade(signals.Claims, scene.GameId, 1, tile);
        Assert.Contains("ScoringComplete", signals.Events);
        Assert.Contains("GameCompleted", signals.Events);
        Assert.DoesNotContain(scene.Winner.Frames.Skip(mark).SelectMany(Entries),
            entry => entry[0].GetString() == "actionRejected");
        AssertInventory(after);
        AssertPersistedMatches(after, (await PersistedAsync(host, scene)).StateJson);
        output.WriteLine($"Normal {(robbingKong ? "rob-Kong" : "discard")} Hu game={scene.GameId} settled exact payment0->1={amount}, tile={tile} transferred once, no extra draw, inventory=108.");
    }

    private static void AssertSingleHuClaimMade(
        IEnumerable<JsonElement> notifications, string game, int winner, int tile)
    {
        var claim = Assert.Single(notifications);
        Assert.Equal(JsonValueKind.Object, claim.ValueKind);
        Assert.True(claim.TryGetProperty("gameId", out var gameId));
        Assert.Equal(JsonValueKind.String, gameId.ValueKind);
        Assert.Equal(game, gameId.GetString());
        Assert.True(claim.TryGetProperty("claimingSeatIndex", out var seat));
        Assert.Equal(JsonValueKind.Number, seat.ValueKind);
        Assert.True(seat.TryGetInt32(out var claimingSeat));
        Assert.Equal(winner, claimingSeat);
        Assert.True(claim.TryGetProperty("claimType", out var type));
        Assert.Equal(JsonValueKind.String, type.ValueKind);
        Assert.Equal("hu", type.GetString());
        Assert.True(claim.TryGetProperty("tileId", out var tileId));
        Assert.Equal(JsonValueKind.Number, tileId.ValueKind);
        Assert.True(tileId.TryGetInt32(out var physicalTile));
        Assert.Equal(tile, physicalTile);
    }

    private static void CaptureClaimMade(
        JsonElement value, ConcurrentQueue<JsonElement> claims, ConcurrentQueue<string> events)
    {
        claims.Enqueue(value.Clone());
        // A rejected rob-Hu can legitimately finish its added Kong after pass.
        // Retain every claim payload; Hu or malformed/unknown claims are success
        // signals for the unchanged negative settlement assertions.
        var ordinaryMeld = value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("claimType", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() is "pung" or "chow" or "kong";
        if (!ordinaryMeld) events.Enqueue("ClaimMade");
    }

    private static JsonElement ClaimMadePayload(
        string game = "claim-oracle", int seat = 1, string type = "hu", int tile = 0) =>
        JsonSerializer.SerializeToElement(new { gameId = game, claimingSeatIndex = seat, claimType = type, tileId = tile });

    [Fact, Trait("Category", "RulesQualificationClaimNotificationOracle")]
    public void ClaimNotificationOracle_AcceptsOneExactHuPayload()
    {
        AssertSingleHuClaimMade([ClaimMadePayload()], "claim-oracle", 1, 0);
    }

    [Theory, Trait("Category", "RulesQualificationClaimNotificationOracle")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("wrong-game")]
    [InlineData("wrong-winner")]
    [InlineData("wrong-physical-tile")]
    [InlineData("wrong-type")]
    [InlineData("wrong-type-case")]
    [InlineData("enum-type")]
    [InlineData("matching-plus-unrelated")]
    [InlineData("wrong-seat-field")]
    public void ClaimNotificationOracle_RejectsMissingDuplicateOrMismatchedPayload(string defect)
    {
        JsonElement[] notifications = defect switch
        {
            "missing" => [],
            "duplicate" => [ClaimMadePayload(), ClaimMadePayload()],
            "wrong-game" => [ClaimMadePayload(game: "different-game")],
            "wrong-winner" => [ClaimMadePayload(seat: 0)],
            "wrong-physical-tile" => [ClaimMadePayload(tile: 1)],
            "wrong-type" => [ClaimMadePayload(type: "kong")],
            "wrong-type-case" => [ClaimMadePayload(type: "Hu")],
            "enum-type" => [JsonSerializer.SerializeToElement(
                new { gameId = "claim-oracle", claimingSeatIndex = 1, claimType = 3, tileId = 0 })],
            "matching-plus-unrelated" => [ClaimMadePayload(), ClaimMadePayload(game: "different-game")],
            "wrong-seat-field" => [JsonSerializer.SerializeToElement(
                new { gameId = "claim-oracle", seatIndex = 1, claimType = "hu", tileId = 0 })],
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertSingleHuClaimMade(notifications, "claim-oracle", 1, 0));
    }

    [Fact, Trait("Category", "RulesQualificationClaimNotificationOracle")]
    public void ClaimObserver_RetainsWrongContextHuForNegativeDetection()
    {
        var claims = new ConcurrentQueue<JsonElement>();
        var events = new ConcurrentQueue<string>();
        CaptureClaimMade(ClaimMadePayload(game: "unexpected-game", seat: 0, tile: 1), claims, events);
        Assert.Equal("ClaimMade", Assert.Single(events));
        Assert.Equal("unexpected-game", Assert.Single(claims).GetProperty("gameId").GetString());
    }

    [Fact, Trait("Category", "RulesQualificationClaimNotificationOracle")]
    public void ClaimObserver_RetainsLegitimateKongWithoutTreatingItAsHuSettlement()
    {
        var claims = new ConcurrentQueue<JsonElement>();
        var events = new ConcurrentQueue<string>();
        CaptureClaimMade(ClaimMadePayload(seat: 0, type: "kong", tile: 19), claims, events);
        Assert.Empty(events);
        Assert.Single(claims);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertSingleHuClaimMade(claims, "claim-oracle", 1, 0));
    }

    [Fact, Trait("Category", "RulesQualificationClaimNotificationOracle")]
    public void ClaimObserver_DoesNotHideMalformedNotifications()
    {
        var claims = new ConcurrentQueue<JsonElement>();
        var events = new ConcurrentQueue<string>();
        CaptureClaimMade(JsonSerializer.SerializeToElement(
            new { gameId = "claim-oracle", claimingSeatIndex = 1, tileId = 0 }), claims, events);
        Assert.Equal("ClaimMade", Assert.Single(events));
        Assert.Single(claims);
    }

    private async Task<ClaimScene> OpenClaimAsync(HudsonOwnTurnWsFixture host, bool robbingKong, bool? winnerOverflow)
    {
        var primary = await host.OpenHumanTableAsync(robbingKong ? 0 : 1, ephemeralKinds: ["ownTurn"]);
        WsPeer? other = null;
        try
        {
            other = await host.ConnectAsync(primary.RoomId, ephemeralKinds: ["ownTurn"]);
            var otherSeat = robbingKong ? 1 : 0;
            await other.UpdateAsync([new object[] { "seats", other.PlayerId, new { seat = otherSeat } }]);
            await other.BarrierAsync();
            var scene = new ClaimScene(primary, other, robbingKong);
            Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(scene.GameId, scene.Source.PlayerId));
            Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(scene.GameId, scene.Winner.PlayerId));
            var prepared = ArrangeBeforeDraw(primary, robbingKong ? "added-kong-rob" : "hu-14");
            if (winnerOverflow.HasValue)
            {
                // Isolate receiver overflow from payer underflow while retaining four-seat zero-sum.
                primary.State.CumulativeScores = winnerOverflow.Value
                    ? new Dictionary<int, int> { [0] = -1_000_000_000, [1] = int.MaxValue, [2] = -1_000_000_000, [3] = -147_483_647 }
                    : new Dictionary<int, int> { [0] = int.MinValue, [1] = 0, [2] = int.MaxValue, [3] = 1 };
            }
            Assert.Equal(0L, primary.State.CumulativeScores.Values.Sum(score => (long)score));
            if (robbingKong)
            {
                await host.AdvanceToDrawAsync(primary, prepared);
                var available = Entry(await scene.Source.BarrierAsync(), "ownTurn", "0");
                Assert.Contains(19, available.GetProperty("addedKongs").EnumerateArray().Select(tile => tile.GetInt32()));
                await scene.Source.UpdateAsync([new object[] { "ownTurn", 0, new
                {
                    gameId = available.GetProperty("gameId").GetString(),
                    expectedVersion = available.GetProperty("stateVersion").GetInt32(),
                    action = "addedKong", tileIds = new[] { 19 }
                } }]);
                await scene.Source.BarrierAsync();
            }
            else
            {
                ArrangeIncomingDiscard(primary, prepared);
                await scene.Source.UpdateAsync([new object[] { "discard", 0, new { tileId = 0 } }]);
                await scene.Source.BarrierAsync();
            }
            var state = await SnapshotAsync(host, scene);
            Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
            Assert.Equal(robbingKong, state.ClaimWindow!.IsKongRobbing);
            Assert.Equal(0, state.ClaimWindow.DiscardSeatIndex);
            Assert.Equal(robbingKong ? 19 : 0, state.ClaimWindow.DiscardTileId);
            Assert.All(state.ClaimWindow.Opportunities, opportunity => Assert.Equal(1, opportunity.SeatIndex));
            var context = ClaimContext(await scene.Winner.BarrierAsync());
            Assert.Equal(scene.GameId, context.GetProperty("gameId").GetString());
            Assert.Equal(state.StateVersion, context.GetProperty("stateVersion").GetInt32());
            Assert.Contains("Hu", context.GetProperty("available").EnumerateArray().Select(value => value.GetString()));
            if (robbingKong)
            {
                Assert.Contains(19, state.Hands[0].ConcealedTiles);
                Assert.Equal(MeldKind.Pung, Assert.Single(state.Hands[0].Melds).Kind);
                Assert.Equal(0, state.WallBackDrawn);
            }
            AssertInventory(state);
            output.WriteLine($"Prepared authenticated {(robbingKong ? "rob-Kong" : "discard")} claim game={scene.GameId}, winner1/source0, v{state.StateVersion}, boundary={winnerOverflow?.ToString() ?? "normal"}, scores={JsonSerializer.Serialize(state.CumulativeScores)}.");
            return scene;
        }
        catch
        {
            if (other is not null) await other.DisposeAsync();
            await primary.DisposeAsync();
            throw;
        }
    }

    private static Task SendClaimAsync(WsPeer peer, JsonElement context, bool pass) =>
        peer.UpdateAsync([new object[] { "claim", "1", new
        {
            gameId = context.GetProperty("gameId").GetString(),
            expectedVersion = context.GetProperty("stateVersion").GetInt32(),
            action = pass ? "pass" : "claim", type = pass ? null : "Hu"
        } }]);

    private static JsonElement ClaimContext(JsonElement frame)
    {
        var value = Entry(frame, "claim", "1");
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        return value;
    }

    private static JsonElement Entry(JsonElement frame, string kind, string key) =>
        Assert.Single(Entries(frame), entry => entry[0].GetString() == kind && entry[1].ToString() == key)[2].Clone();

    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static bool IsFull(JsonElement frame) => frame.GetProperty("type").GetString() == "UPDATE"
        && frame.TryGetProperty("full", out var full) && full.GetBoolean();

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, ClaimScene scene) =>
        await host.Runtime.TryGetSnapshotCopyAsync(scene.GameId)
        ?? throw new InvalidOperationException("The isolated claim game disappeared.");

    private static async Task<(string StateJson, int Version, DateTime UpdatedUtc)> PersistedAsync(
        HudsonOwnTurnWsFixture host, ClaimScene scene)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(scene.GameId);
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == id, timeout.Token);
        return (row.StateJson, row.StateVersion, row.UpdatedUtc);
    }

    private static void AssertPersistedMatches(ChangshaGameState state, string json)
    {
        var stored = JsonSerializer.Deserialize<ChangshaGameState>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(stored);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(stored));
    }

    private static ChangshaGameInstance Instance(HudsonOwnTurnWsFixture host, string game)
    {
        var field = host.Runtime.GetType().GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var games = Assert.IsType<ConcurrentDictionary<string, ChangshaGameInstance>>(field.GetValue(host.Runtime));
        Assert.True(games.TryGetValue(game, out var instance));
        Assert.NotNull(instance);
        return instance;
    }

    private static async Task<(ChangshaClaimWindow Window, CancellationTokenSource Timer, string PendingJson, int PendingCount)>
        WindowStateAsync(ChangshaGameInstance instance)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await instance.Lock.WaitAsync(timeout.Token);
        try
        {
            Assert.NotNull(instance.State.ClaimWindow);
            Assert.NotNull(instance.ClaimWindowCts);
            Assert.False(instance.ClaimWindowCts.IsCancellationRequested);
            return (instance.State.ClaimWindow, instance.ClaimWindowCts,
                JsonSerializer.Serialize(instance.PendingClaims), instance.PendingClaims.Count);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    private sealed record ClaimScene(HumanTable Primary, WsPeer Other, bool RobbingKong) : IAsyncDisposable
    {
        public string GameId => Primary.GameId;
        public WsPeer Source => RobbingKong ? Primary.Peer : Other;
        public WsPeer Winner => RobbingKong ? Other : Primary.Peer;
        public async ValueTask DisposeAsync()
        {
            await Other.DisposeAsync();
            await Primary.DisposeAsync();
        }
    }

    private sealed class SuccessSignals : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly string _game;
        private readonly List<IDisposable> _subscriptions = [];
        private readonly Channel<JsonElement> _full = Channel.CreateUnbounded<JsonElement>();
        public ConcurrentQueue<string> Events { get; } = new();
        public ConcurrentQueue<JsonElement> Claims { get; } = new();

        private SuccessSignals(HubConnection connection, string game)
        {
            _connection = connection;
            _game = game;
            _subscriptions.Add(connection.On<JsonElement>("FullState", value => _full.Writer.TryWrite(value)));
            _subscriptions.Add(connection.On<JsonElement>("ClaimMade", value => CaptureClaimMade(value, Claims, Events)));
            foreach (var name in new[] { "WinDeclared", "ScoringComplete", "HandFinished", "GameCompleted", "GameEnded" })
                _subscriptions.Add(connection.On<JsonElement>(name, _ => Events.Enqueue(name)));
        }

        public static async Task<SuccessSignals> OpenAsync(HudsonOwnTurnWsFixture host, ClaimScene scene)
        {
            var signals = new SuccessSignals(await host.ConnectHubAsync(scene.Winner.PlayerId), scene.GameId);
            try
            {
                await signals.FenceAsync();
                Assert.Empty(signals.Events);
                Assert.Empty(signals.Claims);
                return signals;
            }
            catch
            {
                await signals.DisposeAsync();
                throw;
            }
        }

        public async Task FenceAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _connection.InvokeCoreAsync<JsonElement>("JoinTable", [_game], timeout.Token);
            var full = await _full.Reader.ReadAsync(timeout.Token);
            Assert.Equal(_game, full.GetProperty("gameId").GetString());
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            await _connection.DisposeAsync();
        }
    }
}
