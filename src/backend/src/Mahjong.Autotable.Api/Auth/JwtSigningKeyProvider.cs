using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Autotable.Api.Auth;

/// <summary>
/// Phase K Wave 4 — Bishop. Singleton key store materialised from
/// <see cref="AuthOptions.JwtSigningKeys"/>. The list is cached for
/// the lifetime of the process — rotation requires a pod restart
/// (matches the existing OAuthStateProtector contract; the operator
/// runbook is <c>docs/jwt-rotation.md</c> §4).
///
/// <para>Load-time precedence:
/// <list type="number">
///   <item><see cref="AuthOptions.JwtSigningKeys"/> array — the
///         canonical Wave-3 shape. Entry 0 is the active signer.</item>
///   <item>Legacy <see cref="AuthOptions.JwtSigningKey"/> singular —
///         consumed when the array is empty (one-wave backward
///         compatibility, slated for Wave-5 removal).</item>
///   <item>Per-process random fallback — minted when both above are
///         empty so dev / test never need explicit secrets. A warning
///         is logged so operators notice the unsuitable-for-prod
///         state.</item>
/// </list></para>
/// </summary>
public sealed class JwtSigningKeyProvider
{
    private readonly IReadOnlyList<JwtSigningKey> _keys;
    private readonly Dictionary<string, JwtSigningKey> _byKid;
    private readonly bool _fallbackKeyInUse;

    public JwtSigningKeyProvider(AuthOptions options, ILogger<JwtSigningKeyProvider> logger)
    {
        var resolved = new List<JwtSigningKey>();
        if (options.JwtSigningKeys is { Length: > 0 })
        {
            for (var i = 0; i < options.JwtSigningKeys.Length; i++)
            {
                var raw = options.JwtSigningKeys[i];
                if (string.IsNullOrEmpty(raw)) continue;
                resolved.Add(new JwtSigningKey(resolved.Count, raw));
            }
        }
        if (resolved.Count == 0 && !string.IsNullOrEmpty(options.JwtSigningKey))
        {
            logger.LogWarning(
                "AuthOptions.JwtSigningKey (singular) is set but JwtSigningKeys (array) is empty — falling back to the legacy key. This path is removed in Wave 5; populate Authentication:JwtSigningKeys instead.");
            resolved.Add(new JwtSigningKey(0, options.JwtSigningKey));
        }
        if (resolved.Count == 0)
        {
            // Phase K Wave 4 — dev / test fallback: mint a per-process
            // random key so the issuance + validation surface stays
            // resolvable without explicit operator config. The warning
            // is loud so production deployments notice.
            var random = new byte[48];
            System.Security.Cryptography.RandomNumberGenerator.Fill(random);
            var fallback = Convert.ToBase64String(random);
            resolved.Add(new JwtSigningKey(0, fallback));
            _fallbackKeyInUse = true;
            logger.LogWarning(
                "No JWT signing keys configured (Authentication:JwtSigningKeys empty AND Authentication:JwtSigningKey unset). Minted a per-process random HMAC key. Set Authentication:JwtSigningKeys[0] for production deployments — see docs/jwt-rotation.md.");
        }
        else if (_fallbackKeyInUse == false)
        {
            logger.LogInformation(
                "JWT signing keys loaded: {Count} key(s); active signer kid={Kid}.",
                resolved.Count, resolved[0].Kid);
        }
        _keys = resolved;
        _byKid = resolved.ToDictionary(k => k.Kid, StringComparer.Ordinal);
    }

    /// <summary>Active signer used for new token issuance.</summary>
    public JwtSigningKey ActiveKey => _keys[0];

    /// <summary>Full fallback list in load order. Index 0 is the active
    /// signer; later entries are accepted on validation only.</summary>
    public IReadOnlyList<JwtSigningKey> AllKeys => _keys;

    /// <summary>True when the active key was minted at process start
    /// because the operator left both knobs unset — diagnostic-only.</summary>
    public bool UsingEphemeralFallbackKey => _fallbackKeyInUse;

    /// <summary>Looks up a key by its deterministic <see cref="JwtSigningKey.Kid"/>.
    /// Returns null when the token's <c>kid</c> header does not match
    /// any loaded key (validators then fall through to the
    /// try-all-keys loop).</summary>
    public JwtSigningKey? TryGetByKid(string? kid)
    {
        if (string.IsNullOrEmpty(kid)) return null;
        return _byKid.TryGetValue(kid, out var key) ? key : null;
    }
}
