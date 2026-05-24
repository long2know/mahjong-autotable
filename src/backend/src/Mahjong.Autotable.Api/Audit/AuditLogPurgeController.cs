using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Audit;

/// <summary>
/// Phase K Wave 23 — Bishop. Prom counter for admin-driven
/// audit-log purge calls. Surfaces
/// <c>audit_log_purge_rows_total{outcome="…"}</c>; outcome
/// buckets are <c>purged</c> (rows deleted) / <c>noop</c>
/// (zero rows matched). Renders the HELP+TYPE preamble
/// unconditionally so dashboards see a stable shape.
/// </summary>
public sealed class AuditLogPurgeMetrics
{
    public const string MetricName = "audit_log_purge_rows_total";
    public const string OutcomeLabel = "outcome";

    public const string OutcomePurged = "purged";
    public const string OutcomeNoop = "noop";

    private readonly ConcurrentDictionary<string, long> _counters =
        new(StringComparer.Ordinal);

    public void Add(string outcome, long count)
    {
        var label = string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome;
        _counters.AddOrUpdate(label, count, (_, prev) => prev + count);
    }

    public long Get(string outcome) => _counters.TryGetValue(outcome, out var v) ? v : 0;

    public IReadOnlyDictionary<string, long> Snapshot() =>
        new Dictionary<string, long>(_counters, StringComparer.Ordinal);

    public void AppendPrometheus(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);
        sb.Append("# HELP ").Append(MetricName)
          .AppendLine(" Total audit-log rows purged by the W23 retention purge admin surface. Labelled by `outcome` (`purged` / `noop`).");
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
/// Phase K Wave 23 — Bishop. Pure service that purges
/// <see cref="ReconnectAuditEntry"/> rows older than the
/// supplied threshold. The W22 audit-log query surface
/// reads the table; this is the matching write surface that
/// keeps the table bounded so the operator console can
/// page through "everything since Wave 22" without choking
/// on 100K+ rows.
///
/// <para>The service is intentionally narrow — no batching,
/// no per-kind filtering. Operators who need surgical
/// per-kind purges can drop a row via direct DB access;
/// the W23 surface only supports the time-based bulk
/// purge.</para>
/// </summary>
public sealed class AuditLogPurgeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditLogPurgeMetrics _metrics;

    public AuditLogPurgeService(
        IServiceScopeFactory scopeFactory,
        AuditLogPurgeMetrics metrics)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>Result envelope from a purge call. Wire-stable
    /// — the controller serialises it verbatim.</summary>
    public sealed record PurgeResult(
        int Purged,
        DateTime CutoffUtc,
        DateTime? EarliestRemainingUtc,
        long RemainingRowCount);

    /// <summary>Purges every <see cref="ReconnectAuditEntry"/>
    /// row whose <see cref="ReconnectAuditEntry.At"/> is
    /// strictly older than the supplied
    /// <paramref name="cutoffUtc"/>. Returns the count purged
    /// + the earliest remaining timestamp (null when no rows
    /// remain after the purge).</summary>
    public async Task<PurgeResult> PurgeAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var toPurge = await db.ReconnectAuditEntries
            .Where(r => r.At < cutoffUtc)
            .ToListAsync(ct);
        int purged = 0;
        if (toPurge.Count > 0)
        {
            db.ReconnectAuditEntries.RemoveRange(toPurge);
            purged = toPurge.Count;
            await db.SaveChangesAsync(ct);
        }
        _metrics.Add(purged > 0 ? AuditLogPurgeMetrics.OutcomePurged : AuditLogPurgeMetrics.OutcomeNoop, purged);

        var remaining = await db.ReconnectAuditEntries.AsNoTracking().LongCountAsync(ct);
        DateTime? earliest = null;
        if (remaining > 0)
        {
            earliest = await db.ReconnectAuditEntries
                .AsNoTracking()
                .OrderBy(r => r.At)
                .Select(r => (DateTime?)r.At)
                .FirstOrDefaultAsync(ct);
        }
        return new PurgeResult(purged, cutoffUtc, earliest, remaining);
    }
}

/// <summary>
/// Phase K Wave 23 — Bishop. Admin-gated audit-log retention
/// purge endpoint. Surface:
/// <c>POST /api/audit-log/purge?olderThanDays=N</c>.
///
/// <para>Deletes every <see cref="ReconnectAuditEntry"/> row
/// older than <c>now - olderThanDays</c>. Returns the count
/// purged + the earliest remaining timestamp.</para>
///
/// <para>Auth: 401 / 403 / 400 / 200. Mandatory
/// <c>X-Admin-Reason</c> header. Emits one
/// <see cref="ReconnectAuditEntry.KindAuditLogPurged"/> audit
/// row per call — the meta-audit row is stamped AFTER the
/// purge so it's never accidentally swept by the same
/// call.</para>
/// </summary>
[ApiController]
[Route("api/audit-log/purge")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class AuditLogPurgeController : ControllerBase
{
    public const string AdminReasonHeader = "X-Admin-Reason";
    public const int MaxAdminReasonLength = 512;
    public const int MinOlderThanDays = 1;
    public const int MaxOlderThanDays = 3650;

    public const string ErrorOlderThanDaysMissing = "older-than-days-required";
    public const string ErrorOlderThanDaysOutOfRange = "older-than-days-out-of-range";

    private readonly AuthCookieService _cookies;
    private readonly AuditLogPurgeService _purge;
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditLogPurgeController(
        AuthCookieService cookies,
        AuditLogPurgeService purge,
        IServiceScopeFactory scopeFactory)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _purge = purge ?? throw new ArgumentNullException(nameof(purge));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    [HttpPost]
    public async Task<IActionResult> Purge(
        [FromQuery] int? olderThanDays,
        CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }

        if (!Request.Headers.TryGetValue(AdminReasonHeader, out var reasonValues))
        {
            return BadRequest(new { error = "admin-reason-required", header = AdminReasonHeader });
        }
        var reason = reasonValues.ToString();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { error = "admin-reason-required", header = AdminReasonHeader });
        }
        if (reason.Length > MaxAdminReasonLength)
        {
            return BadRequest(new { error = "admin-reason-too-long", maximum = MaxAdminReasonLength });
        }

        if (olderThanDays is null)
        {
            return BadRequest(new { error = ErrorOlderThanDaysMissing });
        }
        if (olderThanDays < MinOlderThanDays || olderThanDays > MaxOlderThanDays)
        {
            return BadRequest(new
            {
                error = ErrorOlderThanDaysOutOfRange,
                minimum = MinOlderThanDays,
                maximum = MaxOlderThanDays,
            });
        }

        var nowUtc = DateTime.UtcNow;
        var cutoff = nowUtc.AddDays(-olderThanDays.Value);
        var result = await _purge.PurgeAsync(cutoff, ct);

        // Meta-audit row recording the purge action. Written
        // after the delete so it is never accidentally caught
        // by the same call.
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var earliestIso = result.EarliestRemainingUtc?.ToString("o") ?? "null";
            db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
            {
                Id = Guid.NewGuid(),
                PlayerId = session.PlayerId,
                At = DateTime.UtcNow,
                Kind = ReconnectAuditEntry.KindAuditLogPurged,
                Detail = $"reason={reason}|olderThanDays={olderThanDays}|purged={result.Purged}|earliestRemaining={earliestIso}",
            });
            await db.SaveChangesAsync(ct);
        }

        return Ok(new
        {
            purged = result.Purged,
            olderThanDays = olderThanDays.Value,
            cutoffUtc = result.CutoffUtc,
            earliestRemainingUtc = result.EarliestRemainingUtc,
            remainingRowCount = result.RemainingRowCount,
            reason,
        });
    }
}
