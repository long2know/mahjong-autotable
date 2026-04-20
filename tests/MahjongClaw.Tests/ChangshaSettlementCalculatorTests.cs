using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ChangshaSettlementCalculatorTests
{
    private static readonly Guid EastId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SouthId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NorthId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Calculate_AppliesSmallSelfDraw_WhenWinnerIsNotDealer()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false));

        DeltaFor(result, EastId).Should().Be(-20);
        DeltaFor(result, SouthId).Should().Be(+40);
        DeltaFor(result, WestId).Should().Be(-10);
        DeltaFor(result, NorthId).Should().Be(-10);
        result.NextDealerId.Should().Be(SouthId);
        result.NextRoundIncrement.Should().Be(1);
    }

    [Fact]
    public void Calculate_AppliesBigDiscard_WithDealerWinnerAdjustment()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: EastId,
            DealerId: EastId,
            IsSelfDraw: false,
            IsBigWin: true,
            DiscarderId: SouthId));

        DeltaFor(result, EastId).Should().Be(+70);
        DeltaFor(result, SouthId).Should().Be(-70);
        DeltaFor(result, WestId).Should().Be(0);
        DeltaFor(result, NorthId).Should().Be(0);
    }

    [Fact]
    public void Calculate_DoublesAllLosers_WhenBirdHitsWinnerOnSelfDraw()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRank: 2));

        DeltaFor(result, EastId).Should().Be(-40);
        DeltaFor(result, SouthId).Should().Be(+80);
        DeltaFor(result, WestId).Should().Be(-20);
        DeltaFor(result, NorthId).Should().Be(-20);
        result.BirdSeat.Should().Be(Wind.South);
    }

    [Fact]
    public void Calculate_DoublesOnlyBirdPlayer_WhenBirdHitsLoserOnSelfDraw()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRank: 3));

        DeltaFor(result, EastId).Should().Be(-20);
        DeltaFor(result, SouthId).Should().Be(+50);
        DeltaFor(result, WestId).Should().Be(-20);
        DeltaFor(result, NorthId).Should().Be(-10);
        result.BirdSeat.Should().Be(Wind.West);
    }

    [Fact]
    public void Calculate_DoublesDiscarderPayment_WhenBirdHitsWinnerOnDiscardWin()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: false,
            IsBigWin: false,
            DiscarderId: EastId,
            BirdRank: 2));

        DeltaFor(result, EastId).Should().Be(-40);
        DeltaFor(result, SouthId).Should().Be(+40);
        DeltaFor(result, WestId).Should().Be(0);
        DeltaFor(result, NorthId).Should().Be(0);
        result.BirdSeat.Should().Be(Wind.South);
    }

    [Fact]
    public void Calculate_UsesDiscarderAsNextDealer_WhenMultipleDiscardWinFlagSet()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: false,
            IsBigWin: true,
            DiscarderId: NorthId,
            IsMultipleWin: true));

        result.NextDealerId.Should().Be(NorthId);
    }

    [Fact]
    public void Calculate_TwoBirds_OneMatchesWinner_DoublePayments()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRanks: [2, 5]));

        DeltaFor(result, EastId).Should().Be(-40);
        DeltaFor(result, SouthId).Should().Be(+80);
        DeltaFor(result, WestId).Should().Be(-20);
        DeltaFor(result, NorthId).Should().Be(-20);
    }

    [Fact]
    public void Calculate_TwoBirds_BothMatchWinner_TriplePayments()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRanks: [2, 6]));

        DeltaFor(result, EastId).Should().Be(-60);
        DeltaFor(result, SouthId).Should().Be(+120);
        DeltaFor(result, WestId).Should().Be(-30);
        DeltaFor(result, NorthId).Should().Be(-30);
    }

    [Fact]
    public void Calculate_TwoBirds_BothHitSameLoser_ApplyExtraPaymentPerBird()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRanks: [3, 7]));

        DeltaFor(result, EastId).Should().Be(-20);
        DeltaFor(result, SouthId).Should().Be(+60);
        DeltaFor(result, WestId).Should().Be(-30);
        DeltaFor(result, NorthId).Should().Be(-10);
    }

    [Fact]
    public void Calculate_LegacySingleBirdRank_StillWorks()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: false,
            BirdRank: 2));

        DeltaFor(result, EastId).Should().Be(-40);
        DeltaFor(result, SouthId).Should().Be(+80);
        DeltaFor(result, WestId).Should().Be(-20);
        DeltaFor(result, NorthId).Should().Be(-20);
        result.BirdSeat.Should().Be(Wind.South);
    }

    [Fact]
    public void Calculate_LuxurySevenPairs_DoublesAllLosersPayment()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: true,
            WinTypes: [ChangshaWinType.LuxurySevenPairs]));

        DeltaFor(result, EastId).Should().Be(-140);
        DeltaFor(result, SouthId).Should().Be(+260);
        DeltaFor(result, WestId).Should().Be(-60);
        DeltaFor(result, NorthId).Should().Be(-60);
    }

    [Fact]
    public void Calculate_LuxurySevenPairs_WithBird_QuadruplesMatchedPayment()
    {
        var result = ChangshaSettlementCalculator.Calculate(new ChangshaSettlementRequest(
            BuildSeats(),
            WinnerId: SouthId,
            DealerId: EastId,
            IsSelfDraw: true,
            IsBigWin: true,
            BirdRank: 2,
            WinTypes: [ChangshaWinType.LuxurySevenPairs]));

        DeltaFor(result, EastId).Should().Be(-280);
        DeltaFor(result, SouthId).Should().Be(+520);
        DeltaFor(result, WestId).Should().Be(-120);
        DeltaFor(result, NorthId).Should().Be(-120);
    }

    private static IReadOnlyList<ChangshaSettlementSeat> BuildSeats()
        =>
        [
            new ChangshaSettlementSeat(EastId, Wind.East, 0),
            new ChangshaSettlementSeat(SouthId, Wind.South, 0),
            new ChangshaSettlementSeat(WestId, Wind.West, 0),
            new ChangshaSettlementSeat(NorthId, Wind.North, 0)
        ];

    private static int DeltaFor(ChangshaSettlementResult result, Guid playerId)
        => result.ScoreDeltas.Single(delta => delta.PlayerId == playerId).Delta;
}
