using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotMeldAdvisor
{
    private const int KongPotentialOutsDropTolerance = 0;
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
        => TrySelectConcealedKong(handTiles, Array.Empty<Tile>(), Array.Empty<Tile>(), ruleset, out kongTile);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
        => TrySelectConcealedKong(handTiles, meldTiles, Array.Empty<Tile>(), ruleset, out kongTile);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(visibleTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null)
            || meldTiles.Any(static tile => tile is null)
            || visibleTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(handTiles));
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var visibleCounts = BuildVisibleCounts(visibleTiles);

        var candidates = handCounts
            .Where(entry => entry.Value == 4)
            .Select(entry => entry.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            kongTile = null!;
            return false;
        }

        if (ShouldAvoidSevenPairsConcealedKong(handCounts, meldTiles, ownedCounts, visibleCounts))
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
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs, handCounts, ownedCounts, visibleCounts))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var bestPreKongOuts = CountBestWinningOutsWithoutKong(handTiles, meldTiles, visibleCounts, ruleset, requireMinimumFan: true);
        var candidateOuts = candidates
            .ToDictionary(candidate => candidate, candidate => CountWinningOutsAfterConcealedKong(handTiles, meldTiles, visibleCounts, candidate, ruleset, requireMinimumFan: true));
        if (bestPreKongOuts > 0)
        {
            var candidatesThatKeepOuts = candidates
                .Where(candidate => candidateOuts[candidate] > 0)
                .ToList();

            if (candidatesThatKeepOuts.Count == 0)
            {
                kongTile = null!;
                return false;
            }

            candidates = candidatesThatKeepOuts;
        }

        if (candidates.Count > 1)
        {
            var maxPostKongOuts = candidates.Max(candidate => candidateOuts[candidate]);
            if (maxPostKongOuts > 0)
            {
                candidates = candidates
                    .Where(candidate => candidateOuts[candidate] == maxPostKongOuts)
                    .ToList();
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial && bestPreKongOuts == 0)
        {
            var bestPreKongPotentialOuts = CountBestWinningOutsWithoutKong(
                handTiles,
                meldTiles,
                visibleCounts,
                ruleset,
                requireMinimumFan: false);

            if (bestPreKongPotentialOuts > 0)
            {
                var candidatePotentialOuts = candidates
                    .ToDictionary(candidate => candidate,
                        candidate => CountWinningOutsAfterConcealedKong(
                            handTiles,
                            meldTiles,
                            visibleCounts,
                            candidate,
                            ruleset,
                            requireMinimumFan: false));

                var candidatesThatKeepPotential = candidates
                    .Where(candidate => candidatePotentialOuts[candidate] + KongPotentialOutsDropTolerance >= bestPreKongPotentialOuts)
                    .ToList();

                if (candidatesThatKeepPotential.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }

                candidates = candidatesThatKeepPotential;

                if (candidates.Count > 1)
                {
                    var maxPostKongPotentialOuts = candidates.Max(candidate => candidatePotentialOuts[candidate]);
                    candidates = candidates
                        .Where(candidate => candidatePotentialOuts[candidate] == maxPostKongPotentialOuts)
                        .ToList();
                }
            }
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
        => TrySelectPromotedKong(handTiles, meldTiles, Array.Empty<Tile>(), ruleset, out kongTile);

    public static bool TrySelectPromotedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(visibleTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null)
            || meldTiles.Any(static tile => tile is null)
            || visibleTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var meldCounts = meldTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var visibleCounts = BuildVisibleCounts(visibleTiles);

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
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs, handCounts, ownedCounts, visibleCounts))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var bestPreKongOuts = CountBestWinningOutsWithoutKong(handTiles, meldTiles, visibleCounts, ruleset, requireMinimumFan: true);
        var candidateOuts = candidates
            .ToDictionary(candidate => candidate, candidate => CountWinningOutsAfterPromotedKong(handTiles, meldTiles, visibleCounts, candidate, ruleset, requireMinimumFan: true));
        if (bestPreKongOuts > 0)
        {
            var candidatesThatKeepOuts = candidates
                .Where(candidate => candidateOuts[candidate] > 0)
                .ToList();

            if (candidatesThatKeepOuts.Count == 0)
            {
                kongTile = null!;
                return false;
            }

            candidates = candidatesThatKeepOuts;
        }

        if (candidates.Count > 1)
        {
            var maxPostKongOuts = candidates.Max(candidate => candidateOuts[candidate]);
            if (maxPostKongOuts > 0)
            {
                candidates = candidates
                    .Where(candidate => candidateOuts[candidate] == maxPostKongOuts)
                    .ToList();
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial && bestPreKongOuts == 0)
        {
            var bestPreKongPotentialOuts = CountBestWinningOutsWithoutKong(
                handTiles,
                meldTiles,
                visibleCounts,
                ruleset,
                requireMinimumFan: false);

            if (bestPreKongPotentialOuts > 0)
            {
                var candidatePotentialOuts = candidates
                    .ToDictionary(candidate => candidate,
                        candidate => CountWinningOutsAfterPromotedKong(
                            handTiles,
                            meldTiles,
                            visibleCounts,
                            candidate,
                            ruleset,
                            requireMinimumFan: false));

                var candidatesThatKeepPotential = candidates
                    .Where(candidate => candidatePotentialOuts[candidate] + KongPotentialOutsDropTolerance >= bestPreKongPotentialOuts)
                    .ToList();

                if (candidatesThatKeepPotential.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }

                candidates = candidatesThatKeepPotential;

                if (candidates.Count > 1)
                {
                    var maxPostKongPotentialOuts = candidates.Max(candidate => candidatePotentialOuts[candidate]);
                    candidates = candidates
                        .Where(candidate => candidatePotentialOuts[candidate] == maxPostKongPotentialOuts)
                        .ToList();
                }
            }
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
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (meldTiles.Count > 0)
        {
            return false;
        }

        var pairCount = handCounts.Values.Sum(static count => count / 2);
        if (pairCount < 6)
        {
            return false;
        }

        if (visibleCounts.Count == 0)
        {
            return true;
        }

        var singletonTiles = handCounts
            .Where(entry => entry.Value == 1)
            .Select(entry => entry.Key)
            .ToList();

        if (singletonTiles.Count == 0)
        {
            return true;
        }

        foreach (var tile in singletonTiles)
        {
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);
            var remainingCopies = 4 - ownedCount - visibleCount;
            if (remainingCopies > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountBestWinningOutsWithoutKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
    {
        var bestOuts = 0;

        for (var discardIndex = 0; discardIndex < handTiles.Count; discardIndex++)
        {
            var reducedHand = handTiles
                .Where((_, index) => index != discardIndex)
                .ToList();

            var outs = CountWinningDrawOutsAfterSupplementDraw(
                reducedHand,
                meldTiles,
                visibleCounts,
                ruleset,
                requireMinimumFan);
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
        IReadOnlyDictionary<Tile, int> visibleCounts,
        Tile kongTile,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
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

        return CountWinningDrawOutsAfterSupplementDraw(
            handAfterKong,
            meldAfterKong,
            visibleCounts,
            ruleset,
            requireMinimumFan);
    }

    private static int CountWinningOutsAfterPromotedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        Tile kongTile,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
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

        return CountWinningDrawOutsAfterSupplementDraw(
            handAfterKong,
            meldAfterKong,
            visibleCounts,
            ruleset,
            requireMinimumFan);
    }

    private static int CountWinningDrawOutsAfterSupplementDraw(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
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
            visibleCounts.TryGetValue(drawTileType, out var visibleCount);
            var remainingCopies = 4 - drawTileCount - visibleCount;
            if (remainingCopies <= 0)
            {
                continue;
            }

            var candidateTiles = ownedTiles
                .Append(drawTileType)
                .ToList();
            if (IsWinningHand(candidateTiles, ruleset, requireMinimumFan))
            {
                winningOuts++;
            }
        }

        return winningOuts;
    }

    private static bool IsWinningHand(
        IReadOnlyList<Tile> tiles,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
    {
        if (requireMinimumFan)
        {
            return RulesetHandAnalyzer.IsWinningHand(tiles, ruleset);
        }

        return ruleset == MahjongRuleset.Changsha258
            ? Changsha258Ruleset.TryGetWinningHand(tiles, out _)
            : HandAnalyzer.TryGetWinningHand(tiles, out _);
    }

    private static IReadOnlyDictionary<Tile, int> BuildVisibleCounts(IReadOnlyList<Tile> visibleTiles)
        => visibleTiles.Count == 0
            ? new Dictionary<Tile, int>()
            : visibleTiles
                .GroupBy(static tile => tile)
                .ToDictionary(group => group.Key, group => group.Count());

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

    private static bool ShouldAvoidChangshaSmallWinKong(
        Tile candidate,
        IReadOnlySet<Tile> smallWinPairs,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (!Changsha258Ruleset.IsSmallWinPair(candidate))
        {
            return false;
        }

        if (smallWinPairs.Count == 0)
        {
            if (HasLiveAlternateSmallWinSingleton(candidate, handCounts, ownedCounts, visibleCounts))
            {
                return false;
            }

            return true;
        }

        if (smallWinPairs.Count == 1 && smallWinPairs.Contains(candidate))
        {
            return !HasLiveAlternateSmallWinSingleton(candidate, handCounts, ownedCounts, visibleCounts);
        }

        return false;
    }

    private static bool HasLiveAlternateSmallWinSingleton(
        Tile candidate,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        var liveAlternateCount = 0;
        var hasStrongAlternateSingleton = false;

        foreach (var entry in handCounts)
        {
            if (entry.Value != 1)
            {
                continue;
            }

            if (!Changsha258Ruleset.IsSmallWinPair(entry.Key))
            {
                continue;
            }

            if (entry.Key.Equals(candidate))
            {
                continue;
            }

            if (visibleCounts.Count == 0)
            {
                liveAlternateCount++;
            }
            else
            {
                ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                var remainingCopies = 4 - ownedCount - visibleCount;
                if (remainingCopies > 0)
                {
                    liveAlternateCount++;
                }

                if (remainingCopies >= 2)
                {
                    hasStrongAlternateSingleton = true;
                }
            }

            if (liveAlternateCount >= 2)
            {
                return true;
            }
        }

        return hasStrongAlternateSingleton;
    }

    private static bool IsTerminalSuitedTile(Tile tile)
        => !tile.IsHonor && tile.Rank is 1 or 9;
}
