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
    public void CalculateBasicScore_ReturnsZero_WhenOnlyChowsPresent()
    {
        var tiles = new List<Tile>
        {
            // Pair
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            // Chow 1-2-3 characters
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            // Chow 4-5-6 characters
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            // Chow 2-3-4 bamboos
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            // Chow 7-8-9 dots
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();

        ScoreCalculator.CalculateBasicScore(hand!).Should().Be(0);
    }

    [Fact]
    public void CalculateBasicScore_ReturnsZero_WhenOnlyChowsPresent_WithHonorPair()
    {
        var tiles = new List<Tile>
        {
            // Pair: red dragons
            new(TileSuit.Dragons, 0, null, Dragon.Red), new(TileSuit.Dragons, 0, null, Dragon.Red),
            // Chow 1-2-3 characters
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            // Chow 4-5-6 characters
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            // Chow 2-3-4 bamboos
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            // Chow 7-8-9 dots
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9)
        };

        HandAnalyzer.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand!.Pattern.Should().Be(WinningPattern.Standard);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(0);
    }

    [Fact]
    public void CalculateBasicScore_CountsKongsAndPungs_ForStandardHand()
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
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 5),
                    new(TileSuit.Dots, 5),
                    new(TileSuit.Dots, 5)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 9),
                    new(TileSuit.Characters, 9),
                    new(TileSuit.Characters, 9)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 1),
                    new(TileSuit.Bamboos, 2),
                    new(TileSuit.Bamboos, 3)
                })
            },
            new Tile(TileSuit.Dots, 1));

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(12);
    }

    [Fact]
    public void CalculateBasicScore_CountsMultipleKongs_ForStandardHand()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                }),
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 2),
                    new(TileSuit.Characters, 2),
                    new(TileSuit.Characters, 2)
                })
            },
            new Tile(TileSuit.Bamboos, 5));

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(20);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllHonors()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                })
            },
            new Tile(TileSuit.Dragons, 0, null, Dragon.White),
            WinningPattern.AllHonors);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForNineGates()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1),
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 3)
                })
            },
            new Tile(TileSuit.Dots, 5),
            WinningPattern.NineGates);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(88);
    }

    [Theory]
    [InlineData(WinningPattern.AllGreen, 88)]
    [InlineData(WinningPattern.BigFourWinds, 88)]
    [InlineData(WinningPattern.LittleFourWinds, 64)]
    [InlineData(WinningPattern.AllTerminalsAndHonors, 64)]
    public void CalculateBasicScore_UsesPatternScore_ForSpecialHands(WinningPattern pattern, int expectedScore)
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), pattern);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(WinningPattern.SevenPairs, 24)]
    [InlineData(WinningPattern.ThirteenOrphans, 88)]
    [InlineData(WinningPattern.AllTerminals, 64)]
    [InlineData(WinningPattern.BigThreeDragons, 88)]
    [InlineData(WinningPattern.LittleThreeDragons, 64)]
    public void CalculateBasicScore_UsesPatternScore_ForAdditionalSpecialHands(WinningPattern pattern, int expectedScore)
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), pattern);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(expectedScore);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForSevenPairs_EvenWhenMeldsProvided()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1),
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 3)
                })
            },
            new Tile(TileSuit.Dots, 2),
            WinningPattern.SevenPairs);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(24);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForBigThreeDragons_WithMeldsPresent()
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
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.White),
                    new(TileSuit.Dragons, 0, null, Dragon.White),
                    new(TileSuit.Dragons, 0, null, Dragon.White)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1),
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 3)
                })
            },
            new Tile(TileSuit.Bamboos, 5),
            WinningPattern.BigThreeDragons);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(88);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForLittleThreeDragons_WithMeldsPresent()
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
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 4),
                    new(TileSuit.Dots, 5),
                    new(TileSuit.Dots, 6)
                })
            },
            new Tile(TileSuit.Dragons, 0, null, Dragon.White),
            WinningPattern.LittleThreeDragons);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllTerminalsAndHonors_WithMeldsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1)
                })
            },
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            WinningPattern.AllTerminalsAndHonors);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllTerminalsAndHonors_WhenKongPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                })
            },
            new Tile(TileSuit.Bamboos, 1),
            WinningPattern.AllTerminalsAndHonors);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllTerminals_WithMeldsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9)
                })
            },
            new Tile(TileSuit.Bamboos, 1),
            WinningPattern.AllTerminals);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllPungs_WithMeldsPresent()
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
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 2)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Bamboos, 7),
                    new(TileSuit.Bamboos, 7),
                    new(TileSuit.Bamboos, 7)
                }),
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Characters, 9),
                    new(TileSuit.Characters, 9),
                    new(TileSuit.Characters, 9),
                    new(TileSuit.Characters, 9)
                })
            },
            new Tile(TileSuit.Dots, 5),
            WinningPattern.AllPungs);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(6);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllGreen_WithMeldsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 2),
                    new(TileSuit.Bamboos, 3),
                    new(TileSuit.Bamboos, 4)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 2),
                    new(TileSuit.Bamboos, 3),
                    new(TileSuit.Bamboos, 4)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green),
                    new(TileSuit.Dragons, 0, null, Dragon.Green)
                })
            },
            new Tile(TileSuit.Bamboos, 8),
            WinningPattern.AllGreen);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(88);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForBigFourWinds_WithMeldsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.South),
                    new(TileSuit.Winds, 0, Wind.South),
                    new(TileSuit.Winds, 0, Wind.South)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.West),
                    new(TileSuit.Winds, 0, Wind.West),
                    new(TileSuit.Winds, 0, Wind.West)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.North),
                    new(TileSuit.Winds, 0, Wind.North),
                    new(TileSuit.Winds, 0, Wind.North)
                })
            },
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            WinningPattern.BigFourWinds);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(88);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForLittleFourWinds_WithMeldsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.South),
                    new(TileSuit.Winds, 0, Wind.South),
                    new(TileSuit.Winds, 0, Wind.South)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.West),
                    new(TileSuit.Winds, 0, Wind.West),
                    new(TileSuit.Winds, 0, Wind.West)
                }),
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                })
            },
            new Tile(TileSuit.Winds, 0, Wind.North),
            WinningPattern.LittleFourWinds);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(64);
    }

    [Fact]
    public void CalculateBasicScore_IgnoresMeldScore_ForAllPungsPattern()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.White),
                    new(TileSuit.Dragons, 0, null, Dragon.White),
                    new(TileSuit.Dragons, 0, null, Dragon.White),
                    new(TileSuit.Dragons, 0, null, Dragon.White)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 7),
                    new(TileSuit.Characters, 7),
                    new(TileSuit.Characters, 7)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 3),
                    new(TileSuit.Dots, 3),
                    new(TileSuit.Dots, 3)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6)
                })
            },
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(6);
    }

    [Fact]
    public void CalculateBasicScore_UsesPatternScore_ForAllPungs_WhenChowIsPresent()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 7),
                    new(TileSuit.Dots, 7),
                    new(TileSuit.Dots, 7)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 3),
                    new(TileSuit.Characters, 3),
                    new(TileSuit.Characters, 3)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
                    new(TileSuit.Dragons, 0, null, Dragon.Red)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Bamboos, 1),
                    new(TileSuit.Bamboos, 2),
                    new(TileSuit.Bamboos, 3)
                })
            },
            new Tile(TileSuit.Dots, 5),
            WinningPattern.AllPungs);

        ScoreCalculator.CalculateBasicScore(hand).Should().Be(6);
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsShortfall_WhenHandIsBelowThreshold()
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

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(2);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeFalse();
        evaluation.FanShortfall.Should().Be(6);
    }

    [Fact]
    public void EvaluateMinimumFan_UsesPatternScore_ForAllPungsEvenWithKongs()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1),
                    new(TileSuit.Characters, 1)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Bamboos, 5),
                    new(TileSuit.Bamboos, 5),
                    new(TileSuit.Bamboos, 5)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                })
            },
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(6);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeFalse();
        evaluation.FanShortfall.Should().Be(2);
    }

    [Fact]
    public void EvaluateMinimumFan_UsesPatternScore_ForThirteenOrphans()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.ThirteenOrphans);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(88);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void EvaluateMinimumFan_UsesPatternScore_ForNineGates()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 5),
            WinningPattern.NineGates);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(88);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void EvaluateMinimumFan_UsesPatternScore_ForSevenPairs_WhenMeldsProvided()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Dots, 1),
                    new(TileSuit.Dots, 2),
                    new(TileSuit.Dots, 3)
                })
            },
            new Tile(TileSuit.Dots, 2),
            WinningPattern.SevenPairs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(24);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Theory]
    [InlineData(WinningPattern.AllGreen, 88)]
    [InlineData(WinningPattern.AllHonors, 64)]
    [InlineData(WinningPattern.BigThreeDragons, 88)]
    [InlineData(WinningPattern.BigFourWinds, 88)]
    [InlineData(WinningPattern.LittleFourWinds, 64)]
    public void EvaluateMinimumFan_UsesPatternScore_ForSpecialHands(WinningPattern pattern, int expectedScore)
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), pattern);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(expectedScore);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Theory]
    [InlineData(WinningPattern.AllTerminals, 64)]
    [InlineData(WinningPattern.AllTerminalsAndHonors, 64)]
    public void EvaluateMinimumFan_UsesPatternScore_ForTerminalHonorHands(WinningPattern pattern, int expectedScore)
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), pattern);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand);

        evaluation.BasicScore.Should().Be(expectedScore);
        evaluation.MinimumFan.Should().Be(ScoreCalculator.ChineseOfficialMinimumFan);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void EvaluateMinimumFan_UsesOverrideMinimumFan_WhenProvided()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Dots, 3),
                    new(TileSuit.Dots, 3),
                    new(TileSuit.Dots, 3)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Characters, 7),
                    new(TileSuit.Characters, 7),
                    new(TileSuit.Characters, 7)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6),
                    new(TileSuit.Bamboos, 6)
                }),
                new(MeldType.Pung, new List<Tile>
                {
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East),
                    new(TileSuit.Winds, 0, Wind.East)
                })
            },
            new Tile(TileSuit.Dragons, 0, null, Dragon.White),
            WinningPattern.AllPungs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, minimumFan: 6);

        evaluation.BasicScore.Should().Be(6);
        evaluation.MinimumFan.Should().Be(6);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsMeetsThreshold_WhenMinimumFanIsZero()
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

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, minimumFan: 0);

        evaluation.BasicScore.Should().Be(2);
        evaluation.MinimumFan.Should().Be(0);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_WhenMinimumFanIsZero()
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

        ScoreCalculator.MeetsMinimumFan(hand, minimumFan: 0).Should().BeTrue();
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsMeetsThreshold_WhenScoreEqualsMinimum()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9),
                    new(TileSuit.Dots, 9)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6)
                }),
                new(MeldType.Chow, new List<Tile>
                {
                    new(TileSuit.Characters, 7), new(TileSuit.Characters, 8), new(TileSuit.Characters, 9)
                })
            },
            new Tile(TileSuit.Bamboos, 5));

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, minimumFan: 8);

        evaluation.BasicScore.Should().Be(8);
        evaluation.MinimumFan.Should().Be(8);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsShortfall_ForSpecialHand_WhenCustomThresholdIsHigher()
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1), WinningPattern.AllHonors);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, minimumFan: 70);

        evaluation.BasicScore.Should().Be(64);
        evaluation.MinimumFan.Should().Be(70);
        evaluation.MeetsMinimumFan.Should().BeFalse();
        evaluation.FanShortfall.Should().Be(6);
    }

    [Fact]
    public void EvaluateMinimumFan_Throws_WhenMinimumFanIsNegative()
    {
        var hand = new WinningHand(Array.Empty<Meld>(), new Tile(TileSuit.Dots, 1));

        Action act = () => ScoreCalculator.EvaluateMinimumFan(hand, -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("Minimum fan cannot be negative.*");
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
    public void MeetsMinimumFan_ReturnsTrue_WhenStandardHandScoresExactlyEight()
    {
        var hand = new WinningHand(
            new List<Meld>
            {
                new(MeldType.Kong, new List<Tile>
                {
                    new(TileSuit.Dragons, 0, null, Dragon.Red),
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
            new Tile(TileSuit.Bamboos, 2));

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
    public void MeetsMinimumFan_ReturnsFalse_ForSpecialHand_WhenCustomThresholdExceedsSpecialScore()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.SevenPairs);

        ScoreCalculator.MeetsMinimumFan(hand, 25).Should().BeFalse();
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsExpectedShortfall_ForSpecialHand_WhenCustomThresholdExceedsSpecialScore()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.SevenPairs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, 25);

        evaluation.BasicScore.Should().Be(24);
        evaluation.MinimumFan.Should().Be(25);
        evaluation.MeetsMinimumFan.Should().BeFalse();
        evaluation.FanShortfall.Should().Be(1);
    }

    [Fact]
    public void CalculateBasicScore_ThrowsForNullHand()
    {
        var act = () => ScoreCalculator.CalculateBasicScore(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateMinimumFan_ThrowsForNullHand()
    {
        var act = () => ScoreCalculator.EvaluateMinimumFan(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MeetsMinimumFan_ThrowsForNullHand()
    {
        var act = () => ScoreCalculator.MeetsMinimumFan(null!);

        act.Should().Throw<ArgumentNullException>();
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

    [Fact]
    public void EvaluateMinimumFan_ThrowsForNegativeThreshold()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Characters, 1),
            WinningPattern.SevenPairs);

        var act = () => ScoreCalculator.EvaluateMinimumFan(hand, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MeetsMinimumFan_ReturnsTrue_ForZeroThreshold()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        ScoreCalculator.MeetsMinimumFan(hand, 0).Should().BeTrue();
    }

    [Fact]
    public void EvaluateMinimumFan_ReturnsZeroShortfall_ForZeroThreshold_WhenHandWouldOtherwiseFailDefault()
    {
        var hand = new WinningHand(
            Array.Empty<Meld>(),
            new Tile(TileSuit.Dots, 2),
            WinningPattern.AllPungs);

        var evaluation = ScoreCalculator.EvaluateMinimumFan(hand, 0);

        evaluation.BasicScore.Should().Be(6);
        evaluation.MinimumFan.Should().Be(0);
        evaluation.MeetsMinimumFan.Should().BeTrue();
        evaluation.FanShortfall.Should().Be(0);
    }
}
