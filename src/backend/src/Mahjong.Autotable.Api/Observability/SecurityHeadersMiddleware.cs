using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Mahjong.Autotable.Api.Observability;

/// <summary>
/// Phase J Wave 8 — security headers + CDN cache-policy middleware
/// (Apone, DevOps). One ASP.NET Core middleware that:
///
/// <list type="number">
///   <item>Stamps the OWASP-recommended security headers on every response
///         (<c>X-Frame-Options</c>, <c>X-Content-Type-Options</c>,
///         <c>Referrer-Policy</c>, <c>Content-Security-Policy</c>).</item>
///   <item>Rewrites <c>Cache-Control</c> and <c>Vary</c> on static-asset
///         responses so Cloudflare and downstream CDNs cache hashed bundles
///         immutably while letting <c>index.html</c> revalidate every
///         request.</item>
/// </list>
///
/// <para>The CSP starts permissive: <c>script-src 'self' 'unsafe-eval'</c>
/// because Three.js's runtime shader compiler uses <c>new Function(...)</c>.
/// <c>'unsafe-inline'</c> is intentionally NOT in the policy — Parcel
/// already bundles every <c>&lt;script&gt;</c> tag so inline scripts are
/// absent. See <c>docs/cloudflare.md</c> for the rationale + the planned
/// follow-up to swap <c>'unsafe-eval'</c> for a nonce-based policy.</para>
///
/// <para><b>Cache policy.</b> Parcel emits filenames of the form
/// <c>name.&lt;hash&gt;.{js,css,wav,png,glb}</c> where <c>&lt;hash&gt;</c>
/// is 8 hex chars. These are content-addressed so we serve them with
/// <c>Cache-Control: public, max-age=31536000, immutable</c>. Everything
/// else (HTML, the index shell, non-hashed assets) is served with
/// <c>Cache-Control: no-cache, must-revalidate</c> so a new deploy
/// becomes immediately visible.</para>
///
/// <para>Configuration shape (<c>appsettings.json</c> § <c>Security</c>):</para>
/// <list type="bullet">
///   <item><b>ContentSecurityPolicy</b> (string, optional) — overrides the
///         default CSP entirely. Useful when a specific deploy needs to
///         allow additional script sources (e.g. an analytics CDN).</item>
///   <item><b>EnableHsts</b> (bool, default <c>false</c>) — when true,
///         adds <c>Strict-Transport-Security: max-age=31536000; includeSubDomains</c>.
///         Off by default because HSTS sticks once issued; production
///         deploys should opt in explicitly once HTTPS is confirmed
///         stable on the public origin.</item>
/// </list>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>Configuration key for the CSP override (string).</summary>
    public const string CspConfigKey = "Security:ContentSecurityPolicy";

    /// <summary>Configuration key for the HSTS opt-in (bool).</summary>
    public const string HstsConfigKey = "Security:EnableHsts";

    /// <summary>
    /// Default Content-Security-Policy. Permissive on script-src
    /// (<c>'unsafe-eval'</c> for Three.js shader compilation), tight on
    /// everything else.
    /// </summary>
    public const string DefaultCsp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "media-src 'self'; " +
        "font-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "worker-src 'self' blob:; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    /// <summary>
    /// Suffixes that Parcel attaches a content hash to. These get the
    /// immutable Cache-Control treatment iff <see cref="HasContentHash"/>
    /// also matches the filename.
    /// </summary>
    private static readonly string[] HashableSuffixes =
        [".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
         ".woff", ".woff2", ".ttf", ".glb", ".gltf", ".wav", ".mp4",
         ".m4a", ".ogg", ".mp3"];

    private readonly RequestDelegate _next;
    private readonly string _csp;
    private readonly bool _hstsEnabled;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var overrideCsp = configuration.GetValue<string?>(CspConfigKey);
        _csp = string.IsNullOrWhiteSpace(overrideCsp) ? DefaultCsp : overrideCsp;
        _hstsEnabled = configuration.GetValue<bool?>(HstsConfigKey) ?? false;
    }

    public Task InvokeAsync(HttpContext context)
    {
        // OnStarting fires just before the response body is written so
        // header mutations (Cache-Control, Vary) by downstream middleware
        // (UseStaticFiles in particular) have already settled. Capture
        // `this` so the CSP / HSTS settings are reachable without
        // spinning up DI inside the callback.
        var self = this;
        context.Response.OnStarting(() =>
        {
            ApplyHeaders(context, self);
            return Task.CompletedTask;
        });
        return _next(context);
    }

    private static void ApplyHeaders(HttpContext ctx, SecurityHeadersMiddleware instance)
    {
        var headers = ctx.Response.Headers;

        // 1. Security headers — applied on every response, regardless of
        //    Content-Type. Cheap idempotent assignments (header dictionary
        //    is a multi-value set; we use indexer-replace to avoid
        //    duplicates if a downstream component already set the key).
        if (!headers.ContainsKey("X-Frame-Options"))
        {
            headers["X-Frame-Options"] = "DENY";
        }
        if (!headers.ContainsKey("X-Content-Type-Options"))
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }
        if (!headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }
        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] = instance._csp;
        }
        if (instance._hstsEnabled && !headers.ContainsKey("Strict-Transport-Security"))
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        // 2. Cache-Control policy for static assets. Only rewrite when the
        //    request was clearly for a file (path has a known extension)
        //    and the framework hasn't already set an explicit Cache-Control.
        var path = ctx.Request.Path.Value;
        if (!string.IsNullOrEmpty(path))
        {
            var lower = path.ToLowerInvariant();
            var isIndexHtml = lower.EndsWith(".html", StringComparison.Ordinal)
                || lower == "/" || lower.EndsWith("/", StringComparison.Ordinal);

            if (isIndexHtml)
            {
                headers["Cache-Control"] = "no-cache, must-revalidate";
                AppendVary(headers, "Accept-Encoding");
            }
            else if (HasHashableExtension(lower))
            {
                if (HasContentHash(lower))
                {
                    headers["Cache-Control"] = "public, max-age=31536000, immutable";
                }
                else
                {
                    // Non-hashed static asset (e.g. /img/icon-32.png).
                    // Cache for an hour at the edge but allow revalidation.
                    if (!headers.ContainsKey("Cache-Control"))
                    {
                        headers["Cache-Control"] = "public, max-age=3600, must-revalidate";
                    }
                }
                AppendVary(headers, "Accept-Encoding");
            }
        }
    }

    private static void AppendVary(IHeaderDictionary headers, string value)
    {
        if (!headers.TryGetValue("Vary", out var existing) || StringValues.IsNullOrEmpty(existing))
        {
            headers["Vary"] = value;
            return;
        }
        // Avoid duplicating the token.
        foreach (var token in existing.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(token, value, StringComparison.OrdinalIgnoreCase)) return;
        }
        headers["Vary"] = existing + ", " + value;
    }

    private static bool HasHashableExtension(string path)
    {
        foreach (var suffix in HashableSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Detect Parcel's content-hash filename convention: <c>name.&lt;hash&gt;.ext</c>
    /// where <c>&lt;hash&gt;</c> is 8 hex characters. Examples that match:
    /// <c>autotable.9519e86d.js</c>, <c>autotable-src.6633d8fb.css</c>,
    /// <c>tiles.df85b4c4.png</c>. Examples that don't match (and so get
    /// the short-lived <c>max-age=3600</c> policy): <c>index.html</c>,
    /// <c>img/icon-32.auto.png</c> (the <c>.auto</c> token is 4 chars).
    /// </summary>
    internal static bool HasContentHash(string path)
    {
        var slash = path.LastIndexOf('/');
        var fileName = slash >= 0 ? path[(slash + 1)..] : path;
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot <= 0) return false;
        var stem = fileName[..lastDot];
        var prevDot = stem.LastIndexOf('.');
        if (prevDot <= 0) return false;
        var hashCandidate = stem[(prevDot + 1)..];
        if (hashCandidate.Length != 8) return false;
        foreach (var ch in hashCandidate)
        {
            if (!IsHex(ch)) return false;
        }
        return true;
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
