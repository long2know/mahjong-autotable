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
///         be <c>admin</c>). Returns
///         <c>{ token, expiresAtUtc, kid }</c>. 401 when no session,
///         403 when not admin, 400 when subject is empty.</item>
///   <item><c>POST /api/auth/validate</c> — body <c>{ token }</c>.
///         Unauthenticated; rate-limited per-IP at 100/min via the
///         <see cref="RateLimitingExtensions.AuthValidatePolicy"/>
///         token-bucket. Returns
///         <c>{ valid, subject?, claims?, kid?, error? }</c>.</item>
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
        return Ok(new
        {
            token = result.Token,
            expiresAtUtc = result.ExpiresAtUtc,
            kid = result.Kid,
        });
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
