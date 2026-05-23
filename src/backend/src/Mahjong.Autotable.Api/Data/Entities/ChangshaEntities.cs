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
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string EventsJson { get; set; } = string.Empty;
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
}
