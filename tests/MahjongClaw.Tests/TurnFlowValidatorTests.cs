using FluentAssertions;
using MahjongClaw.Domain.Gameplay;

namespace MahjongClaw.Tests;

public sealed class TurnFlowValidatorTests
{
    [Theory]
    [InlineData(13, 0)]
    [InlineData(10, 3)]
    [InlineData(1, 12)]
    public void CanDraw_WhenPlayerOwnsThirteenTilesAndHasHandTile_ReturnsTrue(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDraw(handTileCount, meldTileCount).Should().BeTrue();
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(12, 2)]
    [InlineData(0, 13)]
    public void CanDraw_WhenPlayerHandShapeIsInvalid_ReturnsFalse(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDraw(handTileCount, meldTileCount).Should().BeFalse();
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(11, 3)]
    [InlineData(10, 4)]
    public void CanDiscard_WhenPlayerOwnsFourteenTilesAndHasHandTile_ReturnsTrue(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDiscard(handTileCount, meldTileCount).Should().BeTrue();
    }

    [Theory]
    [InlineData(13, 0)]
    [InlineData(12, 1)]
    [InlineData(15, 0)]
    [InlineData(0, 14)]
    public void CanDiscard_WhenPlayerHandShapeIsInvalid_ReturnsFalse(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDiscard(handTileCount, meldTileCount).Should().BeFalse();
    }

    [Theory]
    [InlineData(14, 0, 3)]
    [InlineData(11, 3, 3)]
    [InlineData(10, 4, 4)]
    public void CanDeclareMeld_WhenPlayerHasValidOwnershipShapeAndEnoughHandTiles_ReturnsTrue(
        int handTileCount,
        int meldTileCount,
        int meldTileRequirement)
    {
        TurnFlowValidator.CanDeclareMeld(handTileCount, meldTileCount, meldTileRequirement).Should().BeTrue();
    }

    [Theory]
    [InlineData(13, 0, 3)]
    [InlineData(15, 0, 3)]
    [InlineData(2, 12, 3)]
    [InlineData(3, 11, 4)]
    public void CanDeclareMeld_WhenOwnershipShapeOrHandTilesAreInvalid_ReturnsFalse(
        int handTileCount,
        int meldTileCount,
        int meldTileRequirement)
    {
        TurnFlowValidator.CanDeclareMeld(handTileCount, meldTileCount, meldTileRequirement).Should().BeFalse();
    }
}
