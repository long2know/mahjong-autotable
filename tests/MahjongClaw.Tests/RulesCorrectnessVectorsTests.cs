using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class RulesCorrectnessVectorsTests
{
    [Fact]
    public void HandAnalyzer_AcceptsStandardMixedHandVector()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Melds: 123 chars, 456 chars, 789 dots, pung of red dragons
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.IsWinningHand(tiles).Should().BeTrue();
    }

    [Fact]
    public void HandAnalyzer_ComputesStandardBasicScore_FromPungsInStandardHand()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Melds: 123 chars, 456 bams, pung 7 dots, pung 9 chars
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_PrefersHigherBasicScore_WhenMultipleStandardDecompositionsExist()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Ambiguous shape: 111222333 can be three pungs or three chows
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            // Required chow: 456 chars
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_SelectsValidPair_WhenEarlierCandidateBreaksMeldFormation()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung 1 chars, chow 234 bams, chow 567 bams, chow 789 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dragons, 0, null, Dragon.Red));
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenAnyTileAppearsFiveTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.West)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenWindTileAppearsFiveTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenDragonTileAppearsFiveTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WhenTileCountIsNotFourteen()
    {
        var thirteenTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(thirteenTiles, out _).Should().BeFalse();

        var fifteenTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(fifteenTiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(TileSuit.Flowers, 1)]
    [InlineData(TileSuit.Seasons, 2)]
    public void HandAnalyzer_RejectsHand_WhenFlowerOrSeasonTileIsPresent(TileSuit suit, int rank)
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Melds: 123 chars, 456 chars, 789 dots, plus two red dragons
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(suit, rank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenPairExistsButNoMeldsForm()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Singles that cannot form any chow or pung
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 5), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenMeldsFormButNoPairExists()
    {
        var tiles = new List<Tile>
        {
            // Melds: 123 chars, 456 chars, 789 dots, pung of red dragons
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Singles that fail to form a pair
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorSinglesPreventFinalMeld()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Melds: 123 chars, 456 chars, 789 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            // Honor singles cannot form a chow
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_AcceptsSevenPairsVariant_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesSevenPairs_WhenStandardMeldDecompositionExists()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_AcceptsSevenPairs_WhenQuadCountsAsTwoPairs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_AcceptsSevenPairs_WhenMultipleQuadsAppear()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_AcceptsSevenPairs_WhenThreeQuadsAndSinglePairAppear()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_UsesSevenPairs_WhenSevenPairsIncludesHonorPairAndNonTerminalSuitedTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllHonors_WhenSevenPairsIncludesHonorQuad()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesSevenPairs_WhenHonorQuadAppearsWithMixedNonTerminalPairs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_RejectsSevenPairs_WhenOnlySixPairsAndTwoSingletons()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsSevenPairs_WhenTripletAppearsAlongsideFivePairs()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllHonors_OverSevenPairs_WhenAllPairsAreHonors()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminals_OverSevenPairs_WhenAllPairsAreTerminals()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminals);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsThirteenOrphans_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.ThirteenOrphans);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(TileSuit.Characters, 1)]
    [InlineData(TileSuit.Characters, 9)]
    [InlineData(TileSuit.Dots, 1)]
    [InlineData(TileSuit.Dots, 9)]
    [InlineData(TileSuit.Bamboos, 1)]
    [InlineData(TileSuit.Bamboos, 9)]
    [InlineData(TileSuit.Winds, 0, Wind.East)]
    [InlineData(TileSuit.Winds, 0, Wind.South)]
    [InlineData(TileSuit.Winds, 0, Wind.West)]
    [InlineData(TileSuit.Winds, 0, Wind.North)]
    [InlineData(TileSuit.Dragons, 0, null, Dragon.Red)]
    [InlineData(TileSuit.Dragons, 0, null, Dragon.Green)]
    [InlineData(TileSuit.Dragons, 0, null, Dragon.White)]
    public void HandAnalyzer_AcceptsThirteenOrphans_WhenPairIsAnyRequiredTerminalOrHonor(
        TileSuit pairSuit,
        int pairRank,
        Wind? pairWind = null,
        Dragon? pairDragon = null)
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        tiles.Add(new Tile(pairSuit, pairRank, pairWind, pairDragon));

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.ThirteenOrphans);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllHonors_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllHonors_WhenPairIsWindTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung south, pung west, pung red dragon, pung green dragon
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesAllHonors_WhenDragonPairHasSingleDragonTriplet()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung green dragon, pung east, pung south, pung west
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllHonorsCandidateContainsSuitedTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, chow 123 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_RejectsAllHonorsCandidate_WhenHonorSinglesPreventMelds()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Triplets: east, south, west winds
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            // Honor singles block the final meld
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenAllHonorsCandidateIncludesSuitedPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, pung 5 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenHonorPungsAndSuitedPairIncludeChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung east, pung south, pung west, chow 123 characters
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleFourWinds_WhenPairIsWindTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung south, pung west, pung north, pung red dragon
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleFourWinds);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenWindPairExistsButOnlyTwoWindTriplets()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung south, pung west, chow 123 dots, chow 789 bamboos
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminalsAndHonors_WhenHonorHandIncludesTerminalPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, pung 1 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminalsAndHonors_WhenHonorPairAndOnlyTerminalPungsAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung 1 chars, pung 9 chars, pung 1 dots, pung 9 dots
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenHonorHandIncludesSuitedChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, chow 234 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminals_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair of terminals
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds using only terminal suited tiles
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminals);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminals_WhenPairIsNineDots()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 dots
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            // Melds: pung 1 chars, pung 9 chars, pung 1 bamboos, pung 9 bamboos
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminals);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_RejectsAllTerminalHand_WhenTripletsAreIncomplete()
    {
        var tiles = new List<Tile>
        {
            // Terminal-only tiles with insufficient triplets to complete four melds.
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllTerminalsCandidateContainsMiddleTile()
    {
        var tiles = new List<Tile>
        {
            // Pair of terminals
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung 9 chars, pung 1 dots, chow 345 dots (middle tiles), pung 9 bamboos
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair of terminals
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung 9 chars, pung 1 dots, pung east, pung red dragon
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenTerminalPairAndHonorPungsAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenPairIsHonorTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 1 chars, pung 9 chars, pung 1 dots, pung east wind
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenHonorPairHasMixedTerminalAndHonorPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east wind, pung 1 chars, pung 9 chars, pung 9 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenHonorPairAndOnlyTerminalPungsAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 1 chars, pung 9 chars, pung 1 bamboos, pung 9 dots
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenTerminalPairIsNineBamboosAndDragonPungPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 bamboos
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            // Melds: pung 1 chars, pung 9 chars, pung 1 dots, pung red dragon
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllTerminalsAndHonors_WhenTerminalPairHasWindPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 bamboos
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            // Melds: pung 1 chars, pung 9 chars, pung 1 dots, pung east wind
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllTerminalsAndHonorsCandidateContainsMiddleTile()
    {
        var tiles = new List<Tile>
        {
            // Pair of terminals
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung 9 chars, pung 1 dots, chow 234 dots (middle tiles), pung red dragon
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllTerminalsAndHonorsCandidateHasHonorPairAndMiddleTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 1 chars, pung 9 chars, chow 234 dots (middle tiles), pung east wind
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_RejectsAllTerminalsAndHonorsShape_WhenNoMeldsCanForm()
    {
        var tiles = new List<Tile>
        {
            // Pairs of honors that never reach triplet counts.
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Singles prevent seven-pairs and block any pung-only decomposition.
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllGreen_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung of 6 bamboo, pung of 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllGreen_WhenNoDragonsAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllGreen_WhenGreenDragonPungAppearsWithSuitedPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung green dragon
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void HandAnalyzer_AcceptsAllGreen_WhenPairIsGreenBambooInChowHeavyHand(int pairRank)
    {
        var tiles = new List<Tile>
        {
            // Pair: 3/4 bamboo (green tile)
            new(TileSuit.Bamboos, pairRank), new(TileSuit.Bamboos, pairRank),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllGreen_OverSevenPairs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Seven pairs shape composed only of legal green tiles.
            // 2-bamboo appears four times, which forms two pairs in the current analyzer.
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllGreen_WhenSevenPairsContainsOnlyGreenSuitedTiles()
    {
        var tiles = new List<Tile>
        {
            // Seven pairs composed solely of green-suited bamboo tiles (no dragons).
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_UsesSevenPairs_WhenSevenPairsContainsNonGreenTile()
    {
        var tiles = new List<Tile>
        {
            // Seven pairs, but includes a 5-bamboo pair (non-green), so it should remain Seven Pairs.
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllGreen_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: pung 2 bamboo, pung 3 bamboo, pung 4 bamboo, pung 6 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllGreen_OverAllPungs_WhenPairIsSuitedTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: 8 bamboo
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            // Melds: pung 2 bamboo, pung 3 bamboo, pung 4 bamboo, pung 6 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateHasBambooSevenPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: 7 bamboo (non-green)
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateHasNonGreenPair(int pairRank)
    {
        var tiles = new List<Tile>
        {
            // Pair: non-green bamboo rank
            new(TileSuit.Bamboos, pairRank), new(TileSuit.Bamboos, pairRank),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllGreen_WhenPairIsSuitedGreenTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung green dragon
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllGreen);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_RejectsAllGreenCandidate_WhenNoValidMeldDecompositionExists()
    {
        var tiles = new List<Tile>
        {
            // All green-legal tiles, but no valid meld decomposition (no 3-bamboo tiles for chows).
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsWhiteDragon()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung white dragon
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsRedDragon()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung red dragon
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenAllGreenCandidateContainsWhiteDragonPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: pung 2 bamboo, pung 3 bamboo, pung 4 bamboo, pung white dragon
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenAllGreenCandidateContainsBambooSevenPung()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: pung 2 bamboo, pung 3 bamboo, pung 4 bamboo, pung 7 bamboo (non-green)
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsWindHonor()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung east wind
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateHasHonorPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooFive()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, chow 456 bamboo (includes 5), pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooFiveWithoutDragons()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds: 234 bamboo, 234 bamboo, chow 456 bamboo (includes 5), pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooSevenWithoutDragons()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds: 234 bamboo, 234 bamboo, chow 678 bamboo (includes 7), pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateIncludesOtherSuitGreenRanks()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, chow 234 dots (non-green suit)
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooTerminal(int terminalRank)
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung terminal bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, terminalRank), new(TileSuit.Bamboos, terminalRank), new(TileSuit.Bamboos, terminalRank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsBigThreeDragons_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung red, pung green, pung white dragons, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsBigThreeDragons_WhenPairIsSuitedTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung red, pung green, pung white dragons, chow 123 bamboos
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsBigFourWinds_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung east, pung south, pung west, pung north
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigFourWinds);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsBigFourWinds_WhenPairIsSuitedTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: 7 characters
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 7),
            // Melds: pung east, pung south, pung west, pung north
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigFourWinds);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooSeven()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 678 bamboo (contains non-green 7), pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooOne()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 123 bamboo (contains non-green 1), pung 6 bamboo, pung 8 bamboo
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenAllGreenCandidateContainsBambooNine()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: 234 bamboo, 234 bamboo, pung 6 bamboo, pung 9 bamboo (non-green 9)
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigThreeDragons_OverAllHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung red, pung green, pung white dragons, pung south
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigThreeDragons_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung red, pung green, pung white dragons, pung east wind
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigThreeDragons_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters (terminal)
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung red, pung green, pung white dragons, pung 9 characters
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_AcceptsBigThreeDragons_WhenFourthMeldIsChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung red, pung green, pung white dragons, chow 123 bamboos
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigThreeDragons);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTwoDragonTripletsButPairIsNotDragon_AndNoThirdDragonTriplet()
    {
        var tiles = new List<Tile>
        {
            // Pair: 3 dots
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 3),
            // Melds: pung red dragon, pung green dragon, pung east wind, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonPairHasOnlyOneDragonTriplet()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung east wind, pung 1 characters, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonPairHasNoDragonTriplets()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: chow 123 chars, chow 456 chars, chow 789 dots, pung 5 bamboos
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigFourWinds_OverAllHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung east, pung south, pung west, pung north
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigFourWinds);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigFourWinds_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung east, pung south, pung west, pung north
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigFourWinds);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesBigFourWinds_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters (terminal)
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung east, pung south, pung west, pung north
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.BigFourWinds);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(Dragon.Red)]
    [InlineData(Dragon.Green)]
    [InlineData(Dragon.White)]
    public void HandAnalyzer_AcceptsLittleThreeDragons_WhenPairIsAnyDragon(Dragon pairDragon)
    {
        var tripletDragons = new[] { Dragon.Red, Dragon.Green, Dragon.White }
            .Where(dragon => dragon != pairDragon)
            .ToArray();

        var tiles = new List<Tile>
        {
            // Pair: selected dragon
            new(TileSuit.Dragons, 0, null, pairDragon),
            new(TileSuit.Dragons, 0, null, pairDragon),
            // Melds: two dragon pungs (other colors), pung east, chow 123 dots
            new(TileSuit.Dragons, 0, null, tripletDragons[0]),
            new(TileSuit.Dragons, 0, null, tripletDragons[0]),
            new(TileSuit.Dragons, 0, null, tripletDragons[0]),
            new(TileSuit.Dragons, 0, null, tripletDragons[1]),
            new(TileSuit.Dragons, 0, null, tripletDragons[1]),
            new(TileSuit.Dragons, 0, null, tripletDragons[1]),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTwoDragonTripletsButPairIsNotDragon()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung red dragon, pung green dragon, pung south wind, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsLittleFourWinds_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: north wind
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            // Melds: pung east, pung south, pung west, chow 123 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleFourWinds);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenWindPairHasOnlyTwoWindTriplets()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung south, pung west, pung red dragon, chow 123 dots
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenWindPairHasOnlyTwoWindTripletsAndAllMeldsArePungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung south, pung west, pung 7 dots, pung 5 bamboos
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenThreeWindTripletsExistButPairIsNotWind()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung east, pung south, pung west, chow 123 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenThreeWindTripletsAndDragonTripletWithNonWindPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleFourWinds_OverAllHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: north wind
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleFourWinds);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleFourWinds_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: north wind
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            // Melds: pung east, pung south, pung west, pung 1 characters
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleFourWinds);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenLittleFourWindsPairIsNonWind()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots (non-wind)
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung east, pung south, pung west, chow 123 dots
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsLittleThreeDragons_WhenOnlyTwoDragonTripletsAndDragonPairExist()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung green dragon, pung east, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTwoDragonTripletsExistButPairIsNonDragon()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots (non-dragon)
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung red dragon, pung green dragon, chow 123 characters, chow 456 bamboos
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonPairExistsButOnlyOneDragonTriplet()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung east, pung south, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenDragonPairHasSingleDragonTripletAndAllMeldsArePungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung 1 characters, pung 9 dots, pung 7 bamboos
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllPungs_WhenMixedDecompositionIsAlsoPossible()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Melds can be four pungs or three chows (123) + pung 444.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllPungs_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung 1 chars, pung 9 chars, pung 5 dots, pung red dragon
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllPungs_WhenOnlySuitedPungsPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Melds: pung 1 dots, pung 3 dots, pung 5 dots, pung 7 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AcceptsAllPungs_WhenHonorPairAndMixedSuitedPungsAppear()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 2 dots, pung 3 dots, pung 4 bamboos, pung east wind
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenSuitedPairWithAllHonorPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenChowBreaksAllPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung 1 chars, pung 9 chars, chow 123 bamboos, pung red dragon
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenMiddleTileBreaksTerminalHonorPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung 1 chars, pung 9 bamboos, pung east wind, pung 5 dots (middle tile)
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenTerminalHonorHandHasNonTerminalPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots (non-terminal)
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung 1 chars, pung 9 chars, pung east wind, pung red dragon
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenHonorPairAndNonTerminalPungBreaksTerminalHonorPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 1 chars, pung 9 chars, pung east wind, pung 5 dots (non-terminal)
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenTerminalPairAndNonTerminalPungBreaksTerminalHonorPattern()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            // Melds: pung 9 chars, pung 1 dots, pung east wind, pung 5 bamboos (non-terminal)
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleFourWinds_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: north wind
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Winds, 0, Wind.North),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleFourWinds);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleThreeDragons_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung green dragon, pung east, pung south
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleThreeDragons_OverAllHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung green dragon, pung east, pung south
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesLittleThreeDragons_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: green dragon
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            // Melds: pung red dragon, pung white dragon, pung 1 characters, pung 9 characters
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9),
            new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_AcceptsLittleThreeDragons_WhenPairIsDragonAndChowsPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragon
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung red dragon, pung green dragon, chow 123 dots, chow 789 characters
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.LittleThreeDragons);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonPairHasSingleDragonTripletAndChowPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung green dragon, pung east, pung south, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonPairHasSingleDragonTripletAndOnlyChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragon
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Melds: pung green dragon, chow 123 bamboos, chow 456 bamboos, chow 789 dots
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_UsesAllPungs_WhenDragonTripletsExistButPairIsNotDragon()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 dots
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            // Melds: pung red dragon, pung green dragon, pung east, pung south
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllPungs);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenDragonTripletsExistButPairIsNotDragonAndChowsPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            // Melds: pung red dragon, pung green dragon, chow 123 bamboos, chow 789 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllHonors_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllHonors_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung east, pung south, pung west, pung red dragon
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenHandContainsNonGreenTile()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            // Melds include a non-green tile (5 bamboo)
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenHandContainsNonGreenHonorTile()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons are honors but not allowed in All Green.
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Remaining melds are valid "green" groups.
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTwoDragonTripletsExistWithoutDragonPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind (not a dragon)
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung red dragon, pung green dragon, pung south, chow 123 dots
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_AcceptsNineGates_AsWinningPattern()
    {
        var tiles = new List<Tile>
        {
            // 1112345678999 + 5 of dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.NineGates);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void HandAnalyzer_AcceptsNineGates_WithAnySingleExtraTileInTheSameSuit(int extraRank)
    {
        var tiles = new List<Tile>
        {
            // Nine Gates base: 1112345678999 of dots + one extra dot tile
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, extraRank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.NineGates);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(TileSuit.Characters)]
    [InlineData(TileSuit.Dots)]
    [InlineData(TileSuit.Bamboos)]
    public void HandAnalyzer_AcceptsNineGates_InAnySingleSuitedFamily(TileSuit suit)
    {
        var tiles = new List<Tile>
        {
            // 1112345678999 + 5 in the same suited family.
            new(suit, 1), new(suit, 1), new(suit, 1),
            new(suit, 2),
            new(suit, 3),
            new(suit, 4),
            new(suit, 5), new(suit, 5),
            new(suit, 6),
            new(suit, 7),
            new(suit, 8),
            new(suit, 9), new(suit, 9), new(suit, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.NineGates);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(TileSuit.Characters, 1)]
    [InlineData(TileSuit.Characters, 9)]
    [InlineData(TileSuit.Bamboos, 1)]
    [InlineData(TileSuit.Bamboos, 9)]
    public void HandAnalyzer_AcceptsNineGates_WhenExtraTileIsTerminalInNonDotSuit(TileSuit suit, int extraRank)
    {
        var tiles = new List<Tile>
        {
            // 1112345678999 + terminal extra in the same suited family.
            new(suit, 1), new(suit, 1), new(suit, 1),
            new(suit, 2),
            new(suit, 3),
            new(suit, 4),
            new(suit, 5),
            new(suit, 6),
            new(suit, 7),
            new(suit, 8),
            new(suit, 9), new(suit, 9), new(suit, 9),
            new(suit, extraRank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.NineGates);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(TileSuit.Characters, 2)]
    [InlineData(TileSuit.Characters, 8)]
    [InlineData(TileSuit.Bamboos, 3)]
    [InlineData(TileSuit.Bamboos, 7)]
    public void HandAnalyzer_AcceptsNineGates_WhenExtraTileIsMiddleRankInNonDotSuit(TileSuit suit, int extraRank)
    {
        var tiles = new List<Tile>
        {
            // 1112345678999 + non-terminal extra in the same suited family.
            new(suit, 1), new(suit, 1), new(suit, 1),
            new(suit, 2),
            new(suit, 3),
            new(suit, 4),
            new(suit, 5),
            new(suit, 6),
            new(suit, 7),
            new(suit, 8),
            new(suit, 9), new(suit, 9), new(suit, 9),
            new(suit, extraRank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.NineGates);
        hand.BasicScore.Should().Be(88);
    }

    [Theory]
    [InlineData(TileSuit.Bamboos, 5)]
    [InlineData(TileSuit.Characters, 5)]
    public void HandAnalyzer_RejectsNineGatesShape_WhenExtraSuitedTileIsOffSuit(TileSuit extraSuit, int extraRank)
    {
        var tiles = new List<Tile>
        {
            // Nine Gates base in dots, but extra tile is off-suit, producing no valid winning decomposition.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(extraSuit, extraRank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void HandAnalyzer_RejectsNineGates_WhenRequiredInnerTileIsOffSuit(int displacedRank)
    {
        var tiles = new List<Tile>
        {
            // Start from Nine Gates base in dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Extra in-suit tile keeps 14 tiles total.
            new(TileSuit.Dots, 5)
        };

        // Replace one required inner dot tile with an off-suit tile of the same rank.
        var displaced = tiles.FindIndex(tile => tile.Suit == TileSuit.Dots && tile.Rank == displacedRank);
        displaced.Should().BeGreaterThanOrEqualTo(0);
        tiles[displaced] = new Tile(TileSuit.Bamboos, displacedRank);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(2, Wind.East)]
    [InlineData(7, Wind.North)]
    public void HandAnalyzer_RejectsNineGates_WhenRequiredInnerTileIsReplacedByWindHonor(int displacedRank, Wind honor)
    {
        var tiles = new List<Tile>
        {
            // Start from Nine Gates base in dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Extra in-suit tile keeps 14 tiles total.
            new(TileSuit.Dots, 5)
        };

        // Replace one required inner dot tile with a wind honor tile.
        var displaced = tiles.FindIndex(tile => tile.Suit == TileSuit.Dots && tile.Rank == displacedRank);
        displaced.Should().BeGreaterThanOrEqualTo(0);
        tiles[displaced] = new Tile(TileSuit.Winds, 0, honor);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(3, Dragon.Red)]
    [InlineData(8, Dragon.White)]
    public void HandAnalyzer_RejectsNineGates_WhenRequiredInnerTileIsReplacedByDragonHonor(int displacedRank, Dragon honor)
    {
        var tiles = new List<Tile>
        {
            // Start from Nine Gates base in dots.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Extra in-suit tile keeps 14 tiles total.
            new(TileSuit.Dots, 5)
        };

        // Replace one required inner dot tile with a dragon honor tile.
        var displaced = tiles.FindIndex(tile => tile.Suit == TileSuit.Dots && tile.Rank == displacedRank);
        displaced.Should().BeGreaterThanOrEqualTo(0);
        tiles[displaced] = new Tile(TileSuit.Dragons, 0, null, honor);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void HandAnalyzer_RejectsNineGates_WhenTerminalMultiplicityIsBrokenByOffSuitReplacement(int displacedTerminalRank)
    {
        var tiles = new List<Tile>
        {
            // Start from Nine Gates base in dots: 1112345678999 + extra 5 dot.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 5)
        };

        // Replace one terminal (1 or 9) with an off-suit tile of the same rank.
        var displaced = tiles.FindIndex(tile => tile.Suit == TileSuit.Dots && tile.Rank == displacedTerminalRank);
        displaced.Should().BeGreaterThanOrEqualTo(0);
        tiles[displaced] = new Tile(TileSuit.Characters, displacedTerminalRank);

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(Dragon.Red)]
    [InlineData(Dragon.Green)]
    [InlineData(Dragon.White)]
    public void HandAnalyzer_RejectsNineGatesShape_WhenExtraTileIsDragonHonor(Dragon extraDragon)
    {
        var tiles = new List<Tile>
        {
            // Nine Gates base in dots, but extra tile is an honor tile.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, extraDragon)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(Wind.East)]
    [InlineData(Wind.South)]
    [InlineData(Wind.West)]
    [InlineData(Wind.North)]
    public void HandAnalyzer_RejectsNineGatesShape_WhenExtraTileIsWindHonor(Wind extraWind)
    {
        var tiles = new List<Tile>
        {
            // Nine Gates base in dots, but extra tile is a wind honor tile.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, extraWind)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenNineGatesMissingRequiredRank()
    {
        var tiles = new List<Tile>
        {
            // Nine Gates shape in dots, but the 2 dot is missing; extra 5 dot keeps 14 tiles.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenPureSuitHandMissingOnesForNineGates()
    {
        var tiles = new List<Tile>
        {
            // Pure dots standard hand with no 1 tiles, so Nine Gates should not apply.
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenPureSuitHandMissingNinesForNineGates()
    {
        var tiles = new List<Tile>
        {
            // Pure dots standard hand with no 9 tiles, so Nine Gates should not apply.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenNineGatesTerminalMultiplicityIsNotMetForOnes()
    {
        var tiles = new List<Tile>
        {
            // Pure dots winning hand: 123, 123, 456, 789 + pair 22
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenNineGatesTerminalMultiplicityIsNotMetForNines()
    {
        var tiles = new List<Tile>
        {
            // Pure dots winning hand: 123, 123, 456, 789 + pair 88 (only one 9)
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenPureSuitMeldsIncludeHonorPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: east winds, melds are all dots.
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_ForPureSuitHand_WhenNineGatesTerminalMultiplicityIsNotMet()
    {
        var tiles = new List<Tile>
        {
            // Pure dots winning hand: 123, 123, 456, 789 + pair 11
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesSevenPairs_OverNineGatesLikePureSuitShapes_WhenRanksAreMissing()
    {
        var tiles = new List<Tile>
        {
            // Pure dots seven pairs, intentionally missing 8/9 to ensure this is not Nine Gates.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllHonors_OverSevenPairs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green), new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminals_OverSevenPairs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminals);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminalsAndHonors_OverSevenPairs_AndDoesNotMisclassifyAsThirteenOrphans()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesSevenPairs_WhenSevenPairsIncludesNonTerminalSuitedPair()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminalsAndHonors_WhenSevenPairsIncludesTerminalQuadAndHonorPairs()
    {
        var tiles = new List<Tile>
        {
            // Terminal quad counts as two pairs.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesSevenPairs_WhenTerminalQuadAppearsWithNonTerminalPairs()
    {
        var tiles = new List<Tile>
        {
            // Terminal quad counts as two pairs, but non-terminal pairs keep this as Seven Pairs.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphans_WhenOneRequiredTerminalOrHonorIsMissing()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            // Missing North wind here.
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Two duplicate pairs but an orphan is missing.
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphans_WhenRequiredTerminalIsMissing()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            // Missing 9 bamboos, replaced by extra orphan duplicates.
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphans_WhenAnyTileAppearsThreeTimes()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphans_WhenQuadOrphanForcesMultipleMissingOrphans()
    {
        var tiles = new List<Tile>
        {
            // Quad of 1 characters replaces multiple orphans.
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            // Missing 9 bamboos and north wind.
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphansShape_WhenMultiplePairsExist()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            // Missing North wind, replaced by an extra dragon pair.
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsThirteenOrphans_WhenAllOrphansExistButPairTileIsNotAnOrphan()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Extra tile is not a terminal/honor, so no valid orphan pair exists.
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_AcceptsThirteenOrphans_WhenPairIsHonorTile()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // Duplicate honor to form the orphan pair.
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.ThirteenOrphans);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesThirteenOrphans_OverAllTerminalsAndHonors_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            // duplicate terminal to form the Thirteen Orphans pair
            new(TileSuit.Characters, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.ThirteenOrphans);
        hand.BasicScore.Should().Be(88);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminals_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            // Melds: pung 9 characters, pung 1 dots, pung 9 dots, pung 1 bamboos
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminals);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesAllTerminalsAndHonors_OverAllPungs_WhenBothApply()
    {
        var tiles = new List<Tile>
        {
            // Pair: east wind
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung 1 characters, pung 9 characters, pung 1 dots, pung red dragon
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.AllTerminalsAndHonors);
        hand.BasicScore.Should().Be(64);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenWinningHandIncludesNonTerminalSuitedTile()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Melds: 123 chars, 456 chars, 789 dots, pung of red dragons
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTerminalHonorHeavyHandIncludesSimpleChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: east winds
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            // Melds: pung 1 chars, pung 9 chars, pung red dragon, chow 234 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_UsesStandardPattern_WhenTerminalHonorHeavyHandIncludesSevenEightChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: white dragons
            new(TileSuit.Dragons, 0, null, Dragon.White), new(TileSuit.Dragons, 0, null, Dragon.White),
            // Melds: pung 1 chars, pung 9 dots, pung east wind, chow 789 bamboos
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WithMoreThanFourIdenticalTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenNoValidMeldDecompositionExists()
    {
        var tiles = new List<Tile>
        {
            // Pair: 2 characters
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            // Remaining tiles avoid any pungs or chows.
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 5), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHand_WhenHonorTilesCannotFormMelds()
    {
        var tiles = new List<Tile>
        {
            // Triplets: east + south winds
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South), new(TileSuit.Winds, 0, Wind.South),
            // Pairs: west wind, north wind, red dragon
            new(TileSuit.Winds, 0, Wind.West), new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North), new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Singletons prevent any valid honor-only meld decomposition.
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsTerminalHonorHand_WhenNoValidMeldDecompositionExists()
    {
        var tiles = new List<Tile>
        {
            // Pair: 1 characters
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            // Triplets: 9 characters, 1 dots
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            // Pairs: east wind, red dragon
            new(TileSuit.Winds, 0, Wind.East), new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Singletons to block any remaining melds.
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_PrefersPungs_WhenMultipleStandardDecompositionsExist()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 characters
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Melds: 111/222/333 dots or 123 dots (x3), plus 789 bamboos
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(3);
        hand.Melds.Count(meld => meld.Type == MeldType.Chow).Should().Be(1);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_SelectsAlternatePair_WhenInitialPairCandidateCannotFormMelds()
    {
        var tiles = new List<Tile>
        {
            // Only the 5 dot pair yields a valid meld decomposition.
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 5));
    }

    [Fact]
    public void HandAnalyzer_SelectsPairThatMaximizesStandardPungScore_WhenMultiplePairsAreValid()
    {
        var tiles = new List<Tile>
        {
            // Pair should be 6 dots to preserve three pungs (7/8/9) plus a 5-6-7 chow.
            // Pairing 9 dots instead yields four chows with zero pungs.
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 6));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(3);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AcceptsStandardHand_WhenQuadSplitsAcrossPungAndChow()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 bamboos
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            // Melds: pung 2 dots, chow 2-3-4 dots, chow 5-6-7 dots, chow 3-4-5 chars
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
    }

    [Fact]
    public void HandAnalyzer_PrioritizesSevenPairs_WhenQuadSplitsAcrossPairAndChows()
    {
        var tiles = new List<Tile>
        {
            // Quad of 2 dots supports the pair and two chows.
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            // Chows: 234 dots (x2), 567 dots (x2)
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.SevenPairs);
        hand.BasicScore.Should().Be(24);
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WhenFlowerOrSeasonTileIsIncluded()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            // Chow 3-4-5 Dots
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5),
            // Chow 6-7-8 Dots
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            // Pung of Red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Invalid in-hand bonus tiles
            new(TileSuit.Flowers, 1),
            new(TileSuit.Seasons, 2),
            // Filler tile to keep 14 tiles
            new(TileSuit.Bamboos, 4)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WithFewerThanFourteenTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WithMoreThanFourteenTiles()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(TileSuit.Flowers, 1)]
    [InlineData(TileSuit.Seasons, 2)]
    public void HandAnalyzer_RejectsHands_WhenFlowerOrSeasonTileAppears(TileSuit suit, int rank)
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 characters
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            // Melds: 123 chars, 456 chars, 789 dots, pung red dragons (flower/season replaces third dragon)
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new Tile(suit, rank)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WhenAnyTileExceedsFourCopies()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 3)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsSevenPairsShape_WhenTileAppearsSixTimes()
    {
        var tiles = new List<Tile>
        {
            // Six copies would otherwise form three pairs, but exceeds tile limits.
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_RejectsHands_WhenSuitedTileExceedsFourCopies()
    {
        var tiles = new List<Tile>
        {
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 7)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
    }

    [Fact]
    public void HandAnalyzer_PrioritizesHigherStandardScore_WhenMultiplePairCandidatesExist()
    {
        var tiles = new List<Tile>
        {
            // Candidate pairs on 1 dots (low score) vs 4 dots (higher score).
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.Pair.Should().Be(new Tile(TileSuit.Dots, 4));
        hand.Melds.Count(meld => meld.Type == MeldType.Pung).Should().Be(3);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AssignsStandardScore_BasedOnPungCount()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 characters
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            // Melds: pung 1 dots, pung red dragon, chow 234 bamboos, chow 678 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(4);
    }

    [Fact]
    public void HandAnalyzer_AssignsStandardScore_WithSinglePung()
    {
        var tiles = new List<Tile>
        {
            // Pair: 9 dots
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            // Melds: pung 3 chars, chow 123 bamboos, chow 456 dots, chow 789 chars
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(2);
    }

    [Fact]
    public void HandAnalyzer_AssignsStandardScore_WithThreePungs()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Melds: pung 2 chars, pung 7 bamboos, pung red dragon, chow 345 dots
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(6);
    }

    [Fact]
    public void HandAnalyzer_AssignsZeroScore_WhenStandardHandHasOnlyChows()
    {
        var tiles = new List<Tile>
        {
            // Pair: 5 dots
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Melds: 123 chars, 456 chars, 234 bamboos, 678 dots
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);
        hand.BasicScore.Should().Be(0);
    }

    [Fact]
    public void Tile_ThrowsForInvalidSuitedRank()
    {
        var create = () => new Tile(TileSuit.Dots, 10);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindMetadataMissing()
    {
        var create = () => new Tile(TileSuit.Winds, 0);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenDragonMetadataMissing()
    {
        var create = () => new Tile(TileSuit.Dragons, 0);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindIncludesDragonValue()
    {
        var create = () => new Tile(TileSuit.Winds, 0, Wind.East, Dragon.Red);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenDragonIncludesWindValue()
    {
        var create = () => new Tile(TileSuit.Dragons, 0, Wind.East, Dragon.Red);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tile_ThrowsWhenWindRankIsNonZero()
    {
        var create = () => new Tile(TileSuit.Winds, 1, Wind.East);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsWhenDragonRankIsNonZero()
    {
        var create = () => new Tile(TileSuit.Dragons, 2, null, Dragon.Red);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsForInvalidFlowerRank()
    {
        var create = () => new Tile(TileSuit.Flowers, 5);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tile_ThrowsForInvalidSeasonRank()
    {
        var create = () => new Tile(TileSuit.Seasons, 0);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
