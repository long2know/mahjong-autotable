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

        if (TryBuildSevenPairs(counts, out hand))
        {
            return true;
        }

        if (TryBuildThirteenOrphans(counts, out hand))
        {
            return true;
        }

        foreach (var tile in counts.Keys.ToList())
        {
            if (counts[tile] >= 2)
            {
                counts[tile] -= 2;
                var melds = new List<Meld>();

                if (TryFormMelds(counts, melds))
                {
                    var pattern = IsAllGreen(tiles)
                        ? WinningPattern.AllGreen
                        : tiles.All(static t => t.IsHonor)
                            ? WinningPattern.AllHonors
                            : tiles.All(static t => !t.IsHonor && !t.IsFlower && (t.Rank is 1 or 9))
                                ? WinningPattern.AllTerminals
                                : tiles.All(static t => t.IsHonor || (!t.IsFlower && (t.Rank is 1 or 9)))
                                    ? WinningPattern.AllTerminalsAndHonors
                                    : WinningPattern.Standard;

                    hand = new WinningHand(melds, tile, pattern);
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

    private static bool TryBuildSevenPairs(Dictionary<Tile, int> counts, out WinningHand? hand)
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

        var pairTile = counts.First(kvp => kvp.Value >= 2).Key;
        hand = new WinningHand(Array.Empty<Meld>(), pairTile, WinningPattern.SevenPairs);
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

        var duplicateTile = counts.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).SingleOrDefault();
        if (duplicateTile is null)
        {
            return false;
        }

        if (counts.Any(kvp => kvp.Value > 2))
        {
            return false;
        }

        hand = new WinningHand(Array.Empty<Meld>(), duplicateTile, WinningPattern.ThirteenOrphans);
        return true;
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

    private static bool IsAllGreen(IReadOnlyList<Tile> tiles)
        => tiles.All(static tile =>
            tile switch
            {
                { Suit: TileSuit.Dragons, Dragon: Dragon.Green } => true,
                { Suit: TileSuit.Bamboos, Rank: 2 or 3 or 4 or 6 or 8 } => true,
                _ => false
            });

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
