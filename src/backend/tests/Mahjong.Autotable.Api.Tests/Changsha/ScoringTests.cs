using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-G: Scoring Tests
/// Tests P0 scenarios for Small Win and Big Win scoring with dealer bonus.
/// V1 uses simplified 1/6/7 model (G-09) per spec resolution.
/// </summary>
public class ScoringTests
{
    // V1 SCOPE: Simplified scoring model per G-09 (S1 model)
    // Small Win = 1 point, Big Win = 6 points (non-dealer) / 7 points (dealer)

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void SmallWin_NonDealerSelfDraw_DealerPays2_OthersPay1()
    {
        // G-01 + G-09: Non-dealer wins by self-draw
        // Simplified model: Small Win = 1 point base
        // Dealer pays double (2), other non-dealers pay 1 each
        
        // Arrange: Seat 2 (non-dealer) wins by self-draw
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 2,
        //     winType: WinType.SmallWin,
        //     isSelfdraw: true,
        //     dealerSeat: 0
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Dealer pays 2, others pay 1, winner receives 4 total
        // Assert.Equal(-2, scores[0]); // Dealer
        // Assert.Equal(-1, scores[1]); // Non-dealer
        // Assert.Equal(4, scores[2]);  // Winner
        // Assert.Equal(-1, scores[3]); // Non-dealer
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void SmallWin_DealerSelfDraw_EachPlayerPays2()
    {
        // G-02 + G-09: Dealer wins by self-draw
        // Simplified model: Small Win = 1 point, dealer receives/pays double
        // Each non-dealer pays 2
        
        // Arrange: Seat 0 (dealer) wins by self-draw
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 0,
        //     winType: WinType.SmallWin,
        //     isSelfdraw: true,
        //     dealerSeat: 0
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Each non-dealer pays 2, dealer receives 6 total
        // Assert.Equal(6, scores[0]);  // Dealer (winner)
        // Assert.Equal(-2, scores[1]); // Non-dealer
        // Assert.Equal(-2, scores[2]); // Non-dealer
        // Assert.Equal(-2, scores[3]); // Non-dealer
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void SmallWin_NonDealerWinsFromDiscard_DiscarderPays1()
    {
        // G-03 + G-09: Non-dealer wins from discard
        // Discarder pays 1 point (or 2 if discarder is dealer)
        
        // Arrange: Seat 2 (non-dealer) wins from seat 3's discard
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 2,
        //     winType: WinType.SmallWin,
        //     isSelfdraw: false,
        //     dealerSeat: 0,
        //     discarderSeat: 3
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Discarder (non-dealer) pays 1, winner receives 1
        // Assert.Equal(0, scores[0]);  // Dealer (not involved)
        // Assert.Equal(0, scores[1]);  // Not involved
        // Assert.Equal(1, scores[2]);  // Winner
        // Assert.Equal(-1, scores[3]); // Discarder
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void SmallWin_NonDealerWinsFromDealerDiscard_DealerPays2()
    {
        // G-03 + G-09: Non-dealer wins from dealer's discard
        // Dealer pays double (2 points)
        
        // Arrange: Seat 1 (non-dealer) wins from seat 0 (dealer) discard
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 1,
        //     winType: WinType.SmallWin,
        //     isSelfdraw: false,
        //     dealerSeat: 0,
        //     discarderSeat: 0
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Dealer pays 2, winner receives 2
        // Assert.Equal(-2, scores[0]); // Dealer (discarder)
        // Assert.Equal(2, scores[1]);  // Winner
        // Assert.Equal(0, scores[2]);  // Not involved
        // Assert.Equal(0, scores[3]);  // Not involved
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void SmallWin_DealerWinsFromDiscard_DiscarderPays2()
    {
        // G-04 + G-09: Dealer wins from discard
        // Discarder pays 2 (1 base + 1 dealer bonus)
        
        // Arrange: Seat 0 (dealer) wins from seat 2's discard
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 0,
        //     winType: WinType.SmallWin,
        //     isSelfdraw: false,
        //     dealerSeat: 0,
        //     discarderSeat: 2
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Discarder pays 2, dealer receives 2
        // Assert.Equal(2, scores[0]);  // Dealer (winner)
        // Assert.Equal(0, scores[1]);  // Not involved
        // Assert.Equal(-2, scores[2]); // Discarder
        // Assert.Equal(0, scores[3]);  // Not involved
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void BigWin_NonDealerSelfDraw_DealerPays7_OthersPay6()
    {
        // G-05 + G-09: Non-dealer wins Big Win by self-draw
        // Simplified model: Big Win = 6 points (non-dealer), 7 points from dealer
        
        // Arrange: Seat 3 (non-dealer) wins Seven Pairs by self-draw
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 3,
        //     winType: WinType.BigWin,
        //     patterns: new[] { WinPattern.SevenPairs },
        //     isSelfdraw: true,
        //     dealerSeat: 0
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Dealer pays 7, others pay 6, winner receives 19 total
        // Assert.Equal(-7, scores[0]); // Dealer
        // Assert.Equal(-6, scores[1]); // Non-dealer
        // Assert.Equal(-6, scores[2]); // Non-dealer
        // Assert.Equal(19, scores[3]); // Winner
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void BigWin_DealerSelfDraw_EachPlayerPays7()
    {
        // G-06 + G-09: Dealer wins Big Win by self-draw
        // Each non-dealer pays 7 points
        
        // Arrange: Seat 0 (dealer) wins All Pungs by self-draw
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 0,
        //     winType: WinType.BigWin,
        //     patterns: new[] { WinPattern.AllPungs },
        //     isSelfdraw: true,
        //     dealerSeat: 0
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Each non-dealer pays 7, dealer receives 21 total
        // Assert.Equal(21, scores[0]); // Dealer (winner)
        // Assert.Equal(-7, scores[1]); // Non-dealer
        // Assert.Equal(-7, scores[2]); // Non-dealer
        // Assert.Equal(-7, scores[3]); // Non-dealer
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void BigWin_NonDealerWinsFromDiscard_DiscarderPays6()
    {
        // G-07 + G-09: Non-dealer wins Big Win from discard
        // Discarder pays 6 (or 7 if discarder is dealer)
        
        // Arrange: Seat 1 (non-dealer) wins Full Flush from seat 2's discard
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 1,
        //     winType: WinType.BigWin,
        //     patterns: new[] { WinPattern.FullFlush },
        //     isSelfdraw: false,
        //     dealerSeat: 0,
        //     discarderSeat: 2
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Discarder pays 6, winner receives 6
        // Assert.Equal(0, scores[0]);  // Dealer (not involved)
        // Assert.Equal(6, scores[1]);  // Winner
        // Assert.Equal(-6, scores[2]); // Discarder
        // Assert.Equal(0, scores[3]);  // Not involved
    }

    [Fact(Skip = "Awaiting Bishop's IScoringService")]
    [Trait("Category", "Changsha")]
    public void BigWin_DealerWinsFromDiscard_DiscarderPays7()
    {
        // G-08 + G-09: Dealer wins Big Win from discard
        // Discarder pays 7 (6 base + 1 dealer bonus)
        
        // Arrange: Seat 0 (dealer) wins Full Flush from seat 3's discard
        // var scoringService = new ChangshaScoringService();
        // var winResult = new WinResult(
        //     winnerSeat: 0,
        //     winType: WinType.BigWin,
        //     patterns: new[] { WinPattern.FullFlush },
        //     isSelfdraw: false,
        //     dealerSeat: 0,
        //     discarderSeat: 3
        // );
        
        // Act: Calculate scores
        // var scores = scoringService.CalculateScores(winResult);
        
        // Assert: Discarder pays 7, dealer receives 7
        // Assert.Equal(7, scores[0]);  // Dealer (winner)
        // Assert.Equal(0, scores[1]);  // Not involved
        // Assert.Equal(0, scores[2]);  // Not involved
        // Assert.Equal(-7, scores[3]); // Discarder
    }
}
