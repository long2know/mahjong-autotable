using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;
using System;

namespace MahjongClaw.Tests;

public class GameStateTests
{
    [Fact]
    public void AdvanceTurn_CyclesThroughWindsInOrder()
    {
        var players = new[]
        {
            new Player(Guid.NewGuid(), "Alice", Wind.East),
            new Player(Guid.NewGuid(), "Bob", Wind.South),
            new Player(Guid.NewGuid(), "Chris", Wind.West),
            new Player(Guid.NewGuid(), "Dana", Wind.North)
        };

        var wall = new Wall(Array.Empty<Tile>());
        var state = new GameState(Guid.NewGuid(), players, wall, Wind.East);

        state.CurrentTurn.Should().Be(Wind.East);

        state.AdvanceTurn();
        state.CurrentTurn.Should().Be(Wind.South);

        state.AdvanceTurn();
        state.CurrentTurn.Should().Be(Wind.West);

        state.AdvanceTurn();
        state.CurrentTurn.Should().Be(Wind.North);

        state.AdvanceTurn();
        state.CurrentTurn.Should().Be(Wind.East);
    }
}
