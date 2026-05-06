namespace MahjongClaw.Persistence.Entities;

public sealed class HandResultSeatDeltaEntity
{
    public Guid Id { get; set; }
    public Guid HandResultId { get; set; }
    public Guid PlayerId { get; set; }
    public int Delta { get; set; }
    public int NewScore { get; set; }
    public string DeltaCategory { get; set; } = "Winnings";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public HandResultEntity? HandResult { get; set; }
}
