using System.Collections.Concurrent;

namespace Mahjong.Autotable.Api.Voice;

// Phase K Wave 3 — Bishop (backend). Lightweight per-connection
// signalling-relay counter used by the VoiceHub. The hub still gates
// per-second chatter via VoiceRateLimiter; this service exposes a
// rolling 60-second window count per connection so that the /metrics
// surface (and future ops dashboards) can observe relay pressure
// without flipping on the rate-limiter's internal token-bucket
// instrumentation.
//
// Phase K Wave 4 — Bishop. Adds rate-limit-rejection +
// join-unauthorized counters to feed the VoiceHubMetrics-named
// Prometheus surfaces (voice_rate_limit_rejection_total,
// voice_join_unauthorized_total). Both are simple monotonic counters
// — the per-connection rolling window only applies to the relay
// counter where per-connection observability is actually useful.
//
// Implementation notes:
// * Records are kept in-memory only — connection-scoped state is
//   meaningless across process restarts (SignalR re-issues connection
//   ids anyway).
// * `GetRelayCount(connectionId)` and `GetTotalRelayCount()` walk the
//   bucket list lazily, dropping expired ticks (>60s ago) on read.
//   Callers that fan-out on every relay still pay O(N) per hub method
//   so we keep the hot path branchless and defer expiry to readers.
public sealed class VoiceHubMetricsService
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, ConnectionMetrics> _byConnection = new();
    private long _rateLimitRejections;
    private long _joinUnauthorized;

    public void RecordRelay(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;
        var metrics = _byConnection.GetOrAdd(connectionId, _ => new ConnectionMetrics());
        metrics.Record(DateTime.UtcNow);
    }

    /// <summary>Phase K Wave 4 — increments the
    /// <see cref="VoiceHubMetrics.MetricRateLimitRejection"/> counter
    /// whenever the rate limiter denies a relay.</summary>
    public void RecordRateLimitRejection() => System.Threading.Interlocked.Increment(ref _rateLimitRejections);

    /// <summary>Phase K Wave 4 — increments the
    /// <see cref="VoiceHubMetrics.MetricJoinUnauthorized"/> counter
    /// whenever JoinVoice is rejected by the per-table auth gate.</summary>
    public void RecordJoinUnauthorized() => System.Threading.Interlocked.Increment(ref _joinUnauthorized);

    public long GetRateLimitRejectionCount() => System.Threading.Interlocked.Read(ref _rateLimitRejections);

    public long GetJoinUnauthorizedCount() => System.Threading.Interlocked.Read(ref _joinUnauthorized);

    public int GetRelayCount(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return 0;
        if (!_byConnection.TryGetValue(connectionId, out var metrics)) return 0;
        return metrics.CountWithin(DateTime.UtcNow - Window);
    }

    public int GetTotalRelayCount()
    {
        var cutoff = DateTime.UtcNow - Window;
        var total = 0;
        foreach (var metrics in _byConnection.Values)
        {
            total += metrics.CountWithin(cutoff);
        }
        return total;
    }

    public void Forget(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;
        _byConnection.TryRemove(connectionId, out _);
    }

    private sealed class ConnectionMetrics
    {
        private readonly object _lock = new();
        // Ring-style queue of relay timestamps. Pruned lazily on read.
        private readonly Queue<DateTime> _ticks = new();

        public void Record(DateTime atUtc)
        {
            lock (_lock)
            {
                _ticks.Enqueue(atUtc);
                Prune(atUtc - Window);
            }
        }

        public int CountWithin(DateTime cutoffUtc)
        {
            lock (_lock)
            {
                Prune(cutoffUtc);
                return _ticks.Count;
            }
        }

        private void Prune(DateTime cutoffUtc)
        {
            while (_ticks.Count > 0 && _ticks.Peek() < cutoffUtc)
            {
                _ticks.Dequeue();
            }
        }
    }
}
