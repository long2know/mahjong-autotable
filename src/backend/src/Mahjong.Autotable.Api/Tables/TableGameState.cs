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

public sealed class TableAction
{
    public string ActionType { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
    public int TurnNumber { get; init; }
    public string Detail { get; init; } = string.Empty;
    public DateTime OccurredUtc { get; init; }
}

public sealed class TableGameState
{
    public int ActiveSeat { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int DrawNumber { get; set; }
    public List<TableSeatState> Seats { get; init; } = [];
    public List<TableAction> ActionLog { get; init; } = [];
}

public enum BotAdvanceStopReason
{
    HumanTurn,
    MaxActionsReached
}

public sealed class BotAdvanceResult
{
    public required IReadOnlyList<TableAction> Actions { get; init; }
    public required BotAdvanceStopReason StopReason { get; init; }
}
