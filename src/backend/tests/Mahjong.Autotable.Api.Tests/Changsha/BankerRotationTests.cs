using Mahjong.Autotable.Api.Changsha;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-H: Banker rotation per spec §6.2.
/// Dealer keeps seat ONLY if dealer wins; otherwise rotate counter-clockwise (including draws).
/// </summary>
public class BankerRotationTests
{
    private static ChangshaGameState NewEndHandState(int dealerSeat = 0)
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 1);
        state.DealerSeatIndex = dealerSeat;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == dealerSeat;
        state.Phase = ChangshaPhase.EndHand;
        return state;
    }

    [Fact, Trait("Category", "Changsha")]
    public void DealerWins_DealerRetainsSeat()
    {
        var state = NewEndHandState(dealerSeat: 0);
        state.CurrentWin = new WinResult
        {
            WinningSeatIndex = 0, SourceSeatIndex = 0, Method = WinMethod.SelfDraw,
            Pattern = WinPattern.Standard, WinningTileId = 0
        };

        ChangshaGameStateMachine.RotateBanker(state);

        Assert.Equal(0, state.DealerSeatIndex);
        Assert.True(state.Seats[0].IsDealer);
    }

    [Fact, Trait("Category", "Changsha")]
    public void NonDealerWins_DealerRotatesCounterClockwise()
    {
        var state = NewEndHandState(dealerSeat: 0);
        state.CurrentWin = new WinResult
        {
            WinningSeatIndex = 2, SourceSeatIndex = 1, Method = WinMethod.Discard,
            Pattern = WinPattern.Standard, WinningTileId = 0
        };

        ChangshaGameStateMachine.RotateBanker(state);

        Assert.Equal(1, state.DealerSeatIndex);
        Assert.True(state.Seats[1].IsDealer);
        Assert.False(state.Seats[0].IsDealer);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Draw_DealerRotatesCounterClockwise()
    {
        var state = NewEndHandState(dealerSeat: 1);
        // CurrentWin null → draw

        ChangshaGameStateMachine.RotateBanker(state);

        Assert.Equal(2, state.DealerSeatIndex);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Rotation_FromSeat3_WrapsToSeat0()
    {
        var state = NewEndHandState(dealerSeat: 3);
        ChangshaGameStateMachine.RotateBanker(state);
        Assert.Equal(0, state.DealerSeatIndex);
    }

    [Fact, Trait("Category", "Changsha")]
    public void RoundWind_AdvancesAfterFourHands_EastToSouth()
    {
        var state = NewEndHandState(dealerSeat: 0);
        state.HandInRound = 4;
        state.RoundNumber = 1;

        ChangshaGameStateMachine.RotateBanker(state); // 4→5 → wraps → round 2

        Assert.Equal(1, state.HandInRound);
        Assert.Equal(2, state.RoundNumber);
        Assert.Equal(Wind.South, state.RoundWind);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Game_Ends_AfterFourthRoundCompletes()
    {
        var state = NewEndHandState(dealerSeat: 0);
        state.HandInRound = 4;
        state.RoundNumber = 4;

        ChangshaGameStateMachine.RotateBanker(state);

        Assert.Equal(ChangshaPhase.EndGame, state.Phase);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Rotate_EmitsBankerRotatedEvent()
    {
        var state = NewEndHandState(dealerSeat: 0);

        var events = ChangshaGameStateMachine.RotateBanker(state);

        Assert.Contains(events, e => e.EventType == "banker-rotated");
    }
}
