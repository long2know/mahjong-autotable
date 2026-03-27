using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerInputValidationTests
{
    [Fact]
    public void TryGetWinningHand_ThrowsWhenTilesAreNull()
    {
        Action act = () => HandAnalyzer.TryGetWinningHand(null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsWinningHand_ThrowsWhenTilesAreNull()
    {
        Action act = () => HandAnalyzer.IsWinningHand(null!);

        act.Should().Throw<ArgumentNullException>();
    }

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

    [Theory]
    [InlineData(13)]
    [InlineData(15)]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountIsNotFourteen(int tileCount)
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 1)
        };

        if (tileCount < tiles.Count)
        {
            tiles.RemoveAt(tiles.Count - 1);
        }
        else if (tileCount > tiles.Count)
        {
            tiles.Add(new Tile(TileSuit.Bamboos, 2));
        }

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
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

    [Theory]
    [InlineData(TileSuit.Dots)]
    [InlineData(TileSuit.Bamboos)]
    public void TryGetWinningHand_ReturnsFalse_WhenSuitedTileAppearsFiveTimes(TileSuit suit)
    {
        var tiles = new List<Tile>
        {
            new(suit, 1), new(suit, 1), new(suit, 1), new(suit, 1), new(suit, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHonorTileAppearsFiveTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenDragonTileAppearsFiveTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenAllTilesAreBonusTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1), new(TileSuit.Flowers, 2), new(TileSuit.Flowers, 3), new(TileSuit.Flowers, 4),
            new(TileSuit.Seasons, 1), new(TileSuit.Seasons, 2), new(TileSuit.Seasons, 3), new(TileSuit.Seasons, 4),
            new(TileSuit.Flowers, 1), new(TileSuit.Flowers, 2), new(TileSuit.Flowers, 3), new(TileSuit.Flowers, 4),
            new(TileSuit.Seasons, 1), new(TileSuit.Seasons, 2)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Theory]
    [InlineData(TileSuit.Flowers, 1)]
    [InlineData(TileSuit.Seasons, 2)]
    public void TryGetWinningHand_ReturnsFalse_WhenBonusTileIncludedInOtherwiseWinningHand(TileSuit bonusSuit, int bonusRank)
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 characters
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Chows: 123 chars, 456 chars, 789 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Pung: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();

        tiles[0] = new Tile(bonusSuit, bonusRank);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenBonusTileIncludedInSevenPairsHand()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);

        tiles[0] = new Tile(TileSuit.Flowers, 1);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Theory]
    [InlineData(TileSuit.Flowers, 1)]
    [InlineData(TileSuit.Seasons, 3)]
    public void TryGetWinningHand_ReturnsFalse_WhenBonusTileIncludedInThirteenOrphansHand(TileSuit bonusSuit, int bonusRank)
    {
        var tiles = new List<Tile>
        {
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
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.ThirteenOrphans);

        tiles[0] = new Tile(bonusSuit, bonusRank);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }
}
