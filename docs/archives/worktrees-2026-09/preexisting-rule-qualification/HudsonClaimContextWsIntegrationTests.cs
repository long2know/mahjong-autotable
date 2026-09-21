using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Tables;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonClaimContextWsIntegrationTests(ITestOutputHelper output)
{
    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task DeadlineZeroIndistinguishableWindows_RejectOldVersionThenAcceptCurrentChoice()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        var old = await OpenWindowAsync(host, table, winning: true);
        var oldPacket = ClaimPacket(old, "Chow", [4, 8]);
        var firstHand = Live(host, table).HandNumber;
        Assert.Contains("Hu", old.GetProperty("available").EnumerateArray().Select(value => value.GetString()));

        await SendAsync(table.Claimant, ClaimPacket(old, "Hu"));
        await table.Claimant.BarrierAsync();
        var dealt = await SnapshotAsync(host, table);
        Assert.Equal(firstHand + 1, dealt.HandNumber);
        Assert.Equal(1, dealt.DealerSeatIndex);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, dealt.Phase);
        Assert.Contains(dealt.CumulativeScores.Values, score => score != 0);
        AssertInventory(dealt);

        ArrangeSafeRotation(Live(host, table));
        for (var seat = 1; seat <= 3; seat++)
        {
            var current = await SnapshotAsync(host, table);
            Assert.Equal(seat, current.ActiveSeatIndex);
            var tile = 103 + seat;
            Assert.Contains(tile, current.Hands[seat].ConcealedTiles);
            Assert.Empty(new ClaimAdjudicator().GetOpportunities(seat, tile, current.Hands));
            await table.Peers[seat].UpdateAsync([new object[] { "discard", seat, new { tileId = tile } }]);
            await table.Peers[seat].BarrierAsync();
        }
        var sourceReady = await SnapshotAsync(host, table);
        Assert.Equal(0, sourceReady.ActiveSeatIndex);
        Assert.Equal(0, sourceReady.LastDrawSeatIndex);

        var fresh = await OpenWindowAsync(host, table, winning: true);
        Assert.Equal(LegacyDescriptor(old), LegacyDescriptor(fresh));
        Assert.Equal(old.GetProperty("gameId").GetString(), fresh.GetProperty("gameId").GetString());
        Assert.True(fresh.GetProperty("stateVersion").GetInt32() > old.GetProperty("stateVersion").GetInt32());
        var before = await SnapshotAsync(host, table);
        await RejectWithoutMutationAsync(host, table, table.Claimant, oldPacket, "stale-version");
        Assert.Equal(fresh.GetProperty("stateVersion").GetInt32(),
            Claim(await table.Claimant.BarrierAsync()).GetProperty("stateVersion").GetInt32());

        await SendAsync(table.Claimant, ClaimPacket(fresh, "Chow", [4, 8]));
        await table.Claimant.BarrierAsync();
        AssertChow(before, await SnapshotAsync(host, table), [4, 8]);
        output.WriteLine($"Repeated deadline0 windows game={table.GameId} oldVersion={old.GetProperty("stateVersion")} newVersion={fresh.GetProperty("stateVersion")}; stale rejected, current Chow committed.");
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task EqualVersionOwnedRooms_RejectSourceGameIdAfterSameSocketJoin()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        var player = $"hudson-claim-owner-{Guid.NewGuid():N}";
        await using var source = await OpenTableAsync(host, player);
        await using var destination = await OpenTableAsync(host, player);
        var old = await OpenWindowAsync(host, source);
        var current = await OpenWindowAsync(host, destination);
        Assert.Equal(LegacyDescriptor(old), LegacyDescriptor(current));
        Assert.Equal(old.GetProperty("stateVersion").GetInt32(), current.GetProperty("stateVersion").GetInt32());
        Assert.NotEqual(old.GetProperty("gameId").GetString(), current.GetProperty("gameId").GetString());
        var sourceBefore = await SnapshotAsync(host, source);
        var destinationBefore = await SnapshotAsync(host, destination);

        var joined = Claim(await source.Claimant.BarrierAsync(destination.RoomId));
        Assert.Equal(destination.GameId, joined.GetProperty("gameId").GetString());
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(destination.GameId, source.Claimant.PlayerId));
        await RejectWithoutMutationAsync(host, destination, source.Claimant,
            ClaimPacket(old, "Chow", [12, 20]), "stale-game");
        AssertUnchanged(sourceBefore, await SnapshotAsync(host, source));

        await SendAsync(source.Claimant, ClaimPacket(joined, "Chow", [20, 12]));
        await source.Claimant.BarrierAsync();
        AssertChow(destinationBefore, await SnapshotAsync(host, destination), [12, 20]);
        AssertUnchanged(sourceBefore, await SnapshotAsync(host, source));
        output.WriteLine($"Cross-room same-owner replay source={source.GameId} destination={destination.GameId} equalVersion={joined.GetProperty("stateVersion")}; source rejected, destination choice committed.");
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("game-only")]
    [InlineData("version-only")]
    [InlineData("null-game")]
    [InlineData("null-version")]
    [InlineData("string-version")]
    [InlineData("negative-version")]
    public async Task MalformedContext_RejectsWithoutConsumingWindowAndCurrentChoiceStillWorks(string malformed)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        var available = await OpenWindowAsync(host, table);
        var packet = ClaimPacket(available, "Chow", [12, 20]);
        switch (malformed)
        {
            case "game-only": packet.Remove("expectedVersion"); break;
            case "version-only": packet.Remove("gameId"); break;
            case "null-game": packet["gameId"] = null; break;
            case "null-version": packet["expectedVersion"] = null; break;
            case "string-version": packet["expectedVersion"] = available.GetProperty("stateVersion").ToString(); break;
            case "negative-version": packet["expectedVersion"] = -1; break;
            default: throw new ArgumentOutOfRangeException(nameof(malformed));
        }
        var before = await SnapshotAsync(host, table);
        await RejectWithoutMutationAsync(host, table, table.Claimant, packet, "invalid-claim-context");
        await SendAsync(table.Claimant, ClaimPacket(available, "Chow", [12, 20]));
        await table.Claimant.BarrierAsync();
        AssertChow(before, await SnapshotAsync(host, table), [12, 20]);
        output.WriteLine($"Malformed context={malformed} rejected; same game={table.GameId} current window remains usable.");
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task CurrentContext_UsesStringSeatAndCommitsTheSelectedPhysicalPair()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        var available = await OpenWindowAsync(host, table);
        Assert.Equal(3, available.GetProperty("chowOptions").GetArrayLength());
        var before = await SnapshotAsync(host, table);
        var mark = table.Claimant.Frames.Count;
        await SendAsync(table.Claimant, ClaimPacket(available, "Chow", [20, 12]));
        await table.Claimant.BarrierAsync();
        Assert.DoesNotContain(table.Claimant.Frames.Skip(mark).SelectMany(Entries),
            entry => entry[0].GetString() == "actionRejected");
        var after = await SnapshotAsync(host, table);
        AssertChow(before, after, [12, 20]);
        Assert.Contains(8, after.Hands[1].ConcealedTiles);
        Assert.Contains(24, after.Hands[1].ConcealedTiles);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("explicit")]
    [InlineData("omitted")]
    [InlineData("empty")]
    public async Task UnversionedCompatibility_PreservesExplicitAndLegacyNoChoiceCommands(string choice)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        await OpenWindowAsync(host, table);
        var before = await SnapshotAsync(host, table);
        var packet = new Dictionary<string, object?> { ["action"] = "claim", ["type"] = "Chow" };
        if (choice == "explicit") packet["tileIds"] = new[] { 12, 20 };
        else if (choice == "empty") packet["tileIds"] = Array.Empty<int>();
        Assert.False(packet.ContainsKey("gameId"));
        Assert.False(packet.ContainsKey("expectedVersion"));

        await SendAsync(table.Claimant, packet);
        await table.Claimant.BarrierAsync();
        AssertChow(before, await SnapshotAsync(host, table), choice == "explicit" ? [12, 20] : [8, 12]);
        output.WriteLine($"Legacy both-absent context accepted choice={choice} game={table.GameId}; compatibility, not stale-message protection.");
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("duplicate", "invalid-claim-choice")]
    [InlineData("non-sequence", "invalid-claim-choice")]
    [InlineData("out-of-range", "invalid-claim-command")]
    [InlineData("not-array", "invalid-claim-command")]
    public async Task InvalidCurrentChoice_PreservesPendingHuWindowAndTimer_ThenValidChoiceCompletes(
        string invalid, string expectedReason)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        var available = await OpenContestedWindowAsync(host, table);
        var instance = RuntimeInstance(host, table);
        var before = await SnapshotAsync(host, table);
        var internals = await ReadWindowAsync(instance);
        Assert.Equal(0, internals.PendingCount);
        var packet = ClaimPacket(available, "Chow", [12, 20]);
        packet["tileIds"] = invalid switch
        {
            "duplicate" => new[] { 12, 12 },
            "non-sequence" => new[] { 8, 20 },
            "out-of-range" => new[] { 12, 108 },
            "not-array" => "12,20",
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };

        await RejectWithoutMutationAsync(host, table, table.Claimant, packet, expectedReason);
        var rejected = await ReadWindowAsync(instance);
        Assert.Same(internals.Window, rejected.Window);
        Assert.Same(internals.Timer, rejected.Timer);
        Assert.Equal(internals.PendingJson, rejected.PendingJson);
        Assert.Equal(0, rejected.PendingCount);
        AssertUnchanged(before, await SnapshotAsync(host, table));

        await SendAsync(table.Claimant, ClaimPacket(available, "Chow", [12, 20]));
        await table.Claimant.BarrierAsync();
        var pending = await ReadWindowAsync(instance);
        Assert.Same(internals.Window, pending.Window);
        Assert.Same(internals.Timer, pending.Timer);
        Assert.Equal(1, pending.PendingCount);
        AssertUnchanged(before, await SnapshotAsync(host, table));
        await PassCompetingHuAsync(table);
        AssertChow(before, await SnapshotAsync(host, table), [12, 20]);
        output.WriteLine($"Current invalid choice={invalid} rejected as {expectedReason}; pending queue/window/timer unchanged; valid Chow plus genuine competing-Hu pass completed game={table.GameId}.");
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task DelayedResolverForOldWindow_CannotConsumeNewQueuedChoiceOrCancelItsTimer()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await OpenTableAsync(host);
        var old = await OpenContestedWindowAsync(host, table);
        var instance = RuntimeInstance(host, table);
        var oldWindow = (await ReadWindowAsync(instance)).Window;
        var firstHand = Live(host, table).HandNumber;
        var decline = ClaimPacket(old, "Chow");
        decline["action"] = "pass";
        decline["type"] = null;
        await SendAsync(table.Claimant, decline);
        await table.Claimant.BarrierAsync();
        var firstHu = CompetingHuClaim(await table.Peers[2].BarrierAsync());
        await table.Peers[2].UpdateAsync([new object[] { "claim", "2", ClaimPacket(firstHu, "Hu") }]);
        await table.Peers[2].BarrierAsync();
        Assert.Equal(firstHand + 1, Live(host, table).HandNumber);

        var nextDealer = Live(host, table).DealerSeatIndex;
        ArrangeRotationFromCurrentDealer(Live(host, table));
        for (var seat = nextDealer; seat <= 3; seat++)
        {
            var current = await SnapshotAsync(host, table);
            Assert.Equal(seat, current.ActiveSeatIndex);
            var tile = 104 + seat - nextDealer;
            Assert.Contains(tile, current.Hands[seat].ConcealedTiles);
            Assert.Empty(new ClaimAdjudicator().GetOpportunities(seat, tile, current.Hands));
            await table.Peers[seat].UpdateAsync([new object[] { "discard", seat, new { tileId = tile } }]);
            await table.Peers[seat].BarrierAsync();
        }
        var fresh = await OpenContestedWindowAsync(host, table);
        Assert.Equal(LegacyDescriptor(old), LegacyDescriptor(fresh));
        Assert.True(fresh.GetProperty("stateVersion").GetInt32() > old.GetProperty("stateVersion").GetInt32());
        await SendAsync(table.Claimant, ClaimPacket(fresh, "Chow", [12, 20]));
        await table.Claimant.BarrierAsync();
        var before = await SnapshotAsync(host, table);
        var pending = await ReadWindowAsync(instance);
        Assert.NotSame(oldWindow, pending.Window);
        Assert.Equal(1, pending.PendingCount);
        Assert.Contains(before.ClaimWindow!.Opportunities,
            opportunity => opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Hu);

        // Replay the real resolver's already-captured callback context without modifying runtime state.
        var resolver = host.Runtime.GetType().GetMethod(
            "ResolveClaimWindowAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(resolver);
        var callback = Assert.IsAssignableFrom<Task>(resolver.Invoke(
            host.Runtime, new object?[] { instance, CancellationToken.None, oldWindow }));
        await callback.WaitAsync(TimeSpan.FromSeconds(5));
        await table.Claimant.BarrierAsync();
        AssertUnchanged(before, await SnapshotAsync(host, table));
        var preserved = await ReadWindowAsync(instance);
        Assert.Same(pending.Window, preserved.Window);
        Assert.Same(pending.Timer, preserved.Timer);
        Assert.Equal(pending.PendingJson, preserved.PendingJson);
        Assert.Equal(1, preserved.PendingCount);

        await PassCompetingHuAsync(table);
        AssertChow(before, await SnapshotAsync(host, table), [12, 20]);
        output.WriteLine($"Old resolver context ignored game={table.GameId}; oldVersion={old.GetProperty("stateVersion")} newVersion={fresh.GetProperty("stateVersion")}; new pending choice and uncancelled timer preserved, normal current pass completed Chow.");
    }

    private async Task<ContextTable> OpenTableAsync(HudsonOwnTurnWsFixture host, string? claimantId = null)
    {
        var room = $"hudson-claim-context-{Guid.NewGuid():N}";
        var peers = new List<WsPeer>();
        try
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var player = seat == 1 && claimantId is not null ? claimantId : $"hudson-claim-{Guid.NewGuid():N}";
                var socket = await host.OpenSocketAsync(
                    $"variant=changsha&gameId={room}&bots=false&botCount=0&dealMode=auto&handCount=4&seed=20260914", player);
                var peer = new WsPeer(socket, room, player, output);
                peers.Add(peer);
                await peer.BarrierAsync();
                await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "ownTurn", "gameComplete" }
                    .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
                await peer.UpdateAsync([new object[] { "seats", player, new { seat } }]);
                await peer.BarrierAsync();
            }
            var game = host.Manager.GetRuntimeGameIdBoundTo(room)
                ?? throw new InvalidOperationException("Normal seats did not create a runtime.");
            var table = new ContextTable(room, game, peers.ToArray());
            var state = await SnapshotAsync(host, table);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
            Assert.Equal(0, state.ActiveSeatIndex);
            Assert.Equal(0, state.LastDrawSeatIndex);
            Assert.Equal(DealMode.Auto, state.DealMode);
            Assert.Equal(4, state.MaxHands);
            Assert.All(state.Seats, seat => Assert.False(seat.IsBot));
            Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(game, table.Claimant.PlayerId));
            AssertInventory(state);
            return table;
        }
        catch
        {
            foreach (var peer in peers) await peer.DisposeAsync();
            throw;
        }
    }

    private async Task<JsonElement> OpenWindowAsync(
        HudsonOwnTurnWsFixture host, ContextTable table, bool winning = false)
    {
        var state = Live(host, table);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Null(state.ClaimWindow);
        int[] waiting = winning
            ? [4, 8, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53]
            : [8, 12, 20, 24, 40, 41, 42, 44, 45, 46, 72, 76, 88];
        var incoming = winning ? 0 : 19;
        int[] fillers = winning
            ? [1, 2, 3, 4, 6, 7, 8, 9, 10, 14, 15, 16, 17]
            : [0, 1, 2, 3, 5, 6, 7, 8, 9, 14, 15, 16, 17];
        var used = waiting.Append(incoming).ToHashSet();
        var desired = new Dictionary<int, int[]> { [1] = waiting };
        foreach (var seat in new[] { 0, 2, 3 })
        {
            var hand = new List<int>();
            foreach (var logical in fillers)
            {
                var tile = Enumerable.Range(logical * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                hand.Add(tile);
            }
            if (seat == 0) hand.Add(incoming);
            desired[seat] = hand.ToArray();
        }
        PlaceHands(state, desired);
        var before = await SnapshotAsync(host, table);
        await table.Peers[0].UpdateAsync([new object[] { "discard", 0, new { tileId = incoming } }]);
        await table.Peers[0].BarrierAsync();
        state = Live(host, table);
        Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
        Assert.Equal(0, state.ClaimWindow!.DiscardSeatIndex);
        Assert.Equal(incoming, state.ClaimWindow.DiscardTileId);
        Assert.All(state.ClaimWindow.Opportunities, opportunity => Assert.Equal(1, opportunity.SeatIndex));
        Assert.Contains(state.ClaimWindow.Opportunities, opportunity => opportunity.ClaimType == TableClaimType.Chow);
        Assert.True(state.StateVersion > before.StateVersion);
        Assert.Contains(state.EventLog.Skip(before.EventLog.Count), entry => entry.EventType == "claim-window-open");

        // Model a legacy/rehydrated unknown deadline; the window itself came from a real discard.
        state.ClaimWindow.OpenedAtUnixMs = 0;
        var available = Claim(await table.Claimant.BarrierAsync());
        Assert.Equal(table.GameId, available.GetProperty("gameId").GetString());
        Assert.NotEqual(table.RoomId, available.GetProperty("gameId").GetString());
        Assert.Equal(state.StateVersion, available.GetProperty("stateVersion").GetInt32());
        Assert.Equal(0, available.GetProperty("deadline").GetInt64());
        Assert.Equal(0, available.GetProperty("source").GetInt32());
        Assert.Equal(incoming, available.GetProperty("tile").GetInt32());
        AssertInventory(state);
        output.WriteLine($"Actual claim window game={table.GameId} hand={state.HandNumber} version={state.StateVersion} source=0 tile={incoming} deadline=0");
        return available;
    }

    private static void ArrangeSafeRotation(ChangshaGameState state)
    {
        Assert.Equal(1, state.DealerSeatIndex);
        Assert.Equal(1, state.ActiveSeatIndex);
        var used = new HashSet<int> { 104, 105, 106, 107 };
        var desired = new Dictionary<int, int[]>();
        for (var seat = 0; seat < 4; seat++)
        {
            var hand = new List<int>();
            for (var logical = 0; logical < 13; logical++)
            {
                var tile = Enumerable.Range(logical * 4, 4).First(tile => !used.Contains(tile));
                used.Add(tile);
                hand.Add(tile);
            }
            if (seat == 1) hand.Add(104);
            desired[seat] = hand.ToArray();
        }
        PlaceHands(state, desired);
        for (var index = 0; index < 3; index++) SwapInto(state, state.Wall, index, 105 + index);
        AssertInventory(state);
    }

    private static void PlaceHands(ChangshaGameState state, IReadOnlyDictionary<int, int[]> desired)
    {
        foreach (var (seat, tiles) in desired)
        {
            var hand = state.Hands[seat].ConcealedTiles;
            Assert.Equal(tiles.Length, hand.Count);
            Assert.Empty(state.Hands[seat].Melds);
            for (var index = 0; index < tiles.Length; index++) SwapInto(state, hand, index, tiles[index]);
        }
        AssertInventory(state);
    }

    private static void SwapInto(ChangshaGameState state, List<int> target, int index, int tile)
    {
        var source = state.Hands.Select(hand => hand.ConcealedTiles).Append(state.Wall)
            .Single(tiles => tiles.Contains(tile));
        var sourceIndex = source.IndexOf(tile);
        (source[sourceIndex], target[index]) = (target[index], source[sourceIndex]);
    }

    private static async Task RejectWithoutMutationAsync(HudsonOwnTurnWsFixture host, ContextTable table,
        WsPeer peer, Dictionary<string, object?> packet, string reason)
    {
        var before = await SnapshotAsync(host, table);
        var mark = peer.Frames.Count;
        await SendAsync(peer, packet);
        await peer.BarrierAsync();
        var frames = peer.Frames.Skip(mark).ToArray();
        var envelope = Assert.Single(frames, frame => Entries(frame)
            .Any(entry => entry[0].GetString() == "actionRejected"));
        Assert.False(envelope.GetProperty("full").GetBoolean());
        var rejected = Assert.Single(Entries(envelope), entry => entry[0].GetString() == "actionRejected");
        Assert.Equal("current", rejected[1].GetString());
        Assert.Equal("claim", rejected[2].GetProperty("action").GetString());
        Assert.Equal(reason, rejected[2].GetProperty("reason").GetString());
        Assert.Equal(1, rejected[2].GetProperty("requestedSeat").GetInt32());
        Assert.Equal(1, rejected[2].GetProperty("ownedSeat").GetInt32());
        AssertUnchanged(before, await SnapshotAsync(host, table));
        var corrected = Claim(await peer.BarrierAsync());
        Assert.Equal(before.StateVersion, corrected.GetProperty("stateVersion").GetInt32());
        Assert.Equal(table.GameId, corrected.GetProperty("gameId").GetString());
    }

    private static void AssertChow(ChangshaGameState before, ChangshaGameState after, int[] partners)
    {
        Assert.Equal(ChangshaPhase.AwaitingDiscard, after.Phase);
        Assert.Equal(1, after.ActiveSeatIndex);
        Assert.Null(after.ClaimWindow);
        Assert.Null(after.LastDrawSeatIndex);
        Assert.True(after.StateVersion > before.StateVersion);
        var meld = Assert.Single(after.Hands[1].Melds);
        Assert.Equal(MeldKind.Chow, meld.Kind);
        Assert.Equal(0, meld.ClaimedFromSeatIndex);
        Assert.Equal(partners.Append(before.ClaimWindow!.DiscardTileId).OrderBy(tile => tile),
            meld.TileIds.OrderBy(tile => tile));
        Assert.Equal(11, after.Hands[1].ConcealedTiles.Count);
        Assert.Equal(before.Hands[1].ConcealedTiles.Except(partners).OrderBy(tile => tile),
            after.Hands[1].ConcealedTiles.OrderBy(tile => tile));
        Assert.Equal(before.Wall, after.Wall);
        Assert.Equal(before.WallBackDrawn, after.WallBackDrawn);
        Assert.Equal(before.CumulativeScores.OrderBy(pair => pair.Key), after.CumulativeScores.OrderBy(pair => pair.Key));
        Assert.Null(after.CurrentWin);
        Assert.Null(after.CurrentScore);
        Assert.DoesNotContain(after.EventLog.Skip(before.EventLog.Count), entry => entry.EventType == "tile-drawn");
        AssertInventory(after);
    }

    private static Dictionary<string, object?> ClaimPacket(JsonElement available, string type, int[]? tiles = null)
    {
        var packet = new Dictionary<string, object?>
        {
            ["action"] = "claim",
            ["type"] = type,
            ["gameId"] = available.GetProperty("gameId").GetString(),
            ["expectedVersion"] = available.GetProperty("stateVersion").GetInt32()
        };
        if (tiles is not null) packet["tileIds"] = tiles;
        return packet;
    }

    private static Task SendAsync(WsPeer peer, Dictionary<string, object?> packet) =>
        peer.UpdateAsync([new object[] { "claim", 1.ToString(CultureInfo.InvariantCulture), packet }]);

    private static JsonElement Claim(JsonElement snapshot)
    {
        var entry = Assert.Single(Entries(snapshot),
            entry => entry[0].GetString() == "claim" && entry[1].ToString() == "1");
        Assert.Equal(JsonValueKind.String, entry[1].ValueKind);
        Assert.Equal(JsonValueKind.Object, entry[2].ValueKind);
        return entry[2].Clone();
    }

    private static string LegacyDescriptor(JsonElement available) => JsonSerializer.Serialize(new
    {
        source = available.GetProperty("source").GetInt32(),
        tile = available.GetProperty("tile").GetInt32(),
        deadline = available.GetProperty("deadline").GetInt64(),
        available = available.GetProperty("available").EnumerateArray().Select(value => value.GetString()).ToArray(),
        chowOptions = available.GetProperty("chowOptions").EnumerateArray()
            .Select(pair => pair.EnumerateArray().Select(tile => tile.GetInt32()).ToArray()).ToArray()
    });

    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static ChangshaGameState Live(HudsonOwnTurnWsFixture host, ContextTable table)
    {
        Assert.True(host.Runtime.TryGetSnapshot(table.GameId, out var state));
        Assert.NotNull(state);
        return state;
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, ContextTable table) =>
        await host.Runtime.TryGetSnapshotCopyAsync(table.GameId)
        ?? throw new InvalidOperationException("Expected live runtime snapshot.");

    private static void AssertUnchanged(ChangshaGameState before, ChangshaGameState after)
    {
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        AssertInventory(after);
    }

    private async Task<JsonElement> OpenContestedWindowAsync(HudsonOwnTurnWsFixture host, ContextTable table)
    {
        var state = Live(host, table);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Null(state.ClaimWindow);
        var desired = new Dictionary<int, int[]>
        {
            [1] = [8, 12, 20, 24, 40, 41, 42, 44, 45, 46, 72, 76, 88],
            [2] = [0, 4, 9, 13, 21, 25, 28, 32, 73, 74, 75, 89, 90]
        };
        var used = desired.Values.SelectMany(tiles => tiles).Append(19).ToHashSet();
        Assert.Equal(27, used.Count);
        int[] fillers = [0, 1, 2, 3, 5, 6, 7, 8, 9, 14, 15, 16, 17];
        foreach (var seat in new[] { 0, 3 })
        {
            var hand = new List<int>();
            foreach (var logical in fillers)
            {
                var tile = Enumerable.Range(logical * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                hand.Add(tile);
            }
            if (seat == 0) hand.Add(19);
            desired[seat] = hand.ToArray();
        }
        PlaceHands(state, desired);
        await table.Peers[0].UpdateAsync([new object[] { "discard", 0, new { tileId = 19 } }]);
        await table.Peers[0].BarrierAsync();
        state = Live(host, table);
        Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
        Assert.NotNull(state.ClaimWindow);
        Assert.Equal(new[] { 1, 2 }, state.ClaimWindow.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct().OrderBy(seat => seat));
        Assert.Contains(state.ClaimWindow.Opportunities,
            opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Chow);
        Assert.Contains(state.ClaimWindow.Opportunities,
            opportunity => opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Hu);
        state.ClaimWindow.OpenedAtUnixMs = 0;
        var available = Claim(await table.Claimant.BarrierAsync());
        Assert.Equal(table.GameId, available.GetProperty("gameId").GetString());
        Assert.Equal(state.StateVersion, available.GetProperty("stateVersion").GetInt32());
        Assert.Equal(0, available.GetProperty("deadline").GetInt64());
        CompetingHuClaim(await table.Peers[2].BarrierAsync());
        AssertInventory(state);
        return available;
    }

    private static void ArrangeRotationFromCurrentDealer(ChangshaGameState state)
    {
        var dealer = state.DealerSeatIndex;
        Assert.InRange(dealer, 1, 3);
        Assert.Equal(dealer, state.ActiveSeatIndex);
        var used = new HashSet<int> { 104, 105, 106, 107 };
        var desired = new Dictionary<int, int[]>();
        for (var seat = 0; seat < 4; seat++)
        {
            var hand = new List<int>();
            for (var logical = 0; logical < 13; logical++)
            {
                var tile = Enumerable.Range(logical * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                hand.Add(tile);
            }
            if (seat == dealer) hand.Add(104);
            desired[seat] = hand.ToArray();
        }
        PlaceHands(state, desired);
        for (var index = 0; index < 4 - dealer; index++)
            SwapInto(state, state.Wall, index, 105 + index);
        AssertInventory(state);
    }

    private static JsonElement CompetingHuClaim(JsonElement snapshot)
    {
        var entry = Assert.Single(Entries(snapshot),
            entry => entry[0].GetString() == "claim" && entry[1].ToString() == "2");
        Assert.Equal(JsonValueKind.String, entry[1].ValueKind);
        Assert.Equal(JsonValueKind.Object, entry[2].ValueKind);
        Assert.Contains("Hu", entry[2].GetProperty("available").EnumerateArray().Select(value => value.GetString()));
        return entry[2].Clone();
    }

    private static async Task PassCompetingHuAsync(ContextTable table)
    {
        var context = CompetingHuClaim(await table.Peers[2].BarrierAsync());
        var packet = ClaimPacket(context, "Hu");
        packet["action"] = "pass";
        packet["type"] = null;
        await table.Peers[2].UpdateAsync([new object[] { "claim", "2", packet }]);
        await table.Peers[2].BarrierAsync();
    }

    private static ChangshaGameInstance RuntimeInstance(HudsonOwnTurnWsFixture host, ContextTable table)
    {
        var field = host.Runtime.GetType().GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var games = Assert.IsType<ConcurrentDictionary<string, ChangshaGameInstance>>(field.GetValue(host.Runtime));
        Assert.True(games.TryGetValue(table.GameId, out var instance));
        Assert.NotNull(instance);
        return instance;
    }

    private static async Task<(ChangshaClaimWindow Window, CancellationTokenSource Timer,
        string PendingJson, int PendingCount)> ReadWindowAsync(ChangshaGameInstance instance)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await instance.Lock.WaitAsync(timeout.Token);
        try
        {
            var window = instance.State.ClaimWindow;
            var timer = instance.ClaimWindowCts;
            Assert.NotNull(window);
            Assert.NotNull(timer);
            Assert.False(timer.IsCancellationRequested);
            return (window, timer, JsonSerializer.Serialize(instance.PendingClaims), instance.PendingClaims.Count);
        }
        finally
        {
            instance.Lock.Release();
        }
    }

    private sealed record ContextTable(string RoomId, string GameId, WsPeer[] Peers) : IAsyncDisposable
    {
        public WsPeer Claimant => Peers[1];
        public async ValueTask DisposeAsync()
        {
            foreach (var peer in Peers) await peer.DisposeAsync();
        }
    }
}
