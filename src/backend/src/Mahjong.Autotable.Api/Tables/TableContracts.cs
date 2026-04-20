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
}
