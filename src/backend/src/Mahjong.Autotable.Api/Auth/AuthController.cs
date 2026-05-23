using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Players;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase J Wave 8 — auth controller. Exposes:
/// <list type="bullet">
///   <item><c>GET  /api/auth/providers</c> — list configured providers (Google, GitHub, EmailMagicLink, dev when applicable).</item>
///   <item><c>GET  /api/auth/login/{provider}?returnUrl=…</c> — 302 to provider authorize URL.</item>
///   <item><c>GET  /api/auth/callback/{provider}?code=&amp;state=</c> — handle OAuth callback.</item>
///   <item><c>POST /api/auth/email/request</c> — issue a magic link.</item>
///   <item><c>GET  /api/auth/email/verify?token=…</c> — consume a magic link.</item>
///   <item><c>POST /api/auth/link/{provider}</c> — link an existing OAuth provider to the current session.</item>
///   <item><c>POST /api/auth/logout</c> — revoke session + clear cookie (keeps mahjong_pid).</item>
///   <item><c>GET  /api/auth/me</c> — current identity snapshot.</item>
///   <item><c>POST /api/auth/dev-login</c> — dev-only fake authenticated session (registered only when IsDevelopment).</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitingExtensions.AnonymousPolicy)]
public sealed class AuthController : ControllerBase
{
    private readonly AuthCookieService _cookies;
    private readonly AuthIdentityService _identities;
    private readonly OAuthService _oauth;
    private readonly MagicLinkService _magicLinks;
    private readonly PlayerIdentityService _playerIdentity;
    private readonly PlayerProfileService _profiles;
    private readonly AuthOptions _options;
    private readonly IHostEnvironment _env;

    public AuthController(
        AuthCookieService cookies,
        AuthIdentityService identities,
        OAuthService oauth,
        MagicLinkService magicLinks,
        PlayerIdentityService playerIdentity,
        PlayerProfileService profiles,
        AuthOptions options,
        IHostEnvironment env)
    {
        _cookies = cookies;
        _identities = identities;
        _oauth = oauth;
        _magicLinks = magicLinks;
        _playerIdentity = playerIdentity;
        _profiles = profiles;
        _options = options;
        _env = env;
    }

    /// <summary>
    /// Lists configured providers. Each entry: <c>{ id, displayName, enabled, kind }</c>.
    /// The dev provider is included only when <c>IHostEnvironment.IsDevelopment()</c>.
    /// </summary>
    [HttpGet("providers")]
    public IActionResult ListProviders()
    {
        var list = new List<object>();
        if (_oauth.IsConfigured("google"))
            list.Add(new { id = "google", displayName = "Google", enabled = true, kind = "oauth" });
        if (_oauth.IsConfigured("github"))
            list.Add(new { id = "github", displayName = "GitHub", enabled = true, kind = "oauth" });
        if (_options.EmailMagicLink.Enabled)
            list.Add(new { id = "email", displayName = "Email magic link", enabled = true, kind = "email" });
        if (_env.IsDevelopment())
            list.Add(new { id = "dev", displayName = "Dev sign-in", enabled = true, kind = "dev" });
        return Ok(new { providers = list });
    }

    /// <summary>302 redirect to the provider's authorize URL.</summary>
    [HttpGet("login/{provider}")]
    public IActionResult Login([FromRoute] string provider, [FromQuery] string? returnUrl = null)
    {
        if (!_oauth.IsConfigured(provider))
            return NotFound(new { error = "Provider not configured.", provider });

        // Ensure the caller has a mahjong_pid cookie so we have something to
        // bind the identity to on callback.
        _playerIdentity.ResolveOrMint(HttpContext);

        var state = OAuthService.GenerateState();
        var redirectUri = BuildCallbackUri(provider);

        HttpContext.Response.Cookies.Append(OAuthService.StateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/",
            IsEssential = true,
        });
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            HttpContext.Response.Cookies.Append(OAuthService.ReturnUrlCookieName, returnUrl, new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/",
                IsEssential = true,
            });
        }

        var authUrl = _oauth.BuildAuthorizeUrl(provider, redirectUri, state);
        return Redirect(authUrl);
    }

    /// <summary>OAuth callback. Validates state, exchanges code, links identity, issues session, redirects.</summary>
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(
        [FromRoute] string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        if (!_oauth.IsConfigured(provider))
            return NotFound(new { error = "Provider not configured.", provider });

        if (!string.IsNullOrEmpty(error))
            return BadRequest(new { error });

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest(new { error = "Missing code or state." });

        if (!HttpContext.Request.Cookies.TryGetValue(OAuthService.StateCookieName, out var expectedState)
            || !OAuthService.ConstantTimeEquals(state, expectedState ?? string.Empty))
        {
            return BadRequest(new { error = "Invalid state token." });
        }
        HttpContext.Response.Cookies.Delete(OAuthService.StateCookieName);
        HttpContext.Request.Cookies.TryGetValue(OAuthService.ReturnUrlCookieName, out var returnUrl);
        HttpContext.Response.Cookies.Delete(OAuthService.ReturnUrlCookieName);

        var redirectUri = BuildCallbackUri(provider);
        var info = await _oauth.ExchangeAndFetchUserInfoAsync(provider, code, redirectUri, ct);
        if (info is null)
            return BadRequest(new { error = "OAuth exchange failed." });

        var currentPlayerId = _playerIdentity.ResolveOrMint(HttpContext);
        var (identity, _) = await _identities.ResolveOrLinkAsync(
            provider: NormaliseProvider(provider),
            providerSubject: info.Subject,
            email: info.Email,
            emailVerified: info.EmailVerified,
            currentPlayerId: currentPlayerId,
            preferredDisplayName: info.DisplayName,
            ct: ct);

        // If the OAuth identity already pointed at a different PlayerId, we
        // need to rewrite the mahjong_pid cookie so subsequent requests use
        // the returning user's id.
        if (!string.Equals(identity.PlayerId, currentPlayerId, StringComparison.Ordinal))
        {
            _playerIdentity.WriteCookie(HttpContext, identity.PlayerId);
        }

        await _cookies.IssueAsync(HttpContext, identity.PlayerId, identity.Id, ct);

        return string.IsNullOrWhiteSpace(returnUrl) ? Redirect("/") : Redirect(returnUrl);
    }

    /// <summary>Issues a magic-link email + token.</summary>
    [HttpPost("email/request")]
    [HttpPost("magic-link/request")]
    public async Task<IActionResult> EmailRequest([FromBody] EmailRequestBody body, CancellationToken ct)
    {
        if (!_options.EmailMagicLink.Enabled)
            return NotFound(new { error = "Email magic-link is not enabled." });
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
            return BadRequest(new { error = "email is required." });

        var currentPlayerId = _playerIdentity.ResolveOrMint(HttpContext);
        var verifyUrl = BuildVerifyUri();

        try
        {
            var token = await _magicLinks.IssueAsync(body.Email, currentPlayerId, verifyUrl, ct);
            // In dev / test, surface the token so the UI / tests can
            // round-trip without parsing email content. In production the
            // token is delivered solely via email.
            if (_env.IsDevelopment())
            {
                return Ok(new
                {
                    requested = true,
                    email = body.Email,
                    devToken = token.Token,
                    expiresAt = token.ExpiresAt,
                });
            }
            return Ok(new { requested = true, email = body.Email });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Consumes a magic-link token + issues an auth session. Accepts
    /// the token via JSON body (<c>POST</c>) or query string (<c>GET</c>).</summary>
    [HttpGet("email/verify")]
    [HttpPost("email/verify")]
    [HttpGet("magic-link/verify")]
    [HttpPost("magic-link/verify")]
    public async Task<IActionResult> EmailVerify(
        [FromQuery] string? token,
        [FromBody] VerifyBody? body,
        CancellationToken ct)
    {
        token ??= body?.Token;
        if (string.IsNullOrEmpty(token))
            return BadRequest(new { error = "token is required." });

        var row = await _magicLinks.ConsumeAsync(token, ct);
        if (row is null)
            return BadRequest(new { error = "Token is invalid, expired, or already used." });

        var currentPlayerId = _playerIdentity.ResolveOrMint(HttpContext);
        var anchorPlayerId = row.RequestedPlayerId ?? currentPlayerId;

        var (identity, _) = await _identities.ResolveOrLinkAsync(
            provider: "EmailMagicLink",
            providerSubject: row.Email,
            email: row.Email,
            emailVerified: true,
            currentPlayerId: anchorPlayerId,
            preferredDisplayName: null,
            ct: ct);

        if (!string.Equals(identity.PlayerId, currentPlayerId, StringComparison.Ordinal))
        {
            _playerIdentity.WriteCookie(HttpContext, identity.PlayerId);
        }

        await _cookies.IssueAsync(HttpContext, identity.PlayerId, identity.Id, ct);

        return Ok(new
        {
            authenticated = true,
            playerId = identity.PlayerId,
            provider = identity.Provider,
            email = identity.Email,
        });
    }

    /// <summary>Allows an authenticated session to add a second provider.</summary>
    [HttpPost("link/{provider}")]
    public async Task<IActionResult> LinkProvider([FromRoute] string provider, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "Not authenticated." });
        if (!_oauth.IsConfigured(provider)) return NotFound(new { error = "Provider not configured.", provider });

        // Linking another provider is structurally identical to the login
        // flow except the caller's mahjong_pid is pinned by the active
        // auth session — return a redirect URL the UI can navigate to.
        var redirect = Url.Action(nameof(Login), new { provider, returnUrl = "/" })!;
        return Ok(new { redirectUrl = redirect });
    }

    /// <summary>Revokes the auth session (keeps the mahjong_pid cookie).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var removed = await _cookies.RevokeAsync(HttpContext, ct);
        return Ok(new { loggedOut = true, sessionRemoved = removed });
    }

    /// <summary>Returns the caller's identity snapshot.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var playerId = _playerIdentity.ResolveOrMint(HttpContext);
        var profile = await _profiles.GetOrCreateAsync(playerId, ct);
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        var identities = await _identities.GetIdentitiesAsync(playerId, ct);
        return Ok(new
        {
            playerId = profile.PlayerId,
            displayName = profile.DisplayName,
            avatarColor = profile.AvatarColor,
            isAuthenticated = session is not null,
            providers = identities.Select(i => new
            {
                provider = i.Provider,
                email = i.Email,
                emailVerified = i.EmailVerified,
                linkedAt = i.CreatedAt,
                lastUsedAt = i.LastUsedAt,
            }).ToArray(),
        });
    }

    /// <summary>Dev-only fake login. NOT registered in non-Development environments.</summary>
    [HttpPost("dev-login")]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginBody body, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound(new { error = "Dev-login is only available in Development." });
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
            return BadRequest(new { error = "email is required." });

        var currentPlayerId = _playerIdentity.ResolveOrMint(HttpContext);
        var (identity, _) = await _identities.ResolveOrLinkAsync(
            provider: "Dev",
            providerSubject: body.Email.Trim().ToLowerInvariant(),
            email: body.Email.Trim().ToLowerInvariant(),
            emailVerified: true,
            currentPlayerId: currentPlayerId,
            preferredDisplayName: body.DisplayName,
            ct: ct);

        if (!string.Equals(identity.PlayerId, currentPlayerId, StringComparison.Ordinal))
        {
            _playerIdentity.WriteCookie(HttpContext, identity.PlayerId);
        }
        var session = await _cookies.IssueAsync(HttpContext, identity.PlayerId, identity.Id, ct);
        var profile = await _profiles.GetOrCreateAsync(identity.PlayerId, ct);
        return Ok(new
        {
            authenticated = true,
            playerId = identity.PlayerId,
            displayName = profile.DisplayName,
            avatarColor = profile.AvatarColor,
            email = identity.Email,
            sessionExpiresAt = session.ExpiresAt,
        });
    }

    private string BuildCallbackUri(string provider)
    {
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}{Url.Action(nameof(Callback), new { provider })}";
    }

    private string BuildVerifyUri()
    {
        var req = HttpContext.Request;
        var configured = _options.EmailMagicLink.BaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/') + "/api/auth/email/verify";
        }
        return $"{req.Scheme}://{req.Host}{Url.Action(nameof(EmailVerify))}";
    }

    private static string NormaliseProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => "Google",
        "github" => "GitHub",
        _ => provider,
    };

    public sealed class EmailRequestBody
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class DevLoginBody
    {
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    public sealed class VerifyBody
    {
        public string? Token { get; set; }
    }
}
