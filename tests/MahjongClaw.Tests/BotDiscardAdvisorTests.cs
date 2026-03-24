using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class BotDiscardAdvisorTests
{
    [Fact]
    public void SelectDiscardIndex_ThrowsForNullHand()
    {
        Action act = () => BotDiscardAdvisor.SelectDiscardIndex(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectDiscardIndex_ThrowsForEmptyHand()
    {
        var tiles = new List<Tile>();

        Action act = () => BotDiscardAdvisor.SelectDiscardIndex(tiles);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Hand cannot be empty.*");
    }

    [Fact]
    public void SelectDiscardIndex_ThrowsWhenTilesContainNull()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            null!
        };

        Action act = () => BotDiscardAdvisor.SelectDiscardIndex(tiles);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBonusTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Flowers, 1));
    }
}
