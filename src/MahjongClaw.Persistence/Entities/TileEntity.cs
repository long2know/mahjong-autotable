namespace MahjongClaw.Persistence.Entities;

public sealed class TileEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid? PlayerId { get; set; }
    public Guid? MeldId { get; set; }
    public string Suit { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string? Wind { get; set; }
    public string? Dragon { get; set; }
    public string Location { get; set; } = "Wall"; // Wall, Hand, Discard, Meld, DeadWall
    public int Sequence { get; set; }

    public GameEntity? Game { get; set; }
    public PlayerEntity? Player { get; set; }
    public MeldEntity? Meld { get; set; }
}
