using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class BotMeldAdvisorTests
{
    [Fact]
    public void TrySelectConcealedKong_ThrowsForNullHand()
    {
        Action act = () => BotMeldAdvisor.TrySelectConcealedKong(null!, MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySelectConcealedKong_ThrowsForEmptyHand()
    {
        var tiles = new List<Tile>();

        Action act = () => BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Hand cannot be empty.*");
    }

    [Fact]
    public void TrySelectConcealedKong_ThrowsWhenTilesContainNull()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            null!
        };

        Action act = () => BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void TrySelectConcealedKong_ReturnsFalse_WhenNoKongAvailable()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.ChineseOfficial, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_PrioritizesHonorKong_ForChineseOfficial()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.ChineseOfficial, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsInvalidKong_ForChangsha258()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Characters, 3));
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsSoleSmallWinPair_ForChangsha258()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_AllowsSmallWinPair_WhenAlternateSmallWinSingletonIsLive()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectConcealedKong_AllowsSmallWinPair_WhenSingleAlternateSingletonHasTwoLiveCopies()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(
            tiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsSmallWinPair_WhenAlternateSingletonsAreNearDead()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(
            tiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.Changsha258,
            out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_AllowsSmallWinPair_WhenChangshaBigWinEyeFlexible()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectConcealedKong_PrefersNonSmallWinPair_ForChangsha258()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void TrySelectConcealedKong_ReturnsFalse_WhenChangshaKongLeavesNoPairOrChowWithNoOuts()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 5), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 6)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsSevenPairsCandidate_ForChineseOfficial()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 7)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.ChineseOfficial, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsSevenPairsCandidate_ForChangsha258()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 9)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(tiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_AllowsSevenPairsKong_WhenSingletonOutsAreDead()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(
            tiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial,
            out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectPromotedKong_ThrowsForNullHand()
    {
        Action act = () => BotMeldAdvisor.TrySelectPromotedKong(null!, Array.Empty<Tile>(), MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySelectPromotedKong_ThrowsForNullMelds()
    {
        var hand = new List<Tile> { new(TileSuit.Characters, 1) };

        Action act = () => BotMeldAdvisor.TrySelectPromotedKong(hand, null!, MahjongRuleset.ChineseOfficial, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySelectPromotedKong_ReturnsFalse_WhenNoPromotedKongAvailable()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.ChineseOfficial, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectPromotedKong_PrioritizesHonorKong_ForChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.ChineseOfficial, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
    }

    [Fact]
    public void TrySelectPromotedKong_SkipsInvalidKong_ForChangsha258()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Characters, 4));
    }

    [Fact]
    public void TrySelectPromotedKong_SkipsSoleSmallWinPair_ForChangsha258()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectPromotedKong_AllowsSmallWinPair_WhenAlternateSmallWinSingletonIsLive()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectPromotedKong_AllowsSmallWinPair_WhenSingleAlternateSingletonHasTwoLiveCopies()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 9)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(
            handTiles,
            meldTiles,
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectPromotedKong_AllowsSmallWinPair_WhenChangshaBigWinEyeFlexible()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void TrySelectPromotedKong_PrefersNonSmallWinPair_ForChangsha258()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out var kongTile);

        result.Should().BeTrue();
        kongTile.Should().Be(new Tile(TileSuit.Dots, 3));
    }

    [Fact]
    public void TrySelectPromotedKong_ReturnsFalse_WhenChangshaKongLeavesNoPairOrChowWithNoOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 5), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 6)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsKong_WhenItEliminatesAllImmediateWinningOutsInChangsha258()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 6)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(handTiles, MahjongRuleset.Changsha258, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectConcealedKong_SkipsKong_WhenPotentialOutsDropBelowBestInChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3)
        };

        var result = BotMeldAdvisor.TrySelectConcealedKong(
            handTiles,
            Array.Empty<Tile>(),
            visibleTiles,
            MahjongRuleset.ChineseOfficial,
            out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TrySelectPromotedKong_SkipsKong_WhenItEliminatesAllImmediateWinningOutsInChineseOfficial()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        var result = BotMeldAdvisor.TrySelectPromotedKong(handTiles, meldTiles, MahjongRuleset.ChineseOfficial, out _);

        result.Should().BeFalse();
    }
}
