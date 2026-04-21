using Mahjong.Autotable.Api.Data.Entities;

namespace Mahjong.Autotable.Api.Tables;

public sealed record CreateTableRequest(
    string? RuleSet = null,
    IReadOnlyList<int>? BotSeatIndexes = null,
    int? Seed = null);

public sealed record AdvanceBotsRequest(int MaxActions = 8);

public sealed record DiscardActionRequest(int SeatIndex, int TileId, int? ExpectedStateVersion = null);

public sealed record TableDto(
    Guid Id,
    string RuleSet,
    int StateVersion,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? LastActionUtc,
    TableGameState State);

public sealed record AdvanceBotsResponse(
    TableDto Table,
    IReadOnlyList<TableAction> Actions,
    BotAdvanceStopReason StopReason);

public sealed record DiscardActionResponse(
    TableDto Table,
    TableAction DiscardAction,
    TableAction? DrawAction);

public sealed record TableActionError(
    string Code,
    string Message,
    int StateVersion,
    long ActionSequence,
    string CorrelationId);

public sealed record ReplayVerificationResponse(
    TableDto Table,
    bool IntegrityMatch,
    string ExpectedStateHash,
    string ReplayedStateHash,
    int ReplayedStateVersion,
    long ReplayedActionSequence);

public sealed record TableEventDto(
    long Sequence,
    string ActionType,
    int SeatIndex,
    int TurnNumber,
    int? TileId,
    string Detail,
    int StateVersion,
    string StateHash,
    DateTime OccurredUtc,
    DateTime PersistedUtc);

public sealed record TableEventsResponse(
    Guid TableId,
    int StateVersion,
    long ActionSequence,
    IReadOnlyList<TableEventDto> Events);

public static class TableMappings
{
    public static TableDto ToDto(this TableSession session, TableGameState state) =>
        new(
            session.Id,
            session.RuleSet,
            state.StateVersion,
            session.CreatedUtc,
            session.UpdatedUtc,
            session.LastActionUtc,
            state);

    public static TableEventDto ToDto(this TableSessionEvent evt) =>
        new(
            evt.Sequence,
            evt.ActionType,
            evt.SeatIndex,
            evt.TurnNumber,
            evt.TileId,
            evt.Detail,
            evt.StateVersion,
            evt.StateHash,
            evt.OccurredUtc,
            evt.PersistedUtc);
}
