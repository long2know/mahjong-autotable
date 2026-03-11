using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class MeldValidatorTests
{
    [Fact]
    public void IsValidChow_ReturnsTrueForSuitedRun()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Dots, 5)
        };

        MeldValidator.IsValidChow(tiles).Should().BeTrue();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseForHonors()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.South),
            new Tile(TileSuit.Winds, 0, Wind.West)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseForMixedSuits()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 4),
            new Tile(TileSuit.Dots, 5)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseForNonConsecutiveRanks()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Characters, 5)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsTrueForTriplet()
    {
        var tile = new Tile(TileSuit.Characters, 7);
        MeldValidator.IsValidPung(new[] { tile, tile, tile }).Should().BeTrue();
    }

    [Fact]
    public void IsValidPung_ReturnsFalseWhenTilesDiffer()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 8)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidKong_ReturnsTrueForQuad()
    {
        var tile = new Tile(TileSuit.Bamboos, 2);
        MeldValidator.IsValidKong(new[] { tile, tile, tile, tile }).Should().BeTrue();
    }

    [Fact]
    public void IsValidKong_ReturnsFalseWhenTilesDiffer()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 3)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsTrueForUnsortedSuitedRun()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Bamboos, 7),
            new Tile(TileSuit.Bamboos, 9),
            new Tile(TileSuit.Bamboos, 8)
        };

        MeldValidator.IsValidChow(tiles).Should().BeTrue();
    }

    [Fact]
    public void IsValidMeld_ReturnsFalseForUnknownType()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Dots, 5)
        };

        MeldValidator.IsValidMeld(tiles, (MeldType)999).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsFalseForWrongTileCount()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 7)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }
}
