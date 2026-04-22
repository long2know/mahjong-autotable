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
        Assert.Equal(4, state.ExposedMelds.Count);
        Assert.All(state.ExposedMelds, seatMelds => Assert.Empty(seatMelds.Melds));
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
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);

        var result = _engine.ApplyHumanDiscard(state, 0, tileId);

        Assert.NotNull(result.DrawAction);
        Assert.Equal(initialStateVersion + 2, state.StateVersion);
        Assert.Equal(initialActionSequence + 2, state.ActionSequence);
        Assert.Equal(initialActionSequence + 1, result.DiscardAction.Sequence);
        Assert.Equal(initialActionSequence + 2, result.DrawAction!.Sequence);
        Assert.False(string.IsNullOrWhiteSpace(result.DiscardAction.StateHash));
        Assert.False(string.IsNullOrWhiteSpace(result.DrawAction.StateHash));
        Assert.Equal(result.DrawAction.Sequence, state.LastAction!.Sequence);
        Assert.Equal(result.DrawAction.StateHash, state.Integrity.StateHash);
    }

    [Fact]
    public void ApplyHumanDiscard_ChangesIntegrityHash()
    {
        var state = _engine.CreateInitialState(seed: 72);
        var beforeHash = state.Integrity.StateHash;
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);

        _engine.ApplyHumanDiscard(state, 0, tileId);

        Assert.NotEqual(beforeHash, state.Integrity.StateHash);
        Assert.False(string.IsNullOrWhiteSpace(state.Integrity.StateHash));
    }

    [Fact]
    public void ApplyHumanDiscard_ClaimWindowSelectsPungOverChow()
    {
        var state = _engine.CreateInitialState(seed: 140);

        const int discardLogicalTile = 4;
        var discardTileId = discardLogicalTile * 4;
        var pungTileOne = discardTileId + 1;
        var pungTileTwo = discardTileId + 2;
        var spareCopy = discardTileId + 3;
        var chowLeftTile = 3 * 4;
        var chowRightTile = 5 * 4;

        ForceTilesIntoSeat(state, 0, discardTileId);
        ForceTilesIntoSeat(state, 1, chowLeftTile, chowRightTile, spareCopy);
        ForceTilesIntoSeat(state, 2, pungTileOne, pungTileTwo);

        var result = _engine.ApplyHumanDiscard(state, 0, discardTileId);

        Assert.Null(result.DrawAction);
        Assert.Equal(TableTurnPhase.AwaitingClaimResolution, state.Phase);
        var claimWindow = Assert.IsType<TableClaimWindowState>(state.ClaimWindow);
        Assert.Equal(result.DiscardAction.Sequence, claimWindow.SourceActionSequence);
        Assert.Equal(TableStateEngine.ClaimPrecedencePolicy, claimWindow.PrecedencePolicy);
        Assert.Contains(claimWindow.Opportunities, opportunity =>
            opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Chow);
        Assert.Contains(claimWindow.Opportunities, opportunity =>
            opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Pung);
        var selected = Assert.IsType<TableClaimOpportunity>(claimWindow.SelectedOpportunity);
        Assert.Equal(2, selected.SeatIndex);
        Assert.Equal(TableClaimType.Pung, selected.ClaimType);
    }

    [Fact]
    public void ApplyHumanDiscard_ClaimWindowSelectsKongOverChow()
    {
        var state = _engine.CreateInitialState(seed: 141);

        const int discardLogicalTile = 6;
        var discardTileId = discardLogicalTile * 4;
        var kongTileOne = discardTileId + 1;
        var kongTileTwo = discardTileId + 2;
        var kongTileThree = discardTileId + 3;
        var chowLeftTile = 5 * 4;
        var chowRightTile = 7 * 4;

        ForceTilesIntoSeat(state, 0, discardTileId);
        ForceTilesIntoSeat(state, 1, chowLeftTile, chowRightTile);
        ForceTilesIntoSeat(state, 2, kongTileOne, kongTileTwo, kongTileThree);

        var result = _engine.ApplyHumanDiscard(state, 0, discardTileId);

        Assert.Null(result.DrawAction);
        Assert.Equal(TableTurnPhase.AwaitingClaimResolution, state.Phase);
        var claimWindow = Assert.IsType<TableClaimWindowState>(state.ClaimWindow);
        Assert.Contains(claimWindow.Opportunities, opportunity =>
            opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Chow);
        Assert.Contains(claimWindow.Opportunities, opportunity =>
            opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Kong);
        var selected = Assert.IsType<TableClaimOpportunity>(claimWindow.SelectedOpportunity);
        Assert.Equal(2, selected.SeatIndex);
        Assert.Equal(TableClaimType.Kong, selected.ClaimType);
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
    public void ReplayFromSeed_WithAcceptedActions_ReproducesIntegrityHash()
    {
        var state = _engine.CreateInitialState(seed: 104);
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);
        _engine.ApplyHumanDiscard(state, 0, tileId);
        _engine.AdvanceBots(state, 12);

        var replay = _engine.ReplayFromSeed(state);

        Assert.Equal(state.Integrity.StateHash, replay.Integrity.StateHash);
        Assert.Equal(state.StateVersion, replay.StateVersion);
        Assert.Equal(state.ActionSequence, replay.ActionSequence);
    }

    [Fact]
    public void ReplayFromSeed_WhenActionLogIsTampered_DoesNotMatchIntegrityHash()
    {
        var state = _engine.CreateInitialState(seed: 121);
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);
        _engine.ApplyHumanDiscard(state, 0, tileId);
        state.ActionLog[0].Detail = "tampered-action";
        _engine.NormalizePersistedState(state, state.StateVersion);

        var replay = _engine.ReplayFromSeed(state);

        Assert.NotEqual(state.Integrity.StateHash, replay.Integrity.StateHash);
    }

    [Fact]
    public void VerifyReplayIntegrity_WhenStateIsUntampered_ReturnsMatch()
    {
        var state = _engine.CreateInitialState(seed: 131);
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);
        _engine.ApplyHumanDiscard(state, 0, tileId);
        _engine.AdvanceBots(state, 4);

        var verification = _engine.VerifyReplayIntegrity(state);

        Assert.True(verification.IntegrityMatch);
        Assert.Equal(verification.ExpectedStateHash, verification.ReplayedStateHash);
        Assert.Equal(state.StateVersion, verification.ReplayedStateVersion);
        Assert.Equal(state.ActionSequence, verification.ReplayedActionSequence);
    }

    [Fact]
    public void VerifyReplayIntegrity_WhenStateIsTampered_ReturnsMismatch()
    {
        var state = _engine.CreateInitialState(seed: 132);
        var tileId = FindDiscardTileWithoutClaimWindow(state, 0);
        _engine.ApplyHumanDiscard(state, 0, tileId);
        state.ActionLog[0].Detail = "tampered-action";
        _engine.NormalizePersistedState(state, state.StateVersion);

        var verification = _engine.VerifyReplayIntegrity(state);

        Assert.False(verification.IntegrityMatch);
        Assert.NotEqual(verification.ExpectedStateHash, verification.ReplayedStateHash);
    }

    [Fact]
    public void ApplyHumanDiscard_AndBotAdvance_PreserveTileConservation()
    {
        var state = _engine.CreateInitialState(seed: 67);
        var humanTile = FindDiscardTileWithoutClaimWindow(state, 0);

        var humanResult = _engine.ApplyHumanDiscard(state, 0, humanTile);
        Assert.NotNull(humanResult.DrawAction);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));

        var botResult = _engine.AdvanceBots(state, 20);
        Assert.NotEmpty(botResult.Actions);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void ResolveClaimWindow_WithPass_ClearsWindowAndContinuesFlow()
    {
        var state = _engine.CreateInitialState(seed: 240);

        const int discardLogicalTile = 8;
        var discardTileId = discardLogicalTile * 4;
        var pungTileOne = discardTileId + 1;
        var pungTileTwo = discardTileId + 2;

        ForceTilesIntoSeat(state, 0, discardTileId);
        ForceTilesIntoSeat(state, 2, pungTileOne, pungTileTwo);

        _ = _engine.ApplyHumanDiscard(state, 0, discardTileId);

        var result = _engine.ResolveClaimWindow(state, TableClaimResolutionDecisionValues.Pass);

        Assert.Equal(TableClaimResolutionDecisionValues.Pass, result.AppliedDecision);
        Assert.Equal("claim-resolve-pass", result.ResolutionAction.ActionType);
        Assert.NotNull(result.DrawAction);
        Assert.Null(state.ClaimWindow);
        Assert.Equal(1, state.ActiveSeat);
        Assert.Equal(TableTurnPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void ResolveClaimWindow_WithTakeSelected_MovesControlToSelectedSeat()
    {
        var state = _engine.CreateInitialState(seed: 241);

        const int discardLogicalTile = 3;
        var discardTileId = discardLogicalTile * 4;
        var pungTileOne = discardTileId + 1;
        var pungTileTwo = discardTileId + 2;
        var spareCopy = discardTileId + 3;

        ForceTilesIntoSeat(state, 0, discardTileId);
        ForceTilesIntoSeat(state, 1, spareCopy);
        ForceTilesIntoSeat(state, 2, pungTileOne, pungTileTwo);

        _ = _engine.ApplyHumanDiscard(state, 0, discardTileId);

        var result = _engine.ResolveClaimWindow(state, TableClaimResolutionDecisionValues.TakeSelected);

        Assert.Equal(TableClaimResolutionDecisionValues.TakeSelected, result.AppliedDecision);
        Assert.Equal("claim-resolve-take-selected", result.ResolutionAction.ActionType);
        Assert.Null(result.DrawAction);
        Assert.Null(state.ClaimWindow);
        Assert.Equal(2, state.ActiveSeat);
        Assert.Equal(TableTurnPhase.AwaitingDiscard, state.Phase);
        Assert.DoesNotContain(discardTileId, state.Hands.Single(hand => hand.SeatIndex == 2).Tiles);
        Assert.DoesNotContain(pungTileOne, state.Hands.Single(hand => hand.SeatIndex == 2).Tiles);
        Assert.DoesNotContain(pungTileTwo, state.Hands.Single(hand => hand.SeatIndex == 2).Tiles);
        var seatMelds = state.ExposedMelds.Single(seatMeld => seatMeld.SeatIndex == 2).Melds;
        var meld = Assert.Single(seatMelds);
        Assert.Equal(TableClaimType.Pung, meld.ClaimType);
        Assert.Equal(3, meld.TileIds.Count);
        Assert.Contains(discardTileId, meld.TileIds);
        Assert.Equal(11, state.Hands.Single(hand => hand.SeatIndex == 2).Tiles.Count);
        Assert.DoesNotContain(state.DiscardPile, discard => discard.TileId == discardTileId && discard.SeatIndex == 0);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void ResolveClaimWindow_WithTakeSelectedKong_CreatesMeldAndDrawsSupplementalTile()
    {
        var state = _engine.CreateInitialState(seed: 242);

        const int discardLogicalTile = 12;
        var discardTileId = discardLogicalTile * 4;
        var kongTileOne = discardTileId + 1;
        var kongTileTwo = discardTileId + 2;
        var kongTileThree = discardTileId + 3;

        ForceTilesIntoSeat(state, 0, discardTileId);
        ForceTilesIntoSeat(state, 2, kongTileOne, kongTileTwo, kongTileThree);

        _ = _engine.ApplyHumanDiscard(state, 0, discardTileId);
        var wallBefore = state.Wall.Count;
        var handBefore = state.Hands.Single(hand => hand.SeatIndex == 2).Tiles.Count;

        var result = _engine.ResolveClaimWindow(state, TableClaimResolutionDecisionValues.TakeSelected);

        Assert.Equal(TableClaimResolutionDecisionValues.TakeSelected, result.AppliedDecision);
        Assert.Equal("claim-resolve-take-selected", result.ResolutionAction.ActionType);
        Assert.NotNull(result.DrawAction);
        Assert.Equal("draw", result.DrawAction!.ActionType);
        Assert.Equal(2, state.ActiveSeat);
        Assert.Equal(TableTurnPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(wallBefore - 1, state.Wall.Count);
        Assert.Equal(handBefore - 2, state.Hands.Single(hand => hand.SeatIndex == 2).Tiles.Count);
        var seatMelds = state.ExposedMelds.Single(seatMeld => seatMeld.SeatIndex == 2).Melds;
        var meld = Assert.Single(seatMelds);
        Assert.Equal(TableClaimType.Kong, meld.ClaimType);
        Assert.Equal(4, meld.TileIds.Count);
        Assert.Contains(discardTileId, meld.TileIds);
        Assert.DoesNotContain(state.DiscardPile, discard => discard.TileId == discardTileId && discard.SeatIndex == 0);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void ReplayFromSeed_WithTakeSelectedClaimMeld_ReproducesIntegrityHash()
    {
        var state = _engine.CreateInitialState(seed: 243);
        var discardTileId = FindDiscardTileWithClaimWindow(state, 0);
        _ = _engine.ApplyHumanDiscard(state, 0, discardTileId);
        _ = _engine.ResolveClaimWindow(state, TableClaimResolutionDecisionValues.TakeSelected);

        var replay = _engine.ReplayFromSeed(state);

        Assert.Equal(state.Integrity.StateHash, replay.Integrity.StateHash);
        Assert.Equal(state.StateVersion, replay.StateVersion);
        Assert.Equal(state.ActionSequence, replay.ActionSequence);
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
    public void AdvanceBots_HeuristicDoesNotBreakPairsByDefault()
    {
        var state = _engine.CreateInitialState(seed: 77);
        state.ActiveSeat = 1;

        ForceTilesIntoSeat(state, 1, 0, 1);

        var result = _engine.AdvanceBots(state, 2);

        var discardAction = Assert.Single(result.Actions, action => action.ActionType == "discard");
        Assert.NotNull(discardAction.TileId);
        Assert.DoesNotContain(discardAction.TileId!.Value, new[] { 0, 1 });
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
    public void AdvanceBotsUntilHumanTurnOrWallExhausted_FromBotTurn_AdvancesToHuman()
    {
        var state = _engine.CreateInitialState(seed: 41);
        state.ActiveSeat = 1;

        var result = _engine.AdvanceBotsUntilHumanTurnOrWallExhausted(state);

        Assert.Equal(BotAdvanceStopReason.HumanTurn, result.StopReason);
        Assert.Equal(6, result.Actions.Count);
        Assert.Equal(0, state.ActiveSeat);
        Assert.Equal(TableTurnPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(TableStateEngine.TotalTiles, CountTrackedTiles(state));
    }

    [Fact]
    public void AdvanceBotsUntilHumanTurnOrWallExhausted_WhenWallIsExhausted_Halts()
    {
        var state = _engine.CreateInitialState(seed: 42);
        state.ActiveSeat = 1;
        ExhaustWallPreservingTileConservation(state);

        var result = _engine.AdvanceBotsUntilHumanTurnOrWallExhausted(state);

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
        var meldTiles = state.ExposedMelds.Sum(seatMelds => seatMelds.Melds.Sum(meld => meld.TileIds.Count));
        var discardTiles = state.DiscardPile.Count;
        return state.Wall.Count + handTiles + meldTiles + discardTiles;
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

    private static void ForceTilesIntoSeat(TableGameState state, int seatIndex, params int[] tileIds)
    {
        var targetHand = state.Hands.Single(hand => hand.SeatIndex == seatIndex);
        var requestedTiles = tileIds.ToHashSet();
        foreach (var tileId in tileIds)
        {
            if (targetHand.Tiles.Contains(tileId))
            {
                continue;
            }

            var replacementIndex = targetHand.Tiles.FindIndex(tile => !requestedTiles.Contains(tile));
            if (replacementIndex < 0)
            {
                throw new InvalidOperationException($"Cannot replace tile in seat {seatIndex} hand.");
            }

            var replacementTile = targetHand.Tiles[replacementIndex];

            var sourceHand = state.Hands
                .Where(hand => hand.SeatIndex != seatIndex)
                .FirstOrDefault(hand => hand.Tiles.Contains(tileId));
            if (sourceHand is not null)
            {
                var sourceIndex = sourceHand.Tiles.IndexOf(tileId);
                sourceHand.Tiles[sourceIndex] = replacementTile;
                targetHand.Tiles[replacementIndex] = tileId;
                continue;
            }

            var wallIndex = state.Wall.IndexOf(tileId);
            if (wallIndex >= 0)
            {
                state.Wall[wallIndex] = replacementTile;
                targetHand.Tiles[replacementIndex] = tileId;
                continue;
            }

            throw new InvalidOperationException($"Tile {tileId} not found in tracked state.");
        }
    }

    private int FindDiscardTileWithoutClaimWindow(TableGameState state, int seatIndex)
    {
        var serializer = new TableStateSerializer();
        var hand = state.Hands.Single(currentHand => currentHand.SeatIndex == seatIndex);
        foreach (var tileId in hand.Tiles.OrderBy(tile => tile))
        {
            var probe = serializer.Deserialize(serializer.Serialize(state));
            var result = _engine.ApplyHumanDiscard(probe, seatIndex, tileId);
            if (probe.ClaimWindow is null && result.DrawAction is not null)
            {
                return tileId;
            }
        }

        throw new InvalidOperationException($"Seat {seatIndex} has no discard tile that avoids opening a claim window.");
    }

    private int FindDiscardTileWithClaimWindow(TableGameState state, int seatIndex)
    {
        var serializer = new TableStateSerializer();
        var hand = state.Hands.Single(currentHand => currentHand.SeatIndex == seatIndex);
        foreach (var tileId in hand.Tiles.OrderBy(tile => tile))
        {
            var probe = serializer.Deserialize(serializer.Serialize(state));
            _ = _engine.ApplyHumanDiscard(probe, seatIndex, tileId);
            if (probe.ClaimWindow?.SelectedOpportunity is not null)
            {
                return tileId;
            }
        }

        throw new InvalidOperationException($"Seat {seatIndex} has no discard tile that opens a claim window.");
    }
}
