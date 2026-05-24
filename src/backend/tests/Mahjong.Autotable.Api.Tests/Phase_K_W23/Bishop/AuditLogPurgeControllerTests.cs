using Mahjong.Autotable.Api.Audit;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the W23 admin-gated
/// audit-log purge endpoint (auth gate, reason header, days
/// bounds, purge math, meta-audit row).
/// </summary>
[Collection("DbSerial")]
public sealed class AuditLogPurgeControllerTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbPath;
    private readonly AuditLogPurgeMetrics _metrics = new();

    public AuditLogPurgeControllerTests()
    {
        var scratch = Path.Combine(AppContext.BaseDirectory, "bishop-w23-audit-sqlite");
        Directory.CreateDirectory(scratch);
        _dbPath = Path.Combine(scratch, $"audit-{Guid.NewGuid():N}.sqlite");
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

    private async Task<(AuthCookieService, HttpContext)> MakeSessionAsync(string role = "admin", string? reason = "test-purge")
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var issue = new DefaultHttpContext();
        var s = await cookies.IssueAsync(issue, $"p-{Guid.NewGuid():N}", Guid.NewGuid(), role);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = $"{AuthCookieService.CookieName}={s.Token}";
        if (reason != null)
        {
            ctx.Request.Headers[AuditLogPurgeController.AdminReasonHeader] = reason;
        }
        return (cookies, ctx);
    }

    private AuditLogPurgeController MakeController(HttpContext ctx, AuthCookieService cookies)
    {
        var scopeFactory = _sp.GetRequiredService<IServiceScopeFactory>();
        var purge = new AuditLogPurgeService(scopeFactory, _metrics);
        var c = new AuditLogPurgeController(cookies, purge, scopeFactory);
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    private async Task SeedRowAsync(DateTime at)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
        {
            Id = Guid.NewGuid(),
            PlayerId = "seed",
            At = at,
            Kind = "test",
            Detail = "seed",
        });
        await db.SaveChangesAsync();
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_NoSession_Returns401()
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var r = await MakeController(new DefaultHttpContext(), cookies).Purge(30, CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_NonAdmin_Returns403()
    {
        var (cookies, ctx) = await MakeSessionAsync(role: "player");
        var r = await MakeController(ctx, cookies).Purge(30, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_NoReason_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync(reason: null);
        var r = await MakeController(ctx, cookies).Purge(30, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_MissingDays_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Purge(null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_DaysOutOfRangeLow_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Purge(0, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_DaysOutOfRangeHigh_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Purge(AuditLogPurgeController.MaxOlderThanDays + 1, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_NoMatchingRows_ReturnsZero()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedRowAsync(DateTime.UtcNow); // recent — not purged
        var r = await MakeController(ctx, cookies).Purge(30, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"purged\":0", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Purge_OldRows_DeletesAndStampsMetaAudit()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        await SeedRowAsync(DateTime.UtcNow.AddDays(-60));
        await SeedRowAsync(DateTime.UtcNow.AddDays(-50));
        await SeedRowAsync(DateTime.UtcNow); // recent — kept
        var r = await MakeController(ctx, cookies).Purge(30, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"purged\":2", json);

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var meta = await db.ReconnectAuditEntries
            .Where(r => r.Kind == ReconnectAuditEntry.KindAuditLogPurged)
            .ToListAsync();
        Assert.Single(meta);
    }
}
