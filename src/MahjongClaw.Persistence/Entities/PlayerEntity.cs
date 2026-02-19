namespace MahjongClaw.Persistence.Entities;

public sealed class PlayerEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Seat { get; set; } = string.Empty;
    public int Score { get; set; }

    public GameEntity? Game { get; set; }
    public ICollection<TileEntity> Tiles { get; set; } = new List<TileEntity>();
}
