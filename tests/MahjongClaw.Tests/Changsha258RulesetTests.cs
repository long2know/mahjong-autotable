using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

namespace MahjongClaw.Tests;

public class Changsha258RulesetTests
{
    [Theory]
    [InlineData(TileSuit.Characters, 2)]
    [InlineData(TileSuit.Dots, 5)]
    [InlineData(TileSuit.Bamboos, 8)]
    public void IsSmallWinPair_ReturnsTrue_ForSuitedTwoFiveEight(TileSuit suit, int rank)
    {
        Changsha258Ruleset.IsSmallWinPair(new Tile(suit, rank)).Should().BeTrue();
    }

    [Theory]
    [InlineData(TileSuit.Characters, 1)]
    [InlineData(TileSuit.Dots, 4)]
    [InlineData(TileSuit.Bamboos, 9)]
    public void IsSmallWinPair_ReturnsFalse_ForNon258SuitedTiles(TileSuit suit, int rank)
    {
        Changsha258Ruleset.IsSmallWinPair(new Tile(suit, rank)).Should().BeFalse();
    }

    [Fact]
    public void IsSmallWinPair_ReturnsFalse_ForHonorOrBonusTiles()
    {
        Changsha258Ruleset.IsSmallWinPair(new Tile(TileSuit.Winds, 0, Wind.East)).Should().BeFalse();
        Changsha258Ruleset.IsSmallWinPair(new Tile(TileSuit.Dragons, 0, null, Dragon.Red)).Should().BeFalse();
        Changsha258Ruleset.IsSmallWinPair(new Tile(TileSuit.Flowers, 1)).Should().BeFalse();
        Changsha258Ruleset.IsSmallWinPair(new Tile(TileSuit.Seasons, 1)).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenSmallWinPairIsTwoFiveOrEight()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Theory]
    [InlineData(TileSuit.Bamboos, 5)]
    [InlineData(TileSuit.Dots, 8)]
    public void TryGetWinningHand_ReturnsTrue_WhenSmallWinPairIsValidAcrossSuits(TileSuit suit, int rank)
    {
        var tiles = CreateWinningHandWithPair(suit, rank);

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(suit, rank));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenHandIncludesKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Should().Contain(meld => meld.Type == MeldType.Kong);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenHandHasTwoKongs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(2);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenHandHasThreeKongs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(3);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenPairIsNotTwoFiveOrEight()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenOnlyNon258PairCompletesHand()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHandHasTooFewTiles()
    {
        var tiles = CreateBaselineMelds();
        tiles.Add(new Tile(TileSuit.Dots, 2));

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHandHasTooManyTiles()
    {
        var tiles = CreateBaselineWinningHand();
        tiles.AddRange(new[]
        {
            new Tile(TileSuit.Bamboos, 1),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 3),
            new Tile(TileSuit.Bamboos, 4),
            new Tile(TileSuit.Bamboos, 5)
        });

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenNoPairExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHonorTilesArePresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenBonusTilesArePresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Flowers, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenSeasonTilesArePresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Seasons, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenSevenPairsShapeAppears()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenQuadSplitsAcrossMeldsWithoutExtraTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 8));
        hand.Melds.Should().HaveCount(4);
        hand.Melds.Should().Contain(meld => meld.Type == MeldType.Pung
            && meld.Tiles.All(tile => tile == new Tile(TileSuit.Bamboos, 2)));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenSevenPairsShapeHasStandard258PairDecomposition()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 8));
    }

    [Fact]
    public void TryGetWinningHand_ChoosesAlternateSmallWinPair_WhenFirstCandidateFails()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void TryGetWinningHand_ChoosesAlternateSmallWinPair_WhenFirstCandidateConsumesRequiredKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(1);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenHandHasFourKongsAndSmallWinPair()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Melds.Should().HaveCount(4);
        hand.Melds.Should().OnlyContain(meld => meld.Type == MeldType.Kong);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileAppearsMoreThanFourTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_Throws_WhenTilesContainNull()
    {
        var tiles = CreateBaselineWinningHand();
        tiles.Add(null!);

        var act = () => Changsha258Ruleset.TryGetWinningHand(tiles, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TryGetWinningHand_ReturnsFalse_WhenExtraTilesLackRequiredKongs(int extraTiles)
    {
        var tiles = CreateBaselineWinningHand();

        for (var i = 0; i < extraTiles; i++)
        {
            tiles.Add(new Tile(TileSuit.Dots, 9 - i));
        }

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenExtraTilesExceedAvailableKongs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHandOnlyFormsThreeMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHandFormsFiveMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    private static List<Tile> CreateBaselineWinningHand()
        => CreateWinningHandWithPair(TileSuit.Dots, 2);

    private static List<Tile> CreateWinningHandWithPair(TileSuit suit, int rank)
    {
        var tiles = CreateBaselineMelds();
        tiles.Add(new Tile(suit, rank));
        tiles.Add(new Tile(suit, rank));
        return tiles;
    }

    private static List<Tile> CreateBaselineMelds()
        => new()
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4)
        };
}

