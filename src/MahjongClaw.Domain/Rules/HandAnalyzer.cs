using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class HandAnalyzer
{
    public static bool IsWinningHand(IReadOnlyList<Tile> tiles)
        => TryGetWinningHand(tiles, out _);

    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        hand = null;

        if (tiles.Count % 3 != 2)
        {
            return false;
        }

        var counts = BuildCounts(tiles);

        foreach (var tile in counts.Keys.ToList())
        {
            if (counts[tile] >= 2)
            {
                counts[tile] -= 2;
                var melds = new List<Meld>();

                if (TryFormMelds(counts, melds))
                {
                    hand = new WinningHand(melds, tile);
                    return true;
                }

                counts[tile] += 2;
            }
        }

        return false;
    }

    private static Dictionary<Tile, int> BuildCounts(IReadOnlyList<Tile> tiles)
    {
        var counts = new Dictionary<Tile, int>();
        foreach (var tile in tiles)
        {
            counts[tile] = counts.TryGetValue(tile, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static bool TryFormMelds(Dictionary<Tile, int> counts, List<Meld> melds)
    {
        var nextTile = counts.FirstOrDefault(kvp => kvp.Value > 0).Key;
        if (nextTile is null)
        {
            return true;
        }

        // Try Pung
        if (counts[nextTile] >= 3)
        {
            counts[nextTile] -= 3;
            melds.Add(new Meld(MeldType.Pung, new[] { nextTile, nextTile, nextTile }));

            if (TryFormMelds(counts, melds))
            {
                return true;
            }

            melds.RemoveAt(melds.Count - 1);
            counts[nextTile] += 3;
        }

        // Try Chow for suited tiles
        if (!nextTile.IsHonor && !nextTile.IsFlower)
        {
            var rank = nextTile.Rank;
            if (rank <= 7)
            {
                var suit = nextTile.Suit;
                var tile2 = new Tile(suit, rank + 1);
                var tile3 = new Tile(suit, rank + 2);

                if (counts.TryGetValue(tile2, out var count2) && count2 > 0 &&
                    counts.TryGetValue(tile3, out var count3) && count3 > 0)
                {
                    counts[nextTile] -= 1;
                    counts[tile2] -= 1;
                    counts[tile3] -= 1;
                    melds.Add(new Meld(MeldType.Chow, new[] { nextTile, tile2, tile3 }));

                    if (TryFormMelds(counts, melds))
                    {
                        return true;
                    }

                    melds.RemoveAt(melds.Count - 1);
                    counts[nextTile] += 1;
                    counts[tile2] += 1;
                    counts[tile3] += 1;
                }
            }
        }

        return false;
    }
}
