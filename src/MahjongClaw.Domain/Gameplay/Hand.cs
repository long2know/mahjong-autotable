using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed class Hand
{
    private readonly List<Tile> _tiles = new();

    public IReadOnlyList<Tile> Tiles => _tiles;

    public void Add(Tile tile) => _tiles.Add(tile);

    public void Remove(Tile tile) => _tiles.Remove(tile);
}
