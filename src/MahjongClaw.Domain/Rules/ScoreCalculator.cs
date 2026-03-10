namespace MahjongClaw.Domain.Rules;

public static class ScoreCalculator
{
    public const int ChineseOfficialMinimumFan = 8;

    public static int CalculateBasicScore(WinningHand hand)
        => hand.BasicScore;

    public static FanEvaluation EvaluateMinimumFan(WinningHand hand, int minimumFan = ChineseOfficialMinimumFan)
    {
        if (minimumFan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFan), "Minimum fan cannot be negative.");
        }

        var score = CalculateBasicScore(hand);
        var shortfall = Math.Max(0, minimumFan - score);

        return new FanEvaluation(score, minimumFan, score >= minimumFan, shortfall);
    }

    public static bool MeetsMinimumFan(WinningHand hand, int minimumFan = ChineseOfficialMinimumFan)
        => EvaluateMinimumFan(hand, minimumFan).MeetsMinimumFan;
}

public sealed record FanEvaluation(int BasicScore, int MinimumFan, bool MeetsMinimumFan, int FanShortfall);
