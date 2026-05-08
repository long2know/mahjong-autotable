using System.Collections.Frozen;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class Changsha258Ruleset
{
    public static readonly IReadOnlySet<int> SmallWinPairRanks = FrozenSet.ToFrozenSet(new[] { 2, 5, 8 });
    public static readonly IReadOnlyList<int> SortedSmallWinPairRanks = SmallWinPairRanks.OrderBy(rank => rank).ToArray();
    public static readonly IReadOnlySet<TileSuit> AllowedSuits = FrozenSet.ToFrozenSet(new[]
    {
        TileSuit.Characters,
        TileSuit.Dots,
        TileSuit.Bamboos
    });

    public const int RequiredMelds = 4;
    public const int MinTiles = RequiredMelds * 3 + 2;
    public const int MaxKongs = 4;
    public const int MaxTiles = MinTiles + MaxKongs;
    public const int MinSuitedRank = 1;
    public const int MaxSuitedRank = 9;
    public const int TileSetSize = 108;
    public const int BigWinPureSuitEyeFlexibleThreshold = 10;

    public static bool TryGetRequiredKongCount(int tileCount, out int requiredKongs)
    {
        requiredKongs = 0;

        if (tileCount < MinTiles || tileCount > MaxTiles)
        {
            return false;
        }

        requiredKongs = tileCount - MinTiles;
        return true;
    }

    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        if (tiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(tiles));
        }

        hand = null;

        if (!TryGetRequiredKongCount(tiles.Count, out var requiredKongs))
        {
            return false;
        }

        if (tiles.Any(static tile => !IsAllowedTile(tile)))
        {
            return false;
        }

        if (!TryBuildCounts(tiles, out var counts))
        {
            return false;
        }

        if (!HasRequiredKongs(counts, requiredKongs))
        {
            return false;
        }

        var orderedTiles = GetOrderedTiles(counts);

        if (TryBuildSevenPairsHand(counts, orderedTiles, requiredKongs, out hand))
        {
            return true;
        }

        if (TryBuildStandardHand(
            tiles,
            counts,
            orderedTiles,
            requiredKongs,
            pairPredicate: IsSmallWinPair,
            requireBigWinForRandomPair: false,
            out hand))
        {
            return true;
        }

        return TryBuildStandardHand(
            tiles,
            counts,
            orderedTiles,
            requiredKongs,
            pairPredicate: static _ => true,
            requireBigWinForRandomPair: true,
            out hand);
    }

    public static bool IsSmallWinPair(Tile pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        return IsAllowedTile(pair)
            && SmallWinPairRanks.Contains(pair.Rank);
    }

    public static bool IsBigWinEyeFlexibleCandidate(IReadOnlyList<Tile> handTiles, IReadOnlyList<Tile> meldTiles)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        if (handTiles.Count == 0 && meldTiles.Count == 0)
        {
            return false;
        }

        var ownedTiles = handTiles.Concat(meldTiles).ToList();
        if (ownedTiles.Count < 7)
        {
            return false;
        }

        if (IsPureSuit(ownedTiles))
        {
            return ownedTiles.Count >= BigWinPureSuitEyeFlexibleThreshold;
        }

        if (IsAllTwoFiveEight(ownedTiles))
        {
            return true;
        }

        if (IsSanTong(ownedTiles))
        {
            return true;
        }

        if (IsSevenPairsCandidate(handTiles, meldTiles))
        {
            return true;
        }

        return HasAllPungsBigWinCandidate(ownedTiles, meldTiles);
    }

    public static bool IsAllowedTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return AllowedSuits.Contains(tile.Suit)
            && tile.Rank is >= MinSuitedRank and <= MaxSuitedRank;
    }

    private static bool IsSevenPairsCandidate(IReadOnlyList<Tile> handTiles, IReadOnlyList<Tile> meldTiles)
    {
        if (meldTiles.Count > 0)
        {
            return false;
        }

        var pairCount = handTiles
            .GroupBy(static tile => tile)
            .Sum(static group => group.Count() / 2);

        return pairCount >= 6;
    }

    private static bool TryBuildCounts(IReadOnlyList<Tile> tiles, out Dictionary<Tile, int> counts)
    {
        counts = new Dictionary<Tile, int>();
        foreach (var tile in tiles)
        {
            if (!counts.TryGetValue(tile, out var count))
            {
                counts[tile] = 1;
                continue;
            }

            count++;
            if (count > 4)
            {
                return false;
            }

            counts[tile] = count;
        }

        return true;
    }

    private static int GetTileSortBucket(Tile tile)
        => tile.Suit switch
        {
            TileSuit.Characters => 0,
            TileSuit.Dots => 1,
            TileSuit.Bamboos => 2,
            _ => 3
        };

    private static int GetTileSortValue(Tile tile)
        => tile.Rank;

    private static List<Tile> GetOrderedTiles(Dictionary<Tile, int> counts)
        => counts.Keys
            .OrderBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .ToList();

    private static bool TryBuildSevenPairsHand(
        IReadOnlyDictionary<Tile, int> counts,
        IReadOnlyList<Tile> orderedTiles,
        int requiredKongs,
        out WinningHand? hand)
    {
        hand = null;

        if (requiredKongs != 0)
        {
            return false;
        }

        if (counts.Values.Any(static count => count % 2 != 0))
        {
            return false;
        }

        var pairCount = counts.Values.Sum(static count => count / 2);
        if (pairCount != 7)
        {
            return false;
        }

        var pairTile = orderedTiles.First(tile => counts[tile] >= 2);
        hand = new WinningHand(Array.Empty<Meld>(), pairTile, WinningPattern.SevenPairs);
        return true;
    }

    private static bool TryBuildStandardHand(
        IReadOnlyList<Tile> allTiles,
        IReadOnlyDictionary<Tile, int> counts,
        IReadOnlyList<Tile> orderedTiles,
        int requiredKongs,
        Func<Tile, bool> pairPredicate,
        bool requireBigWinForRandomPair,
        out WinningHand? hand)
    {
        hand = null;

        var pairCandidates = orderedTiles
            .Where(tile => counts[tile] >= 2 && pairPredicate(tile))
            .OrderBy(tile => counts[tile])
            .ThenBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .ToList();

        foreach (var pairTile in pairCandidates)
        {
            var candidateCounts = new Dictionary<Tile, int>(counts);
            candidateCounts[pairTile] -= 2;

            if (!HasRequiredKongs(candidateCounts, requiredKongs))
            {
                continue;
            }

            var melds = new List<Meld>();
            if (!TryFormMelds(candidateCounts, orderedTiles, melds, requiredKongs, kongsUsed: 0))
            {
                continue;
            }

            if (requireBigWinForRandomPair && !IsEligibleRandomEyeBigWin(allTiles, melds))
            {
                continue;
            }

            var pattern = melds.All(static meld => meld.Type is MeldType.Pung or MeldType.Kong)
                ? WinningPattern.AllPungs
                : WinningPattern.Standard;

            hand = new WinningHand(melds.ToList(), pairTile, pattern);
            return true;
        }

        return false;
    }

    private static bool IsEligibleRandomEyeBigWin(IReadOnlyList<Tile> allTiles, IReadOnlyList<Meld> melds)
        => IsPureSuit(allTiles)
            || IsAllTwoFiveEight(allTiles)
            || IsSanTong(allTiles)
            || melds.All(static meld => meld.Type is MeldType.Pung or MeldType.Kong);

    private static bool IsSanTong(IReadOnlyList<Tile> allTiles)
    {
        var tripletGroups = allTiles
            .Where(static tile => AllowedSuits.Contains(tile.Suit))
            .GroupBy(static tile => tile)
            .Where(group => group.Count() >= 3)
            .ToList();

        if (tripletGroups.Count < 3)
        {
            return false;
        }

        var tripletsGroupedByRank = tripletGroups
            .Select(group => group.Key)
            .GroupBy(static tile => tile.Rank)
            .ToList();

        foreach (var rankGroup in tripletsGroupedByRank)
        {
            var suits = rankGroup.Select(tile => tile.Suit).Distinct().ToList();
            if (suits.Count == 3)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPureSuit(IReadOnlyList<Tile> allTiles)
    {
        if (allTiles.Count == 0)
        {
            return false;
        }

        var suit = allTiles[0].Suit;
        return allTiles.All(tile => tile.Suit == suit);
    }

    private static bool IsAllTwoFiveEight(IReadOnlyList<Tile> allTiles)
        => allTiles.All(tile => SmallWinPairRanks.Contains(tile.Rank));

    private static bool HasAllPungsBigWinCandidate(IReadOnlyList<Tile> ownedTiles, IReadOnlyList<Tile> meldTiles)
    {
        if (ownedTiles.Count == 0)
        {
            return false;
        }

        if (meldTiles.Count > 0)
        {
            var meldGroups = meldTiles
                .GroupBy(static tile => tile)
                .Select(static group => group.Count());

            if (meldGroups.Any(static count => count is not 3 and not 4))
            {
                return false;
            }
        }

        var pungCandidates = ownedTiles
            .GroupBy(static tile => tile)
            .Count(static group => group.Count() >= 3);

        return pungCandidates >= 3;
    }

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

        var remainingKongSlots = requiredKongs - kongsUsed;
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

        if (remainingKongSlots == remainingMeldSlots && counts[nextTile] < 4)
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

        if (remainingKongSlots == remainingMeldSlots)
        {
            return false;
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

        if (IsAllowedTile(nextTile))
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
