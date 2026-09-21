using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

[Trait("Category", "RulesQualificationRecovery")]
public sealed class DrakeCorruptPublicRoomRecoveryTests
{
    private const string Canary = "drake-private-snapshot-canary-not-for-logs";
    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEnumerable<object[]> CorruptCases()
    {
        string[] defects =
        [
            "seats-null", "seat-null", "seat-count", "seat-index",
            "hands-null", "hand-null", "hand-count", "hand-index",
            "concealed-null", "melds-null", "meld-null", "meld-tiles-null",
            "wall-null", "discards-null", "discard-null", "missed-win-null",
            "scores-null", "penalties-null", "penalty-null", "penalty-payments-null",
            "events-null", "event-null", "opportunities-null", "opportunity-null",
            "missing-claim-window", "score-payments-null", "score-payment-null",
            "score-fans-null", "win-patterns-null", "win-keys-null",
            "hand-too-large", "too-many-melds", "invalid-meld-count",
            "invalid-actor", "invalid-dealer", "invalid-pickup", "invalid-last-draw",
            "invalid-claim-seat", "invalid-phase", "invalid-mode", "invalid-tile",
            "invalid-score-seat", "invalid-meld-seat", "invalid-discard-seat"
        ];
        foreach (var defect in defects)
        {
            yield return [defect, true];
            yield return [defect, false];
        }
    }

    [Theory]
    [MemberData(nameof(CorruptCases))]
    public async Task MalformedEssentialState_IsIsolatedBeforePublication_AndKnownAliasRejects(
        string defect, bool duringStartup)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var good = await OpenManualRoomAsync(host);
        var goodBefore = await host.SnapshotAsync(good);
        var bad = await InsertSnapshotAsync(host, goodBefore, json => Corrupt(json, defect));
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        if (duringStartup)
        {
            await good.Peer.DisposeAsync();
            await host.RestartAsync();
        }

        using var client = Server(host).CreateClient();
        using var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True(host.Runtime.TryGetSnapshot(good.GameId, out var restoredGood));
        Assert.NotNull(restoredGood);
        Assert.Equal(ChangshaPhase.RollingDice, restoredGood.Phase);
        Assert.Equal(DealMode.Manual, restoredGood.DealMode);
        Assert.Equal(goodBefore.StateVersion, restoredGood.StateVersion);
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        Assert.Equal(1, host.Runtime.GameCount);

        var failure = await Assert.ThrowsAsync<PublicRoomRecoveryException>(() =>
            host.Runtime.RestorePublicRoomAsync(bad.Room));
        Assert.Equal("room-snapshot-invalid", failure.Reason);
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));

        var logs = new RecoveryLogs();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        await AssertBoundedRejectionAsync(host, bad.Room, "JOIN");
        await AssertBoundedRejectionAsync(host, bad.Room, "NEW");
        Assert.Contains(logs.Messages, text => text.Contains("room-snapshot-invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, text => text.Contains(Canary, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, text => text.Contains("\"concealedTiles\"", StringComparison.Ordinal));
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(bad.Room));
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        Assert.Equal(1, host.Runtime.GameCount);
        await AssertStoredAsync(host, bad, bad.CorruptJson, expectedRows: 2);

        await using var owner = await host.ConnectAsync(good.RoomId, good.Peer.PlayerId);
        Assert.Equal(good.GameId, host.Manager.GetRuntimeGameIdBoundTo(good.RoomId));
        await owner.UpdateAsync([new object[] { "seats", owner.PlayerId, new { seat = 0 } }]);
        await owner.BarrierAsync();
        await owner.UpdateAsync([new object[] { "pickup", "rollDice", new { seatIndex = 0 } }]);
        await owner.BarrierAsync();
        var progressed = await host.Runtime.TryGetSnapshotCopyAsync(good.GameId);
        Assert.NotNull(progressed);
        Assert.Equal(ChangshaPhase.BreakPointMarked, progressed.Phase);
        Assert.Equal(0, progressed.PickupSeatIndex);
        Assert.True(progressed.StateVersion > goodBefore.StateVersion);
        await AssertStoredAsync(host, bad, bad.CorruptJson, expectedRows: 2);
    }

    [Theory]
    [InlineData("seating")]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("terminal")]
    [InlineData("legacy-terminal")]
    [InlineData("legacy-defaults")]
    public async Task ValidSnapshotShapesAndLegacyDefaults_RetainTheirExactBinding(string profile)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        _ = host.Services;
        var (state, _) = ChangshaGameStateMachine.CreateGame(42, []);
        if (profile != "seating")
            ChangshaGameStateMachine.StartGame(state);
        if (profile is "auto" or "terminal" or "legacy-terminal")
        {
            ChangshaGameStateMachine.RollDice(state, new DiceService(42));
            ChangshaGameStateMachine.Deal(state);
        }
        var row = await InsertSnapshotAsync(host, state, json =>
        {
            if (profile is "manual" or "legacy-defaults")
                json["dealMode"] = (int)DealMode.Manual;
            if (profile is "terminal" or "legacy-terminal")
            {
                json["phase"] = profile == "terminal" ? (int)ChangshaPhase.GameComplete : 18;
                json["isGameComplete"] = true;
            }
            if (profile == "legacy-defaults")
            {
                foreach (var key in new[] { "baseUnit", "botDifficulty", "lastDrawSeatIndex", "eventLog", "missedWinSeats", "falseHuPenalties" })
                    json.Remove(key);
            }
        });
        await host.RestartAsync();
        var terminal = profile is "terminal" or "legacy-terminal";
        Assert.Equal(!terminal, host.Runtime.TryGetSnapshot(row.Id.ToString(), out _));
        Assert.Equal(row.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(row.Room));
        var recovered = await host.Runtime.TryGetSnapshotCopyAsync(row.Id.ToString());
        Assert.NotNull(recovered);
        Assert.Equal(row.Id.ToString(), recovered.GameId);
        Assert.Equal(4, recovered.Seats.Count);
        Assert.Equal(4, recovered.Hands.Count);
        Assert.Equal(1, recovered.BaseUnit);
        Assert.Equal(profile is "manual" or "legacy-defaults" ? DealMode.Manual : DealMode.Auto, recovered.DealMode);
        Assert.Equal(terminal, recovered.IsGameComplete);
        if (profile == "legacy-defaults")
        {
            Assert.Null(recovered.BotDifficulty);
            Assert.Null(recovered.LastDrawSeatIndex);
            Assert.Empty(recovered.EventLog);
            Assert.Empty(recovered.MissedWinSeats);
            Assert.Empty(recovered.FalseHuPenalties);
        }
        await AssertStoredAsync(host, row, row.CorruptJson, expectedRows: 1);
    }

    [Fact]
    public async Task CorrectedStoredSnapshotAndOrdinaryNewRoom_SurviveAnotherRestartWithoutRedeal()
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var good = await OpenManualRoomAsync(host);
        var saved = await host.SnapshotAsync(good);
        var bad = await InsertSnapshotAsync(host, saved, json => Corrupt(json, "seats-null"));
        await good.Peer.DisposeAsync();
        await host.RestartAsync();
        await AssertBoundedRejectionAsync(host, bad.Room, "JOIN");
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.ChangshaGames.SingleAsync(game => game.Id == bad.Id);
            row.StateJson = bad.ValidJson;
            await db.SaveChangesAsync();
        }

        await using (var owner = await host.ConnectAsync(bad.Room, good.Peer.PlayerId))
        {
            var joined = Assert.Single(owner.Frames.Where(frame => frame.GetProperty("type").GetString() == "JOINED").Take(1));
            Assert.False(joined.GetProperty("isFirst").GetBoolean());
            Assert.Equal(bad.Id.ToString(), host.Manager.GetRuntimeGameIdBoundTo(bad.Room));
            var recovered = await host.Runtime.TryGetSnapshotCopyAsync(bad.Id.ToString());
            Assert.NotNull(recovered);
            Assert.Equal(saved.StateVersion, recovered.StateVersion);
            Assert.Equal(saved.Phase, recovered.Phase);
            Assert.Equal(saved.BaseUnit, recovered.BaseUnit);
            await AssertStoredAsync(host, bad, bad.ValidJson, expectedRows: 2);
        }

        var newRoom = $"drake-recovery-new-{Guid.NewGuid():N}";
        var newPlayer = $"drake-recovery-owner-{Guid.NewGuid():N}";
        using (var socket = await host.OpenSocketAsync(
            $"variant=changsha&gameId={newRoom}&bots=false&botCount=0&dealMode=manual&seed=42", newPlayer))
        {
            await SendAsync(socket, new { type = "NEW" });
            var joined = await ReceiveJsonAsync(socket);
            Assert.Equal("JOINED", joined.GetProperty("type").GetString());
            Assert.True(joined.GetProperty("isFirst").GetBoolean());
            await using var peer = new HudsonOwnTurnWsFixture.WsPeer(socket, newRoom, newPlayer);
            await peer.BarrierAsync();
            await peer.UpdateAsync([new object[] { "seats", newPlayer, new { seat = 0 } }]);
            await peer.BarrierAsync();
        }
        var newId = host.Manager.GetRuntimeGameIdBoundTo(newRoom);
        Assert.NotNull(newId);
        Assert.NotEqual(bad.Id.ToString(), newId);
        Assert.NotEqual(good.GameId, newId);
        await host.RestartAsync();
        Assert.True(host.Runtime.TryGetSnapshot(good.GameId, out _));
        Assert.True(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        Assert.True(host.Runtime.TryGetSnapshot(newId, out _));
        await using var ordinaryOwner = await host.ConnectAsync(newRoom, newPlayer);
        Assert.Equal(newId, host.Manager.GetRuntimeGameIdBoundTo(newRoom));
        await AssertStoredAsync(host, bad, bad.ValidJson, expectedRows: 3);
    }

    private static async Task<HudsonOwnTurnWsFixture.HumanTable> OpenManualRoomAsync(HudsonOwnTurnWsFixture host)
    {
        var table = await host.OpenHumanTableAsync(extraQuery: "&baseUnit=7");
        await host.Runtime.FillEmptySeatsWithBotsAsync(table.GameId);
        await host.Runtime.StartGameAsync(table.GameId);
        var snapshot = await host.SnapshotAsync(table);
        Assert.Equal(ChangshaPhase.RollingDice, snapshot.Phase);
        Assert.Equal(DealMode.Manual, snapshot.DealMode);
        Assert.Equal(0, snapshot.DealerSeatIndex);
        Assert.False(snapshot.Seats[0].IsBot);
        return table;
    }

    private sealed record StoredSnapshot(Guid Id, string Room, string Key, string ValidJson, string CorruptJson);

    private static async Task<StoredSnapshot> InsertSnapshotAsync(
        HudsonOwnTurnWsFixture host, ChangshaGameState valid, Action<JsonObject> mutate)
    {
        var id = Guid.NewGuid();
        var room = $"drake-corrupt-{Guid.NewGuid():N}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(room))).ToLowerInvariant();
        var json = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(valid, SnapshotJson));
        json["gameId"] = id.ToString();
        var validJson = json.ToJsonString();
        mutate(json);
        var corruptJson = json.ToJsonString();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangshaGames.Add(new ChangshaGame
        {
            Id = id, RuleSet = "changsha-v1", Seed = valid.Seed,
            StateJson = corruptJson, StateVersion = valid.StateVersion,
            CurrentHandNumber = valid.HandNumber, CurrentRoundNumber = valid.RoundNumber,
            CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow
        });
        db.AutotableRoomBindings.Add(new AutotableRoomBinding
        {
            RoomKey = key, RoomId = room, RuntimeGameId = id, CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new StoredSnapshot(id, room, key, validJson, corruptJson);
    }

    private static async Task AssertStoredAsync(
        HudsonOwnTurnWsFixture host, StoredSnapshot expected, string json, int expectedRows)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(expectedRows, await db.ChangshaGames.CountAsync());
        Assert.Equal(expectedRows, await db.AutotableRoomBindings.CountAsync());
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == expected.Id);
        Assert.True(string.Equals(json, row.StateJson, StringComparison.Ordinal), "The stored snapshot must not be rewritten or replaced.");
        var binding = await db.AutotableRoomBindings.AsNoTracking().SingleAsync(item => item.RoomKey == expected.Key);
        Assert.Equal(expected.Id, binding.RuntimeGameId);
        Assert.Equal(expected.Room, binding.RoomId);
    }

    private static void Corrupt(JsonObject state, string defect)
    {
        state["publicName"] = Canary;
        JsonObject Hand() => state["hands"]!.AsArray()[0]!.AsObject();
        JsonObject Meld() => new() { ["kind"] = (int)MeldKind.Pung, ["tileIds"] = new JsonArray(0, 1, 2) };
        JsonObject Payment() => new() { ["fromSeatIndex"] = 1, ["toSeatIndex"] = 0, ["amount"] = 1, ["reason"] = "fixture" };
        JsonObject Score() => new()
        {
            ["category"] = (int)ScoreCategory.SmallWin, ["basePoints"] = 1,
            ["payments"] = new JsonArray(Payment()), ["fans"] = new JsonArray()
        };
        JsonObject Claim() => new()
        {
            ["discardSeatIndex"] = 0, ["discardTileId"] = 0,
            ["opportunities"] = new JsonArray(new JsonObject
            {
                ["seatIndex"] = 1, ["claimType"] = (int)TableClaimType.Pung, ["priority"] = 1
            })
        };
        JsonObject Win() => new()
        {
            ["winningSeatIndex"] = 0, ["method"] = (int)WinMethod.SelfDraw,
            ["pattern"] = (int)WinPattern.Standard, ["winningTileId"] = 0, ["sourceSeatIndex"] = 0,
            ["allPatterns"] = new JsonArray(), ["patternKeys"] = new JsonArray()
        };
        switch (defect)
        {
            case "seats-null": state["seats"] = null; break;
            case "seat-null": state["seats"]!.AsArray()[0] = null; break;
            case "seat-count": state["seats"]!.AsArray().RemoveAt(3); break;
            case "seat-index": state["seats"]!.AsArray()[1]!["seatIndex"] = 0; break;
            case "hands-null": state["hands"] = null; break;
            case "hand-null": state["hands"]!.AsArray()[0] = null; break;
            case "hand-count": state["hands"]!.AsArray().RemoveAt(3); break;
            case "hand-index": Hand()["seatIndex"] = 2; break;
            case "concealed-null": Hand()["concealedTiles"] = null; break;
            case "melds-null": Hand()["melds"] = null; break;
            case "meld-null": Hand()["melds"] = new JsonArray((JsonNode?)null); break;
            case "meld-tiles-null":
                var meld = Meld(); meld["tileIds"] = null; Hand()["melds"] = new JsonArray(meld); break;
            case "wall-null": state["wall"] = null; break;
            case "discards-null": state["discardPile"] = null; break;
            case "discard-null": state["discardPile"] = new JsonArray((JsonNode?)null); break;
            case "missed-win-null": state["missedWinSeats"] = null; break;
            case "scores-null": state["cumulativeScores"] = null; break;
            case "penalties-null": state["falseHuPenalties"] = null; break;
            case "penalty-null": state["falseHuPenalties"] = new JsonArray((JsonNode?)null); break;
            case "penalty-payments-null":
                state["falseHuPenalties"] = new JsonArray(new JsonObject
                {
                    ["offendingSeatIndex"] = 0, ["penaltyPerOpponent"] = 1, ["payments"] = null
                });
                break;
            case "events-null": state["eventLog"] = null; break;
            case "event-null": state["eventLog"] = new JsonArray((JsonNode?)null); break;
            case "opportunities-null":
                var claim = Claim(); claim["opportunities"] = null; state["claimWindow"] = claim; break;
            case "opportunity-null":
                var nullOpportunity = Claim(); nullOpportunity["opportunities"] = new JsonArray((JsonNode?)null); state["claimWindow"] = nullOpportunity; break;
            case "missing-claim-window": state["phase"] = (int)ChangshaPhase.AwaitingClaim; state["claimWindow"] = null; break;
            case "score-payments-null":
                var score = Score(); score["payments"] = null; state["currentScore"] = score; break;
            case "score-payment-null":
                var nullPayment = Score(); nullPayment["payments"] = new JsonArray((JsonNode?)null); state["currentScore"] = nullPayment; break;
            case "score-fans-null":
                var nullFans = Score(); nullFans["fans"] = null; state["currentScore"] = nullFans; break;
            case "win-patterns-null":
                var win = Win(); win["allPatterns"] = null; state["currentWin"] = win; break;
            case "win-keys-null":
                var nullKeys = Win(); nullKeys["patternKeys"] = null; state["currentWin"] = nullKeys; break;
            case "hand-too-large": Hand()["concealedTiles"] = new JsonArray(Enumerable.Range(0, 15).Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()); break;
            case "too-many-melds": Hand()["melds"] = new JsonArray(Enumerable.Range(0, 5).Select(_ => (JsonNode?)Meld()).ToArray()); break;
            case "invalid-meld-count":
                var shortMeld = Meld(); shortMeld["tileIds"] = new JsonArray(0, 1); Hand()["melds"] = new JsonArray(shortMeld); break;
            case "invalid-actor": state["activeSeatIndex"] = 4; break;
            case "invalid-dealer": state["dealerSeatIndex"] = -1; break;
            case "invalid-pickup": state["pickupSeatIndex"] = 4; break;
            case "invalid-last-draw": state["lastDrawSeatIndex"] = 4; break;
            case "invalid-claim-seat":
                var badSeat = Claim(); badSeat["opportunities"]!.AsArray()[0]!["seatIndex"] = 4; state["claimWindow"] = badSeat; break;
            case "invalid-phase": state["phase"] = 99; break;
            case "invalid-mode": state["dealMode"] = 99; break;
            case "invalid-tile": state["wall"] = new JsonArray(108); break;
            case "invalid-score-seat": state["cumulativeScores"]!["4"] = 0; break;
            case "invalid-meld-seat":
                var badMeld = Meld(); badMeld["claimedFromSeatIndex"] = 4; Hand()["melds"] = new JsonArray(badMeld); break;
            case "invalid-discard-seat":
                state["discardPile"] = new JsonArray(new JsonObject { ["seatIndex"] = 4, ["tileId"] = 0, ["turnNumber"] = 1 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
    }

    private static TestServer Server(HudsonOwnTurnWsFixture host) =>
        Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());

    private static async Task SendAsync(WebSocket socket, object message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        using var bytes = new MemoryStream();
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, part.MessageType);
            bytes.Write(buffer, 0, part.Count);
            Assert.True(bytes.Length <= 4096, "Unexpected large frame on bounded room-failure path.");
        } while (!part.EndOfMessage);
        using var document = JsonDocument.Parse(bytes.ToArray());
        return document.RootElement.Clone();
    }

    private static async Task AssertBoundedRejectionAsync(HudsonOwnTurnWsFixture host, string room, string operation)
    {
        using var socket = await host.OpenSocketAsync(
            $"variant=changsha&gameId={room}&bots=false&botCount=0&dealMode=manual&seed=42");
        await SendAsync(socket, new { type = operation, gameId = room });
        var rejection = await ReceiveJsonAsync(socket);
        Assert.Equal("UPDATE", rejection.GetProperty("type").GetString());
        Assert.False(rejection.GetProperty("full").GetBoolean());
        var entry = Assert.Single(rejection.GetProperty("entries").EnumerateArray());
        Assert.Equal("actionRejected", entry[0].GetString());
        Assert.Equal("current", entry[1].GetString());
        Assert.Equal("room", entry[2].GetProperty("action").GetString());
        var reason = entry[2].GetProperty("reason").GetString();
        Assert.Equal("room-snapshot-invalid", reason);
        Assert.True(reason!.Length <= 64);
        Assert.DoesNotContain(Canary, rejection.GetRawText());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[4096]), timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal(WebSocketCloseStatus.InternalServerError, close.CloseStatus);
        Assert.Equal("room-snapshot-invalid", close.CloseStatusDescription);
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
