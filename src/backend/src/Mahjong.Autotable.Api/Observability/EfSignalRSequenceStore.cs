using System.Collections.Concurrent;
using System.Text.Json;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Observability;

/// <summary>
/// Phase K Wave 12 — Bishop. Durable per-(hub, connection)
/// sequence row. Backs <see cref="EfSignalRSequenceStore"/>
/// so long-lived sessions (&gt; 30 min, exceeds the in-memory
/// 256-entry retention window on
/// <see cref="SignalRBackpressureBroadcaster{THub}"/>) can
/// replay from any earlier seq, not just the in-memory tail.
///
/// <para>Wave 12 ships the seam; production runtime hooks
/// will populate this table from
/// <see cref="SignalRBackpressureBroadcaster{THub}.PublishAsync"/>
/// once the SignalR:SequenceStoreImpl toggle is set to
/// <c>"Ef"</c>. See <c>docs/realtime-resilience.md §6</c>.</para>
/// </summary>
public sealed class SignalRSequenceEntry
{
    /// <summary>Surrogate id. The natural key
    /// <c>(HubName, ConnectionId, Sequence)</c> is the unique
    /// constraint enforced by EF.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hub type name — typically
    /// <c>"ChangshaHub"</c>, <c>"TournamentMatchHub"</c>, etc.
    /// Length-capped at 64 to keep the index compact.</summary>
    public string HubName { get; set; } = string.Empty;

    /// <summary>SignalR connection id. Length-capped at 128
    /// (SignalR's own id format is ~20 chars; the cap leaves
    /// headroom for future identity-pinned ids).</summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>Group name the message was published into.
    /// Indexed only by way of (HubName, ConnectionId) — the
    /// dominant lookup path is per-connection, not
    /// per-group.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>SignalR method name — typically
    /// <c>"PublishUpdate"</c>, <c>"FullState"</c>, etc.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Monotonic per-(hub, connection) sequence
    /// stamped at publish time.</summary>
    public long Sequence { get; set; }

    /// <summary>UTC timestamp when the message was published.
    /// Surfaced to consumers so they can compute message age
    /// at replay time.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC retention expiry — the sweeper deletes
    /// rows older than this. Derived as <c>CreatedAt +
    /// RetentionMinutes</c> at insert time.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Serialised payload. JSON-encoded so the read
    /// path can rehydrate the original message without a
    /// round-trip to the runtime.</summary>
    public string PayloadJson { get; set; } = "{}";
}

/// <summary>
/// Phase K Wave 12 — Bishop. Persistence seam for the durable
/// SignalR replay-from-ack surface. Narrow contract: append
/// the next sequence, read entries newer than the last-acked
/// sequence, sweep expired rows.
///
/// <para>Toggle: <c>SignalR:SequenceStoreImpl</c>
/// (<c>"InMemory"</c> default for tests / <c>"Ef"</c> for
/// production). See <c>docs/realtime-resilience.md §6</c>.</para>
/// </summary>
public interface ISignalRSequenceStore
{
    /// <summary>Append a single sequence entry. The store
    /// fills <see cref="SignalRSequenceEntry.ExpiresAt"/>
    /// from the configured retention window when the caller
    /// leaves it at default.</summary>
    Task<SignalRSequenceEntry> AppendAsync(SignalRSequenceEntry entry, CancellationToken ct = default);

    /// <summary>Replay every entry for the supplied
    /// (hub, connection) with
    /// <see cref="SignalRSequenceEntry.Sequence"/> strictly
    /// greater than <paramref name="lastAckedSequence"/>.
    /// Results are ordered ascending by sequence.</summary>
    Task<IReadOnlyList<SignalRSequenceEntry>> ReadFromAckAsync(
        string hubName,
        string connectionId,
        long lastAckedSequence,
        int limit,
        CancellationToken ct = default);

    /// <summary>Sweep expired rows. Returns the count
    /// evicted.</summary>
    Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>Total row count — surfaced for tests.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>
/// Phase K Wave 12 — Bishop. In-memory
/// <see cref="ISignalRSequenceStore"/> used by tests +
/// single-replica dev. Keyed by (HubName, ConnectionId);
/// per-key entries live in a ConcurrentBag so multiple
/// publish-paths can append in parallel without contention.
/// </summary>
public sealed class InMemorySignalRSequenceStore : ISignalRSequenceStore
{
    private readonly ConcurrentDictionary<(string, string), ConcurrentBag<SignalRSequenceEntry>> _entries =
        new();
    private readonly SignalRSequenceStoreOptions _options;

    public InMemorySignalRSequenceStore(SignalRSequenceStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<SignalRSequenceEntry> AppendAsync(SignalRSequenceEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.CreatedAt == default) entry.CreatedAt = DateTime.UtcNow;
        if (entry.ExpiresAt == default)
        {
            var retention = _options.RetentionMinutes > 0 ? _options.RetentionMinutes : SignalRSequenceStoreOptions.DefaultRetentionMinutes;
            entry.ExpiresAt = entry.CreatedAt.AddMinutes(retention);
        }
        var bag = _entries.GetOrAdd((entry.HubName, entry.ConnectionId),
            _ => new ConcurrentBag<SignalRSequenceEntry>());
        bag.Add(entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<SignalRSequenceEntry>> ReadFromAckAsync(
        string hubName,
        string connectionId,
        long lastAckedSequence,
        int limit,
        CancellationToken ct = default)
    {
        if (!_entries.TryGetValue((hubName, connectionId), out var bag))
            return Task.FromResult<IReadOnlyList<SignalRSequenceEntry>>(Array.Empty<SignalRSequenceEntry>());
        IReadOnlyList<SignalRSequenceEntry> rows = bag
            .Where(e => e.Sequence > lastAckedSequence)
            .OrderBy(e => e.Sequence)
            .Take(Math.Max(0, limit))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var pair in _entries)
        {
            var survivors = pair.Value
                .Where(e => e.ExpiresAt >= utcNow)
                .ToList();
            removed += pair.Value.Count - survivors.Count;
            if (survivors.Count == 0)
            {
                _entries.TryRemove(pair.Key, out _);
            }
            else
            {
                _entries[pair.Key] = new ConcurrentBag<SignalRSequenceEntry>(survivors);
            }
        }
        return Task.FromResult(removed);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Task.FromResult(_entries.Values.Sum(b => b.Count));
}

/// <summary>
/// Phase K Wave 12 — Bishop. EF-backed durable
/// <see cref="ISignalRSequenceStore"/>. Persists rows to
/// <see cref="AppDbContext.SignalRSequenceEntries"/>; reads
/// the dominant per-(hub, connection) path off the unique
/// index for O(log n) lookups.
/// </summary>
public sealed class EfSignalRSequenceStore : ISignalRSequenceStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SignalRSequenceStoreOptions _options;
    private readonly ILogger<EfSignalRSequenceStore> _logger;

    public EfSignalRSequenceStore(
        IServiceScopeFactory scopeFactory,
        SignalRSequenceStoreOptions options,
        ILogger<EfSignalRSequenceStore> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SignalRSequenceEntry> AppendAsync(SignalRSequenceEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.CreatedAt == default) entry.CreatedAt = DateTime.UtcNow;
        if (entry.ExpiresAt == default)
        {
            var retention = _options.RetentionMinutes > 0 ? _options.RetentionMinutes : SignalRSequenceStoreOptions.DefaultRetentionMinutes;
            entry.ExpiresAt = entry.CreatedAt.AddMinutes(retention);
        }
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SignalRSequenceEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<IReadOnlyList<SignalRSequenceEntry>> ReadFromAckAsync(
        string hubName,
        string connectionId,
        long lastAckedSequence,
        int limit,
        CancellationToken ct = default)
    {
        if (limit <= 0) return Array.Empty<SignalRSequenceEntry>();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SignalRSequenceEntries
            .AsNoTracking()
            .Where(e => e.HubName == hubName
                     && e.ConnectionId == connectionId
                     && e.Sequence > lastAckedSequence)
            .OrderBy(e => e.Sequence)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> SweepExpiredAsync(DateTime utcNow, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            return await db.SignalRSequenceEntries
                .Where(e => e.ExpiresAt < utcNow)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalRSequence bulk-delete failed; falling back to per-row delete.");
            var rows = await db.SignalRSequenceEntries
                .Where(e => e.ExpiresAt < utcNow)
                .ToListAsync(ct);
            db.SignalRSequenceEntries.RemoveRange(rows);
            await db.SaveChangesAsync(ct);
            return rows.Count;
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SignalRSequenceEntries.CountAsync(ct);
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. Configuration for the durable
/// SignalR sequence-store. Bound from the <c>SignalR</c>
/// configuration section.
/// </summary>
public sealed class SignalRSequenceStoreOptions
{
    /// <summary>Default retention window (minutes). Sequence
    /// rows older than this are deleted by the sweeper.</summary>
    public const int DefaultRetentionMinutes = 60;

    /// <summary>Implementation selector — case-insensitive.
    /// <c>"InMemory"</c> uses
    /// <see cref="InMemorySignalRSequenceStore"/>;
    /// <c>"Ef"</c> uses
    /// <see cref="EfSignalRSequenceStore"/>.</summary>
    public string SequenceStoreImpl { get; set; } = "InMemory";

    /// <summary>Retention window in minutes. 0 = use the
    /// default (<see cref="DefaultRetentionMinutes"/>).</summary>
    public int RetentionMinutes { get; set; } = DefaultRetentionMinutes;

    /// <summary>Background sweep cadence in minutes. Default
    /// 5 — frequent enough that expired rows don't accumulate
    /// over a long session, infrequent enough that the
    /// background load is negligible.</summary>
    public int SweepIntervalMinutes { get; set; } = 5;

    /// <summary>Read-page cap. Replay-from-ack queries clamp
    /// to this value so a stale ack pointer doesn't trigger
    /// an unbounded fetch. Default 1024 — enough for an
    /// hour-long burst at the canonical
    /// <see cref="SignalRBackpressureBroadcaster{THub}.DefaultMaxMessagesPerSecond"/>.</summary>
    public int MaxReplayPageSize { get; set; } = 1024;
}

/// <summary>
/// Phase K Wave 12 — Bishop. Nightly background sweep that
/// deletes <see cref="SignalRSequenceEntry"/> rows past their
/// configured retention window. Registered as a hosted service
/// only when <c>SignalR:SequenceStoreImpl="Ef"</c>.
/// </summary>
public sealed class SignalRSequenceSweepService : BackgroundService
{
    private readonly ISignalRSequenceStore _store;
    private readonly SignalRSequenceStoreOptions _options;
    private readonly ILogger<SignalRSequenceSweepService> _logger;

    public SignalRSequenceSweepService(
        ISignalRSequenceStore store,
        SignalRSequenceStoreOptions options,
        ILogger<SignalRSequenceSweepService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SweepIntervalMinutes));
        _logger.LogInformation(
            "SignalRSequenceSweepService started (interval={Minutes}m, retention={RetentionMinutes}m).",
            interval.TotalMinutes,
            _options.RetentionMinutes);
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
                    "SignalRSequenceSweep failed (non-fatal); next tick in {Minutes}m.",
                    interval.TotalMinutes);
            }
        }
    }

    /// <summary>Single-sweep entry-point exposed so tests can
    /// drive deletions deterministically.</summary>
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var removed = await _store.SweepExpiredAsync(DateTime.UtcNow, ct);
        if (removed > 0)
        {
            _logger.LogInformation(
                "SignalRSequenceSweep removed {Count} expired entries.", removed);
        }
        return removed;
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. Helper that serialises an
/// arbitrary payload object into the canonical JSON wire
/// shape used by <see cref="SignalRSequenceEntry.PayloadJson"/>.
/// Keeps the call sites in
/// <see cref="SignalRBackpressureBroadcaster{THub}.PublishAsync"/>
/// + tests in lockstep — one serialiser, one wire format.
/// </summary>
public static class SignalRSequencePayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object? payload) =>
        payload is null ? "{}" : JsonSerializer.Serialize(payload, Options);

    public static JsonElement Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
