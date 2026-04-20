using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests;

public class TableStateEngineTests
{
    private readonly TableStateEngine _engine = new();

    [Fact]
    public void CreateInitialState_DefaultLayout_CreatesOneHumanAndThreeBots()
    {
        var state = _engine.CreateInitialState();

        Assert.Equal(4, state.Seats.Count);
        Assert.Equal(TableSeatType.Human, state.Seats[0].SeatType);
        Assert.All(state.Seats.Skip(1), seat => Assert.Equal(TableSeatType.Bot, seat.SeatType));
        Assert.Equal(0, state.ActiveSeat);
        Assert.Equal(1, state.TurnNumber);
    }

    [Fact]
    public void AdvanceBots_WhenHumanTurn_DoesNotMutateState()
    {
        var state = _engine.CreateInitialState();

        var result = _engine.AdvanceBots(state, 8);

        Assert.Empty(result.Actions);
        Assert.Equal(BotAdvanceStopReason.HumanTurn, result.StopReason);
        Assert.Equal(0, state.DrawNumber);
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(0, state.ActiveSeat);
    }

    [Fact]
    public void AdvanceBots_WhenBotTurn_AppliesDeterministicDrawDiscardUntilHumanTurn()
    {
        var state = _engine.CreateInitialState();
        state.ActiveSeat = 1;

        var result = _engine.AdvanceBots(state, 8);

        Assert.Equal(BotAdvanceStopReason.HumanTurn, result.StopReason);
        Assert.Equal(6, result.Actions.Count);
        Assert.Equal("draw", result.Actions[0].ActionType);
        Assert.Equal("draw-1-1", result.Actions[0].Detail);
        Assert.Equal("discard", result.Actions[1].ActionType);
        Assert.Equal("tile-3", result.Actions[1].Detail);
        Assert.Equal(3, state.DrawNumber);
        Assert.Equal(4, state.TurnNumber);
        Assert.Equal(0, state.ActiveSeat);
    }

    [Fact]
    public void CreateInitialState_AllBots_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _engine.CreateInitialState([0, 1, 2, 3]));
    }

    [Fact]
    public void AdvanceBots_WhenMaxActionsCannotFitFullTurn_DoesNotPartiallyAdvance()
    {
        var state = _engine.CreateInitialState();
        state.ActiveSeat = 1;

        var result = _engine.AdvanceBots(state, 1);

        Assert.Equal(BotAdvanceStopReason.MaxActionsReached, result.StopReason);
        Assert.Empty(result.Actions);
        Assert.Equal(1, state.ActiveSeat);
        Assert.Equal(0, state.DrawNumber);
        Assert.Equal(1, state.TurnNumber);
    }
}
