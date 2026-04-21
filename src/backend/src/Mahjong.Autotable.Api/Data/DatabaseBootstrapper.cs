using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (db.Database.IsSqlite())
        {
            await EnsureSqliteTableSessionColumnsAsync(db, cancellationToken);
            await EnsureSqliteTableSessionEventsTableAsync(db, cancellationToken);
        }
    }

    private static async Task EnsureSqliteTableSessionColumnsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('TableSessions');";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            if (!existingColumns.Contains("StateVersion"))
            {
                await using var alterStateVersion = connection.CreateCommand();
                alterStateVersion.CommandText =
                    "ALTER TABLE \"TableSessions\" ADD COLUMN \"StateVersion\" INTEGER NOT NULL DEFAULT 1;";
                await alterStateVersion.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!existingColumns.Contains("LastActionUtc"))
            {
                await using var alterLastAction = connection.CreateCommand();
                alterLastAction.CommandText =
                    "ALTER TABLE \"TableSessions\" ADD COLUMN \"LastActionUtc\" TEXT NULL;";
                await alterLastAction.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (closeWhenDone)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureSqliteTableSessionEventsTableAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var createTable = connection.CreateCommand())
            {
                createTable.CommandText = """
                    CREATE TABLE IF NOT EXISTS "TableSessionEvents" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_TableSessionEvents" PRIMARY KEY AUTOINCREMENT,
                        "TableSessionId" TEXT NOT NULL,
                        "Sequence" INTEGER NOT NULL,
                        "ActionType" TEXT NOT NULL,
                        "SeatIndex" INTEGER NOT NULL,
                        "TurnNumber" INTEGER NOT NULL,
                        "TileId" INTEGER NULL,
                        "Detail" TEXT NOT NULL,
                        "StateVersion" INTEGER NOT NULL,
                        "StateHash" TEXT NOT NULL,
                        "OccurredUtc" TEXT NOT NULL,
                        "PersistedUtc" TEXT NOT NULL,
                        CONSTRAINT "FK_TableSessionEvents_TableSessions_TableSessionId" FOREIGN KEY ("TableSessionId") REFERENCES "TableSessions" ("Id") ON DELETE CASCADE
                    );
                    """;
                await createTable.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var createIndex = connection.CreateCommand();
            createIndex.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TableSessionEvents_TableSessionId_Sequence"
                ON "TableSessionEvents" ("TableSessionId", "Sequence");
                """;
            await createIndex.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeWhenDone)
            {
                await connection.CloseAsync();
            }
        }
    }
}
