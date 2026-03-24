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

    [Fact]
    public void SelectDiscardIndex_PrioritizesSeasonTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Seasons, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Seasons, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesIsolatedHonor_WhenStrongSuitedClusterExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesIsolatedTerminal_WhenSuitClusterDominates()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesIsolatedOffSuitSingleton_WhenDominantSuitAndHonorPairExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesIsolatedTerminal_InSingleSuitWithPairs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }
}
