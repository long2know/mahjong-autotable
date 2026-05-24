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

    /// <summary>
    /// Phase K Wave 3 — Bishop. Persistent owner-of-the-table id, mirrored
    /// from <see cref="Mahjong.Autotable.Api.Changsha.ChangshaGameState.CreatorPlayerId"/>
    /// at game creation. Stored as a column so REST endpoints
    /// (<c>POST /api/games/{id}/settings/voice</c>) can gate creator-only
    /// mutations without spinning the runtime's in-memory state up — and
    /// the value survives a process restart even if the live runtime
    /// hasn't yet rehydrated the game.
    /// </summary>
    public string? OwnerPlayerId { get; set; }

    /// <summary>
    /// Phase K Wave 3 — Bishop. Per-table voice-chat toggle. Off by
    /// default (matches <c>VoiceOptions.Enabled</c> global default);
    /// the table creator flips it via
    /// <c>POST /api/games/{id}/settings/voice { "enabled": true }</c>.
    /// The <see cref="Mahjong.Autotable.Api.Voice.VoiceHub"/> rejects
    /// <c>JoinVoice(tableId)</c> when this flag is false so the
    /// signalling surface stays opt-in even when the global voice
    /// feature is enabled at the deployment level.
    /// </summary>
    public bool VoiceEnabled { get; set; } = false;
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
///
/// <para>Phase K Wave 2 — generalised into a multi-purpose audit row
/// via <see cref="Kind"/>. The column carries a stable dotted classifier
/// so operators can filter the trail by event class without joining
/// other tables. Vasquez's contract tests pin two values:
/// <list type="bullet">
///   <item><see cref="KindReconnectTokenRotated"/> — original Wave-9
///         use; default value for existing rows + the rotation service.</item>
///   <item><see cref="KindTournamentForfeit"/> — auto-forfeit emitted by
///         <see cref="Mahjong.Autotable.Api.Tournament.TournamentForfeitService"/>.</item>
///   <item><see cref="KindTournamentMatchComplete"/> — regular completion
///         emitted by <see cref="Mahjong.Autotable.Api.Tournament.TournamentService"/>.</item>
/// </list>
/// The column is additive (default-value-stamped on migration) so
/// historical rows still round-trip without a backfill job.</para>
/// </summary>
public class ReconnectAuditEntry
{
    /// <summary>Phase K Wave 2 — default Kind for the original Wave-9
    /// rotation rows. Stamps every row written by the reconnect-token
    /// rotation path so the trail keeps a stable classifier even pre-K2.</summary>
    public const string KindReconnectTokenRotated = "reconnect.token.rotated";

    /// <summary>Phase K Wave 2 — auto-forfeit emitted when a player's
    /// disconnect exceeds the tournament grace window. Vasquez's
    /// contract pins this exact value.</summary>
    public const string KindTournamentForfeit = "tournament.forfeit";

    /// <summary>Phase K Wave 2 — regular tournament-match completion
    /// (non-forfeit). Lets operators filter the trail to "games that
    /// finished cleanly" without joining the tournament tables.</summary>
    public const string KindTournamentMatchComplete = "tournament.match.complete";

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

    /// <summary>
    /// Phase K Wave 2 — stable dotted event classifier. Defaults to
    /// <see cref="KindReconnectTokenRotated"/> so existing call sites
    /// keep their Wave-9 semantic on migration. Tournament-side writers
    /// pin <see cref="KindTournamentForfeit"/> / <see cref="KindTournamentMatchComplete"/>.
    /// </summary>
    public string Kind { get; set; } = KindReconnectTokenRotated;

    /// <summary>Phase K Wave 2 — voice signalling: peer joined a voice room.</summary>
    public const string KindVoiceJoin = "voice.join";

    /// <summary>Phase K Wave 2 — voice signalling: peer left a voice room.</summary>
    public const string KindVoiceLeave = "voice.leave";

    /// <summary>Phase K Wave 3 — TURN short-term credential mint.</summary>
    public const string KindTurnCredentialsMinted = "voice.turn.credentials.minted";

    /// <summary>
    /// Phase K Wave 4 — Bishop. Prefix for the per-key JWT issuance
    /// audit Kind. The full classifier appends the key's index in
    /// <c>AuthOptions.JwtSigningKeys</c> (e.g.
    /// <c>auth.jwt.signed.with_key.0</c> when the active signer is at
    /// position 0). <see cref="Detail"/> carries the deterministic
    /// <c>kid</c> so operators can reconcile rotations.
    /// </summary>
    public const string KindAuthJwtSignedPrefix = "auth.jwt.signed.with_key.";

    /// <summary>Phase K Wave 4 — Bishop. Tournament admin reseed audit row.</summary>
    public const string KindTournamentSeeded = "tournament.seeded";

    /// <summary>Phase K Wave 6 — Bishop. HLS livestream recording
    /// started for a table by an admin / dealer. <see cref="PlayerId"/>
    /// records the caller; <see cref="Detail"/> carries the table /
    /// game id (Guid "N" form) so the trail joins back to the
    /// game row without a separate lookup.</summary>
    public const string KindVoiceLivestreamStart = "voice.livestream.start";

    /// <summary>Phase K Wave 6 — Bishop. HLS livestream recording
    /// stopped for a table. Paired with <see cref="KindVoiceLivestreamStart"/>;
    /// the trail records both lifecycle transitions so operators can
    /// reconcile recording duration without joining to the encoder
    /// state.</summary>
    public const string KindVoiceLivestreamStop = "voice.livestream.stop";

    /// <summary>Phase K Wave 6 — Bishop. AI commentary generation
    /// triggered for a completed game by an admin caller. The Wave-6
    /// surface ships the stub generator; the real LLM-driven
    /// commentary lands in Phase L behind the same audit Kind so
    /// operator dashboards stay aligned.</summary>
    public const string KindCommentaryReplayRequested = "commentary.replay.requested";

    /// <summary>Phase K Wave 2 — free-form classifier payload (tournament
    /// round number, forfeit reason, voice tableId). Nullable so existing
    /// Wave-9 rows backfill clean.</summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Phase K Wave 8 — Bishop. Client-supplied idempotency key (lifted
    /// from the inbound <c>Idempotency-Key</c> HTTP header) so an
    /// operator tracing a duplicate-POST replay can correlate the
    /// rejected retry to the original audit row without joining via
    /// HTTP logs. Nullable — pre-Wave-8 rows + non-POST audited events
    /// (voice signalling, tournament forfeits) leave the column null
    /// and the migration default-stamps it accordingly.
    /// <para>Max 128 chars matches the
    /// <see cref="Mahjong.Autotable.Api.Audit.IdempotencyMiddleware.MaxKeyLength"/>
    /// header validation cap.</para>
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Phase K Wave 8 — Bishop. Server-generated correlation id that
    /// ties every audit row for a single request (REST + downstream
    /// SignalR + HTTP outbound) under one searchable key. Stamped by
    /// <see cref="Mahjong.Autotable.Api.Audit.CorrelationIdMiddleware"/>
    /// on the inbound request and propagated downstream via the
    /// <c>X-Correlation-Id</c> response header so clients can re-emit
    /// it on retries.
    /// <para>Format: Guid.ToString("N"). 32 chars. Nullable for
    /// pre-Wave-8 rows; the W8 middleware always stamps it on new
    /// rows.</para>
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>Phase K Wave 8 — Bishop. Audit Kind for idempotency
    /// rejections so operators can spot replay floods at a glance.</summary>
    public const string KindIdempotencyReplayRejected = "audit.idempotency.replay.rejected";

    /// <summary>Phase K Wave 8 — Bishop. Audit Kind stamped by the
    /// commentary-generator surface when the LLM fail-open path
    /// engages (provider error → "[commentary unavailable]" record).</summary>
    public const string KindCommentaryLlmFailOpen = "commentary.llm.fail_open";

    /// <summary>Phase K Wave 8 — Bishop. Audit Kind stamped when the
    /// HLS playlist gate denies an anonymous caller (401).</summary>
    public const string KindLivestreamPlaylistUnauthorized = "voice.livestream.playlist.unauthorized";

    /// <summary>Phase K Wave 8 — Bishop. Audit Kind stamped when the
    /// HLS playlist gate denies a non-associated caller (403 — neither
    /// seated nor spectator on the table).</summary>
    public const string KindLivestreamPlaylistForbidden = "voice.livestream.playlist.forbidden";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind stamped when
    /// <see cref="Mahjong.Autotable.Api.Auth.JwtIssuingService.IssueForTenantAsync"/>
    /// blocks token issuance because the per-tenant JWKS rotation
    /// policy has aged past the configured overlap window. The
    /// <see cref="Detail"/> field carries the tenant id so the
    /// trail is searchable by customer. Paired with the W17
    /// <c>jwt_issue_blocked_total{reason="stale_per_tenant_policy"}</c>
    /// Prometheus counter for operator dashboards.</summary>
    public const string KindAuthJwtIssueBlockedStale = "auth.jwt.issue.blocked.stale_per_tenant_policy";

    /// <summary>Phase K Wave 17 — Bishop. Hard-delete companion to
    /// the W16 <c>auth.jwks.per-tenant.deleted</c> soft-delete
    /// kind. Stamped by
    /// <c>PerTenantRotationAdminController.Delete</c> when the
    /// admin caller drops a per-tenant rotation row via the
    /// W17 <see cref="Mahjong.Autotable.Api.Auth.IPerTenantJwksRotationStore.DeleteAsync"/>
    /// path (the W16 sentinel-row workaround is retired).</summary>
    public const string KindAuthJwksPerTenantHardDeleted = "auth.jwks.per-tenant.hard-deleted";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind stamped by
    /// the W17 <c>ReplayRetentionAdminController</c> on a
    /// successful POST. <see cref="Detail"/> carries the tenant
    /// id + the operator-supplied <c>X-Admin-Reason</c> header
    /// joined by <c>"|"</c>.</summary>
    public const string KindReplayRetentionCreated = "replays.retention.created";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind for an
    /// admin-driven update of a per-tenant replay retention row.</summary>
    public const string KindReplayRetentionUpdated = "replays.retention.updated";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind for an
    /// admin-driven delete of a per-tenant replay retention row.</summary>
    public const string KindReplayRetentionDeleted = "replays.retention.deleted";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind stamped by
    /// <c>CommentaryController.Trigger</c> when an admin caller
    /// engages the <c>X-Admin-Reason</c> header to bypass the
    /// 402 commentary cost-budget gate. <see cref="Detail"/>
    /// carries the operator-supplied reason verbatim so the
    /// audit dashboard renders WHY the override was used.</summary>
    public const string KindCommentaryAdminOverride = "commentary.admin.override";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind stamped by
    /// the W17 <c>SignalRRetentionAdminController</c> on a
    /// successful POST.</summary>
    public const string KindSignalRRetentionCreated = "signalr.retention.created";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind for an
    /// admin-driven update of a per-tenant SignalR retention row.</summary>
    public const string KindSignalRRetentionUpdated = "signalr.retention.updated";

    /// <summary>Phase K Wave 17 — Bishop. Audit Kind for an
    /// admin-driven delete of a per-tenant SignalR retention row.</summary>
    public const string KindSignalRRetentionDeleted = "signalr.retention.deleted";

    /// <summary>Phase K Wave 18 — Bishop. Audit Kind stamped by
    /// the W18 <c>SignalRRetentionCeilingAdminController</c> on a
    /// successful grant / revoke of an above-ceiling override for
    /// a tenant. Detail format:
    /// <c>"tenant={tenant}|action={grant|revoke}|reason={X-Admin-Reason}"</c>.</summary>
    public const string KindSignalRRetentionCeilingOverride = "signalr.retention.ceiling.override";

    /// <summary>Phase K Wave 18 — Bishop. Audit Kind stamped by
    /// the W18 <c>PerTenantRotationPolicyListController</c> on a
    /// successful LIST. Detail format:
    /// <c>"prefix={tenant-prefix}|skip={n}|take={n}|rows={n}"</c>.</summary>
    public const string KindAuthJwksPerTenantListed = "auth.jwks.per-tenant.listed";

    /// <summary>Phase K Wave 18 — Bishop. Audit Kind stamped by
    /// the W18 <c>CommentaryCostBudgetExportController</c> on a
    /// successful CSV export. Detail format:
    /// <c>"from={YYYY-MM}|to={YYYY-MM}|tenant={tenant}|rows={count}"</c>.</summary>
    public const string KindCommentaryCostBudgetExport = "commentary.cost-budget.export";
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

/// <summary>
/// Phase J Wave 10 — Tournament shell. A tournament is a multi-game
/// competitive structure: registrations gate which players can play,
/// the chosen format (single-elimination / round-robin / swiss) drives
/// pairing, and each pairing produces one or more games whose
/// completion advances the tournament. Status is a 4-state machine:
/// <c>draft</c> (created, not yet open for registration) →
/// <c>open</c> (accepting registrations) →
/// <c>in-progress</c> (started; pairings active) →
/// <c>complete</c> (final ranking known). The creator owns transitions
/// and is the only player who can call <c>start</c>.
/// </summary>
public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = "round-robin"; // single-elimination | round-robin | swiss
    public string Status { get; set; } = "draft";       // draft | open | in-progress | complete
    public string CreatedByPlayerId { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 16;
    public int GamesPerMatch { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Phase J Wave 10 — per-player registration row for a tournament.
/// <see cref="Seed"/> is the deterministic ordinal used by the pairing
/// algorithms (single-elimination bracket seeding, round-robin slot
/// ordering, Swiss tie-breaker). The unique (TournamentId, PlayerId)
/// index prevents double-registration. Unregistering is permitted
/// only while the tournament status is <c>draft</c> or <c>open</c>.
/// </summary>
public class TournamentRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int Seed { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase J Wave 10 — a single pairing within a tournament. For
/// 4-player formats (round-robin) Player3Id + Player4Id are populated;
/// 2-player formats (single-elim, Swiss) leave them null. The
/// <see cref="GameIdsJson"/> column is a serialised <c>List&lt;Guid&gt;</c>
/// — each entry is a <see cref="ChangshaGame.Id"/> that resolves the
/// match. Wave 10 (<c>GamesPerMatch=1</c>) only ever populates one game,
/// but the JSON-column shape is future-proof for the eventual best-of-N
/// extension without a schema bump.
/// </summary>
public class TournamentMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public int Round { get; set; } = 1;
    public string Player1Id { get; set; } = string.Empty;
    public string Player2Id { get; set; } = string.Empty;
    public string? Player3Id { get; set; }
    public string? Player4Id { get; set; }
    public string? WinnerPlayerId { get; set; }
    public string GameIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "pending"; // pending | in-progress | complete
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Phase K Wave 1 — opposing-seat auto-forfeit lifecycle. Set by
    /// <see cref="Mahjong.Autotable.Api.Tournament.TournamentForfeitService"/>
    /// when the match was decided by a disconnect timeout rather than a
    /// game-completion event. Audited via <c>ReconnectAuditEntries</c>
    /// with the synthetic <c>tournament-forfeit</c> source so the trail
    /// stays append-only in the existing audit surface.
    /// </summary>
    public bool ForfeitedByDisconnect { get; set; }

    /// <summary>
    /// Phase K Wave 1 — player whose drop triggered the forfeit. Null
    /// when <see cref="ForfeitedByDisconnect"/> is false.
    /// </summary>
    public string? ForfeitedPlayerId { get; set; }
}

/// <summary>
/// Phase K Wave 1 — denormalized per-player participation row written
/// at game completion. Powers the public <c>GET /api/games?playerId=…</c>
/// match-history surface without forcing a JSON scan of <see cref="ChangshaGame.StateJson"/>.
/// One row per (player, game). <see cref="OpponentPlayerIdsCsv"/> is a
/// comma-joined snapshot of the OTHER seats' PlayerIds (in canonical
/// seat order, bots filtered) so the CSV export shape is self-contained.
/// </summary>
public sealed class PlayerGameHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = string.Empty;
    public Guid GameId { get; set; }
    public int SeatIndex { get; set; }
    public int FinalScore { get; set; }
    public bool Won { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Comma-joined opponent PlayerIds (other seats' persistent
    /// identities, bots filtered, ordered by seat ascending). Empty when
    /// no human opponents played.</summary>
    public string OpponentPlayerIdsCsv { get; set; } = string.Empty;

    /// <summary>Optional rule preset pinned at game creation
    /// (mirrors <see cref="ChangshaGame.RulePresetId"/>). Null when the
    /// game ran on the runtime default.</summary>
    public Guid? RulePresetId { get; set; }
}

/// <summary>
/// Phase K Wave 1 — Elo-style competitive rating, one row per
/// (player, season). Updated on tournament-match completion by
/// <see cref="Mahjong.Autotable.Api.Tournament.PlayerRatingService"/>.
/// Cross-season rollover snapshots prior-season rows into
/// <see cref="PlayerRatingHistory"/> and resets the row to
/// <c>Rating:DefaultElo</c>.
/// </summary>
public sealed class PlayerRating
{
    /// <summary>Baseline Elo applied to brand-new players (and on every
    /// seasonal reset). Overridable via <c>Rating:DefaultElo</c>; the
    /// constant pins the canonical default the service falls back to.</summary>
    public const int DefaultElo = 1200;

    /// <summary>K-factor for the standard Elo update rule.</summary>
    public const int KFactor = 32;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Canonical season code (e.g. <c>2026-Q1</c>). See
    /// <see cref="Mahjong.Autotable.Api.Tournament.PlayerRatingService.SeasonFromDate"/>
    /// for the derivation.</summary>
    public string Season { get; set; } = string.Empty;

    public int EloRating { get; set; } = DefaultElo;
    public int GamesPlayed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase K Wave 1 — frozen prior-season snapshot of
/// <see cref="PlayerRating"/>. Written once per (player, season) by
/// <see cref="Mahjong.Autotable.Api.Tournament.SeasonRolloverService"/>
/// at the season boundary so a leaderboard query for a closed season
/// returns the canonical end-of-season ranking even after the live
/// table has been reset.
/// </summary>
public sealed class PlayerRatingHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public int EloRating { get; set; }
    public int GamesPlayed { get; set; }
    public DateTime FrozenAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase K Wave 2 — deferred season-rollover record. Persisted by
/// <see cref="Mahjong.Autotable.Api.Tournament.SeasonRolloverService"/>
/// when the quarter boundary lands while a player is mid-tournament.
/// One row per (PlayerId, FromSeasonId, TournamentId); the rollover
/// service waits for the tournament to flip to <c>complete</c> before
/// draining the deferral and applying the rating snapshot. Keeps the
/// player's competitive identity stable for the duration of the
/// in-flight bracket without freezing the rest of the leaderboard.
///
/// <para>Phase K Wave 3 — Bishop renamed the season fields to the
/// canonical <c>FromSeasonId</c> / <c>ToSeasonId</c> / <c>ResolvedAtUtc</c>
/// shape (Vasquez's Wave-2 contract-gap memo, fix #5). The migration
/// pins the rename so the schema matches the soft-pass contract probes
/// that look for "Season" / "Resolved" markers in column names.</para>
/// </summary>
public sealed class PlayerSeasonRolloverDeferral
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Player whose rollover is deferred.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Season the player was in when the boundary fell.
    /// Canonical <c>YYYY-Qn</c> code from
    /// <see cref="Mahjong.Autotable.Api.Tournament.PlayerRatingService.SeasonFromDate"/>.</summary>
    public string FromSeasonId { get; set; } = string.Empty;

    /// <summary>Season the boundary advanced TO. Same canonical code.</summary>
    public string ToSeasonId { get; set; } = string.Empty;

    /// <summary>UTC instant the deferral was recorded.</summary>
    public DateTime DeferredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Tournament that pinned the player to the prior season.
    /// The rollover service drains the deferral when this tournament's
    /// <see cref="Tournament.Status"/> flips to <c>complete</c>.</summary>
    public Guid TournamentId { get; set; }

    /// <summary>UTC instant the deferral was drained + the rating
    /// snapshot applied. Null while the deferral is still pending.</summary>
    public DateTime? ResolvedAtUtc { get; set; }
}

/// <summary>
/// Phase K Wave 3 — Bishop. Server-authoritative onboarding tour
/// progress. Hicks's Wave-2 client mounts a multi-step tour the first
/// time a player lands; this row pins the canonical "how far did they
/// get" so a returning player on a fresh browser/device picks up where
/// they left off rather than re-seeing the splash.
///
/// <para>One row per <see cref="PlayerId"/>. <see cref="Completed"/>
/// flips true once the tour is fully consumed; subsequent POSTs are
/// idempotent (touch <see cref="LastStepCompletedUtc"/> only). The
/// <c>GET /api/players/me/onboarding-status</c> endpoint returns the
/// envelope <c>{ completed, stepsCompleted, lastStepCompletedUtc }</c>;
/// <c>POST</c> persists the supplied delta.</para>
/// </summary>
public sealed class PlayerOnboardingStatus
{
    /// <summary>Persistent player identifier (mahjong_pid cookie value).</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>True once the player has completed every step of the
    /// onboarding tour. Subsequent POSTs leave this true (the tour is
    /// one-shot).</summary>
    public bool Completed { get; set; }

    /// <summary>Number of tour steps the player has confirmed. Climbs
    /// monotonically; a POST with a lower value than the persisted row
    /// is ignored.</summary>
    public int StepsCompleted { get; set; }

    /// <summary>UTC instant of the most recent step-completion POST.
    /// Null when the player has never POSTed (the GET still returns
    /// the default envelope with stepsCompleted=0).</summary>
    public DateTime? LastStepCompletedUtc { get; set; }

    /// <summary>Row creation timestamp; first GET that creates a
    /// default row stamps this.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Row update timestamp; refreshed on every POST.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Phase K Wave 9 — Bishop. Durable per-month LLM token-usage
/// ledger. One row per (PeriodYear, PeriodMonth) tuple. Replaces the
/// W8 in-memory <c>InMemoryCommentaryUsageMeter</c> which lost its
/// counts across replicas and process restarts.
///
/// <para>The <see cref="RowVersion"/> column is the EF Core
/// concurrency token: every increment loads the row, mutates the
/// token totals, and saves under optimistic-concurrency semantics so
/// two API replicas racing to credit the same call don't double-
/// count. On a concurrency conflict the meter retries the read /
/// mutate / save loop up to a small bound — the cap is intentionally
/// finite so a misbehaving caller can't pin a worker thread.</para>
/// </summary>
public sealed class CommentaryUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>UTC year of the period this row covers.</summary>
    public int PeriodYear { get; set; }

    /// <summary>UTC month (1..12) of the period this row covers.</summary>
    public int PeriodMonth { get; set; }

    /// <summary>Cumulative input-prompt token count across every LLM
    /// call recorded under this period.</summary>
    public long InputTokens { get; set; }

    /// <summary>Cumulative completion (output) token count.</summary>
    public long OutputTokens { get; set; }

    /// <summary>Number of LLM calls credited to this period. Useful
    /// for per-request cost telemetry separate from the raw token
    /// totals.</summary>
    public long RequestCount { get; set; }

    /// <summary>Convenience accessor — sum of input + output tokens.</summary>
    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>UTC instant the row was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC instant the row was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>EF Core concurrency token — bumped on every update so
    /// two API replicas can't double-count usage when they race to
    /// increment the same period row. Initialised to a single-byte
    /// sentinel so the NOT NULL column constraint is satisfied on
    /// providers without native rowversion (SQLite).</summary>
    public byte[] RowVersion { get; set; } = new byte[] { 1 };
}

/// <summary>
/// Phase K Wave 9 — Bishop. Durable idempotency-replay ledger.
/// Replaces the in-memory <c>InMemoryIdempotencyStore</c> for the
/// multi-replica production deployment — every replica shares the
/// same row set so a retry that lands on a different pod is still
/// caught.
///
/// <para>The primary key is the client-supplied <see cref="Key"/>;
/// callers re-using a key with a different payload hash get a
/// <c>409 Conflict</c> (payload-mismatch) and the existing row is
/// preserved for forensic comparison. The <see cref="ExpiresAt"/>
/// column drives the W9 5-minute replay window — a background
/// sweeper drops expired rows on a slow cadence, but the
/// middleware also treats an expired row as "not found" defensively
/// so a stale lookup never blocks a fresh request.</para>
/// </summary>
public sealed class IdempotencyEntry
{
    /// <summary>Maximum supported key length — matches the
    /// <see cref="Mahjong.Autotable.Api.Audit.IdempotencyMiddleware.MaxKeyLength"/>
    /// validation cap.</summary>
    public const int MaxKeyLength = 128;

    /// <summary>Maximum response-body length cached per entry. Larger
    /// responses are truncated to avoid the row blowing up — the
    /// idempotency replay is best-effort for big payloads.</summary>
    public const int MaxResponseBodyLength = 64 * 1024;

    /// <summary>Client-supplied idempotency key (Stripe convention).
    /// Primary key — duplicate POSTs share the same row.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex-lowercase) of the request body bytes.
    /// Two calls with the same key but different payload hashes
    /// produce a 409 Conflict.</summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>HTTP status code of the cached response.</summary>
    public int StatusCode { get; set; }

    /// <summary>Cached <c>Content-Type</c> header value. Empty when
    /// the original response carried no body.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Cached response body (UTF-8 string). Truncated at
    /// <see cref="MaxResponseBodyLength"/>; longer payloads emit a
    /// fresh response on each replay rather than blowing the
    /// row.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>UTC instant the row was recorded.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC instant past which the row is treated as expired
    /// (default RecordedAt + 5 minutes per Stripe convention).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>EF Core concurrency token — defends against two
    /// replicas racing to insert the same key under the same window.
    /// Initialised to a single-byte sentinel so the NOT NULL column
    /// constraint is satisfied on providers without native
    /// rowversion (SQLite).</summary>
    public byte[] RowVersion { get; set; } = new byte[] { 1 };
}

/// <summary>
/// Phase K Wave 11 — Bishop. Durable per-record commentary store.
/// W7 introduced <c>CommentaryRecord</c> as an in-memory contract;
/// W11 ships an optional EF-backed persistence implementation
/// behind <c>Commentary:StorageImpl</c>. One row per record; the
/// <see cref="GameId"/> + <see cref="GeneratedAtUtc"/> indexes drive
/// the paginated <c>GET /api/games/{gameId}/commentary?after=…</c>
/// endpoint, and the <see cref="ExpiresAtUtc"/> column powers the
/// retention sweeper that deletes records past the configured
/// retention window.
///
/// <para>The row stores the typed-tile-reference list as the
/// canonical compact JSON shape (one entry per tile reference) so
/// the read path can re-hydrate the
/// <see cref="Mahjong.Autotable.Api.Commentary.TileReference"/>
/// list without an extra parse step. The binary projection
/// (<see cref="Mahjong.Autotable.Api.Commentary.CommentaryRecord.TileReferencesBinary"/>)
/// is re-computed at projection time and not stored — the codec
/// is cheap and storing both forms would diverge under future
/// codec revisions.</para>
/// </summary>
public sealed class CommentaryRecordRow
{
    /// <summary>Surrogate primary key. Guid so multi-replica
    /// inserts don't collide on a sequence.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Game id this record belongs to. Indexed so the
    /// paginated read path can range-scan.</summary>
    public Guid GameId { get; set; }

    /// <summary>1-based turn number inside the game. 0 for pre-deal
    /// commentary; negative values are rejected at the contract
    /// gate (<see cref="Mahjong.Autotable.Api.Commentary.CommentaryRecord"/>).</summary>
    public int TurnNumber { get; set; }

    /// <summary>Canonical phase string —
    /// <c>"draw" | "discard" | "claim" | "win"</c>. Validated
    /// against <see cref="Mahjong.Autotable.Api.Commentary.CommentaryPhases.All"/>
    /// at write time.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Canonical speaker persona —
    /// <c>"play-by-play" | "color" | "analyst"</c>.</summary>
    public string Speaker { get; set; } = string.Empty;

    /// <summary>The utterance text. Plain UTF-8.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>0.0..1.0 inclusive emotion intensity. The contract
    /// gate clamps values outside the range at write time.</summary>
    public double EmotionIntensity { get; set; }

    /// <summary>JSON-serialised list of TileReference entries (one
    /// per mentioned tile). Empty array when the record has no
    /// tile references — never null. The serialised shape matches
    /// the controller's wire projection.</summary>
    public string TileReferencesJson { get; set; } = "[]";

    /// <summary>UTC timestamp the record was generated. Indexed so
    /// the paginated reader can binary-walk by timestamp.</summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp past which the retention sweeper
    /// drops the row. Set at insert-time to
    /// <c>GeneratedAtUtc + RetentionDays</c>. Indexed so the sweep
    /// query is a single index seek.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
