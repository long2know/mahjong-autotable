using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TableSession> TableSessions => Set<TableSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TableSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleSet).HasMaxLength(50);
            entity.Property(x => x.StateJson).HasColumnType("TEXT");
            entity.Property(x => x.StateVersion).HasDefaultValue(1);
        });
    }
}
