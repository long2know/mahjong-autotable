using System.Text;
using Mahjong.Autotable.Api.Observability;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the W23 per-SignalR-
/// group telemetry helper, the Prom metric renderer, the tick
/// service, and the admin-gated GET /api/signalr/groups
/// endpoint.
/// </summary>
public sealed class SignalRGroupTelemetryControllerTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbPath;
    private readonly SignalRConnectionRegistry _registry = new();
    private readonly SignalRGroupTelemetry _telemetry = new();

    public SignalRGroupTelemetryControllerTests()
    {
        var scratch = Path.Combine(AppContext.BaseDirectory, "bishop-w23-signalr-sqlite");
        Directory.CreateDirectory(scratch);
        _dbPath = Path.Combine(scratch, $"signalr-{Guid.NewGuid():N}.sqlite");
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

    private SignalRGroupTelemetryController MakeController(HttpContext ctx, AuthCookieService cookies)
    {
        var c = new SignalRGroupTelemetryController(cookies, _registry, _telemetry);
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Telemetry_AlphaOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalRGroupTelemetry(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalRGroupTelemetry(-0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalRGroupTelemetry(1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalRGroupTelemetry(double.NaN));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Telemetry_AlphaOne_AcceptsValue()
    {
        var t = new SignalRGroupTelemetry(1.0);
        t.RecordMessage("g1");
        var snap = t.Snapshot();
        Assert.Single(snap);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Telemetry_RecordMessage_NullOrEmpty_NoOp()
    {
        var t = new SignalRGroupTelemetry();
        t.RecordMessage("");
        t.RecordMessage(" ");
        Assert.Equal(0, t.GroupCount);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Telemetry_Tick_AdvancesEwma()
    {
        var t = new SignalRGroupTelemetry(alpha: 1.0);
        // Seed initial tick to establish LastTickUtc.
        var t0 = DateTime.UtcNow;
        t.RecordMessage("g1");
        t.Tick(t0); // resets last observed to 1
        for (int i = 0; i < 10; i++) t.RecordMessage("g1");
        t.Tick(t0.AddSeconds(1));
        var snap = t.Snapshot();
        Assert.Single(snap);
        Assert.True(snap[0].EwmaMsgsPerSecond > 0);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Telemetry_Tick_NoElapsedTime_NoChange()
    {
        var t = new SignalRGroupTelemetry();
        var now = DateTime.UtcNow;
        t.RecordMessage("g1");
        t.Tick(now);
        var firstEwma = t.Snapshot()[0].EwmaMsgsPerSecond;
        t.Tick(now); // same tick — elapsed <= 0
        Assert.Equal(firstEwma, t.Snapshot()[0].EwmaMsgsPerSecond);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Metrics_AppendPrometheus_EmitsBothHelpAndType()
    {
        _telemetry.RecordMessage("g1");
        _telemetry.Tick(DateTime.UtcNow);
        var m = new SignalRGroupMetrics(_registry, _telemetry);
        var sb = new StringBuilder();
        m.AppendPrometheus(sb);
        var s = sb.ToString();
        Assert.Contains("# HELP signalr_group_connections", s);
        Assert.Contains("# TYPE signalr_group_connections gauge", s);
        Assert.Contains("# HELP signalr_group_msg_rate", s);
        Assert.Contains("# TYPE signalr_group_msg_rate gauge", s);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void TickService_NonPositiveInterval_Throws()
    {
        var t = new SignalRGroupTelemetry();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SignalRGroupTelemetryTickService(t, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SignalRGroupTelemetryTickService(t, TimeSpan.FromSeconds(-1)));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task GetGroups_NoSession_Returns401()
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var c = MakeController(new DefaultHttpContext(), cookies);
        var r = await c.GetGroups(CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task GetGroups_NonAdmin_Returns403()
    {
        var (cookies, ctx) = await MakeSessionAsync("player");
        var r = await MakeController(ctx, cookies).GetGroups(CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task GetGroups_Admin_Returns200WithShape()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        _registry.Register(new SignalRConnectionRegistry.Entry
        { ConnectionId = "c1", TenantId = "t", Group = "g1", Transport = "websocket" });
        _telemetry.RecordMessage("g1");
        _telemetry.Tick(DateTime.UtcNow);
        var r = await MakeController(ctx, cookies).GetGroups(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("totalGroups", json);
        Assert.Contains("totalConnections", json);
        Assert.Contains("ewmaMsgsPerSecond", json);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task GetGroups_EmptyRegistry_ReturnsZeroTotals()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).GetGroups(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalGroups\":0", json);
        Assert.Contains("\"totalConnections\":0", json);
    }
}
