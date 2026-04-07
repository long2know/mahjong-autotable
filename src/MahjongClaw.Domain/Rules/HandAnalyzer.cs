using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class HandAnalyzer
{
    private const int BaseWinningTileCount = 14;
    private const int RequiredMeldCount = 4;
    private const int MaxKongs = 4;
    private const int MaxWinningTileCount = BaseWinningTileCount + MaxKongs;

    public static bool IsWinningHand(IReadOnlyList<Tile> tiles)
        => TryGetWinningHand(tiles, out _);

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

        if (tiles.Any(static tile => tile.IsFlower))
        {
            return false;
        }

        var counts = BuildCounts(tiles);
        if (counts.Values.Any(static count => count > 4))
        {
            return false;
        }

        if (requiredKongs == 0 && TryBuildSevenPairs(tiles, counts, out hand))
        {
            return true;
        }

        if (requiredKongs == 0 && TryBuildThirteenOrphans(counts, out hand))
        {
            return true;
        }

        return TryBuildStandardHand(tiles, counts, requiredKongs, out hand);
    }

    private static bool TryGetRequiredKongCount(int tileCount, out int requiredKongs)
    {
        requiredKongs = 0;

        if (tileCount < BaseWinningTileCount || tileCount > MaxWinningTileCount)
        {
            return false;
        }

        requiredKongs = tileCount - BaseWinningTileCount;
        return true;
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

    private static bool TryBuildSevenPairs(IReadOnlyList<Tile> tiles, Dictionary<Tile, int> counts, out WinningHand? hand)
    {
        hand = null;

        if (counts.Values.Any(count => count % 2 != 0))
        {
            return false;
        }

        var pairCount = counts.Values.Sum(count => count / 2);
        if (pairCount != 7)
        {
            return false;
        }

        var pairTile = GetOrderedTiles(counts).First(tile => counts[tile] >= 2);
        var pattern = DetermineTileOnlyPattern(tiles) ?? WinningPattern.SevenPairs;
        hand = new WinningHand(Array.Empty<Meld>(), pairTile, pattern);
        return true;
    }

    private static bool TryBuildThirteenOrphans(Dictionary<Tile, int> counts, out WinningHand? hand)
    {
        hand = null;

        var terminalAndHonorTiles = BuildThirteenOrphansTileSet();
        if (counts.Keys.Any(tile => !terminalAndHonorTiles.Contains(tile)))
        {
            return false;
        }

        if (terminalAndHonorTiles.Any(tile => !counts.TryGetValue(tile, out var count) || count < 1))
        {
            return false;
        }

        var duplicateTiles = counts.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).ToList();
        if (duplicateTiles.Count != 1)
        {
            return false;
        }

        var duplicateTile = duplicateTiles[0];

        if (counts.Any(kvp => kvp.Value > 2))
        {
            return false;
        }

        hand = new WinningHand(Array.Empty<Meld>(), duplicateTile, WinningPattern.ThirteenOrphans);
        return true;
    }

    private static bool TryBuildStandardHand(
        IReadOnlyList<Tile> tiles,
        Dictionary<Tile, int> counts,
        int requiredKongs,
        out WinningHand? hand)
    {
        hand = null;
        var bestRank = int.MinValue;
        var bestScore = int.MinValue;

        foreach (var tile in GetOrderedTiles(counts))
        {
            if (counts[tile] < 2)
            {
                continue;
            }

            var candidateCounts = new Dictionary<Tile, int>(counts);
            candidateCounts[tile] -= 2;

            if (!HasRequiredKongs(candidateCounts, requiredKongs))
            {
                continue;
            }

            var melds = new List<Meld>();
            if (!TryFormMelds(candidateCounts, melds, requiredKongs, kongsUsed: 0))
            {
                continue;
            }

            var pattern = DetermineMeldPattern(tiles, melds, tile);
            var rank = GetPatternRank(pattern);
            var candidateHand = new WinningHand(melds.ToList(), tile, pattern);
            var candidateScore = candidateHand.BasicScore;

            if (rank < bestRank || (rank == bestRank && candidateScore <= bestScore))
            {
                continue;
            }

            bestRank = rank;
            bestScore = candidateScore;
            hand = candidateHand;
        }

        return hand is not null;
    }

    private static HashSet<Tile> BuildThirteenOrphansTileSet()
        => new()
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

    private static WinningPattern DetermineMeldPattern(IReadOnlyList<Tile> tiles, IReadOnlyList<Meld> melds, Tile pair)
        => IsNineGates(tiles)
            ? WinningPattern.NineGates
            : IsBigFourWinds(melds)
                ? WinningPattern.BigFourWinds
            : IsBigThreeDragons(melds)
                ? WinningPattern.BigThreeDragons
                : IsLittleFourWinds(melds, pair)
                    ? WinningPattern.LittleFourWinds
                : IsLittleThreeDragons(melds, pair)
                    ? WinningPattern.LittleThreeDragons
                    : DetermineTileOnlyPattern(tiles)
                        ?? (IsAllPungs(melds) ? WinningPattern.AllPungs : WinningPattern.Standard);

    private static int GetPatternRank(WinningPattern pattern)
        => pattern switch
        {
            WinningPattern.ThirteenOrphans => 150,
            WinningPattern.NineGates => 140,
            WinningPattern.BigFourWinds => 130,
            WinningPattern.BigThreeDragons => 120,
            WinningPattern.LittleFourWinds => 110,
            WinningPattern.LittleThreeDragons => 100,
            WinningPattern.AllGreen => 90,
            WinningPattern.AllHonors => 80,
            WinningPattern.AllTerminals => 70,
            WinningPattern.AllTerminalsAndHonors => 60,
            WinningPattern.SevenPairs => 50,
            WinningPattern.AllPungs => 40,
            WinningPattern.Standard => 10,
            _ => 0
        };

    private static WinningPattern? DetermineTileOnlyPattern(IReadOnlyList<Tile> tiles)
    {
        if (IsAllGreen(tiles))
        {
            return WinningPattern.AllGreen;
        }

        if (tiles.All(static t => t.IsHonor))
        {
            return WinningPattern.AllHonors;
        }

        if (tiles.All(static t => !t.IsHonor && !t.IsFlower && (t.Rank is 1 or 9)))
        {
            return WinningPattern.AllTerminals;
        }

        if (tiles.All(static t => t.IsHonor || (!t.IsFlower && (t.Rank is 1 or 9))))
        {
            return WinningPattern.AllTerminalsAndHonors;
        }

        return null;
    }

    private static bool IsAllGreen(IReadOnlyList<Tile> tiles)
        => tiles.All(static tile =>
            tile switch
            {
                { Suit: TileSuit.Dragons, Dragon: Dragon.Green } => true,
                { Suit: TileSuit.Bamboos, Rank: 2 or 3 or 4 or 6 or 8 } => true,
                _ => false
            });

    private static bool IsNineGates(IReadOnlyList<Tile> tiles)
    {
        if (tiles.Count != BaseWinningTileCount)
        {
            return false;
        }

        if (tiles.Any(static tile => tile.IsHonor || tile.IsFlower))
        {
            return false;
        }

        var suit = tiles[0].Suit;
        if (tiles.Any(tile => tile.Suit != suit))
        {
            return false;
        }

        var rankCounts = tiles
            .GroupBy(static tile => tile.Rank)
            .ToDictionary(static group => group.Key, static group => group.Count());

        for (var rank = 1; rank <= 9; rank++)
        {
            if (!rankCounts.TryGetValue(rank, out var count))
            {
                return false;
            }

            var minimum = rank is 1 or 9 ? 3 : 1;
            if (count < minimum)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBigThreeDragons(IReadOnlyList<Meld> melds)
    {
        var dragonTripletCount = melds.Count(static meld =>
            (meld.Type is MeldType.Pung or MeldType.Kong) &&
            meld.Tiles.FirstOrDefault() is { Suit: TileSuit.Dragons } dragonTile &&
            dragonTile.Dragon is Dragon.Red or Dragon.Green or Dragon.White);

        return dragonTripletCount == 3;
    }

    private static bool IsBigFourWinds(IReadOnlyList<Meld> melds)
    {
        var windTripletCount = melds.Count(static meld =>
            (meld.Type is MeldType.Pung or MeldType.Kong) &&
            meld.Tiles.FirstOrDefault() is { Suit: TileSuit.Winds, Wind: Wind.East or Wind.South or Wind.West or Wind.North });

        return windTripletCount == 4;
    }

    private static bool IsLittleThreeDragons(IReadOnlyList<Meld> melds, Tile pair)
    {
        if (pair is not { Suit: TileSuit.Dragons, Dragon: Dragon.Red or Dragon.Green or Dragon.White })
        {
            return false;
        }

        var dragonTripletCount = melds.Count(static meld =>
            (meld.Type is MeldType.Pung or MeldType.Kong) &&
            meld.Tiles.FirstOrDefault() is { Suit: TileSuit.Dragons, Dragon: Dragon.Red or Dragon.Green or Dragon.White });

        return dragonTripletCount == 2;
    }

    private static bool IsLittleFourWinds(IReadOnlyList<Meld> melds, Tile pair)
    {
        if (pair is not { Suit: TileSuit.Winds, Wind: Wind.East or Wind.South or Wind.West or Wind.North })
        {
            return false;
        }

        var windTripletCount = melds.Count(static meld =>
            (meld.Type is MeldType.Pung or MeldType.Kong) &&
            meld.Tiles.FirstOrDefault() is { Suit: TileSuit.Winds, Wind: Wind.East or Wind.South or Wind.West or Wind.North });

        return windTripletCount == 3;
    }

    private static bool IsAllPungs(IReadOnlyList<Meld> melds)
        => melds.Count > 0 && melds.All(static meld => meld.Type is MeldType.Pung or MeldType.Kong);

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

    private static bool TryFormMelds(Dictionary<Tile, int> counts, List<Meld> melds, int requiredKongs, int kongsUsed)
    {
        if (kongsUsed > requiredKongs)
        {
            return false;
        }

        var remainingMeldSlots = RequiredMeldCount - melds.Count;
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

        var nextTile = counts
            .Where(static kvp => kvp.Value > 0)
            .Select(static kvp => kvp.Key)
            .OrderBy(GetTileSortBucket)
            .ThenBy(GetTileSortValue)
            .FirstOrDefault();

        if (nextTile is null)
        {
            return melds.Count == RequiredMeldCount && kongsUsed == requiredKongs;
        }

        if (remainingMeldSlots == 0)
        {
            return false;
        }

        var remainingKongSlots = requiredKongs - kongsUsed;
        if (remainingKongSlots == remainingMeldSlots && counts[nextTile] < 4)
        {
            return false;
        }

        if (counts[nextTile] >= 4 && kongsUsed < requiredKongs)
        {
            counts[nextTile] -= 4;
            melds.Add(new Meld(MeldType.Kong, new[] { nextTile, nextTile, nextTile, nextTile }));

            if (TryFormMelds(counts, melds, requiredKongs, kongsUsed + 1))
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

            if (TryFormMelds(counts, melds, requiredKongs, kongsUsed))
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

                    if (TryFormMelds(counts, melds, requiredKongs, kongsUsed))
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
