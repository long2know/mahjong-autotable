using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class HandAnalyzerPairSelectionTests
{
    [Fact]
    public void HandAnalyzer_SelectsPairThatAllowsMeldCompletion_WhenEarlierPairsFail()
    {
        var tiles = new List<Tile>
        {
            // Candidate pairs exist for 1s, 2s, and 3s, but only 3s allow completion.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(0);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 3));
    }
}
