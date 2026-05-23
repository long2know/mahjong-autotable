using System.Collections.Concurrent;

namespace Mahjong.Autotable.Api.Commentary;

/// <summary>
/// Phase K Wave 8 — Bishop. Tracks LLM token consumption per game +
/// the rolling monthly total so the
/// <see cref="OpenAiCommentaryGenerator"/> can cap spend before
/// firing the next request.
///
/// <para>The implementation is in-process — a Phase L extension
/// will swap to a Redis-backed counter for multi-replica
/// deployments. For W8 the single-replica deployment is the
/// canonical shape.</para>
/// </summary>
public interface ICommentaryUsageMeter
{
    /// <summary>Records the input + output token counts from a
    /// completed LLM call. Idempotent retries against the same
    /// game id within the same minute are deduplicated by the
    /// caller.</summary>
    void RecordUsage(Guid gameId, int inputTokens, int outputTokens);

    /// <summary>Returns the total token consumption (input +
    /// output) for the supplied game across the current process
    /// lifetime.</summary>
    long PerGameTokens(Guid gameId);

    /// <summary>Returns the total token consumption across all
    /// games for the current calendar month.</summary>
    long MonthlyTokens(DateTime utcNow);

    /// <summary>True when the supplied monthly cap has been hit.
    /// 0 = unlimited (always returns false).</summary>
    bool ExceedsMonthlyCap(long cap, DateTime utcNow);
}

/// <summary>
/// Phase K Wave 8 — Bishop. In-memory <see cref="ICommentaryUsageMeter"/>
/// implementation. Singleton-shaped so the counts persist across
/// requests within the same host lifetime.
/// </summary>
public sealed class InMemoryCommentaryUsageMeter : ICommentaryUsageMeter
{
    private readonly ConcurrentDictionary<Guid, long> _perGame = new();
    private readonly ConcurrentDictionary<string, long> _perMonth = new();

    public void RecordUsage(Guid gameId, int inputTokens, int outputTokens)
    {
        var total = (long)Math.Max(0, inputTokens) + Math.Max(0, outputTokens);
        if (total == 0) return;
        _perGame.AddOrUpdate(gameId, total, (_, prev) => prev + total);
        var monthKey = MonthKey(DateTime.UtcNow);
        _perMonth.AddOrUpdate(monthKey, total, (_, prev) => prev + total);
    }

    public long PerGameTokens(Guid gameId) =>
        _perGame.TryGetValue(gameId, out var v) ? v : 0;

    public long MonthlyTokens(DateTime utcNow) =>
        _perMonth.TryGetValue(MonthKey(utcNow), out var v) ? v : 0;

    public bool ExceedsMonthlyCap(long cap, DateTime utcNow)
    {
        if (cap <= 0) return false;
        return MonthlyTokens(utcNow) >= cap;
    }

    internal static string MonthKey(DateTime utc) => $"{utc.Year:D4}-{utc.Month:D2}";
}
