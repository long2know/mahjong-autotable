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

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenPureSuitHandMissingMiddleRanks()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Chows: 123 dots (x2), 234 dots, 789 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairAndMixedChowsWithPungAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: north winds
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            // Pung: 4 bamboos
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            // Chow: 123 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow: 456 characters
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            // Chow: 789 dots
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorPungsAndChowsLeaveNoPair()
    {
        var tiles = new List<Tile>
        {
            // Pungs: east winds, red dragons
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chows: 123 dots, 456 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Singles: 7 dots, 8 dots (no pair)
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorIsMixedIntoSuitedChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 dots
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Chows: 123 dots, 456 dots, 789 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Invalid meld: 1-2 characters with east wind (honors cannot form chows)
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenTripletUsesMixedSuits()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chows: 123 dots, 456 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Pung: 7 characters
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            // Mixed-suit "triplet" cannot form a meld
            new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 8),
            new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenAllChowsLeaveNoPair()
    {
        var tiles = new List<Tile>
        {
            // Chows: 123 dots, 456 dots, 789 dots, 123 bamboos
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            // Singles: 4 bamboos, 6 bamboos (no pair)
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenPairsAndSingletonRemainAfterChows()
    {
        var tiles = new List<Tile>
        {
            // Chows: 123 dots, 456 dots, 789 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Two pairs + singleton in bamboos (cannot complete remaining meld + pair)
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }
}
