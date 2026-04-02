using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class WallBuilderTests
{
    [Fact]
    public void BuildShuffledWall_ReturnsChangsha258WallByDefault()
    {
        var wall = WallBuilder.BuildShuffledWall(new Random(1337));
        var tiles = wall.Tiles;

        tiles.Should().HaveCount(108);
        wall.RemainingCount.Should().Be(108);
        tiles.Should().OnlyContain(tile => !tile.IsHonor && !tile.IsFlower);

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Count(t => t.Suit == suit && t.Rank == rank).Should().Be(4);
            }
        }
    }

    [Theory]
    [InlineData(MahjongRuleset.ChineseOfficial, 144)]
    [InlineData(MahjongRuleset.Changsha258, 108)]
    public void GetWallTotal_ReturnsExpectedCounts(MahjongRuleset ruleset, int expected)
    {
        WallBuilder.GetWallTotal(ruleset).Should().Be(expected);
    }

    [Fact]
    public void BuildShuffledWall_ReturnsChangsha258Wall_WhenRulesetSelected()
    {
        var wall = WallBuilder.BuildShuffledWall(new Random(1337), MahjongRuleset.Changsha258);
        var tiles = wall.Tiles;

        tiles.Should().HaveCount(108);
        wall.RemainingCount.Should().Be(108);
        tiles.Should().OnlyContain(tile => tile.Suit == TileSuit.Characters || tile.Suit == TileSuit.Dots || tile.Suit == TileSuit.Bamboos);
    }

    [Fact]
    public void BuildShuffledWall_ReturnsChangsha258TileCounts()
    {
        var wall = WallBuilder.BuildShuffledWall(new Random(1337), MahjongRuleset.Changsha258);
        var tiles = wall.Tiles;

        tiles.Should().OnlyContain(tile => !tile.IsHonor && !tile.IsFlower);

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Count(t => t.Suit == suit && t.Rank == rank).Should().Be(4);
            }
        }
    }
}
