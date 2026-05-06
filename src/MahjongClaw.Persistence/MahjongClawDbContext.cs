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
    public DbSet<HandResultEntity> HandResults => Set<HandResultEntity>();
    public DbSet<HandResultWinnerEntity> HandResultWinners => Set<HandResultWinnerEntity>();
    public DbSet<HandResultSeatDeltaEntity> HandResultSeatDeltas => Set<HandResultSeatDeltaEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.Ruleset)
                .HasMaxLength(32)
                .HasDefaultValue(nameof(MahjongRuleset.Changsha258));
            entity.Property(e => e.RoundWind)
                .HasMaxLength(16)
                .HasDefaultValue("East");
            entity.Property(e => e.DealerRotationCount)
                .HasDefaultValue(0);
            entity.Property(e => e.BirdRanks).HasMaxLength(256);
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
            entity.Property(e => e.Kind).HasMaxLength(16);
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

        modelBuilder.Entity<HandResultEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoundWind).HasMaxLength(16);
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId);
            entity.HasMany(e => e.Winners)
                .WithOne(e => e.HandResult)
                .HasForeignKey(e => e.HandResultId);
            entity.HasMany(e => e.SeatDeltas)
                .WithOne(e => e.HandResult)
                .HasForeignKey(e => e.HandResultId);
            entity.HasIndex(e => new { e.GameId, e.HandNumber });
        });

        modelBuilder.Entity<HandResultWinnerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WinTypesJson).HasMaxLength(2048);
        });

        modelBuilder.Entity<HandResultSeatDeltaEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeltaCategory).HasMaxLength(32);
        });
    }
}
