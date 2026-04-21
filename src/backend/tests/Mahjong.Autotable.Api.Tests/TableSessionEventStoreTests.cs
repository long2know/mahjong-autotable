using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tables;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Tests;

public class TableSessionEventStoreTests
{
    [Fact]
    public async Task PersistNewEventsAsync_PersistsOnlyNewEvents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await DatabaseBootstrapper.InitializeAsync(db);

        var engine = new TableStateEngine();
        var serializer = new TableStateSerializer();
        var eventStore = new TableSessionEventStore(db);

        var state = engine.CreateInitialState(seed: 7001);
        var session = new TableSession
        {
            StateJson = serializer.Serialize(state),
            StateVersion = state.StateVersion,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        db.TableSessions.Add(session);
        await eventStore.PersistNewEventsAsync(session, state, CancellationToken.None);
        await db.SaveChangesAsync();

        var tileId = state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Min();
        var result = engine.ApplyHumanDiscard(state, 0, tileId);
        session.StateJson = serializer.Serialize(state);
        session.StateVersion = state.StateVersion;
        session.UpdatedUtc = DateTime.UtcNow;
        session.LastActionUtc = result.DrawAction?.OccurredUtc ?? result.DiscardAction.OccurredUtc;

        await eventStore.PersistNewEventsAsync(session, state, CancellationToken.None);
        await db.SaveChangesAsync();

        var events = await db.TableSessionEvents
            .Where(evt => evt.TableSessionId == session.Id)
            .OrderBy(evt => evt.Sequence)
            .ToListAsync();

        Assert.Equal(2, events.Count);
        Assert.Equal(result.DiscardAction.Sequence, events[0].Sequence);
        Assert.Equal(result.DrawAction!.Sequence, events[1].Sequence);
        Assert.Equal(result.DiscardAction.StateHash, events[0].StateHash);
        Assert.Equal(result.DrawAction.StateHash, events[1].StateHash);

        await eventStore.PersistNewEventsAsync(session, state, CancellationToken.None);
        await db.SaveChangesAsync();

        var persistedCount = await db.TableSessionEvents.CountAsync(evt => evt.TableSessionId == session.Id);
        Assert.Equal(2, persistedCount);
    }

    [Fact]
    public async Task GetEventsAsync_AppliesSequenceFilterAndLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await DatabaseBootstrapper.InitializeAsync(db);

        var engine = new TableStateEngine();
        var serializer = new TableStateSerializer();
        var eventStore = new TableSessionEventStore(db);

        var state = engine.CreateInitialState(seed: 7002);
        var session = new TableSession
        {
            StateJson = serializer.Serialize(state),
            StateVersion = state.StateVersion,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        db.TableSessions.Add(session);
        await db.SaveChangesAsync();

        var tileId = state.Hands.Single(hand => hand.SeatIndex == 0).Tiles.Min();
        _ = engine.ApplyHumanDiscard(state, 0, tileId);
        _ = engine.AdvanceBots(state, 4);

        session.StateJson = serializer.Serialize(state);
        session.StateVersion = state.StateVersion;
        session.UpdatedUtc = DateTime.UtcNow;
        session.LastActionUtc = state.ActionLog[^1].OccurredUtc;

        await eventStore.PersistNewEventsAsync(session, state, CancellationToken.None);
        await db.SaveChangesAsync();

        var firstTwo = await eventStore.GetEventsAsync(session.Id, afterSequence: null, limit: 2, CancellationToken.None);
        Assert.Equal(2, firstTwo.Count);
        Assert.Equal(1, firstTwo[0].Sequence);
        Assert.Equal(2, firstTwo[1].Sequence);

        var remaining = await eventStore.GetEventsAsync(session.Id, afterSequence: 2, limit: 20, CancellationToken.None);
        Assert.NotEmpty(remaining);
        Assert.All(remaining, evt => Assert.True(evt.Sequence > 2));
        Assert.Equal((int)(state.ActionSequence - 2), remaining.Count);
    }
}
