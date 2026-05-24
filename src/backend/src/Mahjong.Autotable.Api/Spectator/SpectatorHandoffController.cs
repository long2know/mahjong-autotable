using System.Security.Claims;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Spectator;

/// <summary>
/// Phase K Wave 12 — Bishop. Spectator handoff surface — mints
/// a short-lived JWT scoped to a single game so non-cookie
/// clients (native mobile, embedded Janus, headless test
/// harnesses) can attach to spectator endpoints without round-
/// tripping through the cookie-based auth flow.
///
/// <list type="bullet">
///   <item><c>POST /api/spectator/handoff</c> — body
///         <c>{ gameId }</c>. Resolves the caller's session
///         (cookie), confirms they're seated or already
///         spectating, and mints a JWT with
///         <c>scope = "spectator:{gameId}"</c> and a 5-minute
///         TTL. Returns <c>{ token, expiresAt, scope }</c>.</item>
///   <item>The livestream endpoint
///         (<c>/api/replay/{id}/livestream.m3u8</c>) accepts
///         the token via <c>?token=…</c> as an alternative to
///         the session cookie — the
///         <see cref="SpectatorHandoffTokenValidator"/>
///         resolves both the signature + the scope claim and
///         returns the canonical envelope.</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/spectator")]
public sealed class SpectatorHandoffController : ControllerBase
{
    /// <summary>Default spectator handoff TTL. 5 minutes per
    /// the W12 contract; short enough that a leaked token has
    /// limited blast radius, long enough that a slow mobile
    /// network can still authenticate before expiry.</summary>
    public static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Scope prefix stamped on minted tokens. The
    /// validator pins this prefix so a token minted for game A
    /// can't be replayed against game B.</summary>
    public const string ScopePrefix = "spectator:";

    private readonly AuthCookieService _cookies;
    private readonly JwtIssuingService _issuer;
    private readonly ILogger<SpectatorHandoffController> _logger;
    private readonly ISpectatorHandoffAuditStore? _audit;

    public SpectatorHandoffController(
        AuthCookieService cookies,
        JwtIssuingService issuer,
        ILogger<SpectatorHandoffController> logger,
        ISpectatorHandoffAuditStore? audit = null)
    {
        _cookies = cookies;
        _issuer = issuer;
        _logger = logger;
        _audit = audit;
    }

    [HttpPost("handoff")]
    [EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
    public async Task<IActionResult> Handoff([FromBody] HandoffBody? body, CancellationToken ct)
    {
        if (body is null || body.GameId == Guid.Empty)
        {
            return BadRequest(new { error = "gameId is required." });
        }
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null)
        {
            return Unauthorized(new { error = "session-required" });
        }
        var scope = $"{ScopePrefix}{body.GameId:D}";
        // Phase K Wave 13 — Bishop. Generate a deterministic jti
        // so the audit row + the JWT carry the same identifier.
        // The claim lands inside the existing "claims" envelope
        // (matches the rest of the issuer's payload shape).
        var jti = Guid.NewGuid().ToString("D");
        var claims = new Dictionary<string, object?>
        {
            ["scope"] = scope,
            ["game_id"] = body.GameId.ToString("D"),
            ["jti"] = jti,
        };
        var result = await _issuer.IssueAsync(
            session.PlayerId,
            claims,
            DefaultTokenLifetime,
            ct);
        // Phase K Wave 13 — Bishop. Write the audit row after the
        // mint succeeds so we don't stamp a row for a token that
        // never reached the issuer. The store is optional —
        // legacy callers that haven't wired the store still get
        // a working handoff (with a logged warning).
        if (_audit is null)
        {
            _logger.LogDebug(
                "Spectator handoff audit store not registered; skipping audit row for jti={Jti}.",
                jti);
        }
        else
        {
            try
            {
                var ua = HttpContext.Request.Headers.UserAgent.ToString();
                if (ua.Length > 256) ua = ua[..256];
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                if (ip.Length > 64) ip = ip[..64];
                await _audit.InsertAsync(new SpectatorHandoffAuditRecord
                {
                    UserId = session.PlayerId,
                    GameId = body.GameId,
                    TokenJti = jti,
                    IssuedAt = DateTime.UtcNow,
                    Scope = scope,
                    ClientIp = ip,
                    UserAgent = ua,
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Spectator handoff audit insert failed for jti={Jti}; mint succeeded.",
                    jti);
            }
        }
        return Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAtUtc,
            scope,
            ttlSeconds = (int)DefaultTokenLifetime.TotalSeconds,
        });
    }

    /// <summary>Phase K Wave 12 — Bishop. POST body.</summary>
    public sealed class HandoffBody
    {
        public Guid GameId { get; set; }
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. Token-validator companion for the
/// spectator handoff surface. Wraps
/// <see cref="JwtValidationService"/> with the additional
/// per-game scope check so a token minted for game A can't be
/// replayed against game B.
/// </summary>
public sealed class SpectatorHandoffTokenValidator
{
    private readonly JwtValidationService _validator;

    public SpectatorHandoffTokenValidator(JwtValidationService validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Validates a spectator-handoff token. Returns
    /// <see cref="SpectatorTokenValidationResult.Allow"/> when
    /// the signature is good AND the token's <c>scope</c> claim
    /// matches <c>"spectator:{gameId}"</c>. Returns
    /// <see cref="SpectatorTokenValidationResult.Deny"/> with a
    /// reason code on every failure path.
    /// </summary>
    public SpectatorTokenValidationResult Validate(string? token, Guid gameId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return SpectatorTokenValidationResult.Deny("token-missing");
        }
        var result = _validator.Validate(token);
        if (!result.Ok)
        {
            return SpectatorTokenValidationResult.Deny(result.Error ?? "token-invalid");
        }
        var expectedScope = $"{SpectatorHandoffController.ScopePrefix}{gameId:D}";
        var scope = ExtractScopeClaim(result);
        if (!string.Equals(scope, expectedScope, StringComparison.Ordinal))
        {
            return SpectatorTokenValidationResult.Deny("scope-mismatch");
        }
        return SpectatorTokenValidationResult.Allow(result.Subject ?? string.Empty);
    }

    private static string ExtractScopeClaim(JwtValidationResult result)
    {
        if (result.Claims is null) return string.Empty;
        if (result.Claims.TryGetValue("scope", out var scopeValue) && scopeValue is not null)
        {
            return scopeValue.ToString() ?? string.Empty;
        }
        return string.Empty;
    }
}

/// <summary>
/// Phase K Wave 12 — Bishop. Validation result envelope.
/// </summary>
public readonly record struct SpectatorTokenValidationResult(
    bool Allowed,
    string Subject,
    string Reason)
{
    public static SpectatorTokenValidationResult Allow(string subject) =>
        new(true, subject, "ok");

    public static SpectatorTokenValidationResult Deny(string reason) =>
        new(false, string.Empty, reason);
}
