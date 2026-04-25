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
}
