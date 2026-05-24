using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Replays;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the admin-gated
/// paginated query over ReplayRestorationAttempt (auth gate,
/// since filter, outcome filter, paging, meta-audit row).
/// </summary>
[Collection("DbSerial")]
public sealed class ReplayRestorationAuditHistoryControllerTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbPath;

    public ReplayRestorationAuditHistoryControllerTests()
    {
        var scratch = Path.Combine(AppContext.BaseDirectory, "bishop-w23-resthist-sqlite");
        Directory.CreateDirectory(scratch);
        _dbPath = Path.Combine(scratch, $"resthist-{Guid.NewGuid():N}.sqlite");
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

    private async Task<(AuthCookieService, HttpContext)> MakeSessionAsync(string role = "admin")
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var issue = new DefaultHttpContext();
        var s = await cookies.IssueAsync(issue, $"p-{Guid.NewGuid():N}", Guid.NewGuid(), role);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = $"{AuthCookieService.CookieName}={s.Token}";
        return (cookies, ctx);
    }

    private ReplayRestorationAuditHistoryController MakeController(HttpContext ctx, AuthCookieService cookies)
    {
        var c = new ReplayRestorationAuditHistoryController(
            cookies, _sp.GetRequiredService<IServiceScopeFactory>());
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    private async Task SeedAttemptAsync(DateTime at, string outcome = "read", string replayId = "r1")
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReplayRestorationAttempts.Add(new ReplayRestorationAttempt
        {
            Id = Guid.NewGuid(),
            ReplayId = replayId,
            OperatorId = "op",
            Outcome = outcome,
            DetailMessage = "test",
            AttemptedAtUtc = at,
        });
        await db.SaveChangesAsync();
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_NoSession_Returns401()
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var r = await MakeController(new DefaultHttpContext(), cookies)
            .Query(null, null, null, null, CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_NonAdmin_Returns403()
    {
        var (cookies, ctx) = await MakeSessionAsync("player");
        var r = await MakeController(ctx, cookies).Query(null, null, null, null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_InvalidSince_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Query("not-iso", null, null, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_NegativePage_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Query(null, null, 0, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_NegativePageSize_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Query(null, null, null, 0, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_NoFilter_ReturnsAllRows()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedAttemptAsync(DateTime.UtcNow.AddHours(-1));
        await SeedAttemptAsync(DateTime.UtcNow);
        var r = await MakeController(ctx, cookies).Query(null, null, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalCount\":2", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_SinceFilter_ExcludesOlderRows()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedAttemptAsync(DateTime.UtcNow.AddDays(-10));
        await SeedAttemptAsync(DateTime.UtcNow);
        var since = DateTime.UtcNow.AddDays(-1).ToString("o");
        var r = await MakeController(ctx, cookies).Query(since, null, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalCount\":1", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_OutcomeFilter_Works()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedAttemptAsync(DateTime.UtcNow, outcome: ReplayRestorationAttempt.OutcomeRead);
        await SeedAttemptAsync(DateTime.UtcNow, outcome: ReplayRestorationAttempt.OutcomeIntegrityFailure);
        var r = await MakeController(ctx, cookies)
            .Query(null, ReplayRestorationAttempt.OutcomeIntegrityFailure, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalCount\":1", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_Paginated_RespectsPageAndSize()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        for (int i = 0; i < 5; i++) await SeedAttemptAsync(DateTime.UtcNow.AddMinutes(-i));
        var r = await MakeController(ctx, cookies).Query(null, null, 2, 2, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalCount\":5", json);
        Assert.Contains("\"page\":2", json);
        Assert.Contains("\"pageSize\":2", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Query_StampsMetaAuditRow()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedAttemptAsync(DateTime.UtcNow);
        await MakeController(ctx, cookies).Query(null, null, null, null, CancellationToken.None);
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var meta = await db.ReconnectAuditEntries
            .Where(r => r.Kind == ReconnectAuditEntry.KindReplayRestorationAuditQueried)
            .ToListAsync();
        Assert.NotEmpty(meta);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ParseUtc_AcceptsIsoString()
    {
        var p = ReplayRestorationAuditHistoryController.ParseUtc("2024-01-15T00:00:00Z");
        Assert.NotNull(p);
        Assert.Equal(DateTimeKind.Utc, p!.Value.Kind);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ParseUtc_RejectsInvalid()
    {
        Assert.Null(ReplayRestorationAuditHistoryController.ParseUtc("garbage"));
        Assert.Null(ReplayRestorationAuditHistoryController.ParseUtc(null));
        Assert.Null(ReplayRestorationAuditHistoryController.ParseUtc(""));
    }
}
