using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class RuntimeRulePartialsQualificationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _database;
    private readonly ITestOutputHelper _output;

    public RuntimeRulePartialsQualificationTests(ITestOutputHelper output)
    {
        _output = output;
        var directory = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(directory);
        _database = Path.Combine(directory, $"rules-partials-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_database}");
            builder.ConfigureServices(services => services.Configure<ChangshaRuntimeOptions>(options =>
            {
                options.PersistSnapshots = false;
                options.BotTurnDelayMs = 30000;
                options.BotClaimDelayMs = 1;
                options.ClaimWindowTimeoutMs = 1000;
                options.BotDecisionTimeoutMs = 2000;
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

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "ZeroWallKongRuntime")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OwnKongWithNoReplacement_RejectsAtomicallyOrEndsHand(bool added, bool robbery)
    {
        var (runtime, state) = await NewGameAsync();
        ArrangeKongBeforeLastDraw(state, added, replacementAvailable: false, robbery);
        ChangshaGameStateMachine.DrawTile(state);
        Assert.Empty(state.Wall);
        Assert.Equal(14, state.Hands[0].ConcealedTiles.Count + 3 * state.Hands[0].Melds.Count);
        var before = JsonSerializer.Serialize(state);
        var kind = added ? MeldKind.AddedKong : MeldKind.ConcealedKong;
        var tiles = added ? new[] { 19 } : new[] { 16, 17, 18, 19 };

        var error = await Record.ExceptionAsync(() =>
            runtime.DeclareKongAsync(state.GameId, 0, tiles, requestedKind: kind));
        if (error is not null)
        {
            Assert.IsAssignableFrom<InvalidOperationException>(error);
            Assert.Equal(before, JsonSerializer.Serialize(state));
            _output.WriteLine($"Zero-wall {kind}: explicitly rejected without mutation: {error.Message}");
            return;
        }

        if (state.ClaimWindow is { IsKongRobbing: true } window)
        {
            foreach (var seat in window.Opportunities.Select(o => o.SeatIndex).Distinct().ToArray())
                await runtime.PassAsync(state.GameId, seat);
        }
        _output.WriteLine($"Zero-wall {kind}, robbery={robbery}: accepted; phase={state.Phase}, complete={state.IsGameComplete}");
        Assert.Equal(ChangshaPhase.GameComplete, state.Phase);
        Assert.True(state.IsGameComplete);
        Assert.Null(state.CurrentWin);
        AssertDeck(state);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "ZeroWallExposedKongRuntime")]
    public async Task ExposedKongWithNoReplacement_RejectsAtomicallyOrEndsHand()
    {
        var (runtime, state) = await NewGameAsync();
        Arrange(state, new Dictionary<int, int[]>
        {
            [0] = [19],
            [1] = [16, 17, 18, 40, 41, 42, 44, 45, 46, 48, 49, 50, 52]
        }, wallCount: 0);
        state.MissedWinSeats.UnionWith([1, 2, 3]);
        await runtime.DiscardAsync(state.GameId, 0, 19);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Kong);
        var before = JsonSerializer.Serialize(state);

        var error = await Record.ExceptionAsync(() => runtime.ClaimAsync(state.GameId, 1, "Kong", null));
        if (error is not null)
        {
            Assert.True(error is InvalidOperationException or HubException);
            Assert.Equal(before, JsonSerializer.Serialize(state));
            _output.WriteLine($"Zero-wall exposed Kong: explicitly rejected without mutation: {error.Message}");
            return;
        }

        _output.WriteLine($"Zero-wall exposed Kong: accepted; phase={state.Phase}, complete={state.IsGameComplete}");
        Assert.Equal(ChangshaPhase.GameComplete, state.Phase);
        Assert.True(state.IsGameComplete);
        Assert.Null(state.CurrentWin);
        AssertDeck(state);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "KongReplacementPositiveControl")]
    public async Task ConcealedKongWithOneReplacement_RemainsPlayableAfterTheBackDraw()
    {
        var (runtime, state) = await NewGameAsync();
        ArrangeKongBeforeLastDraw(state, added: false, replacementAvailable: true, robbery: false);
        ChangshaGameStateMachine.DrawTile(state);
        Assert.Single(state.Wall);
        var replacement = state.Wall[^1];

        await runtime.DeclareKongAsync(state.GameId, 0, [16, 17, 18, 19],
            requestedKind: MeldKind.ConcealedKong);

        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.False(state.IsGameComplete);
        Assert.Contains(replacement, state.Hands[0].ConcealedTiles);
        Assert.Equal(1, state.WallBackDrawn);
        Assert.Equal(14, state.Hands[0].ConcealedTiles.Count + 3 * state.Hands[0].Melds.Count);
        AssertDeck(state);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "InvalidHuNormalPath")]
    public async Task InvalidHuNormalCommand_RejectsWithoutPenaltyOrMutation()
    {
        var (runtime, state) = await NewGameAsync();
        int[] invalid = [0, 1, 2, 3, 12, 13, 14, 15, 24, 25, 26, 32, 33, 34];
        Arrange(state, new Dictionary<int, int[]> { [0] = invalid[..^1] },
            wallCount: 1, beforeDraw: true, wallFront: 34);
        ChangshaGameStateMachine.DrawTile(state);
        Assert.False(new ChangshaWinDetector().Detect(state.Hands[0]).IsWin);
        var before = JsonSerializer.Serialize(state);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.DeclareWinAsync(state.GameId, 0));

        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.Empty(state.FalseHuPenalties);
        Assert.All(state.CumulativeScores.Values, score => Assert.Equal(0, score));
        _output.WriteLine($"Normal invalid Hu rejected without mutation/penalty: {error.Message}");
    }

    [Fact, Trait("Category", "RulesQualificationGap"), Trait("Rule", "FalseHuHelperOnly")]
    public async Task DirectFalseHuHelper_HasPaymentsButDoesNotProveAnAutomaticTrigger()
    {
        var (_, state) = await NewGameAsync();

        var penalty = ChangshaGameStateMachine.RecordFalseHu(state, 0);

        Assert.Equal(6, penalty.PenaltyPerOpponent);
        Assert.Equal(new[] { -18, 6, 6, 6 },
            Enumerable.Range(0, 4).Select(seat => state.CumulativeScores[seat]));
        Assert.Equal(0, state.CumulativeScores.Values.Sum());
        Assert.Single(state.FalseHuPenalties);
        _output.WriteLine("Direct helper only: payments [-18,+6,+6,+6] at base unit1; no normal command trigger demonstrated.");
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "HumanUnofferedClaimControl")]
    public async Task HumanUnofferedHu_IsRejectedWithoutConsumingItsLegitimateWindow()
    {
        var (runtime, state) = await PrepareRealMissedWinWindowAsync();
        var before = JsonSerializer.Serialize(state);

        await Assert.ThrowsAsync<HubException>(() => runtime.ClaimAsync(state.GameId, 2, "Hu", null));

        Assert.Equal(before, JsonSerializer.Serialize(state));
        var eligible = state.ClaimWindow!.Opportunities.Select(o => o.SeatIndex).Distinct().ToArray();
        foreach (var seat in eligible)
            await runtime.PassAsync(state.GameId, seat);
        Assert.Null(state.ClaimWindow);
        Assert.Null(state.CurrentWin);
        Assert.All(state.CumulativeScores.Values, score => Assert.Equal(0, score));
        AssertDeck(state);
        _output.WriteLine($"Human unoffered Hu rejected atomically; all {eligible.Length} eligible seats then passed normally.");
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "BotClaimValidationParity")]
    [InlineData(TableClaimType.Pung, true)]
    [InlineData(TableClaimType.Hu, false)]
    public async Task BotProposal_OnlyAnOfferedClaimMayChangeLegalState(TableClaimType proposal, bool offered)
    {
        var (runtime, state) = await PrepareRealMissedWinWindowAsync(openSecondWindow: false);
        var strategy = new FixedClaimStrategy(proposal);
        var instance = Instance(runtime, state.GameId);
        instance.BotStrategy = strategy;
        state.Seats[2].IsBot = true;
        var observedOffers = new ConcurrentQueue<TableClaimType[]>();
        void Observe(string id, ChangshaGameState snapshot)
        {
            if (id == state.GameId && snapshot.ClaimWindow is { DiscardSeatIndex: 1, DiscardTileId: 3 } window)
                observedOffers.Enqueue(window.Opportunities.Where(o => o.SeatIndex == 2).Select(o => o.ClaimType).ToArray());
        }
        runtime.StateChanged += Observe;
        try
        {
            await runtime.DiscardAsync(state.GameId, 1, 3);
        }
        finally
        {
            runtime.StateChanged -= Observe;
        }
        Assert.NotEmpty(observedOffers);
        Assert.Contains(TableClaimType.Pung, observedOffers.First());
        Assert.DoesNotContain(TableClaimType.Hu, observedOffers.First());
        await strategy.Called.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitForAsync(() => instance.LastBotDecisions.ContainsKey(2), TimeSpan.FromSeconds(3));
        Assert.Equal(proposal, instance.LastBotDecisions[2].Action.ClaimType);

        if (offered)
        {
            await WaitForAsync(() => state.Hands[2].Melds.Count == 1, TimeSpan.FromSeconds(3));
            Assert.Equal(MeldKind.Pung, Assert.Single(state.Hands[2].Melds).Kind);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        }
        else
        {
            await Task.Delay(100);
            _output.WriteLine($"Bot offered [{string.Join(",", observedOffers.First())}], proposed {proposal}; " +
                $"phase={state.Phase}, winner={state.CurrentWin?.WinningSeatIndex}, " +
                $"scores=[{string.Join(",", Enumerable.Range(0, 4).Select(seat => state.CumulativeScores[seat]))}]");
            Assert.Null(state.CurrentWin);
            Assert.Empty(state.Hands[2].Melds);
            Assert.All(state.CumulativeScores.Values, score => Assert.Equal(0, score));
            await WaitForAsync(() => state.ClaimWindow is null, TimeSpan.FromSeconds(3));
        }
        Assert.Null(state.CurrentWin);
        AssertDeck(state);
    }

    private async Task<(IChangshaGameRuntime Runtime, ChangshaGameState State)> NewGameAsync()
    {
        var runtime = _factory.Services.GetRequiredService<IChangshaGameRuntime>();
        var id = await runtime.CreateGameAsync(42, [], null, null, maxHands: 1);
        Assert.True(runtime.TryGetSnapshot(id, out var state));
        Assert.NotNull(state);
        return (runtime, state);
    }

    private async Task<(IChangshaGameRuntime Runtime, ChangshaGameState State)> PrepareRealMissedWinWindowAsync(
        bool openSecondWindow = true)
    {
        var (runtime, state) = await NewGameAsync();
        Arrange(state, new Dictionary<int, int[]>
        {
            [0] = [2],
            [1] = [3, 4, 8],
            [2] = [0, 1, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53]
        });
        await runtime.DiscardAsync(state.GameId, 0, 2);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 2 && o.ClaimType == TableClaimType.Hu);
        await runtime.PassAsync(state.GameId, 2);
        await runtime.ClaimAsync(state.GameId, 1, "Chow", [4, 8]);
        Assert.Contains(2, state.MissedWinSeats);
        Assert.Equal(1, state.ActiveSeatIndex);
        Assert.Contains(3, state.Hands[1].ConcealedTiles);
        if (openSecondWindow)
        {
            await runtime.DiscardAsync(state.GameId, 1, 3);
            Assert.Contains(state.ClaimWindow!.Opportunities,
                o => o.SeatIndex == 2 && o.ClaimType == TableClaimType.Pung);
            Assert.DoesNotContain(state.ClaimWindow.Opportunities,
                o => o.SeatIndex == 2 && o.ClaimType == TableClaimType.Hu);
        }
        AssertDeck(state);
        return (runtime, state);
    }

    private static ChangshaGameInstance Instance(IChangshaGameRuntime runtime, string id)
    {
        var field = runtime.GetType().GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var instances = Assert.IsType<ConcurrentDictionary<string, ChangshaGameInstance>>(field.GetValue(runtime));
        return instances[id];
    }

    private static void ArrangeKongBeforeLastDraw(
        ChangshaGameState state, bool added, bool replacementAvailable, bool robbery)
    {
        int[] filler = [40, 41, 42, 44, 45, 46, 48, 49, 50, 52];
        var hands = new Dictionary<int, int[]>
        {
            [0] = added ? filler : new[] { 16, 17, 18 }.Concat(filler).ToArray()
        };
        if (robbery)
            hands[2] = [0, 4, 8, 12, 20, 24, 28, 32, 72, 73, 74, 88, 89];
        var melds = added
            ? new Dictionary<int, Meld[]> { [0] = [new Meld { Kind = MeldKind.Pung, TileIds = [16, 17, 18], ClaimedFromSeatIndex = 3 }] }
            : new Dictionary<int, Meld[]>();
        Arrange(state, hands, melds, replacementAvailable ? 2 : 1, beforeDraw: true, wallFront: 19);
        state.MissedWinSeats.UnionWith(robbery ? new[] { 1, 3 } : new[] { 1, 2, 3 });
    }

    private static void Arrange(
        ChangshaGameState state, IReadOnlyDictionary<int, int[]> hands,
        IReadOnlyDictionary<int, Meld[]>? melds = null, int wallCount = 55,
        bool beforeDraw = false, int? wallFront = null)
    {
        foreach (var hand in state.Hands)
        {
            hand.ConcealedTiles.Clear();
            hand.Melds.Clear();
        }
        foreach (var (seat, tiles) in hands) state.Hands[seat].ConcealedTiles.AddRange(tiles);
        if (melds is not null)
            foreach (var (seat, sets) in melds) state.Hands[seat].Melds.AddRange(sets);
        var assigned = state.Hands.SelectMany(h =>
            h.ConcealedTiles.Concat(h.Melds.SelectMany(m => m.TileIds))).ToList();
        if (wallFront.HasValue) assigned.Add(wallFront.Value);
        Assert.Equal(assigned.Count, assigned.Distinct().Count());
        var remaining = new Queue<int>(Enumerable.Range(0, 108).Except(assigned));
        foreach (var hand in state.Hands)
        {
            var target = 13 - 3 * hand.Melds.Count + (hand.SeatIndex == 0 && !beforeDraw ? 1 : 0);
            Assert.True(hand.ConcealedTiles.Count <= target);
            while (hand.ConcealedTiles.Count < target) hand.ConcealedTiles.Add(remaining.Dequeue());
        }
        state.Wall = wallFront.HasValue ? [wallFront.Value] : [];
        while (state.Wall.Count < wallCount) state.Wall.Add(remaining.Dequeue());
        state.DiscardPile.Clear();
        while (remaining.Count > 0)
            state.DiscardPile.Add(new ChangshaDiscard
            {
                SeatIndex = state.DiscardPile.Count % 4,
                TileId = remaining.Dequeue(),
                TurnNumber = state.DiscardPile.Count + 1
            });
        state.ActiveSeatIndex = 0;
        state.Phase = ChangshaPhase.AwaitingDiscard;
        state.ClaimWindow = null;
        state.CurrentWin = null;
        state.CurrentScore = null;
        state.MissedWinSeats.Clear();
        state.WallBackDrawn = 0;
        state.WallBackIndex = state.Wall.Count - 1;
        state.TurnNumber = state.DiscardPile.Count + 1;
        AssertDeck(state);
    }

    private static void AssertDeck(ChangshaGameState state) =>
        Assert.Equal(Enumerable.Range(0, 108),
            state.Wall.Concat(state.DiscardPile.Select(d => d.TileId))
                .Concat(state.Hands.SelectMany(h => h.ConcealedTiles.Concat(h.Melds.SelectMany(m => m.TileIds))))
                .OrderBy(tile => tile));

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "The runtime did not preserve a usable claim/turn lifecycle.");
    }

    private sealed class FixedClaimStrategy(TableClaimType claim) : IChangshaBotStrategy
    {
        public string Difficulty => "qualification-fault-injection";
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BotAction OnTurnStart(ChangshaGameState state, int seat) => BotAction.Discard(state.Hands[seat].ConcealedTiles[0]);
        public BotAction OnSelfDraw(ChangshaGameState state, int seat) => BotAction.Wait();
        public BotAction OnPickupCue(ChangshaGameState state, int seat) => BotAction.Wait();
        public BotAction OnOtherDiscard(ChangshaGameState state, int seat, int discarder, int tile)
        {
            Called.TrySetResult(true);
            return BotAction.Claim(claim);
        }
        public BotAction DecideAction(ChangshaGameState state, int seat) =>
            OnOtherDiscard(state, seat, state.ClaimWindow!.DiscardSeatIndex, state.ClaimWindow.DiscardTileId);
    }
}
