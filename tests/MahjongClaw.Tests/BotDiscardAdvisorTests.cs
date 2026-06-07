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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsFeedingOpponentKong_WhenMultiplePungsPresent()
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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsDeadKongFeed_WhenOutsTie()
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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_PrefersNonFeedWhenWinningOutsAreClose()
    {
        var handTiles = new List<Tile>
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

        var visibleMeldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        var visibleTiles = visibleMeldTiles.ToList();

        var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().Be(new Tile(TileSuit.Characters, 2));
    }

    [Fact]
    public void SelectDiscardIndex_WithVisibleMeldTiles_AvoidsDeadKongFeedTiles()
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
            MahjongRuleset.ChineseOfficial);

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Dots, 9));
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
    public void SelectDiscardIndex_AvoidsLastCopyWhenOpponentHasPromotedKongThreat()
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

        handTiles[discardIndex].Should().NotBe(new Tile(TileSuit.Bamboos, 2));
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
}

