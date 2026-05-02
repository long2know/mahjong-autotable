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
            new(Guid.NewGuid(), Guid.NewGuid(), null!)
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
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, m1, new Tile(TileSuit.Dots, 3)), new(p1, m1, new Tile(TileSuit.Dots, 4)), new(p1, m1, new Tile(TileSuit.Dots, 5)),
            new(p2, m2, new Tile(TileSuit.Dots, 2)), new(p2, m2, new Tile(TileSuit.Dots, 3)), new(p2, m2, new Tile(TileSuit.Dots, 4)),
            new(p3, m3, new Tile(TileSuit.Dots, 1)), new(p3, m3, new Tile(TileSuit.Dots, 2)), new(p3, m3, new Tile(TileSuit.Dots, 3))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().BeEmpty();
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_DoesNotFlagSamePlayerCrossMeldChowAggregation()
    {
        var playerId = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(playerId, m1, new Tile(TileSuit.Dots, 1)), new(playerId, m1, new Tile(TileSuit.Dots, 2)), new(playerId, m1, new Tile(TileSuit.Dots, 3)),
            new(playerId, m2, new Tile(TileSuit.Dots, 2)), new(playerId, m2, new Tile(TileSuit.Dots, 3)), new(playerId, m2, new Tile(TileSuit.Dots, 4)),
            new(playerId, m3, new Tile(TileSuit.Dots, 3)), new(playerId, m3, new Tile(TileSuit.Dots, 4)), new(playerId, m3, new Tile(TileSuit.Dots, 5))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().BeEmpty();
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_ReturnsTripletTilesForSinglePlayerPung()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var pungMeld = Guid.NewGuid();
        var chowMeld = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, pungMeld, new Tile(TileSuit.Dots, 9)),
            new(p1, pungMeld, new Tile(TileSuit.Dots, 9)),
            new(p1, pungMeld, new Tile(TileSuit.Dots, 9)),
            new(p2, chowMeld, new Tile(TileSuit.Dots, 1)),
            new(p2, chowMeld, new Tile(TileSuit.Dots, 2)),
            new(p2, chowMeld, new Tile(TileSuit.Dots, 3))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().HaveCount(3)
            .And.OnlyContain(tile => tile == new Tile(TileSuit.Dots, 9));
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_IgnoresExposedKongGroups()
    {
        var p1 = Guid.NewGuid();
        var kongMeld = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(p1, kongMeld, new Tile(TileSuit.Dots, 7)),
            new(p1, kongMeld, new Tile(TileSuit.Dots, 7)),
            new(p1, kongMeld, new Tile(TileSuit.Dots, 7)),
            new(p1, kongMeld, new Tile(TileSuit.Dots, 7))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(input);

        threats.Should().BeEmpty();
    }

    [Fact]
    public void BuildPromotableKongThreatTiles_ExcludesSpecifiedPlayers()
    {
        var excludedPlayer = Guid.NewGuid();
        var opponentPlayer = Guid.NewGuid();
        var excludedMeld = Guid.NewGuid();
        var opponentMeld = Guid.NewGuid();

        var input = new List<OpponentMeldTileView>
        {
            new(excludedPlayer, excludedMeld, new Tile(TileSuit.Dots, 2)),
            new(excludedPlayer, excludedMeld, new Tile(TileSuit.Dots, 2)),
            new(excludedPlayer, excludedMeld, new Tile(TileSuit.Dots, 2)),
            new(opponentPlayer, opponentMeld, new Tile(TileSuit.Bamboos, 9)),
            new(opponentPlayer, opponentMeld, new Tile(TileSuit.Bamboos, 9)),
            new(opponentPlayer, opponentMeld, new Tile(TileSuit.Bamboos, 9))
        };

        var threats = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(
            input,
            new HashSet<Guid> { excludedPlayer });

        threats.Should().HaveCount(3)
            .And.OnlyContain(tile => tile == new Tile(TileSuit.Bamboos, 9));
    }
}
