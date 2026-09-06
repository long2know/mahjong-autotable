using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class MissedWinLockTracker
{
    public static MissedWinLock? CreateLock(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        MahjongRuleset ruleset = MahjongRuleset.Changsha258)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);
        ArgumentNullException.ThrowIfNull(discardedTile);

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var declaredKongCount = meldTiles
            .GroupBy(static tile => tile)
            .Count(group => group.Count() == 4);

        if (!TurnFlowValidator.CanDraw(handTiles.Count, meldTiles.Count, declaredKongCount))
        {
            return null;
        }

        var candidateTiles = handTiles
            .Concat(meldTiles)
            .Append(discardedTile)
            .ToList();

        if (!IsWinningWithMinimumFan(candidateTiles, ruleset))
        {
            return null;
        }

        var waitSet = ComputeWaitSet(handTiles, meldTiles, ruleset);
        return waitSet.Count > 0 ? new MissedWinLock(waitSet) : null;
    }

    public static IReadOnlySet<Tile> ComputeWaitSet(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset = MahjongRuleset.Changsha258)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var waitTiles = new HashSet<Tile>();
        var declaredKongCount = meldTiles
            .GroupBy(static tile => tile)
            .Count(group => group.Count() == 4);

        if (!TurnFlowValidator.CanDraw(handTiles.Count, meldTiles.Count, declaredKongCount))
        {
            return waitTiles;
        }

        var baseTiles = handTiles.Concat(meldTiles).ToList();

        var allPossibleTiles = GenerateAllPossibleTiles(ruleset);
        
        foreach (var testTile in allPossibleTiles)
        {
            var candidateTiles = baseTiles.Append(testTile).ToList();
            
            if (IsWinningWithMinimumFan(candidateTiles, ruleset))
            {
                waitTiles.Add(testTile);
            }
        }

        return waitTiles;
    }

    private static IEnumerable<Tile> GenerateAllPossibleTiles(MahjongRuleset ruleset)
    {
        if (ruleset == MahjongRuleset.Changsha258)
        {
            return BuildSuitedTiles(new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos });
        }

        if (ruleset == MahjongRuleset.ChineseOfficial)
        {
            var suitedTiles = BuildSuitedTiles(new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos });
            var windTiles = Enum
                .GetValues<Wind>()
                .Select(wind => new Tile(TileSuit.Winds, 0, wind));
            var dragonTiles = Enum
                .GetValues<Dragon>()
                .Select(dragon => new Tile(TileSuit.Dragons, 0, null, dragon));

            return suitedTiles
                .Concat(windTiles)
                .Concat(dragonTiles);
        }

        throw new NotSupportedException($"Ruleset {ruleset} not supported for wait set computation.");
    }

    private static bool IsWinningWithMinimumFan(IReadOnlyList<Tile> tiles, MahjongRuleset ruleset)
    {
        if (!RulesetHandAnalyzer.TryGetWinningHand(tiles, ruleset, out var winningHand)
            || winningHand is null)
        {
            return false;
        }

        return ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset);
    }

    private static IEnumerable<Tile> BuildSuitedTiles(IEnumerable<TileSuit> suits)
    {
        var tiles = new List<Tile>();
        foreach (var suit in suits)
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Add(new Tile(suit, rank));
            }
        }

        return tiles;
    }
}
