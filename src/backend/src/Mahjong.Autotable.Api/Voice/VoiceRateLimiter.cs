using System.Collections.Concurrent;

namespace Mahjong.Autotable.Api.Voice;

// Phase K Wave 2 — Bishop (backend). Per-connection token bucket used by
// the VoiceHub to clamp signalling chatter. The contract test probes for
// an int field named *Rate* on a Voice-namespaced type so we expose the
// limit as a public const on this class.
public sealed class VoiceRateLimiter
{
    public const int DefaultRatePerSecond = 30;

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly int _capacity;
    private readonly TimeSpan _refill;

    public VoiceRateLimiter(int ratePerSecond)
    {
        _capacity = ratePerSecond > 0 ? ratePerSecond : DefaultRatePerSecond;
        _refill = TimeSpan.FromSeconds(1);
    }

    public bool TryConsume(string connectionId)
    {
        var bucket = _buckets.GetOrAdd(connectionId, _ => new Bucket(_capacity, DateTime.UtcNow));
        lock (bucket)
        {
            var now = DateTime.UtcNow;
            if (now - bucket.LastRefill >= _refill)
            {
                bucket.Tokens = _capacity;
                bucket.LastRefill = now;
            }
            if (bucket.Tokens <= 0) return false;
            bucket.Tokens--;
            return true;
        }
    }

    public void Forget(string connectionId) => _buckets.TryRemove(connectionId, out _);

    private sealed class Bucket
    {
        public int Tokens;
        public DateTime LastRefill;
        public Bucket(int tokens, DateTime at) { Tokens = tokens; LastRefill = at; }
    }
}
