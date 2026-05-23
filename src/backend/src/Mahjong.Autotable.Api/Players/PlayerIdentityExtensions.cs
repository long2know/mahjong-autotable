using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase J Wave 6 — uniform <c>GetPlayerId()</c> helpers that resolve the
/// persistent player identifier across the three entry points: SignalR hub
/// methods, raw <see cref="HttpContext"/> request handlers, and the
/// autotable-WS endpoint.
///
/// <para><b>Resolution order:</b>
/// <list type="number">
///   <item><c>HubCallerContext.Items["playerId"]</c> (set by
///         <c>ChangshaHub.OnConnectedAsync</c> from the cookie).</item>
///   <item><c>HttpContext.Items["playerId"]</c> (set by any earlier
///         middleware / endpoint).</item>
///   <item>The <c>mahjong_pid</c> cookie value.</item>
///   <item>A defensive fall-back to the SignalR connection id (legacy
///         behaviour; reached only when no cookie was provided AND the
///         hub didn't pre-resolve).</item>
/// </list></para>
/// </summary>
public static class PlayerIdentityExtensions
{
    /// <summary>Items-bag key under which the resolved player id is stored.</summary>
    public const string PlayerIdItemKey = "playerId";

    /// <summary>
    /// Returns the caller's persistent player id. See <see cref="PlayerIdentityExtensions"/>
    /// for the resolution order. Never returns <c>null</c> — the worst case
    /// is the SignalR connection id fall-back, which preserves Wave-5
    /// semantics for any code path that somehow bypasses the cookie.
    /// </summary>
    public static string GetPlayerId(this HubCallerContext context)
    {
        if (context.Items.TryGetValue(PlayerIdItemKey, out var v) && v is string s && !string.IsNullOrEmpty(s))
            return s;

        var http = context.GetHttpContext();
        if (http is not null)
        {
            var resolved = http.GetPlayerIdOrNull();
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        return context.ConnectionId;
    }

    /// <summary>
    /// Returns the caller's persistent player id from an
    /// <see cref="HttpContext"/> (REST controllers, autotable-WS endpoint).
    /// Reads <see cref="HttpContext.Items"/> first (so middleware / earlier
    /// handlers can pin the value), then falls back to the cookie. Returns
    /// <c>null</c> when neither source carries a value.
    /// </summary>
    public static string? GetPlayerIdOrNull(this HttpContext context)
    {
        if (context.Items.TryGetValue(PlayerIdItemKey, out var v) && v is string s && !string.IsNullOrEmpty(s))
            return s;

        if (context.Request.Cookies.TryGetValue(PlayerIdentityService.CookieName, out var cookie)
            && PlayerIdentityService.IsValidPlayerId(cookie))
        {
            return cookie;
        }

        return null;
    }
}
