using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class WallBuilder
{
    public static Wall BuildShuffledWall(Random rng)
    {
        var tiles = TileFactory.CreateStandardSet().ToList();
        Shuffle(tiles, rng);
        return new Wall(tiles);
    }

    private static void Shuffle(List<Tile> tiles, Random rng)
    {
        for (var i = tiles.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
    }
}
