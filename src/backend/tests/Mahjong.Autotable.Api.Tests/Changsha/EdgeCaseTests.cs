using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-K: Edge cases & special rules.
/// </summary>
public class EdgeCaseTests
{
    [Fact, Trait("Category", "Changsha")]
    public void ConcealedKong_CannotBeRobbed_ClaimAdjudicatorDoesNotProduceRobOpportunity()
    {
        // The adjudicator only enumerates discard-window claims; concealed kong never reaches it.
        // Build a hand state with a 4-of-a-kind in seat 1; seat 0 discards an unrelated tile.
        var hands = Enumerable.Range(0, 4).Select(i => new ChangshaHandState { SeatIndex = i }).ToList();
        hands[1].ConcealedTiles.AddRange(Tiles(
            (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4)));

        // Seat 0 discards Wan-1 (no relation to Tong-4); no Hu/Kong/Pung opportunities should arise.
        var opps = new ClaimAdjudicator().GetOpportunities(0, Tid(Suit.Wan, 1, 0), hands);
        Assert.Empty(opps);
    }

    [Fact, Trait("Category", "Changsha")]
    public void WallExhausted_NoWinner_HandEndsInDraw()
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 1);
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(1));
        ChangshaGameStateMachine.Deal(state);

        // Force wall exhaustion.
        state.Wall.Clear();
        state.Phase = ChangshaPhase.WallExhausted;

        ChangshaGameStateMachine.HandleWallExhausted(state);

        Assert.Equal(ChangshaPhase.EndHand, state.Phase);
        Assert.Null(state.CurrentWin);
    }

    [Fact, Trait("Category", "Changsha")]
    public void BigWin_AllPungs_ExemptFrom258PairRule()
    {
        // AllPungs hand with pair of 3 (NOT a 258 rank).
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9),
            (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4),
            (Suit.Tiao, 7), (Suit.Tiao, 7), (Suit.Tiao, 7),
            (Suit.Tiao, 3), (Suit.Tiao, 3));

        var result = new ChangshaWinDetector().Detect(hand);
        Assert.True(result.IsWin);
        Assert.Equal(WinPattern.AllPungs, result.Pattern);
    }

    [Fact, Trait("Category", "Changsha")]
    public void BigWin_FullFlush_AllowsChowMelds_NoPungRequirement()
    {
        // Single-suit hand with chows + non-258 pair.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 2), (Suit.Wan, 3), (Suit.Wan, 4),
            (Suit.Wan, 4), (Suit.Wan, 4),
            (Suit.Wan, 6), (Suit.Wan, 7), (Suit.Wan, 8),
            (Suit.Wan, 7), (Suit.Wan, 8), (Suit.Wan, 9));

        var result = new ChangshaWinDetector().Detect(hand);
        Assert.True(result.IsWin);
        Assert.True(result.IsFullFlush);
    }

    [Fact, Trait("Category", "Changsha")]
    public void DiscardFromWrongSeat_Throws()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 11);
        var notDealer = (state.DealerSeatIndex + 1) % 4;
        Assert.Throws<InvalidOperationException>(() =>
            ChangshaGameStateMachine.Discard(state, notDealer, state.Hands[notDealer].ConcealedTiles[0]));
    }

    [Fact, Trait("Category", "Changsha")]
    public void DiscardTileNotHeld_Throws()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 11);
        var dealer = state.DealerSeatIndex;
        var foreign = Enumerable.Range(0, 108).First(t => !state.Hands[dealer].ConcealedTiles.Contains(t));
        Assert.Throws<InvalidOperationException>(() =>
            ChangshaGameStateMachine.Discard(state, dealer, foreign));
    }

    [Fact(Skip = "Robbing exposed kong (抢杠胡) deferred to v2"), Trait("Category", "Changsha")]
    public void ExposedKong_CanBeRobbed_DeferredToV2() { }

    [Fact(Skip = "Stacked big-win pattern multipliers deferred to v2"), Trait("Category", "Changsha")]
    public void MultipleBigWinPatterns_ScoresStack_DeferredToV2() { }

    [Fact(Skip = "Optimistic concurrency on StateVersion deferred — no expectedVersion API yet"), Trait("Category", "Changsha")]
    public void StateVersion_OptimisticConcurrency_DeferredToV2() { }
}
