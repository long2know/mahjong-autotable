using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class AllTerminalsAndHonorsMixedPungsTests
{
    [Fact]
    public void HandAnalyzer_AssignsAllTerminalsAndHonors_WhenDragonPairWithMixedTerminalPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragons.
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Pungs: 1 characters, 9 characters, 1 bamboos, 9 dots.
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }
}
