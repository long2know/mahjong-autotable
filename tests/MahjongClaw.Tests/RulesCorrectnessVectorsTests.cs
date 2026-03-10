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
    public void HandAnalyzer_AcceptsLittleThreeDragons_AsWinningPattern()
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

    [Fact]
    public void HandAnalyzer_RejectsNineGatesShape_WhenExtraTileIsOffSuit()
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
            new(TileSuit.Bamboos, 5)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out _).Should().BeFalse();
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
}
