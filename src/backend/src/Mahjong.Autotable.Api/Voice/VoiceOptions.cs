namespace Mahjong.Autotable.Api.Voice;

// Phase K Wave 2 — Bishop (backend). Configuration knobs for the WebRTC
// voice signalling layer. Default `Enabled = false` keeps the feature
// opt-in per-deployment; table creators can flip individual rooms via
// the future per-table toggle (Wave 3). `MaxPeersPerTable = 4` enforces
// the mesh-only ceiling Vasquez's contract asserts. `TurnServers` lets
// operators pre-populate a STUN/TURN list returned by `/api/turn`.
public sealed class VoiceOptions
{
    public bool Enabled { get; set; } = false;
    public int MaxPeersPerTable { get; set; } = 4;
    public int RateLimitPerSecond { get; set; } = 30;
    public List<TurnServerOption> TurnServers { get; set; } = new();
}

public sealed class TurnServerOption
{
    public string Url { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
