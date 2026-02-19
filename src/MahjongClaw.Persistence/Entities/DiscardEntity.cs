namespace MahjongClaw.Persistence.Entities;

public sealed class DiscardEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid TileId { get; set; }
    public int Sequence { get; set; }
    public DateTimeOffset DiscardedAt { get; set; } = DateTimeOffset.UtcNow;

    public GameEntity? Game { get; set; }
    public PlayerEntity? Player { get; set; }
    public TileEntity? Tile { get; set; }
}
