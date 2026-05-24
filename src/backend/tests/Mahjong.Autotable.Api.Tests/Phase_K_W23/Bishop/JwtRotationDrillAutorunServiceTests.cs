using System.Text;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the W23 JWT
/// rotation-drill autorun background service: options
/// parsing, gating, Prom counter, tick semantics.
/// </summary>
[Collection("DbSerial")]
public sealed class JwtRotationDrillAutorunServiceTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbPath;

    public JwtRotationDrillAutorunServiceTests()
    {
        var scratch = Path.Combine(AppContext.BaseDirectory, "bishop-w23-drill-sqlite");
        Directory.CreateDirectory(scratch);
        _dbPath = Path.Combine(scratch, $"drill-{Guid.NewGuid():N}.sqlite");
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

    private sealed class DummyEnv : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "/";
        public string EnvironmentName { get; set; } = "Development";
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Options_Hourly_ResolvesToOneHour()
    {
        Assert.Equal(TimeSpan.FromHours(1), JwtRotationDrillAutorunOptions.TryResolveInterval("@hourly"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Options_Daily_ResolvesToOneDay()
    {
        Assert.Equal(TimeSpan.FromDays(1), JwtRotationDrillAutorunOptions.TryResolveInterval("@daily"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Options_MinuteSuffix_Resolves()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), JwtRotationDrillAutorunOptions.TryResolveInterval("15m"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Options_SecondSuffix_Resolves()
    {
        Assert.Equal(TimeSpan.FromSeconds(45), JwtRotationDrillAutorunOptions.TryResolveInterval("45s"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Options_Empty_ReturnsNull()
    {
        Assert.Null(JwtRotationDrillAutorunOptions.TryResolveInterval(""));
        Assert.Null(JwtRotationDrillAutorunOptions.TryResolveInterval(null));
        Assert.Null(JwtRotationDrillAutorunOptions.TryResolveInterval("garbage"));
        Assert.Null(JwtRotationDrillAutorunOptions.TryResolveInterval("0m"));
        Assert.Null(JwtRotationDrillAutorunOptions.TryResolveInterval("-1s"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Metrics_Record_IncrementsBucket()
    {
        var m = new JwtRotationDrillAutorunMetrics();
        m.Record(JwtRotationDrillAutorunMetrics.OutcomeSuccess);
        m.Record(JwtRotationDrillAutorunMetrics.OutcomeSuccess);
        m.Record(JwtRotationDrillAutorunMetrics.OutcomeError);
        Assert.Equal(2L, m.Get(JwtRotationDrillAutorunMetrics.OutcomeSuccess));
        Assert.Equal(1L, m.Get(JwtRotationDrillAutorunMetrics.OutcomeError));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Metrics_AppendPrometheus_EmitsHelpAndType()
    {
        var m = new JwtRotationDrillAutorunMetrics();
        m.Record(JwtRotationDrillAutorunMetrics.OutcomeSuccess);
        var sb = new StringBuilder();
        m.AppendPrometheus(sb);
        var text = sb.ToString();
        Assert.Contains("# HELP " + JwtRotationDrillAutorunMetrics.MetricName, text);
        Assert.Contains("# TYPE " + JwtRotationDrillAutorunMetrics.MetricName + " counter", text);
        Assert.Contains("outcome=\"success\"", text);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task TickOnce_PerTenantDisabled_RecordsSkipped()
    {
        var opts = new JwtRotationDrillAutorunOptions { AutorunCronSchedule = "1m" };
        var perTenant = new PerTenantJwksRotationOptions { Enabled = false };
        var validator = new PerTenantJwksRotationValidator(
            perTenant, NullLogger<PerTenantJwksRotationValidator>.Instance);
        var metrics = new JwtRotationDrillAutorunMetrics();
        var svc = new JwtRotationDrillAutorunService(
            opts, _sp.GetRequiredService<IServiceScopeFactory>(),
            new DummyEnv(), NullLogger<JwtRotationDrillAutorunService>.Instance,
            metrics, validator, perTenant);
        await svc.TickOnceAsync(CancellationToken.None);
        Assert.Equal(1L, metrics.Get(JwtRotationDrillAutorunMetrics.OutcomeSkipped));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task TickOnce_PerTenantEnabledNoPolicies_RecordsSuccess()
    {
        var opts = new JwtRotationDrillAutorunOptions { AutorunCronSchedule = "1m" };
        var perTenant = new PerTenantJwksRotationOptions { Enabled = true };
        var store = new InMemoryPerTenantJwksRotationStore();
        var validator = new PerTenantJwksRotationValidator(
            perTenant, NullLogger<PerTenantJwksRotationValidator>.Instance, store);
        var metrics = new JwtRotationDrillAutorunMetrics();
        var svc = new JwtRotationDrillAutorunService(
            opts, _sp.GetRequiredService<IServiceScopeFactory>(),
            new DummyEnv(), NullLogger<JwtRotationDrillAutorunService>.Instance,
            metrics, validator, perTenant, store);
        await svc.TickOnceAsync(CancellationToken.None);
        Assert.Equal(1L, metrics.Get(JwtRotationDrillAutorunMetrics.OutcomeSuccess));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task TickOnce_StampsAuditRow()
    {
        var opts = new JwtRotationDrillAutorunOptions { AutorunCronSchedule = "1m" };
        var perTenant = new PerTenantJwksRotationOptions { Enabled = true };
        var store = new InMemoryPerTenantJwksRotationStore();
        var validator = new PerTenantJwksRotationValidator(
            perTenant, NullLogger<PerTenantJwksRotationValidator>.Instance, store);
        var metrics = new JwtRotationDrillAutorunMetrics();
        var svc = new JwtRotationDrillAutorunService(
            opts, _sp.GetRequiredService<IServiceScopeFactory>(),
            new DummyEnv(), NullLogger<JwtRotationDrillAutorunService>.Instance,
            metrics, validator, perTenant, store);
        await svc.TickOnceAsync(CancellationToken.None);
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.ReconnectAuditEntries
            .Where(r => r.Kind == Data.Entities.ReconnectAuditEntry.KindJwtRotationDrillAutorun)
            .ToListAsync();
        Assert.NotEmpty(rows);
    }
}
