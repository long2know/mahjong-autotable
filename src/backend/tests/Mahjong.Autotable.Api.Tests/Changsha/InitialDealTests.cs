using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-C: Initial Deal Tests
/// Tests P0 scenarios for batch-of-4 dealing and initial tile distribution.
/// </summary>
public class InitialDealTests
{
    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_DealerReceives14Tiles_AfterDealComplete()
    {
        // C-01: Dealer receives 14 tiles after initial deal (13 + 1 first draw)
        
        // Arrange: Initialize deal service with draw wall
        // var dealService = new ChangshaDealService();
        // var drawWall = CreateDrawWall(55); // 55 tiles after break
        
        // Act: Execute initial deal with dealer at seat 0
        // var hands = dealService.DealInitialHands(drawWall, dealerSeat: 0);
        
        // Assert: Dealer (seat 0) has 14 tiles
        // Assert.Equal(14, hands[0].TileCount);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_NonDealersReceive13Tiles_AfterDealComplete()
    {
        // C-02: Each non-dealer player receives 13 tiles after initial deal
        
        // Arrange: Initialize deal service with draw wall
        // var dealService = new ChangshaDealService();
        // var drawWall = CreateDrawWall(55);
        
        // Act: Execute initial deal with dealer at seat 0
        // var hands = dealService.DealInitialHands(drawWall, dealerSeat: 0);
        
        // Assert: Non-dealers (seats 1, 2, 3) have 13 tiles each
        // Assert.Equal(13, hands[1].TileCount);
        // Assert.Equal(13, hands[2].TileCount);
        // Assert.Equal(13, hands[3].TileCount);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_AfterThreeRoundsOfFour_AllPlayersHave12Tiles()
    {
        // C-04: After three rounds of 4 tiles each, all players have 12 tiles
        
        // Arrange: Deal service in batch mode
        // var dealService = new ChangshaDealService();
        // var drawWall = CreateDrawWall(55);
        
        // Act: Deal three rounds of 4 tiles (batch-of-4, counterclockwise)
        // var hands = dealService.DealBatchRounds(drawWall, rounds: 3, dealerSeat: 0);
        
        // Assert: All players have 12 tiles
        // Assert.All(hands, hand => Assert.Equal(12, hand.TileCount));
        
        // Assert: 48 tiles consumed from draw wall (4 players × 3 rounds × 4 tiles)
        // Assert.Equal(55 - 48, drawWall.RemainingTiles);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_FinalSingleTileRound_AllPlayersHave13Tiles()
    {
        // C-05: After 12 tiles dealt, each player draws 1 more tile for total of 13
        
        // Arrange: All players at 12 tiles
        // var dealService = new ChangshaDealService();
        // var hands = CreateHandsWithTileCount(new[] { 12, 12, 12, 12 });
        // var drawWall = CreateDrawWall(7); // 55 - 48 = 7 tiles remain
        
        // Act: Deal final single-tile round
        // dealService.DealSingleTileRound(drawWall, hands, dealerSeat: 0);
        
        // Assert: All players have 13 tiles
        // Assert.All(hands, hand => Assert.Equal(13, hand.TileCount));
        
        // Assert: 4 tiles consumed
        // Assert.Equal(3, drawWall.RemainingTiles);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_DealerFirstDraw_DealerHas14Tiles()
    {
        // C-06: Dealer draws 14th tile as their first draw before first discard
        
        // Arrange: All players at 13 tiles (including dealer)
        // var dealService = new ChangshaDealService();
        // var hands = CreateHandsWithTileCount(new[] { 13, 13, 13, 13 });
        // var drawWall = CreateDrawWall(3); // 3 tiles remain after single-tile round
        
        // Act: Dealer draws first tile
        // dealService.DealerFirstDraw(drawWall, hands, dealerSeat: 0);
        
        // Assert: Dealer has 14 tiles
        // Assert.Equal(14, hands[0].TileCount);
        
        // Assert: Non-dealers still have 13
        // Assert.Equal(13, hands[1].TileCount);
        // Assert.Equal(13, hands[2].TileCount);
        // Assert.Equal(13, hands[3].TileCount);
        
        // Assert: 1 tile consumed from draw wall
        // Assert.Equal(2, drawWall.RemainingTiles);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_Complete_Leaves55TilesInDrawWall()
    {
        // C-07 (P1 but useful baseline): After dealing 53 tiles (14+13+13+13), 55 remain
        
        // Arrange: Full 108-tile wall
        // var dealService = new ChangshaDealService();
        // var drawWall = CreateDrawWall(108);
        
        // Act: Execute complete initial deal
        // var hands = dealService.DealInitialHands(drawWall, dealerSeat: 0);
        
        // Assert: 53 tiles dealt (14 + 13 + 13 + 13)
        // var totalDealt = hands.Sum(h => h.TileCount);
        // Assert.Equal(53, totalDealt);
        
        // Assert: 55 tiles remain in draw wall
        // Assert.Equal(55, drawWall.RemainingTiles);
    }

    [Fact(Skip = "Awaiting Bishop's IDealService")]
    [Trait("Category", "Changsha")]
    public void InitialDeal_WithFixedSeed_IsDeterministic()
    {
        // Determinism test: Same seed produces same hands
        
        // Arrange: Two deal services with same seed
        // var deal1 = new ChangshaDealService(seed: 456);
        // var deal2 = new ChangshaDealService(seed: 456);
        // var wall1 = CreateDrawWall(108);
        // var wall2 = CreateDrawWall(108);
        
        // Act: Deal initial hands
        // var hands1 = deal1.DealInitialHands(wall1, dealerSeat: 0);
        // var hands2 = deal2.DealInitialHands(wall2, dealerSeat: 0);
        
        // Assert: Identical hands
        // for (int i = 0; i < 4; i++)
        // {
        //     Assert.Equal(hands1[i].Tiles, hands2[i].Tiles);
        // }
    }
}
