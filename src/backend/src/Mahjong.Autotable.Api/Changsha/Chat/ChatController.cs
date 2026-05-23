using Mahjong.Autotable.Api.Changsha.Chat;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.Mvc;

namespace Mahjong.Autotable.Api.Changsha.Chat;

/// <summary>
/// Phase J Wave 9 — REST surface for table chat. The hub flow
/// (<c>ChangshaHub.SendChat</c>) is canonical during a live SignalR
/// session; these endpoints exist so a rejoining client can lazily
/// back-fill the conversation, and so non-socket clients can post
/// messages too.
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>POST /api/chat/send</c> — submit a chat message.
///         Returns the persisted row.</item>
///   <item><c>GET /api/games/{gameId}/chat?since=&amp;limit=50</c> — backfill
///         the table conversation.</item>
/// </list></para>
/// </summary>
[ApiController]
public sealed class ChatController : ControllerBase
{
    private readonly ChatService _chat;
    private readonly PlayerIdentityService _playerIdentity;

    public ChatController(ChatService chat, PlayerIdentityService playerIdentity)
    {
        _chat = chat;
        _playerIdentity = playerIdentity;
    }

    [HttpPost("api/chat/send")]
    public async Task<IActionResult> Send([FromBody] SendBody body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.GameId) || string.IsNullOrWhiteSpace(body.Body))
            return BadRequest(new { error = "gameId and body are required." });

        // Resolve the sender's player id from the persistent cookie. Mints
        // a fresh anonymous id when the caller has none.
        var playerId = _playerIdentity.ResolveOrMint(HttpContext);
        var channel = string.IsNullOrWhiteSpace(body.Channel) ? "table" : body.Channel!;

        var (outcome, row) = await _chat.SendAsync(body.GameId!, playerId, body.Body!, channel, ct);
        return outcome switch
        {
            ChatSendOutcome.Ok => Ok(new
            {
                id = row!.Id,
                gameId = row.GameId,
                playerId = row.PlayerId,
                channel = row.Channel,
                body = row.Body,
                at = row.At,
            }),
            ChatSendOutcome.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Chat rate limit exceeded." }),
            ChatSendOutcome.Filtered => BadRequest(new { error = "Message blocked by chat filter." }),
            ChatSendOutcome.TooLong => BadRequest(new { error = "Message exceeds 280-character limit." }),
            _ => BadRequest(new { error = "Invalid chat send." }),
        };
    }

    [HttpGet("api/games/{gameId}/chat")]
    public async Task<IActionResult> Backfill(string gameId, [FromQuery] string? since, [FromQuery] int? limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return BadRequest(new { error = "gameId is required." });
        DateTime? sinceTs = null;
        if (!string.IsNullOrWhiteSpace(since) && DateTime.TryParse(since, out var parsed))
        {
            sinceTs = parsed.ToUniversalTime();
        }
        var resolvedLimit = limit.GetValueOrDefault(50);
        var rows = await _chat.BackfillAsync(gameId, sinceTs, resolvedLimit, ct);
        return Ok(new
        {
            gameId,
            messages = rows.Select(m => new
            {
                id = m.Id,
                playerId = m.PlayerId,
                channel = m.Channel,
                body = m.Body,
                at = m.At,
            }).ToArray(),
        });
    }

    public sealed class SendBody
    {
        public string? GameId { get; set; }
        public string? Channel { get; set; }
        public string? Body { get; set; }
    }
}
