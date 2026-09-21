using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class CurrentProfileKongGateParityTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationGateParity")]
    [InlineData("Pung", "concealedKong")]
    [InlineData("Pung", "addedKong")]
    [InlineData("Chow", "concealedKong")]
    public async Task RealPostClaimKong_AdvertisedGateMatchesExistingDedicatedRuntime(
        string claimType, string kongType)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        var incoming = ArrangeBeforeDraw(table, claimType == "Pung" ? "pung-no-draw" : "chow-options");
        if (claimType == "Chow")
            SwapOwnTile(table.State, table.Seat, 72, 43);
        ArrangeIncomingDiscard(table, incoming);
        await host.Runtime.DiscardAsync(table.GameId, 3, incoming.DrawTile);
        var claimFrame = await table.Peer.BarrierAsync();
        var claim = claimFrame.GetProperty("entries").EnumerateArray()
            .Last(entry => entry[0].GetString() == "claim" && entry[1].ToString() == "0")[2];
        Assert.Contains(claimType, claim.GetProperty("available").EnumerateArray().Select(value => value.GetString()));

        object command = claimType == "Chow"
            ? new { action = "claim", type = claimType, tileIds = new[] { 12, 20 } }
            : new { action = "claim", type = claimType };
        await table.Peer.UpdateAsync([new object[] { "claim", 0, command }]);
        var advertisedFrame = await table.Peer.BarrierAsync();
        var state = table.State;
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(0, state.ActiveSeatIndex);
        Assert.Null(state.LastDrawSeatIndex);
        Assert.Equal(14, state.Hands[0].ConcealedTiles.Count + 3 * state.Hands[0].Melds.Count);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
        AssertInventory(state);

        int[] ids = kongType == "concealedKong" ? [40, 41, 42, 43] : [18];
        if (kongType == "concealedKong")
            Assert.True(ChangshaGameStateMachine.CanDeclareConcealedKong(state, 0, 10));
        else
            Assert.True(ChangshaGameStateMachine.CanDeclareAddedKong(state, 0, 18));

        var own = advertisedFrame.GetProperty("entries").EnumerateArray()
            .Where(entry => entry[0].GetString() == "ownTurn" && entry[1].ToString() == "0")
            .Select(entry => entry[2]).LastOrDefault();
        var advertised = own.ValueKind == JsonValueKind.Object && (kongType == "concealedKong"
            ? own.GetProperty("concealedKongs").EnumerateArray().Any(option =>
                option.EnumerateArray().Select(tile => tile.GetInt32()).Order().SequenceEqual(ids))
            : own.GetProperty("addedKongs").EnumerateArray().Any(tile => tile.GetInt32() == ids[0]));
        if (own.ValueKind == JsonValueKind.Object)
            Assert.False(own.GetProperty("hu").GetBoolean());

        var beforeWall = state.Wall.ToArray();
        var beforeBackDrawn = state.WallBackDrawn;
        // This is the existing dedicated runtime/legacy entry contract, not a new WS action.
        await host.Runtime.DeclareKongAsync(table.GameId, 0, ids);
        if (state.ClaimWindow is { IsKongRobbing: true } robbery)
        {
            foreach (var seat in robbery.Opportunities.Select(opportunity => opportunity.SeatIndex).Distinct().ToArray())
                await host.Runtime.PassAsync(table.GameId, seat);
        }
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(beforeWall.Length - 1, state.Wall.Count);
        Assert.Equal(beforeBackDrawn + 1, state.WallBackDrawn);
        Assert.Equal(beforeWall[^1], state.Hands[0].ConcealedTiles[^1]);
        Assert.Contains(state.Hands[0].Melds, meld =>
            meld.Kind == (kongType == "concealedKong" ? MeldKind.ConcealedKong : MeldKind.AddedKong));
        AssertInventory(state);
        output.WriteLine($"Actual {claimType}, no draw, ready14: pure candidate and dedicated {kongType} accepted; advertised={advertised}.");

        Assert.True(advertised,
            "Current-profile retention parity: metadata hid a Kong accepted by the existing dedicated runtime on the same genuine ready post-claim state. This is not a self-draw-Hu entitlement.");
    }

    private static void SwapOwnTile(ChangshaGameState state, int seat, int oldTile, int newTile)
    {
        var target = state.Hands[seat].ConcealedTiles;
        var targetIndex = target.IndexOf(oldTile);
        Assert.InRange(targetIndex, 0, target.Count - 1);
        Assert.DoesNotContain(newTile, target);
        var owner = state.Hands.FirstOrDefault(hand => hand.ConcealedTiles.Contains(newTile));
        if (owner is not null)
        {
            var index = owner.ConcealedTiles.IndexOf(newTile);
            owner.ConcealedTiles[index] = oldTile;
        }
        else
        {
            var index = state.Wall.IndexOf(newTile);
            Assert.InRange(index, 0, state.Wall.Count - 1);
            state.Wall[index] = oldTile;
        }
        target[targetIndex] = newTile;
        AssertInventory(state);
    }
}
