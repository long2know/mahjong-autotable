using System.Collections.Concurrent;
using System.Reflection;
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

public sealed class HudsonBotInvalidProposalIntegrationTests(ITestOutputHelper output)
{
    [Fact, Trait("Category", "RulesQualificationRuntime")]
    public async Task InvalidSelfHuAfterRealPung_RecordsAndExecutesTheReplacementDiscardDecision()
    {
        await using var host = new Host(output);
        await using var dealer = await host.ConnectAndSeatAsync(0);
        await using var nextHuman = await host.ConnectAndSeatAsync(2);
        await using var otherHuman = await host.ConnectAndSeatAsync(3);
        var game = host.Manager.GetRuntimeGameIdBoundTo(host.Room)
            ?? throw new InvalidOperationException("Signed seats did not bind a runtime.");
        await host.Runtime.FillEmptySeatsWithBotsAsync(game);
        Assert.True(host.Runtime.TryGetSnapshot(game, out var state));
        Assert.NotNull(state);
        Assert.Equal(1, Assert.Single(state.Seats, seat => seat.IsBot).SeatIndex);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(game, dealer.PlayerId));
        Assert.Equal(2, host.Runtime.TryGetSeatForPlayer(game, nextHuman.PlayerId));
        Assert.Equal(3, host.Runtime.TryGetSeatForPlayer(game, otherHuman.PlayerId));
        await host.Runtime.StartGameAsync(game);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        ArrangePungWait(state);
        var wall = state.Wall.ToArray();
        var eventStart = state.EventLog.Count;
        var instance = Instance(host.Runtime, game);
        var deliberatelyInvalid = new InvalidWinStrategy();
        await instance.Lock.WaitAsync();
        try
        {
            instance.BotStrategy = deliberatelyInvalid;
        }
        finally
        {
            instance.Lock.Release();
        }
        var melded = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var advanced = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Observe(string gameId, ChangshaGameState snapshot)
        {
            if (gameId != game) return;
            if (snapshot.Phase == ChangshaPhase.AwaitingDiscard && snapshot.ActiveSeatIndex == 1
                && snapshot.Hands[1].Melds.Count == 1 && snapshot.Hands[1].Melds[0].Kind == MeldKind.Pung
                && snapshot.Hands[1].ConcealedTiles.Count == 11)
                melded.TrySetResult(snapshot);
            if (snapshot.Phase == ChangshaPhase.AwaitingDiscard && snapshot.ActiveSeatIndex == 2
                && snapshot.LastDrawSeatIndex == 2
                && snapshot.EventLog.Skip(eventStart).Any(entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1))
                advanced.TrySetResult(snapshot);
        }

        host.Runtime.StateChanged += Observe;
        try
        {
            using var progress = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await dealer.UpdateAsync([new object[] { "discard", 0, new { tileId = 2 } }]);
            await dealer.BarrierAsync();
            var postClaim = await melded.Task.WaitAsync(progress.Token);
            Assert.Equal(11, postClaim.Hands[1].ConcealedTiles.Count);
            Assert.Equal(new[] { 0, 1, 2 }, Assert.Single(postClaim.Hands[1].Melds).TileIds.OrderBy(tile => tile));
            Assert.Null(postClaim.LastDrawSeatIndex);
            Assert.Contains(1, postClaim.MissedWinSeats);
            Assert.True(new ChangshaWinDetector().Detect(postClaim.Hands[1]).IsWin);
            Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(postClaim, 1));
            Assert.Equal(wall, postClaim.Wall);
            AssertInventory(postClaim);
            var expectedDiscard = ChangshaBotPolicy.SelectDiscardTile(postClaim.Hands[1]);
            Assert.Empty(new ClaimAdjudicator().GetOpportunities(1, expectedDiscard, postClaim.Hands));

            await advanced.Task.WaitAsync(progress.Token);
            var after = await host.Runtime.TryGetSnapshotCopyAsync(game, progress.Token);
            Assert.NotNull(after);
            Assert.Equal(1, deliberatelyInvalid.InvalidTurnProposals);
            Assert.Equal(2, after.ActiveSeatIndex);
            Assert.Equal(2, after.LastDrawSeatIndex);
            Assert.True(after.StateVersion > postClaim.StateVersion);
            Assert.Equal(10, after.Hands[1].ConcealedTiles.Count);
            Assert.DoesNotContain(expectedDiscard, after.Hands[1].ConcealedTiles);
            var discard = Assert.Single(after.DiscardPile);
            Assert.Equal(1, discard.SeatIndex);
            Assert.Equal(expectedDiscard, discard.TileId);
            Assert.Equal(wall.Skip(1), after.Wall);
            Assert.Equal(wall[0], after.Hands[2].ConcealedTiles[^1]);
            Assert.Equal(14, after.Hands[2].ConcealedTiles.Count);
            Assert.Contains(1, after.MissedWinSeats);
            Assert.Equal(postClaim.WallBackDrawn, after.WallBackDrawn);
            Assert.Null(after.CurrentWin);
            Assert.Null(after.CurrentScore);
            Assert.False(after.IsGameComplete);
            Assert.All(after.CumulativeScores.Values, score => Assert.Equal(0, score));
            Assert.DoesNotContain(after.EventLog.Skip(eventStart),
                entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1);
            Assert.DoesNotContain(after.EventLog.Skip(eventStart),
                entry => entry.EventType is "win-declared" or "scoring-complete");
            AssertInventory(after);

            BotDecision recorded;
            await instance.Lock.WaitAsync(progress.Token);
            try
            {
                Assert.True(instance.LastBotDecisions.TryGetValue(1, out recorded));
                Assert.Same(deliberatelyInvalid, instance.BotStrategy);
            }
            finally
            {
                instance.Lock.Release();
            }
            Assert.Equal(BotActionType.Discard, recorded.Action.Type);
            Assert.Equal(expectedDiscard, recorded.Action.TileId);
            Assert.Equal(expectedDiscard, recorded.Tile);
            Assert.Equal(0, recorded.Score);
            Assert.NotEqual(InvalidWinStrategy.BogusTile, recorded.Tile);
            Assert.NotEqual(InvalidWinStrategy.BogusScore, recorded.Score);
            Assert.Equal(new[]
            {
                InvalidWinStrategy.OriginalReason,
                "runtime: self-draw unavailable; deterministic discard fallback"
            }, recorded.Reasoning);
            var warning = Assert.Single(host.Logs.Entries,
                entry => entry.Message.Contains("Bot proposed unavailable self-draw", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.Contains("using deterministic discard fallback", warning.Message);
            Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Error
                || entry.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));
            output.WriteLine($"Injected invalid proposal handled game={game}; invalidTurnCalls=1; actualBotDiscard={expectedDiscard}; nextHumanDraw={wall[0]}; version={after.StateVersion}; recordedAction={recorded.Action.Type}/tile={recorded.Tile}/score={recorded.Score}.");
            output.WriteLine("Recorded replacement reasoning: " + JsonSerializer.Serialize(recorded.Reasoning));
            output.WriteLine("Runtime fallback warning: " + warning.Message);
        }
        finally
        {
            host.Runtime.StateChanged -= Observe;
            output.WriteLine("Runtime logs: " + JsonSerializer.Serialize(host.Logs.Entries));
        }
    }

    private static void ArrangePungWait(ChangshaGameState state)
    {
        int[] waiting = [0, 1, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53];
        var used = waiting.Append(2).ToHashSet();
        var desired = new Dictionary<int, int[]> { [1] = waiting };
        int[] kinds = [1, 2, 3, 4, 6, 7, 8, 9, 10, 14, 15, 16, 17];
        foreach (var seat in new[] { 0, 2, 3 })
        {
            var tiles = new List<int>();
            foreach (var kind in kinds)
            {
                var tile = Enumerable.Range(kind * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                tiles.Add(tile);
            }
            if (seat == 0) tiles.Add(2);
            desired[seat] = tiles.ToArray();
        }
        foreach (var (seat, tiles) in desired)
        {
            var hand = state.Hands[seat].ConcealedTiles;
            Assert.Equal(tiles.Length, hand.Count);
            for (var index = 0; index < tiles.Length; index++)
            {
                var source = state.Hands.Select(candidate => candidate.ConcealedTiles).Append(state.Wall)
                    .Single(candidate => candidate.Contains(tiles[index]));
                var sourceIndex = source.IndexOf(tiles[index]);
                (source[sourceIndex], hand[index]) = (hand[index], source[sourceIndex]);
            }
        }
        AssertInventory(state);
    }

    private static ChangshaGameInstance Instance(IChangshaGameRuntime runtime, string game)
    {
        var field = runtime.GetType().GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var instances = Assert.IsType<ConcurrentDictionary<string, ChangshaGameInstance>>(field.GetValue(runtime));
        Assert.True(instances.TryGetValue(game, out var instance));
        Assert.NotNull(instance);
        return instance;
    }

    private sealed class InvalidWinStrategy : IChangshaBotStrategy
    {
        public const int BogusTile = 107;
        public const int BogusScore = 1234;
        public const string OriginalReason = "fixture: deliberately unavailable self-Hu";
        private int _invalidTurnProposals;
        public string Difficulty => "fixture-invalid-own-win";
        public int InvalidTurnProposals => Volatile.Read(ref _invalidTurnProposals);
        public BotAction OnTurnStart(ChangshaGameState state, int seat) => BotAction.DeclareWin();
        public BotAction OnSelfDraw(ChangshaGameState state, int seat) => BotAction.DeclareWin();
        public BotAction OnPickupCue(ChangshaGameState state, int seat) => BotAction.Wait();
        public BotAction OnOtherDiscard(ChangshaGameState state, int seat, int source, int tile) =>
            state.ClaimWindow?.Opportunities.Any(opportunity =>
                opportunity.SeatIndex == seat && opportunity.ClaimType == TableClaimType.Pung) == true
                ? BotAction.Claim(TableClaimType.Pung) : BotAction.Pass();

        public BotAction DecideAction(ChangshaGameState state, int seat) =>
            state.Phase == ChangshaPhase.AwaitingClaim && state.ClaimWindow is { } window
                ? OnOtherDiscard(state, seat, window.DiscardSeatIndex, window.DiscardTileId)
                : OnTurnStart(state, seat);

        public BotDecision DecideWithReasoning(ChangshaGameState state, int seat)
        {
            var action = DecideAction(state, seat);
            if (action.Type != BotActionType.DeclareWin)
                return BotDecision.FromAction(action);
            Interlocked.Increment(ref _invalidTurnProposals);
            return new BotDecision(action, BogusTile, BogusScore, [OriginalReason]);
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _database;
        private readonly ITestOutputHelper _output;
        public string Room { get; } = $"hudson-invalid-proposal-{Guid.NewGuid():N}";
        public RuntimeLogs Logs { get; } = new();
        public IChangshaGameRuntime Runtime => _factory.Services.GetRequiredService<IChangshaGameRuntime>();
        public AutotableConnectionManager Manager => _factory.Services.GetRequiredService<AutotableConnectionManager>();

        public Host(ITestOutputHelper output)
        {
            _output = output;
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-invalid-bot-proposal");
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
                    options.BotClaimDelayMs = defaults.BotClaimDelayMs;
                    options.BotDecisionTimeoutMs = defaults.BotDecisionTimeoutMs;
                    options.ClaimWindowTimeoutMs = defaults.ClaimWindowTimeoutMs;
                }));
            });
        }

        public async Task<WsPeer> ConnectAndSeatAsync(int seat)
        {
            var server = _factory.Server;
            var player = $"hudson-injected-owner-{seat}-{Guid.NewGuid():N}";
            var token = _factory.Services.GetRequiredService<PlayerIdentityService>().Protect(player);
            var client = server.CreateWebSocketClient();
            client.ConfigureRequest = request =>
                request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={token}";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var peer = new WsPeer(await client.ConnectAsync(new Uri(server.BaseAddress,
                $"autotable/ws?variant=changsha&gameId={Room}&bots=false&botCount=0&dealMode=auto&handCount=4&seed=20260914"), timeout.Token),
                Room, player, _output);
            try
            {
                await peer.BarrierAsync();
                await peer.UpdateAsync(new[] { "claim", "turn", "discard", "ownTurn", "gameComplete" }
                    .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
                await peer.UpdateAsync([new object[] { "seats", player, new { seat } }]);
                await peer.BarrierAsync();
                return peer;
            }
            catch
            {
                await peer.DisposeAsync();
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
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(level))
                    entries.Enqueue(new LogEntry(level, formatter(state, exception), exception?.ToString()));
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, string? Exception);
}
