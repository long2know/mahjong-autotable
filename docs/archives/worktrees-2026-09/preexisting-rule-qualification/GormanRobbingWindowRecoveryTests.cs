using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

[Trait("Category", "RulesQualificationRecovery")]
public sealed class GormanRobbingWindowRecoveryTests(ITestOutputHelper output)
{
    private const string Canary = "gorman-private-recovery-snapshot-not-for-logs";
    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEnumerable<object[]> NonHuTypes() =>
        Enum.GetValues<TableClaimType>().Where(type => type != TableClaimType.Hu)
            .Select(type => new object[] { type });

    public static IEnumerable<object[]> InvalidAliases()
    {
        foreach (var type in NonHuTypes().Select(row => (TableClaimType)row[0]))
        foreach (var startup in new[] { false, true })
        foreach (var operation in new[] { "JOIN", "NEW" })
            yield return [type, startup, operation];
    }

    public static IEnumerable<object[]> OrdinaryClaims()
    {
        foreach (var type in NonHuTypes().Select(row => (TableClaimType)row[0]))
        foreach (var startup in new[] { false, true })
            yield return [type, startup];
    }

    [Theory]
    [MemberData(nameof(InvalidAliases))]
    public async Task NonHuRobbingOffer_RejectsStartupAndOnDemandBeforeAnyJoinedOrFull(
        TableClaimType type, bool duringStartup, string firstOperation)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var good = await host.OpenHumanTableAsync();
        await host.Runtime.FillEmptySeatsWithBotsAsync(good.GameId);
        await host.Runtime.StartGameAsync(good.GameId);
        var goodBefore = await host.SnapshotAsync(good);
        var original = RobbingState();
        var bad = await StoreAsync(host, original, json => MutateOnlyClaimType(json, type));
        if (duringStartup)
        {
            await good.Peer.DisposeAsync();
            await host.RestartAsync();
        }
        var allocatedAtStartup = Instance(host, bad.Id) is not null;
        var logs = new RecoveryLogs();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        using var client = Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>()).CreateClient();
        using var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        foreach (var operation in new[] { firstOperation, firstOperation == "JOIN" ? "NEW" : "JOIN", firstOperation })
        {
            await AssertRejectedSocketAsync(host, bad, operation, type);
            Assert.False(allocatedAtStartup);
            var failure = await Assert.ThrowsAsync<PublicRoomRecoveryException>(() =>
                host.Runtime.RestorePublicRoomAsync(bad.Room));
            Assert.Equal("room-snapshot-invalid", failure.Reason);
            AssertNoBrokenRuntime(host, bad);
            Assert.Equal(1, host.Runtime.GameCount);
            await AssertStoredAsync(host, bad, 2);
        }
        await host.Runtime.HydrateAsync(host.Services);
        AssertNoBrokenRuntime(host, bad);
        Assert.Contains(logs.Messages, message => message.Contains("room-snapshot-invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(Canary, StringComparison.Ordinal)
            || message.Contains("\"concealedTiles\"", StringComparison.Ordinal)
            || message.Contains("\"wall\"", StringComparison.Ordinal)
            || message.Contains(bad.Json, StringComparison.Ordinal));

        var unchangedGood = await host.Runtime.TryGetSnapshotCopyAsync(good.GameId);
        Assert.NotNull(unchangedGood);
        Assert.Equal(goodBefore.StateVersion, unchangedGood.StateVersion);
        Assert.Equal(goodBefore.Phase, unchangedGood.Phase);
        await using var owner = await host.ConnectAsync(good.RoomId, good.Peer.PlayerId);
        await owner.UpdateAsync([new object[] { "seats", owner.PlayerId, new { seat = 0 } }]);
        await owner.BarrierAsync();
        await owner.UpdateAsync([new object[] { "pickup", "rollDice", new { seatIndex = 0 } }]);
        await owner.BarrierAsync();
        var progressed = await host.Runtime.TryGetSnapshotCopyAsync(good.GameId);
        Assert.NotNull(progressed);
        Assert.Equal(ChangshaPhase.BreakPointMarked, progressed.Phase);
        Assert.True(progressed.StateVersion > goodBefore.StateVersion);
        Assert.Equal(good.GameId, host.Manager.GetRuntimeGameIdBoundTo(good.RoomId));
        await AssertStoredAsync(host, bad, 2);
    }

    [Theory]
    [MemberData(nameof(NonHuTypes))]
    public void SingleFieldMutation_ContradictsTheExistingHuOnlyResolver(TableClaimType type)
    {
        var original = RobbingState();
        var json = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(original, SnapshotJson));
        MutateOnlyClaimType(json, type);
        var corrupted = JsonSerializer.Deserialize<ChangshaGameState>(json, SnapshotJson)!;
        var before = JsonSerializer.Serialize(corrupted, SnapshotJson);
        var opportunity = Assert.Single(corrupted.ClaimWindow!.Opportunities);
        var failure = Assert.Throws<InvalidOperationException>(() =>
            ChangshaGameStateMachine.ResolveClaim(corrupted, opportunity.SeatIndex, type));
        Assert.Equal($"Only Hu claims are valid on a robbing-the-added-kong window; got {type}.", failure.Message);
        Assert.Equal(before, JsonSerializer.Serialize(corrupted, SnapshotJson));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GenuineHuOffer_RecoversAndResolvesThroughThePublicRuntime(
        bool duringStartup, bool claimHu)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var original = RobbingState();
        var saved = await StoreAsync(host, original);
        if (duringStartup) await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        await AssertStoredAsync(host, saved, 1);
        var seat = Assert.Single(original.ClaimWindow!.Opportunities).SeatIndex;
        await using var peer = await host.ConnectAsync(saved.Room, original.Seats[seat].PlayerId);
        AssertOffered(await peer.BarrierAsync(), seat, TableClaimType.Hu);
        var ready = await SnapshotAsync(host, saved);
        Assert.Equal(original.StateVersion, ready.StateVersion);
        Assert.False(Assert.IsAssignableFrom<CancellationTokenSource>(Instance(host, saved.Id)!.ClaimWindowCts).IsCancellationRequested);
        if (claimHu)
        {
            await host.Runtime.ClaimAsync(saved.Id.ToString(), seat, "Hu", null,
                expectedVersion: ready.StateVersion, expectedPlayerId: ready.Seats[seat].PlayerId);
            var won = await SnapshotAsync(host, saved);
            Assert.Equal(ChangshaPhase.GameComplete, won.Phase);
            Assert.True(won.CurrentWin!.IsRobbedKong);
            Assert.Equal(WinMethod.RobbingKong, won.CurrentWin.Method);
            Assert.Equal(seat, won.CurrentWin.WinningSeatIndex);
            Assert.Equal(MeldKind.Pung, Assert.Single(won.Hands[0].Melds).Kind);
            Assert.DoesNotContain(19, won.Hands[0].ConcealedTiles);
            Assert.Contains(19, won.Hands[seat].ConcealedTiles);
            Assert.Equal(original.Wall, won.Wall);
            HudsonOwnTurnWsFixture.AssertInventory(won);
        }
        else
        {
            await host.Runtime.PassAsync(saved.Id.ToString(), seat,
                expectedVersion: ready.StateVersion, expectedPlayerId: ready.Seats[seat].PlayerId);
            AssertCanonicalAddedKong(original, await SnapshotAsync(host, saved));
        }
        AssertResolvedWindow(host, saved);
        var resolved = JsonSerializer.Serialize(await SnapshotAsync(host, saved), SnapshotJson);
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        Assert.Equal(resolved, JsonSerializer.Serialize(await SnapshotAsync(host, saved), SnapshotJson));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyLegacyRobbingOffers_RemainRecoverableAndTimeoutCompletesCanonicalKong(bool duringStartup)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var original = RobbingState();
        original.ClaimWindow!.Opportunities.Clear();
        var canonical = Clone(original);
        ChangshaGameStateMachine.PassClaim(canonical);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, canonical.Phase);
        var saved = await StoreAsync(host, original);
        if (duringStartup) await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        await AssertStoredAsync(host, saved, 1);
        Assert.Empty((await SnapshotAsync(host, saved)).ClaimWindow!.Opportunities);
        host.Services.GetRequiredService<IOptions<ChangshaRuntimeOptions>>().Value.ClaimWindowTimeoutMs = 0;
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while ((await SnapshotAsync(host, saved)).Phase == ChangshaPhase.AwaitingClaim)
            await Task.Delay(10, deadline.Token);
        AssertCanonicalAddedKong(original, await SnapshotAsync(host, saved));
        AssertResolvedWindow(host, saved);
    }

    [Theory]
    [MemberData(nameof(OrdinaryClaims))]
    public async Task GenuineOrdinaryNonHuOffer_RemainsRecoverableAndClaimable(
        TableClaimType type, bool duringStartup)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var table = await host.OpenHumanTableAsync();
        var draw = HudsonOwnTurnWsFixture.ArrangeBeforeDraw(table,
            type == TableClaimType.Chow ? "chow-options" : "pung-no-draw");
        HudsonOwnTurnWsFixture.ArrangeIncomingDiscard(table, draw);
        await host.Runtime.DiscardAsync(table.GameId, 3, draw.DrawTile);
        var original = await host.SnapshotAsync(table);
        Assert.Equal(ChangshaPhase.AwaitingClaim, original.Phase);
        Assert.False(original.ClaimWindow!.IsKongRobbing);
        Assert.Contains(original.ClaimWindow.Opportunities, offer => offer.SeatIndex == 0 && offer.ClaimType == type);
        HudsonOwnTurnWsFixture.AssertInventory(original);
        var saved = await StoreAsync(host, original);
        await table.Peer.DisposeAsync();
        if (duringStartup) await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        await using var peer = await host.ConnectAsync(saved.Room, original.Seats[0].PlayerId);
        AssertOffered(await peer.BarrierAsync(), 0, type);
        foreach (var seat in original.ClaimWindow.Opportunities.Select(offer => offer.SeatIndex).Distinct()
            .Where(seat => seat != 0))
        {
            var current = await SnapshotAsync(host, saved);
            await host.Runtime.PassAsync(saved.Id.ToString(), seat,
                expectedVersion: current.StateVersion, expectedPlayerId: current.Seats[seat].PlayerId);
        }
        var ready = await SnapshotAsync(host, saved);
        await host.Runtime.ClaimAsync(saved.Id.ToString(), 0, type.ToString(),
            type == TableClaimType.Chow ? [12, 20] : null,
            expectedVersion: ready.StateVersion, expectedPlayerId: ready.Seats[0].PlayerId);
        var claimed = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, claimed.Phase);
        Assert.Equal(0, claimed.ActiveSeatIndex);
        Assert.Equal(14, claimed.Hands[0].ConcealedTiles.Count + 3 * claimed.Hands[0].Melds.Count);
        Assert.Equal(type switch
        {
            TableClaimType.Pung => MeldKind.Pung,
            TableClaimType.Chow => MeldKind.Chow,
            TableClaimType.Kong => MeldKind.ExposedKong,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        }, Assert.Single(claimed.Hands[0].Melds).Kind);
        Assert.Equal(original.Wall.Count - (type == TableClaimType.Kong ? 1 : 0), claimed.Wall.Count);
        Assert.Equal(type == TableClaimType.Kong ? 0 : (int?)null, claimed.LastDrawSeatIndex);
        Assert.Equal(type == TableClaimType.Kong, claimed.LastDrawWasKongReplacement);
        AssertResolvedWindow(host, saved);
        HudsonOwnTurnWsFixture.AssertInventory(claimed);
    }

    private static ChangshaGameState RobbingState()
    {
        // Reuse the frozen, genuine draw/added-kong fixture without changing its author's tests.
        var method = typeof(FrostRecoveredSnapshotPreconditionsTests)
            .GetMethod("ClaimState", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var state = Assert.IsType<ChangshaGameState>(method.Invoke(null, [true]));
        Assert.True(state.ClaimWindow!.IsKongRobbing);
        Assert.Equal(TableClaimType.Hu, Assert.Single(state.ClaimWindow.Opportunities).ClaimType);
        Assert.Contains(state.EventLog, entry => entry.EventType == "added-kong-declared");
        HudsonOwnTurnWsFixture.AssertInventory(state);
        return state;
    }

    private static ChangshaGameState Clone(ChangshaGameState state) =>
        JsonSerializer.Deserialize<ChangshaGameState>(JsonSerializer.Serialize(state, SnapshotJson), SnapshotJson)!;

    private static void MutateOnlyClaimType(JsonObject json, TableClaimType type)
    {
        Assert.NotEqual(TableClaimType.Hu, type);
        Assert.True(Enum.IsDefined(type));
        var before = json.DeepClone();
        var offer = json["claimWindow"]!["opportunities"]![0]!;
        Assert.Equal((int)TableClaimType.Hu, offer["claimType"]!.GetValue<int>());
        offer["claimType"] = (int)type;
        Assert.False(JsonNode.DeepEquals(before, json));
        offer["claimType"] = (int)TableClaimType.Hu;
        Assert.True(JsonNode.DeepEquals(before, json), "Only the first opportunity's claimType may change.");
        offer["claimType"] = (int)type;
    }

    private static void AssertCanonicalAddedKong(ChangshaGameState original, ChangshaGameState actual)
    {
        var expected = Clone(original);
        ChangshaGameStateMachine.PassClaim(expected);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, actual.Phase);
        Assert.Equal(0, actual.ActiveSeatIndex);
        Assert.Equal(JsonSerializer.Serialize(expected.Hands, SnapshotJson), JsonSerializer.Serialize(actual.Hands, SnapshotJson));
        Assert.Equal(expected.Wall, actual.Wall);
        Assert.Equal(original.Wall.Count - 1, actual.Wall.Count);
        Assert.Equal(MeldKind.AddedKong, Assert.Single(actual.Hands[0].Melds).Kind);
        Assert.Equal(new[] { 16, 17, 18, 19 }, actual.Hands[0].Melds[0].TileIds);
        Assert.Equal(original.Wall[^1], actual.Hands[0].ConcealedTiles[^1]);
        Assert.Equal(14, actual.Hands[0].ConcealedTiles.Count + 3 * actual.Hands[0].Melds.Count);
        Assert.Equal(0, actual.LastDrawSeatIndex);
        Assert.True(actual.LastDrawWasKongReplacement);
        Assert.Single(actual.EventLog.Skip(original.EventLog.Count), entry => entry.EventType == "kong-replacement-drawn");
        HudsonOwnTurnWsFixture.AssertInventory(actual);
    }

    private sealed record StoredSnapshot(Guid Id, string Room, string Key, string Json);

    private static async Task<StoredSnapshot> StoreAsync(
        HudsonOwnTurnWsFixture host, ChangshaGameState state, Action<JsonObject>? mutate = null)
    {
        var id = Guid.NewGuid();
        var room = $"gorman-recovery-{Guid.NewGuid():N}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(room))).ToLowerInvariant();
        var json = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(state, SnapshotJson));
        json["gameId"] = id.ToString();
        json["publicName"] = Canary;
        mutate?.Invoke(json);
        var text = json.ToJsonString();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangshaGames.Add(new ChangshaGame
        {
            Id = id, RuleSet = "changsha-v1", Seed = state.Seed, StateJson = text,
            StateVersion = state.StateVersion, CurrentHandNumber = state.HandNumber,
            CurrentRoundNumber = state.RoundNumber, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow
        });
        db.AutotableRoomBindings.Add(new AutotableRoomBinding
        {
            RoomKey = key, RoomId = room, RuntimeGameId = id, CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new StoredSnapshot(id, room, key, text);
    }

    private static async Task AssertStoredAsync(HudsonOwnTurnWsFixture host, StoredSnapshot saved, int rows)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(rows, await db.ChangshaGames.CountAsync());
        Assert.Equal(rows, await db.AutotableRoomBindings.CountAsync());
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == saved.Id);
        Assert.True(string.Equals(saved.Json, row.StateJson, StringComparison.Ordinal), "Stored snapshot bytes changed.");
        var binding = await db.AutotableRoomBindings.AsNoTracking().SingleAsync(item => item.RoomKey == saved.Key);
        Assert.Equal(saved.Id, binding.RuntimeGameId);
        Assert.Equal(saved.Room, binding.RoomId);
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, StoredSnapshot saved) =>
        await host.Runtime.TryGetSnapshotCopyAsync(saved.Id.ToString())
        ?? throw new InvalidOperationException("Expected the exact saved runtime.");

    private static T RuntimeField<T>(HudsonOwnTurnWsFixture host, string field) where T : class =>
        Assert.IsType<T>(typeof(ChangshaGameRuntime).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(host.Runtime));

    private static ChangshaGameInstance? Instance(HudsonOwnTurnWsFixture host, Guid id) =>
        RuntimeField<ConcurrentDictionary<string, ChangshaGameInstance>>(host, "_games").GetValueOrDefault(id.ToString());

    private static void AssertNoBrokenRuntime(HudsonOwnTurnWsFixture host, StoredSnapshot saved)
    {
        Assert.Null(Instance(host, saved.Id));
        Assert.False(host.Runtime.TryGetSnapshot(saved.Id.ToString(), out _));
        Assert.False(RuntimeField<ConcurrentDictionary<string, string>>(host, "_publicRoomBindings").ContainsKey(saved.Room));
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(saved.Room));
    }

    private static void AssertResolvedWindow(HudsonOwnTurnWsFixture host, StoredSnapshot saved)
    {
        var instance = Assert.IsType<ChangshaGameInstance>(Instance(host, saved.Id));
        Assert.Null(instance.State.ClaimWindow);
        Assert.Empty(instance.PendingClaims);
        Assert.True(Assert.IsAssignableFrom<CancellationTokenSource>(instance.ClaimWindowCts).IsCancellationRequested);
    }

    private static void AssertOffered(JsonElement full, int seat, TableClaimType type)
    {
        Assert.True(full.GetProperty("full").GetBoolean());
        var claim = Assert.Single(full.GetProperty("entries").EnumerateArray(),
            entry => entry[0].GetString() == "claim" && entry[1].ToString() == seat.ToString())[2];
        Assert.Contains(type.ToString(), claim.GetProperty("available").EnumerateArray().Select(value => value.GetString()));
    }

    private async Task AssertRejectedSocketAsync(
        HudsonOwnTurnWsFixture host, StoredSnapshot saved, string operation, TableClaimType type)
    {
        using var socket = await host.OpenSocketAsync(
            $"variant=changsha&gameId={saved.Room}&bots=false&botCount=0&dealMode=manual&seed=42");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = operation, gameId = saved.Room }),
            WebSocketMessageType.Text, true, deadline.Token);
        var first = await ReadJsonAsync(socket, deadline.Token);
        if (first.GetProperty("type").GetString() == "JOINED")
        {
            var full = await ReadJsonAsync(socket, deadline.Token);
            var state = await SnapshotAsync(host, saved);
            var offer = Assert.Single(state.ClaimWindow!.Opportunities);
            AssertOffered(full, offer.SeatIndex, type);
            await DescribeUnsafeBaselineAsync(host, saved, type, deadline.Token);
        }
        Assert.Equal("UPDATE", first.GetProperty("type").GetString());
        Assert.False(first.GetProperty("full").GetBoolean());
        Assert.True(Encoding.UTF8.GetByteCount(first.GetRawText()) <= 4096);
        var rejection = Assert.Single(first.GetProperty("entries").EnumerateArray());
        Assert.Equal("actionRejected", rejection[0].GetString());
        Assert.Equal("current", rejection[1].GetString());
        Assert.Equal("room", rejection[2].GetProperty("action").GetString());
        Assert.Equal("room-snapshot-invalid", rejection[2].GetProperty("reason").GetString());
        Assert.DoesNotContain(Canary, first.GetRawText());
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[4096]), deadline.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal(WebSocketCloseStatus.InternalServerError, close.CloseStatus);
        Assert.Equal("room-snapshot-invalid", close.CloseStatusDescription);
    }

    private async Task DescribeUnsafeBaselineAsync(
        HudsonOwnTurnWsFixture host, StoredSnapshot saved, TableClaimType type, CancellationToken ct)
    {
        var instance = Assert.IsType<ChangshaGameInstance>(Instance(host, saved.Id));
        while (instance.ClaimWindowCts is null) await Task.Delay(10, ct);
        var timer = instance.ClaimWindowCts;
        Assert.False(timer.IsCancellationRequested);
        Assert.Empty(instance.PendingClaims);
        var state = await SnapshotAsync(host, saved);
        var seat = Assert.Single(state.ClaimWindow!.Opportunities).SeatIndex;
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Runtime.ClaimAsync(saved.Id.ToString(), seat, type.ToString(),
                type == TableClaimType.Chow ? [8, 12] : null,
                expectedVersion: state.StateVersion, expectedPlayerId: state.Seats[seat].PlayerId));
        Assert.Equal($"Only Hu claims are valid on a robbing-the-added-kong window; got {type}.", failure.Message);
        Assert.True(timer.IsCancellationRequested);
        Assert.Equal(type, Assert.Single(instance.PendingClaims).Value!.ClaimType);
        Assert.Equal(ChangshaPhase.AwaitingClaim, instance.State.Phase);
        Assert.Same(instance, Instance(host, saved.Id));
        Assert.Equal(saved.Id.ToString(), host.Manager.GetRuntimeGameIdBoundTo(saved.Room));
        output.WriteLine($"OLD PUBLIC FAILURE: FULL advertised {type}; exact-version/owner ClaimAsync threw "
            + "the Hu-only invariant after cancelling its timer; one response and the broken runtime remain cached.");
    }

    private static async Task<JsonElement> ReadJsonAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var bytes = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            Assert.Equal(WebSocketMessageType.Text, frame.MessageType);
            bytes.Write(buffer, 0, frame.Count);
            Assert.True(bytes.Length <= 256 * 1024);
        } while (!frame.EndOfMessage);
        using var json = JsonDocument.Parse(bytes.ToArray());
        return json.RootElement.Clone();
    }

    private sealed class RecoveryLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecoveryLogger(Messages);
        public void Dispose() { }

        private sealed class RecoveryLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                    messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception));
            }
        }
    }
}
