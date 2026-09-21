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

public sealed class HudsonBotMeldContinuationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationRuntime")]
    [InlineData(TableClaimType.Pung)]
    [InlineData(TableClaimType.Chow)]
    public async Task BotMeldWithoutOwnDraw_DiscardsAndAdvancesInsteadOfStoppingOnSelfHu(TableClaimType claim)
    {
        await using var host = new BotHost(output);
        var room = $"hudson-bot-meld-{Guid.NewGuid():N}";
        await using var dealer = await host.ConnectAndSeatAsync(room, 0);
        await using var nextHuman = await host.ConnectAndSeatAsync(room, 2);
        await using var otherHuman = await host.ConnectAndSeatAsync(room, 3);
        var runtime = host.Runtime;
        var game = host.Manager.GetRuntimeGameIdBoundTo(room)
            ?? throw new InvalidOperationException("Authenticated seats did not bind a runtime.");
        Assert.Equal(0, runtime.TryGetSeatForPlayer(game, dealer.PlayerId));
        Assert.Equal(2, runtime.TryGetSeatForPlayer(game, nextHuman.PlayerId));
        Assert.Equal(3, runtime.TryGetSeatForPlayer(game, otherHuman.PlayerId));
        await runtime.FillEmptySeatsWithBotsAsync(game);
        Assert.True(runtime.AreAllSeatsOccupied(game));
        Assert.True(runtime.TryGetSnapshot(game, out var state));
        Assert.NotNull(state);
        Assert.Equal(ChangshaPhase.Seating, state.Phase);
        Assert.Equal(DealMode.Auto, state.DealMode);
        Assert.Equal(1, state.BaseUnit);
        Assert.Equal(4, state.MaxHands);
        Assert.Equal(1, Assert.Single(state.Seats, seat => seat.IsBot).SeatIndex);
        Assert.Equal("medium", runtime.GetActiveBotDifficulty(game));
        await runtime.StartGameAsync(game);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Equal(0, state.LastDrawSeatIndex);
        AssertInventory(state);

        var claimedTile = ArrangeConservedWait(state, claim);
        // An existing pass-Hu lockout makes the bot choose the offered meld, not Hu.
        state.MissedWinSeats.Add(1);
        var expectedMeld = claim == TableClaimType.Pung ? MeldKind.Pung : MeldKind.Chow;
        var expectedTiles = claim == TableClaimType.Pung ? new[] { 0, 1, 2 } : new[] { 0, 4, 8 };
        var wall = state.Wall.ToArray();
        var backDrawn = state.WallBackDrawn;
        var initialVersion = state.StateVersion;
        var initialEvents = state.EventLog.Count;
        var claimSnapshot = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var meldSnapshot = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var advancedSnapshot = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observations = new ConcurrentQueue<ChangshaGameState>();

        void Observe(string changedGame, ChangshaGameState snapshot)
        {
            if (changedGame != game) return;
            observations.Enqueue(snapshot);
            if (snapshot.Phase == ChangshaPhase.AwaitingClaim
                && snapshot.ClaimWindow?.DiscardSeatIndex == 0
                && snapshot.ClaimWindow.DiscardTileId == claimedTile)
                claimSnapshot.TrySetResult(snapshot);
            if (snapshot.Phase == ChangshaPhase.AwaitingDiscard && snapshot.ActiveSeatIndex == 1
                && snapshot.Hands[1].Melds.Count == 1 && snapshot.Hands[1].Melds[0].Kind == expectedMeld)
                meldSnapshot.TrySetResult(snapshot);
            if (snapshot.Phase == ChangshaPhase.AwaitingDiscard && snapshot.ActiveSeatIndex == 2
                && snapshot.LastDrawSeatIndex == 2
                && snapshot.EventLog.Skip(initialEvents).Any(entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1))
                advancedSnapshot.TrySetResult(snapshot);
        }

        runtime.StateChanged += Observe;
        try
        {
            output.WriteLine($"Bot continuation game={game} room={room} claim={claim} seed={state.Seed} version={initialVersion}");
            using var progress = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await dealer.UpdateAsync([new object[] { "discard", 0, new { tileId = claimedTile } }]);
            await dealer.BarrierAsync();

            var offered = await claimSnapshot.Task.WaitAsync(progress.Token);
            var opportunity = Assert.Single(offered.ClaimWindow!.Opportunities);
            Assert.Equal(1, opportunity.SeatIndex);
            Assert.Equal(claim, opportunity.ClaimType);
            Assert.Equal(initialVersion + 2, offered.StateVersion);
            Assert.Equal(new[] { "tile-discarded", "claim-window-open" },
                offered.EventLog.Skip(initialEvents).Select(entry => entry.EventType));
            Assert.Contains(1, offered.MissedWinSeats);
            var claimDecision = ChangshaBotEngine.Default.DecideWithReasoning(offered, 1);
            Assert.Equal(BotActionType.Claim, claimDecision.Action.Type);
            Assert.Equal(claim, claimDecision.Action.ClaimType);

            var melded = await meldSnapshot.Task.WaitAsync(progress.Token);
            Assert.True(melded.StateVersion > offered.StateVersion);
            Assert.Equal(expectedTiles, Assert.Single(melded.Hands[1].Melds).TileIds.OrderBy(tile => tile));
            Assert.Equal(11, melded.Hands[1].ConcealedTiles.Count);
            Assert.Null(melded.LastDrawSeatIndex);
            Assert.Equal(wall, melded.Wall);
            Assert.Equal(backDrawn, melded.WallBackDrawn);
            Assert.Contains(1, melded.MissedWinSeats);
            Assert.True(new ChangshaWinDetector().Detect(melded.Hands[1]).IsWin);
            Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(melded, 1));
            Assert.Null(melded.CurrentWin);
            Assert.Null(melded.CurrentScore);
            var proposed = ChangshaBotEngine.Default.DecideWithReasoning(melded, 1);
            Assert.Equal(BotActionType.Discard, proposed.Action.Type);
            output.WriteLine($"Actual bot meld version={melded.StateVersion}, structurally winning but ownDraw=null; policy proposes {proposed.Action.Type}");
            AssertInventory(melded);

            var advanced = await advancedSnapshot.Task.WaitAsync(progress.Token);
            var stable = await runtime.TryGetSnapshotCopyAsync(game, progress.Token);
            Assert.NotNull(stable);
            Assert.Equal(advanced.StateVersion, stable.StateVersion);
            Assert.True(stable.StateVersion > melded.StateVersion);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, stable.Phase);
            Assert.Equal(2, stable.ActiveSeatIndex);
            Assert.Equal(2, stable.LastDrawSeatIndex);
            var discard = Assert.Single(stable.DiscardPile);
            Assert.Equal(1, discard.SeatIndex);
            Assert.Contains(discard.TileId, melded.Hands[1].ConcealedTiles);
            Assert.Equal(melded.Hands[1].ConcealedTiles.Where(tile => tile != discard.TileId).OrderBy(tile => tile),
                stable.Hands[1].ConcealedTiles.OrderBy(tile => tile));
            Assert.Equal(10, stable.Hands[1].ConcealedTiles.Count);
            Assert.Equal(expectedTiles, Assert.Single(stable.Hands[1].Melds).TileIds.OrderBy(tile => tile));
            Assert.Equal(wall.Skip(1), stable.Wall);
            Assert.Equal(wall[0], stable.Hands[2].ConcealedTiles[^1]);
            Assert.Equal(14, stable.Hands[2].ConcealedTiles.Count);
            Assert.Equal(backDrawn, stable.WallBackDrawn);
            Assert.Contains(1, stable.MissedWinSeats);
            Assert.Null(stable.CurrentWin);
            Assert.Null(stable.CurrentScore);
            Assert.False(stable.IsGameComplete);
            Assert.All(stable.CumulativeScores.Values, score => Assert.Equal(0, score));
            var events = stable.EventLog.Skip(initialEvents).ToArray();
            Assert.DoesNotContain(events, entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1);
            Assert.DoesNotContain(events, entry => entry.EventType is "win-declared" or "scoring-complete");
            var discarded = Assert.Single(events, entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1);
            Assert.Equal(discard.TileId, discarded.TileId);
            var drawn = Assert.Single(events, entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 2);
            Assert.Equal(wall[0], drawn.TileId);
            AssertInventory(stable);
            await dealer.BarrierAsync();
            Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Error);
            output.WriteLine($"Actual bot discard={discard.TileId}; next human drew={wall[0]}; final version={stable.StateVersion}; inventory=108");
        }
        finally
        {
            runtime.StateChanged -= Observe;
            using var diagnostic = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var last = await runtime.TryGetSnapshotCopyAsync(game, diagnostic.Token);
            output.WriteLine("Final runtime snapshot: " + JsonSerializer.Serialize(last));
            output.WriteLine("Observed transitions: " + JsonSerializer.Serialize(observations.Select(snapshot => new
            {
                snapshot.StateVersion, snapshot.Phase, snapshot.ActiveSeatIndex, snapshot.LastDrawSeatIndex,
                botTiles = snapshot.Hands[1].ConcealedTiles.Count, botMelds = snapshot.Hands[1].Melds.Count,
                wallCount = snapshot.Wall.Count, snapshot.CurrentWin
            })));
            output.WriteLine("Runtime warnings/errors: " + JsonSerializer.Serialize(host.Logs.Entries));
        }
    }

    private static int ArrangeConservedWait(ChangshaGameState state, TableClaimType claim)
    {
        int[] waiting = claim == TableClaimType.Pung
            ? [0, 1, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53]
            : [4, 8, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53];
        var incoming = claim == TableClaimType.Pung ? 2 : 0;
        var used = waiting.Append(incoming).ToHashSet();
        var hands = new Dictionary<int, int[]> { [1] = waiting };
        int[] fillerKinds = [1, 2, 3, 4, 6, 7, 8, 9, 10, 14, 15, 16, 17];
        foreach (var seat in new[] { 0, 2, 3 })
        {
            var tiles = new List<int>();
            foreach (var kind in fillerKinds)
            {
                var tile = Enumerable.Range(kind * 4, 4).First(id => !used.Contains(id));
                Assert.True(used.Add(tile));
                tiles.Add(tile);
            }
            if (seat == 0) tiles.Add(incoming);
            hands[seat] = tiles.ToArray();
        }
        foreach (var (seat, desired) in hands)
        {
            var hand = state.Hands[seat].ConcealedTiles;
            Assert.Equal(desired.Length, hand.Count);
            Assert.Empty(state.Hands[seat].Melds);
            for (var index = 0; index < desired.Length; index++)
            {
                var source = state.Hands.Select(candidate => candidate.ConcealedTiles).Append(state.Wall)
                    .Single(tiles => tiles.Contains(desired[index]));
                var sourceIndex = source.IndexOf(desired[index]);
                (source[sourceIndex], hand[index]) = (hand[index], source[sourceIndex]);
            }
        }
        AssertInventory(state);
        return incoming;
    }

    private sealed class BotHost : IAsyncDisposable
    {
        private readonly string _database;
        private readonly WebApplicationFactory<Program> _factory;
        private readonly ITestOutputHelper _output;
        public RuntimeLogs Logs { get; } = new();
        public IChangshaGameRuntime Runtime => _factory.Services.GetRequiredService<IChangshaGameRuntime>();
        public AutotableConnectionManager Manager => _factory.Services.GetRequiredService<AutotableConnectionManager>();

        public BotHost(ITestOutputHelper output)
        {
            _output = output;
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-bot-meld");
            Directory.CreateDirectory(directory);
            _database = Path.Combine(directory, $"{Guid.NewGuid():N}.db");
            var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Persistence:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_database};Pooling=False");
                builder.UseSetting("Authentication:JwtSigningKeys:0", signingKey);
                builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(Logs));
                builder.ConfigureServices(services => services.Configure<ChangshaRuntimeOptions>(options =>
                {
                    var defaults = new ChangshaRuntimeOptions();
                    options.PersistSnapshots = false;
                    options.BotTurnDelayMs = defaults.BotTurnDelayMs;
                    options.BotClaimDelayMs = defaults.BotClaimDelayMs;
                    options.BotDecisionTimeoutMs = defaults.BotDecisionTimeoutMs;
                    options.ClaimWindowTimeoutMs = defaults.ClaimWindowTimeoutMs;
                    options.DealBatchDelayMs = defaults.DealBatchDelayMs;
                }));
            });
        }

        public async Task<WsPeer> ConnectAndSeatAsync(string room, int seat)
        {
            var server = _factory.Server;
            var player = $"hudson-bot-owner-{seat}-{Guid.NewGuid():N}";
            var token = _factory.Services.GetRequiredService<PlayerIdentityService>().Protect(player);
            var client = server.CreateWebSocketClient();
            client.ConfigureRequest = request =>
                request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={token}";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var socket = await client.ConnectAsync(new Uri(server.BaseAddress,
                "autotable/ws?variant=changsha&bots=false&botCount=0&dealMode=auto&handCount=4&seed=20260914"), timeout.Token);
            var peer = new WsPeer(socket, room, player, _output);
            try
            {
                await peer.BarrierAsync();
                await peer.UpdateAsync(new[] { "claim", "discard", "turn", "ownTurn", "gameComplete" }
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
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Entries);
        public void Dispose() { }

        private sealed class CaptureLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) =>
                category == typeof(ChangshaGameRuntime).FullName && logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                    entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception?.ToString()));
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, string? Exception);
}
