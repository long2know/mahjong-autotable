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

        if (!RulesetHandAnalyzer.IsWinningHand(candidateTiles, ruleset))
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
            
            if (RulesetHandAnalyzer.IsWinningHand(candidateTiles, ruleset))
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
            var suits = new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos };
            var tiles = new List<Tile>();
            
            foreach (var suit in suits)
            {
                for (int rank = 1; rank <= 9; rank++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
            
            return tiles;
        }

        throw new NotSupportedException($"Ruleset {ruleset} not supported for wait set computation.");
    }
}
