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
        Assert.Equal(TableTurnPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(1, state.DrawNumber);
        Assert.Equal(1, state.StateVersion);
        Assert.Equal(0, state.ActionSequence);
        Assert.Equal(TableStateEngine.RngAlgorithmId, state.Metadata.AlgorithmId);
        Assert.False(string.IsNullOrWhiteSpace(state.Integrity.StateHash));
        Assert.Equal(14, state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Count);
        Assert.All(state.Hands.Where(hand => hand.SeatIndex != 0), hand => Assert.Equal(13, hand.Tiles.Count));
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void CreateInitialState_WithSameSeed_IsDeterministic()
    {
        var first = _engine.CreateInitialState(seed: 44);
        var second = _engine.CreateInitialState(seed: 44);
        var third = _engine.CreateInitialState(seed: 45);

        Assert.Equal(first.Metadata.Seed, second.Metadata.Seed);
        Assert.Equal(first.Wall, second.Wall);
        Assert.Equal(
            first.Hands.SelectMany(hand => hand.Tiles),
            second.Hands.SelectMany(hand => hand.Tiles));
        Assert.Equal(first.Integrity.StateHash, second.Integrity.StateHash);
        Assert.NotEqual(first.Wall, third.Wall);
    }

    [Fact]
    public void ApplyHumanDiscard_WhenSeatIsNotHuman_RejectsAction()
    {
        var state = _engine.CreateInitialState();
        state.ActiveSeat = 1;

        var tileId = state.Hands.Single(hand => hand.SeatIndex == 1).Tiles[0];

        var exception = Assert.Throws<TableRuleException>(() => _engine.ApplyHumanDiscard(state, 1, tileId));
        Assert.Equal(TableActionErrorCodes.NotActiveSeat, exception.Code);
    }

    [Fact]
    public void ApplyHumanDiscard_WhenTileNotOwned_RejectsAction()
    {
        var state = _engine.CreateInitialState(seed: 11);
        var otherTile = state.Hands.Single(hand => hand.SeatIndex == 1).Tiles[0];

        var exception = Assert.Throws<TableRuleException>(() => _engine.ApplyHumanDiscard(state, 0, otherTile));
        Assert.Equal(TableActionErrorCodes.TileNotInHand, exception.Code);
    }

    [Fact]
    public void ApplyHumanDiscard_IncrementsStateVersionAndActionSequence()
    {
        var state = _engine.CreateInitialState(seed: 51);
        var initialStateVersion = state.StateVersion;
        var initialActionSequence = state.ActionSequence;
        var tileId = state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Min();

        var result = _engine.ApplyHumanDiscard(state, 0, tileId);

        Assert.NotNull(result.DrawAction);
        Assert.Equal(initialStateVersion + 2, state.StateVersion);
        Assert.Equal(initialActionSequence + 2, state.ActionSequence);
        Assert.Equal(initialActionSequence + 1, result.DiscardAction.Sequence);
        Assert.Equal(initialActionSequence + 2, result.DrawAction!.Sequence);
        Assert.Equal(result.DrawAction.Sequence, state.LastAction!.Sequence);
    }

    [Fact]
    public void ApplyHumanDiscard_ChangesIntegrityHash()
    {
        var state = _engine.CreateInitialState(seed: 72);
        var beforeHash = state.Integrity.StateHash;
        var tileId = state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Min();

        _engine.ApplyHumanDiscard(state, 0, tileId);

        Assert.NotEqual(beforeHash, state.Integrity.StateHash);
        Assert.False(string.IsNullOrWhiteSpace(state.Integrity.StateHash));
    }

    [Fact]
    public void NormalizePersistedState_AlignsVersionAndRepairsLegacyFields()
    {
        var state = _engine.CreateInitialState(seed: 88);
        state.StateVersion = 0;
        state.Metadata.AlgorithmId = string.Empty;
        state.Integrity.StateHash = string.Empty;

        _engine.NormalizePersistedState(state, 9);

        Assert.Equal(9, state.StateVersion);
        Assert.Equal(TableStateEngine.RngAlgorithmId, state.Metadata.AlgorithmId);
        Assert.False(string.IsNullOrWhiteSpace(state.Integrity.StateHash));
    }

    [Fact]
    public void ApplyHumanDiscard_AndBotAdvance_PreserveTileConservation()
    {
        var state = _engine.CreateInitialState(seed: 67);
        var humanTile = state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Min();

        var humanResult = _engine.ApplyHumanDiscard(state, 0, humanTile);
        Assert.NotNull(humanResult.DrawAction);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));

        var botResult = _engine.AdvanceBots(state, 20);
        Assert.NotEmpty(botResult.Actions);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void AdvanceBots_WhenHumanTurn_DoesNotMutateState()
    {
        var state = _engine.CreateInitialState();

        var result = _engine.AdvanceBots(state, 8);

        Assert.Empty(result.Actions);
        Assert.Equal(BotAdvanceStopReason.HumanTurn, result.StopReason);
        Assert.Equal(1, state.DrawNumber);
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(0, state.ActiveSeat);
    }

    [Fact]
    public void AdvanceBots_WhenMaxActionsCannotFitFullTurn_DoesNotPartiallyAdvance()
    {
        var state = _engine.CreateInitialState(seed: 22);
        state.ActiveSeat = 1;

        var result = _engine.AdvanceBots(state, 1);

        Assert.Equal(BotAdvanceStopReason.MaxActionsReached, result.StopReason);
        Assert.Empty(result.Actions);
        Assert.Equal(1, state.ActiveSeat);
    }

    [Fact]
    public void AdvanceBots_WhenWallIsExhausted_Halts()
    {
        var state = _engine.CreateInitialState(seed: 33);
        state.ActiveSeat = 1;
        ExhaustWallPreservingTileConservation(state);

        var result = _engine.AdvanceBots(state, 8);

        Assert.Equal(BotAdvanceStopReason.WallExhausted, result.StopReason);
        Assert.Single(result.Actions);
        Assert.Equal("discard", result.Actions[0].ActionType);
        Assert.Equal(TableTurnPhase.WallExhausted, state.Phase);
    }

    [Fact]
    public void CreateInitialState_AllBots_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _engine.CreateInitialState([0, 1, 2, 3]));
    }

    private static int CountTrackedTiles(TableGameState state)
    {
        var handTiles = state.Hands.Sum(hand => hand.Tiles.Count);
        var discardTiles = state.DiscardPile.Count;
        return state.Wall.Count + handTiles + discardTiles;
    }

    private static void ExhaustWallPreservingTileConservation(TableGameState state)
    {
        while (state.Wall.Count > 0)
        {
            var lastIndex = state.Wall.Count - 1;
            var tileId = state.Wall[lastIndex];
            state.Wall.RemoveAt(lastIndex);
            state.DiscardPile.Add(new TableDiscard
            {
                SeatIndex = 0,
                TileId = tileId,
                TurnNumber = 0,
                OccurredUtc = DateTime.UnixEpoch
            });
        }
    }
}
