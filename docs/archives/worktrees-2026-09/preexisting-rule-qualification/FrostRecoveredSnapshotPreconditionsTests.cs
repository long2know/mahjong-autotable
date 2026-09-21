using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Changsha.Scoring;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

[Trait("Category", "RulesQualificationRecovery")]
public sealed class FrostRecoveredSnapshotPreconditionsTests(ITestOutputHelper output)
{
    private const string Canary = "frost-private-recovery-snapshot-not-for-logs";
    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEnumerable<object[]> InvalidInputs()
    {
        string[] defects =
        [
            "fan-null", "fan-unknown", "fan-negative",
            "robbery-declarer-null", "robbery-declarer-foreign", "robbery-pung-missing",
            "robbery-tile-missing", "robbery-effective-twelve",
            "pickup-negative", "pickup-start-offset", "pickup-round-past-end",
            "pickup-round-negative", "pickup-round-overflow", "pickup-single-negative",
            "pickup-extra-offset", "pickup-seat-mismatch", "pickup-wall-short",
            "pickup-hand-overflow", "pickup-extra-short",
            "discard-effective-twelve", "discard-meld-effective-twelve",
            "discard-effective-fifteen", "discard-open-window", "claim-next-effective-twelve"
        ];
        foreach (var defect in defects)
        {
            yield return [defect, true];
            yield return [defect, false];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task InvalidPrerequisite_IsIsolatedBeforeAllocation_AndRepeatedJoinAndNewReject(
        string defect, bool duringStartup)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        await using var good = await host.OpenHumanTableAsync();
        await host.Runtime.FillEmptySeatsWithBotsAsync(good.GameId);
        await host.Runtime.StartGameAsync(good.GameId);
        var goodBefore = await host.SnapshotAsync(good);
        Assert.Equal(ChangshaPhase.RollingDice, goodBefore.Phase);

        var (valid, path, replacement) = InvalidInput(defect);
        Assert.NotEmpty(ChangshaToAutotableTranslator.Translate(valid, 0, valid.Seats[0].PlayerId));
        var bad = await StoreAsync(host, valid, json => ReplaceOneProperty(json, path, replacement));
        if (duringStartup)
        {
            await good.Peer.DisposeAsync();
            await host.RestartAsync();
        }
        using var client = Server(host).CreateClient();
        using var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var failure = await Record.ExceptionAsync(() => host.Runtime.RestorePublicRoomAsync(bad.Room));
        if (failure is null)
            await DescribeRejectedBaselineAdmissionAsync(host, bad, defect);
        var recovery = Assert.IsType<PublicRoomRecoveryException>(failure);
        Assert.Equal("room-snapshot-invalid", recovery.Reason);
        Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
        Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(bad.Room));
        Assert.Equal(1, host.Runtime.GameCount);
        Assert.True(host.Runtime.TryGetSnapshot(good.GameId, out var restoredGood));
        Assert.NotNull(restoredGood);
        Assert.Equal(goodBefore.StateVersion, restoredGood.StateVersion);
        Assert.Equal(goodBefore.Phase, restoredGood.Phase);
        await AssertStoredAsync(host, bad, 2);

        var logs = new RecoveryLogs();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        foreach (var operation in new[] { "JOIN", "NEW", "JOIN" })
        {
            await AssertRejectedSocketAsync(host, bad.Room, operation);
            var again = await Assert.ThrowsAsync<PublicRoomRecoveryException>(() =>
                host.Runtime.RestorePublicRoomAsync(bad.Room));
            Assert.Equal("room-snapshot-invalid", again.Reason);
            Assert.False(host.Runtime.TryGetSnapshot(bad.Id.ToString(), out _));
            Assert.Null(host.Manager.GetRuntimeGameIdBoundTo(bad.Room));
            Assert.Equal(1, host.Runtime.GameCount);
            await AssertStoredAsync(host, bad, 2);
        }
        Assert.Contains(logs.Messages, message => message.Contains("room-snapshot-invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(Canary, StringComparison.Ordinal)
            || message.Contains("\"concealedTiles\"", StringComparison.Ordinal)
            || message.Contains("\"wall\"", StringComparison.Ordinal)
            || message.Contains(bad.Json, StringComparison.Ordinal));

        await using var owner = await host.ConnectAsync(good.RoomId, good.Peer.PlayerId);
        Assert.Equal(good.GameId, host.Manager.GetRuntimeGameIdBoundTo(good.RoomId));
        await owner.UpdateAsync([new object[] { "seats", owner.PlayerId, new { seat = 0 } }]);
        await owner.BarrierAsync();
        await owner.UpdateAsync([new object[] { "pickup", "rollDice", new { seatIndex = 0 } }]);
        await owner.BarrierAsync();
        var progressed = await host.Runtime.TryGetSnapshotCopyAsync(good.GameId);
        Assert.NotNull(progressed);
        Assert.Equal(ChangshaPhase.BreakPointMarked, progressed.Phase);
        Assert.True(progressed.StateVersion > goodBefore.StateVersion);
        await AssertStoredAsync(host, bad, 2);
    }

    [Theory]
    [InlineData("engine")]
    [InlineData("empty")]
    [InlineData("omitted")]
    [InlineData("all-catalog")]
    public async Task RealScoredHand_ProjectsEmptyAndLegacyCatalogFans_AndResumes(string profile)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var state = ScoredState();
        Assert.NotEmpty(state.CurrentScore!.Fans);
        var saved = await StoreAsync(host, state, json =>
        {
            var score = json["currentScore"]!.AsObject();
            if (profile == "empty") score["fans"] = new JsonArray();
            if (profile == "omitted") score.Remove("fans");
            if (profile == "all-catalog")
                score["fans"] = JsonSerializer.SerializeToNode(FanCatalog.Entries.Select(entry =>
                    new DetectedFan(entry.Key, entry.Value.Points)).ToArray(), SnapshotJson);
        });
        await host.RestartAsync();
        Assert.True(host.Runtime.TryGetSnapshot(saved.Id.ToString(), out _));
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        var recovered = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.EndHand, recovered.Phase);
        Assert.Equal(state.StateVersion, recovered.StateVersion);
        Assert.Equal(state.CumulativeScores, recovered.CumulativeScores);
        var projected = Assert.IsType<HandResultEntry>(Assert.Single(
            ChangshaToAutotableTranslator.Translate(recovered).Where(entry => entry.Kind == "result")).Value);
        Assert.Equal("Hu", projected.Type);
        Assert.NotNull(projected.ScoreResult);
        var expected = profile switch
        {
            "empty" or "omitted" => Array.Empty<DetectedFan>(),
            "all-catalog" => FanCatalog.Entries.Select(entry => new DetectedFan(entry.Key, entry.Value.Points)).ToArray(),
            _ => state.CurrentScore.Fans.ToArray()
        };
        Assert.Equal(expected.Select(fan => ChangshaGameStateMachine.FanWireName(fan.Fan)),
            projected.ScoreResult.Fans.Select(fan => fan.Fan));
        Assert.Equal(expected.Select(fan => fan.Points), projected.ScoreResult.Fans.Select(fan => fan.Points));
        await AssertStoredAsync(host, saved, 1);
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        recovered = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.GameComplete, recovered.Phase);
        Assert.True(recovered.IsGameComplete);
        Assert.Equal(state.CumulativeScores, recovered.CumulativeScores);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRobberyWindow_RestoresAndAllPassCompletesTheOriginalAddedKong(bool duringStartup)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var original = ClaimState(robbing: true);
        var replacement = original.Wall[^1];
        var saved = await StoreAsync(host, original);
        if (duringStartup) await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        var recovered = await SnapshotAsync(host, saved);
        Assert.Equal(original.StateVersion, recovered.StateVersion);
        Assert.Equal(0, recovered.ClaimWindow!.KongDeclarerSeatIndex);
        Assert.Equal(MeldKind.Pung, Assert.Single(recovered.Hands[0].Melds).Kind);
        Assert.Contains(19, recovered.Hands[0].ConcealedTiles);
        await AssertStoredAsync(host, saved, 1);
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        foreach (var seat in original.ClaimWindow!.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct())
            await host.Runtime.PassAsync(saved.Id.ToString(), seat);
        recovered = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, recovered.Phase);
        Assert.Equal(0, recovered.ActiveSeatIndex);
        Assert.Null(recovered.ClaimWindow);
        var kong = Assert.Single(recovered.Hands[0].Melds);
        Assert.Equal(MeldKind.AddedKong, kong.Kind);
        Assert.Equal(new[] { 16, 17, 18, 19 }, kong.TileIds);
        Assert.Equal(replacement, recovered.Hands[0].ConcealedTiles[^1]);
        Assert.Equal(14, Effective(recovered.Hands[0]));
        Assert.Equal(original.Wall.Count - 1, recovered.Wall.Count);
        Assert.True(recovered.LastDrawWasKongReplacement);
        Assert.Equal(0, recovered.LastDrawSeatIndex);
        HudsonOwnTurnWsFixture.AssertInventory(recovered);
    }

    [Fact]
    public async Task OrdinaryClaim_WithLegacyNullDeclarer_ResumesPassThenExactlyOneDraw()
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var original = ClaimState(robbing: false);
        Assert.False(original.ClaimWindow!.IsKongRobbing);
        Assert.Null(original.ClaimWindow.KongDeclarerSeatIndex);
        var saved = await StoreAsync(host, original, json =>
        {
            json["claimWindow"]!.AsObject().Remove("kongDeclarerSeatIndex");
            json["claimWindow"]!.AsObject().Remove("openedAtUnixMs");
        });
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        foreach (var seat in original.ClaimWindow.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct())
            await host.Runtime.PassAsync(saved.Id.ToString(), seat);
        var recovered = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, recovered.Phase);
        Assert.Equal(1, recovered.ActiveSeatIndex);
        Assert.Equal(1, recovered.LastDrawSeatIndex);
        Assert.Equal(14, Effective(recovered.Hands[1]));
        Assert.Equal(original.Wall.Count - 1, recovered.Wall.Count);
        Assert.Null(recovered.ClaimWindow);
        HudsonOwnTurnWsFixture.AssertInventory(recovered);
    }

    [Theory]
    [InlineData("pending", 13, true)]
    [InlineData("ready", 14, false)]
    [InlineData("two-chows-pending", 13, true)]
    [InlineData("two-chows-ready", 14, false)]
    [InlineData("kong-ready", 14, false)]
    public async Task ValidEffectiveCounts_ResumeWithoutDoubleDraw_AndCountKongAsOneGroup(
        string profile, int effective, bool draws)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var state = PredrawState(profile.StartsWith("two-chows", StringComparison.Ordinal) ? "two-chows"
            : profile == "kong-ready" ? "concealed-kong" : "hu");
        if (!draws) ChangshaGameStateMachine.DrawTile(state);
        if (profile == "kong-ready") ChangshaGameStateMachine.DeclareConcealedKong(state, 0, 4);
        Assert.Equal(effective, Effective(state.Hands[0]));
        if (profile == "two-chows-pending")
        {
            Assert.Equal(7, state.Hands[0].ConcealedTiles.Count);
            Assert.Equal(2, state.Hands[0].Melds.Count);
        }
        if (profile == "kong-ready")
        {
            Assert.Equal(11, state.Hands[0].ConcealedTiles.Count);
            Assert.Equal(4, Assert.Single(state.Hands[0].Melds).TileIds.Count);
        }
        var saved = await StoreAsync(host, state);
        await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        var before = await SnapshotAsync(host, saved);
        Assert.Equal(state.StateVersion, before.StateVersion);
        Assert.Equal(effective, Effective(before.Hands[0]));
        await AssertStoredAsync(host, saved, 1);
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        var after = await SnapshotAsync(host, saved);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, after.Phase);
        Assert.Equal(14, Effective(after.Hands[0]));
        Assert.Equal(before.Wall.Count - (draws ? 1 : 0), after.Wall.Count);
        Assert.Equal(before.Hands[0].ConcealedTiles.Count + (draws ? 1 : 0), after.Hands[0].ConcealedTiles.Count);
        Assert.Equal(before.HandNumber, after.HandNumber);
        HudsonOwnTurnWsFixture.AssertInventory(after);
        await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
        Assert.Equal(JsonSerializer.Serialize(after, SnapshotJson),
            JsonSerializer.Serialize(await SnapshotAsync(host, saved), SnapshotJson));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EveryEngineManualCursor_RestoresAndMakesItsNextRealPickup(int dealer)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        for (var completed = 0; completed < 17; completed++)
        {
            var state = ManualState(dealer, completed);
            var saved = await StoreAsync(host, state);
            Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
            var before = await SnapshotAsync(host, saved);
            Assert.Equal(state.StateVersion, before.StateVersion);
            var picker = Assert.IsType<int>(before.PickupSeatIndex);
            var count = ChangshaGameStateMachine.ExpectedPickupCount(before.Phase);
            Assert.NotEmpty(ChangshaToAutotableTranslator.Translate(before));
            await AssertStoredAsync(host, saved, completed + 1);
            await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
            await host.Runtime.TakeTilesFromWallAsync(saved.Id.ToString(), picker, count);
            var after = await SnapshotAsync(host, saved);
            var expected = Clone(before);
            ChangshaGameStateMachine.TakeTilesFromWall(expected, picker, count);
            Assert.Equal(expected.Phase, after.Phase);
            Assert.Equal(expected.PickupSeatIndex, after.PickupSeatIndex);
            Assert.Equal(expected.PickupRoundIndex, after.PickupRoundIndex);
            Assert.Equal(before.Wall.Count - count, after.Wall.Count);
            Assert.Equal(expected.Hands.Select(hand => hand.ConcealedTiles.Count),
                after.Hands.Select(hand => hand.ConcealedTiles.Count));
            Assert.NotEmpty(ChangshaToAutotableTranslator.Translate(after));
            HudsonOwnTurnWsFixture.AssertInventory(after);
        }
    }

    [Theory]
    [InlineData("seating")]
    [InlineData("rolling")]
    [InlineData("dealing")]
    [InlineData("scoring")]
    [InlineData("terminal")]
    [InlineData("legacy-terminal")]
    [InlineData("legacy-defaults")]
    [InlineData("round-one-default-cursor")]
    public async Task SetupTransientTerminalAndUnusedLegacyDefaults_RemainRecoverable(string profile)
    {
        await using var host = new HudsonOwnTurnWsFixture(persist: true);
        var state = ChangshaGameStateMachine.CreateGame(42, []).State;
        if (profile is "rolling" or "dealing" or "legacy-defaults")
            ChangshaGameStateMachine.StartGame(state);
        if (profile == "dealing") ChangshaGameStateMachine.RollDice(state, new DiceService(42));
        if (profile == "scoring")
        {
            state = PredrawState("hu");
            ChangshaGameStateMachine.DrawTile(state);
            ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
        }
        if (profile is "terminal" or "legacy-terminal")
        {
            state = ScoredState();
            ChangshaGameStateMachine.RotateBanker(state);
        }
        if (profile == "round-one-default-cursor") state = ManualState(0, 0);
        var saved = await StoreAsync(host, state, json =>
        {
            if (profile == "legacy-terminal") json["phase"] = 18;
            if (profile == "legacy-defaults")
                foreach (var key in new[] { "baseUnit", "lastDrawSeatIndex", "botDifficulty", "pickupRoundIndex" })
                    json.Remove(key);
            if (profile == "round-one-default-cursor")
            {
                json["phase"] = (int)ChangshaPhase.PickupRound1;
                json.Remove("pickupRoundIndex");
            }
            else
                json["pickupRoundIndex"] = -2;
        });
        await host.RestartAsync();
        Assert.Equal(saved.Id.ToString(), await host.Runtime.RestorePublicRoomAsync(saved.Room));
        var recovered = await SnapshotAsync(host, saved);
        Assert.Equal(state.StateVersion, recovered.StateVersion);
        Assert.NotEmpty(ChangshaToAutotableTranslator.Translate(recovered));
        await AssertStoredAsync(host, saved, 1);
        if (profile == "round-one-default-cursor")
        {
            await host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString());
            await host.Runtime.TakeTilesFromWallAsync(saved.Id.ToString(), 0, 4);
            Assert.Equal(1, (await SnapshotAsync(host, saved)).PickupRoundIndex);
        }
    }

    [Theory]
    [InlineData("fan-null", typeof(NullReferenceException))]
    [InlineData("fan-unknown", typeof(KeyNotFoundException))]
    [InlineData("robbery-declarer-null", typeof(InvalidOperationException))]
    [InlineData("robbery-pung-missing", typeof(InvalidOperationException))]
    public void SinglePropertyMutation_ActuallyBreaksTheExistingProjectionOrPassPrerequisite(
        string defect, Type exceptionType)
    {
        var (valid, path, replacement) = InvalidInput(defect);
        var json = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(valid, SnapshotJson));
        ReplaceOneProperty(json, path, replacement);
        var corrupted = JsonSerializer.Deserialize<ChangshaGameState>(json, SnapshotJson)!;
        var failure = Record.Exception(() =>
        {
            if (defect.StartsWith("fan-", StringComparison.Ordinal))
                ChangshaToAutotableTranslator.Translate(corrupted);
            else
                ChangshaGameStateMachine.PassClaim(corrupted);
        });
        Assert.NotNull(failure);
        Assert.Equal(exceptionType, failure.GetType());
    }

    [Fact]
    public void NegativeManualCursor_ReproducesTheActualNegativeNextPicker()
    {
        var state = ManualState(0, 0);
        var json = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(state, SnapshotJson));
        ReplaceOneProperty(json, "pickupRoundIndex", JsonValue.Create(-2));
        var corrupted = JsonSerializer.Deserialize<ChangshaGameState>(json, SnapshotJson)!;
        ChangshaGameStateMachine.TakeTilesFromWall(corrupted, 0, 4);
        Assert.Equal(-1, corrupted.PickupRoundIndex);
        Assert.Equal(-1, corrupted.PickupSeatIndex);
    }

    private static (ChangshaGameState State, string Path, JsonNode? Replacement) InvalidInput(string defect)
    {
        if (defect.StartsWith("fan-", StringComparison.Ordinal))
            return (ScoredState(), "currentScore/fans", defect == "fan-null"
                ? new JsonArray((JsonNode?)null)
                : new JsonArray(new JsonObject
                {
                    ["fan"] = defect == "fan-unknown" ? 999 : -1, ["points"] = 1
                }));
        if (defect.StartsWith("robbery-", StringComparison.Ordinal))
        {
            var state = ClaimState(robbing: true);
            return defect switch
            {
                "robbery-declarer-null" => (state, "claimWindow/kongDeclarerSeatIndex", null),
                "robbery-declarer-foreign" => (state, "claimWindow/kongDeclarerSeatIndex", JsonValue.Create(1)),
                "robbery-pung-missing" => (state, "hands/0/melds/0/kind", JsonValue.Create((int)MeldKind.Chow)),
                "robbery-tile-missing" => (state, "hands/0/concealedTiles", Tiles(state.Hands[0].ConcealedTiles.Where(tile => tile != 19))),
                "robbery-effective-twelve" => (state, "hands/0/concealedTiles",
                    Tiles(state.Hands[0].ConcealedTiles.Skip(2))),
                _ => throw new ArgumentOutOfRangeException(nameof(defect))
            };
        }
        if (defect.StartsWith("pickup-", StringComparison.Ordinal))
        {
            var completed = defect switch
            {
                "pickup-round-past-end" => 1,
                "pickup-round-negative" => 4,
                "pickup-round-overflow" => 8,
                "pickup-single-negative" => 12,
                "pickup-extra-offset" or "pickup-extra-short" => 16,
                _ => 0
            };
            var state = ManualState(0, completed);
            return defect switch
            {
                "pickup-seat-mismatch" => (state, "pickupSeatIndex", JsonValue.Create(1)),
                "pickup-wall-short" => (state, "wall", Tiles(state.Wall.Take(3))),
                "pickup-hand-overflow" => (state, "hands/0/concealedTiles", Tiles(state.Wall.Take(14))),
                "pickup-extra-short" => (state, "hands/0/concealedTiles", Tiles(state.Hands[0].ConcealedTiles.Take(12))),
                _ => (state, "pickupRoundIndex", JsonValue.Create(defect switch
                {
                    "pickup-negative" => -2,
                    "pickup-round-negative" or "pickup-single-negative" => -1,
                    "pickup-start-offset" or "pickup-extra-offset" => 1,
                    "pickup-round-past-end" => 4,
                    "pickup-round-overflow" => int.MaxValue,
                    _ => throw new ArgumentOutOfRangeException(nameof(defect))
                }))
            };
        }
        if (defect == "claim-next-effective-twelve")
        {
            var claim = ClaimState(robbing: false);
            return (claim, "hands/1/concealedTiles", Tiles(claim.Hands[1].ConcealedTiles.Take(12)));
        }
        var ready = PredrawState(defect is "discard-meld-effective-twelve" or "discard-effective-fifteen"
            ? "two-chows" : "hu");
        ChangshaGameStateMachine.DrawTile(ready);
        return defect switch
        {
            "discard-effective-twelve" => (ready, "hands/0/concealedTiles", Tiles(ready.Hands[0].ConcealedTiles.Take(12))),
            "discard-meld-effective-twelve" => (ready, "hands/0/concealedTiles", Tiles(ready.Hands[0].ConcealedTiles.Take(6))),
            "discard-effective-fifteen" => (ready, "hands/0/concealedTiles",
                Tiles(ready.Hands[0].ConcealedTiles.Append(ready.Wall[0]))),
            "discard-open-window" => (ready, "claimWindow", JsonSerializer.SerializeToNode(
                ClaimState(robbing: false).ClaimWindow, SnapshotJson)),
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };
    }

    private static ChangshaGameState ScoredState()
    {
        var state = PredrawState("hu");
        ChangshaGameStateMachine.DrawTile(state);
        ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
        Assert.Equal(ChangshaPhase.Scoring, state.Phase);
        ChangshaGameStateMachine.Score(state);
        Assert.Equal(ChangshaPhase.EndHand, state.Phase);
        Assert.NotNull(state.CurrentWin);
        Assert.NotNull(state.CurrentScore);
        Assert.Contains(state.EventLog, entry => entry.EventType == "tile-drawn");
        HudsonOwnTurnWsFixture.AssertInventory(state);
        return state;
    }

    private static ChangshaGameState ClaimState(bool robbing)
    {
        var state = PredrawState("added-kong");
        ChangshaGameStateMachine.DrawTile(state);
        if (robbing) ChangshaGameStateMachine.DeclareAddedKong(state, 0, 19);
        else ChangshaGameStateMachine.Discard(state, 0, 19);
        Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
        Assert.NotNull(state.ClaimWindow);
        Assert.Equal(robbing, state.ClaimWindow.IsKongRobbing);
        Assert.NotEmpty(state.ClaimWindow.Opportunities);
        HudsonOwnTurnWsFixture.AssertInventory(state);
        return state;
    }

    private static ChangshaGameState ManualState(int dealer, int completed)
    {
        var state = ChangshaGameStateMachine.CreateGame(42, []).State;
        state.DealerSeatIndex = dealer;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == dealer;
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.BeginManualDeal(state, new DiceService(42).Roll());
        for (var index = 0; index < completed; index++)
            ChangshaGameStateMachine.TakeTilesFromWall(state, state.PickupSeatIndex!.Value,
                ChangshaGameStateMachine.ExpectedPickupCount(state.Phase));
        Assert.True(ChangshaGameStateMachine.IsPickupPhase(state.Phase));
        HudsonOwnTurnWsFixture.AssertInventory(state);
        return state;
    }

    private static ChangshaGameState PredrawState(string profile)
    {
        var state = ChangshaGameStateMachine.CreateGame(42, []).State;
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(42));
        ChangshaGameStateMachine.Deal(state);
        // Arrange conserved tile fixtures, not a preconfirmed win/claim/score. The
        // engine's real discard, draw, declaration and score operations make those states.
        foreach (var hand in state.Hands)
        {
            hand.ConcealedTiles.Clear();
            hand.Melds.Clear();
        }
        var draw = profile switch { "two-chows" => 20, "added-kong" or "concealed-kong" => 19, _ => 0 };
        int[] own = profile switch
        {
            "two-chows" => [12, 16, 36, 40, 44, 88, 89],
            "added-kong" => [40, 41, 42, 44, 45, 46, 48, 49, 50, 52],
            "concealed-kong" => [16, 17, 18, 40, 41, 42, 44, 45, 46, 48, 49, 50, 52],
            "hu" => [4, 8, 12, 16, 20, 24, 28, 32, 52, 53, 36, 40, 44],
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        state.Hands[0].ConcealedTiles.AddRange(own);
        if (profile == "two-chows")
        {
            state.Hands[0].Melds.Add(new Meld { Kind = MeldKind.Chow, TileIds = [0, 4, 8], ClaimedFromSeatIndex = 3 });
            state.Hands[0].Melds.Add(new Meld { Kind = MeldKind.Chow, TileIds = [48, 52, 56], ClaimedFromSeatIndex = 3 });
        }
        if (profile == "added-kong")
        {
            state.Hands[0].Melds.Add(new Meld { Kind = MeldKind.Pung, TileIds = [16, 17, 18], ClaimedFromSeatIndex = 2 });
            state.Hands[1].ConcealedTiles.AddRange([0, 1, 2, 24, 25, 26, 60, 61, 62, 88, 89, 8, 12]);
        }
        var used = state.Hands.SelectMany(hand => hand.ConcealedTiles.Concat(hand.Melds.SelectMany(meld => meld.TileIds)))
            .Append(draw).Append(104).ToHashSet();
        int[] kinds = profile == "added-kong"
            ? [1, 2, 3, 5, 7, 8, 9, 13, 14, 16, 17, 18, 19]
            : [0, 1, 2, 3, 5, 6, 7, 8, 9, 14, 15, 16, 17];
        foreach (var hand in state.Hands.Where(hand => hand.SeatIndex != 0 && hand.ConcealedTiles.Count == 0))
            foreach (var kind in kinds)
            {
                var tile = Enumerable.Range(kind * 4, 4).First(tile => !used.Contains(tile));
                Assert.True(used.Add(tile));
                hand.ConcealedTiles.Add(tile);
            }
        state.Hands[3].ConcealedTiles.Add(104);
        state.Wall = new[] { draw }.Concat(Enumerable.Range(0, 108).Where(tile => !used.Contains(tile))).ToList();
        state.WallDrawIndex = state.WallBackDrawn = 0;
        state.WallBackIndex = state.Wall.Count - 1;
        state.ActiveSeatIndex = 3;
        state.LastDrawSeatIndex = 3;
        state.TurnNumber = 3;
        state.MaxHands = 1;
        Assert.Empty(new ClaimAdjudicator().GetOpportunities(3, 104, state.Hands));
        HudsonOwnTurnWsFixture.AssertInventory(state);
        ChangshaGameStateMachine.Discard(state, 3, 104);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Null(state.ClaimWindow);
        Assert.Null(state.LastDrawSeatIndex);
        Assert.Equal(13, Effective(state.Hands[0]));
        HudsonOwnTurnWsFixture.AssertInventory(state);
        return state;
    }

    private static int Effective(ChangshaHandState hand) => hand.ConcealedTiles.Count + 3 * hand.Melds.Count;
    private static JsonArray Tiles(IEnumerable<int> tiles) => new(tiles.Select(tile => (JsonNode?)JsonValue.Create(tile)).ToArray());
    private static ChangshaGameState Clone(ChangshaGameState state) =>
        JsonSerializer.Deserialize<ChangshaGameState>(JsonSerializer.Serialize(state, SnapshotJson), SnapshotJson)!;

    private static void ReplaceOneProperty(JsonObject json, string path, JsonNode? value)
    {
        var before = json.DeepClone();
        var parts = path.Split('/');
        JsonNode parent = json;
        foreach (var part in parts[..^1])
            parent = parent is JsonArray array ? array[int.Parse(part)]! : parent[part]!;
        var property = parts[^1];
        var original = parent[property]?.DeepClone();
        parent[property] = value?.DeepClone();
        Assert.False(JsonNode.DeepEquals(before, json));
        var changed = parent[property]?.DeepClone();
        parent[property] = original;
        Assert.True(JsonNode.DeepEquals(before, json), "Only the named prerequisite property may be mutated.");
        parent[property] = changed;
    }

    private sealed record StoredSnapshot(Guid Id, string Room, string Key, string Json);

    private static async Task<StoredSnapshot> StoreAsync(
        HudsonOwnTurnWsFixture host, ChangshaGameState state, Action<JsonObject>? mutate = null)
    {
        var id = Guid.NewGuid();
        var room = $"frost-recovery-{Guid.NewGuid():N}";
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

    private static async Task<ChangshaGameState> SnapshotAsync(HudsonOwnTurnWsFixture host, StoredSnapshot saved) =>
        await host.Runtime.TryGetSnapshotCopyAsync(saved.Id.ToString())
        ?? throw new InvalidOperationException("Expected the exact recovered runtime.");

    private static async Task AssertStoredAsync(HudsonOwnTurnWsFixture host, StoredSnapshot saved, int rows)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(rows, await db.ChangshaGames.CountAsync());
        Assert.Equal(rows, await db.AutotableRoomBindings.CountAsync());
        var row = await db.ChangshaGames.AsNoTracking().SingleAsync(game => game.Id == saved.Id);
        Assert.True(string.Equals(saved.Json, row.StateJson, StringComparison.Ordinal), "The stored snapshot changed.");
        var binding = await db.AutotableRoomBindings.AsNoTracking().SingleAsync(item => item.RoomKey == saved.Key);
        Assert.Equal(saved.Id, binding.RuntimeGameId);
        Assert.Equal(saved.Room, binding.RoomId);
    }

    private async Task DescribeRejectedBaselineAdmissionAsync(
        HudsonOwnTurnWsFixture host, StoredSnapshot saved, string defect)
    {
        var admitted = await SnapshotAsync(host, saved);
        output.WriteLine($"REJECTED BASELINE: invalid {defect} was allocated/published; phase={admitted.Phase}.");
        Exception? failure;
        if (defect.StartsWith("fan-", StringComparison.Ordinal))
            failure = Record.Exception(() => ChangshaToAutotableTranslator.Translate(admitted));
        else if (defect.StartsWith("robbery-", StringComparison.Ordinal))
            failure = Record.Exception(() => ChangshaGameStateMachine.PassClaim(admitted));
        else if (defect == "pickup-negative")
        {
            failure = Record.Exception(() => ChangshaGameStateMachine.TakeTilesFromWall(admitted, 0, 4));
            output.WriteLine($"Actual next pickup cursor={admitted.PickupRoundIndex}; picker={admitted.PickupSeatIndex}.");
        }
        else if (defect.StartsWith("discard-", StringComparison.Ordinal))
            failure = await Record.ExceptionAsync(() => host.Runtime.ResumeRecoveredPublicRoomAsync(saved.Id.ToString()));
        else
            return;
        output.WriteLine($"Actual first projection/resume/pass failure={failure?.GetType().Name ?? "none"}"
            + (failure is PublicRoomRecoveryException recovery ? $"; reason={recovery.Reason}" : string.Empty));
    }

    private static TestServer Server(HudsonOwnTurnWsFixture host) =>
        Assert.IsType<TestServer>(host.Services.GetRequiredService<IServer>());

    private static async Task AssertRejectedSocketAsync(HudsonOwnTurnWsFixture host, string room, string operation)
    {
        using var socket = await host.OpenSocketAsync(
            $"variant=changsha&gameId={room}&bots=false&botCount=0&dealMode=manual&seed=42");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = operation, gameId = room }),
            WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096];
        using var bytes = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, frame.MessageType);
            bytes.Write(buffer, 0, frame.Count);
            Assert.True(bytes.Length <= 4096, "The room rejection must be bounded, not a FULL snapshot.");
        } while (!frame.EndOfMessage);
        using var document = JsonDocument.Parse(bytes.ToArray());
        var rejection = document.RootElement;
        Assert.Equal("UPDATE", rejection.GetProperty("type").GetString());
        Assert.False(rejection.GetProperty("full").GetBoolean());
        var entry = Assert.Single(rejection.GetProperty("entries").EnumerateArray());
        Assert.Equal("actionRejected", entry[0].GetString());
        Assert.Equal("current", entry[1].GetString());
        Assert.Equal("room", entry[2].GetProperty("action").GetString());
        Assert.Equal("room-snapshot-invalid", entry[2].GetProperty("reason").GetString());
        Assert.DoesNotContain(Canary, rejection.GetRawText());
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
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
