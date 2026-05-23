using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 8 — Bishop. Cache layer for the RS256 JWKS
/// document. Wave 7 shipped the endpoint with per-request RSA-key
/// marshalling — a heavy CPU cost (base64-url encoding +
/// JsonSerializer allocations) repeated on every request. Under
/// load the endpoint became a hot path: federated verifiers re-pull
/// the document at every token refresh and many CDN edges request
/// it on cold cache.
///
/// <para>The cache holds the pre-serialised JSON body + the
/// strong-ETag header value (SHA-256 over the body) keyed by the
/// active RSA key set's combined kid list. A change to any key id
/// (rotation, key addition / removal) invalidates the cache so the
/// next request re-emits the fresh document.</para>
///
/// <para>TTL is 60 seconds — short enough that key rotations land in
/// the document within one CDN refresh cycle, long enough that a
/// 1000-RPS burst hits the cached body roughly 60,000 times per
/// miss. The <see cref="DefaultTtl"/> is a constant so the W8
/// contract test can pin the value; tests that need a different TTL
/// construct the cache directly with a custom value.</para>
///
/// <para>The W8 endpoint also honours <c>If-None-Match</c>: when the
/// inbound ETag matches the cached value the endpoint returns
/// <c>304 Not Modified</c> with no body, saving the network bytes
/// entirely.</para>
/// </summary>
public sealed class JwksCacheService
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);
    private const string CacheKey = "Mahjong.Autotable.Api.Auth.JwksCacheService::doc";

    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public JwksCacheService(IMemoryCache cache, TimeSpan? ttl = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _ttl = ttl ?? DefaultTtl;
    }

    /// <summary>
    /// Resolves the cached JWKS payload for the supplied key
    /// provider. The cache key includes the active + archived kid
    /// list so any rotation invalidates the entry deterministically.
    /// </summary>
    public JwksPayload Resolve(JwtSigningKeyProvider keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var fingerprint = ComputeFingerprint(keys);

        if (_cache.TryGetValue(CacheKey, out JwksPayload? cached)
            && cached is not null
            && string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return cached;
        }

        var payload = Build(keys, fingerprint);
        _cache.Set(CacheKey, payload, _ttl);
        return payload;
    }

    /// <summary>
    /// Removes any cached entry. Used by tests + by operators who
    /// want to force the next request to re-marshal the keys (e.g.
    /// after a manual rotation).
    /// </summary>
    public void Invalidate() => _cache.Remove(CacheKey);

    private static string ComputeFingerprint(JwtSigningKeyProvider keys)
    {
        if (keys.AllRsaKeys.Count == 0) return "empty";
        var sb = new StringBuilder();
        foreach (var k in keys.AllRsaKeys)
        {
            sb.Append(k.Kid).Append('|');
        }
        return sb.ToString();
    }

    private static JwksPayload Build(JwtSigningKeyProvider keys, string fingerprint)
    {
        var publishedKeys = keys.AllRsaKeys.Select(k => new
        {
            kty = "RSA",
            kid = k.Kid,
            use = "sig",
            alg = "RS256",
            n = k.ModulusBase64Url,
            e = k.ExponentBase64Url,
        }).ToArray();

        // Pre-serialise once — the controller writes the body bytes
        // directly to the response, skipping the per-request JSON
        // allocation that was the W7 bottleneck.
        var body = JsonSerializer.Serialize(new { keys = publishedKeys });
        var etag = ComputeStrongEtag(body);
        return new JwksPayload(body, etag, fingerprint, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// RFC 7232 strong ETag — SHA-256 over the body, hex-lowercase,
    /// wrapped in double quotes. Strong (no leading <c>W/</c>) is
    /// correct because the body is a deterministic byte sequence
    /// for the given fingerprint.
    /// </summary>
    public static string ComputeStrongEtag(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        var sb = new StringBuilder(bytes.Length * 2 + 2);
        sb.Append('"');
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        sb.Append('"');
        return sb.ToString();
    }
}

/// <summary>
/// Phase K Wave 8 — Bishop. Cached JWKS payload. The body is the
/// canonical JSON string (already serialised) and ETag is the
/// matching strong-ETag header value. <see cref="Fingerprint"/> is
/// the kid-list signature; the cache layer uses it to detect
/// rotations against the live key provider.
/// </summary>
public sealed record JwksPayload(
    string Body,
    string ETag,
    string Fingerprint,
    DateTimeOffset BuiltAt);
