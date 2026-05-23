using System.Data;
using Mahjong.Autotable.Api.Data.Entities;
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
            // Phase J Wave 8 — auth identity + rule presets. The canonical
            // schema is the AddAuthAndRulePresets EF migration; this guard
            // bootstraps existing-prod SQLite databases that pre-date Wave 8
            // without requiring an out-of-band `dotnet ef database update`.
            await EnsureSqliteWave8TablesAsync(db, cancellationToken);
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

        // Phase J Wave 8 — seed the "Classic Changsha" default preset on
        // every provider once the schema is up. Idempotent: the upsert is
        // gated on the canonical preset id so repeated boot cycles never
        // create duplicates and never overwrite a manually-tuned row that
        // an operator may have edited.
        await SeedClassicChangshaPresetAsync(db, cancellationToken);
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

    /// <summary>
    /// Phase J Wave 8 — defensive SQLite-only bootstrap for the new auth +
    /// rule-preset tables: ChangshaRulePresets, PlayerAuthIdentities,
    /// EmailMagicLinkTokens, PlayerAuthSessions. Also adds the nullable
    /// <c>RulePresetId</c> column to ChangshaGames when the column doesn't
    /// already exist (PRAGMA table_info probe). Mirrors the
    /// AddAuthAndRulePresets EF migration so existing prod SQLite DBs pick
    /// up the new schema without an out-of-band migration sweep.
    /// </summary>
    private static async Task EnsureSqliteWave8TablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var createPresets = connection.CreateCommand())
            {
                createPresets.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ChangshaRulePresets" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ChangshaRulePresets" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Description" TEXT NOT NULL DEFAULT '',
                        "HandLimit" INTEGER NOT NULL DEFAULT 4,
                        "MaxScorePerHand" INTEGER NOT NULL DEFAULT 0,
                        "AllowWashout" INTEGER NOT NULL DEFAULT 1,
                        "AllowKongRobbing" INTEGER NOT NULL DEFAULT 1,
                        "AllowConcealedKongPromotion" INTEGER NOT NULL DEFAULT 1,
                        "AllowSevenPairs" INTEGER NOT NULL DEFAULT 1,
                        "AllowChow" INTEGER NOT NULL DEFAULT 1,
                        "BotDecisionTimeoutMs" INTEGER NOT NULL DEFAULT 2000,
                        "CreatorPlayerId" TEXT NOT NULL DEFAULT 'system',
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    """;
                await createPresets.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxPresets = connection.CreateCommand())
            {
                idxPresets.CommandText = """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChangshaRulePresets_Name"
                    ON "ChangshaRulePresets" ("Name");
                    """;
                await idxPresets.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createIdentities = connection.CreateCommand())
            {
                createIdentities.CommandText = """
                    CREATE TABLE IF NOT EXISTS "PlayerAuthIdentities" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_PlayerAuthIdentities" PRIMARY KEY,
                        "PlayerId" TEXT NOT NULL,
                        "Provider" TEXT NOT NULL,
                        "ProviderSubject" TEXT NOT NULL,
                        "Email" TEXT NULL,
                        "EmailVerified" INTEGER NOT NULL DEFAULT 0,
                        "CreatedAt" TEXT NOT NULL,
                        "LastUsedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_PlayerAuthIdentities_PlayerProfiles_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "PlayerProfiles" ("PlayerId") ON DELETE CASCADE
                    );
                    """;
                await createIdentities.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxIdentitiesProv = connection.CreateCommand())
            {
                idxIdentitiesProv.CommandText = """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerAuthIdentities_Provider_ProviderSubject"
                    ON "PlayerAuthIdentities" ("Provider", "ProviderSubject");
                    """;
                await idxIdentitiesProv.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxIdentitiesPlayer = connection.CreateCommand())
            {
                idxIdentitiesPlayer.CommandText = """
                    CREATE INDEX IF NOT EXISTS "IX_PlayerAuthIdentities_PlayerId"
                    ON "PlayerAuthIdentities" ("PlayerId");
                    """;
                await idxIdentitiesPlayer.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createTokens = connection.CreateCommand())
            {
                createTokens.CommandText = """
                    CREATE TABLE IF NOT EXISTS "EmailMagicLinkTokens" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_EmailMagicLinkTokens" PRIMARY KEY,
                        "Token" TEXT NOT NULL,
                        "Email" TEXT NOT NULL,
                        "RequestedPlayerId" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "ExpiresAt" TEXT NOT NULL,
                        "ConsumedAt" TEXT NULL
                    );
                    """;
                await createTokens.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxTokens = connection.CreateCommand())
            {
                idxTokens.CommandText = """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailMagicLinkTokens_Token"
                    ON "EmailMagicLinkTokens" ("Token");
                    """;
                await idxTokens.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createSessions = connection.CreateCommand())
            {
                createSessions.CommandText = """
                    CREATE TABLE IF NOT EXISTS "PlayerAuthSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_PlayerAuthSessions" PRIMARY KEY,
                        "Token" TEXT NOT NULL,
                        "PlayerId" TEXT NOT NULL,
                        "IdentityId" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "ExpiresAt" TEXT NOT NULL,
                        "LastUsedAt" TEXT NOT NULL
                    );
                    """;
                await createSessions.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxSessionsToken = connection.CreateCommand())
            {
                idxSessionsToken.CommandText = """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerAuthSessions_Token"
                    ON "PlayerAuthSessions" ("Token");
                    """;
                await idxSessionsToken.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var idxSessionsPlayer = connection.CreateCommand())
            {
                idxSessionsPlayer.CommandText = """
                    CREATE INDEX IF NOT EXISTS "IX_PlayerAuthSessions_PlayerId"
                    ON "PlayerAuthSessions" ("PlayerId");
                    """;
                await idxSessionsPlayer.ExecuteNonQueryAsync(cancellationToken);
            }

            // Add RulePresetId column to ChangshaGames if missing. PRAGMA
            // table_info returns one row per column; we scan for the name
            // and only ALTER when it isn't there. SQLite has no
            // ADD-COLUMN-IF-NOT-EXISTS so this probe-then-add pattern is
            // standard.
            var hasRulePresetId = false;
            await using (var probe = connection.CreateCommand())
            {
                probe.CommandText = "PRAGMA table_info(\"ChangshaGames\");";
                await using var reader = await probe.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    // Column name is index 1 in PRAGMA table_info output.
                    if (reader.GetString(1).Equals("RulePresetId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRulePresetId = true;
                        break;
                    }
                }
            }
            if (!hasRulePresetId)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE \"ChangshaGames\" ADD COLUMN \"RulePresetId\" TEXT NULL;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
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
    /// Phase J Wave 8 — seeds the canonical "Classic Changsha" preset row.
    /// Idempotent: keyed off <see cref="ChangshaRulePreset.ClassicPresetId"/>
    /// so repeated boots are no-ops and don't overwrite hand-edited values.
    /// Runs on every provider once the table exists.
    /// </summary>
    private static async Task SeedClassicChangshaPresetAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var canonicalId = Guid.Parse(ChangshaRulePreset.ClassicPresetId);
        var exists = await db.ChangshaRulePresets.AnyAsync(p => p.Id == canonicalId, cancellationToken);
        if (exists) return;

        db.ChangshaRulePresets.Add(new ChangshaRulePreset
        {
            Id = canonicalId,
            Name = "Classic Changsha",
            Description = "Standard Changsha Mahjong house rules.",
            HandLimit = 4,
            MaxScorePerHand = 0,
            AllowWashout = true,
            AllowKongRobbing = true,
            AllowConcealedKongPromotion = true,
            AllowSevenPairs = true,
            AllowChow = true,
            BotDecisionTimeoutMs = 2000,
            CreatorPlayerId = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
