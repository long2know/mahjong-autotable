using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ChangshaRobbingKongDetectorTests
{
    [Fact]
    public void CanRobPromotedKong_ReturnsTrue_WhenOpponentWouldWin()
    {
        List<Tile> handTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Dots, 1), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 5)
        ];

        var promotedTile = new Tile(TileSuit.Bamboos, 5);

        var result = ChangshaRobbingKongDetector.CanRobPromotedKong(handTiles, promotedTile);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRobPromotedKong_ReturnsFalse_WhenOpponentWouldNotWin()
    {
        List<Tile> handTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Dots, 1), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 5)
        ];

        var promotedTile = new Tile(TileSuit.Bamboos, 7);

        var result = ChangshaRobbingKongDetector.CanRobPromotedKong(handTiles, promotedTile);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRobPromotedKong_ReturnsTrue_ForSevenPairsWaiting()
    {
        List<Tile> handTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1),
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5),
            new Tile(TileSuit.Bamboos, 6), new Tile(TileSuit.Bamboos, 6),
            new Tile(TileSuit.Bamboos, 7)
        ];

        var promotedTile = new Tile(TileSuit.Bamboos, 7);

        var result = ChangshaRobbingKongDetector.CanRobPromotedKong(handTiles, promotedTile);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRobPromotedKong_ReturnsFalse_ForIncompleteHand()
    {
        List<Tile> handTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Bamboos, 5)
        ];

        var promotedTile = new Tile(TileSuit.Bamboos, 6);

        var result = ChangshaRobbingKongDetector.CanRobPromotedKong(handTiles, promotedTile);

        result.Should().BeFalse();
    }
}
