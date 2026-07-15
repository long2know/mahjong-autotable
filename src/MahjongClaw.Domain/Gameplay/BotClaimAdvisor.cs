using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotClaimAdvisor
{
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
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(discardedTile);

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
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

        if (ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(discardedTile))
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
            {
                var hasAlternateSmallWinPair = handTiles
                    .GroupBy(static tile => tile)
                    .Any(group => group.Count() >= 2
                        && !group.Key.Equals(discardedTile)
                        && Changsha258Ruleset.IsSmallWinPair(group.Key));

                if (!hasAlternateSmallWinPair)
                {
                    return false;
                }
            }
        }

        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, meldTiles, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, meldTiles, discardedTile, ruleset);
        if (preClaimOuts > 0 && postClaimOuts == 0)
        {
            return false;
        }

        meldTilesForClaim = Enumerable.Repeat(discardedTile, 3).ToArray();
        return true;
    }

    private static int CountWinningDrawOutsAfterDraw(
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
            var remainingCopies = 4 - drawTileCount;
            if (remainingCopies <= 0)
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

    private static int CountWinningDrawOutsAfterExposedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        MahjongRuleset ruleset)
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

        return CountWinningDrawOutsAfterDraw(handAfterKong, meldAfterKong, ruleset);
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
}
