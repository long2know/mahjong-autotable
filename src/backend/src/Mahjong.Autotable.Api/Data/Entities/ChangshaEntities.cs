namespace Mahjong.Autotable.Api.Data.Entities;

/// <summary>
/// Changsha game entity — stores multi-round game state.
/// State is stored as JSON for v1 simplicity.
/// </summary>
public class ChangshaGame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleSet { get; set; } = "changsha-v1";
    public int Seed { get; set; }
    public string StateJson { get; set; } = string.Empty;
    public int StateVersion { get; set; } = 1;
    public int CurrentHandNumber { get; set; } = 1;
    public int CurrentRoundNumber { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Append-only event log for Changsha games.
/// Supports replay and reconnection.
/// </summary>
public class ChangshaGameEvent
{
    public long Id { get; set; }
    public Guid GameId { get; set; }
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public int TurnNumber { get; set; }
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int HandNumber { get; set; }
    public int StateVersion { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime PersistedUtc { get; set; }
}
