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
    public void HandAnalyzer_RejectsSevenPairsVariant_NotYetSupportedByCurrentEngine()
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

        HandAnalyzer.IsWinningHand(tiles).Should().BeFalse();
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
