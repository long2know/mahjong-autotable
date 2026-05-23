using System.Globalization;
using System.Text;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase K Wave 1 — match-history export surface.
///
/// <para><b>GET /api/games?playerId=&amp;from=&amp;to=&amp;format=json|csv&amp;limit=&amp;offset=</b>
/// — returns the completed games this player participated in.
/// <c>playerId</c> is required. <c>from</c> + <c>to</c> are inclusive
/// ISO-8601 UTC instants applied against <c>CompletedAt</c>. <c>format</c>
/// is <c>json</c> (default) or <c>csv</c>; the CSV body includes exactly
/// the columns <c>GameId, StartedAt, CompletedAt, FinalScore, Won,
/// OpponentPlayerIds, RulePresetId</c> per the Phase-K-1 contract.</para>
///
/// <para>Rate limit: the read-only token-bucket-api policy, same shape as
/// every <c>/api/**</c> read endpoint (Phase J Wave 6 default), wired
/// here explicitly so the route inherits the bucket regardless of where
/// the global <c>MapControllers</c> RequireRateLimiting() configures it.</para>
/// </summary>
[ApiController]
[Route("api/games")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class GamesHistoryController : ControllerBase
{
    /// <summary>Maximum page size accepted by the endpoint. Larger
    /// <c>limit</c> values are clamped silently.</summary>
    public const int MaxLimit = 200;

    /// <summary>Default page size when <c>limit</c> is omitted.</summary>
    public const int DefaultLimit = 50;

    private readonly IServiceScopeFactory _scopeFactory;

    public GamesHistoryController(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Lists this player's completed games. Returns <c>400</c> when
    /// <c>playerId</c> is missing or <c>format</c> is unrecognised. JSON
    /// shape: <c>{ playerId, total, limit, offset, games:[…] }</c>. CSV
    /// shape: a single header row followed by one data row per game.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? playerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? format,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return BadRequest(new { error = "playerId is required." });
        }

        var fmt = (format ?? "json").Trim().ToLowerInvariant();
        if (fmt != "json" && fmt != "csv")
        {
            return BadRequest(new { error = "format must be one of: json, csv." });
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(0, offset ?? 0);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.PlayerGameHistory
            .AsNoTracking()
            .Where(h => h.PlayerId == playerId);
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            q = q.Where(h => h.CompletedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
            q = q.Where(h => h.CompletedAt <= toUtc);
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(h => h.CompletedAt)
            .ThenBy(h => h.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .ToListAsync(ct);

        if (fmt == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine("GameId,StartedAt,CompletedAt,FinalScore,Won,OpponentPlayerIds,RulePresetId");
            foreach (var r in rows)
            {
                csv.Append(r.GameId).Append(',');
                csv.Append(r.StartedAt.ToString("o", CultureInfo.InvariantCulture)).Append(',');
                csv.Append(r.CompletedAt.ToString("o", CultureInfo.InvariantCulture)).Append(',');
                csv.Append(r.FinalScore).Append(',');
                csv.Append(r.Won ? "true" : "false").Append(',');
                csv.Append(CsvEscape(r.OpponentPlayerIdsCsv)).Append(',');
                csv.Append(r.RulePresetId?.ToString() ?? string.Empty);
                csv.AppendLine();
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", $"games-{playerId}.csv");
        }

        return Ok(new
        {
            playerId,
            total,
            limit = effectiveLimit,
            offset = effectiveOffset,
            games = rows.Select(r => new
            {
                gameId = r.GameId,
                seatIndex = r.SeatIndex,
                finalScore = r.FinalScore,
                won = r.Won,
                startedAt = r.StartedAt,
                completedAt = r.CompletedAt,
                opponentPlayerIds = string.IsNullOrEmpty(r.OpponentPlayerIdsCsv)
                    ? Array.Empty<string>()
                    : r.OpponentPlayerIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
                rulePresetId = r.RulePresetId,
            }).ToArray(),
        });
    }

    /// <summary>
    /// Wraps a CSV cell in double quotes when it contains a comma, quote,
    /// CR, or LF. Quotes inside the value are doubled per RFC 4180.
    /// Empty strings pass through unquoted (the bare blank between
    /// commas is the canonical empty representation).
    /// </summary>
    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
