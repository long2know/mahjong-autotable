using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotClaimAdvisor
{
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

        meldTilesForClaim = Enumerable.Repeat(discardedTile, 3).ToArray();
        return true;
    }
}
