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
    public void Tile_ThrowsWhenSuitedTileIncludesHonorMetadata()
    {
        var create = () => new Tile(TileSuit.Characters, 5, Wind.East);

        create.Should().Throw<ArgumentException>();
    }
}
