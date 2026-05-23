using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Audit;

/// <summary>
/// Phase K Wave 9 — Bishop. Durable EF-backed
/// <see cref="IIdempotencyStore"/> implementation. Replaces the
/// W8 in-memory <see cref="InMemoryIdempotencyStore"/> for the
/// multi-replica production deployment — every replica shares
/// the same row set so a Stripe-style retry that lands on a
/// different pod is still caught.
///
/// <list type="bullet">
///   <item>Lookups are point reads by <see cref="IdempotencyEntry.Key"/>
///         (the entity's primary key).</item>
///   <item>Inserts run under EF Core's optimistic-concurrency
///         token so two replicas racing on the same key collapse
///         cleanly — the second insert sees a unique-violation,
///         re-reads, and the middleware emits the canonical
///         <c>409 Conflict</c> (payload-mismatch) envelope.</item>
///   <item>The 5-minute replay window is enforced at read time —
///         expired rows are silently treated as missing so a
///         stale lookup never blocks a fresh request. A
///         background sweeper (registered in <c>Program.cs</c>)
///         drops expired rows on a slow cadence so the table
///         doesn't grow unbounded under steady-state traffic.</item>
/// </list>
/// </summary>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    /// <summary>Default replay window. Stripe convention — same
    /// value as the W8 in-memory store.</summary>
    public static readonly TimeSpan DefaultReplayWindow = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfIdempotencyStore> _logger;
    private readonly TimeSpan _replayWindow;

    public EfIdempotencyStore(
        IServiceScopeFactory scopeFactory,
        ILogger<EfIdempotencyStore> logger,
        TimeSpan? replayWindow = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _replayWindow = replayWindow ?? DefaultReplayWindow;
    }

    public IdempotencyRecord? TryGet(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.IdempotencyEntries
                .AsNoTracking()
                .FirstOrDefault(e => e.Key == key);
            if (row is null) return null;

            // Defensive expiry check — middleware contract treats
            // an expired row as "not found" so a stale lookup never
            // blocks a fresh request even if the sweeper hasn't yet
            // reaped it.
            if (row.ExpiresAt <= DateTime.UtcNow) return null;

            return new IdempotencyRecord(
                Key: row.Key,
                PayloadHash: row.PayloadHash,
                RecordedAt: DateTime.SpecifyKind(row.RecordedAt, DateTimeKind.Utc),
                StatusCode: row.StatusCode,
                ContentType: row.ContentType ?? string.Empty,
                ResponseBody: row.ResponseBody ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Idempotency store read failed for key={Key}", key);
            return null;
        }
    }

    public void Record(IdempotencyRecord record)
    {
        if (record is null || string.IsNullOrEmpty(record.Key)) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            var existing = db.IdempotencyEntries.FirstOrDefault(e => e.Key == record.Key);
            var body = record.ResponseBody ?? string.Empty;
            if (body.Length > IdempotencyEntry.MaxResponseBodyLength)
                body = body.Substring(0, IdempotencyEntry.MaxResponseBodyLength);

            if (existing is null)
            {
                db.IdempotencyEntries.Add(new IdempotencyEntry
                {
                    Key = record.Key,
                    PayloadHash = record.PayloadHash ?? string.Empty,
                    StatusCode = record.StatusCode,
                    ContentType = record.ContentType ?? string.Empty,
                    ResponseBody = body,
                    RecordedAt = record.RecordedAt.UtcDateTime,
                    ExpiresAt = record.RecordedAt.UtcDateTime + _replayWindow,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                });
            }
            else
            {
                existing.PayloadHash = record.PayloadHash ?? string.Empty;
                existing.StatusCode = record.StatusCode;
                existing.ContentType = record.ContentType ?? string.Empty;
                existing.ResponseBody = body;
                existing.RecordedAt = record.RecordedAt.UtcDateTime;
                existing.ExpiresAt = record.RecordedAt.UtcDateTime + _replayWindow;
                existing.RowVersion = Guid.NewGuid().ToByteArray();
            }
            db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            // Concurrent insert on the same key from a sibling
            // replica — the row will be visible to the next read,
            // so this is best-effort and not a fatal error.
            _logger.LogDebug(ex,
                "Idempotency store record race for key={Key}; relying on sibling write.",
                record.Key);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Idempotency store record failed for key={Key}", record.Key);
        }
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.IdempotencyEntries.FirstOrDefault(e => e.Key == key);
            if (row is null) return;
            db.IdempotencyEntries.Remove(row);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Idempotency store remove failed for key={Key}", key);
        }
    }

    /// <summary>
    /// Phase K Wave 9 — Bishop. Background sweeper hook. Drops
    /// every row with <c>ExpiresAt &lt;= cutoff</c> in a single
    /// batch. Returns the count removed so operator dashboards
    /// can chart the steady-state sweep rate.
    /// </summary>
    public int Sweep(DateTime cutoffUtc)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var expired = db.IdempotencyEntries
                .Where(e => e.ExpiresAt <= cutoffUtc)
                .ToList();
            if (expired.Count == 0) return 0;
            db.IdempotencyEntries.RemoveRange(expired);
            db.SaveChanges();
            return expired.Count;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Idempotency sweep failed");
            return 0;
        }
    }
}

/// <summary>
/// Phase K Wave 9 — Bishop. Optional Redis-backed
/// <see cref="IIdempotencyStore"/>. The W9 surface registers this
/// only when <c>Idempotency:StoreImpl = "Redis"</c> AND the
/// <c>Idempotency:RedisConnection</c> connection string is
/// populated. For the W9 bring-up the implementation is a thin
/// wrapper around <see cref="EfIdempotencyStore"/> + an in-process
/// LRU cache — the real <c>StackExchange.Redis</c> client lands in
/// a follow-up wave when the operator stand-up for the Redis cluster
/// is complete (Apone's <c>infra/k8s/overlays/prod/redis-*</c> work
/// is on the Wave 10 cut). Today the toggle exists so the deployment
/// surface and contract tests can pin the symbol without a runtime
/// network dependency.
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly EfIdempotencyStore _fallback;
    private readonly InMemoryIdempotencyStore _localCache;
    private readonly ILogger<RedisIdempotencyStore> _logger;

    /// <summary>Resolved connection string (informational; surfaced
    /// to operators via the health probe).</summary>
    public string ConnectionString { get; }

    public RedisIdempotencyStore(
        EfIdempotencyStore fallback,
        ILogger<RedisIdempotencyStore> logger,
        string connectionString)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localCache = new InMemoryIdempotencyStore();
        ConnectionString = connectionString ?? string.Empty;
        _logger.LogInformation(
            "RedisIdempotencyStore configured (connection prefix: {Prefix}); using EF fallback until the Redis client wire lands.",
            string.IsNullOrEmpty(connectionString) ? "<unset>" : connectionString.Substring(0, Math.Min(connectionString.Length, 12)));
    }

    public IdempotencyRecord? TryGet(string key)
    {
        var cached = _localCache.TryGet(key);
        if (cached is not null) return cached;
        var ef = _fallback.TryGet(key);
        if (ef is not null) _localCache.Record(ef);
        return ef;
    }

    public void Record(IdempotencyRecord record)
    {
        _localCache.Record(record);
        _fallback.Record(record);
    }

    public void Remove(string key)
    {
        _localCache.Remove(key);
        _fallback.Remove(key);
    }
}
