using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 4 — Bishop. Machine-to-machine JWT mint + validation
/// endpoints layered on top of <see cref="JwtIssuingService"/> +
/// <see cref="JwtValidationService"/>.
///
/// <list type="bullet">
///   <item><c>POST /api/auth/token</c> — body
///         <c>{ subject, claims? }</c>. Requires an admin session
///         (resolved via <see cref="AuthCookieService"/>, role must
///         be <c>admin</c>). Returns the pinned
///         <see cref="AuthTokenResponse"/> envelope on success. 401
///         when no session, 403 when not admin, 400 when subject is
///         empty.</item>
///   <item><c>POST /api/auth/validate</c> — body <c>{ token }</c>.
///         Unauthenticated; rate-limited per-IP at 100/min via the
///         <see cref="RateLimitingExtensions.AuthValidatePolicy"/>
///         token-bucket. Returns
///         <c>{ valid, subject?, claims?, kid?, error? }</c>.</item>
///   <item><c>GET /api/auth/.well-known/jwks.json</c> — Phase K
///         Wave 5. HS256 signing leaves nothing publishable (the
///         shared secret would compromise the entire surface), so
///         the endpoint returns 404 with
///         <c>Cache-Control: no-store</c> + a structured envelope
///         that explains the rationale. The route MUST exist so
///         downstream caches don't pin a positive 404 ahead of the
///         Phase L RS256 flip — operators can validate the negative
///         shape today and the surface will swap to a real
///         <c>{ keys: [...] }</c> document without a URL change.</item>
/// </list>
///
/// <para>The endpoints are deliberately split into a separate
/// controller from <see cref="AuthController"/> so the cookie-based
/// flows stay isolated from the machine-to-machine surface — the two
/// audiences have different rate-limit profiles and different audit
/// requirements.</para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthTokenController : ControllerBase
{
    private readonly AuthCookieService _cookies;
    private readonly JwtIssuingService _issuer;
    private readonly JwtValidationService _validator;

    public AuthTokenController(
        AuthCookieService cookies,
        JwtIssuingService issuer,
        JwtValidationService validator)
    {
        _cookies = cookies;
        _issuer = issuer;
        _validator = validator;
    }

    [HttpPost("token")]
    [EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
    public async Task<IActionResult> Issue([FromBody] IssueBody? body, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null)
            return Unauthorized(new { error = "Authentication required to mint tokens." });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin role required to mint tokens." });
        if (body is null || string.IsNullOrWhiteSpace(body.Subject))
            return BadRequest(new { error = "subject is required." });

        IReadOnlyDictionary<string, object?>? claims = body.Claims is { Count: > 0 }
            ? body.Claims!
            : null;
        var result = await _issuer.IssueAsync(body.Subject.Trim(), claims, ct: ct);

        // Phase K Wave 5 — Bishop. Surface the pinned AuthTokenResponse
        // envelope (tokenType + expiresInSeconds) on top of the Wave-4
        // { token, expiresAtUtc, kid } triple. Clamp the relative TTL
        // at zero so a token minted right at the expiry boundary never
        // returns a negative integer (some SDK schedulers treat that
        // as "immediate retry forever").
        var expiresInSeconds = (int)Math.Max(
            0,
            Math.Round((result.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds));
        return Ok(new AuthTokenResponse(
            Token: result.Token,
            ExpiresAtUtc: result.ExpiresAtUtc,
            Kid: result.Kid,
            TokenType: AuthTokenResponse.BearerTokenType,
            ExpiresInSeconds: expiresInSeconds));
    }

    [HttpPost("validate")]
    [EnableRateLimiting(RateLimitingExtensions.AuthValidatePolicy)]
    public IActionResult Validate([FromBody] ValidateBody? body)
    {
        if (body is null || string.IsNullOrEmpty(body.Token))
            return Ok(new { valid = false, error = JwtValidationService.ErrorMalformed });
        var result = _validator.Validate(body.Token);
        if (!result.Ok)
            return Ok(new { valid = false, error = result.Error });
        return Ok(new
        {
            valid = true,
            subject = result.Subject,
            claims = result.Claims,
            kid = result.Kid,
        });
    }

    /// <summary>
    /// Phase K Wave 5 — Bishop. RFC 7517 JWKS document endpoint. The
    /// Wave-5 issuance pipeline is HMAC-only (HS256), so a JWKS
    /// document is intentionally NOT published — exposing the HMAC
    /// secret in <c>k</c> form would defeat the entire purpose of
    /// the signing key. The endpoint nonetheless exists so caches
    /// (browser, CDN, sidecar) pin the negative 404 with
    /// <c>Cache-Control: no-store</c>, preventing them from blocking
    /// the Phase L RS256 flip when this route will start returning a
    /// real <c>{ keys: [...] }</c> array under the same URL.
    /// </summary>
    [HttpGet(".well-known/jwks.json")]
    public IActionResult Jwks()
    {
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status404NotFound, new
        {
            error = "JWKS document is not published for HMAC-signed tokens.",
            algorithm = "HS256",
            note = "Phase L will flip this surface to an RS256 JWKS array; the URL is reserved.",
        });
    }

    /// <summary>Body for <c>POST /api/auth/token</c>.</summary>
    public sealed class IssueBody
    {
        public string? Subject { get; set; }
        public Dictionary<string, object?>? Claims { get; set; }
    }

    /// <summary>Body for <c>POST /api/auth/validate</c>.</summary>
    public sealed class ValidateBody
    {
        public string? Token { get; set; }
    }
}
