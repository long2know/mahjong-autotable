namespace Mahjong.Autotable.Api.Data.Entities;

/// <summary>
/// Changsha game entity — stores multi-round game state.
/// State is stored as JSON for v1 simplicity.
/// </summary>
public class ChangshaGame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleSet { get; set; } = "changsha-v1";
    public int Seed { get; set; }
    public string StateJson { get; set; } = string.Empty;
    public int StateVersion { get; set; } = 1;
    public int CurrentHandNumber { get; set; } = 1;
    public int CurrentRoundNumber { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Phase J Wave 8 — optional pinned rule preset for this game. Nullable for
    /// backwards compatibility (pre-Wave-8 games + hub-default games leave this
    /// null). When set, <see cref="Mahjong.Autotable.Api.Changsha.Runtime.ChangshaGameRuntime"/>
    /// resolves the row at game creation time and propagates rule toggles
    /// into <see cref="Mahjong.Autotable.Api.Changsha.ChangshaGameState"/>.
    /// </summary>
    public Guid? RulePresetId { get; set; }
}

/// <summary>
/// Append-only event log for Changsha games.
/// Supports replay and reconnection.
/// </summary>
public class ChangshaGameEvent
{
    public long Id { get; set; }
    public Guid GameId { get; set; }
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public int TurnNumber { get; set; }
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int HandNumber { get; set; }
    public int StateVersion { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime PersistedUtc { get; set; }
}

/// <summary>
/// Phase J Wave 7 — canonical play-by-play snapshot persisted at game
/// completion. Built from <c>ChangshaGameState.EventLog</c> in
/// <see cref="Mahjong.Autotable.Api.Changsha.Runtime.ChangshaGameRuntime.EmitGameCompletedAsync"/>
/// and surfaced through <c>GET /api/games/{gameId}/replay</c>. One row per
/// completed game — re-completion (rare; only via re-hydration after a
/// crash + replay endpoint hit during the same lifecycle) is idempotent
/// (the runtime upserts on <see cref="GameId"/>). Wave 7 read-only
/// surface; the canonical write path is game-completion.
///
/// <para><see cref="EventsJson"/> is a serialised JSON array of
/// <c>{ turn:int, phase:string, actor:int, action:string, tilesJson:string,
/// timestampUtc:DateTime }</c> objects covering every event captured by
/// the runtime state machine (Deal / Discard / Claim / Hu and related
/// setup events). <c>actor</c> is the seat index (or <c>-1</c> for
/// system events); <c>tilesJson</c> is itself a JSON-encoded
/// <c>int[]</c> so the surface is self-describing without the consumer
/// having to know the runtime tile-id encoding.</para>
/// </summary>
public class ChangshaGameReplay
{
    /// <summary>Phase J Wave 9 — current replay schema version stamped on
    /// every new write. Old rows persisted under v1 keep their stored
    /// <see cref="SchemaVersion"/> (defaulted to 1 by the migration) so
    /// readers can branch on the value.</summary>
    public const int CurrentSchemaVersion = 2;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string EventsJson { get; set; } = string.Empty;

    /// <summary>Phase J Wave 9 — version of the JSON envelope stored in
    /// <see cref="EventsJson"/>. v1 = Wave 7/8 (events array only).
    /// v2 = Wave 9 (per-event source/durationMs/debugScore + envelope
    /// schemaVersion). Defaults to 1 so legacy reads don't break.</summary>
    public int SchemaVersion { get; set; } = 1;
}

/// <summary>
/// Phase J Wave 8 — server-driven Changsha rule preset. A preset captures
/// every toggleable rule the engine reads so games can pin a specific
/// behaviour profile at creation time. The default "Classic Changsha"
/// preset is seeded at startup in <c>DatabaseBootstrapper</c>; user-defined
/// presets are created via <c>POST /api/rule-presets</c> (creator-only
/// for update/delete). When a <see cref="ChangshaGame"/> is created with
/// a non-null <see cref="Mahjong.Autotable.Api.Data.Entities.ChangshaGame"/>.<c>RulePresetId</c>
/// the runtime resolves the preset row at init time and propagates the
/// settings to <see cref="Mahjong.Autotable.Api.Changsha.ChangshaGameState"/>.
/// </summary>
public class ChangshaRulePreset
{
    public const string ClassicPresetId = "00000000-0000-0000-0000-000000000001";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Maximum hands played before the game enters GameComplete.
    /// Mirrors <see cref="Mahjong.Autotable.Api.Changsha.ChangshaGameState.MaxHands"/>.</summary>
    public int HandLimit { get; set; } = 4;

    /// <summary>Caps the score the engine awards for a single hand
    /// (legacy "顶" cap). 0 means uncapped.</summary>
    public int MaxScorePerHand { get; set; } = 0;

    /// <summary>Whether wall-exhausted hands re-deal as a wash (washout / 流局).</summary>
    public bool AllowWashout { get; set; } = true;

    /// <summary>Whether a player may rob a freshly declared added-kong for a Hu (抢杠胡).</summary>
    public bool AllowKongRobbing { get; set; } = true;

    /// <summary>Whether a concealed pung may be promoted to an added kong (加杠).</summary>
    public bool AllowConcealedKongPromotion { get; set; } = true;

    /// <summary>Whether the seven-pairs (七对) shape is recognised as a winning hand.</summary>
    public bool AllowSevenPairs { get; set; } = true;

    /// <summary>Whether claiming a chow (吃) is allowed at all. Some house rules disable chow entirely.</summary>
    public bool AllowChow { get; set; } = true;

    /// <summary>Per-decision millisecond budget for bot strategies. Overrides
    /// <see cref="Mahjong.Autotable.Api.Changsha.Runtime.ChangshaRuntimeOptions.BotDecisionTimeoutMs"/>
    /// when the preset is in use.</summary>
    public int BotDecisionTimeoutMs { get; set; } = 2000;

    /// <summary>The persistent <c>PlayerId</c> of the creator.
    /// Authenticated users own their presets (only the creator may update/delete).
    /// The "Classic Changsha" seeded preset has <c>system</c> here.</summary>
    public string CreatorPlayerId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase J Wave 8 — auth identity linked to a <see cref="Mahjong.Autotable.Api.Players.PlayerProfile"/>.
/// Lets a player upgrade their anonymous cookie-only profile to an
/// authenticated one by linking one or more external providers (Google,
/// GitHub, or email magic-link). Multiple rows may share a
/// <see cref="PlayerId"/>; the unique index covers (Provider, ProviderSubject)
/// so a returning OAuth login finds the same PlayerId.
/// </summary>
public class PlayerAuthIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>One of <c>Google</c>, <c>GitHub</c>, <c>EmailMagicLink</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Stable subject id from the provider (Google "sub", GitHub "id",
    /// email address for magic-link).</summary>
    public string ProviderSubject { get; set; } = string.Empty;

    /// <summary>Optional email associated with the identity. Surfaced via
    /// <c>GET /api/auth/me</c> but never used as the join key.</summary>
    public string? Email { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase J Wave 8 — single-use email magic-link token. Created by
/// <c>POST /api/auth/email/request</c>, consumed by
/// <c>GET /api/auth/email/verify?token=</c>.
/// </summary>
public class EmailMagicLinkToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? RequestedPlayerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
    public DateTime? ConsumedAt { get; set; }
}

/// <summary>
/// Phase J Wave 8 — server-side auth session record. Opaque bearer-like
/// token stored in the <c>mahjong_auth</c> cookie. Revocation is a row
/// delete; expiry is enforced by <see cref="ExpiresAt"/>.
/// </summary>
public class PlayerAuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public Guid IdentityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Phase J Wave 9 — optional role stamp used by the
    /// <c>GET /api/games/{gameId}/audit</c> admin gate. Null = ordinary
    /// player. "admin" = full access to the audit endpoint. Future
    /// roles ("moderator", "tournament-host") are just additional
    /// strings; the column is intentionally open-ended.</summary>
    public string? Role { get; set; }
}

/// <summary>
/// Phase J Wave 9 — opaque, rotating reconnect token. The Wave 4 reconnect
/// flow only needed <c>(gameId, seatIndex, playerId)</c> to resume; in
/// Wave 9 we now also hand the client a fresh one-shot token on every
/// successful <c>ReconnectGame</c> RPC and verify the previous token's
/// row before accepting the next reconnect. The chain of
/// <see cref="RotatedFromTokenId"/> back-pointers forms an append-only
/// audit trail (also surfaced via <see cref="ReconnectAuditEntry"/>).
/// </summary>
public class ReconnectToken
{
    /// <summary>Default TTL applied to a freshly-issued (or freshly-rotated)
    /// reconnect token. Matches the Wave-4 reconnect window so behaviour is
    /// invariant — a player who steps away for &lt;5 minutes still
    /// reconnects, and the rotation just refreshes the window.</summary>
    public const int DefaultTtlMinutes = 5;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(DefaultTtlMinutes);

    /// <summary>Set the moment the token is consumed (one-shot). A second
    /// reconnect attempt with the same token is rejected once this is non-null.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>When non-null, identifies the token row this one was rotated
    /// from — i.e. the previous link in the rotation chain. Forms a singly-
    /// linked list back to the initial mint (<see cref="RotatedFromTokenId"/> = null).</summary>
    public Guid? RotatedFromTokenId { get; set; }
}

/// <summary>
/// Phase J Wave 9 — append-only audit log of reconnect-token rotations.
/// One row per rotation event so a security review can replay the chain
/// without re-deriving it from the <see cref="ReconnectToken"/> table
/// (which can be rotated / pruned without losing the trail). IPv4 and
/// User-Agent are SHA-256 hashed for storage; the raw values are never
/// persisted (privacy by default, but operators can still pivot on a
/// suspected client by re-hashing).
/// </summary>
public class ReconnectAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = string.Empty;
    public Guid OldTokenId { get; set; }
    public Guid NewTokenId { get; set; }

    /// <summary>SHA-256 (hex-lowercase) of the caller's IPv4 / IPv6 address.
    /// Empty string when the address could not be resolved (in-memory test
    /// transports leave <c>HttpContext.Connection.RemoteIpAddress</c> null).</summary>
    public string Ipv4Hash { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex-lowercase) of the inbound <c>User-Agent</c>
    /// header. Empty string when the header is absent.</summary>
    public string UserAgentHash { get; set; } = string.Empty;

    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase J Wave 9 — server-side chat message captured by the hub's
/// <c>SendChat</c> RPC. Persisted so a player rejoining mid-game can
/// lazily back-fill the conversation via the
/// <c>GET /api/games/{gameId}/chat</c> REST endpoint. The
/// <see cref="Channel"/> field encodes the routing decision at send time:
/// <list type="bullet">
///   <item><c>table</c> — broadcast to every connection currently in the
///     game's SignalR group (players + spectators).</item>
///   <item><c>private:&lt;to-playerId&gt;</c> — DM routed to a specific
///     player; the receiver and the sender are both delivered the
///     message so both ends of the conversation render the bubble.</item>
///   <item><c>spectator</c> — visible only to seats whose
///     <c>state.Seats[i].IsBot == false</c> AND whose connection sits in
///     the game group but does not own a seat (i.e. spectator camera).</item>
/// </list>
/// </summary>
public class ChatMessage
{
    /// <summary>Hub-level validation cap on the inbound <see cref="Body"/>.
    /// The persisted column is sized to 512 (see <c>AppDbContext</c>) to
    /// keep room for future emoji-padded payloads without a schema bump,
    /// but the hub rejects anything over <see cref="MaxBodyLength"/> at
    /// send time.</summary>
    public const int MaxBodyLength = 280;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GameId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Channel { get; set; } = "table";
}

/// <summary>
/// Phase J Wave 9 — Content-Security-Policy violation report (Apone, DevOps).
///
/// <para>Persisted by <c>POST /api/csp-report</c> for every browser-reported
/// CSP violation. Schema mirrors the canonical <c>application/csp-report</c>
/// (legacy) and <c>application/reports+json</c> (Reporting API) envelopes;
/// fields are unbounded text because user agents disagree on which keys are
/// present (Chromium ships every directive; Firefox sometimes elides
/// <c>script-sample</c>). All columns are nullable so the endpoint never
/// drops a malformed-but-parseable report.</para>
///
/// <para>No FK to <c>PlayerProfiles</c>: reports may arrive from anonymous
/// callers (the public landing page) before any cookie is set. The
/// <see cref="PlayerId"/> column is a best-effort capture of the
/// <c>mahjong_pid</c> cookie at report time.</para>
/// </summary>
public class CspViolation
{
    public long Id { get; set; }

    /// <summary>Best-effort capture of the <c>mahjong_pid</c> cookie at
    /// report time. Null when the caller is fully anonymous.</summary>
    public string? PlayerId { get; set; }

    /// <summary>URL of the document the violation occurred on.</summary>
    public string? DocumentUri { get; set; }

    /// <summary>Origin or page that referred the violating resource.</summary>
    public string? Referrer { get; set; }

    /// <summary>The full effective directive name (e.g. <c>script-src-elem</c>).</summary>
    public string? ViolatedDirective { get; set; }

    /// <summary>Effective parent directive (e.g. <c>script-src</c>).</summary>
    public string? EffectiveDirective { get; set; }

    /// <summary>The original policy header that produced this violation.</summary>
    public string? OriginalPolicy { get; set; }

    /// <summary>Disposition: <c>enforce</c> or <c>report</c>.</summary>
    public string? Disposition { get; set; }

    /// <summary>The URI that was blocked (resource URL or <c>inline</c>/<c>eval</c>).</summary>
    public string? BlockedUri { get; set; }

    /// <summary>Optional source-file URL for inline / eval violations.</summary>
    public string? SourceFile { get; set; }

    /// <summary>Optional line + column position when reported by the UA.</summary>
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }

    /// <summary>Optional 40-char sample of the offending script. Truncated server-side.</summary>
    public string? ScriptSample { get; set; }

    /// <summary>HTTP status code the user agent saw when serving the document.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Caller's User-Agent header.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Raw JSON envelope, retained for forensics even when parsing
    /// extracts the canonical fields above.</summary>
    public string RawJson { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
