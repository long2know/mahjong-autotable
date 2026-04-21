using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Tables;

public interface ITableSessionEventStore
{
    Task PersistNewEventsAsync(TableSession session, TableGameState state, CancellationToken cancellationToken);
    Task<IReadOnlyList<TableSessionEvent>> GetEventsAsync(
        Guid tableSessionId,
        long? afterSequence,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class TableSessionEventStore(AppDbContext db) : ITableSessionEventStore
{
    public async Task PersistNewEventsAsync(
        TableSession session,
        TableGameState state,
        CancellationToken cancellationToken)
    {
        if (state.ActionLog.Count == 0)
        {
            return;
        }

        var lastPersistedSequence = await db.TableSessionEvents
            .Where(evt => evt.TableSessionId == session.Id)
            .Select(evt => (long?)evt.Sequence)
            .MaxAsync(cancellationToken)
            ?? 0;

        var pendingActions = state.ActionLog
            .Where(action => action.Sequence > lastPersistedSequence)
            .OrderBy(action => action.Sequence)
            .ToList();

        if (pendingActions.Count == 0)
        {
            return;
        }

        var persistedUtc = DateTime.UtcNow;
        foreach (var action in pendingActions)
        {
            db.TableSessionEvents.Add(new TableSessionEvent
            {
                TableSessionId = session.Id,
                Sequence = action.Sequence,
                ActionType = action.ActionType,
                SeatIndex = action.SeatIndex,
                TurnNumber = action.TurnNumber,
                TileId = action.TileId,
                Detail = action.Detail,
                StateVersion = ResolveStateVersion(state, action.Sequence),
                StateHash = string.IsNullOrWhiteSpace(action.StateHash)
                    ? state.Integrity.StateHash
                    : action.StateHash,
                OccurredUtc = action.OccurredUtc,
                PersistedUtc = persistedUtc
            });
        }
    }

    public async Task<IReadOnlyList<TableSessionEvent>> GetEventsAsync(
        Guid tableSessionId,
        long? afterSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 500);
        var query = db.TableSessionEvents
            .AsNoTracking()
            .Where(evt => evt.TableSessionId == tableSessionId);

        if (afterSequence.HasValue)
        {
            query = query.Where(evt => evt.Sequence > afterSequence.Value);
        }

        return await query
            .OrderBy(evt => evt.Sequence)
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);
    }

    private static int ResolveStateVersion(TableGameState state, long actionSequence)
    {
        var resolved = (long)state.StateVersion - state.ActionSequence + actionSequence;
        if (resolved is <= 0 or > int.MaxValue)
        {
            return state.StateVersion;
        }

        return (int)resolved;
    }
}
