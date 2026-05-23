using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase K Wave 1 — quarter-boundary season rollover hosted service.
///
/// <para>Polls the <see cref="PlayerRatingService.SeasonFromDate"/> code
/// every <see cref="PollIntervalMinutes"/>. When the current season
/// differs from the most-recent value persisted into
/// <see cref="PlayerRatingHistory"/> (or, on first boot, the
/// <see cref="PlayerRatings"/> table contains rows whose season is
/// older than current), the service:</para>
/// <list type="number">
///   <item>Copies every live <see cref="PlayerRating"/> row whose
///         season is strictly older than current into
///         <see cref="PlayerRatingHistory"/>.</item>
///   <item>Removes those frozen rows from the live table so the
///         leaderboard query for the new season starts at the
///         default-elo baseline (the next match completion will
///         seed fresh rows via <see cref="PlayerRatingService.RecordMatchOutcomeAsync"/>).</item>
/// </list>
///
/// <para>The rollover is idempotent — re-running across the same
/// (season, player) pair is a no-op because the
/// <c>PlayerRatingHistory</c> table is unique on
/// <c>(PlayerId, Season)</c>; on a duplicate we keep the first-frozen
/// snapshot.</para>
/// </summary>
public sealed class SeasonRolloverService : BackgroundService
{
    /// <summary>Default poll interval. Quarter boundaries are hours-coarse
    /// transitions so checking every 30 minutes is plenty fast; the
    /// service deliberately avoids a 1-second tight loop.</summary>
    public const int DefaultPollIntervalMinutes = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeasonRolloverService> _logger;

    public SeasonRolloverService(IServiceScopeFactory scopeFactory, ILogger<SeasonRolloverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Configurable poll interval. Tests can dial down to 1 minute
    /// via override; production keeps the default.</summary>
    public int PollIntervalMinutes { get; set; } = DefaultPollIntervalMinutes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Best-effort startup settle delay so we don't fight EF warmup.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RolloverOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SeasonRolloverService tick failed; swallowing.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, PollIntervalMinutes)), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// One rollover sweep. Returns the number of rating rows frozen +
    /// removed (0 when no boundary was crossed). Public so tests +
    /// admin-trigger endpoints can invoke it without waiting for the
    /// timer.
    /// </summary>
    public async Task<int> RolloverOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ratings = scope.ServiceProvider.GetService<PlayerRatingService>();

        var currentSeason = ratings?.CurrentSeason() ?? PlayerRatingService.SeasonFromDate(DateTime.UtcNow);

        var stale = await db.PlayerRatings
            .Where(r => r.Season != currentSeason)
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;

        var existingHistoryKeys = new HashSet<(string, string)>(
            await db.PlayerRatingHistory
                .Where(h => stale.Select(s => s.Season).Distinct().Contains(h.Season))
                .Select(h => new { h.PlayerId, h.Season })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(x => (x.PlayerId, x.Season)), ct));

        var now = DateTime.UtcNow;
        foreach (var row in stale)
        {
            var key = (row.PlayerId, row.Season);
            if (!existingHistoryKeys.Contains(key))
            {
                db.PlayerRatingHistory.Add(new PlayerRatingHistory
                {
                    PlayerId = row.PlayerId,
                    Season = row.Season,
                    EloRating = row.EloRating,
                    GamesPlayed = row.GamesPlayed,
                    FrozenAt = now,
                });
            }
            db.PlayerRatings.Remove(row);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Season rollover: froze {Count} rating rows; current season is {Season}.",
            stale.Count, currentSeason);
        return stale.Count;
    }
}
