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

    [Fact]
    public void SmallWinPairRanks_ContainsOnlyTwoFiveEight()
    {
        Changsha258Ruleset.SmallWinPairRanks.Should().BeEquivalentTo(new[] { 2, 5, 8 });
    }

    [Fact]
    public void SortedSmallWinPairRanks_ReturnsAscendingTwoFiveEight()
    {
        Changsha258Ruleset.SortedSmallWinPairRanks.Should().Equal(new[] { 2, 5, 8 });
    }

    [Fact]
    public void TileLimitConstants_AreConsistentWithMeldCounts()
    {
        Changsha258Ruleset.MinTiles.Should().Be(Changsha258Ruleset.RequiredMelds * 3 + 2);
        Changsha258Ruleset.MaxTiles.Should().Be(Changsha258Ruleset.MinTiles + Changsha258Ruleset.MaxKongs);
    }

    [Theory]
    [InlineData(Changsha258Ruleset.MinTiles, 0)]
    [InlineData(Changsha258Ruleset.MinTiles + 1, 1)]
    [InlineData(Changsha258Ruleset.MinTiles + 2, 2)]
    [InlineData(Changsha258Ruleset.MinTiles + 3, 3)]
    [InlineData(Changsha258Ruleset.MinTiles + 4, 4)]
    public void TryGetRequiredKongCount_ReturnsTrue_ForValidTileCounts(int tileCount, int expectedKongs)
    {
        Changsha258Ruleset.TryGetRequiredKongCount(tileCount, out var requiredKongs).Should().BeTrue();
        requiredKongs.Should().Be(expectedKongs);
    }

    [Theory]
    [InlineData(Changsha258Ruleset.MinTiles - 1)]
    [InlineData(Changsha258Ruleset.MaxTiles + 1)]
    public void TryGetRequiredKongCount_ReturnsFalse_ForOutOfRangeTileCounts(int tileCount)
    {
        Changsha258Ruleset.TryGetRequiredKongCount(tileCount, out var requiredKongs).Should().BeFalse();
        requiredKongs.Should().Be(0);
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
    public void IsSmallWinPair_Throws_WhenTileIsNull()
    {
        var act = () => Changsha258Ruleset.IsSmallWinPair(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(TileSuit.Characters, 1)]
    [InlineData(TileSuit.Dots, 6)]
    [InlineData(TileSuit.Bamboos, 9)]
    public void IsAllowedTile_ReturnsTrue_ForSuitedTiles(TileSuit suit, int rank)
    {
        Changsha258Ruleset.IsAllowedTile(new Tile(suit, rank)).Should().BeTrue();
    }

    [Theory]
    [InlineData(TileSuit.Characters, 0)]
    [InlineData(TileSuit.Dots, 10)]
    [InlineData(TileSuit.Bamboos, 11)]
    public void IsAllowedTile_Throws_ForOutOfRangeSuitedRanks(TileSuit suit, int rank)
    {
        var act = () => Changsha258Ruleset.IsAllowedTile(new Tile(suit, rank));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsAllowedTile_ReturnsFalse_ForHonorOrBonusTiles()
    {
        Changsha258Ruleset.IsAllowedTile(new Tile(TileSuit.Winds, 0, Wind.East)).Should().BeFalse();
        Changsha258Ruleset.IsAllowedTile(new Tile(TileSuit.Dragons, 0, null, Dragon.Red)).Should().BeFalse();
        Changsha258Ruleset.IsAllowedTile(new Tile(TileSuit.Flowers, 1)).Should().BeFalse();
        Changsha258Ruleset.IsAllowedTile(new Tile(TileSuit.Seasons, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsAllowedTile_Throws_WhenTileIsNull()
    {
        var act = () => Changsha258Ruleset.IsAllowedTile(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsBigWinEyeFlexibleCandidate_ReturnsTrue_ForTripletDenseHand()
    {
        var handTiles = new[]
        {
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Bamboos, 4), new Tile(TileSuit.Bamboos, 4), new Tile(TileSuit.Bamboos, 4)
        }.ToList();

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
    }

    [Fact]
    public void IsBigWinEyeFlexibleCandidate_ReturnsFalse_ForPureSuitBelowThreshold()
    {
        var handTiles = Enumerable.Range(1, 9)
            .Select(rank => new Tile(TileSuit.Characters, rank))
            .ToList();

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeFalse();
    }

    [Fact]
    public void IsBigWinEyeFlexibleCandidate_ReturnsTrue_ForPureSuitAtThreshold()
    {
        var handTiles = Enumerable.Range(1, 9)
            .Select(rank => new Tile(TileSuit.Characters, rank))
            .ToList();
        handTiles.Add(new Tile(TileSuit.Characters, 1));

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
    }

    [Fact]
    public void IsBigWinEyeFlexibleCandidate_ReturnsTrue_ForSevenPairsCandidateWithQuad()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6)
        };

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
    }

    [Fact]
    public void TryGetWinningHand_Throws_WhenTilesNull()
    {
        var act = () => Changsha258Ruleset.TryGetWinningHand(null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryGetWinningHand_Throws_WhenTilesContainNull()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            null!
        };

        var act = () => Changsha258Ruleset.TryGetWinningHand(tiles, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenHonorTilePresent()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountExceedsFour()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenBonusTilePresent()
    {
        var tiles = CreateBaselineWinningHand();
        tiles[0] = new Tile(TileSuit.Flowers, 1);

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
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
    public void TryGetWinningHand_UsesAlternateTwoFiveEightPair_WhenFirstCandidateFails()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void TryGetWinningHand_UsesHigherCountSmallWinPair_WhenLowerCountCandidateFails()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenRequiredKongConsumesOnlySmallWinPair()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_PrefersSmallWinPairThatPreservesRequiredKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(1);
        hand.Melds.Should().Contain(meld => meld.Type == MeldType.Kong
            && meld.Tiles.All(tile => tile == new Tile(TileSuit.Dots, 2)));
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
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Melds.Should().Contain(meld => meld.Type == MeldType.Kong);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenHandIncludesMaxKongs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(4);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_UsesAlternateKong_WhenLowestQuadBlocksMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Bamboos, 2));
        hand.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(1);
        hand.Melds.Should().Contain(meld => meld.Type == MeldType.Kong
            && meld.Tiles.All(tile => tile == new Tile(TileSuit.Dots, 5)));
        hand.Melds.Should().NotContain(meld => meld.Type == MeldType.Kong
            && meld.Tiles.All(tile => tile == new Tile(TileSuit.Characters, 1)));
    }

    [Fact]
    public void TryGetWinningHand_AllowsQuadSplitAcrossPungAndChow()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(1);
        hand.Melds.Count(meld => meld.Type == MeldType.Chow).Should().Be(3);
        hand.Melds.Should().NotContain(meld => meld.Type == MeldType.Kong);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_AllowsQuadSplitWhenKongIsRequired()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(1);
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(1);
        hand.Melds.Count(meld => meld.Type == MeldType.Chow).Should().Be(2);
        hand.Pair.Should().Be(new Tile(TileSuit.Bamboos, 8));
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
    public void TryGetWinningHand_ReturnsTrue_WhenHandHasFourKongs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(4);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountRequiresFourKongsButOnlyThreeQuadsExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 9)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountExceedsFourCopies()
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
    public void TryGetWinningHand_ReturnsFalse_WhenKongHandPairIsNotTwoFiveOrEight()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenAllPungsUsesRandomEye()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenPureSuitUsesRandomEye()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 1));
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenOnlySmallWinPairIsLockedInRequiredKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenAlternateSmallWinPairAllowsRequiredKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Count(meld => meld.Type == MeldType.Kong
            && meld.Tiles.All(tile => tile == new Tile(TileSuit.Dots, 2))).Should().Be(1);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenAlternateSmallWinPairAllowsStandardMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void TryGetWinningHand_PrefersSmallWinPairThatPreservesCompleteMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Should().HaveCount(4);
    }

    [Fact]
    public void TryGetWinningHand_FallsBackToAlternateSmallWinPair_WhenKongMeldRequiresIt()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Count(meld => meld.Type == MeldType.Kong).Should().Be(1);
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
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountImpliesKongButNoQuadExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountRequiresTwoKongsButOnlyOneQuadAvailable()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6)
        };

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
    public void TryGetWinningHand_ReturnsFalse_WhenHandHasOnlyThreeMelds()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 7)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
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
    public void TryGetWinningHand_ReturnsTrue_WhenSevenPairsShapeAppears()
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

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
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
    public void TryGetWinningHand_ReturnsSevenPairsPattern_WhenSevenPairsShapeHasStandard258PairDecomposition()
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
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 1));
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
    public void TryGetWinningHand_ReturnsFalse_WhenOnlySmallWinPairConsumesRequiredKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
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
    public void TryGetWinningHand_ReturnsFalse_WhenKongHandHasNon258Pair()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenOnlySmallWinPairIsLockedInRequiredKong_WithHigherBambooChow()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_WhenFourKongsHandHasRandomEye()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 1));
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
    public void TryGetWinningHand_Throws_WhenTilesContainNull_AfterBaselineHand()
    {
        var tiles = CreateBaselineWinningHand();
        tiles.Add(null!);

        var act = () => Changsha258Ruleset.TryGetWinningHand(tiles, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountBelowMinimum()
    {
        var tiles = CreateBaselineWinningHand();
        tiles.RemoveAt(0);

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_WhenTileCountAboveMaximum()
    {
        var tiles = CreateBaselineWinningHand();
        tiles.AddRange(new[]
        {
            new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Dots, 6),
            new Tile(TileSuit.Bamboos, 1),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 3)
        });

        Changsha258Ruleset.TryGetWinningHand(tiles, out _).Should().BeFalse();
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
    public void TryGetWinningHand_ReturnsSevenPairs_WhenThreeKongsAndPairShapeAppears()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
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
