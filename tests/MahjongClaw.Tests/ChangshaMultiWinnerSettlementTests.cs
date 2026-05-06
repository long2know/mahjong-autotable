using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ChangshaMultiWinnerSettlementTests
{
    private static readonly Guid Player1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Player2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Player3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Player4 = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly IReadOnlyList<ChangshaSettlementSeat> StandardSeats = new[]
    {
        new ChangshaSettlementSeat(Player1, Wind.East, 0),
        new ChangshaSettlementSeat(Player2, Wind.South, 0),
        new ChangshaSettlementSeat(Player3, Wind.West, 0),
        new ChangshaSettlementSeat(Player4, Wind.North, 0)
    };

    [Fact]
    public void CalculateMultiWinner_TwoWinnersSmallWin_DiscarderPaysEach()
    {
        // Player1 discards, Player2 and Player3 both win (small win, no bird)
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player1 (discarder + dealer) pays 20 to each winner (10 base + 10 dealer) = -40 total
        // Player2 wins +20
        // Player3 wins +20
        Assert.Equal(-40, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(0, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_TwoWinnersBigWin_DiscarderPaysEach()
    {
        // Player2 discards, Player3 and Player4 both win (big win)
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: true),
            new ChangshaWinnerInfo(Player4, Array.Empty<ChangshaWinType>(), IsBigWin: true)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player2);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player2 (discarder, not dealer) pays 60 to each winner = -120 total
        Assert.Equal(0, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(-120, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(60, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(60, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_ThreeWinners_DiscarderPaysAll()
    {
        // Player1 (dealer) discards, Player2, Player3, Player4 all win (small win)
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player4, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player1 pays 20 to each of 3 winners = -60 total
        Assert.Equal(-60, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_MixedBigWinSmallWin_IndependentCalculation()
    {
        // Player1 discards, Player2 wins big, Player3 wins small
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: true),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player1 (discarder + dealer) pays:
        // - 70 to Player2 (big win, winner not dealer, discarder is dealer)
        // - 20 to Player3 (small win, discarder is dealer)
        // Total: -90
        Assert.Equal(-90, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(70, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(0, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_WithLuxurySevenPairs_MultiplierAppliesPerWinner()
    {
        // Player2 (dealer) discards, Player3 wins with LuxurySevenPairs, Player4 wins standard
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player3, new[] { ChangshaWinType.LuxurySevenPairs }, IsBigWin: true),
            new ChangshaWinnerInfo(Player4, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player2);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player2 (discarder, not dealer) pays:
        // - 60 to Player3 (big win, but discarder not dealer) 
        // - NOTE: LuxurySevenPairs ×2 only applies to dealer loser's payment
        // - Since Player2 is not the dealer, no LuxurySevenPairs multiplier
        // - 10 to Player4 (small win, discarder not dealer)
        // Total: -70
        Assert.Equal(-70, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(60, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(10, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_WithBirdMatch_EachWinnerGetsDoubling()
    {
        // Player1 (dealer) discards, Player2 and Player3 win, bird rank 1 (lands on Player1/East)
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1,
            BirdRank: 1);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Bird rank 1 from East dealer = East (Player1, discarder)
        // Player1 pays 20 base to each winner + 20 bird doubling to each = 40 per winner
        // Total: -80
        Assert.Equal(-80, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(40, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(40, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
    }

    [Fact]
    public void CalculateMultiWinner_TwoBirdsMultiWinner_DoublingStacks()
    {
        // Player1 (dealer) discards, Player2 and Player3 win, two birds (ranks 1,2 = East,South)
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1,
            BirdRanks: new[] { 1, 2 });

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Bird rank 1 = East (Player1), Bird rank 2 = South (Player2)
        // Each winner's settlement is calculated independently with ALL birds:
        // - Winner Player2: 20 base, bird on Player2 (winner) doubles = 40, bird on Player1 (discarder) adds another 20 = 60 from Player1
        // - Winner Player3: 20 base, bird on Player1 (discarder) doubles = 40 from Player1
        // Player1 total: -60 + -40 = -100
        Assert.Equal(-100, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(60, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
        Assert.Equal(40, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
    }

    [Fact]
    public void SelectNextDealer_SingleWinner_ReturnsWinner()
    {
        var rng = new Random(42);
        var nextDealer = ChangshaSettlementCalculator.SelectNextDealer(
            new[] { Player2 },
            Player1,
            isSelfDraw: false,
            rng);

        Assert.Equal(Player2, nextDealer);
    }

    [Fact]
    public void SelectNextDealer_TwoWinnersOnDiscard_ReturnsRandomWinner()
    {
        var rng = new Random(42);
        var winnerIds = new[] { Player2, Player3 };
        
        var nextDealer = ChangshaSettlementCalculator.SelectNextDealer(
            winnerIds,
            Player1,
            isSelfDraw: false,
            rng);

        // Should be one of the winners
        Assert.Contains(nextDealer, winnerIds);
    }

    [Fact]
    public void SelectNextDealer_ThreeWinners_ReturnsRandomWinner()
    {
        var rng = new Random(123);
        var winnerIds = new[] { Player2, Player3, Player4 };
        
        var nextDealer = ChangshaSettlementCalculator.SelectNextDealer(
            winnerIds,
            Player1,
            isSelfDraw: false,
            rng);

        Assert.Contains(nextDealer, winnerIds);
    }

    [Fact]
    public void SelectNextDealer_DeterministicWithSeed_SameResultForSameSeed()
    {
        var winnerIds = new[] { Player2, Player3, Player4 };
        
        var rng1 = new Random(999);
        var dealer1 = ChangshaSettlementCalculator.SelectNextDealer(
            winnerIds,
            Player1,
            isSelfDraw: false,
            rng1);

        var rng2 = new Random(999);
        var dealer2 = ChangshaSettlementCalculator.SelectNextDealer(
            winnerIds,
            Player1,
            isSelfDraw: false,
            rng2);

        Assert.Equal(dealer1, dealer2);
    }

    [Fact]
    public void SelectNextDealer_DifferentSeeds_CanProduceDifferentResults()
    {
        var winnerIds = new[] { Player2, Player3, Player4 };
        var results = new HashSet<Guid>();

        // Run with 10 different seeds - should get at least 2 different results
        for (var seed = 0; seed < 10; seed++)
        {
            var rng = new Random(seed);
            var dealer = ChangshaSettlementCalculator.SelectNextDealer(
                winnerIds,
                Player1,
                isSelfDraw: false,
                rng);
            results.Add(dealer);
        }

        Assert.True(results.Count >= 2, "Random selection should produce different results with different seeds");
    }

    [Fact]
    public void CalculateMultiWinner_RequiresAtLeastOneWinner()
    {
        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            Array.Empty<ChangshaWinnerInfo>(),
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        
        Assert.Throws<ArgumentException>(() =>
            ChangshaSettlementCalculator.CalculateMultiWinner(request, rng));
    }

    [Fact]
    public void CalculateMultiWinner_DiscarderCannotBeWinner()
    {
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player1, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        
        Assert.Throws<ArgumentException>(() =>
            ChangshaSettlementCalculator.CalculateMultiWinner(request, rng));
    }

    [Fact]
    public void CalculateMultiWinner_RequiresDiscarderForDiscardWin()
    {
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: null);

        var rng = new Random(42);
        
        Assert.Throws<ArgumentException>(() =>
            ChangshaSettlementCalculator.CalculateMultiWinner(request, rng));
    }

    [Fact]
    public void CalculateMultiWinner_ScoreAggregationIsAdditive()
    {
        // Verify that multiple winners' deltas are properly aggregated
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player3, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player4, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player1);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Sum of all deltas should be zero (conservation of points)
        var totalDelta = result.ScoreDeltas.Sum(d => d.Delta);
        Assert.Equal(0, totalDelta);
    }

    [Fact]
    public void CalculateMultiWinner_DealerWinsWithLuxurySevenPairs_NoSelfPaymentMultiplier()
    {
        // Edge case: dealer is one of the winners with LuxurySevenPairs
        // The ×2 multiplier for LuxurySevenPairs only applies to dealer's payment when dealer is a LOSER
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player1, new[] { ChangshaWinType.LuxurySevenPairs }, IsBigWin: true),
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player3);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player3 (discarder) pays:
        // - 70 to Player1 (big win, winner is dealer = 60 + 10 dealer bonus)
        // - 10 to Player2 (small win, winner not dealer, discarder not dealer)
        // Total: -80
        Assert.Equal(-80, result.ScoreDeltas.Single(d => d.PlayerId == Player3).Delta);
        Assert.Equal(70, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(10, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
    }

    [Fact]
    public void SelectNextDealer_EmptyWinnerList_ThrowsException()
    {
        var rng = new Random(42);
        
        Assert.Throws<ArgumentException>(() =>
            ChangshaSettlementCalculator.SelectNextDealer(
                Array.Empty<Guid>(),
                Player1,
                isSelfDraw: false,
                rng));
    }

    [Fact]
    public void CalculateMultiWinner_TwoWinnersOneDealer_DealerWinnerGetsBonus()
    {
        // Player1 (dealer) discards, Player1 is not a winner
        // Player2 (winner, not dealer) and Player1 cannot both win (discarder can't win)
        // So test: Player4 discards, Player1 (dealer) and Player2 both win
        var winners = new[]
        {
            new ChangshaWinnerInfo(Player1, Array.Empty<ChangshaWinType>(), IsBigWin: false),
            new ChangshaWinnerInfo(Player2, Array.Empty<ChangshaWinType>(), IsBigWin: false)
        };

        var request = new ChangshaSettlementRequestMulti(
            StandardSeats,
            winners,
            DealerId: Player1,
            IsSelfDraw: false,
            DiscarderId: Player4);

        var rng = new Random(42);
        var result = ChangshaSettlementCalculator.CalculateMultiWinner(request, rng);

        // Player4 (discarder, not dealer) pays:
        // - 20 to Player1 (small win, winner is dealer, discarder not dealer = 10 base + 10 winner dealer)
        // - 10 to Player2 (small win, winner not dealer, discarder not dealer)
        // Total: -30
        Assert.Equal(-30, result.ScoreDeltas.Single(d => d.PlayerId == Player4).Delta);
        Assert.Equal(20, result.ScoreDeltas.Single(d => d.PlayerId == Player1).Delta);
        Assert.Equal(10, result.ScoreDeltas.Single(d => d.PlayerId == Player2).Delta);
    }
}
