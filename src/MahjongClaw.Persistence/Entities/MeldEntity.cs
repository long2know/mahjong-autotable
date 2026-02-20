namespace MahjongClaw.Persistence.Entities;

public sealed class MeldEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string Type { get; set; } = string.Empty; // Chow, Pung, Kong
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public GameEntity? Game { get; set; }
    public PlayerEntity? Player { get; set; }
    public ICollection<TileEntity> Tiles { get; set; } = new List<TileEntity>();
}
