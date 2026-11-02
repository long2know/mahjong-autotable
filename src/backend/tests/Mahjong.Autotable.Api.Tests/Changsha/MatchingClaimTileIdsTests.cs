using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// Contract coverage for explicit tile-id selection on matching-set claims.
/// Pung/Kong now accept caller-provided concealed tile ids (similar to Chow),
/// validated before claim resolution to keep replay + bot claim execution deterministic.
/// </summary>
public class MatchingClaimTileIdsTests
{
    private static ChangshaGameState BuildMatchingClaimWindow(
        IReadOnlyList<int> seat1Concealed,
        int discardTileId,
        TableClaimType claimType)
    {
        var state = NewGameDealtTo(seed: 23, botSeats: new[] { 1, 2, 3 });
        var dealer = state.DealerSeatIndex;

        state.Hands[1].ConcealedTiles.Clear();
        state.Hands[1].ConcealedTiles.AddRange(seat1Concealed);

        if (!state.Hands[dealer].ConcealedTiles.Contains(discardTileId))
            state.Hands[dealer].ConcealedTiles.Add(discardTileId);

        ChangshaGameStateMachine.Discard(state, dealer, discardTileId);
        Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
        Assert.NotNull(state.ClaimWindow);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == claimType);

        return state;
    }

    [Fact, Trait("Category", "Changsha")]
    public void Pung_TileIdsRespected_WhenSpecificMatchingCopiesProvided()
    {
        var w5c0 = Tid(Suit.Wan, 5, 0);
        var w5c1 = Tid(Suit.Wan, 5, 1);
        var w5c2 = Tid(Suit.Wan, 5, 2);
        var w5c3 = Tid(Suit.Wan, 5, 3);
        var filler = Tid(Suit.Tong, 1, 0);

        var state = BuildMatchingClaimWindow(
            seat1Concealed: new[] { w5c0, w5c1, w5c2, filler },
            discardTileId: w5c3,
            claimType: TableClaimType.Pung);

        ChangshaGameStateMachine.ResolveClaim(
            state,
            claimingSeatIndex: 1,
            claimType: TableClaimType.Pung,
            chosenTileIds: new[] { w5c1, w5c2 });

        var meld = state.Hands[1].Melds.Single();
        Assert.Equal(MeldKind.Pung, meld.Kind);
        Assert.Contains(w5c1, meld.TileIds);
        Assert.Contains(w5c2, meld.TileIds);
        Assert.Contains(w5c3, meld.TileIds);
        Assert.DoesNotContain(w5c0, meld.TileIds);
        Assert.Contains(w5c0, state.Hands[1].ConcealedTiles);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Pung_InvalidTileIds_WrongCount_ReturnsContractError()
    {
        var w5c0 = Tid(Suit.Wan, 5, 0);
        var w5c1 = Tid(Suit.Wan, 5, 1);
        var w5c2 = Tid(Suit.Wan, 5, 2);
        var w5c3 = Tid(Suit.Wan, 5, 3);

        var state = BuildMatchingClaimWindow(
            seat1Concealed: new[] { w5c0, w5c1, w5c2 },
            discardTileId: w5c3,
            claimType: TableClaimType.Pung);

        var ex = Assert.Throws<TableRuleException>(() =>
            ChangshaGameStateMachine.ResolveClaim(
                state,
                claimingSeatIndex: 1,
                claimType: TableClaimType.Pung,
                chosenTileIds: new[] { w5c1 }));

        Assert.Equal(TableActionErrorCodes.ClaimSelectionUnavailable, ex.Code);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Kong_InvalidTileIds_WrongCount_ReturnsContractError()
    {
        var w7c0 = Tid(Suit.Wan, 7, 0);
        var w7c1 = Tid(Suit.Wan, 7, 1);
        var w7c2 = Tid(Suit.Wan, 7, 2);
        var w7c3 = Tid(Suit.Wan, 7, 3);

        var state = BuildMatchingClaimWindow(
            seat1Concealed: new[] { w7c0, w7c1, w7c2 },
            discardTileId: w7c3,
            claimType: TableClaimType.Kong);

        var ex = Assert.Throws<TableRuleException>(() =>
            ChangshaGameStateMachine.ResolveClaim(
                state,
                claimingSeatIndex: 1,
                claimType: TableClaimType.Kong,
                chosenTileIds: new[] { w7c0, w7c1 }));

        Assert.Equal(TableActionErrorCodes.ClaimSelectionUnavailable, ex.Code);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Kong_InvalidTileIds_WrongLogical_ReturnsContractError()
    {
        var w7c0 = Tid(Suit.Wan, 7, 0);
        var w7c1 = Tid(Suit.Wan, 7, 1);
        var w7c2 = Tid(Suit.Wan, 7, 2);
        var w7c3 = Tid(Suit.Wan, 7, 3);
        var foreign = Tid(Suit.Tong, 7, 0);

        var state = BuildMatchingClaimWindow(
            seat1Concealed: new[] { w7c0, w7c1, w7c2, foreign },
            discardTileId: w7c3,
            claimType: TableClaimType.Kong);

        var ex = Assert.Throws<TableRuleException>(() =>
            ChangshaGameStateMachine.ResolveClaim(
                state,
                claimingSeatIndex: 1,
                claimType: TableClaimType.Kong,
                chosenTileIds: new[] { w7c0, w7c1, foreign }));

        Assert.Equal(TableActionErrorCodes.ClaimSelectionUnavailable, ex.Code);
    }
}
