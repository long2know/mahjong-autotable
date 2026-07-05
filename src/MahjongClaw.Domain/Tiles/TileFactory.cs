using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Domain.Tiles;

public static class TileFactory
{
    private static readonly TileSuit[] StandardSuitedSuits =
    {
        TileSuit.Characters,
        TileSuit.Dots,
        TileSuit.Bamboos
    };

    public static IReadOnlyList<Tile> CreateStandardSet()
    {
        var tiles = new List<Tile>(136);

        AddSuitedTiles(tiles, StandardSuitedSuits, 1, 9);

        // Winds: East, South, West, North (four copies each)
        foreach (Wind wind in Enum.GetValues(typeof(Wind)))
        {
            for (var copy = 0; copy < 4; copy++)
            {
                tiles.Add(new Tile(TileSuit.Winds, 0, wind));
            }
        }

        // Dragons: Red, Green, White (four copies each)
        foreach (Dragon dragon in Enum.GetValues(typeof(Dragon)))
        {
            for (var copy = 0; copy < 4; copy++)
            {
                tiles.Add(new Tile(TileSuit.Dragons, 0, null, dragon));
            }
        }

        return tiles;
    }

    public static IReadOnlyList<Tile> CreateChangsha258Set()
    {
        var tiles = new List<Tile>(Changsha258Ruleset.TileSetSize);

        AddSuitedTiles(tiles, Changsha258Ruleset.AllowedSuits, Changsha258Ruleset.MinSuitedRank, Changsha258Ruleset.MaxSuitedRank);

        return tiles;
    }

    private static void AddSuitedTiles(ICollection<Tile> tiles, IEnumerable<TileSuit> suits, int minRank, int maxRank)
    {
        // Suited tiles: min-max ranks, four copies each
        foreach (var suit in suits)
        {
            for (var rank = minRank; rank <= maxRank; rank++)
            {
                for (var copy = 0; copy < 4; copy++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
        }
    }
}
