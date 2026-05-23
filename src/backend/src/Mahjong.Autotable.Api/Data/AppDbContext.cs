using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Players;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

// Phase J Wave 7 — Apone (DevOps). Constructor accepts the non-generic
// `DbContextOptions` so provider-specific subclasses (SqliteAppDbContext,
// PostgresAppDbContext, SqlServerAppDbContext under Persistence/) can
// forward their own typed options without re-implementing the model.
// `DbContextOptions<TContext>` derives from `DbContextOptions`, so
// `services.AddDbContext<AppDbContext>(...)` (which passes the typed
// variant) still binds without any DI-side change.
public class AppDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ChangshaGame> ChangshaGames => Set<ChangshaGame>();
    public DbSet<ChangshaGameEvent> ChangshaGameEvents => Set<ChangshaGameEvent>();

    // Phase J Wave 5 — persistent per-player profile + stats. See
    // Mahjong.Autotable.Api.Players.PlayerProfileService.
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();

    // Phase J Wave 7 — completed-game replay snapshots. See
    // Mahjong.Autotable.Api.Changsha.Runtime.ChangshaGameRuntime.EmitGameCompletedAsync
    // (write path) and the GET /api/games/{gameId}/replay endpoint in Program.cs
    // (read path).
    public DbSet<ChangshaGameReplay> ChangshaGameReplays => Set<ChangshaGameReplay>();

    // Phase J Wave 8 — server-driven rule presets + OAuth/passwordless auth.
    // See Mahjong.Autotable.Api.Auth.AuthCookieService + RulePresetService.
    public DbSet<ChangshaRulePreset> ChangshaRulePresets => Set<ChangshaRulePreset>();
    public DbSet<PlayerAuthIdentity> PlayerAuthIdentities => Set<PlayerAuthIdentity>();
    public DbSet<EmailMagicLinkToken> EmailMagicLinkTokens => Set<EmailMagicLinkToken>();
    public DbSet<PlayerAuthSession> PlayerAuthSessions => Set<PlayerAuthSession>();

    // Phase J Wave 9 — Content-Security-Policy violation reports. Append-only.
    // See Mahjong.Autotable.Api.Observability.CspReportEndpoint.
    public DbSet<CspViolation> CspViolations => Set<CspViolation>();

    // Phase J Wave 9 — reconnect token rotation + audit, plus persisted
    // chat backlog. See ReconnectTokenService, ChatService, and the
    // GET /api/games/{gameId}/chat endpoint.
    public DbSet<ReconnectToken> ReconnectTokens => Set<ReconnectToken>();
    public DbSet<ReconnectAuditEntry> ReconnectAuditEntries => Set<ReconnectAuditEntry>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Phase J Wave 10 — Tournament mode (Bishop). See
    // Mahjong.Autotable.Api.Tournament.TournamentService + the
    // /api/tournaments REST surface in TournamentController.
    // Fully qualified to disambiguate from the sibling Tournament namespace.
    public DbSet<Mahjong.Autotable.Api.Data.Entities.Tournament> Tournaments => Set<Mahjong.Autotable.Api.Data.Entities.Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();

    // Phase K Wave 1 — match-history denormalization + Elo-rating tables
    // (Bishop). See Mahjong.Autotable.Api.Players.PlayerGameHistoryService
    // (writer at game completion) + the GET /api/games match-history
    // endpoint; Mahjong.Autotable.Api.Tournament.PlayerRatingService
    // (writer at tournament-match completion) + the
    // GET /api/ratings/leaderboard endpoint.
    public DbSet<PlayerGameHistory> PlayerGameHistory => Set<PlayerGameHistory>();
    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();
    public DbSet<PlayerRatingHistory> PlayerRatingHistory => Set<PlayerRatingHistory>();

    // Phase K Wave 2 — quarter-boundary deferral table (Bishop). Written
    // by SeasonRolloverService when a player is mid-tournament; drained
    // when the tournament completes. See
    // Mahjong.Autotable.Api.Tournament.SeasonRolloverService.
    public DbSet<PlayerSeasonRolloverDeferral> PlayerSeasonRolloverDeferrals => Set<PlayerSeasonRolloverDeferral>();

    // Phase K Wave 3 — Bishop. Server-authoritative onboarding tour
    // progress. Persisted via the
    // GET/POST /api/players/me/onboarding-status endpoints. See
    // Mahjong.Autotable.Api.Players.PlayerOnboardingService.
    public DbSet<PlayerOnboardingStatus> PlayerOnboardingStatuses => Set<PlayerOnboardingStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChangshaGame>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleSet).HasMaxLength(50);
            // Phase J Wave 7 — Apone. No explicit `HasColumnType` so the
            // unbounded text column maps to the provider-native type:
            // Sqlite=`TEXT`, Postgres=`text`, SQL Server=`nvarchar(max)`.
            // SqlServer's legacy `TEXT` is deprecated, so this override
            // was removed to keep the multi-provider migration set clean.
            entity.Property(x => x.StateVersion).HasDefaultValue(1);
            // Phase K Wave 3 — Bishop. Owner-of-the-table column (Wave 3
            // brief task 3): table creator's persistent PlayerId, gates
            // /api/games/{id}/settings/voice.
            entity.Property(x => x.OwnerPlayerId).HasMaxLength(128);
            // Phase K Wave 3 — Bishop. Per-table voice toggle. Default
            // false so existing/legacy rows backfill correctly.
            entity.Property(x => x.VoiceEnabled).HasDefaultValue(false);
        });

        modelBuilder.Entity<ChangshaGameEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(64);
            entity.Property(x => x.Detail).HasMaxLength(256);
            entity.HasIndex(x => new { x.GameId, x.Sequence }).IsUnique();
            entity.HasOne<ChangshaGame>()
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.HasKey(x => x.PlayerId);
            entity.Property(x => x.PlayerId).HasMaxLength(128);
            entity.Property(x => x.DisplayName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AvatarColor).HasMaxLength(7).IsRequired();
        });

        modelBuilder.Entity<PlayerStats>(entity =>
        {
            entity.HasKey(x => x.PlayerId);
            entity.Property(x => x.PlayerId).HasMaxLength(128);
            entity.HasOne<PlayerProfile>()
                .WithOne()
                .HasForeignKey<PlayerStats>(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Phase J Wave 7 — replay snapshot table. Single row per completed
        // game, keyed by a synthetic Id but uniquely indexed on GameId so
        // re-completion (e.g. hydration after a crash) upserts cleanly.
        // EventsJson holds the serialised play-by-play (no length cap)
        // because end-game replays of long N-hand matches can run into
        // hundreds of KB.
        //
        // Note: no FK to ChangshaGames. Replays are completed-game
        // historical artifacts that should outlive the parent game row
        // (e.g. a future cleanup that purges old ChangshaGames must not
        // cascade-drop the replay). The unique GameId index supports the
        // /api/games/{gameId}/replay lookup directly.
        modelBuilder.Entity<ChangshaGameReplay>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventsJson).IsRequired();
            // Phase J Wave 9 — schema version column. Default 1 so legacy
            // v1 replays (Wave 7/8) keep their implicit version after the
            // migration runs; new writes stamp
            // ChangshaGameReplay.CurrentSchemaVersion (=2).
            entity.Property(x => x.SchemaVersion).HasDefaultValue(1);
            entity.HasIndex(x => x.GameId).IsUnique();
        });

        modelBuilder.Entity<ChangshaRulePreset>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.CreatorPlayerId).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<PlayerAuthIdentity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ProviderSubject).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique();
            entity.HasIndex(x => x.PlayerId);
            entity.HasOne<PlayerProfile>()
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailMagicLinkToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RequestedPlayerId).HasMaxLength(128);
            entity.HasIndex(x => x.Token).IsUnique();
        });

        modelBuilder.Entity<PlayerAuthSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            // Phase J Wave 9 — role stamp for the audit endpoint admin gate.
            // Nullable; null = ordinary player. See AuthCookieService.
            entity.Property(x => x.Role).HasMaxLength(32);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.PlayerId);
        });

        // Phase J Wave 9 — reconnect token rotation. Token column is the
        // unique opaque value (64-char URL-safe base64); a unique index
        // lets the rotation service look the row up in O(log n) on every
        // ReconnectGame call. The (PlayerId, GameId) composite index
        // supports admin audit drill-downs ("show me every rotation for
        // player X in game Y") without a full scan.
        modelBuilder.Entity<ReconnectToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.GameId).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.PlayerId, x.GameId });
        });

        // Phase J Wave 9 — append-only audit log for reconnect-token
        // rotations. PlayerId index for per-player drill-downs;
        // At index for time-window queries from the audit endpoint.
        // Phase K Wave 2 — generalised via the Kind classifier; the new
        // (Kind, At) composite supports operator queries that filter by
        // event class first (e.g. "all tournament forfeits in the last
        // hour"). Existing per-player + per-At indexes stay so the
        // Wave-9 drill-down queries keep their plans.
        modelBuilder.Entity<ReconnectAuditEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Ipv4Hash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserAgentHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Kind)
                .HasMaxLength(64)
                .IsRequired()
                .HasDefaultValue(ReconnectAuditEntry.KindReconnectTokenRotated);
            entity.Property(x => x.Detail).HasMaxLength(256);
            // Phase K Wave 8 — Bishop. Idempotency-Key + CorrelationId
            // enrichment. Both columns are nullable (pre-Wave-8 rows
            // back-fill clean) and the indexes are non-unique because
            // a single key may legitimately appear multiple times (the
            // first hit + the rejection rows under the same key both
            // share the value).
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.Property(x => x.CorrelationId).HasMaxLength(64);
            entity.HasIndex(x => x.PlayerId);
            entity.HasIndex(x => x.At);
            entity.HasIndex(x => new { x.Kind, x.At });
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.IdempotencyKey);
        });

        // Phase J Wave 9 — persisted chat backlog. (GameId, At) composite
        // index supports the lazy backfill endpoint
        // GET /api/games/{gameId}/chat?since=<ts>&limit=50 directly.
        // Body capped at 512 (vs the 280-char hub validation cap) to
        // allow future emoji-padded payloads without a schema bump.
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GameId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.GameId, x.At });
        });

        // Phase J Wave 9 — CSP violation reports (Apone, DevOps). Append-only;
        // index ReceivedAt for time-window queries from the operator dashboard.
        // No FK to PlayerProfiles because reports often arrive from anonymous
        // pre-cookie callers. RawJson holds the unparsed envelope as forensic
        // backup; the parsed columns above just accelerate aggregation.
        modelBuilder.Entity<CspViolation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128);
            entity.Property(x => x.DocumentUri).HasMaxLength(2048);
            entity.Property(x => x.Referrer).HasMaxLength(2048);
            entity.Property(x => x.ViolatedDirective).HasMaxLength(128);
            entity.Property(x => x.EffectiveDirective).HasMaxLength(128);
            entity.Property(x => x.OriginalPolicy).HasMaxLength(4096);
            entity.Property(x => x.Disposition).HasMaxLength(16);
            entity.Property(x => x.BlockedUri).HasMaxLength(2048);
            entity.Property(x => x.SourceFile).HasMaxLength(2048);
            entity.Property(x => x.ScriptSample).HasMaxLength(256);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.RawJson).IsRequired();
            entity.HasIndex(x => x.ReceivedAt);
            entity.HasIndex(x => x.EffectiveDirective);
        });

        // Phase J Wave 10 — Tournament shell. Name has a soft index for
        // the listing endpoint's name-prefix filter (future); Status +
        // CreatedAt index supports the canonical `GET /api/tournaments?status=`
        // query used by the lobby.
        modelBuilder.Entity<Mahjong.Autotable.Api.Data.Entities.Tournament>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Format).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedByPlayerId).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
        });

        // Phase J Wave 10 — registration row. (TournamentId, PlayerId)
        // unique index prevents double-registration; the FK cascades on
        // tournament deletion (operator can drop a tournament wholesale
        // during draft → registrations evaporate with it).
        modelBuilder.Entity<TournamentRegistration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.TournamentId, x.PlayerId }).IsUnique();
            entity.HasIndex(x => x.TournamentId);
            entity.HasOne<Mahjong.Autotable.Api.Data.Entities.Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Phase J Wave 10 — pairing row. (TournamentId, Round) index
        // supports the leaderboard query's grouping by round. GameIdsJson
        // stored as a plain string column (no value converter) — the
        // service serialises/deserialises manually so we avoid the
        // change-tracker quirk where EF Core can't diff a List<Guid>
        // against itself reliably under JSON-column semantics.
        modelBuilder.Entity<TournamentMatch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Player1Id).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Player2Id).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Player3Id).HasMaxLength(128);
            entity.Property(x => x.Player4Id).HasMaxLength(128);
            entity.Property(x => x.WinnerPlayerId).HasMaxLength(128);
            entity.Property(x => x.GameIdsJson).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ForfeitedPlayerId).HasMaxLength(128);
            entity.HasIndex(x => new { x.TournamentId, x.Round });
            entity.HasIndex(x => x.TournamentId);
            entity.HasOne<Mahjong.Autotable.Api.Data.Entities.Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Phase K Wave 1 — per-player game-history denormalization
        // (Bishop). Written at game completion by
        // PlayerGameHistoryService.RecordAsync (game runtime hook); read
        // by GET /api/games?playerId=. The (PlayerId, CompletedAt) index
        // backs the canonical paging query; the unique (PlayerId, GameId)
        // pair keeps re-completion idempotent (matches the existing
        // ChangshaGameReplays.GameId-unique upsert posture).
        modelBuilder.Entity<PlayerGameHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.OpponentPlayerIdsCsv).HasMaxLength(1024).IsRequired();
            entity.HasIndex(x => new { x.PlayerId, x.CompletedAt });
            entity.HasIndex(x => new { x.PlayerId, x.GameId }).IsUnique();
        });

        // Phase K Wave 1 — per-(player, season) Elo rating (Bishop).
        // Updated by PlayerRatingService at tournament-match completion;
        // surfaced via GET /api/ratings/leaderboard. The unique
        // (PlayerId, Season) constraint guarantees one live row per
        // season; the (Season, EloRating desc) index supports the
        // leaderboard sort.
        modelBuilder.Entity<PlayerRating>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Season).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.PlayerId, x.Season }).IsUnique();
            entity.HasIndex(x => x.Season);
        });

        // Phase K Wave 1 — frozen prior-season snapshot (Bishop).
        // SeasonRolloverService copies live PlayerRatings rows here at
        // each quarter boundary then resets the live row. Unique
        // (PlayerId, Season) keeps the snapshot table idempotent on
        // re-runs (the service guards against double-freeze).
        modelBuilder.Entity<PlayerRatingHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Season).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.PlayerId, x.Season }).IsUnique();
            entity.HasIndex(x => x.Season);
        });

        // Phase K Wave 2 — quarter-boundary rollover deferral. Written
        // when a quarter flips while the player is mid-tournament; the
        // (TournamentId, ResolvedAtUtc) composite index supports the
        // "what's still pending for this tournament" lookup performed
        // when the tournament completes. (PlayerId, FromSeasonId,
        // TournamentId) unique keeps the recorder idempotent on re-runs.
        //
        // Phase K Wave 3 — Bishop renamed the season fields to
        // FromSeasonId/ToSeasonId and DrainedAtUtc → ResolvedAtUtc per
        // Vasquez's Wave-2 contract-gap memo (fix #5). Indices were
        // re-named in lockstep with the migration.
        modelBuilder.Entity<PlayerSeasonRolloverDeferral>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlayerId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FromSeasonId).HasMaxLength(16).IsRequired();
            entity.Property(x => x.ToSeasonId).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.PlayerId, x.FromSeasonId, x.TournamentId }).IsUnique();
            entity.HasIndex(x => new { x.TournamentId, x.ResolvedAtUtc });
        });

        // Phase K Wave 3 — Bishop. Onboarding tour progress. Single row
        // per player; PlayerId is the PK so the GET/POST contract is a
        // direct upsert. No FK to PlayerProfiles because the row may
        // exist briefly before the profile is fully resolved on the
        // first /api/identity round-trip; the soft pairing keeps the
        // surface tolerant.
        modelBuilder.Entity<PlayerOnboardingStatus>(entity =>
        {
            entity.HasKey(x => x.PlayerId);
            entity.Property(x => x.PlayerId).HasMaxLength(128);
        });
    }
}
