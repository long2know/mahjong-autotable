using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed class Player
{
    public Player(Guid id, string name, Wind seat)
    {
        Id = id;
        Name = name;
        Seat = seat;
        Hand = new Hand();
    }

    public Guid Id { get; }
    public string Name { get; }
    public Wind Seat { get; }
    public Hand Hand { get; }
    public List<Tile> Discards { get; } = new();
}
