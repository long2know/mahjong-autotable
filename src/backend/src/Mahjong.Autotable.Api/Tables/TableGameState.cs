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
    public string AlgorithmId { get; set; } = string.Empty;
}

public sealed class TableIntegrityState
{
    public string StateHash { get; set; } = string.Empty;
}

public sealed class TableLastActionState
{
    public long Sequence { get; set; }
    public int SeatIndex { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
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
    public long Sequence { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public int TurnNumber { get; set; }
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string StateHash { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
}

public sealed class TableGameState
{
    public int StateVersion { get; set; } = 1;
    public long ActionSequence { get; set; }
    public int ActiveSeat { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int DrawNumber { get; set; }
    public TableTurnPhase Phase { get; set; } = TableTurnPhase.AwaitingDiscard;
    public TableStateMetadata Metadata { get; set; } = new();
    public TableIntegrityState Integrity { get; set; } = new();
    public TableLastActionState? LastAction { get; set; }
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
