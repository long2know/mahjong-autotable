using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class WallBuilderTests
{
    [Fact]
    public void BuildShuffledWall_ReturnsWallWithExpectedTileCounts()
    {
        var wall = WallBuilder.BuildShuffledWall(new Random(1337));
        var tiles = wall.Tiles;

        tiles.Should().HaveCount(144);
        wall.RemainingCount.Should().Be(144);

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Count(t => t.Suit == suit && t.Rank == rank).Should().Be(4);
            }
        }

        foreach (Wind wind in Enum.GetValues(typeof(Wind)))
        {
            tiles.Count(t => t.Suit == TileSuit.Winds && t.Wind == wind).Should().Be(4);
        }

        foreach (Dragon dragon in Enum.GetValues(typeof(Dragon)))
        {
            tiles.Count(t => t.Suit == TileSuit.Dragons && t.Dragon == dragon).Should().Be(4);
        }

        for (var rank = 1; rank <= 4; rank++)
        {
            tiles.Count(t => t.Suit == TileSuit.Flowers && t.Rank == rank).Should().Be(1);
            tiles.Count(t => t.Suit == TileSuit.Seasons && t.Rank == rank).Should().Be(1);
        }
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
