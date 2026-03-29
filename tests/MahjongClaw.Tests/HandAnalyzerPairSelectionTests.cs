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

    [Fact]
    public void HandAnalyzer_PrioritizesSevenPairs_WhenAllPairsPresent()
    {
        var tiles = new List<Tile>
        {
            // Seven pairs shape should win even if a standard decomposition is possible.
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_SelectsHonorPair_WhenSuitedPairsBlockChowCompletion()
    {
        var tiles = new List<Tile>
        {
            // Honor pair to free suited tiles for chow completion.
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Chows: 123 dots, 234 dots, 456 dots, 789 dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Winds, 0, Wind.East));
    }

    [Fact]
    public void HandAnalyzer_SelectsSuitedPair_WhenHonorPairWouldLeaveSingletonHonor()
    {
        var tiles = new List<Tile>
        {
            // East pung must remain intact; using East pair would strand a singleton honor.
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Suited pair candidate.
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Chows: 123 dots, 456 characters, 789 bamboos.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void HandAnalyzer_SelectsPairFromQuad_WhenOnlyQuadPairAllowsMeldCompletion()
    {
        var tiles = new List<Tile>
        {
            // Quad of 2 dots; using a different pair blocks chow completion.
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void HandAnalyzer_SelectsLowestOrderedPair_WhenMultipleStandardPairsYieldSameScore()
    {
        var tiles = new List<Tile>
        {
            // Pair candidates: 2 dots or 5 dots, both yielding a standard hand with one pung.
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void HandAnalyzer_SelectsLowestOrderedPair_WhenSevenPairsFound()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.Pair.Should().Be(new Tile(TileSuit.Characters, 1));
    }
}
