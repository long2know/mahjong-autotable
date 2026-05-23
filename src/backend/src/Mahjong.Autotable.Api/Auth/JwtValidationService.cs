using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 4 — Bishop. Validates HS256 JSON Web Tokens minted by
/// <see cref="JwtIssuingService"/>. The validator is fallback-aware:
/// it iterates every entry in <see cref="JwtSigningKeyProvider.AllKeys"/>
/// (active + previous) so a token signed under any historical key
/// continues to validate until the operator drops the key from the
/// list. Tokens carrying a recognised <c>kid</c> header take the
/// fast-path (single HMAC verify against the matching key);
/// kid-less or unmatched-kid tokens fall back to the try-all-keys
/// loop. The two paths return identical results — <c>kid</c> is an
/// optimisation, not an authorisation gate.
///
/// <para>Returned <see cref="JwtValidationResult"/>:
/// <list type="bullet">
///   <item><c>Ok=true</c> with <c>Subject</c> + <c>Claims</c> on a
///         clean signature + within-lifetime check.</item>
///   <item><c>Ok=false</c> with a stable <c>Error</c> string on any
///         failure (malformed segments, bad signature, expired,
///         pre-iat). The error wire-names are pinned by
///         <c>POST /api/auth/validate</c>.</item>
/// </list></para>
///
/// <para>Phase K Wave 6 — Bishop. RS256 tokens are accepted alongside
/// HS256 to support a zero-downtime HMAC→RSA migration. The token
/// header's <c>alg</c> selects the algorithm family (HS256 vs RS256);
/// the <c>kid</c> selects the specific key inside that family. We
/// never cross algorithm families — a token claiming RS256 MUST
/// verify against an entry in <see cref="JwtSigningKeyProvider.AllRsaKeys"/>
/// and a token claiming HS256 MUST verify against an entry in
/// <see cref="JwtSigningKeyProvider.AllKeys"/>. Any other algorithm
/// returns <see cref="ErrorUnsupportedAlg"/>.</para>
/// </summary>
public sealed class JwtValidationService
{
    public const string ErrorMalformed = "malformed";
    public const string ErrorBadSignature = "bad-signature";
    public const string ErrorExpired = "expired";
    public const string ErrorPremature = "premature";
    public const string ErrorUnsupportedAlg = "unsupported-alg";

    private readonly JwtSigningKeyProvider _keys;

    public JwtValidationService(JwtSigningKeyProvider keys)
    {
        _keys = keys;
    }

    /// <summary>
    /// Validates the supplied JWT against every entry in the fallback
    /// list. Returns the first match; if no key validates, returns
    /// <see cref="ErrorBadSignature"/>.
    /// </summary>
    public JwtValidationResult Validate(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return JwtValidationResult.Failure(ErrorMalformed);

        var parts = token.Split('.');
        if (parts.Length != 3)
            return JwtValidationResult.Failure(ErrorMalformed);

        byte[] headerBytes, payloadBytes, signatureBytes;
        try
        {
            headerBytes = Base64UrlDecode(parts[0]);
            payloadBytes = Base64UrlDecode(parts[1]);
            signatureBytes = Base64UrlDecode(parts[2]);
        }
        catch (FormatException)
        {
            return JwtValidationResult.Failure(ErrorMalformed);
        }

        Dictionary<string, JsonElement>? header;
        Dictionary<string, JsonElement>? payload;
        try
        {
            header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerBytes);
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes);
        }
        catch (JsonException)
        {
            return JwtValidationResult.Failure(ErrorMalformed);
        }
        if (header is null || payload is null)
            return JwtValidationResult.Failure(ErrorMalformed);

        if (!header.TryGetValue("alg", out var algEl) || algEl.ValueKind != JsonValueKind.String)
            return JwtValidationResult.Failure(ErrorUnsupportedAlg);
        var alg = algEl.GetString();
        var isHs256 = string.Equals(alg, "HS256", StringComparison.Ordinal);
        var isRs256 = string.Equals(alg, "RS256", StringComparison.Ordinal);
        if (!isHs256 && !isRs256)
            return JwtValidationResult.Failure(ErrorUnsupportedAlg);

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");

        string? kid = null;
        if (header.TryGetValue("kid", out var kidEl) && kidEl.ValueKind == JsonValueKind.String)
            kid = kidEl.GetString();

        string? matchedKid = null;

        // Phase K Wave 6 — alg-aware verification. RS256 tokens MUST
        // verify against an RSA key; HS256 tokens MUST verify against
        // an HMAC key. The kid header is a fast-path hint inside the
        // algorithm-appropriate fallback list — we never cross
        // algorithm families (a forged token claiming alg=HS256 with
        // an RSA-public-key kid would otherwise let an attacker
        // bypass the signature; the alg-family check upstream blocks
        // that path).
        if (isRs256)
        {
            matchedKid = TryVerifyRsa(signingInput, signatureBytes, kid);
            if (matchedKid is null)
                return JwtValidationResult.Failure(ErrorBadSignature);
        }
        else
        {
            matchedKid = TryVerifyHmac(signingInput, signatureBytes, kid);
            if (matchedKid is null)
                return JwtValidationResult.Failure(ErrorBadSignature);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.TryGetValue("exp", out var expEl)
            && expEl.ValueKind == JsonValueKind.Number
            && expEl.TryGetInt64(out var exp)
            && exp < now)
        {
            return JwtValidationResult.Failure(ErrorExpired);
        }
        if (payload.TryGetValue("iat", out var iatEl)
            && iatEl.ValueKind == JsonValueKind.Number
            && iatEl.TryGetInt64(out var iat)
            && iat > now + 60)
        {
            // 60 second tolerance for clock skew.
            return JwtValidationResult.Failure(ErrorPremature);
        }

        var subject = payload.TryGetValue("sub", out var subEl) && subEl.ValueKind == JsonValueKind.String
            ? subEl.GetString() ?? string.Empty
            : string.Empty;

        Dictionary<string, object?>? claims = null;
        if (payload.TryGetValue("claims", out var claimsEl)
            && claimsEl.ValueKind == JsonValueKind.Object)
        {
            claims = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in claimsEl.EnumerateObject())
            {
                claims[prop.Name] = JsonElementToObject(prop.Value);
            }
        }

        return JwtValidationResult.Success(subject, claims, matchedKid);
    }

    private string? TryVerifyHmac(byte[] signingInput, byte[] expected, string? kid)
    {
        var fastPathKey = _keys.TryGetByKid(kid);
        if (fastPathKey is not null && VerifySignature(signingInput, expected, fastPathKey))
            return fastPathKey.Kid;
        foreach (var candidate in _keys.AllKeys)
        {
            if (VerifySignature(signingInput, expected, candidate))
                return candidate.Kid;
        }
        return null;
    }

    private string? TryVerifyRsa(byte[] signingInput, byte[] expected, string? kid)
    {
        var fastPathKey = _keys.TryGetRsaByKid(kid);
        if (fastPathKey is not null && VerifyRsaSignature(signingInput, expected, fastPathKey))
            return fastPathKey.Kid;
        foreach (var candidate in _keys.AllRsaKeys)
        {
            if (VerifyRsaSignature(signingInput, expected, candidate))
                return candidate.Kid;
        }
        return null;
    }

    private static bool VerifySignature(byte[] signingInput, byte[] expected, JwtSigningKey key)
    {
        Span<byte> computed = stackalloc byte[32];
        HMACSHA256.HashData(key.Material, signingInput, computed);
        if (expected.Length != computed.Length) return false;
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    private static bool VerifyRsaSignature(byte[] signingInput, byte[] expected, JwtRsaSigningKey key)
    {
        return key.Rsa.VerifyData(
            signingInput,
            expected,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.ToString(),
    };

    internal static byte[] Base64UrlDecode(string segment)
    {
        var s = segment.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Invalid base64url segment length.");
        }
        return Convert.FromBase64String(s);
    }
}

/// <summary>
/// Phase K Wave 4 — Bishop. Result envelope for
/// <see cref="JwtValidationService.Validate"/>. Field names match
/// the wire shape returned by <c>POST /api/auth/validate</c>.
/// </summary>
public sealed class JwtValidationResult
{
    public bool Ok { get; }
    public string? Subject { get; }
    public IReadOnlyDictionary<string, object?>? Claims { get; }
    public string? Kid { get; }
    public string? Error { get; }

    private JwtValidationResult(bool ok, string? subject, IReadOnlyDictionary<string, object?>? claims, string? kid, string? error)
    {
        Ok = ok;
        Subject = subject;
        Claims = claims;
        Kid = kid;
        Error = error;
    }

    public static JwtValidationResult Success(string subject, IReadOnlyDictionary<string, object?>? claims, string kid) =>
        new(true, subject, claims, kid, null);

    public static JwtValidationResult Failure(string error) =>
        new(false, null, null, null, error);
}
