using Mahjong.Autotable.Api.Changsha;

namespace Mahjong.Autotable.Api.Tests.ChangshaServices;

public class ScoringServiceTests
{
    private readonly ScoringService _svc = new();

    [Fact]
    public void SmallWin_SelfDraw_NonDealer_EachPays2()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 1,
            Method = WinMethod.SelfDraw,
            Pattern = WinPattern.Standard,
            WinningTileId = 0,
            SourceSeatIndex = 1
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Equal(ScoreCategory.SmallWin, result.Category);
        Assert.Equal(3, result.Payments.Count);

        // Each of 3 other players pays 2
        Assert.All(result.Payments, p =>
        {
            Assert.Equal(1, p.ToSeatIndex);
            Assert.Equal(2, p.Amount);
        });
    }

    [Fact]
    public void SmallWin_Discard_NonDealerToNonDealer_Pays1()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 1,
            Method = WinMethod.Discard,
            Pattern = WinPattern.Standard,
            WinningTileId = 0,
            SourceSeatIndex = 2 // non-dealer discards to non-dealer winner
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Single(result.Payments);
        Assert.Equal(1, result.Payments[0].Amount);
    }

    [Fact]
    public void SmallWin_Discard_DealerInvolved_Pays2()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 0, // dealer wins
            Method = WinMethod.Discard,
            Pattern = WinPattern.Standard,
            WinningTileId = 0,
            SourceSeatIndex = 1
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Single(result.Payments);
        Assert.Equal(2, result.Payments[0].Amount);
    }

    [Fact]
    public void BigWin_SelfDraw_NonDealer_Pays3Or4()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 1,
            Method = WinMethod.SelfDraw,
            Pattern = WinPattern.AllPungs,
            WinningTileId = 0,
            SourceSeatIndex = 1
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Equal(ScoreCategory.BigWin, result.Category);
        Assert.Equal(3, result.Payments.Count);

        // Dealer pays 4 (dealer involved), others pay 3
        var dealerPayment = result.Payments.Single(p => p.FromSeatIndex == 0);
        Assert.Equal(4, dealerPayment.Amount);

        var otherPayments = result.Payments.Where(p => p.FromSeatIndex != 0).ToList();
        Assert.All(otherPayments, p => Assert.Equal(3, p.Amount));
    }

    [Fact]
    public void BigWin_Discard_NonDealer_Pays6()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 1,
            Method = WinMethod.Discard,
            Pattern = WinPattern.SevenPairs,
            WinningTileId = 0,
            SourceSeatIndex = 2 // non-dealer to non-dealer
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Single(result.Payments);
        Assert.Equal(6, result.Payments[0].Amount);
    }

    [Fact]
    public void BigWin_Discard_DealerInvolved_Pays7()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 0, // dealer wins from discard
            Method = WinMethod.Discard,
            Pattern = WinPattern.AllPungs,
            WinningTileId = 0,
            SourceSeatIndex = 1
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: false);
        Assert.Single(result.Payments);
        Assert.Equal(7, result.Payments[0].Amount);
    }

    [Fact]
    public void FullFlush_Doubles_BigWinPayment()
    {
        var win = new WinResult
        {
            WinningSeatIndex = 1,
            Method = WinMethod.Discard,
            Pattern = WinPattern.FullFlush,
            WinningTileId = 0,
            SourceSeatIndex = 2
        };

        var result = _svc.CalculateScore(win, dealerSeatIndex: 0, isFullFlush: true);
        Assert.Single(result.Payments);
        Assert.Equal(12, result.Payments[0].Amount); // 6 * 2
    }
}
