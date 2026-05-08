using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-D: Turn Flow (Draw / Discard) Tests
/// Tests P0 scenarios for basic turn mechanics and wall exhaustion.
/// </summary>
public class TurnFlowTests
{
    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void TurnDraw_PlayerDrawsFromWall_HandIncreasesBy1()
    {
        // D-01: Active player draws one tile from draw wall at start of turn
        
        // Arrange: Player with 13 tiles, wall with tiles remaining
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 1, seatHandCounts: new[] { 13, 13, 13, 13 }, wallTiles: 55);
        
        // Act: Execute draw action
        // var newState = stateMachine.ApplyDraw(state);
        
        // Assert: Active player now has 14 tiles
        // Assert.Equal(14, newState.Hands[1].TileCount);
        
        // Assert: Wall reduced by 1
        // Assert.Equal(54, newState.Wall.RemainingTiles);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void TurnDiscard_PlayerDiscardsOneTile_HandReducesTo13()
    {
        // D-02: After evaluating hand, player discards one tile, ending turn with 13 tiles
        
        // Arrange: Player with 14 tiles
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 1, seatHandCounts: new[] { 13, 14, 13, 13 });
        
        // Act: Execute discard action for tile index 0
        // var newState = stateMachine.ApplyDiscard(state, seatIndex: 1, tileIndex: 0);
        
        // Assert: Player now has 13 tiles
        // Assert.Equal(13, newState.Hands[1].TileCount);
        
        // Assert: Discard pile increased by 1
        // Assert.Equal(1, newState.DiscardPile.TileCount);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void TurnOrder_NoClaimsOnDiscard_NextPlayerIsCounterclockwise()
    {
        // D-03: By default, next player is to the right (counterclockwise)
        
        // Arrange: Player at seat 1 discards, no claims
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 1);
        
        // Act: Apply discard with no claims
        // var newState = stateMachine.ApplyDiscard(state, seatIndex: 1, tileIndex: 0);
        
        // Assert: Active seat advances counterclockwise: 1 → 2
        // Assert.Equal(2, newState.ActiveSeat);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void WallExhaustion_LastTileDrawn_NoWin_HandEndsInDraw()
    {
        // D-05: If wall exhausted and no one wins, hand ends in draw (流局)
        
        // Arrange: Wall with 1 tile remaining, no winning conditions
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 2, wallTiles: 1);
        
        // Act: Draw last tile without winning
        // var newState = stateMachine.ApplyDraw(state);
        
        // Assert: Wall exhausted
        // Assert.Equal(0, newState.Wall.RemainingTiles);
        
        // Act: Check if hand ends
        // var phaseAfterDiscard = stateMachine.ApplyDiscard(newState, seatIndex: 2, tileIndex: 0);
        
        // Assert: Game phase is Draw/WallExhausted
        // Assert.Equal(TableTurnPhase.WallExhausted, phaseAfterDiscard.Phase);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void ChowClaim_NotNextPlayer_Rejected()
    {
        // D-06: Changsha prohibits chow except from player immediately before you
        
        // Arrange: Seat 0 discards, seat 2 (not next) attempts chow
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameState(activeSeat: 0);
        // var discardedTile = new Tile(TileSuit.Bamboo, 5);
        
        // Act: Seat 2 attempts to chow
        // var claim = new ChowClaim(seatIndex: 2, tiles: new[] { 
        //     new Tile(TileSuit.Bamboo, 4), 
        //     new Tile(TileSuit.Bamboo, 6) 
        // });
        // var result = adjudicator.ValidateClaim(state, claim, discardedTile);
        
        // Assert: Claim rejected (seat 2 is not immediately after seat 0)
        // Assert.False(result.IsValid);
        // Assert.Contains("next player only", result.ErrorMessage);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void ChowClaim_FromImmediatePriorPlayer_Accepted()
    {
        // D-07: Player may chow a discard from the player immediately before them (to their left)
        
        // Arrange: Seat 0 discards, seat 1 (next in CCW order) attempts chow
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameState(activeSeat: 0);
        // var discardedTile = new Tile(TileSuit.Dots, 7);
        
        // Act: Seat 1 (next player) attempts valid chow
        // var claim = new ChowClaim(seatIndex: 1, tiles: new[] { 
        //     new Tile(TileSuit.Dots, 5), 
        //     new Tile(TileSuit.Dots, 6) 
        // });
        // var result = adjudicator.ValidateClaim(state, claim, discardedTile);
        
        // Assert: Claim accepted
        // Assert.True(result.IsValid);
    }
}
