using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotMeldAdvisor
{
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
        => TrySelectConcealedKong(handTiles, Array.Empty<Tile>(), ruleset, out kongTile);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(handTiles));
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var candidates = handCounts
            .Where(entry => entry.Value == 4)
            .Select(entry => entry.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            kongTile = null!;
            return false;
        }

        if (ShouldAvoidSevenPairsConcealedKong(handCounts, meldTiles))
        {
            kongTile = null!;
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
            {
                var smallWinPairs = handCounts
                    .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .ToHashSet();

                candidates = candidates
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var bestPreKongOuts = CountBestWinningOutsWithoutKong(handTiles, meldTiles, ruleset);
        if (bestPreKongOuts > 0)
        {
            var candidatesThatKeepOuts = candidates
                .Where(candidate => CountWinningOutsAfterConcealedKong(handTiles, meldTiles, candidate, ruleset) > 0)
                .ToList();

            if (candidatesThatKeepOuts.Count == 0)
            {
                kongTile = null!;
                return false;
            }

            candidates = candidatesThatKeepOuts;
        }

        var prioritizedCandidates = candidates
            .OrderBy(candidate => ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(candidate) ? 1 : 0)
            .ThenBy(candidate => candidate.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate) ? 0 : 1)
            .ThenBy(candidate => candidate.Suit)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Wind.HasValue ? (int)candidate.Wind.Value : -1)
            .ThenBy(candidate => candidate.Dragon.HasValue ? (int)candidate.Dragon.Value : -1)
            .ToList();

        kongTile = prioritizedCandidates[0];
        return true;
    }

    public static bool TrySelectPromotedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var meldCounts = meldTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var candidates = meldCounts
            .Where(entry => entry.Value == 3 && handCounts.ContainsKey(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            kongTile = null!;
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
            {
                var smallWinPairs = handCounts
                    .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .ToHashSet();

                candidates = candidates
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var bestPreKongOuts = CountBestWinningOutsWithoutKong(handTiles, meldTiles, ruleset);
        if (bestPreKongOuts > 0)
        {
            var candidatesThatKeepOuts = candidates
                .Where(candidate => CountWinningOutsAfterPromotedKong(handTiles, meldTiles, candidate, ruleset) > 0)
                .ToList();

            if (candidatesThatKeepOuts.Count == 0)
            {
                kongTile = null!;
                return false;
            }

            candidates = candidatesThatKeepOuts;
        }

        var prioritizedCandidates = candidates
            .OrderBy(candidate => ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(candidate) ? 1 : 0)
            .ThenBy(candidate => candidate.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate) ? 0 : 1)
            .ThenBy(candidate => candidate.Suit)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Wind.HasValue ? (int)candidate.Wind.Value : -1)
            .ThenBy(candidate => candidate.Dragon.HasValue ? (int)candidate.Dragon.Value : -1)
            .ToList();

        kongTile = prioritizedCandidates[0];
        return true;
    }

    private static bool ShouldAvoidSevenPairsConcealedKong(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyList<Tile> meldTiles)
    {
        if (meldTiles.Count > 0)
        {
            return false;
        }

        var pairCount = handCounts.Values.Sum(static count => count / 2);
        return pairCount >= 6;
    }

    private static int CountBestWinningOutsWithoutKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset)
    {
        var bestOuts = 0;

        for (var discardIndex = 0; discardIndex < handTiles.Count; discardIndex++)
        {
            var reducedHand = handTiles
                .Where((_, index) => index != discardIndex)
                .ToList();

            var outs = CountWinningDrawOutsAfterSupplementDraw(reducedHand, meldTiles, ruleset);
            if (outs > bestOuts)
            {
                bestOuts = outs;
            }
        }

        return bestOuts;
    }

    private static int CountWinningOutsAfterConcealedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile kongTile,
        MahjongRuleset ruleset)
    {
        var handAfterKong = new List<Tile>(handTiles);

        for (var i = 0; i < 4; i++)
        {
            var index = handAfterKong.FindIndex(tile => tile.Equals(kongTile));
            if (index < 0)
            {
                return 0;
            }

            handAfterKong.RemoveAt(index);
        }

        var meldAfterKong = meldTiles
            .Concat(Enumerable.Repeat(kongTile, 4))
            .ToList();

        return CountWinningDrawOutsAfterSupplementDraw(handAfterKong, meldAfterKong, ruleset);
    }

    private static int CountWinningOutsAfterPromotedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile kongTile,
        MahjongRuleset ruleset)
    {
        var handAfterKong = new List<Tile>(handTiles);
        var kongTileIndex = handAfterKong.FindIndex(tile => tile.Equals(kongTile));
        if (kongTileIndex < 0)
        {
            return 0;
        }

        handAfterKong.RemoveAt(kongTileIndex);

        var meldAfterKong = meldTiles
            .Concat(new[] { kongTile })
            .ToList();

        return CountWinningDrawOutsAfterSupplementDraw(handAfterKong, meldAfterKong, ruleset);
    }

    private static int CountWinningDrawOutsAfterSupplementDraw(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset)
    {
        var drawTileTypes = ruleset == MahjongRuleset.Changsha258
            ? Changsha258DrawTileTypes
            : ChineseOfficialDrawTileTypes;

        var ownedTiles = handTiles
            .Concat(meldTiles)
            .ToList();
        var ownedCounts = ownedTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOuts = 0;

        foreach (var drawTileType in drawTileTypes)
        {
            ownedCounts.TryGetValue(drawTileType, out var drawTileCount);
            if (drawTileCount >= 4)
            {
                continue;
            }

            var candidateTiles = ownedTiles
                .Append(drawTileType)
                .ToList();
            if (RulesetHandAnalyzer.IsWinningHand(candidateTiles, ruleset))
            {
                winningOuts++;
            }
        }

        return winningOuts;
    }

    private static IReadOnlyList<Tile> BuildDrawTileTypes(MahjongRuleset ruleset)
    {
        var suitedTiles = new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos }
            .SelectMany(static suit => Enumerable.Range(1, 9).Select(rank => new Tile(suit, rank)));

        if (ruleset == MahjongRuleset.Changsha258)
        {
            return suitedTiles.ToArray();
        }

        var windTiles = Enum
            .GetValues<Wind>()
            .Select(wind => new Tile(TileSuit.Winds, 0, wind));
        var dragonTiles = Enum
            .GetValues<Dragon>()
            .Select(dragon => new Tile(TileSuit.Dragons, 0, null, dragon));

        return suitedTiles
            .Concat(windTiles)
            .Concat(dragonTiles)
            .ToArray();
    }

    private static bool ShouldAvoidChangshaSmallWinKong(Tile candidate, IReadOnlySet<Tile> smallWinPairs)
    {
        if (!Changsha258Ruleset.IsSmallWinPair(candidate))
        {
            return false;
        }

        if (smallWinPairs.Count == 0)
        {
            return true;
        }

        if (smallWinPairs.Count == 1 && smallWinPairs.Contains(candidate))
        {
            return true;
        }

        return false;
    }

    private static bool IsTerminalSuitedTile(Tile tile)
        => !tile.IsHonor && tile.Rank is 1 or 9;
}
