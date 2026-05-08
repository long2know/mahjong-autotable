using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-E: Pung / Kong Tests
/// Tests P0 scenarios for meld claims and priority resolution.
/// </summary>
public class PungKongChowTests
{
    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void PungClaim_InterruptsTurnOrder_PlayerBecomesActive()
    {
        // E-01: Any player can claim a discard for pung even if not their turn
        
        // Arrange: Seat 1 discards, seat 3 (out of turn) has 2 matching tiles
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 1);
        // var discardedTile = new Tile(TileSuit.Characters, 3);
        
        // Act: Seat 3 claims pung
        // var claim = new PungClaim(seatIndex: 3, tiles: new[] {
        //     new Tile(TileSuit.Characters, 3),
        //     new Tile(TileSuit.Characters, 3)
        // });
        // var result = adjudicator.ValidateClaim(state, claim, discardedTile);
        
        // Assert: Claim valid
        // Assert.True(result.IsValid);
        
        // Act: Apply claim
        // var newState = stateMachine.ApplyPungClaim(state, claim, discardedTile);
        
        // Assert: Seat 3 becomes active player
        // Assert.Equal(3, newState.ActiveSeat);
        
        // Assert: Pung revealed in exposed melds
        // Assert.Contains(newState.ExposedMelds[3], m => m.Type == MeldType.Pung);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void ConcealedKong_FromOwnDraw_DrawsReplacementTile()
    {
        // E-02: Player draws fourth identical tile, can declare concealed kong and draw replacement
        
        // Arrange: Player has 3 identical tiles, draws 4th from wall
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameStateWithHand(seatIndex: 2, tiles: new[] {
        //     new Tile(TileSuit.Bamboo, 8),
        //     new Tile(TileSuit.Bamboo, 8),
        //     new Tile(TileSuit.Bamboo, 8),
        //     new Tile(TileSuit.Bamboo, 8) // Just drew this
        // });
        
        // Act: Declare concealed kong
        // var newState = stateMachine.ApplyKongDeclaration(state, seatIndex: 2, 
        //     tiles: TileSuit.Bamboo_8_x4, isConcealed: true);
        
        // Assert: Kong revealed (outer tiles face-down in representation)
        // var kong = newState.ExposedMelds[2].First(m => m.Type == MeldType.ConcealedKong);
        // Assert.Equal(4, kong.Tiles.Count);
        
        // Assert: Player draws replacement tile (hand back to 14 tiles after kong reveal)
        // Assert.Equal(14, newState.Hands[2].TileCount); // 14 - 4 (kong) + 1 (replacement) + ...
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void ExposedKong_FromDiscard_DrawsReplacementTile()
    {
        // E-03: Player can claim a discard to form exposed kong (4 identical) and draw replacement
        
        // Arrange: Seat 0 discards, seat 2 has 3 identical tiles
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 0);
        // var discardedTile = new Tile(TileSuit.Dots, 2);
        
        // Act: Seat 2 claims kong
        // var claim = new KongClaim(seatIndex: 2, tiles: new[] {
        //     new Tile(TileSuit.Dots, 2),
        //     new Tile(TileSuit.Dots, 2),
        //     new Tile(TileSuit.Dots, 2)
        // });
        // var newState = stateMachine.ApplyKongClaim(state, claim, discardedTile);
        
        // Assert: Kong revealed (all face-up)
        // var kong = newState.ExposedMelds[2].First(m => m.Type == MeldType.ExposedKong);
        // Assert.Equal(4, kong.Tiles.Count);
        // Assert.True(kong.IsExposed);
        
        // Assert: Replacement tile drawn
        // Assert.Equal(14, newState.Hands[2].TileCount); // Maintains 14 after kong + replacement
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void MultipleClaimsPriority_WinBeatsKong_KongBeatsPung_PungBeatsChow()
    {
        // E-08: If multiple players want a discard, priority: win > pung/kong > chow
        
        // Arrange: Tile discarded, multiple claims submitted
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameState(activeSeat: 0);
        // var discardedTile = new Tile(TileSuit.Characters, 5);
        
        // var winClaim = new WinClaim(seatIndex: 3); // Player 3 can win
        // var pungClaim = new PungClaim(seatIndex: 2); // Player 2 can pung
        // var chowClaim = new ChowClaim(seatIndex: 1); // Player 1 (next) can chow
        
        // Act: Resolve priority
        // var winner = adjudicator.ResolvePriority(state, new[] { winClaim, pungClaim, chowClaim }, discardedTile);
        
        // Assert: Win claim takes priority
        // Assert.Equal(winClaim, winner);
        
        // Act: Test without win claim
        // var winnerNoClaim = adjudicator.ResolvePriority(state, new[] { pungClaim, chowClaim }, discardedTile);
        
        // Assert: Pung takes priority over chow
        // Assert.Equal(pungClaim, winnerNoClaim);
    }

    [Fact(Skip = "Awaiting Bishop's IClaimAdjudicator")]
    [Trait("Category", "Changsha")]
    public void MultipleWinClaims_ProximityRule_ClosestCounterclockwiseWins()
    {
        // E-09 (P1 but critical): If multiple players can win from same discard, 
        // closest in turn order (counterclockwise from discarder) wins
        
        // Arrange: Seat 0 discards, seats 2 and 3 both claim win
        // var adjudicator = new ChangshaClaimAdjudicator();
        // var state = CreateGameState(activeSeat: 0);
        // var discardedTile = new Tile(TileSuit.Bamboo, 4);
        
        // var winClaim2 = new WinClaim(seatIndex: 2); // 2 seats away CCW
        // var winClaim3 = new WinClaim(seatIndex: 3); // 3 seats away CCW
        
        // Act: Resolve proximity
        // var winner = adjudicator.ResolveMultipleWins(state, new[] { winClaim2, winClaim3 }, 
        //     discarderSeat: 0, discardedTile);
        
        // Assert: Seat 2 wins (closer in CCW order: 0 → 1 → 2)
        // Assert.Equal(2, winner.SeatIndex);
    }
}
