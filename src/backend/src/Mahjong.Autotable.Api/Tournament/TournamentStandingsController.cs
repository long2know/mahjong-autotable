using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase K Wave 23 — Bishop. Anonymous-but-rate-limited read
/// surface that returns the persisted final standings for a
/// tournament. Surface:
/// <c>GET /api/tournaments/{id}/standings</c>.
///
/// <para>W22 landed <see cref="TournamentFinalizationController"/>
/// which stamps one <see cref="Data.Entities.TournamentStanding"/>
/// row per player. W23 extends the row shape with two FIDE-style
/// tiebreakers (Buchholz + Sonneborn-Berger) and surfaces them
/// to the public read endpoint so a leaderboard renderer can
/// show the tiebreaker column without re-walking the match
/// graph.</para>
///
/// <para>The endpoint returns 200 with an empty <c>standings</c>
/// array when the tournament exists but has not yet been
/// finalized (the operator dashboard renders "no standings
/// yet" in that case). It returns 404 when no tournament row
/// matches the supplied id. The response is sorted by rank
/// ascending (1 = first place).</para>
///
/// <para>Wire shape:</para>
/// <code>
/// {
///   "tournamentId": "…",
///   "status": "complete" | "in-progress" | "draft",
///   "completedAt": "…iso8601 | null",
///   "count": 4,
///   "standings": [
///     { "playerId": "alice", "rank": 1, "points": 3,
///       "gamesPlayed": 3, "buchholz": 7.0, "sonnebornBerger": 4.5 },
///     …
///   ]
/// }
/// </code>
/// </summary>
[ApiController]
[Route("api/tournaments/{id:guid}/standings")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class TournamentStandingsController : ControllerBase
{
    public const string ErrorTournamentNotFound = "tournament-not-found";

    private readonly IServiceScopeFactory _scopeFactory;

    public TournamentStandingsController(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tournament = await db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament is null)
        {
            return NotFound(new { error = ErrorTournamentNotFound, tournamentId = id });
        }

        var rows = await db.TournamentStandings
            .AsNoTracking()
            .Where(s => s.TournamentId == id)
            .OrderBy(s => s.Rank)
            .ThenBy(s => s.PlayerId)
            .ToListAsync(ct);

        return Ok(new
        {
            tournamentId = id,
            status = tournament.Status,
            completedAt = tournament.CompletedAt,
            count = rows.Count,
            standings = rows.Select(s => new
            {
                playerId = s.PlayerId,
                rank = s.Rank,
                points = s.Points,
                gamesPlayed = s.GamesPlayed,
                buchholz = s.Buchholz,
                sonnebornBerger = s.SonnebornBerger,
                finalizedAtUtc = s.FinalizedAtUtc,
            }).ToArray(),
        });
    }
}
