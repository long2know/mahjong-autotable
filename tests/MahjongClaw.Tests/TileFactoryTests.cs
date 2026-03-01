using FluentAssertions;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class TileFactoryTests
{
    [Fact]
    public void CreateStandardSet_Returns144Tiles()
    {
        var tiles = TileFactory.CreateStandardSet();

        tiles.Should().HaveCount(144);
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
}
