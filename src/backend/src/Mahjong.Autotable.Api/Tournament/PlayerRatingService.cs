using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase K Wave 1 — Elo-rating configuration. Bound from the
/// <c>Rating</c> section in <c>appsettings.json</c>:
/// <code>
/// "Rating": {
///   "CurrentSeason": "",      // empty → compute from UTC date
///   "DefaultElo": 1200,
///   "KFactor": 32
/// }
/// </code>
/// </summary>
public sealed class RatingOptions
{
    /// <summary>Hard-coded season override (e.g. <c>2026-Q1</c>). When
    /// empty / whitespace, <see cref="PlayerRatingService.CurrentSeason"/>
    /// derives the season from the current UTC date.</summary>
    public string CurrentSeason { get; set; } = string.Empty;

    /// <summary>Baseline rating applied to new players and on every
    /// season reset. Defaults to <see cref="PlayerRating.DefaultElo"/>.</summary>
    public int DefaultElo { get; set; } = PlayerRating.DefaultElo;

    /// <summary>K-factor for the standard Elo update rule. Defaults to
    /// <see cref="PlayerRating.KFactor"/>.</summary>
    public int KFactor { get; set; } = PlayerRating.KFactor;
}

/// <summary>
/// Phase K Wave 1 — owns the per-(player, season) <see cref="PlayerRating"/>
/// table. Recomputes Elo for tournament-match participants on
/// completion; the cross-season rollover lives in
/// <see cref="SeasonRolloverService"/>.
///
/// <para>The Elo formula is the standard <c>R' = R + K·(S − E)</c>, with
/// <c>E = 1 / (1 + 10^((R_opp − R)/400))</c>. For 4-player Changsha
/// matches a single match nominates one winner; the winner gains rating
/// against the average of the loser ratings (treated as a virtual
/// opponent), each loser loses rating against the winner's pre-match
/// rating. This keeps the total rating mass roughly conserved while
/// avoiding the noise of pairwise updates within the loser cohort.</para>
/// </summary>
public sealed class PlayerRatingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RatingOptions _options;

    public PlayerRatingService(IServiceScopeFactory scopeFactory, IOptions<RatingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    /// <summary>Configured baseline (clamped non-negative).</summary>
    public int DefaultElo => Math.Max(0, _options.DefaultElo > 0 ? _options.DefaultElo : PlayerRating.DefaultElo);

    /// <summary>Configured K-factor (clamped positive).</summary>
    public int KFactor => Math.Max(1, _options.KFactor > 0 ? _options.KFactor : PlayerRating.KFactor);

    /// <summary>
    /// Returns the configured season code, or — when empty —
    /// derives a canonical <c>YYYY-Qn</c> code from the supplied UTC
    /// timestamp. Defaults to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public string CurrentSeason(DateTime? now = null)
    {
        var overrideValue = (_options.CurrentSeason ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(overrideValue)) return overrideValue;
        return SeasonFromDate(now ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Returns the canonical <c>YYYY-Qn</c> season code for the supplied
    /// UTC instant. Q1 = Jan–Mar, Q2 = Apr–Jun, Q3 = Jul–Sep,
    /// Q4 = Oct–Dec. Pure function — no DI dependencies; the
    /// <see cref="SeasonRolloverService"/> uses it during boundary
    /// detection too.
    /// </summary>
    public static string SeasonFromDate(DateTime utc)
    {
        var month = utc.Month;
        var quarter = month switch
        {
            <= 3 => 1,
            <= 6 => 2,
            <= 9 => 3,
            _ => 4,
        };
        return $"{utc.Year:D4}-Q{quarter}";
    }

    /// <summary>
    /// Returns the prior season's canonical code relative to
    /// <paramref name="season"/>. Used by the rollover service.
    /// </summary>
    public static string PriorSeason(string season)
    {
        if (string.IsNullOrWhiteSpace(season)) return season;
        var hyphen = season.IndexOf('-');
        if (hyphen < 0) return season;
        if (!int.TryParse(season.AsSpan(0, hyphen), out var year)) return season;
        if (season.Length < hyphen + 3 || season[hyphen + 1] != 'Q') return season;
        if (!int.TryParse(season.AsSpan(hyphen + 2), out var quarter)) return season;
        var priorQuarter = quarter - 1;
        var priorYear = year;
        if (priorQuarter == 0) { priorQuarter = 4; priorYear -= 1; }
        return $"{priorYear:D4}-Q{priorQuarter}";
    }

    /// <summary>
    /// Records a tournament-match outcome against the live season rating
    /// table. <paramref name="participantIds"/> includes every seat
    /// (winner + losers); <paramref name="winnerPlayerId"/> must appear
    /// in the list. Bot ids (<c>bot-…</c>) are filtered.
    ///
    /// <para>Returns the per-player rating delta keyed by PlayerId so
    /// callers (the runtime, tests) can audit the change without a
    /// follow-up query. The dictionary excludes filtered bots.</para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> RecordMatchOutcomeAsync(
        IReadOnlyList<string> participantIds,
        string winnerPlayerId,
        DateTime? now = null,
        CancellationToken ct = default)
    {
        if (participantIds is null || participantIds.Count < 2)
            return new Dictionary<string, int>();

        var humans = participantIds
            .Where(p => !string.IsNullOrEmpty(p) && !p.StartsWith("bot-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (humans.Count < 2 || string.IsNullOrEmpty(winnerPlayerId) || !humans.Contains(winnerPlayerId))
            return new Dictionary<string, int>();

        var season = CurrentSeason(now);
        var asOf = now ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Fetch (or seed) the live rating row for each participant.
        var rows = new Dictionary<string, PlayerRating>(StringComparer.Ordinal);
        foreach (var pid in humans)
        {
            var row = await db.PlayerRatings
                .FirstOrDefaultAsync(r => r.PlayerId == pid && r.Season == season, ct);
            if (row is null)
            {
                row = new PlayerRating
                {
                    PlayerId = pid,
                    Season = season,
                    EloRating = DefaultElo,
                    GamesPlayed = 0,
                    CreatedAt = asOf,
                    LastUpdatedAt = asOf,
                };
                db.PlayerRatings.Add(row);
            }
            rows[pid] = row;
        }

        var winner = rows[winnerPlayerId];
        var losers = rows.Where(kv => kv.Key != winnerPlayerId).Select(kv => kv.Value).ToList();
        var avgLoserRating = losers.Count == 0 ? winner.EloRating : (int)Math.Round(losers.Average(r => (double)r.EloRating));

        var deltas = new Dictionary<string, int>(StringComparer.Ordinal);

        // Winner gains against avg loser rating.
        var winnerDelta = ComputeDelta(winner.EloRating, avgLoserRating, won: true);
        winner.EloRating += winnerDelta;
        winner.GamesPlayed += 1;
        winner.LastUpdatedAt = asOf;
        deltas[winnerPlayerId] = winnerDelta;

        // Each loser loses against the winner's pre-match rating snapshot.
        var winnerSnapshot = winner.EloRating - winnerDelta;
        foreach (var loser in losers)
        {
            var loserDelta = ComputeDelta(loser.EloRating, winnerSnapshot, won: false);
            loser.EloRating += loserDelta;
            loser.GamesPlayed += 1;
            loser.LastUpdatedAt = asOf;
            deltas[loser.PlayerId] = loserDelta;
        }

        await db.SaveChangesAsync(ct);
        return deltas;
    }

    /// <summary>
    /// Standard Elo update. Returns the signed delta the caller should
    /// add to <paramref name="rating"/>. Won = +Δ, lost = −Δ; the formula
    /// derives the expected score from the rating gap and scales by
    /// <see cref="KFactor"/>.
    /// </summary>
    public int ComputeDelta(int rating, int opponentRating, bool won)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentRating - rating) / 400.0));
        var score = won ? 1.0 : 0.0;
        return (int)Math.Round(KFactor * (score - expected));
    }

    /// <summary>
    /// Paginated leaderboard for the supplied season. Returns rows
    /// ordered by Elo desc then GamesPlayed desc then PlayerId asc.
    /// </summary>
    public async Task<LeaderboardResponse> LeaderboardAsync(string season, int limit, int offset, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        season = string.IsNullOrWhiteSpace(season) ? CurrentSeason() : season.Trim();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.PlayerRatings.AsNoTracking().Where(r => r.Season == season);
        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(r => r.EloRating)
            .ThenByDescending(r => r.GamesPlayed)
            .ThenBy(r => r.PlayerId)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new LeaderboardResponse(
            Season: season,
            Total: total,
            Rows: rows.Select((r, i) => new LeaderboardRow(
                Rank: offset + i + 1,
                PlayerId: r.PlayerId,
                EloRating: r.EloRating,
                GamesPlayed: r.GamesPlayed,
                LastUpdatedAt: r.LastUpdatedAt)).ToList());
    }

    /// <summary>
    /// Reads a prior season's frozen snapshot from
    /// <see cref="PlayerRatingHistory"/>. When the snapshot table has no
    /// rows for <paramref name="season"/> (the season was never closed
    /// out — e.g. a manual config override that skipped a boundary),
    /// falls back to the live <see cref="PlayerRatings"/> filter.
    /// </summary>
    public async Task<LeaderboardResponse> SnapshotLeaderboardAsync(string season, int limit, int offset, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        season = season?.Trim() ?? string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.PlayerRatingHistory.AsNoTracking().Where(r => r.Season == season);
        var total = await q.CountAsync(ct);
        if (total == 0)
        {
            return await LeaderboardAsync(season, limit, offset, ct);
        }

        var rows = await q
            .OrderByDescending(r => r.EloRating)
            .ThenByDescending(r => r.GamesPlayed)
            .ThenBy(r => r.PlayerId)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new LeaderboardResponse(
            Season: season,
            Total: total,
            Rows: rows.Select((r, i) => new LeaderboardRow(
                Rank: offset + i + 1,
                PlayerId: r.PlayerId,
                EloRating: r.EloRating,
                GamesPlayed: r.GamesPlayed,
                LastUpdatedAt: r.FrozenAt)).ToList());
    }
}

/// <summary>
/// Phase K Wave 1 — single rating leaderboard row. <c>Rank</c> is
/// 1-based within the page slice.
/// </summary>
public sealed record LeaderboardRow(int Rank, string PlayerId, int EloRating, int GamesPlayed, DateTime LastUpdatedAt);

/// <summary>
/// Phase K Wave 1 — rating leaderboard response envelope.
/// </summary>
public sealed record LeaderboardResponse(string Season, int Total, IReadOnlyList<LeaderboardRow> Rows);
