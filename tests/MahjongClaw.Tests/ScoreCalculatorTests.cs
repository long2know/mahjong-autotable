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
