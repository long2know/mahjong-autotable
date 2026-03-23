using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

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
    public void IsValidChow_ReturnsTrueWhenTilesAreUnsorted()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4)
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
    public void IsValidChow_ReturnsFalseForDragonHonors()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Green),
            new Tile(TileSuit.Dragons, 0, null, Dragon.White)
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
    public void IsValidChow_ReturnsFalseForDuplicateRanks()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseForWrapAroundSequence()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Bamboos, 8),
            new Tile(TileSuit.Bamboos, 9),
            new Tile(TileSuit.Bamboos, 1)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseWhenFlowerTileIncluded()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Flowers, 1)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseWhenSeasonTileIncluded()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Seasons, 1)
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
    public void IsValidPung_ReturnsTrueForHonorTriplet()
    {
        var tile = new Tile(TileSuit.Winds, 0, Wind.North);
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
    public void IsValidPung_ReturnsFalse_WhenSameRankDifferentSuits()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Bamboos, 5)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsFalse_WhenHonorTilesDiffer()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.South)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsFalseForFlowerTiles()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Flowers, 1),
            new Tile(TileSuit.Flowers, 1),
            new Tile(TileSuit.Flowers, 1)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsFalseForSeasonTiles()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Seasons, 2),
            new Tile(TileSuit.Seasons, 2),
            new Tile(TileSuit.Seasons, 2)
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
    public void IsValidKong_ReturnsTrueForHonorQuad()
    {
        var tile = new Tile(TileSuit.Dragons, 0, null, Dragon.Red);
        MeldValidator.IsValidKong(new[] { tile, tile, tile, tile }).Should().BeTrue();
    }

    [Fact]
    public void IsValidKong_ReturnsFalseForFlowerTiles()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Flowers, 1),
            new Tile(TileSuit.Flowers, 1),
            new Tile(TileSuit.Flowers, 1),
            new Tile(TileSuit.Flowers, 1)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidKong_ReturnsFalseForSeasonTiles()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Seasons, 2),
            new Tile(TileSuit.Seasons, 2),
            new Tile(TileSuit.Seasons, 2),
            new Tile(TileSuit.Seasons, 2)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
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
    public void IsValidKong_ReturnsFalse_WhenSameRankDifferentSuits()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Bamboos, 5),
            new Tile(TileSuit.Dots, 5)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidKong_ReturnsFalse_WhenHonorTilesDiffer()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.South)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
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
    public void IsValidMeld_ReturnsTrueForKnownTypes()
    {
        var chow = new[]
        {
            new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Characters, 6)
        };

        var pungTile = new Tile(TileSuit.Dragons, 0, null, Dragon.Green);
        var pung = new[] { pungTile, pungTile, pungTile };

        var kongTile = new Tile(TileSuit.Bamboos, 9);
        var kong = new[] { kongTile, kongTile, kongTile, kongTile };

        MeldValidator.IsValidMeld(chow, MeldType.Chow).Should().BeTrue();
        MeldValidator.IsValidMeld(pung, MeldType.Pung).Should().BeTrue();
        MeldValidator.IsValidMeld(kong, MeldType.Kong).Should().BeTrue();
    }

    [Theory]
    [InlineData(MeldType.Chow, 4)]
    [InlineData(MeldType.Pung, 2)]
    [InlineData(MeldType.Kong, 3)]
    public void IsValidMeld_ReturnsFalseForWrongTileCount(MeldType type, int count)
    {
        var tiles = Enumerable.Repeat(new Tile(TileSuit.Dots, 1), count).ToArray();

        MeldValidator.IsValidMeld(tiles, type).Should().BeFalse();
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

    [Fact]
    public void IsValidKong_ReturnsFalseForWrongTileCount()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 2)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalseForWrongTileCount()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 1),
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidChow_ReturnsFalse_WhenTilesContainNull()
    {
        var tiles = new Tile[]
        {
            new(TileSuit.Dots, 2),
            null!,
            new(TileSuit.Dots, 3)
        };

        MeldValidator.IsValidChow(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidPung_ReturnsFalse_WhenTilesContainNull()
    {
        var tiles = new Tile[]
        {
            new(TileSuit.Characters, 5),
            null!,
            new(TileSuit.Characters, 5)
        };

        MeldValidator.IsValidPung(tiles).Should().BeFalse();
    }

    [Fact]
    public void IsValidKong_ReturnsFalse_WhenTilesContainNull()
    {
        var tiles = new Tile[]
        {
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7),
            null!,
            new(TileSuit.Bamboos, 7)
        };

        MeldValidator.IsValidKong(tiles).Should().BeFalse();
    }
}
