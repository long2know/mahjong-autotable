using Microsoft.AspNetCore.Http;

namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase J Wave 6 — issues + validates persistent opaque player identifiers.
///
/// <para>v1 design is intentionally lightweight: a 32-char lowercase hex GUID
/// (no dashes) is generated server-side and pinned to the browser via the
/// <see cref="CookieName"/> cookie. Subsequent SignalR / autotable-WS
/// connections include the cookie automatically (browsers attach cookies to
/// WebSocket upgrade handshakes), letting the backend recognise a returning
/// player and resume their <see cref="PlayerProfile"/> + <see cref="PlayerStats"/>.</para>
///
/// <para><b>No JWT, no signing key</b> — the worst case is someone copies a
/// cookie and impersonates an anonymous player (no auth implications, since
/// the game has no privileged operations beyond "this is my game"). Phase L
/// can layer OAuth on top by adding a verification step in
/// <see cref="ResolveFromCookie(HttpContext)"/> without changing any consumer.</para>
///
/// <para><b>Cookie attributes:</b>
/// <list type="bullet">
///   <item><c>HttpOnly</c> — JavaScript cannot read the value (prevents XSS theft).</item>
///   <item><c>Secure</c> only when the request scheme is HTTPS (so local
///         <c>http://localhost</c> dev still works; browsers reject Secure
///         cookies on plain HTTP).</item>
///   <item><c>SameSite=Lax</c> — sent on top-level navigations + safe
///         cross-origin requests; allows OAuth-style redirect flows in a
///         future wave without rewriting the cookie.</item>
///   <item><c>Max-Age=31536000</c> (1 year) — long-lived; the browser
///         re-creates the cookie on every <c>POST /api/identity</c> call so
///         it slides forward.</item>
///   <item><c>Path=/</c> — visible to <c>/api/*</c>, <c>/hubs/changsha</c>,
///         and <c>/autotable/ws</c>.</item>
/// </list></para>
/// </summary>
public sealed class PlayerIdentityService
{
    /// <summary>Cookie name used to persist the opaque player identifier.</summary>
    public const string CookieName = "mahjong_pid";

    /// <summary>Cookie lifetime (one year). The browser slides this forward on every mint/refresh.</summary>
    public static readonly TimeSpan CookieMaxAge = TimeSpan.FromDays(365);

    /// <summary>
    /// Mints a fresh opaque player identifier. 32-char lowercase hex (a GUID
    /// without dashes). Indistinguishable from a random string to outside
    /// observers — no information leaks from the value.
    /// </summary>
    public string Mint() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Returns the player id stored in the <see cref="CookieName"/> cookie,
    /// or <c>null</c> when the cookie is missing or malformed. Validation is
    /// deliberately permissive (any non-empty string up to 128 chars is
    /// accepted) so cookies set by earlier Wave-6 builds keep working through
    /// any future format tweaks; the only hard rule is that the value matches
    /// the <see cref="IsValidPlayerId(string?)"/> shape.
    /// </summary>
    public string? ResolveFromCookie(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var raw)
            && IsValidPlayerId(raw))
        {
            return raw;
        }
        return null;
    }

    /// <summary>
    /// Reads the cookie if present; mints + writes a fresh one if not. The
    /// returned value is always non-null. The response cookie is set every
    /// call (even on cookie-present pass-through) so the <c>Max-Age</c>
    /// window slides forward; this also re-applies the latest Secure /
    /// SameSite attributes if we ever tighten them.
    /// </summary>
    public string ResolveOrMint(HttpContext context)
    {
        var existing = ResolveFromCookie(context);
        var playerId = existing ?? Mint();
        WriteCookie(context, playerId);
        return playerId;
    }

    /// <summary>
    /// Writes <paramref name="playerId"/> to the response under
    /// <see cref="CookieName"/> with the canonical attribute set.
    /// <c>Secure</c> is conditional on the request scheme so local
    /// <c>http://</c> dev keeps the cookie.
    /// </summary>
    public void WriteCookie(HttpContext context, string playerId)
    {
        if (!IsValidPlayerId(playerId))
            throw new ArgumentException("playerId must be a non-empty opaque token.", nameof(playerId));

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = CookieMaxAge,
            Path = "/",
            IsEssential = true,
        };
        context.Response.Cookies.Append(CookieName, playerId, options);
    }

    /// <summary>
    /// Shape check: non-null, 1..128 chars, only safe URL-token chars
    /// (<c>[A-Za-z0-9_-]</c>). Anything outside this is rejected because the
    /// playerId flows into <see cref="ChangshaSeatState.PlayerId"/>, log
    /// scopes, and persistence keys — accepting arbitrary user input would
    /// open a log-forging / log-injection vector.
    /// </summary>
    public static bool IsValidPlayerId(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        if (candidate.Length > 128) return false;
        foreach (var c in candidate)
        {
            var ok = (c >= 'a' && c <= 'z')
                  || (c >= 'A' && c <= 'Z')
                  || (c >= '0' && c <= '9')
                  || c == '_' || c == '-';
            if (!ok) return false;
        }
        return true;
    }
}
