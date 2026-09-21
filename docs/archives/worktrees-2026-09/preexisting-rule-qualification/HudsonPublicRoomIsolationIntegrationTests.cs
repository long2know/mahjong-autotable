using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonPublicRoomIsolationIntegrationTests(ITestOutputHelper output)
{
    [Fact, Trait("Category", "RulesQualificationRecovery")]
    public async Task FirstOutsiderCannotTakeRecoveredHumanSeat_AndDefaultFillPreservesOtherOwners()
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        await using var room = await CreateRoomAsync(host, $"hudson-reserved-{Guid.NewGuid():N}");
        var before = await DatabaseAsync(host);
        await room.DisposeAsync();
        await host.RestartAsync();
        AssertDatabaseEqual(before, await DatabaseAsync(host));
        AssertCoreEqual(room.State, await SnapshotAsync(host, room.GameId));

        var outsiderIdentity = Identity(host);
        Assert.DoesNotContain(room.Players, player => player.PlayerId == outsiderIdentity.PlayerId);
        await using var outsider = await JoinAsync(host, room.Alias, outsiderIdentity, "changsha");
        AssertPrivateProjection(outsider.FirstSnapshot, room.State, null);
        await TakeSeatAsync(outsider.Peer, 0);
        Assert.Null(host.Runtime.TryGetSeatForPlayer(room.GameId, outsiderIdentity.PlayerId));
        AssertCoreEqual(room.State, await SnapshotAsync(host, room.GameId));
        AssertPrivateProjection(await outsider.Peer.BarrierAsync(), room.State, null);
        AssertDatabaseEqual(before, await DatabaseAsync(host));
        output.WriteLine($"First post-restart connection was outsider={outsiderIdentity.PlayerId}; seat0 rejected before any original-owner reassertion; runtime={room.GameId}, gameRows=1, bindingRows=1.");

        await using var owner = await JoinAsync(host, room.Alias, room.Players[0], "changsha",
            useDefaultBotFill: true, conflictingConfiguration: true);
        await TakeSeatAsync(owner.Peer, 0);
        var owned = await SnapshotAsync(host, room.GameId);
        AssertCoreEqual(room.State, owned);
        AssertPrivateProjection(await owner.Peer.BarrierAsync(), owned, 0);
        Assert.Equal("hard", owned.BotDifficulty);
        Assert.All(owned.Seats, seat => Assert.False(seat.IsBot));
        Assert.Equal(room.Players.Select(player => player.PlayerId),
            owned.Seats.OrderBy(seat => seat.SeatIndex).Select(seat => seat.PlayerId));
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(room.GameId, room.Players[0].PlayerId));
        AssertDatabaseEqual(before, await DatabaseAsync(host));

        await TakeSeatAsync(outsider.Peer, 1);
        Assert.Null(host.Runtime.TryGetSeatForPlayer(room.GameId, outsiderIdentity.PlayerId));
        AssertPrivateProjection(await outsider.Peer.BarrierAsync(), room.State, null);
        AssertCoreEqual(room.State, await SnapshotAsync(host, room.GameId));

        await using var nextOwner = await JoinAsync(host, room.Alias, room.Players[1], "changsha",
            useDefaultBotFill: true, conflictingConfiguration: true);
        await TakeSeatAsync(nextOwner.Peer, 1);
        var resumed = await SnapshotAsync(host, room.GameId);
        AssertCoreEqual(room.State, resumed);
        AssertPrivateProjection(await nextOwner.Peer.BarrierAsync(), resumed, 1);
        Assert.Equal(14, resumed.Hands[1].ConcealedTiles.Count);
        Assert.All(resumed.Seats, seat => Assert.False(seat.IsBot));
        AssertDatabaseEqual(before, await DatabaseAsync(host));
        await AssertPersistedAsync(host, resumed);
        output.WriteLine($"Original owners0/1 reasserted with default bot-fill and conflicting easy/manual/seed42/cap1/unit11 query; four saved humans, hard/auto/seed20261652/cap4/unit7, exact state and both row sets retained; no extra actor1 draw.");
    }

    [Theory, Trait("Category", "RulesQualificationRecovery")]
    [InlineData("four_player")]
    [InlineData("three_player")]
    [InlineData("bamboo")]
    [InlineData("minefield")]
    public async Task SameAliasRelayAndChangsha_KeepStoresOriginEchoAndBroadcastsIndependent(string variant)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        await using var room = await CreateRoomAsync(host, $"hudson-same-mode-{Guid.NewGuid():N}");
        var before = await DatabaseAsync(host);
        await room.DisposeAsync();
        await host.RestartAsync();
        await using var changshaAlice = await JoinAsync(host, room.Alias, room.Players[0], "changsha");
        await using var changshaBob = await JoinAsync(host, room.Alias, room.Players[1], "changsha");
        await TakeSeatAsync(changshaAlice.Peer, 0);
        await TakeSeatAsync(changshaBob.Peer, 1);
        AssertCoreEqual(room.State, await SnapshotAsync(host, room.GameId));
        AssertPrivateProjection(await changshaAlice.Peer.BarrierAsync(), room.State, 0);
        AssertPrivateProjection(await changshaBob.Peer.BarrierAsync(), room.State, 1);

        await using var relayAlice = await JoinAsync(host, room.Alias, Identity(host), variant);
        await using var relayBob = await JoinAsync(host, room.Alias, Identity(host), variant);
        await using var relayReference = await JoinAsync(host,
            $"hudson-relay-reference-{Guid.NewGuid():N}", Identity(host), variant);
        await using var relayReferencePeer = await JoinAsync(host,
            relayReference.Peer.RoomId, Identity(host), variant);
        AssertNoAuthoritativeControls(relayReference.FirstSnapshot);
        AssertNoAuthoritativeControls(relayReferencePeer.FirstSnapshot);
        AssertNoAuthoritativeControls(relayAlice.FirstSnapshot);
        AssertNoAuthoritativeControls(relayBob.FirstSnapshot);
        Assert.Equal(EntryTexts(relayReference.FirstSnapshot), EntryTexts(relayAlice.FirstSnapshot));
        Assert.Equal(EntryTexts(relayReferencePeer.FirstSnapshot), EntryTexts(relayBob.FirstSnapshot));
        Assert.DoesNotContain(Entries(relayAlice.FirstSnapshot), entry => entry[0].GetString() == "things");
        Assert.DoesNotContain(Entries(relayBob.FirstSnapshot), entry => entry[0].GetString() == "things");

        var relayMarker = $"relay-only-{Guid.NewGuid():N}";
        var relaySetup = new object[]
        {
            new object[] { "unique", "seats", "seat" },
            new object[] { "perPlayer", "seats", true },
            new object[] { "seats", relayAlice.Peer.PlayerId, new { seat = 0 } },
            new object[] { "match", 0, new
            {
                conditions = new { gameType = variant.ToUpperInvariant() },
                dealer = 0, honba = 0
            } },
            new object[] { "things", 7, new { slotName = "hand.0@0", rotationIndex = 1, marker = relayMarker } },
            new object[] { "nicks", relayAlice.Peer.PlayerId, relayMarker }
        };
        var originMark = relayAlice.Peer.Frames.Count;
        var relayPeerMark = relayBob.Peer.Frames.Count;
        var changshaAliceMark = changshaAlice.Peer.Frames.Count;
        var changshaBobMark = changshaBob.Peer.Frames.Count;
        await relayAlice.Peer.UpdateAsync(relaySetup);
        var relayOriginSnapshot = await relayAlice.Peer.BarrierAsync();
        var relayPeerSnapshot = await relayBob.Peer.BarrierAsync();
        AssertSetupDelta(relayAlice.Peer.Frames.Skip(originMark), relaySetup);
        var peerSetup = relaySetup.ToArray();
        peerSetup[4] = new object[] { "things", 7, new
        {
            slotName = "hand.0@0", rotationIndex = 2, marker = relayMarker, face = (string?)null
        } };
        AssertSetupDelta(relayBob.Peer.Frames.Skip(relayPeerMark), peerSetup);
        AssertRelayStoredMarker(relayOriginSnapshot, relayMarker);
        AssertRelayStoredMarker(relayPeerSnapshot, relayMarker);

        var changshaAliceSnapshot = await changshaAlice.Peer.BarrierAsync();
        var changshaBobSnapshot = await changshaBob.Peer.BarrierAsync();
        AssertNoMarker(changshaAlice.Peer.Frames.Skip(changshaAliceMark), relayMarker);
        AssertNoMarker(changshaBob.Peer.Frames.Skip(changshaBobMark), relayMarker);
        AssertPrivateProjection(changshaAliceSnapshot, room.State, 0);
        AssertPrivateProjection(changshaBobSnapshot, room.State, 1);
        AssertCoreEqual(room.State, await SnapshotAsync(host, room.GameId));
        AssertDatabaseEqual(before, await DatabaseAsync(host));

        var changshaMarker = $"changsha-only-{Guid.NewGuid():N}";
        var reverseOriginMark = relayAlice.Peer.Frames.Count;
        var reversePeerMark = relayBob.Peer.Frames.Count;
        var changshaPeerMark = changshaBob.Peer.Frames.Count;
        await changshaAlice.Peer.UpdateAsync([new object[]
        {
            "mouse", changshaAlice.Peer.PlayerId, new { x = 3, y = 5, marker = changshaMarker }
        }]);
        await changshaAlice.Peer.BarrierAsync();
        await changshaBob.Peer.BarrierAsync();
        Assert.Contains(changshaBob.Peer.Frames.Skip(changshaPeerMark).Where(IsDelta).SelectMany(Entries),
            entry => entry[0].GetString() == "mouse"
                && entry[2].GetRawText().Contains(changshaMarker, StringComparison.Ordinal));

        var discarded = room.State.Hands[1].ConcealedTiles[0];
        await changshaBob.Peer.UpdateAsync([new object[] { "discard", 1, new { tileId = discarded } }]);
        await changshaBob.Peer.BarrierAsync();
        await changshaAlice.Peer.BarrierAsync();
        var progressed = await SnapshotAsync(host, room.GameId);
        Assert.True(progressed.StateVersion > room.State.StateVersion);
        Assert.Contains(progressed.EventLog.Skip(room.State.EventLog.Count),
            entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 1 && entry.TileId == discarded);
        AssertInventory(progressed);

        AssertRelayStoredMarker(await relayAlice.Peer.BarrierAsync(), relayMarker);
        AssertRelayStoredMarker(await relayBob.Peer.BarrierAsync(), relayMarker);
        AssertNoMarker(relayAlice.Peer.Frames.Skip(reverseOriginMark), changshaMarker);
        AssertNoMarker(relayBob.Peer.Frames.Skip(reversePeerMark), changshaMarker);
        Assert.All(relayAlice.Peer.Frames.Skip(reverseOriginMark), AssertNoAuthoritativeState);
        Assert.All(relayBob.Peer.Frames.Skip(reversePeerMark), AssertNoAuthoritativeState);
        Assert.Equal(room.GameId, host.Manager.GetRuntimeGameIdBoundTo(room.Alias));
        Assert.Equal(1, host.Runtime.GameCount);
        AssertDatabaseEqual(before, await DatabaseAsync(host));
        await AssertPersistedAsync(host, progressed);
        output.WriteLine($"Same alias={room.Alias}, Relay={variant}, Changsha runtime={room.GameId}: exact relay origin/peer setup and two-direction marker isolation; real Changsha discard={discarded}/version={progressed.StateVersion} did not enter relay frames/store; both row sets unchanged.");
    }

    [Fact, Trait("Category", "RulesQualificationRecovery")]
    public async Task CaseDistinctAliases_WithSameSeedAndOwners_RetainIndependentOrdinalBindings()
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        var token = Guid.NewGuid().ToString("N");
        var lower = $"hudson-case-{token}";
        var upper = $"HUDSON-CASE-{token}";
        Assert.NotEqual(lower, upper);
        Assert.True(string.Equals(lower, upper, StringComparison.OrdinalIgnoreCase));
        var players = Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        await using var first = await CreateRoomAsync(host, lower, players);
        await using var second = await CreateRoomAsync(host, upper, players);
        Assert.NotEqual(first.GameId, second.GameId);
        Assert.Equal(first.State.Seed, second.State.Seed);
        var before = await DatabaseAsync(host);
        Assert.Equal(2, before.GameIds.Length);
        Assert.Equal(2, before.Bindings.Length);
        var firstBinding = Assert.Single(before.Bindings, binding => binding.RoomId == lower);
        var secondBinding = Assert.Single(before.Bindings, binding => binding.RoomId == upper);
        Assert.Equal(RoomKey(lower), firstBinding.RoomKey);
        Assert.Equal(RoomKey(upper), secondBinding.RoomKey);
        Assert.NotEqual(firstBinding.RoomKey, secondBinding.RoomKey);
        Assert.Equal(Guid.Parse(first.GameId), firstBinding.RuntimeGameId);
        Assert.Equal(Guid.Parse(second.GameId), secondBinding.RuntimeGameId);
        await first.DisposeAsync();
        await second.DisposeAsync();
        await host.RestartAsync();
        AssertDatabaseEqual(before, await DatabaseAsync(host));

        await using var firstOwner = await JoinAsync(host, lower, players[1], "changsha");
        await using var secondOwner = await JoinAsync(host, upper, players[1], "changsha");
        await TakeSeatAsync(firstOwner.Peer, 1);
        await TakeSeatAsync(secondOwner.Peer, 1);
        Assert.Equal(first.GameId, host.Manager.GetRuntimeGameIdBoundTo(lower));
        Assert.Equal(second.GameId, host.Manager.GetRuntimeGameIdBoundTo(upper));
        AssertCoreEqual(first.State, await SnapshotAsync(host, first.GameId));
        AssertCoreEqual(second.State, await SnapshotAsync(host, second.GameId));
        AssertPrivateProjection(await firstOwner.Peer.BarrierAsync(), first.State, 1);
        AssertPrivateProjection(await secondOwner.Peer.BarrierAsync(), second.State, 1);

        var tile = first.State.Hands[1].ConcealedTiles[0];
        await firstOwner.Peer.UpdateAsync([new object[] { "discard", 1, new { tileId = tile } }]);
        await firstOwner.Peer.BarrierAsync();
        await secondOwner.Peer.BarrierAsync();
        var progressed = await SnapshotAsync(host, first.GameId);
        Assert.True(progressed.StateVersion > first.State.StateVersion);
        AssertCoreEqual(second.State, await SnapshotAsync(host, second.GameId));
        AssertDatabaseEqual(before, await DatabaseAsync(host));
        await AssertPersistedAsync(host, progressed);
        await AssertPersistedAsync(host, second.State);
        output.WriteLine($"Case-distinct aliases={lower}/{upper}, identical seed and saved owners, keys={firstBinding.RoomKey}/{secondBinding.RoomKey}; runtimes={first.GameId}/{second.GameId}, rows2/bindings2 unchanged; only lower alias advanced.");
    }

    private async Task<Room> CreateRoomAsync(
        HudsonOwnTurnWsFixture host, string alias, SignedPlayer[]? existingPlayers = null)
    {
        var players = existingPlayers ?? Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        var connections = new List<Connection>();
        try
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var connection = await JoinAsync(host, alias, players[seat], "changsha");
                connections.Add(connection);
                await TakeSeatAsync(connection.Peer, seat);
            }
            var game = host.Manager.GetRuntimeGameIdBoundTo(alias);
            Assert.NotNull(game);
            var initial = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, initial.Phase);
            Assert.Equal(0, initial.ActiveSeatIndex);
            Assert.Equal(20261652, initial.Seed);
            Assert.Equal(7, initial.BaseUnit);
            Assert.Equal(4, initial.MaxHands);
            Assert.Equal(DealMode.Auto, initial.DealMode);
            Assert.Equal("hard", initial.BotDifficulty);
            Assert.All(initial.Seats, seat => Assert.False(seat.IsBot));
            AssertInventory(initial);
            var tile = initial.Hands[0].ConcealedTiles[0];
            await connections[0].Peer.UpdateAsync([new object[] { "discard", 0, new { tileId = tile } }]);
            await connections[0].Peer.BarrierAsync();
            var state = await SnapshotAsync(host, game);
            if (state.ClaimWindow is { } window)
            {
                foreach (var seat in window.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct())
                {
                    var current = await SnapshotAsync(host, game);
                    Assert.NotNull(current.ClaimWindow);
                    await connections[seat].Peer.UpdateAsync([new object[] { "claim", seat, new
                    {
                        gameId = game, expectedVersion = current.StateVersion, action = "pass"
                    } }]);
                    await connections[seat].Peer.BarrierAsync();
                }
            }
            foreach (var connection in connections) await connection.Peer.BarrierAsync();
            state = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
            Assert.Null(state.ClaimWindow);
            Assert.Equal(1, state.ActiveSeatIndex);
            Assert.Equal(1, state.LastDrawSeatIndex);
            Assert.Equal(14, state.Hands[1].ConcealedTiles.Count);
            Assert.Equal(initial.Wall.Skip(1), state.Wall);
            Assert.True(state.StateVersion > initial.StateVersion);
            AssertInventory(state);
            await AssertPersistedAsync(host, state);
            output.WriteLine($"Created actual public room={alias}, runtime={game}, real discard={tile}, turn={state.TurnNumber}/v{state.StateVersion}/wall{state.Wall.Count}, hard/auto/seed20261652/cap4/unit7; all108 conserved.");
            return new Room(alias, game, players, connections, state);
        }
        catch
        {
            foreach (var connection in connections) await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<Connection> JoinAsync(HudsonOwnTurnWsFixture host, string alias,
        SignedPlayer player, string variant, bool useDefaultBotFill = false, bool conflictingConfiguration = false)
    {
        var server = Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());
        var client = server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={player.Credential}";
        var query = $"autotable/ws?variant={Uri.EscapeDataString(variant)}&gameId={Uri.EscapeDataString(alias)}";
        if (!useDefaultBotFill) query += "&bots=false&botCount=0";
        query += conflictingConfiguration
            ? "&dealMode=manual&handCount=1&seed=42&baseUnit=11&botDifficulty=easy"
            : "&dealMode=auto&handCount=4&seed=20261652&baseUnit=7&botDifficulty=hard";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var peer = new WsPeer(await client.ConnectAsync(new Uri(server.BaseAddress, query), timeout.Token),
            alias, player.PlayerId, output);
        try
        {
            var first = await peer.BarrierAsync();
            await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "ownTurn", "gameComplete" }
                .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
            await peer.BarrierAsync();
            output.WriteLine($"Joined alias={alias}, variant={variant}, player={player.PlayerId}, defaultBotFill={useDefaultBotFill}, conflictingConfig={conflictingConfiguration}; signed credential retained.");
            return new Connection(peer, first);
        }
        catch
        {
            await peer.DisposeAsync();
            throw;
        }
    }

    private static SignedPlayer Identity(HudsonOwnTurnWsFixture host)
    {
        var player = $"hudson-isolation-{Guid.NewGuid():N}";
        return new SignedPlayer(player, host.Services.GetRequiredService<PlayerIdentityService>().Protect(player));
    }

    private static string RoomKey(string alias) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(alias))).ToLowerInvariant();

    private static async Task TakeSeatAsync(WsPeer peer, int seat)
    {
        await peer.UpdateAsync([new object[] { "seats", peer.PlayerId, new { seat } }]);
        await peer.BarrierAsync();
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, string game) =>
        await host.Runtime.TryGetSnapshotCopyAsync(game)
        ?? throw new InvalidOperationException($"Expected actual public runtime {game}.");

    private static async Task<DatabaseRows> DatabaseAsync(HudsonOwnTurnWsFixture host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var games = await db.ChangshaGames.AsNoTracking().Select(game => game.Id).ToArrayAsync();
        var bindings = await db.AutotableRoomBindings.AsNoTracking().ToArrayAsync();
        return new DatabaseRows(games.OrderBy(id => id).ToArray(),
            bindings.Select(binding => new Binding(binding.RoomKey, binding.RoomId, binding.RuntimeGameId, binding.CreatedUtc))
                .OrderBy(binding => binding.RoomKey, StringComparer.Ordinal).ToArray());
    }

    private static void AssertDatabaseEqual(DatabaseRows expected, DatabaseRows actual)
    {
        Assert.Equal(expected.GameIds, actual.GameIds);
        Assert.Equal(expected.Bindings, actual.Bindings);
    }

    private static async Task AssertPersistedAsync(HudsonOwnTurnWsFixture host, ChangshaGameState expected)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(expected.GameId);
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == id);
        var state = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(state);
        Assert.Equal(expected.StateVersion, row.StateVersion);
        AssertCoreEqual(expected, state);
    }

    private static void AssertCoreEqual(ChangshaGameState expected, ChangshaGameState actual)
    {
        Assert.Equal(Core(expected), Core(actual));
        AssertInventory(actual);
    }

    private static string Core(ChangshaGameState state) => JsonSerializer.Serialize(new
    {
        state.GameId, state.Seed, state.BaseUnit, state.MaxHands, state.DealMode, state.BotDifficulty,
        state.Phase, state.HandNumber, state.RoundNumber, state.RoundWind, state.DealerSeatIndex,
        state.ActiveSeatIndex, state.TurnNumber, state.StateVersion, state.EventSequence, state.EventLog,
        state.DiscardsThisHand, state.LastDrawSeatIndex, state.LastDrawWasKongReplacement,
        state.Wall, state.WallDrawIndex, state.WallBackIndex, state.WallBackDrawn, state.BreakPoint,
        state.Hands, state.DiscardPile, state.ClaimWindow, state.CurrentWin, state.CurrentScore,
        state.CumulativeScores, state.IsGameComplete, state.MissedWinSeats, state.FalseHuPenalties,
        seats = state.Seats.Select(seat => new { seat.SeatIndex, seat.PlayerId, seat.IsBot, seat.IsDealer })
    });

    private static void AssertPrivateProjection(JsonElement snapshot, ChangshaGameState state, int? owner)
    {
        foreach (var hand in state.Hands)
        {
            var tiles = Entries(snapshot).Where(entry => entry[0].GetString() == "things"
                && entry[2].ValueKind == JsonValueKind.Object
                && entry[2].TryGetProperty("slotName", out var slot)
                && slot.ValueKind == JsonValueKind.String
                && slot.GetString()!.StartsWith("hand.", StringComparison.Ordinal)
                && slot.GetString()!.EndsWith($"@{hand.SeatIndex}", StringComparison.Ordinal)).ToArray();
            Assert.Equal(hand.ConcealedTiles.Count, tiles.Length);
            if (hand.SeatIndex == owner)
            {
                Assert.Equal(hand.ConcealedTiles.OrderBy(tile => tile),
                    tiles.Select(entry => entry[1].GetInt32()).OrderBy(tile => tile));
                Assert.All(tiles, entry => Assert.Equal(1, entry[2].GetProperty("rotationIndex").GetInt32()));
            }
            else
            {
                Assert.All(tiles, entry =>
                {
                    Assert.Equal(JsonValueKind.String, entry[1].ValueKind);
                    Assert.StartsWith("h_", entry[1].GetString());
                    Assert.Equal(2, entry[2].GetProperty("rotationIndex").GetInt32());
                    Assert.False(entry[2].TryGetProperty("face", out var face) && face.ValueKind != JsonValueKind.Null);
                });
                Assert.DoesNotContain(Entries(snapshot), entry => entry[0].GetString() == "ownTurn"
                    && entry[1].ToString() == hand.SeatIndex.ToString()
                    && entry[2].ValueKind != JsonValueKind.Null);
            }
        }
    }

    private static void AssertSetupDelta(IEnumerable<JsonElement> frames, object[] setup)
    {
        var expected = JsonSerializer.Serialize(setup);
        Assert.Contains(frames.Where(IsDelta), frame => frame.GetProperty("entries").GetRawText() == expected);
    }

    private static void AssertRelayStoredMarker(JsonElement frame, string marker) =>
        Assert.Contains(Entries(frame), entry => entry[0].GetString() == "things"
            && entry[1].ToString() == "7"
            && entry[2].GetProperty("marker").GetString() == marker);

    private static void AssertNoMarker(IEnumerable<JsonElement> frames, string marker) =>
        Assert.DoesNotContain(frames, frame => frame.GetRawText().Contains(marker, StringComparison.Ordinal));

    private static void AssertNoAuthoritativeControls(JsonElement frame) =>
        Assert.DoesNotContain(Entries(frame), entry =>
            entry[0].GetString() is "ownTurn" or "claim" or "turn" or "pickup" or "discard" or "gameComplete" or "result"
                && entry[2].ValueKind != JsonValueKind.Null);

    private static void AssertNoAuthoritativeState(JsonElement frame)
    {
        AssertNoAuthoritativeControls(frame);
        Assert.DoesNotContain(Entries(frame), entry => entry[0].GetString() == "match"
            && entry[2].ValueKind == JsonValueKind.Object
            && entry[2].TryGetProperty("conditions", out var conditions)
            && conditions.ValueKind == JsonValueKind.Object
            && conditions.TryGetProperty("gameType", out var type)
            && string.Equals(type.GetString(), "CHANGSHA", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] EntryTexts(JsonElement frame) =>
        Entries(frame).Select(entry => entry.GetRawText()).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static bool IsDelta(JsonElement frame) => frame.GetProperty("type").GetString() == "UPDATE"
        && frame.TryGetProperty("full", out var full) && !full.GetBoolean();

    private sealed record SignedPlayer(string PlayerId, string Credential);
    private sealed record Binding(string RoomKey, string RoomId, Guid RuntimeGameId, DateTime CreatedUtc);
    private sealed record DatabaseRows(Guid[] GameIds, Binding[] Bindings);
    private sealed record Connection(WsPeer Peer, JsonElement FirstSnapshot) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Peer.DisposeAsync();
    }

    private sealed record Room(string Alias, string GameId, SignedPlayer[] Players,
        List<Connection> Connections, ChangshaGameState State) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var connection in Connections) await connection.DisposeAsync();
        }
    }
}
