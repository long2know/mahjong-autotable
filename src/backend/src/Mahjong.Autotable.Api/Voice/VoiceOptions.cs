namespace Mahjong.Autotable.Api.Voice;

// Phase K Wave 2 — Bishop (backend). Configuration knobs for the WebRTC
// voice signalling layer. Default `Enabled = false` keeps the feature
// opt-in per-deployment; table creators can flip individual rooms via
// the per-table toggle (Wave 3, ChangshaGame.VoiceEnabled).
// `MaxPeersPerTable = 4` enforces the mesh-only ceiling Vasquez's
// contract asserts. `TurnServers` lets operators pre-populate a
// STUN/TURN list returned by `/api/turn`.
//
// Phase K Wave 3 — Bishop. `TurnSharedSecret` enables the
// `/api/turn/credentials` HMAC-SHA1 minting endpoint (RFC 7635-style
// short-term TURN credentials). When unset, the credential endpoint
// returns 503 and the older anon `/api/turn` STUN-only path remains
// the only fallback. `TurnCredentialTtlSeconds` controls how long a
// minted credential is valid (default 1h).
public sealed class VoiceOptions
{
    public bool Enabled { get; set; } = false;
    public int MaxPeersPerTable { get; set; } = 4;
    public int RateLimitPerSecond { get; set; } = 30;
    public List<TurnServerOption> TurnServers { get; set; } = new();

    /// <summary>
    /// Phase K Wave 3 — shared secret used by the TURN server (e.g.
    /// coturn's <c>--static-auth-secret</c>) to validate
    /// <c>username = unix_ttl:playerId</c> + <c>credential =
    /// base64(HMAC-SHA1(secret, username))</c> short-term credentials.
    /// Leave null/empty to disable the mint endpoint.
    /// </summary>
    public string? TurnSharedSecret { get; set; }

    /// <summary>
    /// Phase K Wave 3 — TTL (seconds) applied to short-term TURN
    /// credentials minted by <c>POST /api/turn/credentials</c>. Default
    /// 3600 (1h) matches the coturn convention.
    /// </summary>
    public int TurnCredentialTtlSeconds { get; set; } = 3600;
}

public sealed class TurnServerOption
{
    public string Url { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
