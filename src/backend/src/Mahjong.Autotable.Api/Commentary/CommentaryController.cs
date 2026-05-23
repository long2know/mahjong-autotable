using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Commentary;

/// <summary>
/// Phase K Wave 6 — Bishop. REST surface for the LLM-driven play-by-
/// play commentary feature. Wave 6 ships the contract + the stub
/// generator; Phase L re-binds <see cref="ICommentaryGenerator"/>
/// to a real implementation without changing the URL shape.
///
/// <list type="bullet">
///   <item><c>POST /api/games/{gameId}/commentary</c> — triggers
///         generation. Admin-only — anonymous callers get 401, non-
///         admin sessions get 403. Audited via
///         <see cref="ReconnectAuditEntry.KindCommentaryReplayRequested"/>.</item>
///   <item><c>GET  /api/games/{gameId}/commentary</c> — returns the
///         previously-generated commentary. Anonymous-allowed (the
///         lobby reads it for spectator playback).</item>
///   <item><c>POST /api/games/{gameId}/commentary/replay</c> +
///         <c>GET /api/games/{gameId}/commentary/replay</c> — aliases
///         matching the user-spec URL shape; identical semantics to
///         the base route.</item>
/// </list>
///
/// <para>The route prefix uses <c>games</c> to match the existing
/// <c>/api/games/{gameId}/audit</c> + <c>/api/games/{gameId}/chat</c>
/// surfaces — operators reach for commentary via the same
/// game-scoped path.</para>
/// </summary>
[ApiController]
[Route("api/games/{gameId:guid}/commentary")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class CommentaryController : ControllerBase
{
    private readonly ICommentaryGenerator _generator;
    private readonly AuthCookieService _cookies;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommentaryController> _logger;

    public CommentaryController(
        ICommentaryGenerator generator,
        AuthCookieService cookies,
        IServiceScopeFactory scopeFactory,
        ILogger<CommentaryController> logger)
    {
        _generator = generator;
        _cookies = cookies;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpPost]
    [HttpPost("replay")]
    public async Task<IActionResult> Trigger([FromRoute] Guid gameId, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null)
            return Unauthorized(new { error = "Authentication required to trigger commentary." });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Admin role required to trigger commentary generation.",
            });

        try
        {
            var replay = await _generator.GenerateAsync(gameId, ct);
            await WriteAuditAsync(session.PlayerId, gameId, replay.Generator, ct);
            return Ok(BuildEnvelope(replay));
        }
        catch (UsageCapExceededException ex)
        {
            // Phase K Wave 9 — Bishop. Hard cap surfaces as HTTP 429
            // when Commentary:ThrowOnMonthlyCap is true. The envelope
            // carries the canonical "monthly-token-cap" reason so
            // clients can branch on the error name without parsing
            // the human-readable message.
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "monthly-token-cap",
                detail = ex.Message,
                gameId,
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromRoute] Guid gameId, CancellationToken ct)
    {
        var replay = await _generator.GetAsync(gameId, ct);
        return Ok(BuildEnvelope(replay));
    }

    /// <summary>
    /// Phase K Wave 7 — Bishop. Records-flavoured replay endpoint.
    /// Returns the per-turn <see cref="CommentaryRecord"/> array per
    /// the finalised Phase-L JSON contract (one record per speaker
    /// utterance, with phase/speaker/intensity/tileReferences fields).
    /// Anonymous-allowed so the lobby spectator UI can pull commentary
    /// without an authenticated session.
    /// </summary>
    [HttpGet("replay")]
    public async Task<IActionResult> Replay([FromRoute] Guid gameId, CancellationToken ct)
    {
        var records = await _generator.GetRecordsAsync(gameId, ct);
        // Project to a wire-explicit anonymous object so the field
        // casing (camelCase wire / PascalCase record) is fixed at the
        // controller boundary instead of leaking the System.Text.Json
        // default for record properties.
        var wire = records.Select(r => new
        {
            gameId = r.GameId,
            turnNumber = r.TurnNumber,
            phase = r.Phase,
            speaker = r.Speaker,
            text = r.Text,
            emotionIntensity = r.EmotionIntensity,
            tileReferences = r.TileReferences ?? Array.Empty<string>(),
            generatedAt = r.GeneratedAt,
        }).ToArray();
        return Ok(wire);
    }

    private static object BuildEnvelope(CommentaryReplay replay) => new
    {
        gameId = replay.GameId,
        generator = replay.Generator,
        status = replay.Status,
        items = replay.Items.Select(i => new
        {
            sequence = i.Sequence,
            text = i.Text,
            roundOrdinal = i.RoundOrdinal,
            tone = i.Tone,
        }).ToArray(),
    };

    private async Task WriteAuditAsync(string playerId, Guid gameId, string generatorId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                At = DateTime.UtcNow,
                Kind = ReconnectAuditEntry.KindCommentaryReplayRequested,
                Detail = $"{gameId:N}:{generatorId}",
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Commentary audit write failed for gameId={GameId}", gameId);
        }
    }
}
