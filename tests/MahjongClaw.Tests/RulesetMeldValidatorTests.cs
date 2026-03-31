using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class RulesetMeldValidatorTests
{
    [Fact]
    public void IsValidMeld_AllowsHonorPung_ForChineseOfficial()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East)
        };

        RulesetMeldValidator.IsValidMeld(tiles, MeldType.Pung, MahjongRuleset.ChineseOfficial)
            .Should().BeTrue();
    }

    [Fact]
    public void IsValidMeld_RejectsHonorPung_ForChangsha258()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East),
            new Tile(TileSuit.Winds, 0, Wind.East)
        };

        RulesetMeldValidator.IsValidMeld(tiles, MeldType.Pung, MahjongRuleset.Changsha258)
            .Should().BeFalse();
    }

    [Fact]
    public void IsValidMeld_AllowsSuitedChow_ForChangsha258()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Bamboos, 1),
            new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 3)
        };

        RulesetMeldValidator.IsValidMeld(tiles, MeldType.Chow, MahjongRuleset.Changsha258)
            .Should().BeTrue();
    }

    [Fact]
    public void IsValidMeld_RejectsHonorKong_ForChangsha258()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red),
            new Tile(TileSuit.Dragons, 0, null, Dragon.Red)
        };

        RulesetMeldValidator.IsValidMeld(tiles, MeldType.Kong, MahjongRuleset.Changsha258)
            .Should().BeFalse();
    }

    [Fact]
    public void IsValidMeld_AllowsSuitedKong_ForChangsha258()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 2)
        };

        RulesetMeldValidator.IsValidMeld(tiles, MeldType.Kong, MahjongRuleset.Changsha258)
            .Should().BeTrue();
    }
}
