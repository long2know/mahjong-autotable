using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class TileFactoryTests
{
    [Fact]
    public void CreateStandardSet_Returns136Tiles()
    {
        var tiles = TileFactory.CreateStandardSet();

        tiles.Should().HaveCount(136);
    }

    [Fact]
    public void CreateStandardSet_IncludesFourCopiesOfEachSuitRank()
    {
        var tiles = TileFactory.CreateStandardSet();

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Count(t => t.Suit == suit && t.Rank == rank).Should().Be(4);
            }
        }
    }

    [Fact]
    public void CreateStandardSet_IncludesFourCopiesOfEachHonor()
    {
        var tiles = TileFactory.CreateStandardSet();

        foreach (Wind wind in Enum.GetValues(typeof(Wind)))
        {
            tiles.Count(t => t.Suit == TileSuit.Winds && t.Wind == wind).Should().Be(4);
        }

        foreach (Dragon dragon in Enum.GetValues(typeof(Dragon)))
        {
            tiles.Count(t => t.Suit == TileSuit.Dragons && t.Dragon == dragon).Should().Be(4);
        }
    }

    [Fact]
    public void CreateStandardSet_ExcludesFlowersAndSeasons()
    {
        var tiles = TileFactory.CreateStandardSet();

        tiles.Should().NotContain(tile => tile.Suit == TileSuit.Flowers || tile.Suit == TileSuit.Seasons);
    }

    [Fact]
    public void CreateChangsha258Set_Returns108Tiles()
    {
        var tiles = TileFactory.CreateChangsha258Set();

        tiles.Should().HaveCount(Changsha258Ruleset.TileSetSize);
    }

    [Fact]
    public void CreateChangsha258Set_IncludesFourCopiesOfEachSuitRank()
    {
        var tiles = TileFactory.CreateChangsha258Set();

        foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                tiles.Count(t => t.Suit == suit && t.Rank == rank).Should().Be(4);
            }
        }
    }

    [Fact]
    public void CreateChangsha258Set_ExcludesHonorsAndBonusTiles()
    {
        var tiles = TileFactory.CreateChangsha258Set();

        tiles.Should().OnlyContain(tile => tile.Suit == TileSuit.Characters || tile.Suit == TileSuit.Dots || tile.Suit == TileSuit.Bamboos);
    }
}

