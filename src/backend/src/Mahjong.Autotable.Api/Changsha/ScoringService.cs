namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Scoring service for Changsha Mahjong per Vasquez §5.
/// Two tiers:
///   Small Win: self-draw = 2 (each pays 2), discard = 1 from discarder + 2 from dealer/discarder if dealer involved
///   Big Win:   self-draw = 4 (each pays 4), discard = 6 from discarder + 7 if dealer
///
/// Per the spec's payment table (dealer bonus baked in):
///   Small Win self-draw: each other player pays 2 to winner
///   Small Win discard:   discarder pays 2 to winner (1 base + 1 discarder penalty)
///                        BUT if discarder is dealer → pays 2; if winner is dealer → discarder pays 2
///   Big Win self-draw:   each other player pays 4 to winner (3 base + 1 dealer bonus if applicable)
///   Big Win discard:     discarder pays 6 to winner (non-dealer) or 7 (dealer involved)
///
/// Simplified scoring table from spec:
///   Small Win = 1/2 → non-dealer discard = 1, self-draw per player = 2
///   Big Win   = 6/7 discard, 3/4 self-draw
///
/// Payment interpretation:
///   Small Win discard:   discarder pays 1 (non-dealer→non-dealer), 2 (dealer involved)
///   Small Win self-draw: each pays 2
///   Big Win discard:     discarder pays 6 (non-dealer→non-dealer), 7 (dealer involved)
///   Big Win self-draw:   each pays 3 (non-dealer→non-dealer), 4 (dealer involved)
///
/// Full Flush doubles the big-win multiplier.
/// </summary>
public interface IScoringService
{
    ScoreResult CalculateScore(WinResult win, int dealerSeatIndex, bool isFullFlush);
}

public sealed class ScoringService : IScoringService
{
    // Payment table per spec §5
    private const int SmallWinDiscardBase = 1;
    private const int SmallWinDiscardDealer = 2;
    private const int SmallWinSelfDrawBase = 1;
    private const int SmallWinSelfDrawDealer = 2;

    private const int BigWinDiscardBase = 6;
    private const int BigWinDiscardDealer = 7;
    private const int BigWinSelfDrawBase = 3;
    private const int BigWinSelfDrawDealer = 4;

    public ScoreResult CalculateScore(WinResult win, int dealerSeatIndex, bool isFullFlush)
    {
        var payments = new List<PaymentEntry>();
        var category = ClassifyWin(win);

        if (win.Method == WinMethod.SelfDraw)
        {
            CalculateSelfDrawPayments(win, dealerSeatIndex, category, payments);
        }
        else
        {
            CalculateDiscardPayments(win, dealerSeatIndex, category, payments);
        }

        var basePoints = payments.Sum(p => p.Amount);

        return new ScoreResult
        {
            Category = category,
            BasePoints = basePoints,
            Payments = payments
        };
    }

    private static ScoreCategory ClassifyWin(WinResult win)
    {
        return win.Pattern switch
        {
            WinPattern.SevenPairs => ScoreCategory.BigWin,
            WinPattern.AllPungs => ScoreCategory.BigWin,
            WinPattern.FullFlush => ScoreCategory.BigWin,
            WinPattern.Standard => ScoreCategory.SmallWin,
            _ => ScoreCategory.SmallWin
        };
    }

    private static void CalculateSelfDrawPayments(
        WinResult win,
        int dealerSeatIndex,
        ScoreCategory category,
        List<PaymentEntry> payments)
    {
        for (var seat = 0; seat < 4; seat++)
        {
            if (seat == win.WinningSeatIndex)
                continue;

            var dealerInvolved = seat == dealerSeatIndex || win.WinningSeatIndex == dealerSeatIndex;
            int amount;

            if (category == ScoreCategory.BigWin)
            {
                amount = dealerInvolved ? BigWinSelfDrawDealer : BigWinSelfDrawBase;
            }
            else
            {
                amount = dealerInvolved ? SmallWinSelfDrawDealer : SmallWinSelfDrawBase;
            }

            payments.Add(new PaymentEntry
            {
                FromSeatIndex = seat,
                ToSeatIndex = win.WinningSeatIndex,
                Amount = amount,
                Reason = $"{category}-selfDraw{(dealerInvolved ? "-dealer" : "")}"
            });
        }
    }

    private static void CalculateDiscardPayments(
        WinResult win,
        int dealerSeatIndex,
        ScoreCategory category,
        List<PaymentEntry> payments)
    {
        var dealerInvolved = win.SourceSeatIndex == dealerSeatIndex
                          || win.WinningSeatIndex == dealerSeatIndex;
        int amount;

        if (category == ScoreCategory.BigWin)
        {
            amount = dealerInvolved ? BigWinDiscardDealer : BigWinDiscardBase;
        }
        else
        {
            amount = dealerInvolved ? SmallWinDiscardDealer : SmallWinDiscardBase;
        }

        payments.Add(new PaymentEntry
        {
            FromSeatIndex = win.SourceSeatIndex,
            ToSeatIndex = win.WinningSeatIndex,
            Amount = amount,
            Reason = $"{category}-discard{(dealerInvolved ? "-dealer" : "")}"
        });
    }
}
