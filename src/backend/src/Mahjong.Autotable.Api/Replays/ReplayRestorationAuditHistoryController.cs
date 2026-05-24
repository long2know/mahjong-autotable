using System.Globalization;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Replays;

/// <summary>
/// Phase K Wave 23 — Bishop. Admin-gated paginated query
/// endpoint over the W21 <see cref="ReplayRestorationAttempt"/>
/// table. Surface:
/// <c>GET /api/replays/audit/restorations?since=...&amp;page=...&amp;pageSize=...&amp;outcome=...</c>.
///
/// <para>The W21 single-replay endpoint surfaces the last 10
/// attempt rows for ONE replay; the W23 query endpoint is the
/// global trail-by-time view that auditors use when chasing a
/// "did anyone restore a replay last weekend?" question. Rows
/// are returned most-recent-first.</para>
///
/// <para>Filters:
/// <list type="bullet">
///   <item><c>since</c> — ISO 8601 UTC lower bound on
///         <see cref="ReplayRestorationAttempt.AttemptedAtUtc"/>.
///         Inclusive.</item>
///   <item><c>outcome</c> — exact match on
///         <see cref="ReplayRestorationAttempt.Outcome"/>
///         (one of the canonical wire-name constants).</item>
/// </list>
/// Paging:
/// <list type="bullet">
///   <item><c>page</c> — 1-based page index (default 1).</item>
///   <item><c>pageSize</c> — default
///         <see cref="DefaultPageSize"/>, max
///         <see cref="MaxPageSize"/>.</item>
/// </list></para>
///
/// <para>Auth: 401 / 403 / 400 / 200. Reads do NOT stamp a
/// meta-audit row by default (volume would be high); operator
/// usage is captured by the standard request log.</para>
/// </summary>
[ApiController]
[Route("api/replays/audit/restorations")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class ReplayRestorationAuditHistoryController : ControllerBase
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public const string ErrorSinceNotIso = "since-must-be-iso8601";
    public const string ErrorPageMustBePositive = "page-must-be-positive";
    public const string ErrorPageSizeMustBePositive = "page-size-must-be-positive";

    private readonly AuthCookieService _cookies;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReplayRestorationAuditHistoryController(
        AuthCookieService cookies,
        IServiceScopeFactory scopeFactory)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? since,
        [FromQuery] string? outcome,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }

        DateTime? sinceUtc = ParseUtc(since);
        if (!string.IsNullOrWhiteSpace(since) && sinceUtc is null)
        {
            return BadRequest(new { error = ErrorSinceNotIso });
        }

        int pageIndex = page ?? 1;
        if (pageIndex < 1)
        {
            return BadRequest(new { error = ErrorPageMustBePositive, page = pageIndex });
        }
        int requestedPageSize = pageSize ?? DefaultPageSize;
        if (requestedPageSize < 1)
        {
            return BadRequest(new { error = ErrorPageSizeMustBePositive, pageSize = requestedPageSize });
        }
        int effectivePageSize = Math.Min(requestedPageSize, MaxPageSize);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        IQueryable<ReplayRestorationAttempt> q = db.ReplayRestorationAttempts.AsNoTracking();
        if (sinceUtc is not null)
        {
            var lower = sinceUtc.Value;
            q = q.Where(a => a.AttemptedAtUtc >= lower);
        }
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            var trimmed = outcome.Trim();
            q = q.Where(a => a.Outcome == trimmed);
        }

        var totalCount = await q.LongCountAsync(ct);
        var rows = await q
            .OrderByDescending(a => a.AttemptedAtUtc)
            .ThenBy(a => a.Id)
            .Skip((pageIndex - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(ct);

        // Meta-audit: record that an operator queried the
        // global restoration trail (low volume so cheap).
        db.ReconnectAuditEntries.Add(new Data.Entities.ReconnectAuditEntry
        {
            Id = Guid.NewGuid(),
            PlayerId = session.PlayerId,
            At = DateTime.UtcNow,
            Kind = Data.Entities.ReconnectAuditEntry.KindReplayRestorationAuditQueried,
            Detail = $"actor={session.PlayerId}|since={(sinceUtc?.ToString("o") ?? "null")}|page={pageIndex}|pageSize={effectivePageSize}|rows={rows.Count}",
        });
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            totalCount,
            page = pageIndex,
            pageSize = effectivePageSize,
            rows = rows.Select(a => new
            {
                id = a.Id,
                replayId = a.ReplayId,
                operatorId = a.OperatorId,
                outcome = a.Outcome,
                detailMessage = a.DetailMessage,
                attemptedAtUtc = a.AttemptedAtUtc,
            }).ToArray(),
        });
    }

    internal static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return null;
        }
        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }
}
