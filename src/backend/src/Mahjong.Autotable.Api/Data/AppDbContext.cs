using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TableSession> TableSessions => Set<TableSession>();
    public DbSet<TableSessionEvent> TableSessionEvents => Set<TableSessionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TableSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleSet).HasMaxLength(50);
            entity.Property(x => x.StateJson).HasColumnType("TEXT");
            entity.Property(x => x.StateVersion).HasDefaultValue(1);
        });

        modelBuilder.Entity<TableSessionEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionType).HasMaxLength(32);
            entity.Property(x => x.Detail).HasMaxLength(128);
            entity.Property(x => x.StateHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.TableSessionId, x.Sequence }).IsUnique();
            entity.HasOne<TableSession>()
                .WithMany()
                .HasForeignKey(x => x.TableSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
