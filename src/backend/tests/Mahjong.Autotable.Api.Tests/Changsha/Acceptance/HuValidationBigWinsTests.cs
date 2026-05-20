using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.Changsha.Acceptance;

/// <summary>
/// Acceptance: Big-Win patterns relax the 258 pair rule per Vasquez §1.11 +
/// MahjongPros §"Big Wins" + Reddit §"big hands". Each of Seven Pairs (七对), All Pungs
/// (碰碰胡), and Full Flush (清一色) is a single-tier Big Win in v1 — any pair allowed,
/// scoring uses the BigWin payment table.
/// </summary>
public class HuValidationBigWinsTests
{
    private static readonly ChangshaWinDetector Detector = new();

    [Theory, Trait("Category", "Acceptance")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(9)]
    public void Hu_SevenPairs_NotSubjectTo258(int pairRankNon258)
    {
        // MahjongPros §"Seven Pairs": any 7 distinct pairs — no 258 requirement.
        // We pick 7 distinct pair ranks; one of them is a non-258 rank to prove the exemption.
        var ranks = new[] { 1, 3, 4, 6, 7, 9, pairRankNon258 }.Distinct().Take(7).ToList();
        // Pad to 7 distinct ranks if a duplicate snuck in.
        var fallback = new[] { 1, 3, 4, 6, 7, 9, 2 };
        for (var i = 0; ranks.Count < 7 && i < fallback.Length; i++)
            if (!ranks.Contains(fallback[i])) ranks.Add(fallback[i]);

        var tiles = new List<(Suit, int)>();
        var suits = new[] { Suit.Wan, Suit.Tong, Suit.Tiao };
        for (var i = 0; i < 7; i++)
        {
            var s = suits[i % 3];
            tiles.Add((s, ranks[i]));
            tiles.Add((s, ranks[i]));
        }

        var hand = HandOf(0, tiles.ToArray());
        var result = Detector.Detect(hand);

        Assert.True(result.IsWin,
            $"Seven Pairs (any-pair) must be a valid Hu — ranks used: [{string.Join(",", ranks)}]");
        Assert.Equal(WinPattern.SevenPairs, result.Pattern);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Hu_AllPungs_NotSubjectTo258()
    {
        // MahjongPros §"All Pungs": 4 pungs/kongs + any pair. Vasquez §1.11 confirms 258 exemption.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9),
            (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4),
            (Suit.Tiao, 7), (Suit.Tiao, 7), (Suit.Tiao, 7),
            (Suit.Tiao, 3), (Suit.Tiao, 3));  // non-258 pair (rank 3)

        var result = Detector.Detect(hand);

        Assert.True(result.IsWin);
        Assert.Equal(WinPattern.AllPungs, result.Pattern);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Hu_FullFlush_NotSubjectTo258()
    {
        // MahjongPros §"Full Flush" (清一色): every tile single suit. Pair rank unconstrained.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 2), (Suit.Wan, 3), (Suit.Wan, 4),
            (Suit.Wan, 4), (Suit.Wan, 4),                          // non-258 pair
            (Suit.Wan, 6), (Suit.Wan, 7), (Suit.Wan, 8),
            (Suit.Wan, 7), (Suit.Wan, 8), (Suit.Wan, 9));

        var result = Detector.Detect(hand);

        Assert.True(result.IsWin);
        Assert.True(result.IsFullFlush);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Hu_FullFlush_OverlapsSevenPairs_PatternStillDetected()
    {
        // Edge case: a single-suit seven-pairs hand is BOTH FullFlush AND SevenPairs. The detector
        // must surface SOME big-win pattern (either is acceptable) and classify as BigWin.
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 2), (Suit.Wan, 2),
            (Suit.Wan, 3), (Suit.Wan, 3),
            (Suit.Wan, 5), (Suit.Wan, 5),
            (Suit.Wan, 6), (Suit.Wan, 6),
            (Suit.Wan, 8), (Suit.Wan, 8),
            (Suit.Wan, 9), (Suit.Wan, 9));

        var result = Detector.Detect(hand);

        Assert.True(result.IsWin);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
        Assert.True(result.IsSevenPairs || result.IsFullFlush,
            $"Single-suit seven-pairs hand must be classified as a Big Win pattern. " +
            $"Got Pattern={result.Pattern}, IsFullFlush={result.IsFullFlush}, IsSevenPairs={result.IsSevenPairs}.");
    }

    [Fact(Skip = "Deferred to Phase E (v2): 13-Orphans (十三幺) Big Win pattern. Out of scope for v1 MVP — Stephen and Vasquez agreed in Phase D-tests review. Implement WinPattern.ThirteenOrphans + Detector.CheckThirteenOrphans() when this becomes a v2 priority."), Trait("Category", "Acceptance")]
    public void Hu_ThirteenOrphans_SpecGap_Skipped()
    {
        // Reddit §"Big hands" lists 13-Orphans as a Big Win in some Changsha variants.
        // V1 detector does not implement it; flagged here for Phase D-backend (or v2).
    }
}
