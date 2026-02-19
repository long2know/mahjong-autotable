using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed class Wall
{
    private readonly List<Tile> _tiles;

    public Wall(IEnumerable<Tile> tiles)
    {
        _tiles = tiles.ToList();
    }

    public int RemainingCount => _tiles.Count;

    public IReadOnlyList<Tile> Tiles => _tiles;

    public Tile Draw()
    {
        if (_tiles.Count == 0)
        {
            throw new InvalidOperationException("Wall is empty.");
        }

        var tile = _tiles[0];
        _tiles.RemoveAt(0);
        return tile;
    }
}
