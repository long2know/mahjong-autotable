using FluentAssertions;
using MahjongClaw.Persistence;
using MahjongClaw.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MahjongClaw.Tests;

public sealed class HandResultPersistenceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MahjongClawDbContext _dbContext;

    public HandResultPersistenceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MahjongClawDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MahjongClawDbContext>();
    }

    [Fact]
    public async Task PersistHandResult_SavesSingleWinner_WithAllFields()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "East",
            IsSelfDraw = true,
            DiscarderId = null,
            WallExhausted = false
        };

        var winner = new HandResultWinnerEntity
        {
            Id = Guid.NewGuid(),
            HandResultId = handResult.Id,
            PlayerId = game.Players.First().Id,
            WinTypesJson = "[\"AllPungs\",\"PureSuit\"]",
            IsBigWin = true,
            IsDealer = true
        };

        var seatDeltas = game.Players.Select(p => new HandResultSeatDeltaEntity
        {
            Id = Guid.NewGuid(),
            HandResultId = handResult.Id,
            PlayerId = p.Id,
            Delta = p.Id == game.Players.First().Id ? 210 : -70,
            NewScore = p.Id == game.Players.First().Id ? 210 : -70,
            DeltaCategory = p.Id == game.Players.First().Id ? "Winnings" : "Loss"
        }).ToList();

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultWinners.Add(winner);
        _dbContext.HandResultSeatDeltas.AddRange(seatDeltas);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedResult = await _dbContext.HandResults
            .Include(hr => hr.Winners)
            .Include(hr => hr.SeatDeltas)
            .FirstOrDefaultAsync(hr => hr.GameId == game.Id);

        savedResult.Should().NotBeNull();
        savedResult!.HandNumber.Should().Be(1);
        savedResult.IsSelfDraw.Should().BeTrue();
        savedResult.WallExhausted.Should().BeFalse();
        savedResult.Winners.Should().HaveCount(1);
        savedResult.Winners.First().IsBigWin.Should().BeTrue();
        savedResult.SeatDeltas.Should().HaveCount(4);
        savedResult.SeatDeltas.Sum(sd => sd.Delta).Should().Be(0);
    }

    [Fact]
    public async Task PersistHandResult_SavesMultiWinner_WithMultipleWinners()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 2,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "South",
            IsSelfDraw = false,
            DiscarderId = game.Players.Last().Id,
            WallExhausted = false
        };

        var winners = new[]
        {
            new HandResultWinnerEntity
            {
                Id = Guid.NewGuid(),
                HandResultId = handResult.Id,
                PlayerId = game.Players.ElementAt(1).Id,
                WinTypesJson = "[\"SevenPairs\"]",
                IsBigWin = true,
                IsDealer = false
            },
            new HandResultWinnerEntity
            {
                Id = Guid.NewGuid(),
                HandResultId = handResult.Id,
                PlayerId = game.Players.ElementAt(2).Id,
                WinTypesJson = "[\"AllTwoFiveEight\"]",
                IsBigWin = true,
                IsDealer = false
            }
        };

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultWinners.AddRange(winners);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedResult = await _dbContext.HandResults
            .Include(hr => hr.Winners)
            .FirstOrDefaultAsync(hr => hr.HandNumber == 2);

        savedResult.Should().NotBeNull();
        savedResult!.Winners.Should().HaveCount(2);
        savedResult.IsSelfDraw.Should().BeFalse();
        savedResult.DiscarderId.Should().NotBeNull();
    }

    [Fact]
    public async Task PersistHandResult_SavesWallExhausted_WithNoWinners()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 3,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "West",
            IsSelfDraw = false,
            DiscarderId = null,
            WallExhausted = true
        };

        var seatDeltas = game.Players.Select(p => new HandResultSeatDeltaEntity
        {
            Id = Guid.NewGuid(),
            HandResultId = handResult.Id,
            PlayerId = p.Id,
            Delta = 0,
            NewScore = 0,
            DeltaCategory = "ZeroNoGame"
        }).ToList();

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultSeatDeltas.AddRange(seatDeltas);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedResult = await _dbContext.HandResults
            .Include(hr => hr.Winners)
            .Include(hr => hr.SeatDeltas)
            .FirstOrDefaultAsync(hr => hr.WallExhausted);

        savedResult.Should().NotBeNull();
        savedResult!.WallExhausted.Should().BeTrue();
        savedResult.Winners.Should().BeEmpty();
        savedResult.SeatDeltas.Should().HaveCount(4);
        savedResult.SeatDeltas.Should().OnlyContain(sd => sd.Delta == 0);
        savedResult.SeatDeltas.Should().OnlyContain(sd => sd.DeltaCategory == "ZeroNoGame");
    }

    [Fact]
    public async Task HandNumber_Increments_AcrossMultipleHands()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act - Create 5 hand results
        for (int i = 1; i <= 5; i++)
        {
            var handResult = new HandResultEntity
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                HandNumber = i,
                CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(i),
                DealerId = game.Players.First().Id,
                RoundWind = "East",
                IsSelfDraw = true,
                WallExhausted = false
            };
            _dbContext.HandResults.Add(handResult);
        }
        await _dbContext.SaveChangesAsync();

        // Assert
        var handResults = await _dbContext.HandResults
            .Where(hr => hr.GameId == game.Id)
            .OrderBy(hr => hr.HandNumber)
            .ToListAsync();

        handResults.Should().HaveCount(5);
        handResults.Select(hr => hr.HandNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task DeltaSum_AlwaysZero_ForAllHandResults()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "East",
            IsSelfDraw = true,
            WallExhausted = false
        };

        var seatDeltas = new[]
        {
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(0).Id, Delta = 210, NewScore = 210 },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(1).Id, Delta = -70, NewScore = -70 },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(2).Id, Delta = -70, NewScore = -70 },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(3).Id, Delta = -70, NewScore = -70 }
        };

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultSeatDeltas.AddRange(seatDeltas);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedDeltas = await _dbContext.HandResultSeatDeltas
            .Where(sd => sd.HandResultId == handResult.Id)
            .ToListAsync();

        savedDeltas.Sum(sd => sd.Delta).Should().Be(0);
    }

    [Fact]
    public async Task BirdRanks_StoredOnGameEntity_AsJson()
    {
        // Arrange
        var game = CreateTestGame();
        game.BirdRank = 5;
        game.BirdRanks = "[2,7]";
        _dbContext.Games.Add(game);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedGame = await _dbContext.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
        savedGame.Should().NotBeNull();
        savedGame!.BirdRank.Should().Be(5);
        savedGame.BirdRanks.Should().Be("[2,7]");
    }

    [Fact]
    public async Task BirdRanks_CanBeNull_OnGameEntity()
    {
        // Arrange
        var game = CreateTestGame();
        game.BirdRank = null;
        game.BirdRanks = null;
        _dbContext.Games.Add(game);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedGame = await _dbContext.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
        savedGame.Should().NotBeNull();
        savedGame!.BirdRank.Should().BeNull();
        savedGame.BirdRanks.Should().BeNull();
    }

    [Fact]
    public async Task WinTypes_SerializedAsJson_InWinnerEntity()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "East",
            IsSelfDraw = true,
            WallExhausted = false
        };

        var winner = new HandResultWinnerEntity
        {
            Id = Guid.NewGuid(),
            HandResultId = handResult.Id,
            PlayerId = game.Players.First().Id,
            WinTypesJson = "[\"AllPungs\",\"PureSuit\",\"LastTileDraw\"]",
            IsBigWin = true,
            IsDealer = true
        };

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultWinners.Add(winner);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedWinner = await _dbContext.HandResultWinners
            .FirstOrDefaultAsync(w => w.HandResultId == handResult.Id);

        savedWinner.Should().NotBeNull();
        savedWinner!.WinTypesJson.Should().Contain("AllPungs");
        savedWinner.WinTypesJson.Should().Contain("PureSuit");
        savedWinner.WinTypesJson.Should().Contain("LastTileDraw");
    }

    [Fact]
    public async Task DeltaCategory_StoresCorrectCategory_ForWinnersAndLosers()
    {
        // Arrange
        var game = CreateTestGame();
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handResult = new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game.Players.First().Id,
            RoundWind = "East",
            IsSelfDraw = true,
            WallExhausted = false
        };

        var seatDeltas = new[]
        {
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(0).Id, Delta = 210, NewScore = 210, DeltaCategory = "Winnings" },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(1).Id, Delta = -70, NewScore = -70, DeltaCategory = "Loss" },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(2).Id, Delta = -70, NewScore = -70, DeltaCategory = "Loss" },
            new HandResultSeatDeltaEntity { Id = Guid.NewGuid(), HandResultId = handResult.Id, PlayerId = game.Players.ElementAt(3).Id, Delta = -70, NewScore = -70, DeltaCategory = "Loss" }
        };

        _dbContext.HandResults.Add(handResult);
        _dbContext.HandResultSeatDeltas.AddRange(seatDeltas);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedDeltas = await _dbContext.HandResultSeatDeltas
            .Where(sd => sd.HandResultId == handResult.Id)
            .ToListAsync();

        savedDeltas.Should().ContainSingle(sd => sd.DeltaCategory == "Winnings");
        savedDeltas.Should().Contain(sd => sd.DeltaCategory == "Loss", "Should have 3 losers");
        savedDeltas.Count(sd => sd.DeltaCategory == "Loss").Should().Be(3);
    }

    [Fact]
    public async Task GameEntityIndex_OnGameIdAndHandNumber_AllowsFastLookup()
    {
        // Arrange
        var game1 = CreateTestGame();
        var game2 = CreateTestGame();
        _dbContext.Games.AddRange(game1, game2);
        await _dbContext.SaveChangesAsync();

        // Create multiple hands for game1
        for (int i = 1; i <= 10; i++)
        {
            _dbContext.HandResults.Add(new HandResultEntity
            {
                Id = Guid.NewGuid(),
                GameId = game1.Id,
                HandNumber = i,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                DealerId = game1.Players.First().Id,
                RoundWind = "East",
                IsSelfDraw = true,
                WallExhausted = false
            });
        }

        // Create one hand for game2
        _dbContext.HandResults.Add(new HandResultEntity
        {
            Id = Guid.NewGuid(),
            GameId = game2.Id,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DealerId = game2.Players.First().Id,
            RoundWind = "East",
            IsSelfDraw = true,
            WallExhausted = false
        });

        await _dbContext.SaveChangesAsync();

        // Act - Query specific hand in game1
        var hand5 = await _dbContext.HandResults
            .FirstOrDefaultAsync(hr => hr.GameId == game1.Id && hr.HandNumber == 5);

        // Assert
        hand5.Should().NotBeNull();
        hand5!.HandNumber.Should().Be(5);
        hand5.GameId.Should().Be(game1.Id);
    }

    private static GameEntity CreateTestGame()
    {
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Status = "Active",
            Round = 1,
            RoundWind = "East",
            Ruleset = "Changsha258",
            CreatedAt = DateTimeOffset.UtcNow
        };

        game.Players = new List<PlayerEntity>
        {
            new() { Id = Guid.NewGuid(), GameId = game.Id, Name = "East", Seat = "East", Score = 0 },
            new() { Id = Guid.NewGuid(), GameId = game.Id, Name = "South", Seat = "South", Score = 0 },
            new() { Id = Guid.NewGuid(), GameId = game.Id, Name = "West", Seat = "West", Score = 0 },
            new() { Id = Guid.NewGuid(), GameId = game.Id, Name = "North", Seat = "North", Score = 0 }
        };

        game.DealerId = game.Players.First().Id;
        return game;
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
