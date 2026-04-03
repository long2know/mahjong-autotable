using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class Changsha258RulesetPatternTests
{
    [Fact]
    public void TryGetWinningHand_ReturnsStandardPattern_ForAllPungsShape()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Melds.Should().HaveCount(4);
        hand.Melds.All(meld => meld.Type == MeldType.Pung).Should().BeTrue();
    }
}
