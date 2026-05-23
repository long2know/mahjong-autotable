using System.Collections.Concurrent;
using System.Security.Cryptography;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Replays;

/// <summary>
/// Phase K Wave 12 — Bishop. Persistence seam for the durable
/// replay-by-id surface (<c>GET /api/replays/{replayId}</c> +
/// <c>POST /api/replays</c>). The store is intentionally narrow:
/// insert with a server-minted id, fetch by id, sweep expired
/// rows. Listing-by-game / paging-by-time live forward of this
/// surface in W13.
///
/// <para>Toggle: <c>Replays:StorageImpl</c>
/// (<c>"InMemory"</c> default for tests / <c>"Ef"</c> for
/// production). See <c>docs/replay-by-id.md</c>.</para>
/// </summary>
public interface IReplayStore
{
    /// <summary>Insert a replay row. The implementation mints
    /// the <see cref="ReplayRecord.ReplayId"/> when the caller
    /// passes an empty string; otherwise the supplied id wins.
    /// Returns the stored record (with id + timestamps
    /// resolved).</summary>
    Task<ReplayRecord> InsertAsync(ReplayRecord record, CancellationToken ct = default);

    /// <summary>Fetch a single replay row by id. Returns
    /// <c>null</c> when no row exists.</summary>
    Task<ReplayRecord?> GetAsync(string replayId, CancellationToken ct = default);

    /// <summary>Delete every replay row with
    /// <see cref="ReplayRecord.ExpiresAt"/> strictly older than
    /// <paramref name="utcNow"/>. Returns the count of records
    /// evicted.</summary>
    Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>Total row count — surfaced so tests can assert
    /// the insert path landed.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>
/// Phase K Wave 12 — Bishop. In-memory <see cref="IReplayStore"/>.
/// Used by tests + single-replica development. The shape mirrors
/// the EF impl so the contract test suite passes against both
/// bindings.
/// </summary>
public sealed class InMemoryReplayStore : IReplayStore
{
    private readonly ConcurrentDictionary<string, ReplayRecord> _rows =
        new(StringComparer.Ordinal);

    public Task<ReplayRecord> InsertAsync(ReplayRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.ReplayId))
        {
            record.ReplayId = ReplayIdGenerator.Mint();
        }
        if (record.IngestedAt == default) record.IngestedAt = DateTime.UtcNow;
        if (record.ExpiresAt == default)
        {
            record.ExpiresAt = (record.CompletedAt == default ? record.IngestedAt : record.CompletedAt)
                .AddDays(ReplayOptions.DefaultRetentionDays);
        }
        _rows[record.ReplayId] = record;
        return Task.FromResult(record);
    }

    public Task<ReplayRecord?> GetAsync(string replayId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(replayId)) return Task.FromResult<ReplayRecord?>(null);
        return Task.FromResult(_rows.TryGetValue(replayId, out var r) ? r : null);
    }

    public Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var pair in _rows)
        {
            if (pair.Value.ExpiresAt < utcNow && _rows.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }
        return Task.FromResult(removed);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Task.FromResult(_rows.Count);
}

/// <summary>
/// Phase K Wave 12 — Bishop. EF-backed durable replay store.
/// Persists rows to the <see cref="AppDbContext.Replays"/>
/// table. Reads are O(log n) on the
/// <see cref="ReplayRecord.ReplayId"/> primary key; the
/// retention sweep walks the <c>ExpiresAt</c> index.
/// </summary>
public sealed class EfReplayStore : IReplayStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReplayOptions _options;
    private readonly ILogger<EfReplayStore> _logger;

    public EfReplayStore(
        IServiceScopeFactory scopeFactory,
        ReplayOptions options,
        ILogger<EfReplayStore> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReplayRecord> InsertAsync(ReplayRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.ReplayId))
        {
            record.ReplayId = ReplayIdGenerator.Mint();
        }
        if (record.IngestedAt == default) record.IngestedAt = DateTime.UtcNow;
        if (record.ExpiresAt == default)
        {
            var anchor = record.CompletedAt == default ? record.IngestedAt : record.CompletedAt;
            var retentionDays = _options.RetentionDays > 0 ? _options.RetentionDays : ReplayOptions.DefaultRetentionDays;
            record.ExpiresAt = anchor.AddDays(retentionDays);
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Replays.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<ReplayRecord?> GetAsync(string replayId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(replayId)) return null;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Replays
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReplayId == replayId, ct);
    }

    public async Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            return await db.Replays
                .Where(r => r.ExpiresAt < utcNow)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Replay bulk-delete failed; falling back to per-row delete.");
            var rows = await db.Replays
                .Where(r => r.ExpiresAt < utcNow)
                .ToListAsync(ct);
            db.Replays.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            return rows.Count;
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Replays.CountAsync(ct);
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. URL-safe synthetic replay id minter.
/// Format: <c>r-{8 url-safe base64 chars}</c>. 6 bytes of
/// randomness = 48 bits ≈ 2.8 × 10¹⁴ values; collision-free at any
/// realistic ingest rate.
/// </summary>
public static class ReplayIdGenerator
{
    private const string Prefix = "r-";

    public static string Mint()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        // url-safe base64 without padding — 8 chars for 6 bytes.
        return Prefix + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. Configuration for the replay
/// persistence surface. Bound from the <c>Replays</c>
/// configuration section.
/// </summary>
public sealed class ReplayOptions
{
    /// <summary>Default retention window (days). Replay rows
    /// older than this are deleted by the nightly sweeper.</summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>Implementation selector — case-insensitive.
    /// <c>"InMemory"</c> uses <see cref="InMemoryReplayStore"/>
    /// (default for tests + dev); <c>"Ef"</c> uses
    /// <see cref="EfReplayStore"/> (default for production).</summary>
    public string StorageImpl { get; set; } = "InMemory";

    /// <summary>Retention window in days. 0 = use the default
    /// (<see cref="DefaultRetentionDays"/>).</summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;

    /// <summary>Background sweep cadence in hours. Default 24
    /// (nightly). Lowered values are tolerated for test
    /// fixtures.</summary>
    public int SweepIntervalHours { get; set; } = 24;

    /// <summary>Maximum compressed-payload size accepted on
    /// POST. Defaults to 8 MB — large enough for the longest
    /// 16-hand replays we've observed (~600 KB after gzip),
    /// small enough that a hostile client can't pin a worker
    /// thread on a multi-MB upload.</summary>
    public int MaxCompressedBytes { get; set; } = 8 * 1024 * 1024;
}

/// <summary>
/// Phase K Wave 12 — Bishop. Nightly background sweep that
/// deletes <see cref="ReplayRecord"/> rows past their configured
/// retention window. Registered as a hosted service only when
/// <c>Replays:StorageImpl="Ef"</c> — the in-memory store has no
/// on-disk footprint to sweep.
/// </summary>
public sealed class ReplayRetentionSweepService : BackgroundService
{
    private readonly IReplayStore _store;
    private readonly ReplayOptions _options;
    private readonly ILogger<ReplayRetentionSweepService> _logger;

    public ReplayRetentionSweepService(
        IReplayStore store,
        ReplayOptions options,
        ILogger<ReplayRetentionSweepService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _options.SweepIntervalHours));
        _logger.LogInformation(
            "ReplayRetentionSweepService started (interval={Hours}h, retention={Days}d).",
            interval.TotalHours,
            _options.RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ReplayRetentionSweep failed (non-fatal); next tick in {Hours}h.",
                    interval.TotalHours);
            }
        }

        _logger.LogInformation("ReplayRetentionSweepService stopped.");
    }

    /// <summary>Single-sweep entry-point exposed so tests can
    /// drive deletions deterministically.</summary>
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow;
        var removed = await _store.SweepExpiredAsync(cutoff, ct);
        if (removed > 0)
        {
            _logger.LogInformation(
                "ReplayRetentionSweep removed {Count} expired record(s) older than {Cutoff:O}.",
                removed, cutoff);
        }
        return removed;
    }
}
