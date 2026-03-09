namespace MahjongClaw.Domain.Rules;

public static class ScoreCalculator
{
    public static int CalculateBasicScore(WinningHand hand)
        => hand.BasicScore;

    public static bool MeetsMinimumFan(WinningHand hand, int minimumFan = 8)
    {
        if (minimumFan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFan), "Minimum fan cannot be negative.");
        }

        return CalculateBasicScore(hand) >= minimumFan;
    }
}
