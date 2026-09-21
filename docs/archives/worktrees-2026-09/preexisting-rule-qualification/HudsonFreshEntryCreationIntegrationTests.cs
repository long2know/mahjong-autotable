using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonFreshEntryCreationIntegrationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationFreshEntry")]
    [InlineData("JOIN")]
    [InlineData("NEW")]
    public async Task OrdinaryFreshEntry_AcquiresSignedOwnersAndAdvancesWithoutDuplicateRows(string firstPacket)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        var alias = $"hudson-fresh-entry-{Guid.NewGuid():N}";
        var players = Enumerable.Range(0, 4)
            .Select(_ => $"hudson-fresh-owner-{Guid.NewGuid():N}").ToArray();
        const int seed = 20261652;
        var query = $"variant=changsha&gameId={Uri.EscapeDataString(alias)}"
            + $"&bots=false&botCount=0&dealMode=auto&handCount=4&seed={seed}&baseUnit=7&botDifficulty=hard";
        var peers = new List<WsPeer>();
        Assert.Empty((await RowsAsync(host)).GameIds);
        Assert.Empty((await RowsAsync(host)).Bindings);
        Assert.Equal(0, host.Runtime.GameCount);

        try
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var socket = await host.OpenSocketAsync(query, players[seat]);
                var peer = new WsPeer(socket, alias, players[seat], output);
                peers.Add(peer);
                if (seat == 0 && firstPacket == "NEW")
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "NEW" });
                    output.WriteLine($"First native request: {Encoding.UTF8.GetString(bytes)}");
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
                    var buffer = new byte[8192];
                    var acknowledgement = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    Assert.Equal(WebSocketMessageType.Text, acknowledgement.MessageType);
                    Assert.True(acknowledgement.EndOfMessage);
                    using var document = JsonDocument.Parse(buffer.AsMemory(0, acknowledgement.Count));
                    var joined = document.RootElement;
                    Assert.Equal("JOINED", joined.GetProperty("type").GetString());
                    Assert.Equal(alias, joined.GetProperty("gameId").GetString());
                    Assert.Equal(players[0], joined.GetProperty("playerId").GetString());
                    Assert.True(joined.GetProperty("isFirst").GetBoolean());
                    output.WriteLine($"First native response: {joined.GetRawText()}");
                }
                else if (seat == 0)
                {
                    Assert.Equal("JOIN", firstPacket);
                    output.WriteLine($"First native request: {JsonSerializer.Serialize(new { type = "JOIN", gameId = alias })}");
                }

                var initialFull = await peer.BarrierAsync();
                Assert.True(initialFull.GetProperty("full").GetBoolean());
                if (seat == 0)
                {
                    var joined = Assert.Single(peer.Frames.Where(frame =>
                        frame.GetProperty("type").GetString() == "JOINED"));
                    Assert.Equal(alias, joined.GetProperty("gameId").GetString());
                    Assert.Equal(players[0], joined.GetProperty("playerId").GetString());
                    if (firstPacket == "JOIN") Assert.True(joined.GetProperty("isFirst").GetBoolean());
                    Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(alias));
                    Assert.Empty((await RowsAsync(host)).GameIds);
                    Assert.Empty((await RowsAsync(host)).Bindings);
                }

                await peer.UpdateAsync(new[] { "claim", "pickup", "turn", "discard", "ownTurn", "gameComplete" }
                    .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
                await peer.UpdateAsync([new object[] { "seats", players[seat], new { seat } }]);
                await peer.BarrierAsync();
                var bound = host.Manager.GetRuntimeGameIdBoundTo(alias);
                Assert.NotNull(bound);
                Assert.Equal(seat, host.Runtime.TryGetSeatForPlayer(bound, players[seat]));
                var rows = await RowsAsync(host);
                Assert.Equal(new[] { Guid.Parse(bound) }, rows.GameIds);
                var binding = Assert.Single(rows.Bindings);
                Assert.Equal(alias, binding.RoomId);
                Assert.Equal(Guid.Parse(bound), binding.RuntimeGameId);
                Assert.Equal(RoomKey(alias), binding.RoomKey);
            }

            var game = host.Manager.GetRuntimeGameIdBoundTo(alias);
            Assert.NotNull(game);
            var ready = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, ready.Phase);
            Assert.Equal(0, ready.ActiveSeatIndex);
            Assert.Equal(14, ready.Hands[0].ConcealedTiles.Count);
            Assert.Equal(55, ready.Wall.Count);
            Assert.Equal(seed, ready.Seed);
            Assert.Equal(7, ready.BaseUnit);
            Assert.Equal(4, ready.MaxHands);
            Assert.Equal(DealMode.Auto, ready.DealMode);
            Assert.Equal("hard", ready.BotDifficulty);
            Assert.Equal(players[0], ready.CreatorPlayerId);
            Assert.Equal(players, ready.Seats.OrderBy(seat => seat.SeatIndex).Select(seat => seat.PlayerId));
            Assert.All(ready.Seats, seat => Assert.False(seat.IsBot));
            AssertInventory(ready);
            var committed = await RowsAsync(host);
            Assert.Equal(1, host.Runtime.GameCount);
            AssertOwnedHand(await peers[0].BarrierAsync(), ready, 0);
            await AssertPersistedAsync(host, ready);

            var discarded = ready.Hands[0].ConcealedTiles[0];
            await peers[0].UpdateAsync([new object[] { "discard", 0, new { tileId = discarded } }]);
            await peers[0].BarrierAsync();
            var afterDiscard = await SnapshotAsync(host, game);
            var claimPasses = 0;
            if (afterDiscard.ClaimWindow is { } window)
            {
                foreach (var seat in window.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct())
                {
                    var current = await SnapshotAsync(host, game);
                    Assert.NotNull(current.ClaimWindow);
                    await peers[seat].UpdateAsync([new object[] { "claim", seat, new
                    {
                        gameId = game, expectedVersion = current.StateVersion, action = "pass"
                    } }]);
                    await peers[seat].BarrierAsync();
                    claimPasses++;
                }
            }
            foreach (var peer in peers) await peer.BarrierAsync();
            var progressed = await SnapshotAsync(host, game);
            Assert.Equal(game, host.Manager.GetRuntimeGameIdBoundTo(alias));
            Assert.True(progressed.StateVersion > ready.StateVersion);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, progressed.Phase);
            Assert.Equal(1, progressed.ActiveSeatIndex);
            Assert.Equal(1, progressed.LastDrawSeatIndex);
            Assert.Equal(ready.Hands[0].ConcealedTiles.Where(tile => tile != discarded),
                progressed.Hands[0].ConcealedTiles);
            Assert.Equal(14, progressed.Hands[1].ConcealedTiles.Count);
            Assert.Equal(ready.Wall[0], progressed.Hands[1].ConcealedTiles[^1]);
            Assert.Equal(ready.Wall.Skip(1), progressed.Wall);
            Assert.Contains(progressed.EventLog.Skip(ready.EventLog.Count),
                entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 0 && entry.TileId == discarded);
            Assert.Contains(progressed.EventLog.Skip(ready.EventLog.Count),
                entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1 && entry.TileId == ready.Wall[0]);
            Assert.Null(progressed.ClaimWindow);
            Assert.Null(progressed.CurrentWin);
            Assert.Null(progressed.CurrentScore);
            Assert.Equal(ready.CumulativeScores.OrderBy(pair => pair.Key),
                progressed.CumulativeScores.OrderBy(pair => pair.Key));
            AssertInventory(progressed);
            AssertOwnedHand(await peers[1].BarrierAsync(), progressed, 1);
            var after = await RowsAsync(host);
            Assert.Equal(committed.GameIds, after.GameIds);
            Assert.Equal(committed.Bindings, after.Bindings);
            Assert.Equal(1, host.Runtime.GameCount);
            await AssertPersistedAsync(host, progressed);
            output.WriteLine($"ORDINARY_FRESH_ENTRY firstPacket={firstPacket}, alias={alias}, runtime={game}, seed={seed}, Auto/unit7/cap4/hard/botCount0; four real signed owners, gameRows1/bindingRows1; dealer14 -> discard={discarded}, normalClaimPasses={claimPasses}, nextHumanDraw={ready.Wall[0]}, version={ready.StateVersion}->{progressed.StateVersion}, wall55->{progressed.Wall.Count}, inventory108, persisted state exact.");
            output.WriteLine("Native ordinary nonlegacy creation only: no legacy baseline, recovery, lost-confirmation retry, browser control, UI marker-consumption or match-qualification claim.");
        }
        finally
        {
            foreach (var peer in peers) await peer.DisposeAsync();
        }
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, string game) =>
        await host.Runtime.TryGetSnapshotCopyAsync(game)
        ?? throw new InvalidOperationException($"Expected actual fresh runtime {game}.");

    private static void AssertOwnedHand(JsonElement frame, ChangshaGameState state, int seat)
    {
        Assert.Equal("UPDATE", frame.GetProperty("type").GetString());
        Assert.True(frame.GetProperty("full").GetBoolean());
        var tiles = frame.GetProperty("entries").EnumerateArray()
            .Where(entry => entry[0].GetString() == "things"
                && entry[2].ValueKind == JsonValueKind.Object
                && entry[2].TryGetProperty("slotName", out var slot)
                && slot.ValueKind == JsonValueKind.String
                && slot.GetString()!.StartsWith("hand.", StringComparison.Ordinal)
                && slot.GetString()!.EndsWith($"@{seat}", StringComparison.Ordinal)).ToArray();
        Assert.Equal(state.Hands[seat].ConcealedTiles.Count, tiles.Length);
        Assert.Equal(state.Hands[seat].ConcealedTiles.OrderBy(tile => tile),
            tiles.Select(entry => entry[1].GetInt32()).OrderBy(tile => tile));
        Assert.All(tiles, entry => Assert.Equal(1, entry[2].GetProperty("rotationIndex").GetInt32()));
    }

    private static async Task<DatabaseRows> RowsAsync(HudsonOwnTurnWsFixture host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var games = await db.ChangshaGames.AsNoTracking().Select(game => game.Id).ToArrayAsync();
        var bindings = await db.AutotableRoomBindings.AsNoTracking().ToArrayAsync();
        return new DatabaseRows(games.OrderBy(id => id).ToArray(),
            bindings.Select(binding => new Binding(binding.RoomKey, binding.RoomId, binding.RuntimeGameId, binding.CreatedUtc))
                .OrderBy(binding => binding.RoomKey, StringComparer.Ordinal).ToArray());
    }

    private static async Task AssertPersistedAsync(HudsonOwnTurnWsFixture host, ChangshaGameState expected)
    {
        using var scope = host.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangshaGames
            .AsNoTracking().SingleAsync(game => game.Id == Guid.Parse(expected.GameId));
        var persisted = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(persisted);
        Assert.Equal(expected.StateVersion, row.StateVersion);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(persisted));
        AssertInventory(persisted);
    }

    private static string RoomKey(string alias) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(alias))).ToLowerInvariant();

    private sealed record Binding(string RoomKey, string RoomId, Guid RuntimeGameId, DateTime CreatedUtc);
    private sealed record DatabaseRows(Guid[] GameIds, Binding[] Bindings);
}
