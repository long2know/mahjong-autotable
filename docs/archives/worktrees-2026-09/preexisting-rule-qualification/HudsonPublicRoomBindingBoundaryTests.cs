using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Persistence;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonPublicRoomBindingBoundaryTests(ITestOutputHelper output)
{
    private const string CorruptSnapshot = "{\"private\":\"hudson-binding-private-canary\",";

    [Theory, Trait("Category", "RulesQualificationRecovery")]
    [InlineData("missing-state")]
    [InlineData("corrupt-json")]
    public async Task RecordedAlias_WithUnavailableState_RejectsJoinAndNewWithoutReplacement(string defect)
    {
        await using var host = new BindingBoundaryHost(persist: true, output: output);
        await using var room = await CreateProgressedRoomAsync(host);
        var before = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.NotNull(before.Binding);
        Assert.Equal(Guid.Parse(room.GameId), before.Binding.RuntimeGameId);
        Assert.Equal(room.State.StateVersion, before.StateVersion);

        var faultsApplied = 0;
        using var fault = AtStoppedHost(host, db =>
        {
            Assert.Equal(1, db.AutotableRoomBindings.Count(binding => binding.RoomId == room.RoomId));
            var id = Guid.Parse(room.GameId);
            if (defect == "missing-state")
            {
                // Simulate offline snapshot loss, retaining the binding created by real WS commands.
                db.Database.OpenConnection();
                db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
                Assert.Equal(1, db.ChangshaGames.Where(game => game.Id == id).ExecuteDelete());
            }
            else if (defect == "corrupt-json")
            {
                Assert.Equal(1, db.ChangshaGames.Where(game => game.Id == id)
                    .ExecuteUpdate(update => update.SetProperty(game => game.StateJson, CorruptSnapshot)));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(defect), defect, "Unknown stored-state fault.");
            }
            Assert.Equal(1, db.AutotableRoomBindings.Count(binding => binding.RoomId == room.RoomId));
            Interlocked.Increment(ref faultsApplied);
        });
        await room.DisposeAsync();
        await host.RestartAsync();
        Assert.Equal(1, faultsApplied);

        var damaged = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Equal(before.Binding, damaged.Binding);
        Assert.Equal(before.BindingCount, damaged.BindingCount);
        Assert.Equal(defect == "missing-state" ? 0 : 1, damaged.GameIds.Length);
        Assert.Equal(defect == "missing-state" ? null : CorruptSnapshot, damaged.StateJson);
        Assert.False(host.Runtime.TryGetSnapshot(room.GameId, out _));
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(room.RoomId));
        Assert.Equal(0, host.Runtime.GameCount);

        foreach (var operation in new[] { "JOIN", "NEW" })
        {
            using var socket = await SocketAsync(host, room.RoomId, room.Players[0], "changsha");
            await SendAsync(socket, new { type = operation, gameId = room.RoomId });
            var rejected = await ReceiveAsync(socket);
            Assert.Equal(WebSocketMessageType.Text, rejected.Type);
            using var document = JsonDocument.Parse(rejected.Bytes);
            var frame = document.RootElement;
            Assert.Equal("UPDATE", frame.GetProperty("type").GetString());
            Assert.False(frame.GetProperty("full").GetBoolean());
            var entry = Assert.Single(Entries(frame));
            Assert.Equal("actionRejected", entry[0].GetString());
            Assert.Equal("current", entry[1].GetString());
            Assert.Equal("room", entry[2].GetProperty("action").GetString());
            var reason = entry[2].GetProperty("reason").GetString();
            Assert.NotNull(reason);
            Assert.InRange(reason.Length, 1, 64);
            Assert.Matches("^[a-z][a-z0-9-]*$", reason);
            Assert.StartsWith("room-", reason);
            if (defect == "corrupt-json")
                Assert.Equal("room-snapshot-invalid", reason);
            Assert.DoesNotContain("hudson-binding-private-canary", frame.GetRawText());
            var close = await ReceiveAsync(socket);
            Assert.Equal(WebSocketMessageType.Close, close.Type);
            Assert.Equal(WebSocketCloseStatus.InternalServerError, close.CloseStatus);
            Assert.Equal(reason, close.Description);
            AssertStorageEqual(damaged, await StoredAsync(host, room.RoomId, room.GameId));
            Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(room.RoomId));
            Assert.False(host.Runtime.TryGetSnapshot(room.GameId, out _));
            Assert.Equal(0, host.Runtime.GameCount);
            output.WriteLine($"Actual-created alias={room.RoomId} runtime={room.GameId} defect={defect} {operation}: reason={reason}, close=1011, no JOINED/full projection/replacement row; original credential reused.");
        }

        var healthyAlias = $"hudson-binding-healthy-{Guid.NewGuid():N}";
        await using var healthy = await PeerAsync(host, healthyAlias, room.Players[0], "changsha");
        await TakeSeatAsync(healthy, 0);
        var healthyGame = host.Manager.GetRuntimeGameIdBoundTo(healthyAlias);
        Assert.NotNull(healthyGame);
        Assert.NotEqual(room.GameId, healthyGame);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(healthyGame, room.Players[0].PlayerId));
        var afterHealthy = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Equal(damaged.GameIds.Length + 1, afterHealthy.GameIds.Length);
        Assert.Equal(damaged.BindingCount + 1, afterHealthy.BindingCount);
        Assert.Equal(damaged.Binding, afterHealthy.Binding);
        Assert.Equal(damaged.StateJson, afterHealthy.StateJson);
        Assert.Equal(damaged.StateVersion, afterHealthy.StateVersion);
        Assert.Equal(damaged.UpdatedUtc, afterHealthy.UpdatedUtc);
        output.WriteLine($"Unrelated healthy alias remained usable after {defect}; old damaged binding and snapshot unchanged.");
    }

    [Fact, Trait("Category", "RulesQualificationRecovery")]
    public async Task LostLegacyAliasBinding_IsNotGuessedFromMatchingSeedOrSignedOwner()
    {
        await using var host = new BindingBoundaryHost(persist: true, output: output);
        await using var room = await CreateProgressedRoomAsync(host);
        var before = await StoredAsync(host, room.RoomId, room.GameId);
        var faultsApplied = 0;
        using var fault = AtStoppedHost(host, db =>
        {
            Assert.Equal(1, db.AutotableRoomBindings.Where(binding => binding.RoomId == room.RoomId)
                .ExecuteDelete());
            Assert.Equal(1, db.ChangshaGames.Count(game => game.Id == Guid.Parse(room.GameId)));
            Interlocked.Increment(ref faultsApplied);
        });
        await room.DisposeAsync();
        await host.RestartAsync();
        Assert.Equal(1, faultsApplied);
        var legacy = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Null(legacy.Binding);
        Assert.Equal(0, legacy.BindingCount);
        Assert.Single(legacy.GameIds);
        Assert.Equal(before.GameIds, legacy.GameIds);
        Assert.Equal(before.BindingCount - 1, legacy.BindingCount);
        Assert.Equal(before.StateVersion, legacy.StateVersion);
        Assert.NotNull(legacy.StateJson);

        using (var ambiguous = await SocketAsync(host, room.RoomId, room.Players[0], "changsha"))
        {
            await SendAsync(ambiguous, new { type = "JOIN", gameId = room.RoomId });
            var rejected = await ReceiveAsync(ambiguous);
            Assert.Equal(WebSocketMessageType.Text, rejected.Type);
            using var document = JsonDocument.Parse(rejected.Bytes);
            Assert.Equal("UPDATE", document.RootElement.GetProperty("type").GetString());
            Assert.False(document.RootElement.GetProperty("full").GetBoolean());
            var entry = Assert.Single(Entries(document.RootElement));
            Assert.Equal("actionRejected", entry[0].GetString());
            Assert.Equal("current", entry[1].GetString());
            Assert.Equal("room", entry[2].GetProperty("action").GetString());
            Assert.Equal("legacy-room-binding-unavailable", entry[2].GetProperty("reason").GetString());
            var close = await ReceiveAsync(ambiguous);
            Assert.Equal(WebSocketMessageType.Close, close.Type);
            Assert.Equal(WebSocketCloseStatus.InternalServerError, close.CloseStatus);
            Assert.Equal("legacy-room-binding-unavailable", close.Description);
        }
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(room.RoomId));
        AssertStorageEqual(legacy, await StoredAsync(host, room.RoomId, room.GameId));

        var socket = await SocketAsync(host, room.RoomId, room.Players[0], "changsha");
        await using var owner = new WsPeer(socket, room.RoomId, room.Players[0].PlayerId, output);
        await SendAsync(socket, new { type = "NEW" });
        var joined = await ReceiveAsync(socket);
        Assert.Equal(WebSocketMessageType.Text, joined.Type);
        using (var document = JsonDocument.Parse(joined.Bytes))
        {
            Assert.Equal("JOINED", document.RootElement.GetProperty("type").GetString());
            Assert.Equal(room.RoomId, document.RootElement.GetProperty("gameId").GetString());
            Assert.Equal(room.Players[0].PlayerId, document.RootElement.GetProperty("playerId").GetString());
        }
        await owner.BarrierAsync();
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(room.RoomId));
        AssertStorageEqual(legacy, await StoredAsync(host, room.RoomId, room.GameId));
        var unbound = await owner.BarrierAsync();
        Assert.DoesNotContain(Entries(unbound), entry => entry[0].GetString() == "things"
            && entry[2].ValueKind == JsonValueKind.Object
            && entry[2].TryGetProperty("slotName", out var slot)
            && slot.ValueKind == JsonValueKind.String
            && slot.GetString()!.StartsWith("hand.", StringComparison.Ordinal));

        await TakeSeatAsync(owner, 0);
        var freshGame = host.Manager.GetRuntimeGameIdBoundTo(room.RoomId);
        Assert.NotNull(freshGame);
        Assert.NotEqual(room.GameId, freshGame);
        var fresh = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Equal(2, fresh.GameIds.Length);
        Assert.Equal(1, fresh.BindingCount);
        Assert.NotNull(fresh.Binding);
        Assert.Equal(Guid.Parse(freshGame), fresh.Binding.RuntimeGameId);
        Assert.Equal(legacy.StateJson, fresh.StateJson);
        Assert.Equal(legacy.StateVersion, fresh.StateVersion);
        Assert.Equal(legacy.UpdatedUtc, fresh.UpdatedUtc);
        output.WriteLine($"No legacy alias inference: plain JOIN with the same alias/seed/original signed owner rejected explicitly; only real NEW followed by seat creation produced distinct runtime={freshGame}, not {room.GameId}, with the old snapshot unchanged.");
    }

    [Theory, Trait("Category", "RulesQualificationRecovery")]
    [InlineData("four_player")]
    [InlineData("three_player")]
    [InlineData("bamboo")]
    [InlineData("minefield")]
    public async Task RelayVariantAfterRestart_DoesNotBindOrMutateTheOwnersPersistedChangshaGame(string variant)
    {
        await using var host = new BindingBoundaryHost(persist: true, output: output);
        await using var room = await CreateProgressedRoomAsync(host);
        var before = await StoredAsync(host, room.RoomId, room.GameId);
        await room.DisposeAsync();
        await host.RestartAsync();
        var stored = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Equal(before.GameIds, stored.GameIds);
        Assert.Equal(before.BindingCount, stored.BindingCount);
        Assert.Equal(before.Binding, stored.Binding);
        Assert.Equal(before.StateVersion, stored.StateVersion);
        var live = await SnapshotAsync(host, room.GameId);
        Assert.Equal(StableGame(room.State), StableGame(live));
        AssertInventory(live);
        var serialized = JsonSerializer.Serialize(live);
        var relayAlias = $"hudson-binding-relay-{Guid.NewGuid():N}";

        await using var alice = await PeerAsync(host, relayAlias, room.Players[0], variant);
        await using var bob = await PeerAsync(host, relayAlias, room.Players[1], variant);
        var seatMark = alice.Frames.Count;
        await TakeSeatAsync(alice, 0);
        Assert.Contains(alice.Frames.Skip(seatMark).Where(IsDelta).SelectMany(Entries),
            entry => entry[0].GetString() == "seats"
                && entry[1].GetString() == alice.PlayerId
                && entry[2].ValueKind == JsonValueKind.Object
                && entry[2].GetProperty("seat").GetInt32() == 0);
        await TakeSeatAsync(bob, 1);
        var peerMark = bob.Frames.Count;
        var payload = new { x = 17, y = 29, z = 3, marker = $"hudson-relay-{variant}" };
        await alice.UpdateAsync([new object[] { "mouse", alice.PlayerId, payload }]);
        await alice.BarrierAsync();
        await bob.BarrierAsync();
        var relayed = Assert.Single(bob.Frames.Skip(peerMark).Where(IsDelta).SelectMany(Entries),
            entry => entry[0].GetString() == "mouse" && entry[1].GetString() == alice.PlayerId);
        Assert.Equal(JsonSerializer.Serialize(payload), relayed[2].GetRawText());
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(relayAlias));
        AssertStorageEqual(stored, await StoredAsync(host, room.RoomId, room.GameId));
        Assert.Equal(serialized, JsonSerializer.Serialize(await SnapshotAsync(host, room.GameId)));
        Assert.Equal(1, host.Runtime.GameCount);
        output.WriteLine($"Relay {variant}: real origin seat echo and peer cosmetic UPDATE after restart; no Changsha alias binding/new rows/old state mutation, original signed users reused.");
    }

    private async Task<ProgressedRoom> CreateProgressedRoomAsync(BindingBoundaryHost host)
    {
        var alias = $"hudson-binding-{Guid.NewGuid():N}";
        var players = Enumerable.Range(0, 4).Select(_ => Identity(host)).ToArray();
        var peers = new List<WsPeer>();
        try
        {
            for (var seat = 0; seat < players.Length; seat++)
            {
                var peer = await PeerAsync(host, alias, players[seat], "changsha");
                peers.Add(peer);
                await TakeSeatAsync(peer, seat);
            }
            var game = host.Manager.GetRuntimeGameIdBoundTo(alias);
            Assert.NotNull(game);
            var initial = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, initial.Phase);
            Assert.Equal(0, initial.ActiveSeatIndex);
            Assert.Equal(7, initial.BaseUnit);
            Assert.All(initial.Seats, seat => Assert.False(seat.IsBot));
            AssertInventory(initial);
            var tile = initial.Hands[0].ConcealedTiles[0];
            await peers[0].UpdateAsync([new object[] { "discard", 0, new { tileId = tile } }]);
            await peers[0].BarrierAsync();
            var state = await SnapshotAsync(host, game);
            if (state.ClaimWindow is { } window)
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
                }
            }
            foreach (var peer in peers) await peer.BarrierAsync();
            state = await SnapshotAsync(host, game);
            Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
            Assert.Null(state.ClaimWindow);
            Assert.Equal(1, state.ActiveSeatIndex);
            Assert.Equal(1, state.LastDrawSeatIndex);
            Assert.True(state.TurnNumber > initial.TurnNumber);
            Assert.True(state.StateVersion > initial.StateVersion);
            Assert.Equal(initial.Wall.Skip(1), state.Wall);
            AssertInventory(state);
            var stored = await StoredAsync(host, alias, game);
            Assert.Single(stored.GameIds);
            Assert.Equal(1, stored.BindingCount);
            Assert.NotNull(stored.Binding);
            Assert.Equal(Guid.Parse(game), stored.Binding.RuntimeGameId);
            Assert.Equal(state.StateVersion, stored.StateVersion);
            output.WriteLine($"Actual-created binding room={alias} runtime={game} seed=20261652 baseUnit=7; real discard={tile}, turn={state.TurnNumber}, version={state.StateVersion}, wall={state.Wall.Count}, inventory=108.");
            return new ProgressedRoom(alias, game, players, peers, state);
        }
        catch
        {
            foreach (var peer in peers) await peer.DisposeAsync();
            throw;
        }
    }

    private static IDisposable AtStoppedHost(BindingBoundaryHost host, Action<AppDbContext> fault)
    {
        return host.RegisterStoppedFault(db =>
        {
            var ownedRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-own-turn"))
                + Path.DirectorySeparatorChar;
            Assert.StartsWith(ownedRoot, Path.GetFullPath(db.Database.GetDbConnection().DataSource));
            fault(db);
        });
    }

    [Fact, Trait("Category", "RulesQualificationRecoveryHelper")]
    public async Task FaultBoundary_AwaitsOldProviderDisposalBeforeCommittingDamage()
    {
        await using var host = new BindingBoundaryHost(persist: true, output: output);
        await using var room = await CreateProgressedRoomAsync(host);
        var oldScopes = host.Services.GetRequiredService<IServiceScopeFactory>();
        var oldLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var faultsApplied = 0;
        using var fault = AtStoppedHost(host, db =>
        {
            Assert.True(oldLifetime.ApplicationStopped.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var scope = oldScopes.CreateScope();
            });
            Assert.Equal(1, db.AutotableRoomBindings.Where(binding => binding.RoomId == room.RoomId).ExecuteDelete());
            Interlocked.Increment(ref faultsApplied);
        });
        await room.DisposeAsync();
        await host.RestartAsync();
        Assert.Equal(1, faultsApplied);
        var stored = await StoredAsync(host, room.RoomId, room.GameId);
        Assert.Null(stored.Binding);
        Assert.Equal(0, stored.BindingCount);
        Assert.Single(stored.GameIds);
    }

    [Fact, Trait("Category", "RulesQualificationRecoveryHelper")]
    public async Task FaultBoundary_PropagatesCommittedFaultExceptionWithoutStartingReplacement()
    {
        await using var host = new BindingBoundaryHost(persist: true, output: output);
        await using var room = await CreateProgressedRoomAsync(host);
        var oldScopes = host.Services.GetRequiredService<IServiceScopeFactory>();
        var expected = new InvalidOperationException("Deliberate isolated fixture failure.");
        var affected = 0;
        string dataSource;
        using (var scope = host.Services.CreateScope())
            dataSource = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection().DataSource;
        using var fault = AtStoppedHost(host, db =>
        {
            affected = db.AutotableRoomBindings.Where(binding => binding.RoomId == room.RoomId).ExecuteDelete();
            Assert.Equal(1, affected);
            throw expected;
        });
        await room.DisposeAsync();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => host.RestartAsync());
        Assert.Same(expected, actual);
        Assert.Equal(1, affected);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            using var scope = oldScopes.CreateScope();
        });
        Assert.Throws<InvalidOperationException>(() => host.Services);
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dataSource, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM AutotableRoomBindings";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private static async Task<Storage> StoredAsync(BindingBoundaryHost host, string alias, string game)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.ChangshaGames.AsNoTracking().Select(row => row.Id).ToArrayAsync();
        var binding = await db.AutotableRoomBindings.AsNoTracking().SingleOrDefaultAsync(row => row.RoomId == alias);
        var id = Guid.Parse(game);
        var state = await db.ChangshaGames.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id);
        return new Storage(ids.OrderBy(value => value).ToArray(), await db.AutotableRoomBindings.CountAsync(),
            binding is null ? null : new Binding(binding.RoomKey, binding.RoomId, binding.RuntimeGameId, binding.CreatedUtc),
            state?.StateJson, state?.StateVersion, state?.UpdatedUtc);
    }

    private static void AssertStorageEqual(Storage expected, Storage actual)
    {
        Assert.Equal(expected.GameIds, actual.GameIds);
        Assert.Equal(expected.BindingCount, actual.BindingCount);
        Assert.Equal(expected.Binding, actual.Binding);
        Assert.Equal(expected.StateJson, actual.StateJson);
        Assert.Equal(expected.StateVersion, actual.StateVersion);
        Assert.Equal(expected.UpdatedUtc, actual.UpdatedUtc);
    }

    private static string StableGame(ChangshaGameState state) => JsonSerializer.Serialize(new
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

    private static SignedPlayer Identity(BindingBoundaryHost host)
    {
        var id = $"hudson-binding-player-{Guid.NewGuid():N}";
        return new SignedPlayer(id, host.Services.GetRequiredService<PlayerIdentityService>().Protect(id));
    }

    private static async Task<WebSocket> SocketAsync(
        BindingBoundaryHost host, string alias, SignedPlayer player, string variant)
    {
        var server = Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());
        var client = server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers["Cookie"] = $"{PlayerIdentityService.CookieName}={player.Credential}";
        var query = $"autotable/ws?variant={Uri.EscapeDataString(variant)}&gameId={Uri.EscapeDataString(alias)}"
            + "&bots=false&botCount=0&dealMode=auto&handCount=4&seed=20261652&baseUnit=7";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await client.ConnectAsync(new Uri(server.BaseAddress, query), timeout.Token);
    }

    private async Task<WsPeer> PeerAsync(
        BindingBoundaryHost host, string alias, SignedPlayer player, string variant)
    {
        var peer = new WsPeer(await SocketAsync(host, alias, player, variant), alias, player.PlayerId, output);
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

    private static async Task<ChangshaGameState> SnapshotAsync(BindingBoundaryHost host, string game) =>
        await host.Runtime.TryGetSnapshotCopyAsync(game)
        ?? throw new InvalidOperationException($"Expected actual runtime {game}.");

    private static IEnumerable<JsonElement> Entries(JsonElement frame) =>
        frame.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static bool IsDelta(JsonElement frame) => frame.GetProperty("type").GetString() == "UPDATE"
        && frame.TryGetProperty("full", out var full) && !full.GetBoolean();

    private static async Task SendAsync(WebSocket socket, object value)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<Received> ReceiveAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        using var bytes = new MemoryStream();
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            if (part.MessageType == WebSocketMessageType.Close)
                return new Received(part.MessageType, [], part.CloseStatus, part.CloseStatusDescription);
            Assert.Equal(WebSocketMessageType.Text, part.MessageType);
            bytes.Write(buffer, 0, part.Count);
            Assert.True(bytes.Length <= 4096, "Unexpected large frame before the bounded room failure.");
        } while (!part.EndOfMessage);
        return new Received(part.MessageType, bytes.ToArray(), null, null);
    }

    private sealed record SignedPlayer(string PlayerId, string Credential);
    private sealed record Binding(string RoomKey, string RoomId, Guid RuntimeGameId, DateTime CreatedUtc);
    private sealed record Storage(Guid[] GameIds, int BindingCount, Binding? Binding,
        string? StateJson, int? StateVersion, DateTime? UpdatedUtc);
    private sealed record Received(WebSocketMessageType Type, byte[] Bytes,
        WebSocketCloseStatus? CloseStatus, string? Description);

    private sealed record ProgressedRoom(string RoomId, string GameId, SignedPlayer[] Players,
        List<WsPeer> Peers, ChangshaGameState State) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var peer in Peers) await peer.DisposeAsync();
        }
    }

    private sealed class BindingBoundaryHost : IAsyncDisposable
    {
        private readonly string _database;
        private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        private readonly bool _persist;
        private readonly ITestOutputHelper? _output;
        private WebApplicationFactory<Program>? _factory;
        private Action<AppDbContext>? _pendingFault;
        private int _restarting;

        public BindingBoundaryHost(bool persist, ITestOutputHelper? output)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data", "hudson-own-turn");
            Directory.CreateDirectory(directory);
            _database = Path.Combine(directory, $"{Guid.NewGuid():N}.db");
            _persist = persist;
            _output = output;
            _factory = CreateFactory();
        }

        public IServiceProvider Services =>
            (_factory ?? throw new InvalidOperationException("No running binding-test host.")).Services;
        public IChangshaGameRuntime Runtime => Services.GetRequiredService<IChangshaGameRuntime>();
        public AutotableConnectionManager Manager => Services.GetRequiredService<AutotableConnectionManager>();

        private WebApplicationFactory<Program> CreateFactory() =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Persistence:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_database};Pooling=False");
                builder.UseSetting("Authentication:JwtSigningKeys:0", _signingKey);
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services => services.Configure<ChangshaRuntimeOptions>(options =>
                {
                    options.PersistSnapshots = _persist;
                    options.BotTurnDelayMs = 30_000;
                    options.BotClaimDelayMs = 30_000;
                    options.BotPickupDelayMs = 30_000;
                    options.ClaimWindowTimeoutMs = 30_000;
                    options.DealBatchDelayMs = 0;
                }));
            });

        public IDisposable RegisterStoppedFault(Action<AppDbContext> fault)
        {
            if (_factory is null || _pendingFault is not null || Volatile.Read(ref _restarting) != 0)
                throw new InvalidOperationException("A binding-test fault requires one running host and one registration.");
            _pendingFault = fault;
            return new FaultRegistration(this, fault);
        }

        public async Task RestartAsync()
        {
            if (Interlocked.Exchange(ref _restarting, 1) != 0)
                throw new InvalidOperationException("Concurrent binding-test restarts are not permitted.");
            try
            {
                var oldFactory = _factory ?? throw new InvalidOperationException("No running binding-test host.");
                var fault = _pendingFault;
                _pendingFault = null;
                await oldFactory.DisposeAsync();
                _factory = null;
                _output?.WriteLine("Binding lifecycle: old factory and services fully disposed.");

                if (fault is not null)
                    await ApplyStoppedFaultAsync(fault);

                _factory = CreateFactory();
                _ = _factory.Server;
                _output?.WriteLine("Binding lifecycle: replacement factory started after completed fault.");
            }
            finally
            {
                Volatile.Write(ref _restarting, 0);
            }
        }

        private async Task ApplyStoppedFaultAsync(Action<AppDbContext> fault)
        {
            var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
                .UseSqlite($"Data Source={_database};Pooling=False").Options;
            await using (var db = new SqliteAppDbContext(options))
            {
                await db.Database.OpenConnectionAsync();
                // Hold the isolated store exclusively while the original synchronous
                // ExecuteUpdate/ExecuteDelete faults commit in their normal autocommit mode.
                await db.Database.ExecuteSqlRawAsync("PRAGMA locking_mode = EXCLUSIVE");
                await db.Database.ExecuteSqlRawAsync("BEGIN EXCLUSIVE");
                await db.Database.ExecuteSqlRawAsync("COMMIT");
                Assert.Null(db.Database.CurrentTransaction);
                fault(db);
                Assert.Null(db.Database.CurrentTransaction);
            }
            _output?.WriteLine("Binding lifecycle: isolated fault returned and its database connection disposed.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }
            foreach (var file in new[] { _database, _database + "-wal", _database + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }

        private sealed class FaultRegistration(BindingBoundaryHost host, Action<AppDbContext> fault) : IDisposable
        {
            public void Dispose()
            {
                if (ReferenceEquals(host._pendingFault, fault))
                    host._pendingFault = null;
            }
        }
    }
}
