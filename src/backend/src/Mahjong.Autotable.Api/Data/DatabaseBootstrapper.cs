using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            // SQLite — dev / single-replica default. EnsureCreated + the
            // CREATE-IF-NOT-EXISTS bootstrap below remain canonical so
            // existing dev DBs (which predate the migration set) keep
            // working without an out-of-band `dotnet ef database update`.
            await db.Database.EnsureCreatedAsync(cancellationToken);
            // Phase A (autotable-vendored pivot): drop the legacy 136-tile
            // TableSessions + TableSessionEvents tables on startup if a
            // pre-pivot SQLite database is still present. The EF Core entities
            // and `/api/tables/*` REST surface have been hard-deleted; only
            // Changsha-native tables remain. This bootstrap drop replaces a
            // formal migration because the project never wired EF Core
            // migrations (it relied on EnsureCreatedAsync + manual ALTERs).
            await DropLegacyTableSessionsAsync(db, cancellationToken);
            await EnsureSqliteChangshaTablesAsync(db, cancellationToken);
            // Phase J Wave 5 — defensive SQLite-only bootstrap for the new
            // PlayerProfiles + PlayerStats tables. The EF Core migration
            // (AddPlayerProfileAndStats) is the canonical schema source; this
            // CREATE-IF-NOT-EXISTS pass keeps existing-DB Sqlite installs
            // working without requiring an out-of-band `dotnet ef database
            // update`, matching the existing Changsha-tables pattern above.
            await EnsureSqlitePlayerTablesAsync(db, cancellationToken);
            // Phase J Wave 7 — same belt-and-braces pattern for the replay
            // snapshot table. Canonical schema is the AddChangshaGameReplay
            // migration; this guard auto-bootstraps the new table on
            // existing prod DBs so the runtime's game-completion hook
            // (ChangshaGameRuntime.EmitGameCompletedAsync) never trips on
            // a missing table.
            await EnsureSqliteReplayTablesAsync(db, cancellationToken);
        }
        else
        {
            // Phase J Wave 7 — Apone (DevOps). Postgres + SqlServer go
            // through the canonical EF Core migration runner so deploys
            // are versioned, rollbackable, and recorded in the
            // `__EFMigrationsHistory` table. The provider-specific
            // migration sets live under Persistence/Migrations/Postgres
            // and Persistence/Migrations/SqlServer; the right one is
            // discovered automatically because `AddPersistence` registers
            // the provider's typed subclass of AppDbContext (the
            // PostgresAppDbContext / SqlServerAppDbContext shells).
            await db.Database.MigrateAsync(cancellationToken);
        }
    }

    private static async Task DropLegacyTableSessionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var dropEvents = connection.CreateCommand();
            dropEvents.CommandText = "DROP TABLE IF EXISTS \"TableSessionEvents\";";
            await dropEvents.ExecuteNonQueryAsync(cancellationToken);

            await using var dropSessions = connection.CreateCommand();
            dropSessions.CommandText = "DROP TABLE IF EXISTS \"TableSessions\";";
            await dropSessions.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeWhenDone)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureSqliteChangshaTablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var createGames = connection.CreateCommand())
            {
                createGames.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ChangshaGames" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ChangshaGames" PRIMARY KEY,
                        "RuleSet" TEXT NOT NULL DEFAULT 'changsha-v1',
                        "Seed" INTEGER NOT NULL,
                        "StateJson" TEXT NOT NULL,
                        "StateVersion" INTEGER NOT NULL DEFAULT 1,
                        "CurrentHandNumber" INTEGER NOT NULL DEFAULT 1,
                        "CurrentRoundNumber" INTEGER NOT NULL DEFAULT 1,
                        "CreatedUtc" TEXT NOT NULL,
                        "UpdatedUtc" TEXT NOT NULL
                    );
                    """;
                await createGames.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createEvents = connection.CreateCommand())
            {
                createEvents.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ChangshaGameEvents" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_ChangshaGameEvents" PRIMARY KEY AUTOINCREMENT,
                        "GameId" TEXT NOT NULL,
                        "Sequence" INTEGER NOT NULL,
                        "EventType" TEXT NOT NULL,
                        "SeatIndex" INTEGER NOT NULL,
                        "TurnNumber" INTEGER NOT NULL,
                        "TileId" INTEGER NULL,
                        "Detail" TEXT NOT NULL,
                        "HandNumber" INTEGER NOT NULL DEFAULT 1,
                        "StateVersion" INTEGER NOT NULL,
                        "OccurredUtc" TEXT NOT NULL,
                        "PersistedUtc" TEXT NOT NULL,
                        CONSTRAINT "FK_ChangshaGameEvents_ChangshaGames_GameId" FOREIGN KEY ("GameId") REFERENCES "ChangshaGames" ("Id") ON DELETE CASCADE
                    );
                    """;
                await createEvents.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var createIndex = connection.CreateCommand();
            createIndex.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChangshaGameEvents_GameId_Sequence"
                ON "ChangshaGameEvents" ("GameId", "Sequence");
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

    private static async Task EnsureSqlitePlayerTablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var createProfiles = connection.CreateCommand())
            {
                createProfiles.CommandText = """
                    CREATE TABLE IF NOT EXISTS "PlayerProfiles" (
                        "PlayerId" TEXT NOT NULL CONSTRAINT "PK_PlayerProfiles" PRIMARY KEY,
                        "DisplayName" TEXT NOT NULL,
                        "AvatarColor" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "LastSeenAt" TEXT NOT NULL
                    );
                    """;
                await createProfiles.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createStats = connection.CreateCommand())
            {
                createStats.CommandText = """
                    CREATE TABLE IF NOT EXISTS "PlayerStats" (
                        "PlayerId" TEXT NOT NULL CONSTRAINT "PK_PlayerStats" PRIMARY KEY,
                        "GamesPlayed" INTEGER NOT NULL DEFAULT 0,
                        "GamesWon" INTEGER NOT NULL DEFAULT 0,
                        "TotalScore" INTEGER NOT NULL DEFAULT 0,
                        "HighestSingleGameScore" INTEGER NOT NULL DEFAULT 0,
                        "LongestWinStreak" INTEGER NOT NULL DEFAULT 0,
                        "CurrentWinStreak" INTEGER NOT NULL DEFAULT 0,
                        "LastGameAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                        CONSTRAINT "FK_PlayerStats_PlayerProfiles_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "PlayerProfiles" ("PlayerId") ON DELETE CASCADE
                    );
                    """;
                await createStats.ExecuteNonQueryAsync(cancellationToken);
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

    /// <summary>
    /// Phase J Wave 7 — defensive SQLite-only bootstrap for the
    /// ChangshaGameReplays table. Mirrors the
    /// <c>AddChangshaGameReplay</c> EF migration so existing prod DBs
    /// running on EnsureCreatedAsync semantics pick up the new table at
    /// startup without an out-of-band migration sweep. The unique GameId
    /// index lets the runtime upsert on completion (rare; only when
    /// hydration re-enters a completed game) without violating PK
    /// uniqueness.
    /// </summary>
    private static async Task EnsureSqliteReplayTablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var createReplays = connection.CreateCommand())
            {
                createReplays.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ChangshaGameReplays" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ChangshaGameReplays" PRIMARY KEY,
                        "GameId" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "EventsJson" TEXT NOT NULL
                    );
                    """;
                await createReplays.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var createIndex = connection.CreateCommand();
            createIndex.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChangshaGameReplays_GameId"
                ON "ChangshaGameReplays" ("GameId");
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
