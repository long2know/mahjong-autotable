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
}
