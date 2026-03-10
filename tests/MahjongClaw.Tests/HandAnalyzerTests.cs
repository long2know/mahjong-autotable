using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerTests
{
    [Fact]
    public void IsWinningHand_ReturnsTrueForStandardHand()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            // Chow 3-4-5 Dots
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            // Chow 6-7-8 Dots
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            // Pung of Red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chow 2-3-4 Bamboos
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Melds.Should().HaveCount(4);
        hand.Pair.Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void IsWinningHand_ReturnsFalseForInvalidHand()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsWinningHand_AcceptsWinningHand_WhenInputOrderStartsWithMiddleTileOfChow()
    {
        var tiles = new List<Tile>
        {
            // Pair (first two inserted as rank 2 to force dictionary insertion order edge case)
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            // Chow 1-2-3 (2 is not a valid chow start for this meld)
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            // Chow 3-4-5
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            // Chow 6-7-8
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            // Pung of red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
    }

    [Fact]
    public void IsWinningHand_ReturnsFalseForThirteenOrphansWithMultipleDuplicateTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }
}
