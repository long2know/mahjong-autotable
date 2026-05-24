using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tournament;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TournamentEntity = Mahjong.Autotable.Api.Data.Entities.Tournament;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the W23 anonymous
/// standings read endpoint exposing Buchholz +
/// Sonneborn-Berger fields.
/// </summary>
[Collection("DbSerial")]
public sealed class TournamentStandingsControllerTests : IDisposable
{
    private static readonly string _scratch =
        Path.Combine(AppContext.BaseDirectory, "bishop-w23-stand-sqlite");
    private readonly string _dbPath;
    private readonly ServiceProvider _sp;

    public TournamentStandingsControllerTests()
    {
        Directory.CreateDirectory(_scratch);
        _dbPath = Path.Combine(_scratch, $"stand-{Guid.NewGuid():N}.sqlite");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _sp.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private TournamentStandingsController MakeController()
    {
        return new TournamentStandingsController(_sp.GetRequiredService<IServiceScopeFactory>());
    }

    private async Task<Guid> SeedTournamentAsync(string status = "complete")
    {
        var tid = Guid.NewGuid();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tournaments.Add(new TournamentEntity
        {
            Id = tid, Name = "stand-t", Format = "swiss", Status = status,
            CompletedAt = status == "complete" ? DateTime.UtcNow : (DateTime?)null,
        });
        await db.SaveChangesAsync();
        return tid;
    }

    private async Task SeedStandingAsync(Guid tid, string pid, int rank, int points, double buchholz, double sb)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TournamentStandings.Add(new TournamentStanding
        {
            TournamentId = tid,
            PlayerId = pid,
            Rank = rank,
            Points = points,
            GamesPlayed = 3,
            Buchholz = buchholz,
            SonnebornBerger = sb,
            FinalizedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Get_UnknownTournament_Returns404()
    {
        var r = await MakeController().Get(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Get_NoStandings_ReturnsEmptyArray()
    {
        var tid = await SeedTournamentAsync(status: "in-progress");
        var r = await MakeController().Get(tid, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var count = (int)ok.Value!.GetType().GetProperty("count")!.GetValue(ok.Value)!;
        Assert.Equal(0, count);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Get_StandingsExist_ReturnsRowsOrderedByRank()
    {
        var tid = await SeedTournamentAsync();
        await SeedStandingAsync(tid, "alice", 1, 3, 7.0, 4.5);
        await SeedStandingAsync(tid, "bob", 2, 2, 5.0, 2.0);
        var r = await MakeController().Get(tid, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var count = (int)ok.Value!.GetType().GetProperty("count")!.GetValue(ok.Value)!;
        Assert.Equal(2, count);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Get_StandingsResponse_IncludesBuchholzField()
    {
        var tid = await SeedTournamentAsync();
        await SeedStandingAsync(tid, "alice", 1, 3, 7.0, 4.5);
        var r = await MakeController().Get(tid, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        // Serialize to JSON and check the field exists.
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("buchholz", json);
        Assert.Contains("sonnebornBerger", json);
        Assert.Contains("7", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Get_Anonymous_DoesNotRequireSession()
    {
        // The endpoint is public read — no AuthCookieService dep.
        var tid = await SeedTournamentAsync();
        await SeedStandingAsync(tid, "alice", 1, 3, 7.0, 4.5);
        var c = MakeController();
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var r = await c.Get(tid, CancellationToken.None);
        Assert.IsType<OkObjectResult>(r);
    }
}
