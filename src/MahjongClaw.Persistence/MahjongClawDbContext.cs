using MahjongClaw.Domain.Rules;
using MahjongClaw.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MahjongClaw.Persistence;

public sealed class MahjongClawDbContext : DbContext
{
    public MahjongClawDbContext(DbContextOptions<MahjongClawDbContext> options)
        : base(options)
    {
    }

    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<TileEntity> Tiles => Set<TileEntity>();
    public DbSet<MeldEntity> Melds => Set<MeldEntity>();
    public DbSet<DiscardEntity> Discards => Set<DiscardEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.Ruleset)
                .HasMaxLength(32)
                .HasDefaultValue(nameof(MahjongRuleset.Changsha258));
            entity.HasMany(e => e.Players)
                .WithOne(e => e.Game)
                .HasForeignKey(e => e.GameId);
            entity.HasMany(e => e.Tiles)
                .WithOne(e => e.Game)
                .HasForeignKey(e => e.GameId);
            entity.HasMany(e => e.Melds)
                .WithOne(e => e.Game)
                .HasForeignKey(e => e.GameId);
            entity.HasMany(e => e.Discards)
                .WithOne(e => e.Game)
                .HasForeignKey(e => e.GameId);
        });

        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.Seat).HasMaxLength(16);
        });

        modelBuilder.Entity<TileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Suit).HasMaxLength(16);
            entity.Property(e => e.Location).HasMaxLength(16);
            entity.Property(e => e.Wind).HasMaxLength(8);
            entity.Property(e => e.Dragon).HasMaxLength(8);
            entity.HasOne(e => e.Player)
                .WithMany(e => e.Tiles)
                .HasForeignKey(e => e.PlayerId);
            entity.HasOne(e => e.Meld)
                .WithMany(e => e.Tiles)
                .HasForeignKey(e => e.MeldId);
        });

        modelBuilder.Entity<MeldEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(16);
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId);
        });

        modelBuilder.Entity<DiscardEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId);
            entity.HasOne(e => e.Tile)
                .WithMany()
                .HasForeignKey(e => e.TileId);
        });
    }
}
