using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed class GameState
{
    public GameState(Guid id, IReadOnlyList<Player> players, Wall wall, Wind dealer)
    {
        Id = id;
        Players = players;
        Wall = wall;
        Dealer = dealer;
        CurrentTurn = dealer;
    }

    public Guid Id { get; }
    public IReadOnlyList<Player> Players { get; }
    public Wall Wall { get; }
    public Wind Dealer { get; }
    public Wind CurrentTurn { get; private set; }

    public Player GetPlayer(Wind seat) => Players.Single(p => p.Seat == seat);

    public void AdvanceTurn()
    {
        CurrentTurn = CurrentTurn switch
        {
            Wind.East => Wind.South,
            Wind.South => Wind.West,
            Wind.West => Wind.North,
            Wind.North => Wind.East,
            _ => throw new InvalidOperationException("Invalid wind.")
        };
    }
}
