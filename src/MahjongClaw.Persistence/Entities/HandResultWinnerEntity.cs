namespace MahjongClaw.Persistence.Entities;

public sealed class HandResultWinnerEntity
{
    public Guid Id { get; set; }
    public Guid HandResultId { get; set; }
    public Guid PlayerId { get; set; }
    public string WinTypesJson { get; set; } = "[]";
    public bool IsBigWin { get; set; }
    public bool IsDealer { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public HandResultEntity? HandResult { get; set; }
}
