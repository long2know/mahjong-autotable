using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void CalculateBasicScore_CountsPungs()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            // Pung of red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chow 1-2-3 dots
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            // Chow 4-5-6 dots
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            // Chow 7-8-9 dots
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();

        ScoreCalculator.CalculateBasicScore(hand!).Should().Be(2);
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsFalse_WhenHandIsBelowChineseOfficialThreshold()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
                })
            },
            new Tile(TileSuit.Characters, 2));

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeFalse();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_WhenHandMeetsChineseOfficialThreshold()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 9)
                })
            },
            new Tile(TileSuit.Characters, 5));

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForSevenPairsSpecialHand()
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), WinningPattern.SevenPairs);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForThirteenOrphansSpecialHand()
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Characters, 1), WinningPattern.ThirteenOrphans);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForAllHonorsSpecialHand()
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Winds, 0, Wind.East), WinningPattern.AllHonors);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForAllTerminalsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.AllTerminals);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForAllTerminalsAndHonorsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.AllTerminalsAndHonors);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForAllGreenSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Green),
            WinningPattern.AllGreen);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForBigThreeDragonsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Winds, 0, Wind.East),
            WinningPattern.BigThreeDragons);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForBigFourWindsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            WinningPattern.BigFourWinds);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForLittleThreeDragonsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dragons, 0, null, Dragon.White),
            WinningPattern.LittleThreeDragons);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForLittleFourWindsSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Winds, 0, Wind.North),
            WinningPattern.LittleFourWinds);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForNineGatesSpecialHand()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 5),
            WinningPattern.NineGates);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsFalse_ForAllPungsWithoutAdditionalFan()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        ScoreCalculator.MeetsMinimumFan(hand).Should().BeFalse();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_WhenCustomThresholdMatchesHandScore()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        ScoreCalculator.MeetsMinimumFan(hand, 6).Should().BeTrue();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsFalse_WhenCustomThresholdExceedsHandScore()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        ScoreCalculator.MeetsMinimumFan(hand, 7).Should().BeFalse();
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsExpectedShortfall_WhenHandIsBelowThreshold()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(6);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeFalse();
        evaluation.FanShortfall.Should().Be(2);
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsZeroShortfall_WhenHandMeetsThreshold()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.SevenPairs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(24);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void MeetsMinimumFan_ThrowsForNegativeThreshold()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3)
                })
            },
            new Tile(TileSuit.Characters, 1));

        var act = () => ScoreCalculator.MeetsMinimumFan(hand, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
