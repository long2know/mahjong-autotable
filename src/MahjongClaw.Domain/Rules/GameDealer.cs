using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class GameDealer
{
    public static GameState InitializeGame(IReadOnlyList<string> playerNames, Random rng)
    {
        if (playerNames.Count != 4)
        {
            throw new ArgumentException("Exactly four players are required.");
        }

        var wall = WallBuilder.BuildShuffledWall(rng);
        var players = new List<Player>
        {
            new(Guid.NewGuid(), playerNames[0], Wind.East),
            new(Guid.NewGuid(), playerNames[1], Wind.South),
            new(Guid.NewGuid(), playerNames[2], Wind.West),
            new(Guid.NewGuid(), playerNames[3], Wind.North)
        };

        DealInitialHands(wall, players);

        return new GameState(Guid.NewGuid(), players, wall, Wind.East);
    }

    public static void DealInitialHands(Wall wall, IReadOnlyList<Player> players)
    {
        // Deal 13 to each player, then East draws one extra for 14.
        for (var round = 0; round < 3; round++)
        {
            foreach (var player in players)
            {
                DrawTiles(wall, player.Hand, 4);
            }
        }

        foreach (var player in players)
        {
            DrawTiles(wall, player.Hand, 1);
        }

        var east = players.Single(p => p.Seat == Wind.East);
        DrawTiles(wall, east.Hand, 1);
    }

    public static void DrawTiles(Wall wall, Hand hand, int count)
    {
        for (var i = 0; i < count; i++)
        {
            hand.Add(wall.Draw());
        }
    }
}
