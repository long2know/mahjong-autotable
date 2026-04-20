namespace Mahjong.Autotable.Api.Tables;

public enum TableSeatType
{
    Human,
    Bot
}

public sealed class TableSeatState
{
    public int SeatIndex { get; init; }
    public TableSeatType SeatType { get; init; }
    public string PlayerId { get; init; } = string.Empty;
}

public enum TableTurnPhase
{
    AwaitingDiscard,
    WallExhausted
}

public sealed class TableStateMetadata
{
    public int Seed { get; set; }
}

public sealed class TableSeatHandState
{
    public int SeatIndex { get; set; }
    public List<int> Tiles { get; set; } = [];
}

public sealed class TableDiscard
{
    public int SeatIndex { get; init; }
    public int TileId { get; init; }
    public int TurnNumber { get; init; }
    public DateTime OccurredUtc { get; init; }
}

public sealed class TableAction
{
    public string ActionType { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
    public int TurnNumber { get; init; }
    public int? TileId { get; init; }
    public string Detail { get; init; } = string.Empty;
    public DateTime OccurredUtc { get; init; }
}

public sealed class TableGameState
{
    public int ActiveSeat { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int DrawNumber { get; set; }
    public TableTurnPhase Phase { get; set; } = TableTurnPhase.AwaitingDiscard;
    public TableStateMetadata Metadata { get; set; } = new();
    public List<int> Wall { get; set; } = [];
    public List<TableSeatState> Seats { get; set; } = [];
    public List<TableSeatHandState> Hands { get; set; } = [];
    public List<TableDiscard> DiscardPile { get; set; } = [];
    public List<TableAction> ActionLog { get; set; } = [];
}

public enum BotAdvanceStopReason
{
    HumanTurn,
    MaxActionsReached,
    WallExhausted
}

public sealed class BotAdvanceResult
{
    public required IReadOnlyList<TableAction> Actions { get; init; }
    public required BotAdvanceStopReason StopReason { get; init; }
}
