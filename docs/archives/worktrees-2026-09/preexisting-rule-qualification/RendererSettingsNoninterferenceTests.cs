using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class RendererSettingsNoninterferenceTests(ITestOutputHelper output)
{
    private const int BaseUnit = 7;
    private static readonly JsonSerializerOptions StoredJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(DealMode.Auto)]
    [InlineData(DealMode.Manual)]
    public async Task NormalChangshaUpdates_PreserveActiveGameAndPersistence(DealMode mode)
    {
        RecordLoadedAssemblies();
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var peer = await OpenOwnedPeerAsync(host, mode, 20260912);
        var table = await BindOwnedTableAsync(host, peer, mode, 20260912, 0);
        using (var setup = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            await host.Runtime.StartGameAsync(table.GameId, setup.Token);
            if (mode == DealMode.Manual)
            {
                await host.Runtime.RollDiceAsync(table.GameId, 0, setup.Token);
                for (var pickup = 0; pickup < 17; pickup++)
                {
                    Assert.True(ChangshaGameStateMachine.IsPickupPhase(table.State.Phase));
                    await host.Runtime.TakeTilesFromWallAsync(table.GameId,
                        table.State.PickupSeatIndex!.Value,
                        ChangshaGameStateMachine.ExpectedPickupCount(table.State.Phase), setup.Token);
                }
            }
            await host.Runtime.AcknowledgeDealAsync(table.GameId, table.Seat, setup.Token);
        }

        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(new[] { 14, 13, 13, 13 },
            table.State.Hands.OrderBy(hand => hand.SeatIndex).Select(hand => hand.ConcealedTiles.Count));
        Assert.Equal(55, table.State.Wall.Count);
        await AssertSettingUpdatesPreserveAsync(host, table, mode);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task NormalChangshaAutoUpdates_PreserveRealHuAndNonzeroLedger()
    {
        RecordLoadedAssemblies();
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var peer = await OpenOwnedPeerAsync(host, DealMode.Auto, 4924);
        var table = await BindOwnedTableAsync(host, peer, DealMode.Auto, 4924, 2);
        await PrepareAutoStandardHandAsync(host.Runtime, table);
        await AssertSettingUpdatesPreserveAsync(host, table, DealMode.Auto);

        var ready = Entry(await table.Peer.BarrierAsync(), "ownTurn", table.Seat);
        Assert.True(ready.GetProperty("hu").GetBoolean());
        Assert.Equal(table.GameId, ready.GetProperty("gameId").GetString());
        await table.Peer.UpdateAsync([new object[]
        {
            "ownTurn", table.Seat, new
            {
                action = "hu",
                gameId = table.GameId,
                expectedVersion = ready.GetProperty("stateVersion").GetInt32()
            }
        }]);
        await table.Peer.BarrierAsync();

        var won = await host.SnapshotAsync(table);
        Assert.Equal(ChangshaPhase.GameComplete, won.Phase);
        Assert.True(won.IsGameComplete);
        Assert.Equal(2, won.CurrentWin!.WinningSeatIndex);
        Assert.Equal(WinMethod.SelfDraw, won.CurrentWin.Method);
        Assert.Equal(WinPattern.Standard, won.CurrentWin.Pattern);
        Assert.Equal(new[] { -14, -7, 28, -7 },
            won.CumulativeScores.OrderBy(score => score.Key).Select(score => score.Value));
        Assert.Equal(0, won.CumulativeScores.Values.Sum());
        await AssertSettingUpdatesPreserveAsync(host, table, DealMode.Auto);
        output.WriteLine("O19 Auto seed4924/owner2 real Standard self-draw: unit7 ledger [-14,-7,28,-7] survives both setting profiles.");
    }

    private async Task PrepareAutoStandardHandAsync(IChangshaGameRuntime runtime, HumanTable table)
    {
        var state = table.State;
        Assert.Equal(DealMode.Auto, state.DealMode);
        Assert.Equal(4924, state.Seed);
        Assert.Equal(2, table.Seat);
        Assert.Equal(1, state.MaxHands);
        var field = typeof(ChangshaGameRuntime).GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var instances = Assert.IsType<ConcurrentDictionary<string, ChangshaGameInstance>>(field.GetValue(runtime));
        using var setup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var trace = new PreparationTrace(instances[table.GameId], setup.Token, output);
        await trace.OperationAsync("StartGame", -1, () => runtime.StartGameAsync(table.GameId, setup.Token));
        await trace.OperationAsync("AcknowledgeDeal", 2, () => runtime.AcknowledgeDealAsync(table.GameId, 2, setup.Token));

        // Fixed, pure-engine-confirmed Auto transcript; runtime performs the two
        // normal front draws when the respective claim windows finish passing.
        await DiscardAndPassAsync(0, 74, [1]);
        await DiscardAndPassAsync(1, 106, [0, 2]);

        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(2, state.ActiveSeatIndex);
        Assert.Equal(2, state.LastDrawSeatIndex);
        Assert.False(state.LastDrawWasKongReplacement);
        Assert.Null(state.ClaimWindow);
        Assert.Empty(state.Hands[2].Melds);
        Assert.Equal(new[] { 18, 94, 82, 52, 59, 7, 6, 13, 91, 9, 48, 100, 97, 86 },
            state.Hands[2].ConcealedTiles);
        Assert.Equal(new[] { 74, 106 }, state.DiscardPile.Select(discard => discard.TileId));
        Assert.Equal(53, state.Wall.Count);
        Assert.True(ChangshaOwnTurnActions.Available(state, 2)?.Hu);
        AssertInventory(state);
        setup.Token.ThrowIfCancellationRequested();
        trace.Mark("preparation.ready");

        async Task DiscardAndPassAsync(int seat, int tile, int[] passingSeats)
        {
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
            Assert.Equal(seat, state.ActiveSeatIndex);
            await trace.OperationAsync("Discard", seat, () => runtime.DiscardAsync(table.GameId, seat, tile, setup.Token));
            var window = state.ClaimWindow;
            Assert.NotNull(window);
            Assert.Equal(passingSeats, window.Opportunities.Select(item => item.SeatIndex).Distinct().Order());
            foreach (var passingSeat in passingSeats)
            {
                Assert.Same(window, state.ClaimWindow);
                await trace.OperationAsync("Pass", passingSeat,
                    () => runtime.PassAsync(table.GameId, passingSeat, setup.Token));
            }
            Assert.Null(state.ClaimWindow);
            AssertInventory(state);
        }
    }

    private async Task AssertSettingUpdatesPreserveAsync(
        HudsonOwnTurnWsFixture host, HumanTable table, DealMode mode)
    {
        var oppositeDeal = mode == DealMode.Manual ? "HANDS" : "INITIAL";
        var replacements = new[]
        {
            new { back = 1, fives = "111", points = "100", dealType = oppositeDeal, baseUnit = 99 },
            new { back = 2, fives = "121", points = "40", dealType = "UNSHUFFLED", baseUnit = 11 }
        };
        foreach (var replacement in replacements)
        {
            AssertCanonicalMatch(await table.Peer.BarrierAsync(), mode);
            var before = await CaptureBoundaryAsync(host, table);
            var firstFrame = table.Peer.Frames.Count;
            await table.Peer.UpdateAsync([new object[]
            {
                "match", 0, new { dealer = table.State.DealerSeatIndex, conditions = replacement }
            }]);
            var afterWire = await table.Peer.BarrierAsync();
            var responses = table.Peer.Frames.Skip(firstFrame).ToArray();
            var joined = Array.FindIndex(responses,
                frame => frame.GetProperty("type").GetString() == "JOINED");
            Assert.True(joined > 0, "The setting UPDATE must be handled before the JOIN barrier.");

            // The extra full response proves the normal scene-push handler ran;
            // the JOIN's own snapshot cannot stand in for an ignored input.
            var corrections = responses.Take(joined).Where(frame =>
                frame.GetProperty("type").GetString() == "UPDATE"
                && frame.TryGetProperty("full", out var full) && full.GetBoolean()).ToArray();
            Assert.Single(corrections);
            AssertCanonicalMatch(corrections[0], mode);
            AssertCanonicalMatch(afterWire, mode);
            Assert.Equal(before, await CaptureBoundaryAsync(host, table));
            output.WriteLine(
                $"O19 mode={mode} phase={table.State.Phase} settings={JsonSerializer.Serialize(replacement)} " +
                $"correctiveFull=1 stateVersion={before.StateVersion} eventSequence={before.EventSequence} " +
                $"journalRows={before.JournalRows} unchanged.");
        }
    }

    private static async Task<WsPeer> OpenOwnedPeerAsync(
        HudsonOwnTurnWsFixture host, DealMode mode, int seed)
    {
        var room = $"renderer-settings-{Guid.NewGuid():N}";
        var owner = $"test-owner-{Guid.NewGuid():N}";
        return new WsPeer(await host.OpenSocketAsync(
            $"variant=changsha&bots=false&botCount=0&dealMode={mode.ToString().ToLowerInvariant()}" +
            $"&handCount=1&seed={seed}&baseUnit={BaseUnit}", owner), room, owner);
    }

    private static async Task<HumanTable> BindOwnedTableAsync(
        HudsonOwnTurnWsFixture host, WsPeer peer, DealMode mode, int seed, int seat)
    {
        await peer.BarrierAsync();
        await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "gameComplete", "ownTurn" }
            .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
        await peer.BarrierAsync();
        await peer.UpdateAsync([new object[] { "seats", peer.PlayerId, new { seat } }]);
        var wire = await peer.BarrierAsync();
        var game = host.Manager.GetRuntimeGameIdBoundTo(peer.RoomId)
            ?? throw new InvalidOperationException("The normal owned seat did not bind a runtime.");
        Assert.Equal(seat, host.Runtime.TryGetSeatForPlayer(game, peer.PlayerId));
        Assert.True(host.Runtime.TryGetSnapshot(game, out var state));
        Assert.NotNull(state);
        Assert.Equal(mode, state.DealMode);
        Assert.Equal(seed, state.Seed);
        Assert.Equal(BaseUnit, state.BaseUnit);
        Assert.Equal(peer.PlayerId, state.CreatorPlayerId);
        Assert.All(state.Seats, item => Assert.False(item.IsBot));
        AssertCanonicalMatch(wire, mode);
        return new HumanTable(peer.RoomId, game, seat, state, peer);
    }

    private static async Task<Boundary> CaptureBoundaryAsync(HudsonOwnTurnWsFixture host, HumanTable table)
    {
        var state = await host.SnapshotAsync(table);
        AssertInventory(state);
        Assert.All(Enumerable.Range(0, 108),
            tile => Assert.Equal(tile / 4, ChangshaDeckBuilder.GetLogicalTile(tile)));
        Assert.Equal(BaseUnit, state.BaseUnit);
        Assert.Equal(table.Peer.PlayerId, state.CreatorPlayerId);
        var stateJson = JsonSerializer.Serialize(state);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gameId = Guid.Parse(table.GameId);
        var stored = await db.ChangshaGames.AsNoTracking().SingleAsync(row => row.Id == gameId, timeout.Token);
        var journal = await db.ChangshaGameEvents.AsNoTracking().Where(row => row.GameId == gameId)
            .OrderBy(row => row.Sequence).ThenBy(row => row.Id).ToArrayAsync(timeout.Token);
        var persisted = JsonSerializer.Deserialize<ChangshaGameState>(stored.StateJson, StoredJson);
        Assert.NotNull(persisted);
        Assert.Equal(stateJson, JsonSerializer.Serialize(persisted));
        Assert.Equal(state.StateVersion, stored.StateVersion);
        Assert.Equal("changsha-v1", stored.RuleSet);
        Assert.Equal(table.Peer.PlayerId, stored.OwnerPlayerId);
        Assert.NotEmpty(journal);
        Assert.True(state.EventSequence > 0);
        return new Boundary(stateJson, stored.StateJson, stored.StateVersion, state.EventSequence,
            JsonSerializer.Serialize(journal), journal.Length);
    }

    private static void AssertCanonicalMatch(JsonElement frame, DealMode mode)
    {
        var conditions = Entry(frame, "match", 0).GetProperty("conditions");
        Assert.Equal("CHANGSHA", conditions.GetProperty("gameType").GetString());
        Assert.Equal("changsha", conditions.GetProperty("variant").GetString());
        Assert.Equal(0, conditions.GetProperty("back").GetInt32());
        Assert.Equal("000", conditions.GetProperty("fives").GetString());
        Assert.Equal("25", conditions.GetProperty("points").GetString());
        Assert.Equal(JsonValueKind.Number, conditions.GetProperty("baseUnit").ValueKind);
        Assert.Equal(BaseUnit, conditions.GetProperty("baseUnit").GetInt32());
        Assert.Equal(mode.ToString().ToLowerInvariant(), conditions.GetProperty("dealMode").GetString());
        Assert.Equal(mode == DealMode.Manual ? "INITIAL" : "HANDS",
            conditions.GetProperty("dealType").GetString());
    }

    private static JsonElement Entry(JsonElement frame, string kind, int key) =>
        frame.GetProperty("entries").EnumerateArray().Single(entry =>
            entry[0].GetString() == kind && entry[1].ToString() == key.ToString(CultureInfo.InvariantCulture))[2];

    private void RecordLoadedAssemblies()
    {
        foreach (var assembly in new[] { typeof(ChangshaGameState).Assembly, GetType().Assembly })
            output.WriteLine($"Native loaded assembly={assembly.GetName().Name} " +
                $"mvid={assembly.ManifestModule.ModuleVersionId:D} path={assembly.Location}");
    }

    private sealed record Boundary(
        string RuntimeStateJson, string PersistedStateJson, int StateVersion, long EventSequence,
        string JournalJson, int JournalRows);

    private sealed class PreparationTrace : IDisposable, IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>
    {
        private readonly ChangshaGameInstance _instance;
        private readonly CancellationToken _token;
        private readonly ITestOutputHelper _output;
        private readonly long _origin = Stopwatch.GetTimestamp();
        private readonly ConcurrentQueue<Point> _points = new();
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();
        private readonly CancellationTokenRegistration _cancellation;
        private string _operation = "setup";
        private int _index = -1;

        internal PreparationTrace(ChangshaGameInstance instance, CancellationToken token, ITestOutputHelper output)
        {
            _instance = instance;
            _token = token;
            _output = output;
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
            _cancellation = token.Register(() => Mark("cts.cancelled"));
            Mark("cts.total5s");
        }

        internal async Task OperationAsync(string operation, int index, Func<Task> action)
        {
            _operation = operation;
            _index = index;
            Mark("operation.begin");
            var completed = false;
            try
            {
                await action();
                completed = true;
            }
            finally
            {
                Mark(completed ? "operation.completed" : "operation.failed");
            }
        }

        internal void Mark(string kind, Guid? context = null, double? durationMs = null, string? exception = null) =>
            _points.Enqueue(new(Stopwatch.GetTimestamp(), kind, _operation, _index,
                _instance.State.Phase, _instance.State.StateVersion, _instance.State.EventSequence,
                _instance.Lock.CurrentCount, _token.IsCancellationRequested, context, durationMs, exception));

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
                _subscriptions.Add(listener.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Key.Contains("SaveChanges", StringComparison.Ordinal))
                Mark(item.Key, (item.Value as DbContextEventData)?.Context?.ContextId.InstanceId,
                    exception: (item.Value as DbContextErrorEventData)?.Exception.GetType().FullName);
            else if (item.Value is CommandEndEventData command)
                Mark(item.Key, command.Context?.ContextId.InstanceId, command.Duration.TotalMilliseconds,
                    (item.Value as CommandErrorEventData)?.Exception.GetType().FullName);
        }

        public void OnError(Exception error) => Mark("observer.error", exception: error.GetType().FullName);
        public void OnCompleted() { }

        public void Dispose()
        {
            _cancellation.Dispose();
            foreach (var subscription in _subscriptions) subscription.Dispose();
            Mark("observer.stop");
            _output.WriteLine("O19 Auto preparation trace " + JsonSerializer.Serialize(new
            {
                Origin = _origin, Frequency = Stopwatch.Frequency,
                GateOwner = "Not observed; CurrentCount is availability only",
                Overhead = "Buffered test-only operation/EF observations; no gate acquisition or state mutation",
                Points = _points.ToArray()
            }));
        }

        private sealed record Point(long Ticks, string Kind, string Operation, int Index,
            ChangshaPhase Phase, int StateVersion, long EventSequence, int GateAvailable,
            bool TokenCanceled, Guid? ContextId, double? DurationMs, string? ExceptionType);
    }
}
