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

    [Fact]
    public void CanDraw_WhenPlayerHasDeclaredKongAndValidOwnedShape_ReturnsTrue()
    {
        TurnFlowValidator.CanDraw(handTileCount: 10, meldTileCount: 4, declaredKongCount: 1).Should().BeTrue();
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(12, 2)]
    [InlineData(0, 13)]
    public void CanDraw_WhenPlayerHandShapeIsInvalid_ReturnsFalse(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDraw(handTileCount, meldTileCount).Should().BeFalse();
    }

    [Fact]
    public void CanDraw_WhenDeclaredKongCountMismatch_ReturnsFalse()
    {
        TurnFlowValidator.CanDraw(handTileCount: 10, meldTileCount: 4, declaredKongCount: 0).Should().BeFalse();
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(11, 3)]
    [InlineData(10, 4)]
    public void CanDiscard_WhenPlayerOwnsFourteenTilesAndHasHandTile_ReturnsTrue(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDiscard(handTileCount, meldTileCount).Should().BeTrue();
    }

    [Fact]
    public void CanDiscard_WhenPlayerHasDeclaredKongAndValidOwnedShape_ReturnsTrue()
    {
        TurnFlowValidator.CanDiscard(handTileCount: 11, meldTileCount: 4, declaredKongCount: 1).Should().BeTrue();
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

    [Fact]
    public void CanDeclareMeld_WhenPlayerHasDeclaredKongAndValidOwnedShape_ReturnsTrue()
    {
        TurnFlowValidator.CanDeclareMeld(handTileCount: 11, meldTileCount: 4, meldTileRequirement: 3, declaredKongCount: 1).Should().BeTrue();
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

    [Theory]
    [InlineData(14, 0)]
    [InlineData(11, 3)]
    [InlineData(2, 12)]
    public void CanDeclareWin_WhenPlayerOwnsFourteenTilesAndHasHandTile_ReturnsTrue(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDeclareWin(handTileCount, meldTileCount).Should().BeTrue();
    }

    [Fact]
    public void CanDeclareWin_WhenPlayerHasDeclaredKongAndValidOwnedShape_ReturnsTrue()
    {
        TurnFlowValidator.CanDeclareWin(handTileCount: 11, meldTileCount: 4, declaredKongCount: 1).Should().BeTrue();
    }

    [Theory]
    [InlineData(13, 0)]
    [InlineData(15, 0)]
    [InlineData(0, 14)]
    public void CanDeclareWin_WhenOwnershipShapeIsInvalid_ReturnsFalse(int handTileCount, int meldTileCount)
    {
        TurnFlowValidator.CanDeclareWin(handTileCount, meldTileCount).Should().BeFalse();
    }

    [Fact]
    public void CanDeclareWin_WhenDeclaredKongCountMismatch_ReturnsFalse()
    {
        TurnFlowValidator.CanDeclareWin(handTileCount: 11, meldTileCount: 4, declaredKongCount: 0).Should().BeFalse();
    }
}
