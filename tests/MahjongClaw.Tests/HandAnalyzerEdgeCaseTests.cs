using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerEdgeCaseTests
{
    [Fact]
    public void IsWinningHand_ReturnsTrueForAllPungsHand()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Pungs
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
    }

    [Fact]
    public void IsWinningHand_ReturnsTrueForMixedChowHand()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            // Chows
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
    }

    [Fact]
    public void IsWinningHand_ReturnsFalseWhenNoPairExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsWinningHand_ReturnsFalse_WhenTileCountIsNotFourteen()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 1)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsWinningHand_ReturnsFalse_WhenTileAppearsMoreThanFourTimes()
    {
        var tiles = new List<Tile>
        {
            // Five of the same tile is invalid.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.West)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsWinningHand_ReturnsFalse_WhenFlowerTileIsIncluded()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Flowers, 1)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }
}
