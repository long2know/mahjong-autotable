namespace MahjongClaw.Persistence.Entities;

public sealed class GameEntity
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Waiting";
    public Guid? CurrentPlayerId { get; set; }
    public Guid? DealerId { get; set; }
    public int Round { get; set; }
    public string Ruleset { get; set; } = "ChineseOfficial";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlayerEntity> Players { get; set; } = new List<PlayerEntity>();
    public ICollection<TileEntity> Tiles { get; set; } = new List<TileEntity>();
    public ICollection<MeldEntity> Melds { get; set; } = new List<MeldEntity>();
    public ICollection<DiscardEntity> Discards { get; set; } = new List<DiscardEntity>();
}
