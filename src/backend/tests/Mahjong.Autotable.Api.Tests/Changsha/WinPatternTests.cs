using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-F: Win Pattern detection.
/// </summary>
public class WinPatternTests
{
    private static readonly ChangshaWinDetector Detector = new();

    [Fact, Trait("Category", "Changsha")]
    public void Standard_FourMeldsPlusValidPair_2Pair_Detected()
    {
        // 4 chows + a pair of 2s.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 2), (Suit.Tong, 2));

        var result = Detector.Detect(hand);
        Assert.True(result.IsWin);
        Assert.Equal(WinPattern.Standard, result.Pattern);
        Assert.Equal(ScoreCategory.SmallWin, result.Category);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Standard_Pair3_RejectedBy258Rule()
    {
        // 4 melds + pair of 3 (invalid pair rank for Standard).
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 3), (Suit.Tong, 3));

        var result = Detector.Detect(hand);
        Assert.False(result.IsWin);
    }

    [Fact, Trait("Category", "Changsha")]
    public void SevenPairs_ExemptFrom258_Detected()
    {
        // 7 distinct pairs, none of which are 258.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 3), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 4),
            (Suit.Tong, 6), (Suit.Tong, 6),
            (Suit.Tong, 7), (Suit.Tong, 7),
            (Suit.Tiao, 1), (Suit.Tiao, 1),
            (Suit.Tiao, 9), (Suit.Tiao, 9));

        var result = Detector.Detect(hand);
        Assert.True(result.IsWin);
        Assert.Equal(WinPattern.SevenPairs, result.Pattern);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
    }

    [Fact, Trait("Category", "Changsha")]
    public void AllPungs_FourPungsPlusPair_Detected()
    {
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9),
            (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4),
            (Suit.Tiao, 7), (Suit.Tiao, 7), (Suit.Tiao, 7),
            (Suit.Tiao, 3), (Suit.Tiao, 3));

        var result = Detector.Detect(hand);
        Assert.True(result.IsWin);
        Assert.Equal(WinPattern.AllPungs, result.Pattern);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
        Assert.True(result.IsAllPungs);
    }

    [Fact, Trait("Category", "Changsha")]
    public void FullFlush_AllSingleSuit_Detected()
    {
        // 4 chows + pair of 5, all in Wan.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 2), (Suit.Wan, 3), (Suit.Wan, 4),
            (Suit.Wan, 5), (Suit.Wan, 5),
            (Suit.Wan, 6), (Suit.Wan, 7), (Suit.Wan, 8),
            (Suit.Wan, 7), (Suit.Wan, 8), (Suit.Wan, 9));

        var result = Detector.Detect(hand);
        Assert.True(result.IsWin);
        Assert.True(result.IsFullFlush);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
    }

    [Fact, Trait("Category", "Changsha")]
    public void NonWin_RandomHand_NotDetected()
    {
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 4), (Suit.Wan, 7),
            (Suit.Tong, 2), (Suit.Tong, 5), (Suit.Tong, 8),
            (Suit.Tiao, 3), (Suit.Tiao, 6), (Suit.Tiao, 9),
            (Suit.Wan, 2), (Suit.Tong, 1), (Suit.Tiao, 1),
            (Suit.Wan, 9), (Suit.Tong, 9));

        var result = Detector.Detect(hand);
        Assert.False(result.IsWin);
    }

    [Fact(Skip = "Deferred to v2: 13-Orphans (十三幺) — single big-win pattern overlap"), Trait("Category", "Changsha")]
    public void ThirteenOrphans_DeferredToV2()
    {
    }

    [Fact(Skip = "Deferred to v2: Kong-rob win on exposed kong"), Trait("Category", "Changsha")]
    public void RobbingKong_Win_DeferredToV2()
    {
    }

    [Fact(Skip = "Deferred to v2: pattern stacking score multipliers"), Trait("Category", "Changsha")]
    public void StackedBigWinPatterns_DeferredToV2()
    {
    }
}
