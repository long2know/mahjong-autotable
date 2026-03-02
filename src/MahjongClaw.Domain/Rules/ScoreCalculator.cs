namespace MahjongClaw.Domain.Rules;

public static class ScoreCalculator
{
    public static int CalculateBasicScore(WinningHand hand)
        => hand.BasicScore;
}
