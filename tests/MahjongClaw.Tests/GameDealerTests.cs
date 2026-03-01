using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class GameDealerTests
{
    [Fact]
    public void InitializeGame_DealsExpectedHandCounts()
    {
        var rng = new Random(42);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        var east = state.GetPlayer(Wind.East);
        var south = state.GetPlayer(Wind.South);
        var west = state.GetPlayer(Wind.West);
        var north = state.GetPlayer(Wind.North);

        east.Hand.Tiles.Should().HaveCount(14);
        south.Hand.Tiles.Should().HaveCount(13);
        west.Hand.Tiles.Should().HaveCount(13);
        north.Hand.Tiles.Should().HaveCount(13);
    }

    [Fact]
    public void InitializeGame_LeavesExpectedWallCount()
    {
        var rng = new Random(99);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        state.Wall.RemainingCount.Should().Be(144 - 53);
    }
}
