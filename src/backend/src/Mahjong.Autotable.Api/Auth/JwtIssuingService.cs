using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 4 — Bishop. Mints HS256 JSON Web Tokens against the
/// active signer in <see cref="JwtSigningKeyProvider.ActiveKey"/>.
/// Implements RFC 7519 manually (no Microsoft.IdentityModel.Tokens
/// dependency — we already depend on EF Core + ASP.NET; another
/// transitive surface is unnecessary for a 30-line HMAC-SHA256 mint).
///
/// <para>Header shape: <c>{ "alg": "HS256", "typ": "JWT", "kid": "&lt;deterministic-kid&gt;" }</c>.
/// Payload shape: <c>{ "sub": subject, "iat": unix-now, "exp": unix-now+ttl, "claims": &lt;passed-through&gt; }</c>.
/// Both objects are JSON-serialised, base64url-encoded, joined with a
/// `.`, signed with HMAC-SHA256(activeKey.Material), and the signature
/// appended as a third base64url segment.</para>
///
/// <para>Audit: every successful mint writes a
/// <see cref="ReconnectAuditEntry"/> row with
/// <c>Kind = "auth.jwt.signed.with_key.{index}"</c> so the operator
/// trail records which fallback-list slot signed each token. Failures
/// to write the audit row are swallowed (best-effort — the audit
/// table is a debugging convenience, not a hard prerequisite for
/// auth).</para>
/// </summary>
public sealed class JwtIssuingService
{
    public const int DefaultTokenLifetimeSeconds = 3600;

    private readonly JwtSigningKeyProvider _keys;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JwtIssuingService> _logger;

    public JwtIssuingService(
        JwtSigningKeyProvider keys,
        IServiceScopeFactory scopeFactory,
        ILogger<JwtIssuingService> logger)
    {
        _keys = keys;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Mints a JWT for <paramref name="subject"/> carrying the
    /// supplied <paramref name="claims"/>. The token lifetime defaults
    /// to <see cref="DefaultTokenLifetimeSeconds"/> when
    /// <paramref name="lifetime"/> is null.
    /// </summary>
    public async Task<JwtIssueResult> IssueAsync(
        string subject,
        IReadOnlyDictionary<string, object?>? claims = null,
        TimeSpan? lifetime = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("subject must not be empty.", nameof(subject));

        var key = _keys.ActiveKey;
        var ttl = lifetime ?? TimeSpan.FromSeconds(DefaultTokenLifetimeSeconds);
        var now = DateTimeOffset.UtcNow;
        var exp = now + ttl;

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
            ["kid"] = key.Kid,
        };
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = subject,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
        };
        if (claims is { Count: > 0 })
        {
            payload["claims"] = claims;
        }

        var headerSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerSegment}.{payloadSegment}";
        var signature = Sign(signingInput, key);
        var token = $"{signingInput}.{signature}";

        await WriteAuditAsync(subject, key, ct);

        return new JwtIssueResult(token, exp.UtcDateTime, key.Kid);
    }

    private static string Sign(string signingInput, JwtSigningKey key)
    {
        Span<byte> digest = stackalloc byte[32];
        HMACSHA256.HashData(key.Material, Encoding.ASCII.GetBytes(signingInput), digest);
        return Base64UrlEncode(digest);
    }

    private async Task WriteAuditAsync(string subject, JwtSigningKey key, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
            {
                Id = Guid.NewGuid(),
                PlayerId = subject,
                At = DateTime.UtcNow,
                Kind = $"{ReconnectAuditEntry.KindAuthJwtSignedPrefix}{key.Index}",
                Detail = key.Kid,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JWT mint audit write failed for subject={Subject}", subject);
        }
    }

    internal static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

/// <summary>
/// Phase K Wave 4 — Bishop. Result of <see cref="JwtIssuingService.IssueAsync"/>.
/// The shape mirrors the public <c>POST /api/auth/token</c> response
/// envelope so the controller can pass the record straight through.
/// </summary>
public sealed record JwtIssueResult(string Token, DateTime ExpiresAtUtc, string Kid);
