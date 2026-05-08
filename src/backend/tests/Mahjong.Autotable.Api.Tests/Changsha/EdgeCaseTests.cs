using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-K: Edge Cases & Special Rules Tests
/// Tests P0 scenarios for boundary conditions and exceptional flows.
/// </summary>
public class EdgeCaseTests
{
    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void ConcealedKong_CannotBeRobbed_UnlikeExposedKong()
    {
        // K-05: Concealed kong cannot be claimed by another player for win
        // Only exposed kong (added to pung) can be robbed
        
        // Arrange: Player declares concealed kong (4 tiles from hand)
        // var stateMachine = new ChangshaGameStateMachine();
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameState(activeSeat: 1);
        
        // Act: Player 1 declares concealed kong of Dots-4
        // var kongState = stateMachine.ApplyKongDeclaration(state, seatIndex: 1, 
        //     tiles: FourOf_Dots4, isConcealed: true);
        
        // Act: Player 2 attempts to rob the concealed kong
        // var robClaim = new RobKongClaim(seatIndex: 2, targetKong: Dots_4_Kong);
        // var result = adjudicator.ValidateClaim(kongState, robClaim);
        
        // Assert: Robbery disallowed
        // Assert.False(result.IsValid);
        // Assert.Contains("concealed kong cannot be robbed", result.ErrorMessage);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void ExposedKong_AddedToPung_CanBeRobbed()
    {
        // K-05 positive case: Exposed kong (added to pung) can be robbed
        
        // Arrange: Player 1 has exposed pung, draws 4th matching tile and adds it
        // var stateMachine = new ChangshaGameStateMachine();
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameStateWithExposedPung(seatIndex: 1, pung: Characters_2_Pung);
        
        // Act: Player 1 adds 4th Characters-2 to form kong
        // var addedKongState = stateMachine.ApplyAddedKong(state, seatIndex: 1, tile: Characters_2);
        
        // Act: Player 3 claims rob kong for win
        // var robClaim = new RobKongClaim(seatIndex: 3, targetKong: Characters_2_Kong);
        // var result = adjudicator.ValidateClaim(addedKongState, robClaim);
        
        // Assert: Robbery allowed
        // Assert.True(result.IsValid);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void WallExhaustion_NoWinner_HandEndsInDraw()
    {
        // Wall exhausted, no win = draw game, no points exchanged
        
        // Arrange: Wall with 0 tiles, no winning conditions
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(wallTiles: 0, phase: TableTurnPhase.WallExhausted);
        
        // Act: Resolve hand
        // var result = stateMachine.ResolveDrawGame(state);
        
        // Assert: No score changes
        // Assert.All(result.ScoreChanges, sc => Assert.Equal(0, sc));
        
        // Assert: Dealer rotates counterclockwise
        // var nextState = stateMachine.StartNextHand(state, result);
        // Assert.Equal((state.DealerSeat + 1) % 4, nextState.DealerSeat);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void BigWin_258PairExemption_AllPungs_AcceptsAnyPair()
    {
        // K-08: Big Win patterns (All Pungs, Full Flush) exempt from 258 pair requirement
        
        // Arrange: All Pungs hand with pair of 3s (not 258)
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     // 4 pungs
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
        //     new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 7),
        //     new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9),
        //     // Pair of 3s (exempted by All Pungs)
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Big Win (All Pungs exempts 258 pair rule)
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.BigWin, result.WinType);
        // Assert.Contains(WinPattern.AllPungs, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void BigWin_FullFlush_AllowsChowMelds()
    {
        // K-09: Full Flush (all tiles one suit) can include chow melds
        
        // Arrange: Full Flush hand with chows (all Dots)
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     // All Dots suit
        //     new Tile(TileSuit.Dots, 1), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3), // Chow
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 5), // Chow
        //     new Tile(TileSuit.Dots, 6), new Tile(TileSuit.Dots, 7), new Tile(TileSuit.Dots, 8), // Chow
        //     new Tile(TileSuit.Dots, 8), new Tile(TileSuit.Dots, 8), new Tile(TileSuit.Dots, 8), // Pung
        //     new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5) // Pair
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Full Flush Big Win
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.BigWin, result.WinType);
        // Assert.Contains(WinPattern.FullFlush, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void InvalidTileIndex_ThrowsRuleException()
    {
        // Attempting to discard invalid tile index throws
        
        // Arrange: Player with 14 tiles (indices 0-13)
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 0, seatHandCounts: new[] { 14, 13, 13, 13 });
        
        // Act & Assert: Discard index 99 throws
        // var ex = Assert.Throws<TableRuleException>(() =>
        //     stateMachine.ApplyDiscard(state, seatIndex: 0, tileIndex: 99));
        
        // Assert.Equal(TableActionErrorCodes.InvalidTileIndex, ex.ErrorCode);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void DiscardFromWrongSeat_ThrowsRuleException()
    {
        // Attempting to discard when not active seat throws
        
        // Arrange: Active seat is 2
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 2, seatHandCounts: new[] { 13, 13, 14, 13 });
        
        // Act & Assert: Seat 1 tries to discard (not their turn)
        // var ex = Assert.Throws<TableRuleException>(() =>
        //     stateMachine.ApplyDiscard(state, seatIndex: 1, tileIndex: 0));
        
        // Assert.Equal(TableActionErrorCodes.NotYourTurn, ex.ErrorCode);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void MultipleBigWinPatterns_ScoresStack()
    {
        // G-10: If hand qualifies for multiple Big Win patterns, scores stack
        // Example: Full Flush + All Pungs
        
        // Arrange: Hand with both Full Flush and All Pungs
        // var detector = new ChangshaWinDetector();
        // var scoringService = new ChangshaScoringService();
        // var hand = new[] {
        //     // All Bamboo (Full Flush) AND all pungs (All Pungs)
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Bamboo, 3), new Tile(TileSuit.Bamboo, 3), new Tile(TileSuit.Bamboo, 3),
        //     new Tile(TileSuit.Bamboo, 6), new Tile(TileSuit.Bamboo, 6), new Tile(TileSuit.Bamboo, 6),
        //     new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9),
        //     new Tile(TileSuit.Bamboo, 7), new Tile(TileSuit.Bamboo, 7)
        // };
        
        // Act: Detect win
        // var winResult = detector.IsWinningHand(hand);
        
        // Assert: Multiple Big Win patterns detected
        // Assert.Contains(WinPattern.FullFlush, winResult.Patterns);
        // Assert.Contains(WinPattern.AllPungs, winResult.Patterns);
        
        // Act: Calculate score (implementation-specific: may stack or max)
        // var score = scoringService.CalculateBigWinScore(winResult.Patterns);
        
        // Assert: Score reflects multiple patterns (per spec G-10)
        // Assert.True(score > 6); // Base Big Win is 6, stacking should increase
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateVersion_OptimisticConcurrency_DetectsStaleUpdate()
    {
        // State version prevents stale updates
        
        // Arrange: State at version 5
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(stateVersion: 5, activeSeat: 1);
        
        // Act: Apply action expecting version 5
        // var newState = stateMachine.ApplyDiscard(state, seatIndex: 1, tileIndex: 0, expectedVersion: 5);
        
        // Assert: Success, version incremented to 6
        // Assert.Equal(6, newState.StateVersion);
        
        // Act: Attempt to apply action expecting stale version 5
        // var ex = Assert.Throws<TableRuleException>(() =>
        //     stateMachine.ApplyDiscard(newState, seatIndex: 2, tileIndex: 0, expectedVersion: 5));
        
        // Assert: Concurrency conflict detected
        // Assert.Equal(TableActionErrorCodes.StaleState, ex.ErrorCode);
    }
}
