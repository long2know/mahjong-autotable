using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ChowEligibilityRuleTests
{
    private static readonly Guid EastPlayer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SouthPlayer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WestPlayer = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NorthPlayer = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Dictionary<Guid, Wind> FourPlayerSeats = new()
    {
        { EastPlayer, Wind.East },
        { SouthPlayer, Wind.South },
        { WestPlayer, Wind.West },
        { NorthPlayer, Wind.North }
    };

    [Fact]
    public void IsEligibleForChow_EastDiscardsToSouth_IsEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            SouthPlayer, EastPlayer, FourPlayerSeats);
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForChow_SouthDiscardsToWest_IsEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            WestPlayer, SouthPlayer, FourPlayerSeats);
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForChow_WestDiscardsToNorth_IsEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            NorthPlayer, WestPlayer, FourPlayerSeats);
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForChow_NorthDiscardsToEast_IsEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            EastPlayer, NorthPlayer, FourPlayerSeats);
        Assert.True(result);
    }

    [Fact]
    public void IsEligibleForChow_EastDiscardsToWest_NotEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            WestPlayer, EastPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_EastDiscardsToNorth_NotEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            NorthPlayer, EastPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_SouthDiscardsToEast_NotEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            EastPlayer, SouthPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_SouthDiscardsToNorth_NotEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            NorthPlayer, SouthPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_ClaimerNotInSeats_ReturnsFalse()
    {
        var unknownPlayer = Guid.NewGuid();
        var result = ChowEligibilityRule.IsEligibleForChow(
            unknownPlayer, EastPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_DiscarderNotInSeats_ReturnsFalse()
    {
        var unknownPlayer = Guid.NewGuid();
        var result = ChowEligibilityRule.IsEligibleForChow(
            SouthPlayer, unknownPlayer, FourPlayerSeats);
        Assert.False(result);
    }

    [Fact]
    public void IsEligibleForChow_NullPlayerSeats_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ChowEligibilityRule.IsEligibleForChow(SouthPlayer, EastPlayer, null!));
    }

    [Fact]
    public void IsEligibleForChow_SelfChow_NotEligible()
    {
        var result = ChowEligibilityRule.IsEligibleForChow(
            EastPlayer, EastPlayer, FourPlayerSeats);
        Assert.False(result);
    }
}
