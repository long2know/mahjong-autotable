using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Rules;
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
    public void SelectDiscardIndex_PreservesOnlyPair_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesOnlyActualPair_WhenTripletAlsoPresent_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingSoleTripletPair_WhenOutsImprove_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleNonTerminalPair_WhenTerminalHonorLeanHasNoVisibility()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersHonorSingleton_WhenNoHonorPairs()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersHonorSingleton_WhenHonorsAreVerySparse()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersHonorSingleton_WhenHonorsAreSparse()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.South),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersHonorSingleton_WhenHonorsAreSparseWithoutVisibleTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].IsHonor.Should().BeTrue();
    }

    [Fact]
    public void SelectDiscardIndex_PrefersNonOrphanDiscard_WhenThirteenOrphansPathIsLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingConcealedKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.ChineseOfficial);

        tiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesPairsWhenSixPairsPresent_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 9),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesOffSuitTiles_WhenChineseOfficialNearPureSuit()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Suit.Should().NotBe(TileSuit.Dots);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadSingleton_WhenSevenPairsLeanHasTriplet_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearDeadSingleton_WhenSevenPairsLeanWithVisibleTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBreakingDeadPair_WhenChineseOfficialAndExtraPairAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 5)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesPairs_WhenChineseOfficialSevenPairsNearLeanHasDeadPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 5)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsNearDeadPair_WhenChineseOfficialAndExtraPairAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 5), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotBreakDeadPair_WhenChineseOfficialHasSequencePotential()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardDeadPair_WhenItEliminatesImmediateWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Bamboos, 5));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksOffRankPair_WhenChangsha258AndNoSmallWinTilesSeen()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangsha258Ruleset_DiscardsHonorTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesOffRankPair_WhenChangsha258HasIsolatedSmallWinSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsTrulyIsolatedSmallWinSingleton_WhenGapSequenceExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesOffRankPair_WhenNoSmallWinTilesInHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersChowPotentialOverDeadOffRankPair_WhenChangsha258EyeAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotProtectGapBridgePair_WhenSmallWinSingletonIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithSmallWinPair_PrioritizesDeadOffRankSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOffRankPair_WhenOnlyOneSmallWinOutRemains()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOffRankPair_WhenAllPungsOutsAreAtRisk()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 6));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakSoleSmallWinPair_WhenOffSuitMeldBlocksPureSuit()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesOnlySmallWinSingleton_WhenNoVisibleTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksSoleSmallWinPair_WhenPureSuitLeanIsStrongWithoutDominantSmallWinPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingConcealedKong_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsIsolatedOffRankConcealedKong_WhenSmallWinPairMissing()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsIsolatedOffRankConcealedKong_WhenNoSmallWinTilesInHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsIsolatedOffRankConcealedKong_WhenSevenPairsOutsAreDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsOffRankConcealedKong_WhenSequencePotentialIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardDominantSuitConcealedKong_WhenPureSuitLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_BreaksConcealedKong_WhenKeepingItLosesAllImmediateWinningOuts()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 1)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingPromotedKong_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingPromotedKong_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingPromotedKong_WhenChangsha258SmallWinPairIsExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotTreatPromotableChangshaSmallWinSingletonAsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_KeepsOffSuitSmallWinPair_WhenDominantSuitSmallWinOutsAreExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
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
    public void SelectDiscardIndex_PrioritizesKeepingTwoFiveEight_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingTwoFiveEightSingles_WhenNoPairExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleSmallWinSingleton_WhenChangshaNoVisibility()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleSmallWinSingleton_WhenChangshaNoVisibilityAndPureSuitLeanStillOffSuit()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardIsolatedSmallWinSingleton_WhenItEliminatesAllWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 9)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var visibleCounts = new Dictionary<Tile, int>();
        var isolatedIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Bamboos, 2)));
        isolatedIndex.Should().BeGreaterOrEqualTo(0);

        var isolatedOuts = CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            isolatedIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true);

        isolatedOuts.TileTypes.Should().Be(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));

        var preferredOuts = CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            discardIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true);

        preferredOuts.TileTypes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsDeadOffRankPair_WhenSmallWinCandidatesAreWeak()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var smallWinRanks = new[] { 2, 5, 8 };
        var smallWinSuits = new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos };
        foreach (var suit in smallWinSuits)
        {
            foreach (var rank in smallWinRanks)
            {
                if (suit == TileSuit.Dots && rank == 2)
                {
                    continue;
                }

                visibleTiles.Add(new Tile(suit, rank));
                visibleTiles.Add(new Tile(suit, rank));
                visibleTiles.Add(new Tile(suit, rank));
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBestSmallWinSingleton_WhenMultipleLiveSingles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardLastSmallWinSingleton_WhenOnlyLiveCandidateHasMultipleCopies()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsDeadSmallWinSingleton_WhenOtherLiveSmallWinSinglesRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Dots, 1)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsDeadSmallWinSingleton_WhenSmallWinPairAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadSmallWinSingleton_WhenOnlyLiveSmallWinSingletonIsNearlyDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsNearDeadSmallWinSingleton_WhenAlternateSmallWinCandidatesRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardNearDeadSmallWinSingleton_WhenAdjacentTileKeepsSequencePotential()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDeadOffRankPair_WhenNearDeadSmallWinSingletonHasAlternateCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardNearDeadSmallWinSingleton_WhenItBreaksOnlyWinningWait()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 7));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardNearDeadSmallWinSingleton_WhenItEliminatesOnlyWinningOut()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var dot2Index = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Dots, 2)));
        var dot4Index = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Dots, 4)));

        dot2Index.Should().BeGreaterThanOrEqualTo(0);
        dot4Index.Should().BeGreaterThanOrEqualTo(0);

        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            dot2Index,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).TileTypes.Should().Be(0);

        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            dot4Index,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).TileTypes.Should().BeGreaterThan(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardDeadSmallWinSingleton_WhenItEliminatesOnlyWinningOut()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var dot2Index = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Dots, 2)));
        var fillerIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Characters, 1)));

        dot2Index.Should().BeGreaterThanOrEqualTo(0);
        fillerIndex.Should().BeGreaterThanOrEqualTo(0);

        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            dot2Index,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).TileTypes.Should().Be(0);

        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            fillerIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).TileTypes.Should().BeGreaterThan(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDiscardingInvalidTile_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBonusTileAmongInvalidTiles_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Flowers, 1),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Flowers, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesInvalidTile_WhenSmallWinSingletonAnchorsChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 9)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesKeepingRequiredSmallWinPair_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Suit.Should().Be(TileSuit.Characters);
        tiles[discardIndex].Rank.Should().BeOneOf(4, 6);
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenChangshaPureSuitLean()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenPureSuitLeanIsVeryStrong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenPureSuitLeanHasDistinctRankSpread()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOffSuitSmallWinPair_WhenPureSuitLeanHasNoDominantSmallWinTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenChangshaPureSuitLeanHasTripletWithoutLiveDominantSmallWinCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>();
        foreach (var rank in new[] { 2, 5, 8 })
        {
            for (var i = 0; i < 4; i++)
            {
                visibleTiles.Add(new Tile(TileSuit.Characters, rank));
            }
        }

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenDominantSuitSmallWinOutsAreDeadWithoutTriplet()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>();
        foreach (var rank in new[] { 2, 5, 8 })
        {
            for (var i = 0; i < 4; i++)
            {
                visibleTiles.Add(new Tile(TileSuit.Characters, rank));
            }
        }

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOffSuitSmallWinPair_WhenDominantSuitSmallWinOutsNearlyDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>();
        foreach (var rank in new[] { 2, 5 })
        {
            for (var i = 0; i < 4; i++)
            {
                visibleTiles.Add(new Tile(TileSuit.Characters, rank));
            }
        }

        for (var i = 0; i < 3; i++)
        {
            visibleTiles.Add(new Tile(TileSuit.Characters, 8));
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOffSuitSmallWinPair_WhenPureSuitLeanHasNoAlternateSmallWinCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                if (suit == TileSuit.Dots && rank == 2)
                {
                    continue;
                }

                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsDiscardingExtraCopyOfSoleSmallWinPair_WhenTriplet()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsSmallWinSingleton_WhenAllPungsBigWinCandidate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesLiveSmallWinSingleton_WhenAlternatePairIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesLastSmallWinSingleton_WhenNotAllPungsCandidate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesLastSmallWinSingleton_WhenOnlyOneCopyRemainsAndNoAlternateSmallWinCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakOnlyPair_WhenNoSmallWinTilesInHand()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_BreaksNonSmallWinPair_WhenSmallWinTilesSeen()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersBreakingNonSmallWinPair_WhenSmallWinPairStillPossible()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersOffRankPairs_WhenSmallWinSingletonPresentAndNoSmallWinPairYet()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesOffRankPair_WhenSmallWinSingletonIsGapAdjacentWithBridge()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakNonSmallWinPair_WhenPureSuitBigWinCandidate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotDiscardSoleSmallWinPair_WhenDominantSuitFilterActive()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Suit.Should().Be(TileSuit.Dots);
    }

    [Fact]
    public void SelectDiscardIndex_DiscardsExtraSmallWinSingleton_WhenPairAlreadySecured()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakDeadOffRankPair_WhenSevenPairsCandidateInChangsha()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDiscardThatKeepsImmediateWinningOut_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBigWinOuts_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesBigWinOutsOverSmallWinOuts_WhenChangsha258EyeIsFlexible()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesSmallWinPair_WhenBigWinOutsAreNearlyDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesSoleSmallWinSingleton_WhenBigWinOutsLag()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSevenPairsBias_WhenChangsha258Ruleset()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 1)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersSevenPairsTrack_WhenFivePairsAndOffSuitSmallWinSingleton()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.Changsha258);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSingleton_WhenSevenPairsLeanInChangsha()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadSmallWinSingleton_WhenSevenPairsLeanInChangsha()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSevenPairs_WhenDeadOffRankPairIsIsolatedInChangsha()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersDeadSingletonOverDeadOffRankPair_WhenSmallWinPairSecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithMelds_DoesNotApplySevenPairsBias()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithMelds_PrioritizesDiscardThatPreservesImmediateWinningOut()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 7)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesLiveWinningOuts_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 8));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadTwoFiveEightSingleton_WhenChangshaPairNotYetMade()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesDeadSmallWinSingletonWithSequencePotential_WhenAlternateLiveSmallWinSingletonExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesDeadPair_WhenSevenPairsLeanInChangsha()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsNearDeadOffRankPair_WhenSmallWinPairSecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesDominantSuitNearDeadPair_WhenPureSuitLeanHasOffSuitSingletons()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Characters);
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsIsolatedOffRankPair_WhenSmallWinPairSecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotDiscardNearDeadOffRankPair_WhenChowPotentialExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesNearDeadOffRankPair_WhenItWouldEliminateWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var winningCandidate = handTiles
            .Where(tile => tile != new Tile(TileSuit.Bamboos, 9))
            .Append(new Tile(TileSuit.Characters, 9))
            .ToList();
        RulesetHandAnalyzer.TryGetWinningHand(winningCandidate, MahjongRuleset.Changsha258, out _).Should().BeTrue();

        int CountWinningOuts(int discardIndex)
        {
            var discardTile = handTiles[discardIndex];
            var reducedHand = handTiles
                .Where((_, index) => index != discardIndex)
                .ToList();
            var ownedCounts = reducedHand
                .GroupBy(tile => tile)
                .ToDictionary(group => group.Key, group => group.Count());
            var visibleCounts = visibleTiles
                .GroupBy(tile => tile)
                .ToDictionary(group => group.Key, group => group.Count());
            var winningOuts = 0;

            foreach (var suit in Changsha258Ruleset.AllowedSuits)
            {
                for (var rank = 1; rank <= 9; rank++)
                {
                    var drawTile = new Tile(suit, rank);
                    ownedCounts.TryGetValue(drawTile, out var ownedCount);
                    visibleCounts.TryGetValue(drawTile, out var visibleCount);
                    if (drawTile.Equals(discardTile))
                    {
                        visibleCount++;
                    }

                    var remainingCopies = 4 - ownedCount - visibleCount;
                    if (remainingCopies <= 0)
                    {
                        continue;
                    }

                    var candidateHand = reducedHand.Append(drawTile).ToList();
                    if (!RulesetHandAnalyzer.TryGetWinningHand(candidateHand, MahjongRuleset.Changsha258, out _))
                    {
                        continue;
                    }

                    winningOuts++;
                }
            }

            return winningOuts;
        }

        var bambooDiscardIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Bamboos, 9)));
        var characterDiscardIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Characters, 9)));

        CountWinningOuts(bambooDiscardIndex).Should().BeGreaterThan(0);
        CountWinningOuts(characterDiscardIndex).Should().Be(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesNearDeadOffRankPair_WhenNonTerminalPairEliminatesWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var winningCandidate = handTiles
            .Where(tile => tile != new Tile(TileSuit.Bamboos, 9))
            .Append(new Tile(TileSuit.Characters, 4))
            .ToList();
        RulesetHandAnalyzer.TryGetWinningHand(winningCandidate, MahjongRuleset.Changsha258, out _).Should().BeTrue();

        int CountWinningOuts(int discardIndex)
        {
            var discardTile = handTiles[discardIndex];
            var reducedHand = handTiles
                .Where((_, index) => index != discardIndex)
                .ToList();
            var ownedCounts = reducedHand
                .GroupBy(tile => tile)
                .ToDictionary(group => group.Key, group => group.Count());
            var visibleCounts = visibleTiles
                .GroupBy(tile => tile)
                .ToDictionary(group => group.Key, group => group.Count());

            var winningOuts = 0;

            foreach (var suit in Changsha258Ruleset.AllowedSuits)
            {
                for (var rank = 1; rank <= 9; rank++)
                {
                    var drawTile = new Tile(suit, rank);
                    ownedCounts.TryGetValue(drawTile, out var ownedCount);
                    visibleCounts.TryGetValue(drawTile, out var visibleCount);
                    if (drawTile.Equals(discardTile))
                    {
                        visibleCount++;
                    }

                    var remainingCopies = 4 - ownedCount - visibleCount;
                    if (remainingCopies <= 0)
                    {
                        continue;
                    }

                    var candidateHand = reducedHand.Append(drawTile).ToList();
                    if (!RulesetHandAnalyzer.TryGetWinningHand(candidateHand, MahjongRuleset.Changsha258, out _))
                    {
                        continue;
                    }

                    winningOuts++;
                }
            }

            return winningOuts;
        }

        var bambooDiscardIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Bamboos, 9)));
        var characterDiscardIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Characters, 4)));

        CountWinningOuts(bambooDiscardIndex).Should().BeGreaterThan(0);
        CountWinningOuts(characterDiscardIndex).Should().Be(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsNearDeadSingleton_WhenSmallWinPairSecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersDeadTileWhenSmallWinBiasActive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsDeadSmallWinSingleton_WhenItAnchorsChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsDeadSmallWinSingletonAnchoringChow_WhenOtherSmallWinSingletonLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_ProtectsSoleLiveSmallWinSingleton_WhenNoStrongPairCandidate()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_ProtectsSoleLiveSmallWinSingleton_WhenOnlyOneCopyRemains()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 4)
        };

        var visibleTiles = new List<Tile>();
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 2), 2));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 2), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 2), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 5), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 5), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 5), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 8), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 8), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 8), 4));

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_BreaksNonSmallWinPair_WhenSmallWinSingletonAnchorsChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadExtraSmallWinSingleton_WhenPairAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersSmallWinPairWithMoreRemainingCopies_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadSmallWinPair_WhenMultiplePairsAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersChowPotentialOverDeadOffRankPair_WhenSmallWinPairAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 8));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadOffRankSingleton_WhenSmallWinPairAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadOffRankPair_WhenNoSmallWinPairsAreLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotForceDeadOffRankQuadDiscard_WhenItLosesImmediateWinningOut()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 1)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Bamboos, 2), new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsSoleLiveSmallWinPairCandidate_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                if (suit == TileSuit.Dots && rank == 2)
                {
                    continue;
                }

                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_BreaksOffRankPair_WhenOnlySingleSmallWinCopyIsLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                var copies = suit == TileSuit.Dots && rank == 2 ? 2 : 3;
                for (var i = 0; i < copies; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersSmallWinSingletonWithAdjacentSuit_WhenMultipleLiveSmallWinSingletons()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsExhaustedSmallWinPair_WhenPairAlreadyFormed()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotBreakOffRankPair_WhenAdjacentSmallWinSingletonIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDeadOffRankSingleton_WhenSmallWinPairMissing()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>();
        for (var i = 0; i < 4; i++)
        {
            visibleTiles.Add(new Tile(TileSuit.Dots, 7));
            visibleTiles.Add(new Tile(TileSuit.Dots, 8));
        }

        for (var i = 0; i < 3; i++)
        {
            visibleTiles.Add(new Tile(TileSuit.Dots, 9));
        }

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersNearDeadSmallWinPair_WhenMultipleSmallWinPairsAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDeadHonorSingleton_WhenOutsTie()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotDiscardPromotableHonorKongSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 5)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PreservesOffRankPairWithSequencePotential_WhenSmallWinPairMissing()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>();
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 6), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 8), 4));

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsOnlyNonSmallWinPair_WhenNoSmallWinPairIsLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                var tile = new Tile(suit, rank);
                var handCount = handTiles.Count(candidate => candidate.Equals(tile));
                for (var i = 0; i < 4 - handCount; i++)
                {
                    visibleTiles.Add(tile);
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_KeepsSoleLiveSmallWinSingletonCandidate_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                var copies = suit == TileSuit.Dots && rank == 2 ? 2 : 4;
                for (var i = 0; i < copies; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_ProtectsOnlySmallWinSingleton_WhenOnlyOneCopyRemains()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                var tile = new Tile(suit, rank);
                if (suit == TileSuit.Dots && rank == 2)
                {
                    visibleTiles.Add(tile);
                    visibleTiles.Add(tile);
                    continue;
                }

                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(tile);
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsSmallWinSingleton_WhenNoSmallWinPairsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithMeldsAndVisibleTiles_DiscardsDeadSmallWinSingleton_WhenPairNotInHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDiscardingDeadSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_WithMeldedPung_PrefersLiveSmallWinPairCandidate_WhenSingletonIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesNearlyDeadHonorSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadAdjacency_WhenNoCompletionTilesRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadGap_WhenMiddleTileIsExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsStrictDeadSingleton_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadSmallWinSingleton_WhenChangshaPairAlreadySecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDeadSingletonOverNearDeadOffRankPair_WhenChangshaPairSecured()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsNearDeadSingleton_WhenChangshaPairSecuredAndNoSequenceAnchor()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotDiscardDeadOffRankPair_WhenChowPotentialExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotBreakOnlyChangshaSmallWinPair_WhenPairIsExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_ProtectsSmallWinSingleton_WhenOnlyNonSmallWinPairIsDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                var tile = new Tile(suit, rank);
                if (suit == TileSuit.Dots && rank == 2)
                {
                    continue;
                }

                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(tile);
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesPureSuitOverOffSuitSmallWinTile_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_OverridesPureSuitFilter_WhenWinningOutsExist()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Dots);
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotBreakSoleChangshaSmallWinPair_WhenBreakingEliminatesAllWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotOverprotectOffSuitSmallWinSingleton_WhenChangshaPureSuitLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersDominantSuitSmallWinSingleton_WhenChangshaPureSuitLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesOffSuitDeadSmallWinSingleton_WhenChangshaPureSuitLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_ReenablesSmallWinPairBias_WhenBigWinOutsAreDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesPureSuitWhenMeldsPushDominantSuit_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Bamboos);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearPureSuit_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Bamboos);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesPureSuitAtBigWinThreshold_WhenChangsha258Ruleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Characters);
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotOverridePureSuitOuts_WhenChangshaPureSuitLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Suit.Should().Be(TileSuit.Characters);
    }

    [Fact]
    public void SelectDiscardIndex_AllowsBreakingOffSuitSmallWinPair_WhenDominantSuitHasLiveCandidate()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesPureSuitHonors_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearPureSuit_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Suit.Should().NotBe(TileSuit.Dots);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesAllHonorsLean_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].IsHonor.Should().BeFalse();
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialOrphanDuplicates_PrioritizesDeadDuplicateOrphan()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_BreaksNonTerminalPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_PreservesNonOrphanPairWhenSingletonExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_PreservesNonOrphanPung()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_WhenNonOrphanPungIsDead_DiscardsPungTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialHonorLean_EarlierThreshold()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].IsHonor.Should().BeFalse();
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_AccountsForDiscardedTileAvailability()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsFeedingOpponentKong()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_DoesNotAssumeKongThreats_WhenThreatTilesNotProvided()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_IgnoresNonThreatMelds_WhenKongThreatsEmpty()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_UsesMeldCountsAsMinimumNotSum()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1)
        };

        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 2), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 3), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 2), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 3), 4));

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_PrefersSafelyExposedTile_WhenWinningOutsTie()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersMeldExposedTile_WhenDiscardScoresTie()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles
            .Concat(new[] { new Tile(TileSuit.Characters, 9) })
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_PrefersSafelyExposedTile_WhenChangshaHasNoOutsAndSafetyTies()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles
            .Concat(Enumerable.Repeat(new Tile(TileSuit.Characters, 9), 3))
            .ToList();
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var dotNineIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Dots, 9)));
        var charNineIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Characters, 9)));

        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            dotNineIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).Should().Be(new WinningDrawOutSummary(0, 0));
        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            charNineIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: true).Should().Be(new WinningDrawOutSummary(0, 0));
        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            dotNineIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: false).Should().Be(new WinningDrawOutSummary(0, 0));
        CountWinningDrawOutsAfterDiscard(
            handTiles,
            visibleCounts,
            charNineIndex,
            MahjongRuleset.Changsha258,
            requireMinimumFan: false).Should().Be(new WinningDrawOutSummary(0, 0));

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AccountsForMeldsWhenVisibleTilesOmitThem()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            visibleMeldTiles,
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AddsDiscardCounts_WhenVisibleTilesExcludeMelds()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_PrefersDeadTile_WhenDiscardsLagMeldCounts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_DoesNotBreakConcealedKongToAvoidThreat()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsKongThreat_WhenDeadSingletonFilterLeavesOnlyThreatTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 5), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 6), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 7),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        potentialOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AllowsDeadKongThreatDiscard_WhenMultiplePungsPresent()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 5)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsFeedingOpponentKong_WhenOutsTie()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 2)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsKongFeed_WhenNoWinningOutsExist()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var visibleTiles = BuildExhaustedVisibleTiles();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsKongThreat_WhenOnlyLowFanOutsExist()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_KeepsPotentialOuts_WhenMinimumFanBlocksWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 9)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().BeGreaterThan(0);

        var maxPotentialCopies = potentialOutSummaries
            .Where(summary => summary.TileTypes == maxPotentialOuts)
            .Max(summary => summary.RemainingCopies);

        var bestIndices = potentialOutSummaries
            .Select((summary, index) => new { summary, index })
            .Where(entry => entry.summary.TileTypes == maxPotentialOuts
                && entry.summary.RemainingCopies == maxPotentialCopies)
            .Select(entry => entry.index)
            .ToHashSet();

        bestIndices.Should().OnlyContain(index => handTiles[index].Equals(new Tile(TileSuit.Bamboos, 9)));

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_DoesNotUsePotentialOuts_WhenChangshaSmallWinBlocksWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2)
        };

        var visibleTiles = visibleMeldTiles.ToList();
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.Changsha258,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.Changsha258,
                requireMinimumFan: false))
            .ToList();

        potentialOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.Changsha258);

        Changsha258Ruleset.IsSmallWinPair(handTiles[discardIndex]).Should().BeFalse();
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_UsesRemainingCopyTieBreaker()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_IgnoresSingleChowExposureForSafety()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3)
        };
        visibleTiles.AddRange(visibleMeldTiles);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_PrefersNonFeedWhenWinningOutsAreClose()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        var bestOverallOuts = winningOutSummaries.Max(summary => summary.TileTypes);
        var bestNonThreatOuts = winningOutSummaries
            .Select((summary, index) => new { summary, index })
            .Where(entry => !handTiles[entry.index].Equals(new Tile(TileSuit.Characters, 9)))
            .Max(entry => entry.summary.TileTypes);

        bestOverallOuts.Should().BeGreaterThan(0);
        bestNonThreatOuts.Should().BeGreaterOrEqualTo(bestOverallOuts - 2);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_KeepsWinningOuts_WhenAvoidingFeedWouldLoseAllOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 2)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsFeedingOpponentKong_WhenOnlyCopyInHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_IgnoresExposedKongWhenChoosingDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 5)
        };

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_DiscardsNonOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_PrioritizesFlowerDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Flowers, 1),
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Flowers, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphansThreshold_DiscardsNonOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphansExtendedThreshold_DiscardsNonOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialEightDistinctOrphans_DiscardsNonOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 6));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_WhenMissingOrphanIsDead_DiscardsOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_WhenOrphanSingletonIsDead_PrioritizesDeadOrphan()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearThirteenOrphans_WhenDeadHonorSingletonExists_PreservesOrphanTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearAllTerminalsAndHonors_DiscardsNonTerminalTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearAllTerminalsAndHonors_AllowsBreakingSoleNonTerminalPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialNearAllTerminalsAndHonors_PrefersNonTerminalSingletonOverSolePair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 6));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialTerminalHonorLean_PrefersDeadHonorSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 5)
        };

        var visibleTiles = BuildExhaustedVisibleTiles();
        visibleTiles.RemoveAll(tile => tile.Equals(new Tile(TileSuit.Dots, 4))
            || tile.Equals(new Tile(TileSuit.Dots, 6)));

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialDuplicateOrphans_PrefersNonOrphanDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithChineseOfficialAllOrphans_PrefersDuplicateOrphanDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_SkipsThirteenOrphansBias_WhenMissingOrphanTypesAreExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should()
            .NotBe(new Tile(TileSuit.Dots, 2))
            .And.NotBe(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadPair_WhenMultiplePairsAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotBreakDeadPair_WhenPairStillHasSequencePotential_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadHonorSingleton_WhenOutsAreClose()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesFewerRemainingCopies_WhenScoresTie()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, Array.Empty<Tile>(), visibleTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesFewerRemainingCopies_WhenChangsha258BigWinCandidate()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 8)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesDeadTile_WhenChangsha258SmallWinBiasHasNoImmediateOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DoesNotOverrideDeadSingletonFilter_WhenSmallWinOutsExist()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrioritizesSmallWinOuts_WhenBigWinOutsAreZero()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadSmallWinSingleton_WhenOtherSmallWinCandidatesAreLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_DiscardsDeadSmallWinSingleton_WhenNoLiveSmallWinSingletonsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_ProtectsSoleLiveSmallWinSingleton_WhenOnlyOneCopyRemains_WithSparseHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersDeadSmallWinPair_WhenLiveSmallWinPairAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_PrefersDeadSmallWinPair_WhenOutsAreClose()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleTiles_UsesRemainingCopyTieBreaker_WhenChangshaSmallWinBiasActiveAndLowestScoreCandidatesAreOffRank()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangsha258NearAllTwoFiveEight_TradesOffRankTilesFirst()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 5), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Rank.Should().Be(3);
    }

    [Fact]
    public void SelectDiscardIndex_WithMelds_PrioritizesKeepingDominantMeldSuit()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 9)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithMeldedPung_PrioritizesKeepingKongUpgradeTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithMeldedNeighbors_DoesNotTreatBlockedMeldTilesAsAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 9)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, visibleTiles);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersKeepingTerminal_WhenOnlyNeighborIsPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        discardIndex.Should().Be(1);
        tiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
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
    public void SelectDiscardIndex_PrioritizesWiderSevenPairsWait_InSingleSuitWithPairs()
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

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
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
    public void SelectDiscardIndex_PrioritizesHonorSingleton_WhenHonorSetsDominate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.West));
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
    public void SelectDiscardIndex_PreservesHonorPair_WhenSevenPairsLeanCompetesWithPureSuitBias()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles);

        tiles[discardIndex].Should().BeOneOf(new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 7));
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
    public void SelectDiscardIndex_PrioritizesWiderWinningWaitOverGapSingleton()
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

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 9));
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
    public void SelectDiscardIndex_WithHonorMelds_PrioritizesSuitedSingleton_WhenHonorMajorityExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 4)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
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

    [Fact]
    public void SelectDiscardIndex_AllowsLastCopyDiscardWhenOpponentHasPromotedKongThreat()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2)
        };

        var opponentMeldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2)
        };

        var visibleTiles = opponentMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            opponentMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsKongThreatDiscard_WhenItPreservesOnlyWinningPath()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 4)
        };

        var opponentMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 6)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            opponentMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void SelectDiscardIndex_AllowsMultiCopyKongThreatDiscard_WhenItPreservesOnlyWinningPath()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2)
        };

        var opponentMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 8)
        };

        var visibleTiles = opponentMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            opponentMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void SelectDiscardIndex_AvoidsKongThreatDiscard_WhenNoWinningOutsExistWithMultiCopyThreat()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 9)
        };

        var opponentMeldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };

        var visibleTiles = opponentMeldTiles
            .Concat(new[]
            {
                new Tile(TileSuit.Dots, 3),
                new Tile(TileSuit.Dots, 3),
                new Tile(TileSuit.Dots, 3),
                new Tile(TileSuit.Dots, 3)
            })
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            opponentMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_ProtectsLastLiveChangshaSmallWinSingleton_WhenNoStrongAlternatives()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_ProtectsFallbackPair_WhenChangshaSmallWinTilesAreDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3)
        };

        var visibleTiles = new List<Tile>();
        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            foreach (var rank in new[] { 2, 5, 8 })
            {
                for (var i = 0; i < 4; i++)
                {
                    visibleTiles.Add(new Tile(suit, rank));
                }
            }
        }

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Characters, 6));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersBreakingOffRankPair_WhenNoVisibleTilesAndNoSmallWinTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 7));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleSmallWinPair_WhenNoLive258InDominantSuit()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleSmallWinPair_WhenNoDominantSuitSmallWinTilesAndNoVisibleTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleSmallWinPair_WhenDominantSuitCandidateIsNearDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadOffRankPairWithoutSequencePotential_WhenNoChangshaSmallWinTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_PivotsFromDeadThirteenOrphansPath_WhenOrphanTilesAreExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_PivotsFromDeadThirteenOrphans_WhenPairCannotBeCompleted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>();
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 1), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 9), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 1), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 9), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 1), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 9), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.East), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.South), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.West), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.North), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.Red), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.Green), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.White), 3));

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex]
            .Should()
            .Match<Tile>(tile => tile.IsHonor || tile.Rank == 1 || tile.Rank == 9);
    }

    [Fact]
    public void SelectDiscardIndex_PivotsFromNearDeadThirteenOrphans_WhenMissingOrphanHasOneCopyLeft()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex]
            .Should()
            .Match<Tile>(tile => tile.IsHonor || tile.Rank == 1 || tile.Rank == 9);
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleTripletPairCandidate_ForChineseOfficial()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 7)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.ChineseOfficial);

        tiles[discardIndex].Should().Be(new Tile(TileSuit.Bamboos, 7));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotForceTerminalDiscard_WhenHonorHeavyWithOnlyTerminalSuits()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.ChineseOfficial);

        tiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Winds, 0, Wind.North),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Green),
            new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadHonorSingleton_OverBreakingSoleNonTerminalPair_WhenTerminalHonorLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Green));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearDeadHonorSingleton_OverBreakingSoleNonTerminalPair_WhenTerminalHonorLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesSparseHonorSingleton_OverBreakingSoleNonTerminalPair_WhenTerminalHonorLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSoleNonTerminalPair_WhenTerminalHonorLeanAndPairIsExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadPair_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesPotentialOuts_WhenDeadPairBlocksTenpai_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadSuitedSingleton_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadHonorSingleton_OverDeadSuitedSingleton_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesDeadHonorSingleton_OverDeadPair_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Green));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearDeadHonorSingleton_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 8),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesHonorSingleton_WhenHonorCountIsFourAndSuitedHeavy()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.South),
            new Tile(TileSuit.Winds, 0, Wind.West),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearDeadSuitedSingleton_WhenChineseOfficialRuleset()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_PivotsFromDeadThirteenOrphans_WhenMissingOrphanIsExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex]
            .Should()
            .Match<Tile>(tile => tile.IsHonor || tile.Rank == 1 || tile.Rank == 9);
    }

    [Fact]
    public void SelectDiscardIndex_PivotsFromNearDeadThirteenOrphans_WhenMissingOrphanHasSingleCopyAndMultipleOffOrphans()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex]
            .Should()
            .Match<Tile>(tile => tile.IsHonor || tile.Rank == 1 || tile.Rank == 9);
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesNearDeadOffRankSingleton_WhenChangshaSmallWinPairMissing()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangshaSmallWinSingleton_PrefersNearDeadOffRankPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 4)
        };
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 2), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 3), 4));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 5), 4));

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 4));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangshaSmallWinPair_PrioritizesDeadSmallWinSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangshaPureSuitLean_PrioritizesDeadOffSuitSmallWinSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesSevenPairsBias_WhenQuadAndTripletPresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(tiles, MahjongRuleset.ChineseOfficial);

        tiles[discardIndex].Should().BeOneOf(
            new Tile(TileSuit.Characters, 1),
            new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7),
            new Tile(TileSuit.Characters, 8));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangshaSevenPairsLean_PrefersTripletOverSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 7));
    }

    [Fact]
    public void SelectDiscardIndex_WithChangshaPureSuitLean_DoesNotBreakSoleSmallWinPair_WhenBigWinOutsLag()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 6), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesWinningOuts_WhenHeuristicsCollapseToZero()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 3)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 7));
    }

    [Fact]
    public void SelectDiscardIndex_PrefersSuitedSingleton_WhenHonorHeavyAndHonorSingletonPresent()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 4));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesCompletedChow_WhenHonorHeavyAndHonorSingletonPresent()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        new[]
        {
            new Tile(TileSuit.Dots, 7),
            new Tile(TileSuit.Dots, 8),
            new Tile(TileSuit.Dots, 9)
        }.Should().NotContain(handTiles[discardIndex]);
    }

    [Fact]
    public void SelectDiscardIndex_UsesPotentialOuts_WhenMinimumFanBlocksAllWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        var visibleCounts = new Dictionary<Tile, int>();
        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().BeGreaterThan(0);

        var maxPotentialCopies = potentialOutSummaries
            .Where(summary => summary.TileTypes == maxPotentialOuts)
            .Max(summary => summary.RemainingCopies);

        var bestIndices = potentialOutSummaries
            .Select((summary, index) => new { summary, index })
            .Where(entry => entry.summary.TileTypes == maxPotentialOuts
                && entry.summary.RemainingCopies == maxPotentialCopies)
            .Select(entry => entry.index)
            .ToHashSet();

        bestIndices.Count.Should().BeLessThan(handTiles.Count);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        bestIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithHonorSparseHand_PreservesPotentialOuts_WhenHonorDiscardWouldEliminateOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().BeGreaterThan(0);

        var maxPotentialCopies = potentialOutSummaries
            .Where(summary => summary.TileTypes == maxPotentialOuts)
            .Max(summary => summary.RemainingCopies);

        var bestIndices = potentialOutSummaries
            .Select((summary, index) => new { summary, index })
            .Where(entry => entry.summary.TileTypes == maxPotentialOuts
                && entry.summary.RemainingCopies == maxPotentialCopies)
            .Select(entry => entry.index)
            .ToHashSet();

        var honorIndex = handTiles.FindIndex(tile => tile.Equals(new Tile(TileSuit.Winds, 0, Wind.East)));
        honorIndex.Should().BeGreaterThanOrEqualTo(0);
        bestIndices.Should().NotContain(honorIndex);

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        bestIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithDeadHonorSingleton_UsesMaxPotentialOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().BeGreaterThan(0);

        var maxPotentialCopies = potentialOutSummaries
            .Where(summary => summary.TileTypes == maxPotentialOuts)
            .Max(summary => summary.RemainingCopies);

        var bestIndices = potentialOutSummaries
            .Select((summary, index) => new { summary, index })
            .Where(entry => entry.summary.TileTypes == maxPotentialOuts
                && entry.summary.RemainingCopies == maxPotentialCopies)
            .Select(entry => entry.index)
            .ToHashSet();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        bestIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithHonorSparseHand_PrefersHonorSingletons_WhenFourHonorsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.North)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().Be(0);

        var honorIndices = handTiles
            .Select((tile, index) => new { tile, index })
            .Where(entry => entry.tile.IsHonor)
            .Select(entry => entry.index)
            .ToHashSet();

        honorIndices.Should().NotBeEmpty();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        honorIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithHonorSparseHand_PrefersHonorSingletons_WhenFiveHonorsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().Be(0);

        var honorIndices = handTiles
            .Select((tile, index) => new { tile, index })
            .Where(entry => entry.tile.IsHonor)
            .Select(entry => entry.index)
            .ToHashSet();

        honorIndices.Should().NotBeEmpty();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        honorIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithHonorSparseHand_PrefersHonorSingletons_WhenSixHonorsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 9)
        };

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        winningOutSummaries.Max(summary => summary.TileTypes).Should().Be(0);

        var potentialOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: false))
            .ToList();

        var maxPotentialOuts = potentialOutSummaries.Max(summary => summary.TileTypes);
        maxPotentialOuts.Should().Be(0);

        var honorIndices = handTiles
            .Select((tile, index) => new { tile, index })
            .Where(entry => entry.tile.IsHonor)
            .Select(entry => entry.index)
            .ToHashSet();

        honorIndices.Should().NotBeEmpty();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        honorIndices.Should().Contain(discardIndex);
    }

    [Fact]
    public void SelectDiscardIndex_WithHonorSparseHand_PreservesOrphanPath_WhenNoNonOrphansPresent()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        var duplicateOrphanTiles = new HashSet<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1)
        };

        duplicateOrphanTiles.Should().Contain(handTiles[discardIndex]);
    }

    [Fact]
    public void SelectDiscardIndex_PrefersNonOrphanTriplet_WhenNearThirteenOrphans_WithNoVisibility()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_AvoidsBreakingSolePair_WhenTerminalHonorLeanPreservesWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, Wind.West)
        };

        var meldTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, meldTiles, MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.West));
    }

    [Fact]
    public void SelectDiscardIndex_PreservesNearDeadSoleNonTerminalPair_WhenTerminalHonorLean()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_AvoidsOpponentKongThreat_WhenAlternateDiscardExists()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South)
        };

        var baselineIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);
        handTiles[baselineIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            visibleMeldTiles,
            visibleMeldTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Winds, 0, Wind.South));
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotAvoidKongThreat_WhenOnlySafeOptionBreaksSoleChangshaPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        var soleSmallWinPair = new Tile(TileSuit.Dots, 2);
        var threatenedTileTypes = handTiles
            .Distinct()
            .Where(tile => !tile.Equals(soleSmallWinPair))
            .ToList();
        var kongThreatTiles = threatenedTileTypes
            .SelectMany(tile => Enumerable.Repeat(tile, 3))
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(soleSmallWinPair);
        threatenedTileTypes.Should().Contain(handTiles[discardIndex]);
    }

    [Fact]
    public void SelectDiscardIndex_DoesNotAvoidKongThreat_WhenWinningOutCopiesDropSignificantly()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        var visibleTiles = new List<Tile>();
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 9), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 1), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Bamboos, 9), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.East), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.South), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.West), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Winds, 0, Wind.North), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.Red), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.Green), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dragons, 0, null, Dragon.White), 3));

        var baselineIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            MahjongRuleset.ChineseOfficial);
        var baselineTile = handTiles[baselineIndex];

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        var bestOverallOuts = winningOutSummaries.Max(summary => summary.TileTypes);
        var bestOverallOutCopies = winningOutSummaries
            .Where(summary => summary.TileTypes == bestOverallOuts)
            .Max(summary => summary.RemainingCopies);

        var nonThreatSummaries = handTiles
            .Select((tile, index) => new { tile, summary = winningOutSummaries[index] })
            .Where(entry => !entry.tile.Equals(baselineTile))
            .ToList();

        nonThreatSummaries.Should().NotBeEmpty();

        var bestNonThreatOuts = nonThreatSummaries.Max(entry => entry.summary.TileTypes);
        var bestNonThreatOutCopies = nonThreatSummaries
            .Where(entry => entry.summary.TileTypes == bestNonThreatOuts)
            .Max(entry => entry.summary.RemainingCopies);

        var withinOutsTolerance = bestNonThreatOuts >= bestOverallOuts - 2;
        var withinCopyTolerance = bestNonThreatOutCopies >= bestOverallOutCopies - 2;

        withinOutsTolerance.Should().BeTrue();
        withinCopyTolerance.Should().BeFalse($"bestOverallOutCopies={bestOverallOutCopies}, bestNonThreatOutCopies={bestNonThreatOutCopies}, baseline={baselineTile}");

        var kongThreatTiles = Enumerable
            .Repeat(baselineTile, 3)
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(baselineTile);
    }

    [Fact]
    public void SelectDiscardIndex_WithKongThreatTiles_AvoidsThreatEvenWithoutVisibleTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 5)
        };

        var kongThreatTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithKongThreatTiles_DoesNotDiscardSoleChangshaSmallWinSingletonToAvoidThreat()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9)
        };

        var kongThreatTiles = handTiles
            .Where(tile => !tile.Equals(new Tile(TileSuit.Dots, 2)))
            .SelectMany(tile => Enumerable.Repeat(tile, 3))
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithKongThreatTiles_DoesNotForceDiscardOfSoleLiveChangshaSmallWinSingleton_WhenOutsRemain()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2)
        };

        var visibleTiles = new List<Tile>();
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 2), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 5), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Characters, 8), 3));
        visibleTiles.AddRange(Enumerable.Repeat(new Tile(TileSuit.Dots, 2), 2));

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.Changsha258,
                requireMinimumFan: true))
            .ToList();

        var bestOverallOuts = winningOutSummaries.Max(summary => summary.TileTypes);
        bestOverallOuts.Should().BeGreaterThan(0);

        var bestOverallOutCopies = winningOutSummaries
            .Where(summary => summary.TileTypes == bestOverallOuts)
            .Max(summary => summary.RemainingCopies);

        var smallWinTile = new Tile(TileSuit.Dots, 2);
        var smallWinIndex = handTiles.FindIndex(tile => tile.Equals(smallWinTile));
        smallWinIndex.Should().BeGreaterThanOrEqualTo(0);

        var nonThreatSummary = winningOutSummaries[smallWinIndex];
        var withinOutsTolerance = nonThreatSummary.TileTypes >= bestOverallOuts - 2;
        var withinCopyTolerance = nonThreatSummary.RemainingCopies >= bestOverallOutCopies - 2;
        withinOutsTolerance.Should().BeTrue();
        withinCopyTolerance.Should().BeTrue();

        var kongThreatTiles = handTiles
            .Where(tile => !tile.Equals(smallWinTile))
            .SelectMany(tile => Enumerable.Repeat(tile, 3))
            .ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.Changsha258);

        handTiles[discardIndex].Should().NotBe(smallWinTile);
    }

    [Fact]
    public void SelectDiscardIndex_WithKongThreatsAndNoPotentialOuts_DoesNotForceMultiCopyThreatDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var kongThreatTiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1), new(TileSuit.Flowers, 1), new(TileSuit.Flowers, 1),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8), new(TileSuit.Characters, 8)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            Array.Empty<Tile>(),
            kongThreatTiles,
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Flowers, 1));
    }

    [Fact]
    public void SelectDiscardIndex_PrioritizesHigherWinningOuts_WhenTenpai()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(handTiles, MahjongRuleset.ChineseOfficial);

        var visibleCounts = new Dictionary<Tile, int>();
        var winningOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                handTiles,
                visibleCounts,
                index,
                MahjongRuleset.ChineseOfficial,
                requireMinimumFan: true))
            .ToList();

        var bestOuts = winningOutSummaries.Max(summary => summary.TileTypes);

        winningOutSummaries[discardIndex].TileTypes.Should().Be(bestOuts);
        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 2));
    }

    private static List<Tile> BuildExhaustedVisibleTiles()
    {
        var tiles = new List<Tile>();

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                for (var i = 0; i < 4; i++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
        }

        foreach (var wind in Enum.GetValues<Wind>())
        {
            for (var i = 0; i < 4; i++)
            {
                tiles.Add(new Tile(TileSuit.Winds, 0, wind));
            }
        }

        foreach (var dragon in Enum.GetValues<Dragon>())
        {
            for (var i = 0; i < 4; i++)
            {
                tiles.Add(new Tile(TileSuit.Dragons, 0, null, dragon));
            }
        }

        return tiles;
    }

    private static WinningDrawOutSummary CountWinningDrawOutsAfterDiscard(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        int discardIndex,
        MahjongRuleset ruleset,
        bool requireMinimumFan)
    {
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var discardTile = handTiles[discardIndex];
        var reducedHand = handTiles
            .Where((_, index) => index != discardIndex)
            .ToList();
        var ownedTiles = reducedHand.ToList();
        var ownedCounts = ownedTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningDrawOuts = 0;
        var winningDrawOutCopies = 0;

        foreach (var drawTileType in drawTileTypes)
        {
            ownedCounts.TryGetValue(drawTileType, out var drawTileCount);
            visibleCounts.TryGetValue(drawTileType, out var visibleCount);
            if (drawTileType.Equals(discardTile))
            {
                visibleCount++;
            }

            var remainingCopies = 4 - drawTileCount - visibleCount;
            if (remainingCopies <= 0)
            {
                continue;
            }

            var candidateHand = ownedTiles
                .Append(drawTileType)
                .ToList();

            WinningHand? winningHand;
            if (!requireMinimumFan)
            {
                if (ruleset == MahjongRuleset.Changsha258)
                {
                    if (!Changsha258Ruleset.TryGetWinningHand(candidateHand, out winningHand))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!HandAnalyzer.TryGetWinningHand(candidateHand, out winningHand))
                    {
                        continue;
                    }
                }
            }
            else
            {
                if (!RulesetHandAnalyzer.TryGetWinningHand(candidateHand, ruleset, out winningHand))
                {
                    continue;
                }
            }

            if (winningHand is null)
            {
                continue;
            }

            if (requireMinimumFan && !ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset))
            {
                continue;
            }

            winningDrawOuts++;
            winningDrawOutCopies += remainingCopies;
        }

        return new WinningDrawOutSummary(winningDrawOuts, winningDrawOutCopies);
    }

    private static IReadOnlyList<Tile> BuildDrawTileTypes(MahjongRuleset ruleset)
    {
        var suitedTiles = new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos }
            .SelectMany(static suit => Enumerable.Range(1, 9).Select(rank => new Tile(suit, rank)));

        if (ruleset == MahjongRuleset.Changsha258)
        {
            return suitedTiles.ToArray();
        }

        var windTiles = Enum
            .GetValues<Wind>()
            .Select(wind => new Tile(TileSuit.Winds, 0, wind));
        var dragonTiles = Enum
            .GetValues<Dragon>()
            .Select(dragon => new Tile(TileSuit.Dragons, 0, null, dragon));

        return suitedTiles
            .Concat(windTiles)
            .Concat(dragonTiles)
            .ToArray();
    }

    private readonly record struct WinningDrawOutSummary(int TileTypes, int RemainingCopies);
}

