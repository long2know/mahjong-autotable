using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Players;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ChangshaGame> ChangshaGames => Set<ChangshaGame>();
    public DbSet<ChangshaGameEvent> ChangshaGameEvents => Set<ChangshaGameEvent>();

    // Phase J Wave 5 — persistent per-player profile + stats. See
    // Mahjong.Autotable.Api.Players.PlayerProfileService.
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChangshaGame>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleSet).HasMaxLength(50);
            entity.Property(x => x.StateJson).HasColumnType("TEXT");
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
    }
}
