namespace MahjongClaw.Domain.Rules;

public static class ScoreCalculator
{
    public const int ChineseOfficialMinimumFan = 8;

    public static int CalculateBasicScore(WinningHand hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        return hand.BasicScore;
    }

    public static FanEvaluation EvaluateMinimumFan(WinningHand hand, int minimumFan = ChineseOfficialMinimumFan)
    {
        ArgumentNullException.ThrowIfNull(hand);

        if (minimumFan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFan), "Minimum fan cannot be negative.");
        }

        var score = CalculateBasicScore(hand);
        var shortfall = Math.Max(0, minimumFan - score);

        return new FanEvaluation(score, minimumFan, score >= minimumFan, shortfall);
    }

    public static FanEvaluation EvaluateRulesetMinimumFan(
        WinningHand hand,
        MahjongRuleset ruleset,
        int chineseOfficialMinimumFan = ChineseOfficialMinimumFan)
        => ruleset == MahjongRuleset.Changsha258
            ? EvaluateMinimumFan(hand, minimumFan: 0)
            : EvaluateMinimumFan(hand, chineseOfficialMinimumFan);

    public static bool MeetsMinimumFan(WinningHand hand, int minimumFan = ChineseOfficialMinimumFan)
        => EvaluateMinimumFan(hand, minimumFan).MeetsMinimumFan;

    public static bool MeetsRulesetMinimumFan(
        WinningHand hand,
        MahjongRuleset ruleset,
        int chineseOfficialMinimumFan = ChineseOfficialMinimumFan)
        => EvaluateRulesetMinimumFan(hand, ruleset, chineseOfficialMinimumFan).MeetsMinimumFan;
}

public sealed record FanEvaluation(int BasicScore, int MinimumFan, bool MeetsMinimumFan, int FanShortfall);
