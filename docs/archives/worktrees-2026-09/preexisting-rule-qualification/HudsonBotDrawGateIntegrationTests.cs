using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Players;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonBotDrawGateIntegrationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationRuntime")]
    [InlineData("easy", TableClaimType.Pung)]
    [InlineData("easy", TableClaimType.Chow)]
    [InlineData("medium", TableClaimType.Pung)]
    [InlineData("medium", TableClaimType.Chow)]
    [InlineData("hard", TableClaimType.Pung)]
    [InlineData("hard", TableClaimType.Chow)]
    [InlineData("master", TableClaimType.Pung)]
    [InlineData("master", TableClaimType.Chow)]
    public async Task RealPostClaim_NoOwnDraw_DiscardIsAutonomousAndUsesSelectedStrategy(
        string difficulty, TableClaimType claim)
    {
        await using var host = new BotHost(output);
        await using var table = await host.OpenTableAsync(difficulty);
        int[] waiting = claim == TableClaimType.Pung
            ? [0, 1, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53]
            : [4, 8, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53];
        var incoming = claim == TableClaimType.Pung ? 2 : 0;
        ArrangeHands(table.State, waiting, incoming, front: null);
        var wall = table.State.Wall.ToArray();
        var initialEvents = table.State.EventLog.Count;
        var expectedMeld = claim == TableClaimType.Pung ? MeldKind.Pung : MeldKind.Chow;
        int[] partners = claim == TableClaimType.Pung ? [0, 1] : [4, 8];
        var melded = NewSnapshotSignal();
        var discarded = NewSnapshotSignal();
        var advanced = NewSnapshotSignal();

        void Observe(string game, ChangshaGameState state)
        {
            if (game != table.GameId) return;
            if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == 1
                && state.Hands[1].Melds.Count == 1 && state.Hands[1].Melds[0].Kind == expectedMeld
                && state.Hands[1].ConcealedTiles.Count == 11)
                melded.TrySetResult(state);
            if (state.EventLog.Skip(initialEvents).Any(entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1))
            {
                discarded.TrySetResult(state);
                if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == 2
                    && state.LastDrawSeatIndex == 2)
                    advanced.TrySetResult(state);
            }
        }

        host.Runtime.StateChanged += Observe;
        try
        {
            using var progress = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await table.Humans[0].UpdateAsync([new object[] { "discard", 0, new { tileId = incoming } }]);
            await table.Humans[0].BarrierAsync();
            var offered = await SnapshotAsync(host, table, progress.Token);
            Assert.Equal(ChangshaPhase.AwaitingClaim, offered.Phase);
            Assert.Contains(offered.ClaimWindow!.Opportunities,
                opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == claim);
            Assert.Contains(offered.ClaimWindow.Opportunities,
                opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Hu);

            // This controls the legal setup choice, not the bot's subsequent turn.
            // In particular, Easy's policy deliberately never chooses Chow.
            await host.Runtime.ClaimAsync(table.GameId, 1, claim.ToString(),
                claim == TableClaimType.Chow ? partners : null, progress.Token,
                expectedVersion: offered.StateVersion);
            var postClaim = await melded.Task.WaitAsync(progress.Token);
            Assert.Equal(11, postClaim.Hands[1].ConcealedTiles.Count);
            Assert.Equal(14, Effective(postClaim.Hands[1]));
            Assert.Null(postClaim.LastDrawSeatIndex);
            Assert.Contains(1, postClaim.MissedWinSeats);
            Assert.True(new ChangshaWinDetector().Detect(postClaim.Hands[1]).IsWin);
            Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(postClaim, 1));
            Assert.Equal(wall, postClaim.Wall);
            AssertInventory(postClaim);

            var strategy = ChangshaBotEngine.Resolve(difficulty);
            Assert.Equal(difficulty, strategy.Difficulty);
            var beforeQueries = JsonSerializer.Serialize(postClaim);
            var proposed = strategy.DecideWithReasoning(postClaim, 1);
            Assert.All(new[] { strategy.OnTurnStart(postClaim, 1), strategy.DecideAction(postClaim, 1), proposed.Action },
                action => Assert.Equal(BotActionType.Discard, action.Type));
            Assert.NotEqual(BotActionType.DeclareWin, strategy.OnSelfDraw(postClaim, 1).Type);
            Assert.Equal(beforeQueries, JsonSerializer.Serialize(postClaim));
            Assert.True(proposed.Action.TileId.HasValue);
            var expectedDiscard = proposed.Action.TileId.Value;
            Assert.Contains(expectedDiscard, postClaim.Hands[1].ConcealedTiles);

            var afterDiscard = await discarded.Task.WaitAsync(progress.Token);
            var discardEvent = Assert.Single(afterDiscard.EventLog.Skip(initialEvents),
                entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1);
            Assert.Equal(expectedDiscard, discardEvent.TileId);
            await PassHumanResponsesAsync(host, table, expectedDiscard, progress.Token);
            await advanced.Task.WaitAsync(progress.Token);
            var after = await SnapshotAsync(host, table, progress.Token);
            Assert.Equal(2, after.ActiveSeatIndex);
            Assert.Equal(2, after.LastDrawSeatIndex);
            Assert.True(after.StateVersion > postClaim.StateVersion);
            Assert.True(after.Seats[1].IsBot);
            Assert.Equal(difficulty, host.Runtime.GetActiveBotDifficulty(table.GameId));
            Assert.Equal(10, after.Hands[1].ConcealedTiles.Count);
            Assert.Equal(expectedMeld, Assert.Single(after.Hands[1].Melds).Kind);
            Assert.Equal(partners.Append(incoming).OrderBy(tile => tile),
                after.Hands[1].Melds[0].TileIds.OrderBy(tile => tile));
            Assert.DoesNotContain(expectedDiscard, after.Hands[1].ConcealedTiles);
            Assert.Equal(wall.Skip(1), after.Wall);
            Assert.Equal(wall[0], after.Hands[2].ConcealedTiles[^1]);
            Assert.Equal(14, after.Hands[2].ConcealedTiles.Count);
            Assert.Equal(postClaim.WallBackDrawn, after.WallBackDrawn);
            Assert.Contains(1, after.MissedWinSeats);
            Assert.Null(after.CurrentWin);
            Assert.Null(after.CurrentScore);
            Assert.False(after.IsGameComplete);
            Assert.DoesNotContain(after.EventLog.Skip(initialEvents),
                entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1);
            Assert.DoesNotContain(after.EventLog.Skip(initialEvents),
                entry => entry.EventType is "win-declared" or "scoring-complete");
            Assert.All(after.CumulativeScores.Values, score => Assert.Equal(0, score));
            AssertNoRuntimeFailureOrDiscardFallback(host);
            AssertInventory(after);
            output.WriteLine($"Autonomous {difficulty} post-{claim} discard={expectedDiscard}, predictedStrategyDiscard={expectedDiscard}, nextHumanDraw={wall[0]}, noOwnDrawBeforeDiscard=true, noFalseHu=true, version={after.StateVersion}, game={table.GameId}.");
        }
        finally
        {
            host.Runtime.StateChanged -= Observe;
            output.WriteLine("Runtime diagnostic log: " + JsonSerializer.Serialize(host.Logs.Entries));
        }
    }

    [Theory, Trait("Category", "RulesQualificationRuntime")]
    [InlineData("easy")]
    [InlineData("medium")]
    [InlineData("hard")]
    [InlineData("master")]
    public async Task RealOwnDraw_WinningHandIsAutonomouslyDeclaredAndScored(string difficulty)
    {
        await using var host = new BotHost(output);
        await using var table = await host.OpenTableAsync(difficulty);
        int[] waiting = [4, 8, 12, 16, 20, 24, 28, 32, 52, 53, 36, 40, 44];
        ArrangeHands(table.State, waiting, discard: 104, front: 0);
        var beforeWall = table.State.Wall.ToArray();
        var initialEvents = table.State.EventLog.Count;
        Assert.Equal(13, table.State.Hands[1].ConcealedTiles.Count);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(table.State, 1));
        var drawn = NewSnapshotSignal();
        var completed = NewSnapshotSignal();

        void Observe(string game, ChangshaGameState state)
        {
            if (game != table.GameId) return;
            if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == 1
                && state.LastDrawSeatIndex == 1 && state.Hands[1].ConcealedTiles.Count == 14
                && state.CurrentWin is null)
                drawn.TrySetResult(state);
            if (state.IsGameComplete && state.CurrentWin?.WinningSeatIndex == 1)
                completed.TrySetResult(state);
        }

        host.Runtime.StateChanged += Observe;
        try
        {
            using var progress = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Assert.Empty(new ClaimAdjudicator().GetOpportunities(0, 104, table.State.Hands));
            await table.Humans[0].UpdateAsync([new object[] { "discard", 0, new { tileId = 104 } }]);
            await table.Humans[0].BarrierAsync();
            var ready = await drawn.Task.WaitAsync(progress.Token);
            Assert.Equal(0, ready.Hands[1].ConcealedTiles[^1]);
            Assert.Equal(1, ready.LastDrawSeatIndex);
            Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(ready, 1));
            var strategy = ChangshaBotEngine.Resolve(difficulty);
            Assert.All(new[]
            {
                strategy.OnTurnStart(ready, 1), strategy.OnSelfDraw(ready, 1),
                strategy.DecideAction(ready, 1), strategy.DecideWithReasoning(ready, 1).Action
            }, action => Assert.Equal(BotActionType.DeclareWin, action.Type));
            AssertInventory(ready);

            await completed.Task.WaitAsync(progress.Token);
            var after = await SnapshotAsync(host, table, progress.Token);
            Assert.Equal(ChangshaPhase.GameComplete, after.Phase);
            Assert.True(after.IsGameComplete);
            Assert.Equal(1, after.CurrentWin!.WinningSeatIndex);
            Assert.Equal(WinMethod.SelfDraw, after.CurrentWin.Method);
            Assert.Equal(0, after.CurrentWin.WinningTileId);
            Assert.True(after.CurrentWin.IsSelfDraw);
            Assert.False(after.CurrentWin.IsKongReplacement);
            Assert.NotNull(after.CurrentScore);
            Assert.True(after.CurrentScore.BasePoints > 0);
            Assert.Equal(4, after.CumulativeScores.Count);
            Assert.Equal(0, after.CumulativeScores.Values.Sum());
            Assert.True(after.CumulativeScores[1] > 0);
            Assert.Equal(beforeWall.Skip(1), after.Wall);
            Assert.Equal(0, after.WallBackDrawn);
            var events = after.EventLog.Skip(initialEvents).ToArray();
            var drawEvent = Assert.Single(events, entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1);
            var winEvent = Assert.Single(events, entry => entry.EventType == "win-declared" && entry.SeatIndex == 1);
            Assert.True(winEvent.Sequence > drawEvent.Sequence);
            Assert.Contains(events, entry => entry.EventType == "scoring-complete");
            Assert.DoesNotContain(events, entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1);
            AssertNoRuntimeFailureOrDiscardFallback(host);
            AssertInventory(after);
            output.WriteLine($"Autonomous {difficulty} actualDraw=0 -> SelfDrawHu seat1 -> score={after.CumulativeScores[1]}, totalSum=0, gameComplete=true, game={table.GameId}; fixture only, no qualification credit.");
        }
        finally
        {
            host.Runtime.StateChanged -= Observe;
            output.WriteLine("Runtime diagnostic log: " + JsonSerializer.Serialize(host.Logs.Entries));
        }
    }

    private static void ArrangeHands(ChangshaGameState state, int[] botTiles, int discard, int? front)
    {
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Equal(0, state.LastDrawSeatIndex);
        var used = botTiles.Append(discard).ToHashSet();
        if (front.HasValue) Assert.True(used.Add(front.Value));
        var desired = new Dictionary<int, int[]> { [1] = botTiles };
        int[] kinds = front.HasValue
            ? [0, 1, 2, 3, 5, 6, 7, 8, 9, 14, 15, 16, 17]
            : [1, 2, 3, 4, 6, 7, 8, 9, 10, 14, 15, 16, 17];
        foreach (var seat in new[] { 0, 2, 3 })
        {
            var tiles = new List<int>();
            foreach (var kind in kinds)
            {
                var tile = Enumerable.Range(kind * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                tiles.Add(tile);
            }
            if (seat == 0) tiles.Add(discard);
            desired[seat] = tiles.ToArray();
        }
        foreach (var (seat, tiles) in desired)
        {
            var hand = state.Hands[seat].ConcealedTiles;
            Assert.Equal(tiles.Length, hand.Count);
            Assert.Empty(state.Hands[seat].Melds);
            for (var index = 0; index < tiles.Length; index++)
                SwapInto(state, hand, index, tiles[index]);
        }
        if (front.HasValue) SwapInto(state, state.Wall, 0, front.Value);
        AssertInventory(state);
    }

    private static void SwapInto(ChangshaGameState state, List<int> target, int index, int tile)
    {
        var source = state.Hands.Select(hand => hand.ConcealedTiles).Append(state.Wall)
            .Single(tiles => tiles.Contains(tile));
        var sourceIndex = source.IndexOf(tile);
        (source[sourceIndex], target[index]) = (target[index], source[sourceIndex]);
    }

    private static async Task PassHumanResponsesAsync(BotHost host, BotTable table, int discard, CancellationToken ct)
    {
        var state = await SnapshotAsync(host, table, ct);
        if (state.Phase != ChangshaPhase.AwaitingClaim) return;
        Assert.Equal(1, state.ClaimWindow!.DiscardSeatIndex);
        Assert.Equal(discard, state.ClaimWindow.DiscardTileId);
        var seats = state.ClaimWindow.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct().ToArray();
        foreach (var seat in seats)
        {
            state = await SnapshotAsync(host, table, ct);
            if (state.ClaimWindow is null) break;
            Assert.False(state.Seats[seat].IsBot);
            var peer = table.Humans[seat];
            var frame = await peer.BarrierAsync();
            var entry = Assert.Single(frame.GetProperty("entries").EnumerateArray(),
                entry => entry[0].GetString() == "claim" && entry[1].ToString() == seat.ToString());
            var available = entry[2];
            Assert.Equal(JsonValueKind.Object, available.ValueKind);
            await peer.UpdateAsync([new object[] { "claim", seat.ToString(), new
            {
                action = "pass", type = (string?)null,
                gameId = available.GetProperty("gameId").GetString(),
                expectedVersion = available.GetProperty("stateVersion").GetInt32()
            } }]);
            await peer.BarrierAsync();
        }
    }

    private static TaskCompletionSource<ChangshaGameState> NewSnapshotSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<ChangshaGameState> SnapshotAsync(BotHost host, BotTable table, CancellationToken ct) =>
        await host.Runtime.TryGetSnapshotCopyAsync(table.GameId, ct)
        ?? throw new InvalidOperationException("The isolated bot game disappeared.");

    private static void AssertNoRuntimeFailureOrDiscardFallback(BotHost host) =>
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Error
            || entry.Message.Contains("discard fallback", StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));

    private static int Effective(ChangshaHandState hand) => hand.ConcealedTiles.Count + 3 * hand.Melds.Count;

    private sealed record BotTable(string GameId, ChangshaGameState State, Dictionary<int, WsPeer> Humans) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var peer in Humans.Values) await peer.DisposeAsync();
        }
    }

    private sealed class BotHost : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _database;
        private readonly ITestOutputHelper _output;
        public RuntimeLogs Logs { get; } = new();
        public IChangshaGameRuntime Runtime => _factory.Services.GetRequiredService<IChangshaGameRuntime>();

        public BotHost(ITestOutputHelper output)
        {
            _output = output;
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-bot-draw-gates");
            Directory.CreateDirectory(directory);
            _database = Path.Combine(directory, $"{Guid.NewGuid():N}.db");
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Persistence:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_database};Pooling=False");
                builder.UseSetting("Authentication:JwtSigningKeys:0", key);
                builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(Logs));
                builder.ConfigureServices(services => services.Configure<ChangshaRuntimeOptions>(options =>
                {
                    var defaults = new ChangshaRuntimeOptions();
                    options.PersistSnapshots = false;
                    options.BotTurnDelayMs = defaults.BotTurnDelayMs;
                    options.BotDecisionTimeoutMs = defaults.BotDecisionTimeoutMs;
                    options.BotClaimDelayMs = 30_000;
                    options.ClaimWindowTimeoutMs = 30_000;
                    options.DealBatchDelayMs = defaults.DealBatchDelayMs;
                }));
            });
        }

        public async Task<BotTable> OpenTableAsync(string difficulty)
        {
            var room = $"hudson-draw-gate-{Guid.NewGuid():N}";
            var humans = new Dictionary<int, WsPeer>();
            try
            {
                foreach (var seat in new[] { 0, 2, 3 })
                {
                    var player = $"hudson-draw-human-{seat}-{Guid.NewGuid():N}";
                    var server = _factory.Server;
                    var client = server.CreateWebSocketClient();
                    var token = _factory.Services.GetRequiredService<PlayerIdentityService>().Protect(player);
                    client.ConfigureRequest = request =>
                        request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={token}";
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var socket = await client.ConnectAsync(new Uri(server.BaseAddress,
                        $"autotable/ws?variant=changsha&gameId={room}&bots=false&botCount=0&dealMode=auto&handCount=1&seed=20260914"), timeout.Token);
                    var peer = new WsPeer(socket, room, player, _output);
                    humans.Add(seat, peer);
                    await peer.BarrierAsync();
                    await peer.UpdateAsync(new[] { "claim", "turn", "discard", "ownTurn", "gameComplete" }
                        .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
                    await peer.UpdateAsync([new object[] { "seats", player, new { seat } }]);
                    await peer.BarrierAsync();
                }
                var manager = _factory.Services.GetRequiredService<AutotableConnectionManager>();
                var game = manager.GetRuntimeGameIdBoundTo(room)
                    ?? throw new InvalidOperationException("Normal signed seats did not bind a runtime.");
                await Runtime.FillEmptySeatsWithBotsAsync(game);
                Assert.True(await Runtime.SetBotStrategyAsync(game, difficulty));
                Assert.Equal(difficulty, Runtime.GetActiveBotDifficulty(game));
                Assert.True(Runtime.TryGetSnapshot(game, out var state));
                Assert.NotNull(state);
                Assert.Equal(1, Assert.Single(state.Seats, seat => seat.IsBot).SeatIndex);
                foreach (var (seat, peer) in humans)
                    Assert.Equal(seat, Runtime.TryGetSeatForPlayer(game, peer.PlayerId));
                Assert.Equal(ChangshaPhase.Seating, state.Phase);
                Assert.Equal(DealMode.Auto, state.DealMode);
                Assert.Equal(1, state.MaxHands);
                await Runtime.StartGameAsync(game);
                Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
                AssertInventory(state);
                _output.WriteLine($"Bound actual runtime={game} room={room} strategy={difficulty}, botSeat=1, three signed human peers; setup claim is controlled, bot own turn is autonomous.");
                return new BotTable(game, state, humans);
            }
            catch
            {
                foreach (var peer in humans.Values) await peer.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _factory.DisposeAsync();
            foreach (var file in new[] { _database, _database + "-wal", _database + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private sealed class RuntimeLogs : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, Entries);
        public void Dispose() { }

        private sealed class Recorder(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => category == typeof(ChangshaGameRuntime).FullName && level >= LogLevel.Warning;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(level))
                    entries.Enqueue(new LogEntry(level, formatter(state, exception), exception?.ToString()));
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, string? Exception);
}
