using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 23 — Bishop. Options for the optional cron-
/// driven autorun of the W20 JWT key-rotation drill. Bound from
/// the <c>Auth:RotationDrill</c> section.
///
/// <para>The drill is destructive only in the sense that it
/// re-evaluates every per-tenant rotation policy and stamps an
/// audit row — it does NOT mint or rotate a real key. The
/// autorun toggle therefore defaults OFF; operators flip it on
/// only in environments that need continuous drill posture
/// (staging clusters with multi-tenant policies).</para>
/// </summary>
public sealed class JwtRotationDrillAutorunOptions
{
    /// <summary>Cron-like interval the autorun service ticks
    /// on. The W23 implementation accepts a SIMPLE subset:
    /// <c>"@hourly"</c> / <c>"@daily"</c> / a positive
    /// integer-minutes string like <c>"30m"</c> / a positive
    /// integer-seconds string like <c>"45s"</c>. An empty /
    /// invalid value disables the autorun without erroring.
    /// The constant <see cref="ConfigKey"/> documents the
    /// AppSettings binding path.</summary>
    public string AutorunCronSchedule { get; set; } = string.Empty;

    /// <summary>Settle delay before the first tick on startup
    /// (seconds). Defaults to 60 — long enough for EF Core to
    /// finish warm-up before the first drill issues queries.</summary>
    public int StartupSettleSeconds { get; set; } = 60;

    public const string ConfigKey = "Auth:RotationDrill:AutorunCronSchedule";
    public const string ConfigSection = "Auth:RotationDrill";

    /// <summary>Parse <see cref="AutorunCronSchedule"/> into a
    /// concrete <see cref="TimeSpan"/>. Returns null when the
    /// value is empty or unparseable (the autorun service
    /// declines to schedule in that case). The grammar is
    /// intentionally narrow — heavy cron support lives forward
    /// of this wave in a Hangfire-style scheduler.</summary>
    public static TimeSpan? TryResolveInterval(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var v = expression.Trim();
        if (string.Equals(v, "@hourly", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromHours(1);
        }
        if (string.Equals(v, "@daily", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromDays(1);
        }
        if (string.Equals(v, "@every-minute", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromMinutes(1);
        }
        if (v.Length > 1 && (v.EndsWith("m", StringComparison.OrdinalIgnoreCase)
                          || v.EndsWith("s", StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = v[^1];
            var numPart = v.Substring(0, v.Length - 1);
            if (int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > 0)
            {
                return char.ToLowerInvariant(suffix) == 'm'
                    ? TimeSpan.FromMinutes(n)
                    : TimeSpan.FromSeconds(n);
            }
        }
        return null;
    }
}

/// <summary>
/// Phase K Wave 23 — Bishop. Prometheus counter for cron-driven
/// JWT rotation drills. Surfaces <c>jwt_rotation_drill_runs_total
/// {outcome="…"}</c>; outcome buckets are <c>success</c> /
/// <c>error</c> / <c>skipped</c>. Renders the schema preamble
/// unconditionally so dashboards see a stable shape even
/// before the first tick.
/// </summary>
public sealed class JwtRotationDrillAutorunMetrics
{
    public const string MetricName = "jwt_rotation_drill_runs_total";
    public const string OutcomeLabel = "outcome";

    public const string OutcomeSuccess = "success";
    public const string OutcomeError = "error";
    public const string OutcomeSkipped = "skipped";

    private readonly ConcurrentDictionary<string, long> _counters =
        new(StringComparer.Ordinal);

    public void Record(string outcome)
    {
        var label = string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome;
        _counters.AddOrUpdate(label, 1, (_, prev) => prev + 1);
    }

    public long Get(string outcome) => _counters.TryGetValue(outcome, out var v) ? v : 0;

    public IReadOnlyDictionary<string, long> Snapshot() =>
        new Dictionary<string, long>(_counters, StringComparer.Ordinal);

    public void AppendPrometheus(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);
        sb.Append("# HELP ").Append(MetricName)
          .AppendLine(" Total cron-driven JWT rotation-drill runs. Labelled by `outcome` (`success` / `error` / `skipped`).");
        sb.Append("# TYPE ").Append(MetricName).AppendLine(" counter");
        foreach (var kv in _counters)
        {
            sb.Append(MetricName)
              .Append('{').Append(OutcomeLabel).Append("=\"")
              .Append(Escape(kv.Key)).Append("\"} ")
              .AppendLine(kv.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static string Escape(string v)
    {
        if (string.IsNullOrEmpty(v)) return v;
        if (v.IndexOfAny(['\\', '"', '\n']) < 0) return v;
        var sb = new StringBuilder(v.Length + 8);
        foreach (var ch in v)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// Phase K Wave 23 — Bishop. Background service that runs the
/// JWT key-rotation drill on a cron-like schedule when
/// <see cref="JwtRotationDrillAutorunOptions.AutorunCronSchedule"/>
/// resolves to a positive interval.
///
/// <para>The service is a thin wrapper around the W20 drill
/// logic: walk every per-tenant policy, evaluate the
/// validator, invalidate the JWKS cache, stamp an audit row,
/// and record a Prom counter. The hosted service is
/// production-safe — it short-circuits in production
/// environments AND when the autorun schedule is unset, so a
/// missed appsettings stanza never spins up surprise
/// background load.</para>
///
/// <para>Failure semantics: a single tick that throws is
/// caught and recorded as <c>outcome="error"</c>; the next
/// scheduled tick is unaffected. The service does not exit on
/// error.</para>
/// </summary>
public sealed class JwtRotationDrillAutorunService : BackgroundService
{
    private readonly JwtRotationDrillAutorunOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JwtRotationDrillAutorunService> _logger;
    private readonly IHostEnvironment _env;
    private readonly JwtRotationDrillAutorunMetrics _metrics;
    private readonly PerTenantJwksRotationValidator _validator;
    private readonly PerTenantJwksRotationOptions _perTenantOptions;
    private readonly IPerTenantJwksRotationStore? _store;
    private readonly JwksCacheService? _cache;

    public JwtRotationDrillAutorunService(
        JwtRotationDrillAutorunOptions options,
        IServiceScopeFactory scopeFactory,
        IHostEnvironment env,
        ILogger<JwtRotationDrillAutorunService> logger,
        JwtRotationDrillAutorunMetrics metrics,
        PerTenantJwksRotationValidator validator,
        PerTenantJwksRotationOptions perTenantOptions,
        IPerTenantJwksRotationStore? store = null,
        JwksCacheService? cache = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _perTenantOptions = perTenantOptions ?? throw new ArgumentNullException(nameof(perTenantOptions));
        _store = store;
        _cache = cache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_env.IsProduction())
        {
            _logger.LogInformation("JwtRotationDrillAutorunService is gated off in production; not scheduling.");
            return;
        }
        var interval = JwtRotationDrillAutorunOptions.TryResolveInterval(_options.AutorunCronSchedule);
        if (interval is null)
        {
            _logger.LogDebug("JwtRotationDrillAutorunService: no autorun schedule configured ({Schedule}); skipping.",
                _options.AutorunCronSchedule);
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupSettleSeconds)), stoppingToken);
        }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await TickOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(interval.Value, stoppingToken);
            }
            catch (TaskCanceledException) { return; }
        }
    }

    /// <summary>Single tick — exposed so tests can drive the
    /// service deterministically without waiting for the
    /// interval timer.</summary>
    public async Task TickOnceAsync(CancellationToken ct)
    {
        if (!_perTenantOptions.Enabled || _store is null)
        {
            _metrics.Record(JwtRotationDrillAutorunMetrics.OutcomeSkipped);
            await WriteAuditAsync(JwtRotationDrillAutorunMetrics.OutcomeSkipped, tenantsExercised: 0,
                drillId: Guid.NewGuid(), ct: ct);
            return;
        }
        var drillId = Guid.NewGuid();
        int tenantsExercised = 0;
        try
        {
            var policies = await _store.ListAsync(ct);
            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var p in policies)
            {
                ct.ThrowIfCancellationRequested();
                _ = await _validator.EvaluateAsync(p.TenantId, nowUtc, ct);
                tenantsExercised++;
            }
            _cache?.Invalidate();
            _metrics.Record(JwtRotationDrillAutorunMetrics.OutcomeSuccess);
            await WriteAuditAsync(JwtRotationDrillAutorunMetrics.OutcomeSuccess, tenantsExercised, drillId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "JwtRotationDrillAutorunService tick failed (drillId={DrillId}).", drillId);
            _metrics.Record(JwtRotationDrillAutorunMetrics.OutcomeError);
            try
            {
                await WriteAuditAsync(JwtRotationDrillAutorunMetrics.OutcomeError, tenantsExercised, drillId, ct);
            }
            catch { /* swallow audit failures */ }
        }
    }

    private async Task WriteAuditAsync(string outcome, int tenantsExercised, Guid drillId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
            {
                Id = Guid.NewGuid(),
                PlayerId = "system",
                At = DateTime.UtcNow,
                Kind = ReconnectAuditEntry.KindJwtRotationDrillAutorun,
                Detail = $"cron={_options.AutorunCronSchedule}|outcome={outcome}|tenants={tenantsExercised}|drillId={drillId:N}",
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JwtRotationDrillAutorunService audit write failed.");
        }
    }
}
