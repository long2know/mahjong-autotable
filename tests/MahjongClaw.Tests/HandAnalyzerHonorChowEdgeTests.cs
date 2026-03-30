using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

namespace MahjongClaw.Tests;

public class HandAnalyzerHonorChowEdgeTests
{
    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorPairsAndChowsLeaveNoFourthMeld()
    {
        var tiles = new List<Tile>
        {
            // Chows: 123 dots, 456 dots, 789 dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Honor pairs that cannot both be the pair.
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Extra singleton blocks any valid fourth meld.
            new(TileSuit.Characters, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_UsesChowsOverPung_WhenHonorPairPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons.
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Chows: 123, 234, 345, 678 dots (three 3-dots tempt a pung).
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(0);
        hand.Melds.Count(meld => meld.Type == MeldType.Chow).Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_SelectsHonorPair_WhenSuitedPairLeavesHonorSingletons()
    {
        var tiles = new List<Tile>
        {
            // Pair candidate: east winds.
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Pung: south winds.
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            // Triplet: 2 dots (could tempt the pair choice).
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chows: 345 dots, 789 bamboos.
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(2);
        hand.BasicScore.Should().Be(4);
    }
}
