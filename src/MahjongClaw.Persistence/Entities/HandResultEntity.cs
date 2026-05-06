namespace MahjongClaw.Persistence.Entities;

public sealed class HandResultEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int HandNumber { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public Guid DealerId { get; set; }
    public string RoundWind { get; set; } = "East";
    public bool IsSelfDraw { get; set; }
    public Guid? DiscarderId { get; set; }
    public bool WallExhausted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public GameEntity? Game { get; set; }
    public ICollection<HandResultWinnerEntity> Winners { get; set; } = new List<HandResultWinnerEntity>();
    public ICollection<HandResultSeatDeltaEntity> SeatDeltas { get; set; } = new List<HandResultSeatDeltaEntity>();
}
