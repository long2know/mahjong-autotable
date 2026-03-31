using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class WallBuilder
{
    private static readonly int StandardWallTotal = TileFactory.CreateStandardSet().Count;
    private static readonly int ChangshaWallTotal = TileFactory.CreateChangsha258Set().Count;

    public static Wall BuildShuffledWall(Random rng)
        => BuildShuffledWall(rng, MahjongRuleset.ChineseOfficial);

    public static int GetWallTotal(MahjongRuleset ruleset)
        => ruleset == MahjongRuleset.Changsha258 ? ChangshaWallTotal : StandardWallTotal;

    public static Wall BuildShuffledWall(Random rng, MahjongRuleset ruleset)
    {
        var tiles = ruleset switch
        {
            MahjongRuleset.Changsha258 => TileFactory.CreateChangsha258Set().ToList(),
            _ => TileFactory.CreateStandardSet().ToList()
        };

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
