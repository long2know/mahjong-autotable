using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotClaimAdvisor
{
    private const int ExposedKongOutsDropTolerance = 0;
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);
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

        if (ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(discardedTile))
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
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
        if (preClaimOuts > 0 && postClaimOuts == 0)
        {
            return false;
        }

        if (ruleset == MahjongRuleset.ChineseOfficial && preClaimOuts == 0)
        {
            var preClaimPotentialOuts = CountWinningDrawOutsAfterDraw(
                handTiles,
                meldTiles,
                visibleCounts,
                ruleset,
                requireMinimumFan: false);

            if (preClaimPotentialOuts > 0)
            {
                var postClaimPotentialOuts = CountWinningDrawOutsAfterExposedKong(
                    handTiles,
                    meldTiles,
                    discardedTile,
                    visibleCounts,
                    ruleset,
                    requireMinimumFan: false);

                if (postClaimPotentialOuts + ExposedKongOutsDropTolerance < preClaimPotentialOuts)
                {
                    return false;
                }
            }
        }

        var allowChangshaFlexibleEye = ruleset == MahjongRuleset.Changsha258
            && Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);

        if (!allowChangshaFlexibleEye
            && preClaimOuts > 0
            && postClaimOuts + ExposedKongOutsDropTolerance < preClaimOuts)
        {
            return false;
        }

        meldTilesForClaim = Enumerable.Repeat(discardedTile, 3).ToArray();
        return true;
    }

    private static int CountWinningDrawOutsAfterDraw(
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

        }

        return winningOuts;
    }

    private static int CountWinningDrawOutsAfterExposedKong(
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
                return 0;
            }

            handAfterKong.RemoveAt(index);
        }

        var meldAfterKong = meldTiles
            .Concat(Enumerable.Repeat(discardedTile, 4))
            .ToList();

        return CountWinningDrawOutsAfterDraw(handAfterKong, meldAfterKong, visibleCounts, ruleset, requireMinimumFan);
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
            }
        }

        return false;
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
}
