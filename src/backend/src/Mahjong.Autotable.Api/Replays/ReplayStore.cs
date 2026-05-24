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

    /// <summary>
    /// Phase K Wave 15 — Bishop. Sweep rows whose
    /// <see cref="ReplayRecord.CompletedAt"/> is strictly older
    /// than <paramref name="utcNow"/> minus <paramref name="retentionDays"/>.
    /// The W12 sweep used <see cref="ReplayRecord.ExpiresAt"/>
    /// (computed once at insert time) — a runtime change to
    /// <c>Replays:RetentionDays</c> would not retro-apply to
    /// rows already in the store. The W15 sweep evaluates the
    /// retention window against the current configured retention
    /// at each tick, so the operator can dial retention down
    /// (or up) and the next sweep honours the new window.
    ///
    /// <para>The two sweeps are intentionally orthogonal — both
    /// can run alongside each other without double-counting (the
    /// second pass over a row already deleted by the first is a
    /// no-op). The <see cref="ReplayStoreRetentionSweep"/> hosted
    /// service drives this path; see
    /// <c>docs/replay-by-id.md §4 "Retention sweep"</c>.</para>
    /// </summary>
    Task<int> SweepByCompletedAtAsync(int retentionDays, DateTime utcNow, CancellationToken ct = default);

    /// <summary>Total row count — surfaced so tests can assert
    /// the insert path landed.</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Phase K Wave 14 — Bishop. Paginated metadata-only listing
    /// backing <c>GET /api/replays</c>. Filters by completed-at
    /// range (<paramref name="fromUtc"/> / <paramref name="toUtc"/>
    /// applied against <see cref="ReplayRecord.CompletedAt"/>) and
    /// optional <paramref name="variant"/>. Returns
    /// <see cref="ReplayRecord"/> shells with the compressed
    /// payload column zeroed — the wire surface drops the heavy
    /// payload (clients pull it via
    /// <c>GET /api/replays/{replayId}</c>). Results are ordered
    /// <c>CompletedAt</c> descending (most-recent first).
    /// See <c>docs/replay-by-id.md §3</c>.
    /// </summary>
    Task<IReadOnlyList<ReplayRecord>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? variant,
        int skip,
        int take,
        CancellationToken ct = default);
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

    public Task<int> SweepByCompletedAtAsync(int retentionDays, DateTime utcNow, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return Task.FromResult(0);
        var cutoff = utcNow.AddDays(-retentionDays);
        var removed = 0;
        foreach (var pair in _rows)
        {
            if (pair.Value.CompletedAt < cutoff && _rows.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }
        return Task.FromResult(removed);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Task.FromResult(_rows.Count);

    public Task<IReadOnlyList<ReplayRecord>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? variant,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        IEnumerable<ReplayRecord> q = _rows.Values;
        if (fromUtc is { } f) q = q.Where(r => r.CompletedAt >= f);
        if (toUtc is { } t) q = q.Where(r => r.CompletedAt <= t);
        if (!string.IsNullOrWhiteSpace(variant))
        {
            var v = variant.Trim();
            q = q.Where(r => string.Equals(r.Variant, v, StringComparison.OrdinalIgnoreCase));
        }
        IReadOnlyList<ReplayRecord> rows = q
            .OrderByDescending(r => r.CompletedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .Select(r => new ReplayRecord
            {
                ReplayId = r.ReplayId,
                GameId = r.GameId,
                CompletedAt = r.CompletedAt,
                Variant = r.Variant,
                TurnCount = r.TurnCount,
                IngestedAt = r.IngestedAt,
                ExpiresAt = r.ExpiresAt,
                // Metadata-only — clients fetch the payload via
                // GET /api/replays/{replayId}.
                CompressedPayload = Array.Empty<byte>(),
            })
            .ToList();
        return Task.FromResult(rows);
    }
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

    public async Task<int> SweepByCompletedAtAsync(int retentionDays, DateTime utcNow, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;
        var cutoff = utcNow.AddDays(-retentionDays);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            return await db.Replays
                .Where(r => r.CompletedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Replay completed-at bulk-delete failed; falling back to per-row delete.");
            var rows = await db.Replays
                .Where(r => r.CompletedAt < cutoff)
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

    public async Task<IReadOnlyList<ReplayRecord>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? variant,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IQueryable<ReplayRecord> q = db.Replays.AsNoTracking();
        if (fromUtc is { } f) q = q.Where(r => r.CompletedAt >= f);
        if (toUtc is { } t) q = q.Where(r => r.CompletedAt <= t);
        if (!string.IsNullOrWhiteSpace(variant))
        {
            var v = variant.Trim();
            q = q.Where(r => r.Variant == v);
        }
        // Metadata-only projection — the heavy CompressedPayload
        // column is intentionally dropped from the wire so the
        // listing endpoint stays cheap even when the result set
        // spans tens of thousands of rows.
        return await q
            .OrderByDescending(r => r.CompletedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .Select(r => new ReplayRecord
            {
                ReplayId = r.ReplayId,
                GameId = r.GameId,
                CompletedAt = r.CompletedAt,
                Variant = r.Variant,
                TurnCount = r.TurnCount,
                IngestedAt = r.IngestedAt,
                ExpiresAt = r.ExpiresAt,
                CompressedPayload = Array.Empty<byte>(),
            })
            .ToListAsync(ct);
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

    /// <summary>
    /// Phase K Wave 13 — Bishop. When <c>true</c> (default),
    /// <c>POST /api/replays</c> requires an authenticated session
    /// with the <c>admin</c> role. Anonymous callers get HTTP 401,
    /// non-admin sessions get HTTP 403. Development fixtures can
    /// flip this to <c>false</c> to keep the W12 open-POST behaviour
    /// for fast iteration. See <c>docs/replay-by-id.md</c> §POST
    /// admin gating.
    /// </summary>
    public bool RequireAdminForPost { get; set; } = true;

    /// <summary>
    /// Phase K Wave 14 — Bishop. Default page size for the
    /// metadata-only listing endpoint
    /// (<c>GET /api/replays</c>). Bound from
    /// <c>Replays:PageSize</c>. See
    /// <c>docs/replay-by-id.md §3</c>.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Phase K Wave 14 — Bishop. Hard upper bound on the listing
    /// page size — larger client-supplied <c>limit</c> values are
    /// silently clamped.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Phase K Wave 14 — Bishop. Server-side default page size.
    /// 0 / negative → use <see cref="DefaultPageSize"/>.
    /// </summary>
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// Phase K Wave 15 — Bishop. Hourly retention sweep cadence
    /// (minutes) for <see cref="ReplayStoreRetentionSweep"/>. The
    /// W12 sweep ran daily against <c>ExpiresAt</c>; the W15
    /// sweep runs at this cadence against
    /// <c>CompletedAt &lt; now - RetentionDays</c> so changes to
    /// <see cref="RetentionDays"/> retro-apply on the next tick.
    /// Default 60 minutes. Values &lt; 1 fall back to 1 minute
    /// (tests pin a short cadence).
    /// </summary>
    public int StoreSweepIntervalMinutes { get; set; } = 60;
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

/// <summary>
/// Phase K Wave 15 — Bishop. Hourly retention sweep that
/// evaluates the current configured <c>Replays:RetentionDays</c>
/// against each row's <see cref="ReplayRecord.CompletedAt"/>.
/// Complements the W12 <see cref="ReplayRetentionSweepService"/>
/// (daily, <see cref="ReplayRecord.ExpiresAt"/>-based) by giving
/// the operator a live knob — a runtime change to
/// <c>Replays:RetentionDays</c> takes effect on the next hourly
/// tick instead of only on rows ingested after the change.
///
/// <para>The two sweeps coexist: both delete the same row at
/// most once. The W15 sweep is registered only when
/// <c>Replays:StorageImpl="Ef"</c> — the in-memory store has no
/// on-disk footprint and the W12 service is sufficient.</para>
///
/// <para>Toggle: <see cref="ReplayOptions.StoreSweepIntervalMinutes"/>
/// (default 60). See <c>docs/replay-by-id.md §4</c>.</para>
/// </summary>
public sealed class ReplayStoreRetentionSweep : BackgroundService
{
    public const int DefaultSweepIntervalMinutes = 60;

    private readonly IReplayStore _store;
    private readonly ReplayOptions _options;
    private readonly ILogger<ReplayStoreRetentionSweep> _logger;

    public ReplayStoreRetentionSweep(
        IReplayStore store,
        ReplayOptions options,
        ILogger<ReplayStoreRetentionSweep> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = _options.StoreSweepIntervalMinutes > 0
            ? _options.StoreSweepIntervalMinutes
            : DefaultSweepIntervalMinutes;
        var interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
        _logger.LogInformation(
            "ReplayStoreRetentionSweep started (interval={Minutes}m, retention={Days}d).",
            interval.TotalMinutes,
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
                    "ReplayStoreRetentionSweep failed (non-fatal); next tick in {Minutes}m.",
                    interval.TotalMinutes);
            }
        }

        _logger.LogInformation("ReplayStoreRetentionSweep stopped.");
    }

    /// <summary>Single-sweep entry-point exposed so tests can
    /// drive deletions deterministically.</summary>
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var retention = _options.RetentionDays > 0
            ? _options.RetentionDays
            : ReplayOptions.DefaultRetentionDays;
        var utcNow = DateTime.UtcNow;
        var removed = await _store.SweepByCompletedAtAsync(retention, utcNow, ct);
        if (removed > 0)
        {
            _logger.LogInformation(
                "ReplayStoreRetentionSweep removed {Count} record(s) older than {Days}d (cutoff={Cutoff:O}).",
                removed, retention, utcNow.AddDays(-retention));
        }
        return removed;
    }
}
