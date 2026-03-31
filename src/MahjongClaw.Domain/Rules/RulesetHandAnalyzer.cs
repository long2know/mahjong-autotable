using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class RulesetHandAnalyzer
{
    public static bool IsWinningHand(IReadOnlyList<Tile> tiles, MahjongRuleset ruleset)
        => TryGetWinningHand(tiles, ruleset, out _);

    public static bool TryGetWinningHand(IReadOnlyList<Tile> tiles, MahjongRuleset ruleset, out WinningHand? hand)
    {
        hand = null;

        return ruleset switch
        {
            MahjongRuleset.Changsha258 => Changsha258Ruleset.TryGetWinningHand(tiles, out hand),
            _ => TryGetChineseOfficialHand(tiles, out hand)
        };
    }

    private static bool TryGetChineseOfficialHand(IReadOnlyList<Tile> tiles, out WinningHand? hand)
    {
        if (!HandAnalyzer.TryGetWinningHand(tiles, out hand) || hand is null)
        {
            hand = null;
            return false;
        }

        if (!ScoreCalculator.MeetsMinimumFan(hand))
        {
            hand = null;
            return false;
        }

        return true;
    }
}
