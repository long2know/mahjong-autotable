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
