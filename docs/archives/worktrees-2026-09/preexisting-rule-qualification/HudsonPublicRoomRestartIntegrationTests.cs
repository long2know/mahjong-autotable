using System.Globalization;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Players;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonPublicRoomRestartIntegrationTests(ITestOutputHelper output)
{
    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task PublicAliasRestart_ResumesProgressedRuntimeAndKeepsOwnershipRoomScoped()
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        var firstServer = Server(host);
        var firstRuntime = host.Runtime;
        var firstManager = host.Manager;
        var room = $"hudson-resume-{Guid.NewGuid():N}";
        var otherRoom = $"hudson-resume-other-{Guid.NewGuid():N}";
        var players = Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        var otherPlayers = Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        var outsider = Identity(host);
        var firstPeers = new List<WsPeer>();
        try
        {
            foreach (var (player, seat) in players.Select((player, seat) => (player, seat)))
            {
                var peer = await ConnectAsync(host, room, player);
                firstPeers.Add(peer);
                await TakeSeatAsync(peer, seat);
            }
            var game = host.Manager.GetRuntimeGameIdBoundTo(room)
                ?? throw new InvalidOperationException("Initial signed seats did not create a runtime binding.");
            var initial = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, initial.Phase);
            Assert.Equal(7, initial.BaseUnit);
            Assert.Equal(4, initial.MaxHands);
            Assert.All(initial.Seats, seat => Assert.False(seat.IsBot));
            AssertInventory(initial);

            var checkpoint = await PlayToCheckpointAsync(host, game, firstPeers.ToArray());
            Assert.True(checkpoint.HandNumber >= 2);
            Assert.Contains(checkpoint.CumulativeScores.Values, score => score != 0);
            Assert.Equal(0, checkpoint.CumulativeScores.Values.Sum());
            Assert.True(checkpoint.DiscardsThisHand > 0);
            Assert.True(checkpoint.Wall.Count < initial.Wall.Count);
            Assert.Equal(0, checkpoint.ActiveSeatIndex);
            Assert.Equal(0, checkpoint.LastDrawSeatIndex);
            Assert.NotEqual(JsonSerializer.Serialize(initial.Hands), JsonSerializer.Serialize(checkpoint.Hands));
            AssertInventory(checkpoint);

            foreach (var (player, seat) in otherPlayers.Select((player, seat) => (player, seat)))
            {
                var peer = await ConnectAsync(host, otherRoom, player);
                firstPeers.Add(peer);
                await TakeSeatAsync(peer, seat);
            }
            var otherGame = host.Manager.GetRuntimeGameIdBoundTo(otherRoom)
                ?? throw new InvalidOperationException("The foreign owner's control room did not bind.");
            var otherCheckpoint = await SnapshotAsync(host, otherGame);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, otherCheckpoint.Phase);
            Assert.Equal(0, otherCheckpoint.ActiveSeatIndex);
            AssertInventory(otherCheckpoint);
            var persistedIds = await DatabaseGameIdsAsync(host);
            Assert.Equal(new[] { game, otherGame }.OrderBy(id => id), persistedIds);
            await AssertPersistedAsync(host, checkpoint);
            await AssertPersistedAsync(host, otherCheckpoint);
            output.WriteLine($"Before restart room={room} runtime={game} hand={checkpoint.HandNumber} turn={checkpoint.TurnNumber} version={checkpoint.StateVersion} wall={checkpoint.Wall.Count} baseUnit={checkpoint.BaseUnit} scores={JsonSerializer.Serialize(checkpoint.CumulativeScores)} rows={JsonSerializer.Serialize(persistedIds)}");

            foreach (var peer in firstPeers) await peer.DisposeAsync();
            await host.RestartAsync();
            Assert.NotSame(firstServer, Server(host));
            Assert.NotSame(firstRuntime, host.Runtime);
            Assert.NotSame(firstManager, host.Manager);
            Assert.Equal(persistedIds, await DatabaseGameIdsAsync(host));
            AssertCoreEqual(checkpoint, await SnapshotAsync(host, game));
            AssertCoreEqual(otherCheckpoint, await SnapshotAsync(host, otherGame));

            // Reuse the host-one credential verbatim, rather than re-signing the ID after restart.
            await using var owner = await ConnectAsync(host, room, players[0], baseUnit: 11);
            await TakeSeatAsync(owner, 0);
            var rebound = host.Manager.GetRuntimeGameIdBoundTo(room);
            var rowsAfterReassertion = await DatabaseGameIdsAsync(host);
            output.WriteLine($"After restart sameCredential=true room={room} expectedRuntime={game} reboundRuntime={rebound} rows={JsonSerializer.Serialize(rowsAfterReassertion)}");
            Assert.Equal(game, rebound);
            Assert.Equal(persistedIds, rowsAfterReassertion);
            Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(game, players[0].PlayerId));
            AssertCoreEqual(checkpoint, await SnapshotAsync(host, game));
            AssertProjection(await owner.BarrierAsync(), checkpoint, 0);

            await using var observer = await ConnectAsync(host, room, outsider, seatHint: 0);
            AssertProjection(await observer.BarrierAsync(), checkpoint, null);
            await TakeSeatAsync(observer, 0);
            Assert.Null(host.Runtime.TryGetSeatForPlayer(game, outsider.PlayerId));
            AssertCoreEqual(checkpoint, await SnapshotAsync(host, game));
            AssertProjection(await observer.BarrierAsync(), checkpoint, null);

            await using var otherOwner = await ConnectAsync(host, otherRoom, otherPlayers[0], baseUnit: 13);
            await TakeSeatAsync(otherOwner, 0);
            Assert.Equal(otherGame, host.Manager.GetRuntimeGameIdBoundTo(otherRoom));
            Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(otherGame, otherPlayers[0].PlayerId));
            AssertCoreEqual(otherCheckpoint, await SnapshotAsync(host, otherGame));
            AssertProjection(await otherOwner.BarrierAsync(), otherCheckpoint, 0);

            var switchMark = owner.Frames.Count;
            AssertProjection(await owner.BarrierAsync(otherRoom), otherCheckpoint, null);
            var joinedIndex = owner.Frames.FindIndex(switchMark, frame =>
                frame.GetProperty("type").GetString() == "JOINED"
                && frame.GetProperty("gameId").GetString() == otherRoom);
            Assert.True(joinedIndex >= switchMark);
            Assert.Null(host.Runtime.TryGetSeatForPlayer(otherGame, players[0].PlayerId));
            var victimTile = otherCheckpoint.Hands[0].ConcealedTiles[0];
            await owner.UpdateAsync([new object[] { "discard", 0, new { tileId = victimTile } }]);
            await owner.BarrierAsync();
            var foreignFrames = owner.Frames.Skip(joinedIndex + 1).ToArray();
            Assert.All(foreignFrames.Where(IsFull), frame => AssertProjection(frame, otherCheckpoint, null));
            var rejection = Assert.Single(foreignFrames.SelectMany(Entries),
                entry => entry[0].GetString() == "actionRejected");
            Assert.Equal("discard", rejection[2].GetProperty("action").GetString());
            Assert.Equal("connection-owns-no-seat", rejection[2].GetProperty("reason").GetString());
            Assert.Equal(JsonValueKind.Null, rejection[2].GetProperty("ownedSeat").ValueKind);
            AssertCoreEqual(checkpoint, await SnapshotAsync(host, game));
            AssertCoreEqual(otherCheckpoint, await SnapshotAsync(host, otherGame));
            Assert.Equal(persistedIds, await DatabaseGameIdsAsync(host));

            AssertProjection(await owner.BarrierAsync(room), checkpoint, 0);
            await TakeSeatAsync(owner, 0);
            AssertCoreEqual(checkpoint, await SnapshotAsync(host, game));
            var resumedTile = checkpoint.Hands[0].ConcealedTiles[0];
            await owner.UpdateAsync([new object[] { "discard", 0, new { tileId = resumedTile } }]);
            await owner.BarrierAsync();
            var continued = await SnapshotAsync(host, game);
            Assert.True(continued.StateVersion > checkpoint.StateVersion);
            Assert.DoesNotContain(resumedTile, continued.Hands[0].ConcealedTiles);
            Assert.Contains(continued.EventLog.Skip(checkpoint.EventLog.Count),
                entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 0 && entry.TileId == resumedTile);
            Assert.Equal(checkpoint.BaseUnit, continued.BaseUnit);
            Assert.Equal(persistedIds, await DatabaseGameIdsAsync(host));
            AssertInventory(continued);
            AssertProjection(await observer.BarrierAsync(), continued, null);
            await AssertPersistedAsync(host, continued);
            output.WriteLine($"Recovered runtime={game}; ownerDiscard={resumedTile}; version={continued.StateVersion}; foreign/cross-room privacy and ownership intact; runtimeRows={persistedIds.Length}");
        }
        finally
        {
            foreach (var peer in firstPeers) await peer.DisposeAsync();
        }
    }

    private async Task<ChangshaGameState> PlayToCheckpointAsync(
        HudsonOwnTurnWsFixture host, string game, WsPeer[] peers)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var actions = 0;
        while (actions < 400)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var state = await SnapshotAsync(host, game);
            if (state.HandNumber >= 2 && state.CumulativeScores.Values.Any(score => score != 0)
                && state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == 0
                && state.LastDrawSeatIndex == 0 && state.DiscardsThisHand > 0)
            {
                output.WriteLine($"Normal WS progress actions={actions}; hand={state.HandNumber}; turn={state.TurnNumber}; version={state.StateVersion}; nonzeroScores=true");
                return state;
            }
            Assert.False(state.IsGameComplete, "Fixed-seed progress did not reach the required nonterminal scored checkpoint.");
            var frameMarks = peers.Select(peer => peer.Frames.Count).ToArray();
            if (state.Phase == ChangshaPhase.AwaitingDiscard)
            {
                var seat = state.ActiveSeatIndex;
                var wire = await peers[seat].BarrierAsync();
                var own = Entry(wire, "ownTurn", seat);
                if (own.ValueKind == JsonValueKind.Object && own.GetProperty("hu").GetBoolean())
                {
                    await OwnTurnAsync(peers[seat], seat, own, "hu");
                }
                else if (own.ValueKind == JsonValueKind.Object
                    && own.GetProperty("concealedKongs").GetArrayLength() > 0)
                {
                    var tiles = own.GetProperty("concealedKongs")[0].EnumerateArray().Select(tile => tile.GetInt32()).ToArray();
                    await OwnTurnAsync(peers[seat], seat, own, "concealedKong", tiles);
                }
                else if (own.ValueKind == JsonValueKind.Object && own.GetProperty("addedKongs").GetArrayLength() > 0)
                {
                    await OwnTurnAsync(peers[seat], seat, own, "addedKong", [own.GetProperty("addedKongs")[0].GetInt32()]);
                }
                else
                {
                    var tile = MediumStrategy.SelectDiscardTile(state.Hands[seat]);
                    await peers[seat].UpdateAsync([new object[] { "discard", seat, new { tileId = tile } }]);
                }
                actions++;
            }
            else if (state.Phase == ChangshaPhase.AwaitingClaim)
            {
                var window = state.ClaimWindow!;
                var selected = window.Opportunities.OrderByDescending(opportunity => opportunity.Priority)
                    .ThenBy(opportunity => (opportunity.SeatIndex - window.DiscardSeatIndex + 4) % 4).First();
                foreach (var seat in window.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct()
                    .Where(seat => seat != selected.SeatIndex).Append(selected.SeatIndex))
                {
                    var wire = await peers[seat].BarrierAsync();
                    var available = Entry(wire, "claim", seat);
                    Assert.Equal(JsonValueKind.Object, available.ValueKind);
                    var current = await SnapshotAsync(host, game);
                    Assert.Equal(window.DiscardTileId, current.ClaimWindow!.DiscardTileId);
                    Assert.Equal(window.DiscardSeatIndex, current.ClaimWindow.DiscardSeatIndex);
                    var payload = new Dictionary<string, object?>
                    {
                        ["gameId"] = game,
                        ["expectedVersion"] = current.StateVersion,
                        ["action"] = seat == selected.SeatIndex ? "claim" : "pass",
                        ["type"] = seat == selected.SeatIndex ? selected.ClaimType.ToString() : null
                    };
                    if (seat == selected.SeatIndex && selected.ClaimType == TableClaimType.Chow)
                    {
                        var options = available.GetProperty("chowOptions");
                        Assert.True(options.GetArrayLength() > 0);
                        payload["tileIds"] = options[0].EnumerateArray().Select(tile => tile.GetInt32()).ToArray();
                    }
                    await peers[seat].UpdateAsync([new object[] { "claim", seat, payload }]);
                    await peers[seat].BarrierAsync();
                    actions++;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected stable phase during normal WS progress: {state.Phase}");
            }
            foreach (var peer in peers) await peer.BarrierAsync();
            Assert.All(peers.SelectMany((peer, index) => peer.Frames.Skip(frameMarks[index])).SelectMany(Entries),
                entry => Assert.NotEqual("actionRejected", entry[0].GetString()));
        }
        throw new InvalidOperationException("Fixed 400-action budget exhausted before a scored mid-hand checkpoint.");
    }

    private static Task OwnTurnAsync(WsPeer peer, int seat, JsonElement available, string action, int[]? tiles = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["gameId"] = available.GetProperty("gameId").GetString(),
            ["expectedVersion"] = available.GetProperty("stateVersion").GetInt32(),
            ["action"] = action
        };
        if (tiles is not null) payload["tileIds"] = tiles;
        return peer.UpdateAsync([new object[] { "ownTurn", seat, payload }]);
    }

    private static TestServer Server(HudsonOwnTurnWsFixture host) =>
        Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());

    private static SignedPlayer Identity(HudsonOwnTurnWsFixture host)
    {
        var playerId = $"hudson-resume-player-{Guid.NewGuid():N}";
        return new SignedPlayer(playerId, host.Services.GetRequiredService<PlayerIdentityService>().Protect(playerId));
    }

    private async Task<WsPeer> ConnectAsync(
        HudsonOwnTurnWsFixture host, string room, SignedPlayer player, int baseUnit = 7, int? seatHint = null)
    {
        var server = Server(host);
        var client = server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={player.Credential}";
        var query = $"autotable/ws?variant=changsha&gameId={Uri.EscapeDataString(room)}&bots=false&botCount=0&dealMode=auto&handCount=4&seed=20261652&baseUnit={baseUnit.ToString(CultureInfo.InvariantCulture)}";
        if (seatHint.HasValue) query += $"&seat={seatHint.Value}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var peer = new WsPeer(await client.ConnectAsync(new Uri(server.BaseAddress, query), timeout.Token),
            room, player.PlayerId, output);
        try
        {
            await peer.BarrierAsync();
            await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "ownTurn", "gameComplete" }
                .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
            await peer.BarrierAsync();
            return peer;
        }
        catch
        {
            await peer.DisposeAsync();
            throw;
        }
    }

    private static async Task TakeSeatAsync(WsPeer peer, int seat)
    {
        await peer.UpdateAsync([new object[] { "seats", peer.PlayerId, new { seat } }]);
        await peer.BarrierAsync();
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, string game) =>
        await host.Runtime.TryGetSnapshotCopyAsync(game)
        ?? throw new InvalidOperationException($"Expected existing runtime {game}.");

    private static async Task<string[]> DatabaseGameIdsAsync(HudsonOwnTurnWsFixture host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.ChangshaGames.AsNoTracking().Select(game => game.Id).ToArrayAsync();
        return ids.Select(id => id.ToString()).OrderBy(id => id).ToArray();
    }

    private static async Task AssertPersistedAsync(HudsonOwnTurnWsFixture host, ChangshaGameState expected)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.Parse(expected.GameId);
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == id);
        var persisted = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(persisted);
        Assert.Equal(expected.StateVersion, row.StateVersion);
        AssertCoreEqual(expected, persisted);
    }

    private static void AssertCoreEqual(ChangshaGameState expected, ChangshaGameState actual)
    {
        Assert.Equal(Core(expected), Core(actual));
        AssertInventory(actual);
    }

    private static string Core(ChangshaGameState state) => JsonSerializer.Serialize(new
    {
        state.GameId, state.Seed, state.BaseUnit, state.MaxHands, state.DealMode,
        state.Phase, state.HandNumber, state.RoundNumber, state.RoundWind, state.DealerSeatIndex,
        state.ActiveSeatIndex, state.TurnNumber, state.StateVersion, state.EventSequence, state.EventLog,
        state.DiscardsThisHand, state.LastDrawSeatIndex, state.LastDrawWasKongReplacement,
        state.Wall, state.WallDrawIndex, state.WallBackIndex, state.WallBackDrawn, state.BreakPoint,
        state.Hands, state.DiscardPile, state.ClaimWindow, state.CurrentWin, state.CurrentScore,
        state.CumulativeScores, state.IsGameComplete, state.MissedWinSeats,
        seats = state.Seats.Select(seat => new { seat.SeatIndex, seat.PlayerId, seat.IsBot, seat.IsDealer })
    });

    private static void AssertProjection(JsonElement snapshot, ChangshaGameState state, int? ownerSeat)
    {
        var match = Entry(snapshot, "match", 0);
        Assert.Equal(state.BaseUnit, match.GetProperty("conditions").GetProperty("baseUnit").GetInt32());
        foreach (var hand in state.Hands)
        {
            var entries = Entries(snapshot).Where(entry => entry[0].GetString() == "things"
                && entry[2].ValueKind == JsonValueKind.Object
                && entry[2].TryGetProperty("slotName", out var slot)
                && slot.ValueKind == JsonValueKind.String
                && slot.GetString()!.StartsWith("hand.", StringComparison.Ordinal)
                && slot.GetString()!.EndsWith($"@{hand.SeatIndex}", StringComparison.Ordinal)).ToArray();
            Assert.Equal(hand.ConcealedTiles.Count, entries.Length);
            if (hand.SeatIndex == ownerSeat)
            {
                Assert.Equal(hand.ConcealedTiles.OrderBy(tile => tile), entries.Select(entry => entry[1].GetInt32()).OrderBy(tile => tile));
                Assert.All(entries, entry => Assert.Equal(1, entry[2].GetProperty("rotationIndex").GetInt32()));
            }
            else
            {
                Assert.All(entries, entry =>
                {
                    Assert.Equal(JsonValueKind.String, entry[1].ValueKind);
                    Assert.StartsWith("h_", entry[1].GetString());
                    Assert.Equal(2, entry[2].GetProperty("rotationIndex").GetInt32());
                    Assert.False(entry[2].TryGetProperty("face", out var face) && face.ValueKind != JsonValueKind.Null);
                });
                Assert.Equal(JsonValueKind.Null, Entry(snapshot, "ownTurn", hand.SeatIndex).ValueKind);
            }
        }
    }

    private static JsonElement Entry(JsonElement snapshot, string kind, int seat)
    {
        var entries = Entries(snapshot).Where(entry => entry[0].GetString() == kind
            && entry[1].ToString() == seat.ToString(CultureInfo.InvariantCulture)).ToArray();
        Assert.NotEmpty(entries);
        return entries[^1][2];
    }

    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static bool IsFull(JsonElement frame) => frame.GetProperty("type").GetString() == "UPDATE"
        && frame.TryGetProperty("full", out var full) && full.GetBoolean();

    private sealed record SignedPlayer(string PlayerId, string Credential);
}
