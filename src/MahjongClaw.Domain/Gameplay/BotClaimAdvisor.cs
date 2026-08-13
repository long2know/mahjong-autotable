using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotClaimAdvisor
{
    private const int ExposedKongOutsDropTolerance = 0;
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);
    private readonly record struct WinningOutSummary(int TileTypes, int RemainingCopies);

    public static bool ShouldClaimDiscardWin(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial,
        MissedWinLock? missedWinLock = null)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(discardedTile);

        if (missedWinLock is not null && missedWinLock.IsLockedOnTile(discardedTile))
        {
            return false;
        }

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var declaredKongCount = meldTiles
            .GroupBy(static tile => tile)
            .Count(group => group.Count() == 4);

        if (!TurnFlowValidator.CanDraw(handTiles.Count, meldTiles.Count, declaredKongCount))
        {
            return false;
        }

        var candidateTiles = handTiles
            .Concat(meldTiles)
            .Append(discardedTile)
            .ToList();

        if (!RulesetHandAnalyzer.TryGetWinningHand(candidateTiles, ruleset, out var winningHand)
            || winningHand is null)
        {
            return false;
        }

        return ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset);
    }

    public static bool TrySelectExposedKongClaim(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        MahjongRuleset ruleset,
        out IReadOnlyList<Tile> meldTilesForClaim)
        => TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            Array.Empty<Tile>(),
            ruleset,
            out meldTilesForClaim);

    public static bool TrySelectExposedKongClaim(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset,
        out IReadOnlyList<Tile> meldTilesForClaim)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(discardedTile);
        ArgumentNullException.ThrowIfNull(visibleTiles);

        if (handTiles.Any(static tile => tile is null)
            || meldTiles.Any(static tile => tile is null)
            || visibleTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        meldTilesForClaim = Array.Empty<Tile>();

        if (ruleset == MahjongRuleset.Changsha258 && !Changsha258Ruleset.IsAllowedTile(discardedTile))
        {
            return false;
        }

        var matchingCount = handTiles.Count(tile => tile.Equals(discardedTile));
        if (matchingCount < 3)
        {
            return false;
        }

        var visibleCounts = BuildVisibleCounts(visibleTiles);
        // visibleTiles should exclude the current discard; add it here so outs reflect the new visible tile.
        visibleCounts.TryGetValue(discardedTile, out var discardCount);
        visibleCounts[discardedTile] = discardCount + 1;

        var allowChangshaFlexibleEye = ruleset == MahjongRuleset.Changsha258
            && Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);

        if (ruleset == MahjongRuleset.Changsha258
            && !Changsha258Ruleset.IsSmallWinPair(discardedTile)
            && ShouldAvoidChangshaSevenPairsExposedKong(handTiles, meldTiles, visibleCounts))
        {
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(discardedTile))
        {
            var hasSevenPairsLean = IsChangshaSevenPairsLean(handTiles, meldTiles);

            if (!allowChangshaFlexibleEye || hasSevenPairsLean)
            {
                var hasAlternateSmallWinPairCandidate = HasAlternateSmallWinPairCandidate(
                    handTiles,
                    meldTiles,
                    discardedTile,
                    visibleCounts);

                if (!hasAlternateSmallWinPairCandidate)
                {
                    return false;
                }
            }
        }

        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, meldTiles, visibleCounts, ruleset, requireMinimumFan: true);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            meldTiles,
            discardedTile,
            visibleCounts,
            ruleset,
            requireMinimumFan: true);
        if (preClaimOuts.TileTypes > 0 && postClaimOuts.TileTypes == 0)
        {
            return false;
        }

        if (ruleset == MahjongRuleset.ChineseOfficial && preClaimOuts.TileTypes == 0)
        {
            var preClaimPotentialOuts = CountWinningDrawOutsAfterDraw(
                handTiles,
                meldTiles,
                visibleCounts,
                ruleset,
                requireMinimumFan: false);

            if (preClaimPotentialOuts.TileTypes > 0)
            {
                var postClaimPotentialOuts = CountWinningDrawOutsAfterExposedKong(
                    handTiles,
                    meldTiles,
                    discardedTile,
                    visibleCounts,
                    ruleset,
                    requireMinimumFan: false);

                if (IsOutsDropBeyondTolerance(preClaimPotentialOuts, postClaimPotentialOuts, ExposedKongOutsDropTolerance))
                {
                    return false;
                }
            }
        }

        if (preClaimOuts.TileTypes > 0
            && IsOutsDropBeyondTolerance(preClaimOuts, postClaimOuts, ExposedKongOutsDropTolerance)
            && !(allowChangshaFlexibleEye && Changsha258Ruleset.IsSmallWinPair(discardedTile)))
        {
            return false;
        }

        meldTilesForClaim = Enumerable.Repeat(discardedTile, 3).ToArray();
        return true;
    }

    private static WinningOutSummary CountWinningDrawOutsAfterDraw(
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
        var winningOutCopies = 0;

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

            WinningHand? winningHand;
            if (!requireMinimumFan)
            {
                if (ruleset == MahjongRuleset.Changsha258)
                {
                    if (!Changsha258Ruleset.TryGetWinningHand(candidateTiles, out winningHand))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!HandAnalyzer.TryGetWinningHand(candidateTiles, out winningHand))
                    {
                        continue;
                    }
                }
            }
            else
            {
                if (!RulesetHandAnalyzer.TryGetWinningHand(candidateTiles, ruleset, out winningHand))
                {
                    continue;
                }
            }

            if (winningHand is null)
            {
                continue;
            }

            if (requireMinimumFan && !ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset))
            {
                continue;
            }

            winningOuts++;
            winningOutCopies += remainingCopies;

        }

        return new WinningOutSummary(winningOuts, winningOutCopies);
    }

    private static WinningOutSummary CountWinningDrawOutsAfterExposedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
    {
        var handAfterKong = new List<Tile>(handTiles);

        for (var i = 0; i < 3; i++)
        {
            var index = handAfterKong.FindIndex(tile => tile.Equals(discardedTile));
            if (index < 0)
            {
                return default;
            }

            handAfterKong.RemoveAt(index);
        }

        var meldAfterKong = meldTiles
            .Concat(Enumerable.Repeat(discardedTile, 4))
            .ToList();

        return CountWinningDrawOutsAfterDraw(handAfterKong, meldAfterKong, visibleCounts, ruleset, requireMinimumFan);
    }

    private static bool IsOutsDropBeyondTolerance(WinningOutSummary preClaimOuts, WinningOutSummary postClaimOuts, int tolerance)
    {
        if (postClaimOuts.TileTypes + tolerance < preClaimOuts.TileTypes)
        {
            return true;
        }

        if (postClaimOuts.TileTypes + tolerance == preClaimOuts.TileTypes)
        {
            return postClaimOuts.RemainingCopies + tolerance < preClaimOuts.RemainingCopies;
        }

        return false;
    }

    private static bool IsChangshaSevenPairsLean(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles)
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

    private static bool ShouldAvoidChangshaSevenPairsExposedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (meldTiles.Count > 0)
        {
            return false;
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

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

        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

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

    private static bool HasAlternateSmallWinPairCandidate(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var singletonLiveCopyCount = 0;

        foreach (var entry in handCounts)
        {
            if (!Changsha258Ruleset.IsSmallWinPair(entry.Key) || entry.Key.Equals(discardedTile))
            {
                continue;
            }

            if (entry.Value >= 2)
            {
                return true;
            }

            if (entry.Value == 1)
            {
                ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                var remainingCopies = 4 - ownedCount - visibleCount;

                if (remainingCopies >= 2)
                {
                    return true;
                }

                if (remainingCopies == 1)
                {
                    singletonLiveCopyCount += 1;
                }
            }
        }

        return singletonLiveCopyCount >= 2;
    }

    private static Dictionary<Tile, int> BuildVisibleCounts(IReadOnlyList<Tile> visibleTiles)
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
}
