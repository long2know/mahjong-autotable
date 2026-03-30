using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class Changsha258Ruleset
{
    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(tiles));
        }

        hand = null;

        if (tiles.Count < 14 || tiles.Count > 18)
        {
            return false;
        }

        if (tiles.Any(static tile => tile.IsHonor || tile.IsFlower))
        {
            return false;
        }

        var counts = BuildCounts(tiles);
        if (counts.Values.Any(static count => count > 4))
        {
            return false;
        }

        foreach (var tile in GetOrderedTiles(counts))
        {
            if (counts[tile] < 2)
            {
                continue;
            }

            if (!IsSmallWinPair(tile))
            {
                continue;
            }

            var candidateCounts = new Dictionary<Tile, int>(counts);
            candidateCounts[tile] -= 2;

            var melds = new List<Meld>();
            if (!TryFormMelds(candidateCounts, melds))
            {
                continue;
            }

            hand = new WinningHand(melds.ToList(), tile, WinningPattern.Standard);
            return true;
        }

        return false;
    }

    public static bool IsSmallWinPair(Tile pair)
        => !pair.IsHonor && !pair.IsFlower && pair.Rank is 2 or 5 or 8;

    private static Dictionary<Tile, int> BuildCounts(IReadOnlyList<Tile> tiles)
    {
        var counts = new Dictionary<Tile, int>();
        foreach (var tile in tiles)
        {
            counts[tile] = counts.TryGetValue(tile, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static int GetTileSortBucket(Tile tile)
        => tile.Suit switch
        {
            TileSuit.Characters => 0,
            TileSuit.Bamboos => 1,
            TileSuit.Dots => 2,
            TileSuit.Winds => 3,
            TileSuit.Dragons => 4,
            TileSuit.Flowers => 5,
            _ => 6
        };

    private static int GetTileSortValue(Tile tile)
        => tile.Suit switch
        {
            TileSuit.Winds => tile.Wind switch
            {
                Wind.East => 1,
                Wind.South => 2,
                Wind.West => 3,
                Wind.North => 4,
                _ => 9
            },
            TileSuit.Dragons => tile.Dragon switch
            {
                Dragon.Red => 1,
                Dragon.Green => 2,
                Dragon.White => 3,
                _ => 9
            },
            _ => tile.Rank
        };

    private static List<Tile> GetOrderedTiles(Dictionary<Tile, int> counts)
        => counts.Keys
            .OrderBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .ToList();

    private static bool TryFormMelds(Dictionary<Tile, int> counts, List<Meld> melds)
    {
        var nextTile = counts
            .Where(static kvp => kvp.Value > 0)
            .Select(static kvp => kvp.Key)
            .OrderBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .FirstOrDefault();

        if (nextTile is null)
        {
            return true;
        }

        if (counts[nextTile] >= 4)
        {
            counts[nextTile] -= 4;
            melds.Add(new Meld(MeldType.Kong, new[] { nextTile, nextTile, nextTile, nextTile }));

            if (TryFormMelds(counts, melds))
            {
                return true;
            }

            melds.RemoveAt(melds.Count - 1);
            counts[nextTile] += 4;
        }

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
