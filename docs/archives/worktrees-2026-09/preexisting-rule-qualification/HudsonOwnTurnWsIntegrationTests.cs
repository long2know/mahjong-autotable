using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonOwnTurnWsIntegrationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(0, "hu-14", 14, 0)]
    [InlineData(1, "hu-14", 14, 0)]
    [InlineData(2, "hu-14", 14, 0)]
    [InlineData(3, "hu-14", 14, 0)]
    [InlineData(0, "hu-11", 11, 1)]
    [InlineData(2, "hu-11", 11, 1)]
    [InlineData(0, "hu-8", 8, 2)]
    [InlineData(2, "hu-8", 8, 2)]
    public async Task AuthenticatedSelfHu_UsesActualDrawAndOwnTurnRoute(
        int seat, string scenario, int concealed, int meldCount)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(seat, ephemeralKinds: ["ownTurn"]);
        var draw = ArrangeBeforeDraw(table, scenario);
        await host.AdvanceToDrawAsync(table, draw);
        Assert.Equal(concealed, table.State.Hands[seat].ConcealedTiles.Count);
        Assert.Equal(meldCount, table.State.Hands[seat].Melds.Count);
        Assert.Equal(14, concealed + 3 * meldCount);
        Assert.True(new ChangshaWinDetector().Detect(table.State.Hands[seat]).IsWin);
        Assert.Null(table.State.CurrentWin);

        var available = OwnTurn(await table.Peer.BarrierAsync(), seat);
        Assert.Equal(table.GameId, available.GetProperty("gameId").GetString());
        Assert.True(available.GetProperty("hu").GetBoolean());
        Assert.Equal(table.State.StateVersion, available.GetProperty("stateVersion").GetInt32());
        var version = table.State.StateVersion;
        await SendAsync(table.Peer, seat, "hu", available);
        await table.Peer.BarrierAsync();

        Assert.Equal(ChangshaPhase.GameComplete, table.State.Phase);
        Assert.True(table.State.IsGameComplete);
        Assert.Equal(seat, table.State.CurrentWin!.WinningSeatIndex);
        Assert.Equal(WinMethod.SelfDraw, table.State.CurrentWin.Method);
        Assert.Equal(draw.DrawTile, table.State.CurrentWin.WinningTileId);
        Assert.True(table.State.StateVersion > version);
        Assert.Equal(4, table.State.CumulativeScores.Count);
        Assert.Equal(0, table.State.CumulativeScores.Values.Sum());
        Assert.True(table.State.CumulativeScores[seat] > 0);
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AuthenticatedConcealedKong_ConsumesAdvertisedFourAndDrawsExactBack(int seat)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(seat, ephemeralKinds: ["ownTurn"]);
        var draw = ArrangeBeforeDraw(table, "concealed-kong");
        await host.AdvanceToDrawAsync(table, draw);
        var before = await host.SnapshotAsync(table);
        var available = OwnTurn(await table.Peer.BarrierAsync(), seat);
        var choices = available.GetProperty("concealedKongs").EnumerateArray()
            .Select(option => option.EnumerateArray().Select(id => id.GetInt32()).OrderBy(id => id).ToArray()).ToArray();
        Assert.Contains(choices, option => option.SequenceEqual(new[] { 16, 17, 18, 19 }));

        await SendAsync(table.Peer, seat, "concealedKong", available, [19, 17, 16, 18]);
        await table.Peer.BarrierAsync();
        var hand = table.State.Hands[seat];
        var meld = Assert.Single(hand.Melds);
        Assert.Equal(MeldKind.ConcealedKong, meld.Kind);
        Assert.Equal(new[] { 16, 17, 18, 19 }, meld.TileIds.OrderBy(id => id));
        Assert.Equal(11, hand.ConcealedTiles.Count);
        Assert.Equal(draw.BackTile, hand.ConcealedTiles[^1]);
        Assert.Equal(before.Wall.Count - 1, table.State.Wall.Count);
        Assert.Equal(before.WallBackDrawn + 1, table.State.WallBackDrawn);
        Assert.True(table.State.LastDrawWasKongReplacement);
        Assert.Null(table.State.ClaimWindow);
        AssertInventory(table.State);

        var settled = await host.SnapshotAsync(table);
        await SendAsync(table.Peer, seat, "concealedKong", available, [16, 17, 18, 19]);
        await RejectedBarrierAsync(table.Peer, "stale-version");
        AssertUnchanged(settled, await host.SnapshotAsync(table));
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AuthenticatedAddedKong_CommitsOnlyWithNoRobberAndDrawsBack(int seat)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(seat, ephemeralKinds: ["ownTurn"]);
        var draw = ArrangeBeforeDraw(table, "added-kong");
        await host.AdvanceToDrawAsync(table, draw);
        Assert.All(table.State.Hands.Where(h => h.SeatIndex != seat), hand =>
            Assert.False(CanHuOn(hand, draw.DrawTile)));
        var before = await host.SnapshotAsync(table);
        var available = OwnTurn(await table.Peer.BarrierAsync(), seat);
        Assert.Contains(19, available.GetProperty("addedKongs").EnumerateArray().Select(x => x.GetInt32()));

        await SendAsync(table.Peer, seat, "addedKong", available, [19]);
        await table.Peer.BarrierAsync();
        var meld = Assert.Single(table.State.Hands[seat].Melds);
        Assert.Equal(MeldKind.AddedKong, meld.Kind);
        Assert.Equal(new[] { 16, 17, 18, 19 }, meld.TileIds.OrderBy(id => id));
        Assert.Equal(11, table.State.Hands[seat].ConcealedTiles.Count);
        Assert.Equal(draw.BackTile, table.State.Hands[seat].ConcealedTiles[^1]);
        Assert.Equal(before.WallBackDrawn + 1, table.State.WallBackDrawn);
        Assert.Equal(before.Wall.Count - 1, table.State.Wall.Count);
        Assert.Null(table.State.ClaimWindow);
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddedKong_OpensRealHuOnlyWindowAndHonorsAuthenticatedRobOrPass(bool rob)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var robber = await host.ConnectAsync(table.RoomId, ephemeralKinds: ["ownTurn"]);
        await robber.UpdateAsync([new object[] { "seats", robber.PlayerId, new { seat = 1 } }]);
        await robber.BarrierAsync();
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(table.GameId, robber.PlayerId));
        var draw = ArrangeBeforeDraw(table, "added-kong-rob");
        await host.AdvanceToDrawAsync(table, draw);
        Assert.True(CanHuOn(table.State.Hands[1], 19));
        Assert.All(table.State.Hands.Where(hand => hand.SeatIndex is 2 or 3),
            hand => Assert.False(CanHuOn(hand, 19)));
        var before = await host.SnapshotAsync(table);
        var available = OwnTurn(await table.Peer.BarrierAsync(), 0);

        await SendAsync(table.Peer, 0, "addedKong", available, [19]);
        await table.Peer.BarrierAsync();
        Assert.Equal(ChangshaPhase.AwaitingClaim, table.State.Phase);
        Assert.True(table.State.ClaimWindow!.IsKongRobbing);
        Assert.Equal(19, table.State.ClaimWindow.DiscardTileId);
        Assert.Equal(0, table.State.ClaimWindow.DiscardSeatIndex);
        Assert.Equal(before.Wall.Count, table.State.Wall.Count);
        Assert.Equal(before.WallBackDrawn, table.State.WallBackDrawn);
        Assert.Equal(MeldKind.Pung, Assert.Single(table.State.Hands[0].Melds).Kind);
        var opportunity = Assert.Single(table.State.ClaimWindow.Opportunities);
        Assert.Equal(TableClaimType.Hu, opportunity.ClaimType);
        Assert.Equal(1, opportunity.SeatIndex);
        AssertOwnTurnHidden(await table.Peer.BarrierAsync());
        var claim = Entry(await robber.BarrierAsync(), "claim", 1);
        Assert.Equal(new[] { "Hu" }, claim.GetProperty("available").EnumerateArray().Select(value => value.GetString()));
        AssertInventory(table.State);

        await robber.UpdateAsync([new object[]
        {
            "claim", 1, new { action = rob ? "claim" : "pass", type = rob ? "Hu" : null }
        }]);
        await robber.BarrierAsync();
        Assert.Null(table.State.ClaimWindow);
        if (rob)
        {
            Assert.Equal(ChangshaPhase.GameComplete, table.State.Phase);
            Assert.Equal(WinMethod.RobbingKong, table.State.CurrentWin!.Method);
            Assert.True(table.State.CurrentWin.IsRobbedKong);
            Assert.Equal(1, table.State.CurrentWin.WinningSeatIndex);
            Assert.Equal(0, table.State.CurrentWin.SourceSeatIndex);
            Assert.Equal(19, table.State.CurrentWin.WinningTileId);
            Assert.DoesNotContain(19, table.State.Hands[0].ConcealedTiles);
            Assert.Contains(19, table.State.Hands[1].ConcealedTiles);
            Assert.Equal(MeldKind.Pung, Assert.Single(table.State.Hands[0].Melds).Kind);
            Assert.Equal(before.Wall.Count, table.State.Wall.Count);
            Assert.Equal(before.WallBackDrawn, table.State.WallBackDrawn);
            Assert.Equal(0, table.State.CumulativeScores.Values.Sum());
        }
        else
        {
            Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
            Assert.Equal(0, table.State.ActiveSeatIndex);
            Assert.Equal(MeldKind.AddedKong, Assert.Single(table.State.Hands[0].Melds).Kind);
            Assert.Equal(draw.BackTile, table.State.Hands[0].ConcealedTiles[^1]);
            Assert.Equal(before.Wall.Count - 1, table.State.Wall.Count);
            Assert.Equal(before.WallBackDrawn + 1, table.State.WallBackDrawn);
            Assert.Equal(0, table.State.LastDrawSeatIndex);
        }
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("hu-14", "hu")]
    [InlineData("hu-11", "hu")]
    [InlineData("hu-8", "hu")]
    [InlineData("concealed-kong", "concealedKong")]
    [InlineData("added-kong", "addedKong")]
    public async Task PredrawPhase_OffersNoOwnActionAndCannotMutate(string scenario, string action)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        ArrangeBeforeDraw(table, scenario);
        ChangshaGameStateMachine.Discard(table.State, 3, 104);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(0, table.State.ActiveSeatIndex);
        Assert.Null(table.State.LastDrawSeatIndex);
        Assert.Equal(13, table.State.Hands[0].ConcealedTiles.Count + 3 * table.State.Hands[0].Melds.Count);
        var before = await host.SnapshotAsync(table);
        AssertOwnTurnHidden(await table.Peer.BarrierAsync());
        var ids = action == "hu" ? null : action == "concealedKong" ? new[] { 16, 17, 18, 19 } : new[] { 19 };
        await SendAtVersionAsync(table.Peer, 0, action, before.StateVersion, table.GameId, ids);
        await RejectedBarrierAsync(table.Peer, "own-turn-not-available");
        AssertUnchanged(before, await host.SnapshotAsync(table));
        AssertInventory(table.State);
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("hu-14", "Hu", "hu")]
    [InlineData("concealed-kong", "Kong", "concealedKong")]
    public async Task LegacyDiscardClaimRoute_IsNotReinterpretedAsOwnTurn(string scenario, string claimType, string action)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        var draw = ArrangeBeforeDraw(table, scenario);
        await host.AdvanceToDrawAsync(table, draw);
        var own = OwnTurn(await table.Peer.BarrierAsync(), 0);
        var before = await host.SnapshotAsync(table);
        await table.Peer.UpdateAsync([new object[] { "claim", 0, new { action = "claim", type = claimType } }]);
        await table.Peer.BarrierAsync();
        AssertUnchanged(before, await host.SnapshotAsync(table));
        Assert.NotEqual(JsonValueKind.Null, own.ValueKind);
        Assert.Equal(action == "hu", own.GetProperty("hu").GetBoolean());
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("hu-14", "hu")]
    [InlineData("concealed-kong", "concealedKong")]
    [InlineData("added-kong", "addedKong")]
    public async Task SpectatorWrongSeatAndStaleVersions_CannotUseOwnersAdvertisedAction(string scenario, string action)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var otherOwner = await host.ConnectAsync(table.RoomId, ephemeralKinds: ["ownTurn"]);
        await otherOwner.UpdateAsync([new object[] { "seats", otherOwner.PlayerId, new { seat = 1 } }]);
        await otherOwner.BarrierAsync();
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(table.GameId, otherOwner.PlayerId));
        var draw = ArrangeBeforeDraw(table, scenario);
        await host.AdvanceToDrawAsync(table, draw);
        var available = OwnTurn(await table.Peer.BarrierAsync(), 0);
        var ids = action == "hu" ? null : action == "concealedKong" ? new[] { 16, 17, 18, 19 } : new[] { 19 };
        await using var spectator = await host.ConnectAsync(table.RoomId, ephemeralKinds: ["ownTurn"]);
        AssertOwnTurnHidden(await spectator.BarrierAsync());
        AssertOwnTurnHidden(await otherOwner.BarrierAsync());
        var before = await host.SnapshotAsync(table);

        await SendAsync(spectator, 0, action, available, ids);
        AssertOwnTurnHidden(await RejectedBarrierAsync(spectator));
        AssertUnchanged(before, await host.SnapshotAsync(table));
        await SendAsync(otherOwner, 0, action, available, ids);
        AssertOwnTurnHidden(await RejectedBarrierAsync(otherOwner));
        AssertUnchanged(before, await host.SnapshotAsync(table));
        await SendAsync(table.Peer, 1, action, available, ids);
        await RejectedBarrierAsync(table.Peer);
        AssertUnchanged(before, await host.SnapshotAsync(table));
        await SendAtVersionAsync(table.Peer, 0, action, before.StateVersion - 1, table.GameId, ids);
        await RejectedBarrierAsync(table.Peer, "stale-version");
        AssertUnchanged(before, await host.SnapshotAsync(table));
        Assert.All(spectator.Frames.SelectMany(Entries).Where(e => e[0].GetString() == "ownTurn"),
            e => Assert.Equal(JsonValueKind.Null, e[2].ValueKind));
        Assert.All(otherOwner.Frames.SelectMany(Entries).Where(e => e[0].GetString() == "ownTurn"),
            e => Assert.Equal(JsonValueKind.Null, e[2].ValueKind));
    }

    [Theory, Trait("Category", "RulesQualificationWs")]
    [InlineData("missing-version")]
    [InlineData("null-version")]
    [InlineData("string-version")]
    [InlineData("future-version")]
    [InlineData("unknown-action")]
    [InlineData("missing-game")]
    [InlineData("null-game")]
    [InlineData("relay-id-instead-runtime")]
    [InlineData("other-runtime")]
    [InlineData("hu-with-tiles")]
    [InlineData("concealed-one-id")]
    [InlineData("concealed-duplicate")]
    [InlineData("concealed-mixed")]
    [InlineData("added-meld-tile")]
    [InlineData("added-multiple")]
    public async Task MalformedOrUnadvertisedOwnTurnCommand_DoesNotMutate(string defect)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        var scenario = defect.StartsWith("concealed-", StringComparison.Ordinal) ? "concealed-kong"
            : defect.StartsWith("added-", StringComparison.Ordinal) ? "added-kong" : "hu-14";
        await host.AdvanceToDrawAsync(table, ArrangeBeforeDraw(table, scenario));
        var available = OwnTurn(await table.Peer.BarrierAsync(), 0);
        var version = available.GetProperty("stateVersion").GetInt32();
        var payload = new Dictionary<string, object?> { ["action"] = "hu", ["expectedVersion"] = version, ["gameId"] = table.GameId };
        switch (defect)
        {
            case "missing-version": payload.Remove("expectedVersion"); break;
            case "null-version": payload["expectedVersion"] = null; break;
            case "string-version": payload["expectedVersion"] = version.ToString(); break;
            case "future-version": payload["expectedVersion"] = version + 1; break;
            case "unknown-action": payload["action"] = "pass"; break;
            case "missing-game": payload.Remove("gameId"); break;
            case "null-game": payload["gameId"] = null; break;
            case "relay-id-instead-runtime": payload["gameId"] = table.RoomId; break;
            case "other-runtime": payload["gameId"] = Guid.NewGuid().ToString(); break;
            case "hu-with-tiles": payload["tileIds"] = new[] { 0 }; break;
            case "concealed-one-id":
                payload["action"] = "concealedKong"; payload["tileIds"] = new[] { 19 }; break;
            case "concealed-duplicate":
                payload["action"] = "concealedKong"; payload["tileIds"] = new[] { 16, 16, 17, 19 }; break;
            case "concealed-mixed":
                payload["action"] = "concealedKong"; payload["tileIds"] = new[] { 16, 17, 18, 40 }; break;
            case "added-meld-tile":
                payload["action"] = "addedKong"; payload["tileIds"] = new[] { 16 }; break;
            case "added-multiple":
                payload["action"] = "addedKong"; payload["tileIds"] = new[] { 19, 40 }; break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        var before = await host.SnapshotAsync(table);
        await table.Peer.UpdateAsync([new object[] { "ownTurn", 0, payload }]);
        var expectedReason = defect switch
        {
            "future-version" => "stale-version",
            "relay-id-instead-runtime" or "other-runtime" => "stale-game",
            "concealed-mixed" or "added-meld-tile" => "own-turn-not-available",
            _ => "invalid-own-turn-command"
        };
        await RejectedBarrierAsync(table.Peer, expectedReason);
        AssertUnchanged(before, await host.SnapshotAsync(table));
        AssertInventory(table.State);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task SignedOwnerReconnect_RecoversOnlyItsOwnActionAndCanWin()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(2, ephemeralKinds: ["ownTurn"]);
        var draw = ArrangeBeforeDraw(table, "hu-11");
        await host.AdvanceToDrawAsync(table, draw);
        var player = table.Peer.PlayerId;
        await table.Peer.DisposeAsync();
        await using var reconnect = await host.ConnectAsync(table.RoomId, player, ephemeralKinds: ["ownTurn"]);
        var available = OwnTurn(await reconnect.BarrierAsync(), 2);
        Assert.Equal(2, host.Runtime.TryGetSeatForPlayer(table.GameId, player));
        Assert.True(available.GetProperty("hu").GetBoolean());
        await SendAsync(reconnect, 2, "hu", available);
        await reconnect.BarrierAsync();
        Assert.Equal(WinMethod.SelfDraw, table.State.CurrentWin!.Method);
        Assert.Equal(2, table.State.CurrentWin.WinningSeatIndex);
        AssertInventory(table.State);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task SameSocketCrossRoom_CannotInheritOrInvokeDestinationOwnersPrivateAction()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var source = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var destination = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await host.AdvanceToDrawAsync(source, ArrangeBeforeDraw(source, "concealed-kong"));
        await host.AdvanceToDrawAsync(destination, ArrangeBeforeDraw(destination, "hu-8"));
        var sourceAction = OwnTurn(await source.Peer.BarrierAsync(), 0);
        var destinationAction = OwnTurn(await destination.Peer.BarrierAsync(), 0);
        var before = await host.SnapshotAsync(destination);
        var mark = source.Peer.Frames.Count;

        AssertOwnTurnHidden(await source.Peer.BarrierAsync(destination.RoomId));
        var destinationJoined = source.Peer.Frames.FindIndex(mark, frame =>
            frame.GetProperty("type").GetString() == "JOINED"
            && frame.GetProperty("gameId").GetString() == destination.RoomId);
        Assert.True(destinationJoined >= mark);
        await SendAsync(source.Peer, 0, "hu", destinationAction);
        AssertOwnTurnHidden(await RejectedBarrierAsync(source.Peer));
        await SendAsync(source.Peer, 0, "concealedKong", sourceAction, [16, 17, 18, 19]);
        AssertOwnTurnHidden(await RejectedBarrierAsync(source.Peer));
        AssertUnchanged(before, await host.SnapshotAsync(destination));
        Assert.All(source.Peer.Frames.Skip(destinationJoined + 1).SelectMany(Entries).Where(e => e[0].GetString() == "ownTurn"),
            e => Assert.Equal(JsonValueKind.Null, e[2].ValueKind));

        await SendAsync(destination.Peer, 0, "hu", OwnTurn(await destination.Peer.BarrierAsync(), 0));
        await destination.Peer.BarrierAsync();
        Assert.Equal(WinMethod.SelfDraw, destination.State.CurrentWin!.Method);
        AssertInventory(destination.State);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task TwoOwnedRoomsWithEqualVersions_RejectOldRoomActionBeforeValidDestinationAction()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var source = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var destination = await host.OpenHumanTableAsync(
            playerId: source.Peer.PlayerId, ephemeralKinds: ["ownTurn"]);
        await host.AdvanceToDrawAsync(source, ArrangeBeforeDraw(source, "hu-14"));
        await host.AdvanceToDrawAsync(destination, ArrangeBeforeDraw(destination, "hu-14"));
        var oldAction = OwnTurn(await source.Peer.BarrierAsync(), 0);
        var fresh = OwnTurn(await source.Peer.BarrierAsync(destination.RoomId), 0);
        Assert.Equal(source.Peer.PlayerId, destination.Peer.PlayerId);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(destination.GameId, source.Peer.PlayerId));
        Assert.Equal(oldAction.GetProperty("stateVersion").GetInt32(), fresh.GetProperty("stateVersion").GetInt32());
        Assert.Equal(source.GameId, oldAction.GetProperty("gameId").GetString());
        Assert.Equal(destination.GameId, fresh.GetProperty("gameId").GetString());
        Assert.NotEqual(source.GameId, destination.GameId);
        var sourceBefore = await host.SnapshotAsync(source);
        var destinationBefore = await host.SnapshotAsync(destination);

        await SendAsync(source.Peer, 0, "hu", oldAction);
        await RejectedBarrierAsync(source.Peer, "stale-game");
        AssertUnchanged(sourceBefore, await host.SnapshotAsync(source));
        AssertUnchanged(destinationBefore, await host.SnapshotAsync(destination));
        await SendAsync(source.Peer, 0, "hu", OwnTurn(await source.Peer.BarrierAsync(), 0));
        await source.Peer.BarrierAsync();
        Assert.Equal(ChangshaPhase.GameComplete, destination.State.Phase);
        Assert.Null(source.State.CurrentWin);
        AssertInventory(source.State);
        AssertInventory(destination.State);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task OwnTurnAvailabilityAndPrivacyTombstones_DoNotRequireClientRegistration()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync();
        await host.AdvanceToDrawAsync(table, ArrangeBeforeDraw(table, "hu-14"));
        var owner = OwnTurn(await table.Peer.BarrierAsync(), 0);
        Assert.True(owner.GetProperty("hu").GetBoolean());
        await using var spectator = await host.ConnectAsync(table.RoomId);
        AssertOwnTurnHidden(await spectator.BarrierAsync());
        await SendAsync(table.Peer, 0, "hu", owner);
        AssertOwnTurnHidden(await table.Peer.BarrierAsync());
        AssertOwnTurnHidden(await spectator.BarrierAsync());
        Assert.Equal(ChangshaPhase.GameComplete, table.State.Phase);
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task PungWithoutOwnDraw_AllowsLegalKongButRejectsSelfHu()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        var incoming = ArrangeBeforeDraw(table, "pung-no-draw");
        var held = table.State.Hands[0].ConcealedTiles;
        foreach (var (oldTile, newTile) in new[] { (18, 0), (45, 4), (46, 8), (49, 5), (50, 6) })
        {
            var index = held.IndexOf(oldTile);
            Assert.InRange(index, 0, held.Count - 1);
            Assert.DoesNotContain(newTile, held);
            var source = table.State.Hands.Select(hand => hand.ConcealedTiles).Append(table.State.Wall)
                .Single(tiles => tiles.Contains(newTile));
            var sourceIndex = source.IndexOf(newTile);
            (source[sourceIndex], held[index]) = (held[index], source[sourceIndex]);
        }
        AssertInventory(table.State);
        ArrangeIncomingDiscard(table, incoming);
        await host.Runtime.DiscardAsync(table.GameId, 3, incoming.DrawTile);
        Assert.Equal(ChangshaPhase.AwaitingClaim, table.State.Phase);
        Assert.All(table.State.ClaimWindow!.Opportunities, opportunity => Assert.Equal(0, opportunity.SeatIndex));
        var claim = Entry(await table.Peer.BarrierAsync(), "claim", 0);
        Assert.Contains("Pung", claim.GetProperty("available").EnumerateArray().Select(v => v.GetString()));
        await table.Peer.UpdateAsync([new object[] { "claim", "0", new { action = "claim", type = "Pung" } }]);
        await table.Peer.BarrierAsync();
        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(0, table.State.ActiveSeatIndex);
        Assert.Null(table.State.LastDrawSeatIndex);
        Assert.Equal(11, table.State.Hands[0].ConcealedTiles.Count);
        Assert.Equal(14, table.State.Hands[0].ConcealedTiles.Count + 3 * table.State.Hands[0].Melds.Count);
        var pung = Assert.Single(table.State.Hands[0].Melds);
        Assert.Equal(MeldKind.Pung, pung.Kind);
        Assert.Equal(new[] { 16, 17, 19 }, pung.TileIds.OrderBy(tile => tile));
        Assert.All(new[] { 40, 41, 42, 43 }, id => Assert.Contains(id, table.State.Hands[0].ConcealedTiles));
        Assert.DoesNotContain(table.State.EventLog, e => e.EventType == "tile-drawn" && e.SeatIndex == 0);
        Assert.True(new ChangshaWinDetector().Detect(table.State.Hands[0]).IsWin);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(table.State, 0));
        Assert.True(ChangshaGameStateMachine.CanDeclareConcealedKong(table.State, 0, 10));
        AssertInventory(table.State);

        var before = await host.SnapshotAsync(table);
        await SendAtVersionAsync(table.Peer, 0, "hu", before.StateVersion, table.GameId);
        await RejectedBarrierAsync(table.Peer, "own-turn-not-available");
        AssertUnchanged(before, await host.SnapshotAsync(table));
        Assert.Null(table.State.CurrentWin);
        Assert.Null(table.State.CurrentScore);
        output.WriteLine($"Post-Pung game={table.GameId} effective14/structuralWin=true/ownDraw=null: fake SelfHu rejected unchanged; pure concealed-Kong candidate is legal.");

        var frame = await table.Peer.BarrierAsync();
        output.WriteLine("Post-Pung ownTurn metadata: " + Entry(frame, "ownTurn", 0).GetRawText());
        var available = OwnTurn(frame, 0);
        Assert.False(available.GetProperty("hu").GetBoolean());
        Assert.Equal(table.GameId, available.GetProperty("gameId").GetString());
        Assert.Equal(before.StateVersion, available.GetProperty("stateVersion").GetInt32());
        Assert.Contains(available.GetProperty("concealedKongs").EnumerateArray(),
            option => option.EnumerateArray().Select(tile => tile.GetInt32()).OrderBy(tile => tile)
                .SequenceEqual(new[] { 40, 41, 42, 43 }));
        var replacement = before.Wall[^1];
        await SendAsync(table.Peer, 0, "concealedKong", available, [43, 42, 41, 40]);
        await table.Peer.BarrierAsync();
        Assert.Equal(ChangshaPhase.AwaitingDiscard, table.State.Phase);
        Assert.Equal(0, table.State.ActiveSeatIndex);
        Assert.Equal(0, table.State.LastDrawSeatIndex);
        Assert.True(table.State.LastDrawWasKongReplacement);
        Assert.Null(table.State.ClaimWindow);
        Assert.Equal(2, table.State.Hands[0].Melds.Count);
        Assert.Equal(new[] { 16, 17, 19 }, table.State.Hands[0].Melds[0].TileIds.OrderBy(tile => tile));
        var kong = Assert.Single(table.State.Hands[0].Melds, meld => meld.Kind == MeldKind.ConcealedKong);
        Assert.Equal(new[] { 40, 41, 42, 43 }, kong.TileIds.OrderBy(tile => tile));
        Assert.Equal(8, table.State.Hands[0].ConcealedTiles.Count);
        Assert.Equal(replacement, table.State.Hands[0].ConcealedTiles[^1]);
        Assert.Equal(before.Wall.Count - 1, table.State.Wall.Count);
        Assert.Equal(before.WallDrawIndex, table.State.WallDrawIndex);
        Assert.Equal(before.WallBackDrawn + 1, table.State.WallBackDrawn);
        Assert.True(table.State.StateVersion > before.StateVersion);
        Assert.Null(table.State.CurrentWin);
        Assert.Null(table.State.CurrentScore);
        Assert.Equal(before.CumulativeScores.OrderBy(pair => pair.Key), table.State.CumulativeScores.OrderBy(pair => pair.Key));
        AssertInventory(table.State);
        output.WriteLine($"Retained current-profile post-claim Kong accepted over normal WS; exact4 moved, backDraw={replacement}, version={table.State.StateVersion}, inventory=108.");
    }

    [Fact, Trait("Category", "RulesQualificationWs")]
    public async Task ChowOptions_ArePrivateExactHeldPairsAndExplicitSelectionIsHonored()
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        var incoming = ArrangeBeforeDraw(table, "chow-options");
        ArrangeIncomingDiscard(table, incoming);
        await using var spectator = await host.ConnectAsync(table.RoomId, ephemeralKinds: ["ownTurn"]);
        await host.Runtime.DiscardAsync(table.GameId, 3, 19);
        var claim = Entry(await table.Peer.BarrierAsync(), "claim", 0);
        Assert.Contains("Chow", claim.GetProperty("available").EnumerateArray().Select(v => v.GetString()));
        var choices = claim.GetProperty("chowOptions").EnumerateArray()
            .Select(x => x.EnumerateArray().Select(t => t.GetInt32()).OrderBy(t => t).ToArray()).ToArray();
        Assert.Equal(3, choices.Length);
        Assert.Contains(choices, x => x.SequenceEqual(new[] { 8, 12 }));
        Assert.Contains(choices, x => x.SequenceEqual(new[] { 12, 20 }));
        Assert.Contains(choices, x => x.SequenceEqual(new[] { 20, 24 }));
        Assert.All(choices, pair =>
        {
            Assert.Equal(2, pair.Length);
            Assert.DoesNotContain(19, pair);
            Assert.All(pair, tile => Assert.Contains(tile, table.State.Hands[0].ConcealedTiles));
        });
        var other = await spectator.BarrierAsync();
        Assert.All(Entries(other).Where(e => e[0].GetString() == "claim" && e[2].ValueKind == JsonValueKind.Object),
            e =>
            {
                if (!e[2].TryGetProperty("chowOptions", out var leaked) || leaked.ValueKind == JsonValueKind.Null)
                    return;
                Assert.Equal(JsonValueKind.Array, leaked.ValueKind);
                Assert.Empty(leaked.EnumerateArray());
            });

        var beforeInvalidChoice = await host.SnapshotAsync(table);
        await table.Peer.UpdateAsync([new object[] { "claim", 0, new { action = "claim", type = "Chow", tileIds = new[] { 8, 20 } } }]);
        await table.Peer.BarrierAsync();
        AssertUnchanged(beforeInvalidChoice, await host.SnapshotAsync(table));

        await table.Peer.UpdateAsync([new object[] { "claim", 0, new { action = "claim", type = "Chow", tileIds = new[] { 12, 20 } } }]);
        await table.Peer.BarrierAsync();
        var meld = Assert.Single(table.State.Hands[0].Melds);
        Assert.Equal(MeldKind.Chow, meld.Kind);
        Assert.Equal(new[] { 12, 19, 20 }, meld.TileIds.OrderBy(t => t));
        Assert.Contains(8, table.State.Hands[0].ConcealedTiles);
        Assert.Contains(24, table.State.Hands[0].ConcealedTiles);
        Assert.DoesNotContain(12, table.State.Hands[0].ConcealedTiles);
        Assert.DoesNotContain(20, table.State.Hands[0].ConcealedTiles);
        AssertInventory(table.State);
    }

    private static Task SendAsync(WsPeer peer, int seat, string action, JsonElement available, int[]? tiles = null) =>
        SendAtVersionAsync(peer, seat, action, available.GetProperty("stateVersion").GetInt32(),
            available.GetProperty("gameId").GetString() ?? throw new InvalidOperationException("Availability omitted runtime game identity."), tiles);

    private static async Task<JsonElement> RejectedBarrierAsync(WsPeer peer, string? expectedReason = null)
    {
        var mark = peer.Frames.Count;
        var snapshot = await peer.BarrierAsync();
        var errors = peer.Frames.Skip(mark).SelectMany(Entries)
            .Where(entry => entry[0].GetString() == "actionRejected").ToArray();
        Assert.NotEmpty(errors);
        var reason = errors[^1][2].GetProperty("reason").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reason));
        if (expectedReason is not null) Assert.Equal(expectedReason, reason);
        return snapshot;
    }

    private static Task SendAtVersionAsync(WsPeer peer, int seat, string action, int version, string gameId, int[]? tiles = null)
    {
        var payload = new Dictionary<string, object?> { ["action"] = action, ["expectedVersion"] = version, ["gameId"] = gameId };
        if (tiles is not null) payload["tileIds"] = tiles;
        return peer.UpdateAsync([new object[] { "ownTurn", seat, payload }]);
    }

    private static JsonElement OwnTurn(JsonElement snapshot, int seat)
    {
        var value = Entry(snapshot, "ownTurn", seat);
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        Assert.True(value.TryGetProperty("stateVersion", out _), "Own-turn availability needs a concurrency version.");
        Assert.Equal(JsonValueKind.String, value.GetProperty("gameId").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(value.GetProperty("gameId").GetString()));
        return value;
    }

    private static JsonElement Entry(JsonElement snapshot, string kind, int seat)
    {
        var entries = Entries(snapshot).Where(e => e[0].GetString() == kind && e[1].ToString() == seat.ToString()).ToArray();
        Assert.NotEmpty(entries);
        return entries[^1][2].Clone();
    }

    private static IEnumerable<JsonElement> Entries(JsonElement snapshot) =>
        snapshot.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().ToArray() : [];

    private static void AssertOwnTurnHidden(JsonElement snapshot)
    {
        var entries = Entries(snapshot).Where(e => e[0].GetString() == "ownTurn").ToArray();
        Assert.Equal(new[] { "0", "1", "2", "3" }, entries.Select(entry => entry[1].ToString()).Distinct().OrderBy(key => key));
        Assert.All(entries, e => Assert.Equal(JsonValueKind.Null, e[2].ValueKind));
    }

    private static bool CanHuOn(ChangshaHandState hand, int tile)
    {
        var candidate = new ChangshaHandState
        {
            SeatIndex = hand.SeatIndex,
            ConcealedTiles = hand.ConcealedTiles.Append(tile).ToList(),
            Melds = hand.Melds.ToList(),
        };
        return new ChangshaWinDetector().Detect(candidate).IsWin;
    }

    private static void AssertUnchanged(ChangshaGameState before, ChangshaGameState after)
    {
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
    }
}
