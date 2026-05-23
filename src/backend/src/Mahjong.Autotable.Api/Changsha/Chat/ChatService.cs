using System.Collections.Concurrent;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Changsha.Chat;

/// <summary>
/// Phase J Wave 9 — server-side table chat. Validates inbound messages,
/// rate-limits a sender's burst, persists each accepted message, and
/// returns the persisted row so the caller (hub or REST controller) can
/// broadcast it.
///
/// <para><b>Rate limit.</b> A sliding 30-second window with a 6-message
/// cap per <c>playerId</c>. Implemented in-memory (single-process
/// authoritative server is the autotable deployment model). The 7th
/// send within the window is rejected with
/// <see cref="ChatSendOutcome.RateLimited"/>.</para>
///
/// <para><b>Profanity.</b> Delegated to <see cref="ChatContentFilter"/>,
/// which substitutes banned tokens with asterisk runs of the same
/// length. The message is still persisted (so chat history stays
/// continuous), but the persisted body never contains the original
/// profanity. Operators extend the catalog at runtime via
/// <see cref="AddProfanity"/>; the canonical seed is intentionally
/// minimal so the wave-9 surface ships a contract without committing
/// to a real moderation policy.</para>
/// </summary>
public sealed class ChatService
{
    public const int RateLimitWindowSeconds = 30;
    public const int RateLimitMaxMessages = 6;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChatContentFilter _filter;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _windows = new();

    public ChatService(IServiceScopeFactory scopeFactory, ChatContentFilter filter)
    {
        _scopeFactory = scopeFactory;
        _filter = filter;
    }

    /// <summary>
    /// Validate + persist a chat message. Returns the persisted row +
    /// outcome. Outcomes other than <see cref="ChatSendOutcome.Ok"/>
    /// return a null message.
    /// </summary>
    public async Task<(ChatSendOutcome outcome, ChatMessage? message)> SendAsync(string gameId, string playerId, string body, string channel, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(playerId))
            return (ChatSendOutcome.Invalid, null);
        if (string.IsNullOrWhiteSpace(body))
            return (ChatSendOutcome.Invalid, null);
        var trimmed = body.Trim();
        if (trimmed.Length > ChatMessage.MaxBodyLength)
            return (ChatSendOutcome.TooLong, null);
        var resolvedChannel = ResolveChannel(channel);

        // Sanitize first — banned tokens are masked rather than rejected,
        // so the conversation continues but the persisted body / audit
        // log never contains the original profanity.
        var sanitized = _filter.Sanitize(trimmed);

        if (!RecordSendInWindow(playerId))
            return (ChatSendOutcome.RateLimited, null);

        var row = new ChatMessage
        {
            GameId = gameId,
            PlayerId = playerId,
            Body = sanitized,
            Channel = resolvedChannel,
            At = DateTime.UtcNow,
        };
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChatMessages.Add(row);
        await db.SaveChangesAsync(ct);
        return (ChatSendOutcome.Ok, row);
    }

    /// <summary>
    /// Reads the last <paramref name="limit"/> messages for
    /// <paramref name="gameId"/>, oldest first, optionally filtered to
    /// rows newer than <paramref name="since"/>.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> BackfillAsync(string gameId, DateTime? since, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return Array.Empty<ChatMessage>();
        limit = Math.Clamp(limit, 1, 200);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.ChatMessages.AsNoTracking().Where(m => m.GameId == gameId);
        if (since.HasValue)
            query = query.Where(m => m.At > since.Value);
        // Take most recent N, but return chronological — matches the
        // ascending order a client expects when appending to a buffer.
        var rows = await query.OrderByDescending(m => m.At).Take(limit).ToListAsync(ct);
        rows.Reverse();
        return rows;
    }

    public void AddProfanity(string word) => _filter.Add(word);

    private bool RecordSendInWindow(string playerId)
    {
        var window = _windows.GetOrAdd(playerId, _ => new Queue<DateTime>());
        lock (window)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddSeconds(-RateLimitWindowSeconds);
            while (window.Count > 0 && window.Peek() < cutoff)
            {
                window.Dequeue();
            }
            if (window.Count >= RateLimitMaxMessages)
                return false;
            window.Enqueue(now);
            return true;
        }
    }

    private static string ResolveChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return "table";
        var c = channel.Trim();
        if (c.Equals("table", StringComparison.OrdinalIgnoreCase)) return "table";
        if (c.Equals("spectator", StringComparison.OrdinalIgnoreCase)) return "spectator";
        if (c.StartsWith("private:", StringComparison.OrdinalIgnoreCase)) return c;
        return "table";
    }
}

public enum ChatSendOutcome
{
    Ok,
    Invalid,
    TooLong,
    Filtered,
    RateLimited,
}
