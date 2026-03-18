using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class WallTests
{
    [Fact]
    public void Draw_RemovesFirstTileAndDecrementsCount()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2)
        };

        var wall = new Wall(tiles);
        var drawn = wall.Draw();

        drawn.Should().Be(new Tile(TileSuit.Dots, 1));
        wall.RemainingCount.Should().Be(1);
        wall.Tiles.Should().ContainSingle().Which.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void Draw_ReturnsTilesInOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 7)
        };

        var wall = new Wall(tiles);

        wall.Draw().Should().Be(tiles[0]);
        wall.Draw().Should().Be(tiles[1]);
        wall.Draw().Should().Be(tiles[2]);
        wall.RemainingCount.Should().Be(0);
    }

    [Fact]
    public void Draw_ThrowsWhenWallEmpty()
    {
        var wall = new Wall(Array.Empty<Tile>());

        var act = () => wall.Draw();

        act.Should().Throw<InvalidOperationException>().WithMessage("Wall is empty.");
    }
}
