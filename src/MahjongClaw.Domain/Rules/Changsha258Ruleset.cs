using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class Changsha258Ruleset
{
    public static readonly IReadOnlySet<int> SmallWinPairRanks = new HashSet<int> { 2, 5, 8 };

    public const int RequiredMelds = 4;
    public const int MinTiles = RequiredMelds * 3 + 2;
    public const int MaxKongs = 4;
    public const int MaxTiles = MinTiles + MaxKongs;

    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(tiles));
        }

        hand = null;

        if (tiles.Count < MinTiles || tiles.Count > MaxTiles)
        {
            return false;
        }

        if (tiles.Any(static tile => !IsAllowedTile(tile)))
        {
            return false;
        }

        var counts = BuildCounts(tiles);
        if (counts.Values.Any(static count => count > 4))
        {
            return false;
        }

        var requiredKongs = tiles.Count - MinTiles;
        if (requiredKongs > MaxKongs)
        {
            return false;
        }

        if (!HasRequiredKongs(counts, requiredKongs))
        {
            return false;
        }

        var orderedTiles = GetOrderedTiles(counts);
        var pairCandidates = orderedTiles
            .Where(tile => counts[tile] >= 2 && IsSmallWinPair(tile))
            .OrderBy(tile => counts[tile])
            .ThenBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .ToList();

        if (pairCandidates.Count == 0)
        {
            return false;
        }

        foreach (var tile in pairCandidates)
        {
            var candidateCounts = new Dictionary<Tile, int>(counts);
            candidateCounts[tile] -= 2;

            if (!HasRequiredKongs(candidateCounts, requiredKongs))
            {
                continue;
            }

            var melds = new List<Meld>();
            if (!TryFormMelds(candidateCounts, orderedTiles, melds, requiredKongs, 0))
            {
                continue;
            }

            hand = new WinningHand(melds.ToList(), tile, WinningPattern.Standard);
            return true;
        }

        return false;
    }

    public static bool IsSmallWinPair(Tile pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return IsAllowedTile(pair)
            && SmallWinPairRanks.Contains(pair.Rank);
    }

    public static bool IsAllowedTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return tile.Suit is TileSuit.Characters or TileSuit.Dots or TileSuit.Bamboos
            && tile.Rank is >= 1 and <= 9;
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

    private static int GetTileSortBucket(Tile tile)
        => tile.Suit switch
        {
            TileSuit.Characters => 0,
            TileSuit.Dots => 1,
            TileSuit.Bamboos => 2,
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

    private static bool HasRequiredKongs(IReadOnlyDictionary<Tile, int> counts, int requiredKongs)
        => requiredKongs <= 0
            || counts.Values.Count(static count => count >= 4) >= requiredKongs;

    private static bool CanSatisfyRemainingKongs(IReadOnlyDictionary<Tile, int> counts, int requiredKongs, int kongsUsed)
    {
        if (requiredKongs <= 0)
        {
            return true;
        }

        var remainingKongs = counts.Values.Count(static count => count >= 4);
        return kongsUsed + remainingKongs >= requiredKongs;
    }

    private static bool TryFormMelds(Dictionary<Tile, int> counts, IReadOnlyList<Tile> orderedTiles, List<Meld> melds, int requiredKongs, int kongsUsed)
    {
        if (kongsUsed > requiredKongs)
        {
            return false;
        }

        var remainingMeldSlots = RequiredMelds - melds.Count;
        if (remainingMeldSlots < 0)
        {
            return false;
        }

        if (kongsUsed + remainingMeldSlots < requiredKongs)
        {
            return false;
        }

        if (!CanSatisfyRemainingKongs(counts, requiredKongs, kongsUsed))
        {
            return false;
        }

        var nextTile = orderedTiles
            .FirstOrDefault(tile => counts.TryGetValue(tile, out var count) && count > 0);

        if (nextTile is null)
        {
            return melds.Count == RequiredMelds && kongsUsed == requiredKongs;
        }

        if (remainingMeldSlots == 0)
        {
            return false;
        }

        if (counts[nextTile] >= 4 && kongsUsed < requiredKongs)
        {
            counts[nextTile] -= 4;
            melds.Add(new Meld(MeldType.Kong, new[] { nextTile, nextTile, nextTile, nextTile }));

            if (TryFormMelds(counts, orderedTiles, melds, requiredKongs, kongsUsed + 1))
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

            if (TryFormMelds(counts, orderedTiles, melds, requiredKongs, kongsUsed))
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

                    if (TryFormMelds(counts, orderedTiles, melds, requiredKongs, kongsUsed))
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
