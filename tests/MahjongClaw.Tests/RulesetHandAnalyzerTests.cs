using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class RulesetHandAnalyzerTests
{
    [Fact]
    public void TryGetWinningHand_Throws_WhenTilesNull()
    {
        var act = () => RulesetHandAnalyzer.TryGetWinningHand(null!, MahjongRuleset.Changsha258, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryGetWinningHand_Throws_WhenTilesContainNull()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            null!
        };

        var act = () => RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_ForChangsha258SmallWinPair()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
        hand.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_ForChangsha258WithKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Melds.Should().Contain(meld => meld.Type == MeldType.Kong);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
        hand.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChangsha258WhenExtraTileDoesNotFormKong()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 6)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChangsha258WhenPairIsNotTwoFiveOrEight()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChangsha258WhenHonorOrBonusTilesPresent()
    {
        var honorTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2)
        };

        RulesetHandAnalyzer.TryGetWinningHand(honorTiles, MahjongRuleset.Changsha258, out var honorHand)
            .Should().BeFalse();
        honorHand.Should().BeNull();

        var bonusTiles = new List<Tile>
        {
            new(TileSuit.Flowers, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2)
        };

        RulesetHandAnalyzer.TryGetWinningHand(bonusTiles, MahjongRuleset.Changsha258, out var bonusHand)
            .Should().BeFalse();
        bonusHand.Should().BeNull();

        var seasonTiles = new List<Tile>
        {
            new(TileSuit.Seasons, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2)
        };

        RulesetHandAnalyzer.TryGetWinningHand(seasonTiles, MahjongRuleset.Changsha258, out var seasonHand)
            .Should().BeFalse();
        seasonHand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChangsha258SevenPairsShape()
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

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_ForChineseOfficialSevenPairs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.ChineseOfficial, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_ForChineseOfficialAllHonors()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.ChineseOfficial, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChineseOfficialWhenMinimumFanMissing()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.ChineseOfficial, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsFalse_ForChineseOfficialAllPungsWhenMinimumFanMissing()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Pungs
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.ChineseOfficial, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void TryGetWinningHand_ReturnsTrue_ForChangsha258WithoutMinimumFanGate()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        RulesetHandAnalyzer.TryGetWinningHand(tiles, MahjongRuleset.Changsha258, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pair.Should().Be(new Tile(TileSuit.Bamboos, 5));
        hand.Pattern.Should().Be(WinningPattern.Standard);
    }
}
