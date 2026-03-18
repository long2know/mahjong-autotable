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
    public void Tile_ThrowsWhenSuitedTileIncludesHonorMetadata()
    {
        var create = () => new Tile(TileSuit.Characters, 5, Wind.East);

        create.Should().Throw<ArgumentException>();
    }
}
