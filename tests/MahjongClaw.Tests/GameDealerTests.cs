using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using System.Linq;

namespace MahjongClaw.Tests;

public class GameDealerTests
{
    [Fact]
    public void InitializeGame_DealsExpectedHandCounts()
    {
        var rng = new Random(42);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        var east = state.GetPlayer(Wind.East);
        var south = state.GetPlayer(Wind.South);
        var west = state.GetPlayer(Wind.West);
        var north = state.GetPlayer(Wind.North);

        east.Hand.Tiles.Should().HaveCount(14);
        south.Hand.Tiles.Should().HaveCount(13);
        west.Hand.Tiles.Should().HaveCount(13);
        north.Hand.Tiles.Should().HaveCount(13);
    }

    [Fact]
    public void InitializeGame_LeavesExpectedWallCount()
    {
        var rng = new Random(99);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        state.Wall.RemainingCount.Should().Be(108 - 53);
    }

    [Fact]
    public void InitializeGame_LeavesExpectedWallCount_ForChineseOfficial()
    {
        var rng = new Random(101);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng, MahjongRuleset.ChineseOfficial);

        state.Wall.RemainingCount.Should().Be(144 - 53);
    }

    [Fact]
    public void InitializeGame_UsesChangsha258TileSet()
    {
        var rng = new Random(123);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng, MahjongRuleset.Changsha258);

        var allTiles = state.Players
            .SelectMany(player => player.Hand.Tiles)
            .Concat(state.Wall.Tiles)
            .ToList();

        allTiles.Should().HaveCount(108);
        allTiles.Should().OnlyContain(tile => !tile.IsHonor && !tile.IsFlower);
    }

    [Fact]
    public void InitializeGame_AssignsPlayersToWindsInSeatOrder()
    {
        var rng = new Random(17);
        var state = GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, rng);

        state.GetPlayer(Wind.East).Name.Should().Be("Alice");
        state.GetPlayer(Wind.South).Name.Should().Be("Bob");
        state.GetPlayer(Wind.West).Name.Should().Be("Chris");
        state.GetPlayer(Wind.North).Name.Should().Be("Dana");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void InitializeGame_ThrowsWhenPlayerCountIsNotFour(int playerCount)
    {
        var names = Enumerable.Range(0, playerCount)
            .Select(index => $"Player{index}")
            .ToArray();

        var act = () => GameDealer.InitializeGame(names, new Random(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Exactly four players are required.");
    }

    [Fact]
    public void InitializeGame_ThrowsWhenPlayerNamesAreNull()
    {
        var act = () => GameDealer.InitializeGame(null!, new Random(5));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InitializeGame_ThrowsWhenRandomIsNull()
    {
        var act = () => GameDealer.InitializeGame(new[] { "Alice", "Bob", "Chris", "Dana" }, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DrawTiles_ThrowsWhenWallIsNull()
    {
        var act = () => GameDealer.DrawTiles(null!, new Hand(), 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DrawTiles_ThrowsWhenHandIsNull()
    {
        var wall = new Wall(new[] { new Tile(TileSuit.Dots, 1) });

        var act = () => GameDealer.DrawTiles(wall, null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DrawTiles_ThrowsWhenCountIsNegative()
    {
        var wall = new Wall(new[] { new Tile(TileSuit.Dots, 1) });
        var hand = new Hand();

        var act = () => GameDealer.DrawTiles(wall, hand, -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*count*negative*");
    }

    [Fact]
    public void DrawTiles_AddsTilesToHandAndReducesWall()
    {
        var tiles = new[]
        {
            new Tile(TileSuit.Dots, 1),
            new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 3)
        };

        var wall = new Wall(tiles);
        var hand = new Hand();

        GameDealer.DrawTiles(wall, hand, 2);

        hand.Tiles.Should().Equal(tiles[0], tiles[1]);
        wall.RemainingCount.Should().Be(1);
    }
}
