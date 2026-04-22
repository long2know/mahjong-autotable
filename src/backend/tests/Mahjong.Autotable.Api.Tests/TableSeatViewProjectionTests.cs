using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests;

public class TableSeatViewProjectionTests
{
    private readonly TableStateEngine _engine = new();

    [Fact]
    public void ToSeatViewDto_HidesOpponentTilesAndWallContents()
    {
        var state = _engine.CreateInitialState(seed: 300);
        var session = CreateSession();

        var view = session.ToSeatViewDto(state, viewerSeatIndex: 0);

        Assert.Equal(0, view.ViewerSeatIndex);
        Assert.Equal(state.Wall.Count, view.State.WallCount);

        var selfHand = Assert.Single(view.State.Hands, hand => hand.SeatIndex == 0);
        Assert.NotNull(selfHand.Tiles);
        Assert.Equal(state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Count, selfHand.TileCount);
        Assert.Equal(selfHand.Tiles!.OrderBy(tile => tile), selfHand.Tiles);

        foreach (var opponentHand in view.State.Hands.Where(hand => hand.SeatIndex != 0))
        {
            Assert.Null(opponentHand.Tiles);
            Assert.Equal(
                state.Hands.Single(hand => hand.SeatIndex == opponentHand.SeatIndex).Tiles.Count,
                opponentHand.TileCount);
        }
    }

    [Fact]
    public void ToSeatViewDto_WhenSeatIsMissing_ThrowsArgumentOutOfRangeException()
    {
        var state = _engine.CreateInitialState(seed: 301);
        var session = CreateSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ToSeatViewDto(state, viewerSeatIndex: 4));
    }

    private static TableSession CreateSession() =>
        new()
        {
            RuleSet = "changsha",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
}
