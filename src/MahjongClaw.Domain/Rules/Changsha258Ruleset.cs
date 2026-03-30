using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class Changsha258Ruleset
{
    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        if (!HandAnalyzer.TryGetWinningHand(tiles, out var candidate))
        {
            hand = null;
            return false;
        }

        if (candidate is null)
        {
            hand = null;
            return false;
        }

        if (tiles.Any(tile => tile.IsHonor || tile.IsFlower))
        {
            hand = null;
            return false;
        }

        if (candidate.Pattern is WinningPattern.SevenPairs or WinningPattern.ThirteenOrphans or WinningPattern.NineGates)
        {
            hand = null;
            return false;
        }

        if (!IsSmallWinPair(candidate.Pair))
        {
            hand = null;
            return false;
        }

        hand = candidate with { Pattern = WinningPattern.Standard };
        return true;
    }

    public static bool IsSmallWinPair(Tile pair)
        => !pair.IsHonor && !pair.IsFlower && pair.Rank is 2 or 5 or 8;
}
