using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Observability;

/// <summary>
/// Phase K Wave 23 — Bishop. Per-SignalR-group telemetry state.
/// Maintains a per-group EWMA (exponentially weighted moving
/// average) of the messages-per-second rate so the
/// <c>GET /api/signalr/groups</c> surface can render the
/// "noisy group" hotspots without a separate Prom scrape.
///
/// <para>The EWMA tracks the per-tick message-count delta:
/// <c>ewma' = alpha · rate + (1 - alpha) · ewma</c> where
/// <c>alpha</c> defaults to <c>0.2</c> (smoothing factor that
/// favours stability over reactivity — operator dashboards
/// noticed the W22 unfiltered counter spiked on every batch
/// of replays). The state is process-local; multi-replica
/// deployments aggregate via the Prom metric.</para>
/// </summary>
public sealed class SignalRGroupTelemetry
{
    public const double DefaultAlpha = 0.2;

    public sealed class GroupState
    {
        public string Group { get; init; } = string.Empty;
        public long MessageCount;
        public long LastObservedCount { get; set; }
        public double EwmaMsgsPerSecond { get; set; }
        public DateTime LastTickUtc { get; set; } = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<string, GroupState> _groups =
        new(StringComparer.Ordinal);
    private readonly double _alpha;

    public SignalRGroupTelemetry() : this(DefaultAlpha) { }

    public SignalRGroupTelemetry(double alpha)
    {
        if (double.IsNaN(alpha) || alpha <= 0.0 || alpha > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha),
                "Alpha must be in the half-open interval (0, 1].");
        }
        _alpha = alpha;
    }

    /// <summary>Record one message dispatched to a group.</summary>
    public void RecordMessage(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) return;
        var state = _groups.GetOrAdd(group, g => new GroupState { Group = g });
        Interlocked.Increment(ref state.MessageCount);
    }

    /// <summary>Tick every group's EWMA forward by the
    /// elapsed wall-clock interval since the last tick.
    /// Called from the W23 background service on a fixed
    /// cadence (1s default).</summary>
    public void Tick(DateTime utcNow)
    {
        foreach (var kv in _groups)
        {
            var state = kv.Value;
            lock (state)
            {
                var elapsed = (utcNow - state.LastTickUtc).TotalSeconds;
                if (elapsed <= 0) continue;
                var currentCount = Interlocked.Read(ref state.MessageCount);
                var delta = currentCount - state.LastObservedCount;
                var instantaneous = delta / elapsed;
                state.EwmaMsgsPerSecond =
                    _alpha * instantaneous + (1.0 - _alpha) * state.EwmaMsgsPerSecond;
                state.LastObservedCount = currentCount;
                state.LastTickUtc = utcNow;
            }
        }
    }

    /// <summary>Snapshot of every group's telemetry, sorted
    /// by group name for deterministic test assertions.</summary>
    public IReadOnlyList<GroupState> Snapshot()
    {
        return _groups.Values
            .OrderBy(g => g.Group, StringComparer.Ordinal)
            .ToArray();
    }

    public int GroupCount => _groups.Count;

    public void Clear() => _groups.Clear();
}

/// <summary>
/// Phase K Wave 23 — Bishop. Per-group Prom metric collector.
/// Renders <c>signalr_group_connections{group="…"}</c> as a
/// gauge — the value is the live count of active connections
/// in the group (drawn from
/// <see cref="SignalRConnectionRegistry.Snapshot"/>) plus a
/// rate gauge <c>signalr_group_msg_rate{group="…"}</c> derived
/// from <see cref="SignalRGroupTelemetry"/>.
/// </summary>
public sealed class SignalRGroupMetrics
{
    public const string ConnectionsMetricName = "signalr_group_connections";
    public const string RateMetricName = "signalr_group_msg_rate";
    public const string GroupLabel = "group";
    public const string UnknownGroup = "_default";

    private readonly SignalRConnectionRegistry _registry;
    private readonly SignalRGroupTelemetry _telemetry;

    public SignalRGroupMetrics(
        SignalRConnectionRegistry registry,
        SignalRGroupTelemetry telemetry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public void AppendPrometheus(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);
        sb.Append("# HELP ").Append(ConnectionsMetricName)
          .AppendLine(" Live SignalR connection count per hub group. Gauge keyed by `group`.");
        sb.Append("# TYPE ").Append(ConnectionsMetricName).AppendLine(" gauge");
        var snapshot = _registry.Snapshot();
        var byGroup = snapshot
            .GroupBy(e => string.IsNullOrEmpty(e.Group) ? UnknownGroup : e.Group, StringComparer.Ordinal);
        foreach (var g in byGroup)
        {
            sb.Append(ConnectionsMetricName)
              .Append('{').Append(GroupLabel).Append("=\"")
              .Append(Escape(g.Key)).Append("\"} ")
              .AppendLine(g.Count().ToString(CultureInfo.InvariantCulture));
        }
        sb.Append("# HELP ").Append(RateMetricName)
          .AppendLine(" EWMA-smoothed SignalR messages-per-second per hub group.");
        sb.Append("# TYPE ").Append(RateMetricName).AppendLine(" gauge");
        foreach (var s in _telemetry.Snapshot())
        {
            sb.Append(RateMetricName)
              .Append('{').Append(GroupLabel).Append("=\"")
              .Append(Escape(string.IsNullOrEmpty(s.Group) ? UnknownGroup : s.Group)).Append("\"} ")
              .AppendLine(s.EwmaMsgsPerSecond.ToString("F4", CultureInfo.InvariantCulture));
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
/// Phase K Wave 23 — Bishop. Background service that ticks
/// <see cref="SignalRGroupTelemetry"/> on a fixed cadence so
/// the EWMA rate stays current even when the
/// <c>/api/signalr/groups</c> endpoint isn't being polled.
/// The tick cadence is intentionally tight (default 1s) — the
/// per-tick work is bounded by the group count and is a few
/// arithmetic ops per group, so the cost is negligible.
/// </summary>
public sealed class SignalRGroupTelemetryTickService : BackgroundService
{
    private readonly SignalRGroupTelemetry _telemetry;
    private readonly TimeSpan _tickInterval;

    public SignalRGroupTelemetryTickService(SignalRGroupTelemetry telemetry)
        : this(telemetry, TimeSpan.FromSeconds(1)) { }

    public SignalRGroupTelemetryTickService(SignalRGroupTelemetry telemetry, TimeSpan tickInterval)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        if (tickInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tickInterval));
        }
        _tickInterval = tickInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _telemetry.Tick(DateTime.UtcNow);
                await Task.Delay(_tickInterval, stoppingToken);
            }
            catch (TaskCanceledException) { return; }
            catch { /* swallow per-tick failures so a single
                       transient hiccup does not exit the loop */ }
        }
    }
}

/// <summary>
/// Phase K Wave 23 — Bishop. Admin-gated read-only endpoint
/// returning per-group connection + EWMA-rate telemetry. Surface:
/// <c>GET /api/signalr/groups</c>.
///
/// <para>Auth: 401 / 403 / 200. The endpoint is read-only and
/// does not mutate any state; the <c>X-Admin-Reason</c> header
/// is NOT required (matches the W22 diagnostic-read pattern).</para>
/// </summary>
[ApiController]
[Route("api/signalr/groups")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class SignalRGroupTelemetryController : ControllerBase
{
    private readonly AuthCookieService _cookies;
    private readonly SignalRConnectionRegistry _registry;
    private readonly SignalRGroupTelemetry _telemetry;

    public SignalRGroupTelemetryController(
        AuthCookieService cookies,
        SignalRConnectionRegistry registry,
        SignalRGroupTelemetry telemetry)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    [HttpGet]
    public async Task<IActionResult> GetGroups(CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }

        var snapshot = _registry.Snapshot();
        var byGroupConnections = snapshot
            .GroupBy(e => string.IsNullOrEmpty(e.Group) ? "_default" : e.Group, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byGroupTelemetry = _telemetry.Snapshot()
            .ToDictionary(s => string.IsNullOrEmpty(s.Group) ? "_default" : s.Group,
                          s => s, StringComparer.Ordinal);

        var allGroups = byGroupConnections.Keys
            .Union(byGroupTelemetry.Keys, StringComparer.Ordinal)
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToArray();

        var rows = allGroups.Select(g => new
        {
            group = g,
            connections = byGroupConnections.GetValueOrDefault(g, 0),
            messageCount = byGroupTelemetry.TryGetValue(g, out var t) ? t.MessageCount : 0,
            ewmaMsgsPerSecond = byGroupTelemetry.TryGetValue(g, out var t2) ? t2.EwmaMsgsPerSecond : 0.0,
            lastTickUtc = byGroupTelemetry.TryGetValue(g, out var t3) ? (DateTime?)t3.LastTickUtc : null,
        }).ToArray();

        return Ok(new
        {
            totalGroups = rows.Length,
            totalConnections = snapshot.Count,
            groups = rows,
        });
    }
}
