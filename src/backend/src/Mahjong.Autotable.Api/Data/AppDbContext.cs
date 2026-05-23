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
            entity.HasIndex(x => x.GameId).IsUnique();
        });
    }
}
