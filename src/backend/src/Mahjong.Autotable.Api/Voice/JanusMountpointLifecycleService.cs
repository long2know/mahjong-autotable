using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Voice;

/// <summary>
/// Phase K Wave 10 — Bishop. Lifecycle metadata for a single Janus
/// audio-only mountpoint. The mountpoint is the per-table audio
/// fan-out endpoint configured on the Janus SFU; this record is the
/// process-side bookkeeping the lifecycle service uses to garbage-
/// collect mountpoints once their owning table goes idle.
/// </summary>
/// <param name="TableId">Logical table identifier.</param>
/// <param name="MountpointId">Deterministic 6-digit Janus
/// mountpoint id, computed via
/// <see cref="JanusSpectatorVoiceHub.ComputeMountpointId"/>.</param>
/// <param name="CreatedAtUtc">First time this mountpoint was
/// registered.</param>
/// <param name="LastSeenAtUtc">Most-recent join keeping the mount
/// alive. The lifecycle service tears the mountpoint down once
/// (UtcNow - LastSeen) exceeds the configured idle TTL.</param>
/// <param name="ActiveSpectators">Best-effort spectator count.
/// Decremented on disconnect; on race the mountpoint may briefly
/// show zero before <see cref="LastSeenAtUtc"/> ages out — the
/// idle TTL guards against premature teardown.</param>
public sealed record JanusMountpointEntry(
    string TableId,
    long MountpointId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    int ActiveSpectators);

/// <summary>
/// Phase K Wave 10 — Bishop. In-memory registry of every active
/// Janus mountpoint owned by this process. The registry tracks
/// the (tableId → mountpoint) mapping plus a heartbeat
/// (<see cref="JanusMountpointEntry.LastSeenAtUtc"/>) the
/// <see cref="JanusMountpointLifecycleService"/> uses to decide
/// when to garbage-collect idle mountpoints.
///
/// <para>The registry is process-local: a multi-replica deployment
/// has one registry per pod. That is fine because mountpoints are
/// pinned to a single Janus instance by deterministic
/// <see cref="JanusSpectatorVoiceHub.ComputeMountpointId"/>, so a
/// concurrent registration on a second pod targets the same Janus
/// mountpoint — the lifecycle service tolerates this by treating
/// duplicate registrations as idempotent refresh-touches.</para>
/// </summary>
public sealed class JanusMountpointRegistry
{
    private readonly ConcurrentDictionary<string, JanusMountpointEntry> _entries =
        new(StringComparer.Ordinal);

    private readonly Func<DateTimeOffset> _now;

    public JanusMountpointRegistry(Func<DateTimeOffset>? now = null)
    {
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Current registry snapshot — useful for diagnostics
    /// and for the lifecycle service's tick.</summary>
    public IReadOnlyCollection<JanusMountpointEntry> Entries =>
        _entries.Values.ToList();

    /// <summary>Number of currently registered mountpoints.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Register a join or refresh-touch an existing mountpoint.
    /// Idempotent — calling twice for the same table id refreshes
    /// <see cref="JanusMountpointEntry.LastSeenAtUtc"/> and bumps
    /// the active-spectator count.
    /// </summary>
    public JanusMountpointEntry RegisterJoin(string tableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);
        var now = _now();
        var id = JanusSpectatorVoiceHub.ComputeMountpointId(tableId);
        return _entries.AddOrUpdate(
            tableId,
            _ => new JanusMountpointEntry(
                TableId: tableId,
                MountpointId: id,
                CreatedAtUtc: now,
                LastSeenAtUtc: now,
                ActiveSpectators: 1),
            (_, existing) => existing with
            {
                LastSeenAtUtc = now,
                ActiveSpectators = existing.ActiveSpectators + 1,
            });
    }

    /// <summary>
    /// Record a spectator leaving the mountpoint. Decrements the
    /// active-spectator counter but does NOT remove the entry —
    /// the lifecycle service decides when to GC based on the
    /// idle-TTL window.
    /// </summary>
    public bool RecordLeave(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId)) return false;
        var now = _now();
        var modified = false;
        _entries.AddOrUpdate(
            tableId,
            _ => new JanusMountpointEntry(
                TableId: tableId,
                MountpointId: JanusSpectatorVoiceHub.ComputeMountpointId(tableId),
                CreatedAtUtc: now,
                LastSeenAtUtc: now,
                ActiveSpectators: 0),
            (_, existing) =>
            {
                modified = true;
                return existing with
                {
                    LastSeenAtUtc = now,
                    ActiveSpectators = Math.Max(0, existing.ActiveSpectators - 1),
                };
            });
        return modified;
    }

    /// <summary>Look up a mountpoint by table id.</summary>
    public JanusMountpointEntry? TryGet(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId)) return null;
        _entries.TryGetValue(tableId, out var v);
        return v;
    }

    /// <summary>Force-evict a mountpoint regardless of TTL.</summary>
    public bool Evict(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId)) return false;
        return _entries.TryRemove(tableId, out _);
    }

    /// <summary>
    /// Sweep entries idle longer than the supplied window. Returns
    /// the list of evicted entries so the lifecycle service can
    /// emit one log line per eviction.
    /// </summary>
    public IReadOnlyList<JanusMountpointEntry> Sweep(TimeSpan idleTtl)
    {
        var now = _now();
        var evicted = new List<JanusMountpointEntry>();
        foreach (var pair in _entries)
        {
            var age = now - pair.Value.LastSeenAtUtc;
            if (age < idleTtl) continue;
            if (pair.Value.ActiveSpectators > 0) continue;
            if (_entries.TryRemove(pair.Key, out var removed))
            {
                evicted.Add(removed);
            }
        }
        return evicted;
    }
}

/// <summary>
/// Phase K Wave 10 — Bishop. Background lifecycle service for the
/// <see cref="JanusMountpointRegistry"/>. Polls the registry on a
/// slow cadence (default 60s) and tears down any mountpoint idle
/// past the configured TTL. Registered as a hosted service via
/// <c>Program.cs</c>.
/// </summary>
public sealed class JanusMountpointLifecycleService : BackgroundService
{
    /// <summary>Default sweep cadence — once per minute.</summary>
    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromSeconds(60);

    /// <summary>Default idle TTL — five minutes. A mountpoint with
    /// zero active spectators that hasn't been touched in this
    /// window is GC'd.</summary>
    public static readonly TimeSpan DefaultIdleTtl = TimeSpan.FromMinutes(5);

    private readonly JanusMountpointRegistry _registry;
    private readonly ILogger<JanusMountpointLifecycleService> _logger;
    private readonly TimeSpan _sweepInterval;
    private readonly TimeSpan _idleTtl;

    public JanusMountpointLifecycleService(
        JanusMountpointRegistry registry,
        ILogger<JanusMountpointLifecycleService> logger,
        TimeSpan? sweepInterval = null,
        TimeSpan? idleTtl = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sweepInterval = sweepInterval ?? DefaultSweepInterval;
        _idleTtl = idleTtl ?? DefaultIdleTtl;
    }

    /// <summary>Sweep cadence — exposed for the contract suite.</summary>
    public TimeSpan SweepInterval => _sweepInterval;

    /// <summary>Idle TTL — exposed for the contract suite.</summary>
    public TimeSpan IdleTtl => _idleTtl;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "JanusMountpointLifecycleService started (sweep={Sweep}s, idleTtl={Ttl}s).",
            _sweepInterval.TotalSeconds, _idleTtl.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_sweepInterval, stoppingToken);
                RunOnce();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "JanusMountpointLifecycleService sweep failed (non-fatal); next tick in {Sweep}s.",
                    _sweepInterval.TotalSeconds);
            }
        }

        _logger.LogInformation("JanusMountpointLifecycleService stopped.");
    }

    /// <summary>
    /// Phase K Wave 10 — Bishop. Single-sweep entry-point exposed
    /// for the contract suite so tests can drive evictions
    /// deterministically without waiting for the timer.
    /// </summary>
    internal int RunOnce()
    {
        var evicted = _registry.Sweep(_idleTtl);
        foreach (var e in evicted)
        {
            _logger.LogInformation(
                "JanusMountpoint evicted: tableId={TableId} mountpointId={MountpointId} age={AgeS}s",
                e.TableId, e.MountpointId,
                (DateTimeOffset.UtcNow - e.LastSeenAtUtc).TotalSeconds);
        }
        return evicted.Count;
    }
}
