using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerAdditionalVectorsTests
{
    [Fact]
    public void HandAnalyzer_RejectsHand_WhenTripletsLeaveUnpairedSingletons()
    {
        var tiles = new List<Tile>
        {
            // Triplets of 1/2/3 dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            // Remaining singles cannot form the last two melds + pair.
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        hand.Should().BeNull();
    }
}
