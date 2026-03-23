using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerInputValidationTests
{
    [Fact]
    public void TryGetWinningHand_ThrowsWhenTilesContainNullEntry()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            null!
        };

        WinningHand? hand = null;
        Action act = () => HandAnalyzer.TryGetWinningHand(tiles, out hand);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void IsWinningHand_ThrowsWhenTilesContainNullEntry()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            null!
        };

        Action act = () => HandAnalyzer.IsWinningHand(tiles);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountExceedsFour()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }
}
