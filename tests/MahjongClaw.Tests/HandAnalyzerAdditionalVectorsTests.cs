using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

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
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithTwoChowsAndTwoPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Chows: 123 bamboos, 456 bamboos
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            // Pungs: 9 dots, 2 characters
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairAndTerminalPungWithThreeChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: east winds
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Pung: 1 dots (terminal)
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Chows: 123 bamboos, 456 bamboos, 789 characters
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairUnlocksDuplicateChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Pung: 9 dots
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Chows: 123 bamboos (twice), 456 bamboos
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenOverlappingHighRankChowsLeadToTerminalPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Chow: 567 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            // Chow: 678 dots
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            // Chow: 789 dots
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Pung: 9 dots (terminal)
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(1);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenMultiplePairChoicesExistButOnlyOneCompletesMelds()
    {
        var tiles = new List<Tile>
        {
            // Pung: 1 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Chows: 234 dots (x2)
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            // Chow: 567 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            // Pair: 9 dots
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 9));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(1);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenQuadSuppliesPungAndChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Pung: 4 dots (uses three of four copies)
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            // Chow: 234 dots (uses fourth 4)
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            // Chow: 567 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            // Chow: 123 bamboos
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenQuadProvidesPairAndOverlappingChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots (uses two of four copies)
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chow: 123 dots (uses one of the remaining 2 dots)
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow: 234 dots (uses the final 2 dot)
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            // Chow: 345 dots
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            // Chow: 678 dots
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(0);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 2));
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenQuadLeavesOnlyPairs()
    {
        var tiles = new List<Tile>
        {
            // Quad: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chows: 345 dots, 678 dots
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            // Pairs: 1 characters, 9 bamboos
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenTwoTripletsAndFourPairsRemain()
    {
        var tiles = new List<Tile>
        {
            // Triplets: 1 dots, 2 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Pairs: 3, 4, 5, 6 dots
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 3));
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
    public void HandAnalyzer_RejectsHand_WhenHonorTripletUsesMixedTypes()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chows: 123 dots, 456 dots, 789 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Honors cannot form a mixed triplet
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
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
    public void HandAnalyzer_AcceptsStandardHand_WhenAllChowsAndTerminalPairAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 dots (terminal)
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Chows: 123 dots, 456 dots, 789 dots, 123 bamboos
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 1));
        hand.BasicScore.Should().Be(0);
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

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenTwoChowsLeaveOnlyPairs()
    {
        var tiles = new List<Tile>
        {
            // Chows: 123 dots, 456 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Pairs: 2 bamboos, 5 bamboos, 8 characters, red dragons
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 8), new(TileSuit.Characters, 8),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairIsRequiredForDuplicateChows()
    {
        var tiles = new List<Tile>
        {
            // Unsorted honor-pair hand: white dragon pair with four chows in dots (123 x2, 456, 789).
            new(TileSuit.Dots, 5),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorPungsLeaveSuitedSingletons()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Honor pungs: east, south, west winds
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            // Singles that cannot form a chow or pung
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithTerminalPungAndMixedChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragons
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Pung: 9 characters (terminal)
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Chow: 123 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow: 456 dots
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Chow: 789 bamboos
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorPairLeavesHonorSingletons()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Chow: 123 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow: 456 dots
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Pung: 7 bamboos
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            // Honor singletons that cannot form a meld
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenSuitedChowSkipsRank()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 dots
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Chows: 123 dots, 456 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Pung: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Invalid suited "chow": 1-3-4 bamboos (gap)
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorAndTerminalPungsMixWithTwoChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 bamboos
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            // Pung: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Pung: 9 dots (terminal)
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Chow: 123 characters
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            // Chow: 456 dots
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenSuitedChowWrapsAcrossTerminal()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Pung: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chow: 123 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow: 456 dots
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Invalid suited "chow": 8-9-1 dots (wrap-around)
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9), new(TileSuit.Dots, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPungsSupportTerminalPairWithChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters (terminal)
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            // Pungs: east winds, red dragons
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chows: 234 bamboos, 789 dots
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_SelectsPairThatAllowsCompleteChowSet()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Chows: 123 dots, 234 dots, 678 bamboos, 789 characters
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void HandAnalyzer_SelectsOnlyViablePair_WhenMultiplePairCandidatesExist()
    {
        var tiles = new List<Tile>
        {
            // Pair candidates: 1/2/3/5 dots, but only 5 dots yields complete melds.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void HandAnalyzer_PrefersHonorPair_WhenSuitedPairBreaksChows()
    {
        var tiles = new List<Tile>
        {
            // Honor pair: green dragons.
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Pung: 9 bamboos.
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            // Chows: 123 dots, 234 dots, 345 dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Green));
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenUnsortedHonorPairMixesChowsAndPung()
    {
        var tiles = new List<Tile>
        {
            // Unsorted mix: white dragon pair, dot pung, and three chows across suits.
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPungWithThreeChowsAndSuitedPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 characters
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            // Honor pung: green dragons
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Chows: 123 dots, 456 dots, 789 bamboos
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithDuplicateChowAndTwoPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragons
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Pungs: 1 dots, 9 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Chows: 123 characters (x2)
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesSevenPairs_WhenPungHeavyStandardAlsoExists()
    {
        var tiles = new List<Tile>
        {
            // Seven pairs candidate across 6/7/8/9 dots still allows a pung-heavy standard hand.
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenFourPungsLeaveNoPair()
    {
        var tiles = new List<Tile>
        {
            // Pungs: 1/2/3/4 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            // Singles: 5 dots, 7 dots (no pair)
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeFalse();
        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
        hand.Should().BeNull();
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithTerminalChowsAndTwoPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Pungs: 1 dots, 9 bamboos (terminals)
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            // Chows: 123 characters, 789 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithThreePungsAndOneChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Pungs: 1 dots, 9 dots, 5 bamboos
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            // Chow: 456 characters
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithDuplicateChowsAndHonorPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Pung: green dragons
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Chows: 234 dots (x2)
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            // Chow: 789 characters
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.White));
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenHonorPairWithTwoHonorPungsAndTwoChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Pungs: green dragons, east winds
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Chows: 123 dots, 789 characters
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }
}

