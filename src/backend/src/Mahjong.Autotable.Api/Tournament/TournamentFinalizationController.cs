using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase K Wave 22 — Bishop. Admin-gated endpoint that
/// finalizes a tournament: locks every round, computes the
/// final standings, persists one
/// <see cref="TournamentStanding"/> row per player, marks the
/// tournament status as <c>complete</c>, and emits a
/// <see cref="ReconnectAuditEntry.KindTournamentCompleted"/>
/// event row. Surface:
/// <c>POST /api/admin/tournaments/{id}/finalize</c>.
///
/// <para>The endpoint is idempotent — a second call returns
/// 200 with the already-recorded standings (no rows
/// re-stamped). Tournaments with any <c>pending</c> or
/// <c>in-progress</c> matches are refused with 409 +
/// <see cref="ErrorIncompleteRounds"/> so the operator surface
/// fails loudly rather than silently locking an unfinished
/// event.</para>
///
/// <para>Auth: 401 / 403 / 400 / 404 / 409 / 200. Mandatory
/// <c>X-Admin-Reason</c> header — the reason is stamped on
/// the audit row so the trail captures WHY the operator
/// finalized the tournament.</para>
/// </summary>
[ApiController]
[Route("api/admin/tournaments/{id:guid}/finalize")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class TournamentFinalizationController : ControllerBase
{
    public const string AdminReasonHeader = "X-Admin-Reason";
    public const int MaxAdminReasonLength = 512;

    public const string ErrorIncompleteRounds = "incomplete-rounds";
    public const string ErrorTournamentNotFound = "tournament-not-found";
    public const string ErrorTournamentNotStarted = "tournament-not-started";

    private readonly AuthCookieService _cookies;
    private readonly IServiceScopeFactory _scopeFactory;

    public TournamentFinalizationController(
        AuthCookieService cookies,
        IServiceScopeFactory scopeFactory)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    [HttpPost]
    public async Task<IActionResult> Finalize(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }
        if (!HttpContext.Request.Headers.TryGetValue(AdminReasonHeader, out var reasonValues))
        {
            return BadRequest(new { error = "admin-reason-required", header = AdminReasonHeader });
        }
        var adminReason = reasonValues.ToString();
        if (string.IsNullOrWhiteSpace(adminReason))
        {
            return BadRequest(new { error = "admin-reason-required", header = AdminReasonHeader });
        }
        if (adminReason.Length > MaxAdminReasonLength)
        {
            return BadRequest(new { error = "admin-reason-too-long", maximum = MaxAdminReasonLength });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament is null)
        {
            return NotFound(new { error = ErrorTournamentNotFound, tournamentId = id });
        }

        // Idempotent: already-finalized tournaments return the
        // recorded standings without re-stamping rows. The
        // schema-level (TournamentId, PlayerId) unique index
        // is the safety net.
        if (string.Equals(tournament.Status, "complete", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await db.TournamentStandings
                .AsNoTracking()
                .Where(s => s.TournamentId == id)
                .OrderBy(s => s.Rank)
                .ToListAsync(ct);
            return Ok(new
            {
                tournamentId = id,
                status = tournament.Status,
                idempotent = true,
                completedAt = tournament.CompletedAt,
                standings = existing.Select(s => new
                {
                    playerId = s.PlayerId,
                    rank = s.Rank,
                    points = s.Points,
                    gamesPlayed = s.GamesPlayed,
                    buchholz = s.Buchholz,
                    sonnebornBerger = s.SonnebornBerger,
                }).ToArray(),
                adminReason,
            });
        }

        if (string.Equals(tournament.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = ErrorTournamentNotStarted, status = tournament.Status });
        }

        var matches = await db.TournamentMatches
            .Where(m => m.TournamentId == id)
            .ToListAsync(ct);

        var incomplete = matches
            .Where(m => !string.Equals(m.Status, "complete", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (incomplete.Count > 0)
        {
            return Conflict(new
            {
                error = ErrorIncompleteRounds,
                tournamentId = id,
                incompleteMatches = incomplete.Count,
                rounds = incomplete.Select(m => m.Round).Distinct().OrderBy(r => r).ToArray(),
            });
        }

        // Compute per-player (wins, gamesPlayed) from the
        // completed matches. Multi-seat (round-robin) matches
        // contribute one game for every seated player.
        var playerWins = new Dictionary<string, int>(StringComparer.Ordinal);
        var playerGames = new Dictionary<string, int>(StringComparer.Ordinal);
        var registrations = await db.TournamentRegistrations
            .AsNoTracking()
            .Where(r => r.TournamentId == id)
            .ToListAsync(ct);
        foreach (var reg in registrations)
        {
            playerWins.TryAdd(reg.PlayerId, 0);
            playerGames.TryAdd(reg.PlayerId, 0);
        }
        foreach (var m in matches)
        {
            foreach (var seated in EnumerateSeats(m))
            {
                playerGames[seated] = playerGames.GetValueOrDefault(seated, 0) + 1;
            }
            if (!string.IsNullOrWhiteSpace(m.WinnerPlayerId))
            {
                playerWins[m.WinnerPlayerId] = playerWins.GetValueOrDefault(m.WinnerPlayerId, 0) + 1;
            }
        }

        // Phase K Wave 23 — Bishop. Buchholz + Sonneborn-Berger
        // tiebreaker computation. Buchholz = Σ (each opponent's
        // final wins). Sonneborn-Berger = Σ (defeated opponent
        // wins) + 0.5 · Σ (drawn opponent wins). Higher Buchholz
        // means the player faced a tougher field; higher SB
        // means the player beat the strong field. Both are
        // persisted on the standings row.
        var buchholz = ComputeBuchholz(matches, playerGames.Keys, playerWins);
        var sonneborn = ComputeSonnebornBerger(matches, playerGames.Keys, playerWins);

        // Competition ranking with multi-key tiebreaker:
        // primary = wins (Points), secondary = Buchholz,
        // tertiary = Sonneborn-Berger, then PlayerId (ordinal)
        // for deterministic settlement when every tiebreaker
        // ties.
        var ordered = playerGames.Keys
            .OrderByDescending(p => playerWins.GetValueOrDefault(p, 0))
            .ThenByDescending(p => buchholz.GetValueOrDefault(p, 0.0))
            .ThenByDescending(p => sonneborn.GetValueOrDefault(p, 0.0))
            .ThenBy(p => p, StringComparer.Ordinal)
            .ToList();
        var finalizedAt = DateTime.UtcNow;
        int currentRank = 0;
        int seenCount = 0;
        // Tied-on-the-full-tiebreaker-stack share a rank — the
        // W22 single-key competition-ranking guard is widened
        // to a multi-key tuple here so a player tied on every
        // observable tiebreaker shares the rank rather than
        // arbitrarily resolving via PlayerId (PlayerId is the
        // serialisation tie-breaker, not a ranking signal).
        (int W, double B, double S)? lastKey = null;
        var standings = new List<TournamentStanding>(ordered.Count);
        foreach (var pid in ordered)
        {
            seenCount++;
            var pts = playerWins.GetValueOrDefault(pid, 0);
            var b = buchholz.GetValueOrDefault(pid, 0.0);
            var s = sonneborn.GetValueOrDefault(pid, 0.0);
            var key = (pts, b, s);
            if (lastKey is null || !lastKey.Value.Equals(key))
            {
                currentRank = seenCount;
                lastKey = key;
            }
            standings.Add(new TournamentStanding
            {
                Id = Guid.NewGuid(),
                TournamentId = id,
                PlayerId = pid,
                Rank = currentRank,
                Points = pts,
                GamesPlayed = playerGames.GetValueOrDefault(pid, 0),
                Buchholz = b,
                SonnebornBerger = s,
                FinalizedAtUtc = finalizedAt,
            });
        }
        db.TournamentStandings.AddRange(standings);

        tournament.Status = "complete";
        tournament.CompletedAt = finalizedAt;

        var winnerPlayerId = standings.FirstOrDefault(s => s.Rank == 1)?.PlayerId ?? string.Empty;

        db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
        {
            Id = Guid.NewGuid(),
            PlayerId = "admin",
            At = finalizedAt,
            Kind = ReconnectAuditEntry.KindTournamentFinalized,
            Detail = $"reason={adminReason}|tournamentId={id:N}|standings={standings.Count}",
        });
        db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
        {
            Id = Guid.NewGuid(),
            PlayerId = "admin",
            At = finalizedAt,
            Kind = ReconnectAuditEntry.KindTournamentCompleted,
            Detail = $"tournamentId={id:N}|winnerPlayerId={winnerPlayerId}|playerCount={standings.Count}",
        });

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            tournamentId = id,
            status = tournament.Status,
            idempotent = false,
            completedAt = tournament.CompletedAt,
            standings = standings.Select(s => new
            {
                playerId = s.PlayerId,
                rank = s.Rank,
                points = s.Points,
                gamesPlayed = s.GamesPlayed,
                buchholz = s.Buchholz,
                sonnebornBerger = s.SonnebornBerger,
            }).ToArray(),
            adminReason,
        });
    }

    /// <summary>
    /// Enumerates every non-empty seat on a match — supports
    /// 2-player formats (single-elim/Swiss) which leave
    /// Player3Id/Player4Id null AND 4-player round-robin
    /// matches which populate all four.
    /// </summary>
    internal static IEnumerable<string> EnumerateSeats(TournamentMatch m)
    {
        if (!string.IsNullOrWhiteSpace(m.Player1Id)) yield return m.Player1Id;
        if (!string.IsNullOrWhiteSpace(m.Player2Id)) yield return m.Player2Id;
        if (!string.IsNullOrWhiteSpace(m.Player3Id)) yield return m.Player3Id!;
        if (!string.IsNullOrWhiteSpace(m.Player4Id)) yield return m.Player4Id!;
    }

    /// <summary>
    /// Phase K Wave 23 — Bishop. Computes the Buchholz score
    /// for every player. Buchholz(p) = Σ wins(o) where the
    /// sum is taken over every opponent <c>o</c> who shared a
    /// completed match with <c>p</c>. A multi-seat (round-robin)
    /// match counts every seated peer as an opponent, so a
    /// 4-player table contributes 3 opponents per seated player
    /// (which matches FIDE's "Buchholz for round-robin"
    /// definition).
    ///
    /// <para>Internal-static so the math can be pinned in
    /// isolation by the W23 test suite without going through a
    /// controller round-trip.</para>
    /// </summary>
    internal static Dictionary<string, double> ComputeBuchholz(
        IEnumerable<TournamentMatch> matches,
        IEnumerable<string> players,
        IReadOnlyDictionary<string, int> playerWins)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(playerWins);

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in players) result[p] = 0.0;

        foreach (var m in matches)
        {
            var seats = EnumerateSeats(m).ToList();
            foreach (var seat in seats)
            {
                foreach (var opponent in seats)
                {
                    if (string.Equals(opponent, seat, StringComparison.Ordinal)) continue;
                    var w = playerWins.TryGetValue(opponent, out var wins) ? wins : 0;
                    result[seat] = result.GetValueOrDefault(seat, 0.0) + w;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Phase K Wave 23 — Bishop. Computes Sonneborn-Berger for
    /// every player. SB(p) = Σ wins(o where p beat o) +
    /// 0.5 · Σ wins(o where p drew o). A "draw" is a completed
    /// match with no WinnerPlayerId (current schema doesn't
    /// surface draws but the future-proofing is cheap). A
    /// multi-seat match where the winner is one of the seats
    /// counts as a "p beat o" relation for every other seated
    /// player from the winner's perspective; the losers
    /// contribute the winner's score weighted as half (so the
    /// SB calculation degrades gracefully in 4-seat formats
    /// where head-to-head doesn't apply cleanly).
    /// </summary>
    internal static Dictionary<string, double> ComputeSonnebornBerger(
        IEnumerable<TournamentMatch> matches,
        IEnumerable<string> players,
        IReadOnlyDictionary<string, int> playerWins)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(playerWins);

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in players) result[p] = 0.0;

        foreach (var m in matches)
        {
            var seats = EnumerateSeats(m).ToList();
            var winner = string.IsNullOrWhiteSpace(m.WinnerPlayerId) ? null : m.WinnerPlayerId;
            foreach (var seat in seats)
            {
                foreach (var opponent in seats)
                {
                    if (string.Equals(opponent, seat, StringComparison.Ordinal)) continue;
                    var w = playerWins.TryGetValue(opponent, out var wins) ? wins : 0;
                    if (winner is null)
                    {
                        // Drawn (no winner recorded) — half weight
                        // from both perspectives.
                        result[seat] = result.GetValueOrDefault(seat, 0.0) + 0.5 * w;
                    }
                    else if (string.Equals(winner, seat, StringComparison.Ordinal))
                    {
                        // Seat won — full weight from the
                        // opponent's wins.
                        result[seat] = result.GetValueOrDefault(seat, 0.0) + w;
                    }
                    // Seat lost — contributes nothing.
                }
            }
        }
        return result;
    }
}
