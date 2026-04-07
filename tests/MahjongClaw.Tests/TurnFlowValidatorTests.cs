using FluentAssertions;
using MahjongClaw.Domain.Gameplay;

namespace MahjongClaw.Tests;

public sealed class TurnFlowValidatorTests
{
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
}
