using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class OpponentMeldThreatAnalyzerTests
{
    [Fact]
    public void BuildPromotableKongThreatTiles_ThrowsForNullInput()
    {
        Action act = () => OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_ThrowsWhenTileIsNull()
    {
        var input = new List<OpponentMeldTileView>
        {
            new(Guid.NewGuid(), null!)
        };

        Action act = () => OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_DoesNotFlagCrossPlayerChowAggregation()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, new Tile(TileSuit.Dots, 3)), new(p1, new Tile(TileSuit.Dots, 4)), new(p1, new Tile(TileSuit.Dots, 5)),
            new(p2, new Tile(TileSuit.Dots, 2)), new(p2, new Tile(TileSuit.Dots, 3)), new(p2, new Tile(TileSuit.Dots, 4)),
            new(p3, new Tile(TileSuit.Dots, 1)), new(p3, new Tile(TileSuit.Dots, 2)), new(p3, new Tile(TileSuit.Dots, 3))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().BeEmpty();
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_ReturnsTripletTilesForSinglePlayerPung()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, new Tile(TileSuit.Dots, 9)),
            new(p1, new Tile(TileSuit.Dots, 9)),
            new(p1, new Tile(TileSuit.Dots, 9)),
            new(p2, new Tile(TileSuit.Dots, 1)),
            new(p2, new Tile(TileSuit.Dots, 2)),
            new(p2, new Tile(TileSuit.Dots, 3))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().HaveCount(3)
            .And.OnlyContain(tile => tile == new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_IgnoresExposedKongGroups()
    {
        var p1 = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, new Tile(TileSuit.Dots, 7)),
            new(p1, new Tile(TileSuit.Dots, 7)),
            new(p1, new Tile(TileSuit.Dots, 7)),
            new(p1, new Tile(TileSuit.Dots, 7))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().BeEmpty();
    }
}
