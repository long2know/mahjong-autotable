using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed record MissedWinLock(IReadOnlySet<Tile> LockedWinningTiles)
{
    public bool IsLockedOnTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return LockedWinningTiles.Any(lockedTile => 
            lockedTile.Suit == tile.Suit && 
            lockedTile.Rank == tile.Rank);
    }
}
