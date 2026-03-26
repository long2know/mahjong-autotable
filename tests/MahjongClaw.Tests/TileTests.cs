using FluentAssertions;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class TileTests
{
    [Fact]
    public void Tile_ThrowsWhenDragonMetadataMissing()
    {
        var create = () => new Tile(TileSuit.Dragons, 0);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenDragonTileIncludesWindMetadata()
    {
        var create = () => new Tile(TileSuit.Dragons, 0, Wind.East, Dragon.Red);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenDragonRankIsNotZero()
    {
        var create = () => new Tile(TileSuit.Dragons, 1, null, Dragon.Red);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindMetadataMissing()
    {
        var create = () => new Tile(TileSuit.Winds, 0);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindTileIncludesDragonMetadata()
    {
        var create = () => new Tile(TileSuit.Winds, 0, Wind.East, Dragon.Red);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindRankIsNotZero()
    {
        var create = () => new Tile(TileSuit.Winds, 1, Wind.East);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenFlowerRankIsOutOfRange()
    {
        var create = () => new Tile(TileSuit.Flowers, 0);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenFlowerTileIncludesHonorMetadata()
    {
        var create = () => new Tile(TileSuit.Flowers, 1, Wind.East);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenSeasonRankIsOutOfRange()
    {
        var create = () => new Tile(TileSuit.Seasons, 5);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenSeasonTileIncludesHonorMetadata()
    {
        var create = () => new Tile(TileSuit.Seasons, 1, null, Dragon.Red);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenSuitedRankIsOutOfRange()
    {
        var create = () => new Tile(TileSuit.Bamboos, 0);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenSuitedTileIncludesHonorMetadata()
    {
        var create = () => new Tile(TileSuit.Characters, 5, Wind.East);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_IsHonor_ReturnsTrue_ForHonorTiles()
    {
        new Tile(TileSuit.Winds, 0, Wind.East).IsHonor.Should().BeTrue();
        new Tile(TileSuit.Dragons, 0, null, Dragon.Red).IsHonor.Should().BeTrue();
    }

    [Fact]
    public void Tile_IsHonor_ReturnsFalse_ForSuitedAndBonusTiles()
    {
        new Tile(TileSuit.Characters, 1).IsHonor.Should().BeFalse();
        new Tile(TileSuit.Dots, 1).IsHonor.Should().BeFalse();
        new Tile(TileSuit.Bamboos, 1).IsHonor.Should().BeFalse();
        new Tile(TileSuit.Flowers, 1).IsHonor.Should().BeFalse();
        new Tile(TileSuit.Seasons, 1).IsHonor.Should().BeFalse();
    }

    [Fact]
    public void Tile_IsFlower_ReturnsTrue_ForBonusTiles()
    {
        new Tile(TileSuit.Flowers, 1).IsFlower.Should().BeTrue();
        new Tile(TileSuit.Seasons, 1).IsFlower.Should().BeTrue();
    }

    [Fact]
    public void Tile_IsFlower_ReturnsFalse_ForSuitedAndHonorTiles()
    {
        new Tile(TileSuit.Characters, 1).IsFlower.Should().BeFalse();
        new Tile(TileSuit.Dots, 1).IsFlower.Should().BeFalse();
        new Tile(TileSuit.Bamboos, 1).IsFlower.Should().BeFalse();
        new Tile(TileSuit.Winds, 0, Wind.East).IsFlower.Should().BeFalse();
        new Tile(TileSuit.Dragons, 0, null, Dragon.Red).IsFlower.Should().BeFalse();
    }
}
