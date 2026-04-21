namespace Mahjong.Autotable.Api.Data.Entities;

public class TableSessionEvent
{
    public long Id { get; set; }
    public Guid TableSessionId { get; set; }
    public long Sequence { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public int TurnNumber { get; set; }
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int StateVersion { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
    public DateTime PersistedUtc { get; set; } = DateTime.UtcNow;
}
