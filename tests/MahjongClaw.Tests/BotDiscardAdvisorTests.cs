using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;
using System.Linq;

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
    public void SelectDiscardIndex_PrioritizesFlowerOverSeason_WhenBothBonusTilesPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Seasons, 2),
            new(TileSuit.Flowers, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Flowers, 1));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksBonusTileTies_ByIndexOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1),
            new(TileSuit.Flowers, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        discardIndex.Should().Be(0);
    }

    [Fact]
    public void SelectDiscardIndex_BreaksHonorTie_BySuitOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        discardIndex.Should().Be(1);
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
    public void SelectDiscardIndex_PrioritizesIsolatedTerminal_WhenPairsAndTripletsPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
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

    [Fact]
    public void SelectDiscardIndex_PrioritizesIsolatedHonor_WhenHonorPairAndStrongSuitClusterExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesOffSuitSingleton_WhenHonorMajorityPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksHonorSingletonTies_ByWindOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksHonorSingletonTies_ByDragonOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesHonorSingleton_WhenSevenPairsBiasIsStrong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksSuitedSingletonTies_BySuitOrder_WithTerminalSingletons()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSingleton_WhenSixPairsPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);
        var discardedTile = tiles[discardIndex];

        tiles.Count(tile => tile == discardedTile).Should().Be(1);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesHonorSingleton_WhenSixPairsAndSuitedNeighborExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSingleton_WhenFourPairsPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);
        var discardedTile = tiles[discardIndex];

        tiles.Count(tile => tile == discardedTile).Should().Be(1);
        discardedTile.IsHonor.Should().BeTrue();
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSingleton_WhenFivePairsPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);
        var discardedTile = tiles[discardIndex];

        tiles.Count(tile => tile == discardedTile).Should().Be(1);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesHonorSingleton_WhenMultipleSuitedPairsExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSuitedSingleton_WhenHonorQuadsDominate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 5),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardOnlyPair_WhenSinglesAvailable()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 5), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesOffSuitSingleton_WhenDominantSuitHasConnectedRuns()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesOffSuitSingleton_WhenPairedRunPotentialExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesGapSingleton_WhenOnlyTwoAwayNeighborExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSingletonWithWeakerRunPotential_WhenTwoSidedRunExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesTerminalOffSuit_WhenMultipleOffSuitSingletonsExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSuitedSingleton_WhenHonorMajorityExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesWindHonor_WhenHonorSingletonsHaveEqualScores()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesTerminalSingleton_WhenHonorMajorityExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksSuitedSingletonTies_BySuitOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksSuitedSingletonTies_ByRankOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksWindTies_ByWindOrder()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }
}
