using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-A: Tile Set & Wall Construction Tests
/// Tests P0 scenarios for 108-tile Changsha deck composition and wall building.
/// </summary>
public class TileSetAndWallTests
{
    [Fact(Skip = "Awaiting Bishop's Changsha tile set builder")]
    [Trait("Category", "Changsha")]
    public void TileSetComposition_BuildsExactly108Tiles_WithThreeSuitsOnly()
    {
        // A-01: Verify tile set contains exactly 108 tiles: 36 Characters, 36 Dots, 36 Bamboo
        // No winds, dragons, or flowers
        
        // Arrange: Initialize Changsha deck builder
        // var deckBuilder = new ChangshaDeckBuilder();
        
        // Act: Build tile set
        // var tiles = deckBuilder.BuildDeck();
        
        // Assert: Exactly 108 tiles
        // Assert.Equal(108, tiles.Count);
        
        // Assert: 36 of each suit (Characters, Dots, Bamboo)
        // var characters = tiles.Where(t => t.Suit == TileSuit.Characters).ToList();
        // var dots = tiles.Where(t => t.Suit == TileSuit.Dots).ToList();
        // var bamboo = tiles.Where(t => t.Suit == TileSuit.Bamboo).ToList();
        // Assert.Equal(36, characters.Count);
        // Assert.Equal(36, dots.Count);
        // Assert.Equal(36, bamboo.Count);
        
        // Assert: Each number 1-9 appears 4 times per suit
        // for (int rank = 1; rank <= 9; rank++)
        // {
        //     Assert.Equal(4, characters.Count(t => t.Rank == rank));
        //     Assert.Equal(4, dots.Count(t => t.Rank == rank));
        //     Assert.Equal(4, bamboo.Count(t => t.Rank == rank));
        // }
        
        // Assert: No winds, dragons, flowers, jokers
        // Assert.DoesNotContain(tiles, t => t.Suit == TileSuit.Wind);
        // Assert.DoesNotContain(tiles, t => t.Suit == TileSuit.Dragon);
        // Assert.DoesNotContain(tiles, t => t.Suit == TileSuit.Flower);
    }

    [Fact(Skip = "Awaiting Bishop's Changsha wall builder")]
    [Trait("Category", "Changsha")]
    public void WallConstruction_TotalTileCount_Equals108()
    {
        // A-05: Total tiles in all walls equals 108
        
        // Arrange: Build Changsha deck and wall
        // var deckBuilder = new ChangshaDeckBuilder();
        // var wallBuilder = new ChangshaWallBuilder();
        // var tiles = deckBuilder.BuildDeck();
        
        // Act: Construct walls for 4 players
        // var walls = wallBuilder.BuildWalls(tiles);
        
        // Assert: Total tile count across all walls is 108
        // var totalTiles = walls.Sum(w => w.TileCount);
        // Assert.Equal(108, totalTiles);
    }

    [Fact(Skip = "Awaiting Bishop's Changsha wall builder")]
    [Trait("Category", "Changsha")]
    public void WallConstruction_NonDealerSegments_Each26Tiles()
    {
        // A-02: Each non-dealer player builds wall segment of exactly 26 tiles (13 long × 2 high)
        
        // Arrange: Build Changsha deck and wall
        // var deckBuilder = new ChangshaDeckBuilder();
        // var wallBuilder = new ChangshaWallBuilder();
        // var tiles = deckBuilder.BuildDeck();
        
        // Act: Construct walls with dealer at seat 0
        // var walls = wallBuilder.BuildWalls(tiles, dealerSeat: 0);
        
        // Assert: Non-dealer walls (seats 1, 2, 3) have 26 tiles each
        // Assert.Equal(26, walls[1].TileCount);
        // Assert.Equal(26, walls[2].TileCount);
        // Assert.Equal(26, walls[3].TileCount);
    }

    [Fact(Skip = "Awaiting Bishop's Changsha wall builder")]
    [Trait("Category", "Changsha")]
    public void WallConstruction_DealerSegment_Has28Tiles()
    {
        // A-03: Dealer builds wall segment of exactly 28 tiles (14 long × 2 high)
        
        // Arrange: Build Changsha deck and wall
        // var deckBuilder = new ChangshaDeckBuilder();
        // var wallBuilder = new ChangshaWallBuilder();
        // var tiles = deckBuilder.BuildDeck();
        
        // Act: Construct walls with dealer at seat 0
        // var walls = wallBuilder.BuildWalls(tiles, dealerSeat: 0);
        
        // Assert: Dealer wall (seat 0) has 28 tiles
        // Assert.Equal(28, walls[0].TileCount);
    }
}
