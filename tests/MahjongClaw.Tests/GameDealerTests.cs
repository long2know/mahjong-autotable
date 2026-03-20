using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

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

    [Fact]
    public void InitializeGame_AssignsPlayersToWindsInSeatOrder()
    {
        var rng = new Random(17);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        state.GetPlayer(Wind.East).Name.Should().Be("Alice");
        state.GetPlayer(Wind.South).Name.Should().Be("Bob");
        state.GetPlayer(Wind.West).Name.Should().Be("Chris");
        state.GetPlayer(Wind.North).Name.Should().Be("Dana");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void InitializeGame_ThrowsWhenPlayerCountIsNotFour(int playerCount)
    {
        var names = Enumerable.Range(0, playerCount)
            .Select(index => $"Player{index}")
            .ToArray();

        var act = () => GameDealer.InitializeGame(names, new Random(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Exactly four players are required.");
    }
}
