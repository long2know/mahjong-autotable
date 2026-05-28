using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed record MissedWinLock(IReadOnlySet<Tile> LockedWinningTiles)
{
    public bool IsLockedOnTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return LockedWinningTiles.Contains(tile);
    }
}
