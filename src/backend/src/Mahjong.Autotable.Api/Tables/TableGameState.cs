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
    AwaitingClaimResolution,
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

public enum TableClaimType
{
    Hu,
    Kong,
    Pung,
    Chow
}

public static class TableClaimResolutionDecisionValues
{
    public const string Pass = "pass";
    public const string TakeSelected = "take-selected";
}

public sealed class TableClaimOpportunity
{
    public int SeatIndex { get; init; }
    public TableClaimType ClaimType { get; init; }
    public int Priority { get; init; }
}

public sealed class TableClaimWindowState
{
    public long SourceActionSequence { get; set; }
    public int DiscardSeatIndex { get; set; }
    public int DiscardTileId { get; set; }
    public int DiscardTurnNumber { get; set; }
    public string PrecedencePolicy { get; set; } = string.Empty;
    public List<TableClaimOpportunity> Opportunities { get; set; } = [];
    public TableClaimOpportunity? SelectedOpportunity { get; set; }
}

public sealed class TableMeldState
{
    public TableClaimType ClaimType { get; set; }
    public List<int> TileIds { get; set; } = [];
    public int ClaimedFromSeatIndex { get; set; }
    public int SourceTurnNumber { get; set; }
    public long SourceActionSequence { get; set; }
}

public sealed class TableSeatMeldState
{
    public int SeatIndex { get; set; }
    public List<TableMeldState> Melds { get; set; } = [];
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
    public List<TableSeatMeldState> ExposedMelds { get; set; } = [];
    public List<TableDiscard> DiscardPile { get; set; } = [];
    public TableClaimWindowState? ClaimWindow { get; set; }
    public List<TableAction> ActionLog { get; set; } = [];
}

public enum BotAdvanceStopReason
{
    HumanTurn,
    ClaimResolutionRequired,
    MaxActionsReached,
    WallExhausted
}

public sealed class BotAdvanceResult
{
    public required IReadOnlyList<TableAction> Actions { get; init; }
    public required BotAdvanceStopReason StopReason { get; init; }
}
