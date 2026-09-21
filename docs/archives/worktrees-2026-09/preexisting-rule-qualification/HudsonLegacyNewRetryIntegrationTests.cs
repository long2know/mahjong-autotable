using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Persistence;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonLegacyNewRetryIntegrationTests(ITestOutputHelper output)
{
    private const int Seed = 20261652;

    [Theory, Trait("Category", "RulesQualificationRecovery")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyOwnerExplicitNew_LostConfirmationRetryRestoresSameCommittedRuntime(
        bool restartBeforeRetry)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true, output: output);
        var players = Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        var legacy = await CreateUnaliasedLegacyGameAsync(host, players);
        var originalRows = await RowsAsync(host);
        Assert.Single(originalRows.GameIds);
        Assert.Empty(originalRows.Bindings);
        await AssertPersistedAsync(host, legacy);
        await host.RestartAsync();
        AssertRowsEqual(originalRows, await RowsAsync(host));
        AssertCoreEqual(legacy, await SnapshotAsync(host, legacy.GameId));
        var legacyStored = await StoredRowAsync(host, legacy.GameId);
        using var lifecycle = new PersistedLifecycleTimeline(host, legacy.GameId, restartBeforeRetry, output);
        var legacyBaseline = lifecycle.Observe("initial-request-baseline")[Guid.Parse(legacy.GameId)];
        Assert.Equal(legacyStored, legacyBaseline.OriginalRow);

        var alias = $"hudson-legacy-new-{Guid.NewGuid():N}";
        var query = $"autotable/ws?variant=changsha&gameId={Uri.EscapeDataString(alias)}"
            + $"&bots=false&botCount=0&dealMode=auto&handCount=4&seed={Seed}&baseUnit=7&botDifficulty=hard";
        using (var ambiguous = await SocketAsync(host, query, players[0]))
        {
            await SendAsync(ambiguous, new { type = "JOIN", gameId = alias });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var rejected = await ReceiveAsync(ambiguous, timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, rejected.Type);
            using var document = JsonDocument.Parse(rejected.Bytes);
            var frame = document.RootElement;
            Assert.Equal("UPDATE", frame.GetProperty("type").GetString());
            Assert.False(frame.GetProperty("full").GetBoolean());
            var error = Assert.Single(Entries(frame));
            Assert.Equal("actionRejected", error[0].GetString());
            Assert.Equal("current", error[1].GetString());
            Assert.Equal("room", error[2].GetProperty("action").GetString());
            Assert.Equal("legacy-room-binding-unavailable", error[2].GetProperty("reason").GetString());
            var close = await ReceiveAsync(ambiguous, timeout.Token);
            Assert.Equal(WebSocketMessageType.Close, close.Type);
            Assert.Equal(WebSocketCloseStatus.InternalServerError, close.CloseStatus);
            Assert.Equal("legacy-room-binding-unavailable", close.Description);
        }
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(alias));
        AssertRowsEqual(originalRows, await RowsAsync(host));
        Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));
        output.WriteLine($"Real signed legacy-Hub runtime={legacy.GameId}, seed={Seed}, rows1/bindings0; after same-key/database restart, unknown alias JOIN rejected/1011 without guessed binding or new row.");

        string freshGame;
        ChangshaGameState ready;
        DatabaseRows committedRows;
        var otherOwners = new List<WsPeer>();
        await using (var dropped = new DroppedConfirmationPeer(await SocketAsync(host, query, players[0])))
        {
            var first = await dropped.ExchangeAsync(new { type = "NEW" });
            AssertJoined(first.Joined, alias, players[0], isFirst: true);
            Assert.DoesNotContain(Entries(first.Full), entry => entry[0].GetString() == "things");
            Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(alias));
            AssertRowsEqual(originalRows, await RowsAsync(host));

            await dropped.SendUpdateAsync(new[] { "claim", "pickup", "turn", "discard", "ownTurn", "gameComplete" }
                .Select(kind => new object[] { "ephemeral", kind, true }).ToArray());
            await dropped.SendUpdateAsync([new object[] { "seats", players[0].PlayerId, new { seat = 0 } }]);
            await dropped.ExchangeAsync(new { type = "JOIN", gameId = alias });
            freshGame = host.Manager.GetRuntimeGameIdBoundTo(alias)
                ?? throw new InvalidOperationException("Explicit NEW plus actual seat request did not create a runtime.");
            lifecycle.Track(freshGame);
            Assert.NotEqual(legacy.GameId, freshGame);
            var createdRows = await RowsAsync(host);
            Assert.Equal(2, createdRows.GameIds.Length);
            var binding = Assert.Single(createdRows.Bindings);
            Assert.Equal(alias, binding.RoomId);
            Assert.Equal(RoomKey(alias), binding.RoomKey);
            Assert.Equal(Guid.Parse(freshGame), binding.RuntimeGameId);
            Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));

            try
            {
                for (var seat = 1; seat < 4; seat++)
                {
                    var peer = new WsPeer(await SocketAsync(host, query, players[seat]),
                        alias, players[seat].PlayerId, output);
                    otherOwners.Add(peer);
                    await peer.BarrierAsync();
                    await peer.UpdateAsync([new object[] { "seats", peer.PlayerId, new { seat } }]);
                    await peer.BarrierAsync();
                }
                ready = await SnapshotAsync(host, freshGame);
                Assert.Equal(ChangshaPhase.AwaitingDiscard, ready.Phase);
                Assert.Equal(0, ready.ActiveSeatIndex);
                Assert.Equal(14, ready.Hands[0].ConcealedTiles.Count);
                Assert.Equal(Seed, ready.Seed);
                Assert.Equal(7, ready.BaseUnit);
                Assert.Equal(4, ready.MaxHands);
                Assert.Equal(DealMode.Auto, ready.DealMode);
                Assert.Equal("hard", ready.BotDifficulty);
                Assert.Equal(players.Select(player => player.PlayerId),
                    ready.Seats.OrderBy(seat => seat.SeatIndex).Select(seat => seat.PlayerId));
                Assert.All(ready.Seats, seat => Assert.False(seat.IsBot));
                AssertInventory(ready);
                var unconsumedConfirmation = await dropped.ExchangeAsync(new { type = "JOIN", gameId = alias });
                await AssertBoundTurnAsync(host, unconsumedConfirmation, alias, players[0], ready, 0);
                AssertPrivateProjection(unconsumedConfirmation.Full, ready, 0);
                await AssertPersistedAsync(host, ready);
                committedRows = await RowsAsync(host);
                AssertRowsEqual(createdRows, committedRows);
                Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));
                Assert.NotEmpty(dropped.Frames.Where(frame => frame.GetProperty("type").GetString() == "JOINED"));
                Assert.NotEmpty(dropped.Frames.Where(IsFull));
            }
            finally
            {
                foreach (var peer in otherOwners) await peer.DisposeAsync();
            }
            await dropped.ExchangeAsync(new { type = "JOIN", gameId = alias });
            foreach (var frame in dropped.Frames)
                output.WriteLine($"Native confirmation deliberately dropped before any application consumer: {frame.GetRawText()}");
            output.WriteLine($"Committed before loss: alias={alias}, runtime={freshGame}, rows2/bindings1, phase={ready.Phase}, v{ready.StateVersion}, wall={ready.Wall.Count}; raw transport drained, no UI/client collection or synthetic acknowledgment involved.");
        }

        var beforeRestart = lifecycle.Observe("before-restart-or-retry");
        AssertDurableEqual(legacyBaseline, beforeRestart[Guid.Parse(legacy.GameId)]);
        if (restartBeforeRetry)
        {
            await host.RestartAsync();
            var recovered = lifecycle.Observe("after-startup-recovery");
            lifecycle.AssertRecoveryBoundary(beforeRestart, recovered);
            // Recovery itself durably records its no-gameplay replay round trip.
            // The following full-row equalities isolate requests after that boundary.
            legacyBaseline = recovered[Guid.Parse(legacy.GameId)];
            legacyStored = legacyBaseline.OriginalRow;
        }
        AssertRowsEqual(committedRows, await RowsAsync(host));
        AssertCoreEqual(ready, await SnapshotAsync(host, freshGame));
        AssertCoreEqual(legacy, await SnapshotAsync(host, legacy.GameId));
        Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));

        var retrySocket = await SocketAsync(host, query, players[0]);
        await using var retry = new WsPeer(retrySocket, alias, players[0].PlayerId, output);
        AssertDurableEqual(legacyBaseline, lifecycle.Observe("before-new-retry")[Guid.Parse(legacy.GameId)]);
        await SendAsync(retrySocket, new { type = "NEW" });
        var confirmation = await ReadHandshakeAsync(retrySocket);
        AssertDurableEqual(legacyBaseline, lifecycle.Observe("after-new-retry")[Guid.Parse(legacy.GameId)]);
        AssertJoined(confirmation.Joined, alias, players[0], isFirst: false);
        Assert.Equal(freshGame, host.Manager.GetRuntimeGameIdBoundTo(alias));
        await AssertBoundTurnAsync(host, confirmation, alias, players[0], ready, 0);
        AssertPrivateProjection(confirmation.Full, ready, 0);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(freshGame, players[0].PlayerId));
        await retry.UpdateAsync([new object[] { "seats", players[0].PlayerId, new { seat = 0 } }]);
        var joinedAfterIntent = await retry.BarrierAsync();
        AssertDurableEqual(legacyBaseline, lifecycle.Observe("after-ordinary-join")[Guid.Parse(legacy.GameId)]);
        await AssertBoundTurnAsync(host,
            new Handshake(retry.Frames.Last(frame => frame.GetProperty("type").GetString() == "JOINED"), joinedAfterIntent),
            alias, players[0], ready, 0);
        AssertCoreEqual(ready, await SnapshotAsync(host, freshGame));
        AssertRowsEqual(committedRows, await RowsAsync(host));
        Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));
        await AssertPersistedAsync(host, ready);

        var noClaimDiscards = ready.Hands[0].ConcealedTiles
            .Where(tile => new ClaimAdjudicator().GetOpportunities(0, tile, ready.Hands).Count == 0)
            .OrderBy(tile => tile).ToArray();
        Assert.NotEmpty(noClaimDiscards);
        var discarded = noClaimDiscards[0];
        await retry.UpdateAsync([new object[] { "discard", 0, new { tileId = discarded } }]);
        await retry.BarrierAsync();
        var progressed = await SnapshotAsync(host, freshGame);
        Assert.True(progressed.StateVersion > ready.StateVersion);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, progressed.Phase);
        Assert.Equal(1, progressed.ActiveSeatIndex);
        Assert.Equal(1, progressed.LastDrawSeatIndex);
        Assert.Equal(14, progressed.Hands[1].ConcealedTiles.Count);
        Assert.Equal(ready.Wall.Skip(1), progressed.Wall);
        Assert.Contains(progressed.EventLog.Skip(ready.EventLog.Count),
            entry => entry.EventType == "tile-discarded" && entry.SeatIndex == 0 && entry.TileId == discarded);
        Assert.Contains(progressed.EventLog.Skip(ready.EventLog.Count),
            entry => entry.EventType == "tile-drawn" && entry.SeatIndex == 1 && entry.TileId == ready.Wall[0]);
        Assert.Null(progressed.CurrentWin);
        Assert.Null(progressed.CurrentScore);
        AssertInventory(progressed);
        AssertRowsEqual(committedRows, await RowsAsync(host));
        AssertCoreEqual(legacy, await SnapshotAsync(host, legacy.GameId));
        Assert.Equal(legacyStored, await StoredRowAsync(host, legacy.GameId));
        await AssertPersistedAsync(host, progressed);
        AssertDurableEqual(legacyBaseline, lifecycle.Observe("after-continued-discard")[Guid.Parse(legacy.GameId)]);
        output.WriteLine($"NEW retry restartBeforeRetry={restartBeforeRetry}, identical alias/query/verbatim signed credential SHA256={Hash(Encoding.UTF8.GetBytes(players[0].Credential))}: isFirst=false, original fresh runtime={freshGame}, gameRows2/bindingRows1, owner14 without extra draw/redeal; subsequent ordinary JOIN and real discard={discarded} -> next human draw={ready.Wall[0]}/v{progressed.StateVersion}, inventory108. Legacy runtime={legacy.GameId} and its stored row stayed unchanged.");
    }

    private static async Task<ChangshaGameState> CreateUnaliasedLegacyGameAsync(
        HudsonOwnTurnWsFixture host, SignedPlayer[] players)
    {
        var connections = new List<HubConnection>();
        string? game = null;
        try
        {
            foreach (var player in players) connections.Add(await HubAsync(host, player));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var created = await connections[0].InvokeCoreAsync<JsonElement>("CreateGame",
                ["changsha-v1", Array.Empty<int>(), Seed], timeout.Token);
            game = created.GetProperty("gameId").GetString();
            Assert.False(string.IsNullOrEmpty(game));
            for (var seat = 0; seat < 4; seat++)
            {
                var taken = await connections[seat].InvokeCoreAsync<JsonElement>("TakeSeat",
                    [game, seat], timeout.Token);
                Assert.True(taken.GetProperty("success").GetBoolean());
                Assert.Equal(seat, taken.GetProperty("seatIndex").GetInt32());
            }
            await connections[0].InvokeCoreAsync("StartGame", [game], timeout.Token);
            for (var seat = 0; seat < 4; seat++)
                await connections[seat].InvokeCoreAsync("AcknowledgeDeal", [game, seat], timeout.Token);
        }
        finally
        {
            foreach (var connection in connections) await connection.DisposeAsync();
        }
        Assert.NotNull(game);
        var state = await SnapshotAsync(host, game);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Equal(14, state.Hands[0].ConcealedTiles.Count);
        Assert.Equal(Seed, state.Seed);
        Assert.Equal(players.Select(player => player.PlayerId),
            state.Seats.OrderBy(seat => seat.SeatIndex).Select(seat => seat.PlayerId));
        Assert.All(state.Seats, seat => Assert.False(seat.IsBot));
        AssertInventory(state);
        return state;
    }

    private static SignedPlayer Identity(HudsonOwnTurnWsFixture host)
    {
        var player = $"hudson-legacy-new-owner-{Guid.NewGuid():N}";
        return new SignedPlayer(player, host.Services.GetRequiredService<PlayerIdentityService>().Protect(player));
    }

    private static TestServer Server(HudsonOwnTurnWsFixture host) =>
        Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());

    private static async Task<HubConnection> HubAsync(HudsonOwnTurnWsFixture host, SignedPlayer player)
    {
        var server = Server(host);
        var connection = new HubConnectionBuilder().WithUrl(new Uri(server.BaseAddress, "hubs/changsha"), options =>
        {
            options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            options.Headers.Add("Cookie", $"{PlayerIdentityService.CookieName}={player.Credential}");
        }).Build();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await connection.StartAsync(timeout.Token);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<WebSocket> SocketAsync(HudsonOwnTurnWsFixture host, string query, SignedPlayer player)
    {
        var server = Server(host);
        var client = server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={player.Credential}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await client.ConnectAsync(new Uri(server.BaseAddress, query), timeout.Token);
    }

    private static async Task SendAsync(WebSocket socket, object message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message),
            WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<ReceivedFrame> ReceiveAsync(WebSocket socket, CancellationToken cancellation)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[64 * 1024];
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
            bytes.Write(buffer, 0, part.Count);
        } while (!part.EndOfMessage);
        return new ReceivedFrame(part.MessageType, bytes.ToArray(), part.CloseStatus, part.CloseStatusDescription);
    }

    private static async Task<Handshake> ReadHandshakeAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        JsonElement? joined = null;
        while (true)
        {
            var received = await ReceiveAsync(socket, timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, received.Type);
            using var document = JsonDocument.Parse(received.Bytes);
            var frame = document.RootElement.Clone();
            if (frame.GetProperty("type").GetString() == "JOINED") joined = frame;
            else if (joined.HasValue && IsFull(frame)) return new Handshake(joined.Value, frame);
        }
    }

    private static void AssertJoined(JsonElement frame, string alias, SignedPlayer player, bool isFirst)
    {
        Assert.Equal(alias, frame.GetProperty("gameId").GetString());
        Assert.Equal(player.PlayerId, frame.GetProperty("playerId").GetString());
        Assert.Equal(isFirst, frame.GetProperty("isFirst").GetBoolean());
    }

    private static async Task AssertBoundTurnAsync(
        HudsonOwnTurnWsFixture host, Handshake confirmation, string alias, SignedPlayer player,
        ChangshaGameState expected, int seat)
    {
        Assert.Equal("JOINED", confirmation.Joined.GetProperty("type").GetString());
        AssertJoined(confirmation.Joined, alias, player, isFirst: false);
        var frame = confirmation.Full;
        Assert.True(IsFull(frame));
        var binding = Assert.Single((await RowsAsync(host)).Bindings,
            candidate => string.Equals(candidate.RoomId, alias, StringComparison.Ordinal));
        Assert.Equal(RoomKey(alias), binding.RoomKey);
        Assert.Equal(Guid.Parse(expected.GameId), binding.RuntimeGameId);
        Assert.Equal(expected.GameId, host.Manager.GetRuntimeGameIdBoundTo(alias));
        var state = await SnapshotAsync(host, expected.GameId);
        Assert.Equal(expected.GameId, state.GameId);
        Assert.Equal(expected.StateVersion, state.StateVersion);
        Assert.Equal(expected.Phase, state.Phase);

        // JOINED names the public alias; the exact durable binding supplies the
        // runtime identity. TurnEntry itself carries only these three cue fields.
        var turn = Assert.Single(Entries(frame), entry => entry[0].GetString() == "turn"
            && entry[1].ToString() == "current")[2];
        Assert.Equal(new[] { "activeSeat", "awaitingDiscard", "phase" },
            turn.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(expected.Phase.ToString(), turn.GetProperty("phase").GetString());
        Assert.Equal(seat, turn.GetProperty("activeSeat").GetInt32());
        Assert.True(turn.GetProperty("awaitingDiscard").GetBoolean());
        var match = Assert.Single(Entries(frame),
            entry => entry[0].GetString() == "match" && entry[1].ToString() == "0")[2];
        Assert.Equal(expected.DealerSeatIndex, match.GetProperty("dealer").GetInt32());
        Assert.Equal(expected.BaseUnit, match.GetProperty("conditions").GetProperty("baseUnit").GetInt32());
        Assert.Equal(expected.DealMode == DealMode.Manual ? "manual" : "auto",
            match.GetProperty("conditions").GetProperty("dealMode").GetString());
    }

    private static void AssertPrivateProjection(JsonElement frame, ChangshaGameState state, int owner)
    {
        foreach (var hand in state.Hands)
        {
            var tiles = Entries(frame).Where(entry => entry[0].GetString() == "things"
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
            }
        }
    }

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, string game) =>
        await host.Runtime.TryGetSnapshotCopyAsync(game)
        ?? throw new InvalidOperationException($"Expected real runtime {game}.");

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

    private static async Task<string> StoredRowAsync(HudsonOwnTurnWsFixture host, string game)
    {
        using var scope = host.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangshaGames
            .AsNoTracking().SingleAsync(gameRow => gameRow.Id == Guid.Parse(game));
        return JsonSerializer.Serialize(new { row.Id, row.StateJson, row.StateVersion, row.UpdatedUtc });
    }

    private static async Task AssertPersistedAsync(HudsonOwnTurnWsFixture host, ChangshaGameState expected)
    {
        using var scope = host.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangshaGames
            .AsNoTracking().SingleAsync(game => game.Id == Guid.Parse(expected.GameId));
        var saved = JsonSerializer.Deserialize<ChangshaGameState>(row.StateJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(saved);
        Assert.Equal(expected.StateVersion, row.StateVersion);
        AssertCoreEqual(expected, saved);
    }

    private static void AssertRowsEqual(DatabaseRows expected, DatabaseRows actual)
    {
        Assert.Equal(expected.GameIds, actual.GameIds);
        Assert.Equal(expected.Bindings, actual.Bindings);
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

    private static string RoomKey(string alias) => Hash(Encoding.UTF8.GetBytes(alias));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];
    private static bool IsFull(JsonElement frame) => frame.GetProperty("type").GetString() == "UPDATE"
        && frame.TryGetProperty("full", out var full) && full.GetBoolean();

    private sealed record SignedPlayer(string PlayerId, string Credential);
    private sealed record Binding(string RoomKey, string RoomId, Guid RuntimeGameId, DateTime CreatedUtc);
    private sealed record DatabaseRows(Guid[] GameIds, Binding[] Bindings);
    private sealed record Handshake(JsonElement Joined, JsonElement Full);
    private sealed record ReceivedFrame(
        WebSocketMessageType Type, byte[] Bytes, WebSocketCloseStatus? CloseStatus, string? Description);

    private sealed record DurableSnapshot(
        Guid GameId, DateTime ObservedUtc, DateTime UpdatedUtc, int StateVersion,
        string FullRow, string OriginalRow, string StateJson, string[] JournalRows, string[] JournalDetails);

    private static void AssertDurableEqual(DurableSnapshot expected, DurableSnapshot actual)
    {
        Assert.Equal(expected.GameId, actual.GameId);
        Assert.True(string.Equals(expected.FullRow, actual.FullRow, StringComparison.Ordinal),
            "The complete persisted row changed outside the recovery boundary.");
        Assert.True(expected.JournalRows.SequenceEqual(actual.JournalRows),
            "The persisted replay journal changed outside the recovery boundary.");
    }

    private sealed class PersistedLifecycleTimeline :
        IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private const string RecoveryPhase = "replacement-built-before-recovery";
        private readonly string _database;
        private readonly bool _restart;
        private readonly ITestOutputHelper _output;
        private readonly IServiceScopeFactory _oldScopes;
        private readonly ConcurrentDictionary<Guid, byte> _games = new();
        private readonly ConcurrentQueue<object> _events = new();
        private readonly ConcurrentQueue<(string Phase, DurableSnapshot Snapshot)> _writes = new();
        private readonly ConcurrentDictionary<Guid, DurableSnapshot> _beforeRecovery = new();
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();
        private string _phase = "initialized";
        private int _hostBuilds;
        private bool _oldProviderDisposed;

        public PersistedLifecycleTimeline(HudsonOwnTurnWsFixture host, string game, bool restart, ITestOutputHelper output)
        {
            using var scope = host.Services.CreateScope();
            _database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection().DataSource;
            var owned = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-own-turn"))
                + Path.DirectorySeparatorChar;
            Assert.StartsWith(owned, Path.GetFullPath(_database));
            _restart = restart;
            _output = output;
            _oldScopes = host.Services.GetRequiredService<IServiceScopeFactory>();
            Track(game);
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            _subscriptions.Add(lifetime.ApplicationStopping.Register(() => Mark("old-application-stopping")));
            _subscriptions.Add(lifetime.ApplicationStopped.Register(() => Mark("old-application-stopped")));
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public void Track(string game) => _games.TryAdd(Guid.Parse(game), 0);

        private void Mark(string phase)
        {
            Volatile.Write(ref _phase, phase);
            _events.Enqueue(new { kind = "marker", phase, atUtc = DateTime.UtcNow });
        }

        public Dictionary<Guid, DurableSnapshot> Observe(string phase)
        {
            Mark(phase);
            return _games.Keys.ToDictionary(game => game, game => Capture("snapshot", game, null));
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name is "Microsoft.EntityFrameworkCore" or "Microsoft.Extensions.Hosting")
                _subscriptions.Add(listener.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "HostBuilt")
            {
                Interlocked.Increment(ref _hostBuilds);
                try { using var scope = _oldScopes.CreateScope(); }
                catch (ObjectDisposedException) { _oldProviderDisposed = true; }
                Mark(RecoveryPhase);
                foreach (var game in _games.Keys)
                    _beforeRecovery[game] = Capture("post-disposal-pre-recovery", game, null);
            }
            if (value.Value is SaveChangesCompletedEventData saved && saved.Context is { } context
                && string.Equals(context.Database.GetDbConnection().DataSource, _database, StringComparison.Ordinal))
            {
                var stack = new StackTrace().GetFrames()
                    .Select(frame => frame.GetMethod()?.DeclaringType?.FullName ?? "")
                    .Where(name => name.Contains("ChangshaGameRuntime", StringComparison.Ordinal)).Distinct().ToArray();
                foreach (var entry in context.ChangeTracker.Entries<ChangshaGame>())
                {
                    if (!_games.ContainsKey(entry.Entity.Id)) continue;
                    var snapshot = Capture("save-changes-completed", entry.Entity.Id, stack);
                    _writes.Enqueue((Volatile.Read(ref _phase), snapshot));
                }
            }
        }

        private DurableSnapshot Capture(string kind, Guid game, string[]? stack)
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = _database, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false
            };
            using var db = new SqliteAppDbContext(new DbContextOptionsBuilder<SqliteAppDbContext>()
                .UseSqlite(connection.ToString()).Options);
            var row = db.ChangshaGames.AsNoTracking().Single(value => value.Id == game);
            var journal = db.ChangshaGameEvents.AsNoTracking().Where(value => value.GameId == game)
                .OrderBy(value => value.Sequence).ToArray();
            var snapshot = new DurableSnapshot(game, DateTime.UtcNow, row.UpdatedUtc, row.StateVersion,
                JsonSerializer.Serialize(row),
                JsonSerializer.Serialize(new { row.Id, row.StateJson, row.StateVersion, row.UpdatedUtc }),
                row.StateJson, journal.Select(value => JsonSerializer.Serialize(value)).ToArray(),
                journal.Select(value => value.Detail).ToArray());
            var operations = snapshot.JournalDetails.Select(detail =>
            {
                using var document = JsonDocument.Parse(detail);
                return document.RootElement.GetProperty("operation").GetString();
            }).ToArray();
            _events.Enqueue(new
            {
                kind, phase = Volatile.Read(ref _phase), gameId = game, snapshot.ObservedUtc,
                snapshot.UpdatedUtc, snapshot.StateVersion,
                stateJsonSha256 = Hash(Encoding.UTF8.GetBytes(snapshot.StateJson)),
                fullRowSha256 = Hash(Encoding.UTF8.GetBytes(snapshot.FullRow)),
                journalRows = journal.Length,
                journalSha256 = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot.JournalRows))),
                operations, stack
            });
            return snapshot;
        }

        public void AssertRecoveryBoundary(
            IReadOnlyDictionary<Guid, DurableSnapshot> before, IReadOnlyDictionary<Guid, DurableSnapshot> after)
        {
            Assert.Equal(1, Volatile.Read(ref _hostBuilds));
            Assert.True(_oldProviderDisposed, "Replacement construction must follow complete old-provider disposal.");
            Assert.Equal(before.Keys.Order(), after.Keys.Order());
            foreach (var game in before.Keys)
            {
                Assert.True(_beforeRecovery.TryGetValue(game, out var stopped));
                AssertDurableEqual(before[game], stopped);
                var write = Assert.Single(_writes,
                    item => item.Phase == RecoveryPhase && item.Snapshot.GameId == game).Snapshot;
                AssertDurableEqual(write, after[game]);
                Assert.Equal(before[game].StateVersion, after[game].StateVersion);
                Assert.Equal(before[game].StateJson, after[game].StateJson);

                using var original = JsonDocument.Parse(before[game].FullRow);
                using var recovered = JsonDocument.Parse(after[game].FullRow);
                Assert.Equal(original.RootElement.EnumerateObject().Select(property => property.Name),
                    recovered.RootElement.EnumerateObject().Select(property => property.Name));
                foreach (var property in original.RootElement.EnumerateObject())
                {
                    if (property.Name == nameof(ChangshaGame.UpdatedUtc))
                    {
                        Assert.Equal(write.UpdatedUtc, after[game].UpdatedUtc);
                        Assert.InRange(after[game].UpdatedUtc.Ticks, stopped.ObservedUtc.Ticks, write.ObservedUtc.Ticks);
                    }
                    else
                    {
                        Assert.Equal(property.Value.GetRawText(), recovered.RootElement.GetProperty(property.Name).GetRawText());
                    }
                }

                var prefix = before[game].JournalRows.Length;
                Assert.Equal(prefix + 2, after[game].JournalRows.Length);
                Assert.True(before[game].JournalRows.SequenceEqual(after[game].JournalRows.Take(prefix)),
                    "Recovery must retain every existing journal row byte-for-byte.");
                var operations = new List<string?>();
                foreach (var detail in after[game].JournalDetails.Skip(prefix))
                {
                    using var record = JsonDocument.Parse(detail);
                    operations.Add(record.RootElement.GetProperty("operation").GetString());
                    Assert.Equal(record.RootElement.GetProperty("before").GetRawText(),
                        record.RootElement.GetProperty("after").GetRawText());
                }
                Assert.Equal(new[] { "SnapshotRoundTrip", "BindAuthoritativeGameId" }, operations);
                _output.WriteLine($"Verified recovery-only persistence boundary for game={game}: " +
                    "old disposal preserved the full row/journal; one startup write, unchanged state/version, " +
                    "exact two no-gameplay journal records; UpdatedUtc matches the observed committed write.");
            }
        }

        public void OnError(Exception error) => throw new InvalidOperationException("Lifecycle observation failed.", error);
        public void OnCompleted() { }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            var file = Path.Combine(AppContext.BaseDirectory, $"legacy-lifecycle-{_restart}-{Guid.NewGuid():N}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(_events.ToArray(), new JsonSerializerOptions { WriteIndented = true }));
            _output.WriteLine($"Read-only lifecycle timeline: {file}");
        }
    }

    private sealed class DroppedConfirmationPeer : IAsyncDisposable
    {
        private readonly WebSocket _socket;
        private readonly CancellationTokenSource _stop = new();
        private readonly Channel<JsonElement> _received = Channel.CreateUnbounded<JsonElement>();
        private readonly Task _reader;
        public ConcurrentQueue<JsonElement> Frames { get; } = new();

        public DroppedConfirmationPeer(WebSocket socket)
        {
            _socket = socket;
            _reader = DrainAsync();
        }

        public Task SendUpdateAsync(object[] entries) =>
            SendAsync(_socket, new { type = "UPDATE", entries, full = false });

        public async Task<Handshake> ExchangeAsync(object message)
        {
            await SendAsync(_socket, message);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            JsonElement? joined = null;
            while (true)
            {
                var frame = await _received.Reader.ReadAsync(timeout.Token);
                if (frame.GetProperty("type").GetString() == "JOINED") joined = frame;
                else if (joined.HasValue && IsFull(frame)) return new Handshake(joined.Value, frame);
            }
        }

        private async Task DrainAsync()
        {
            try
            {
                while (true)
                {
                    var received = await ReceiveAsync(_socket, _stop.Token);
                    Assert.Equal(WebSocketMessageType.Text, received.Type);
                    using var document = JsonDocument.Parse(received.Bytes);
                    var frame = document.RootElement.Clone();
                    Frames.Enqueue(frame);
                    await _received.Writer.WriteAsync(frame, _stop.Token);
                }
            }
            finally
            {
                _received.Writer.TryComplete();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            try
            {
                await _reader;
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            finally
            {
                _socket.Abort();
                _socket.Dispose();
                _stop.Dispose();
            }
        }
    }
}
