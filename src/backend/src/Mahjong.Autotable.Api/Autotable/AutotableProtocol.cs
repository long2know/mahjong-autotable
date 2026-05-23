using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mahjong.Autotable.Api.Autotable;

/// <summary>
/// Envelope record types for the upstream pwmarcz/autotable WebSocket protocol.
/// Mirrors <c>server/protocol.ts</c> from upstream so the byte-identical
/// <c>autotable.9519e86d.js</c> bundle connects unchanged.
/// </summary>
public enum AutotableMessageKind
{
    NEW,
    JOIN,
    JOINED,
    UPDATE
}

/// <summary>
/// A single collection mutation. Serialized as the three-element JSON array
/// <c>[kind, key, value]</c> per upstream <c>protocol.ts</c>.
/// <para>Examples:</para>
/// <list type="bullet">
/// <item><c>["things", 42, { slotName: "hand.0@0", ... }]</c></item>
/// <item><c>["match", 0, { dealer: 0, honba: 0, conditions: { ... } }]</c></item>
/// <item><c>["dice", 0, { dice: [3, 4], state: "rolled" }]</c></item>
/// </list>
/// </summary>
[JsonConverter(typeof(CollectionEntryJsonConverter))]
public sealed record CollectionEntry(string Kind, object Key, object? Value);

/// <summary>Inbound envelope from the bundle. Discriminated by <see cref="Type"/>.</summary>
public sealed class AutotableInboundMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("gameId")]
    public string? GameId { get; set; }

    [JsonPropertyName("entries")]
    public List<CollectionEntry>? Entries { get; set; }

    [JsonPropertyName("full")]
    public bool? Full { get; set; }
}

/// <summary>
/// Changsha-specific collection kinds layered on top of the upstream autotable protocol.
/// These reuse the same <c>UPDATE { entries: [[kind, key, value], …] }</c> envelope —
/// no new message types — and live alongside upstream collections (<c>things</c>,
/// <c>seats</c>, <c>nicks</c>, <c>match</c>, <c>mouse</c>, <c>sound</c>, <c>dice</c>).
///
/// <list type="bullet">
///   <item><b><c>claim</c></b> (Bishop Phase D-backend §2): keyed by seat index (0..3),
///   value = <c>{ available: ["Pung","Chow","Kong","Hu"], deadline: long, source: int,
///   tile: int }</c>. Server-emitted when a discard opens a claim window for that seat;
///   client-emitted when a player presses a claim button (or "pass"). Cleared with a
///   tombstone (value=<c>null</c>) when the window closes.</item>
///
///   <item><b><c>result</c></b> (Bishop Phase D-backend §2): keyed by the literal string
///   <c>"current"</c>, value = <c>{ winner: int, type: "Hu"|"Draw"|"ZhaHu",
///   score: { seat: points }[], hand: int[], nextBanker: int }</c>. Server-emitted on
///   hand end (Hu, washout, or false-Hu penalty). Used by the autotable scene to drive
///   the result panel + banker arrow rotation. Lives until the next deal clears it.</item>
///
///   <item><b><c>pickup</c></b> (Bishop Phase F §2): keyed by the literal string
///   <c>"current"</c>, value = <c>{ phase: string, seatIndex: int, count: int,
///   dealMode: "auto"|"manual", breakPoint: {wallIndex, stackIndex, tileIndex},
///   wallIndex: int }</c>. Server-emitted whenever the active pickup phase or cursor
///   advances (BreakPointMarked, PickupRound1..3, SingleTilePickup, DealerExtra).
///   Tombstoned (value=<c>null</c>) when phase transitions to <c>AwaitingDiscard</c>
///   (manual deal complete). Drives the autotable scene's "Take Tiles" affordance and
///   bot pickup animation.</item>
/// </list>
/// </summary>
public static class ChangshaCollectionKinds
{
    public const string Claim = "claim";
    public const string Result = "result";
    public const string Pickup = "pickup";
}

/// <summary>
/// Server-emitted snapshot of an open claim window for one seat. Maps to the
/// <see cref="ChangshaCollectionKinds.Claim"/> collection value.
/// </summary>
public sealed class ClaimWindowEntry
{
    [JsonPropertyName("available")]
    public List<string> Available { get; set; } = [];

    /// <summary>UTC unix millis at which auto-pass kicks in.</summary>
    [JsonPropertyName("deadline")]
    public long DeadlineUnixMs { get; set; }

    /// <summary>Seat that discarded the tile triggering the window.</summary>
    [JsonPropertyName("source")]
    public int SourceSeat { get; set; }

    /// <summary>Changsha tile id of the discarded tile.</summary>
    [JsonPropertyName("tile")]
    public int TileId { get; set; }
}

/// <summary>
/// Server-emitted hand result. Maps to the <see cref="ChangshaCollectionKinds.Result"/>
/// collection value under key <c>"current"</c>.
/// </summary>
public sealed class HandResultEntry
{
    [JsonPropertyName("winner")]
    public int Winner { get; set; }

    /// <summary>One of <c>"Hu"</c>, <c>"Draw"</c>, <c>"ZhaHu"</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Cumulative net payments per seat for this hand (positive = gained).</summary>
    [JsonPropertyName("score")]
    public Dictionary<int, int> Score { get; set; } = new();

    /// <summary>Tile ids in the winning hand (concealed + meld) for the result panel.</summary>
    [JsonPropertyName("hand")]
    public List<int> Hand { get; set; } = [];

    /// <summary>Banker seat for the next hand per Changsha v1.2 rotation.</summary>
    [JsonPropertyName("nextBanker")]
    public int NextBanker { get; set; }

    /// <summary>
    /// Phase I Wave 1 — nested win metadata mirroring the SignalR <c>WinDeclared</c>
    /// shape so the frontend result modal (chips, RobbingKong badge) can render
    /// without a second WS subscription. Null when the hand ended on draw/false-Hu.
    /// </summary>
    [JsonPropertyName("winResult")]
    public WinResultEntry? WinResult { get; set; }

    /// <summary>
    /// Phase I Wave 1 — nested scoring metadata (category, base points, multiplier,
    /// payments) so the result modal can render the multiplier breakdown
    /// (<c>base × patterns = total</c>) without an additional round-trip.
    /// Null when the hand ended on draw/false-Hu.
    /// </summary>
    [JsonPropertyName("scoreResult")]
    public ScoreResultEntry? ScoreResult { get; set; }
}

/// <summary>
/// Phase I Wave 1 — nested win metadata carried inside <see cref="HandResultEntry"/>.
/// Mirrors the SignalR <c>WinDeclared</c> message shape so the frontend result-modal
/// chip strip + RobbingKong badge work uniformly across both transports.
/// </summary>
public sealed class WinResultEntry
{
    [JsonPropertyName("winningSeatIndex")]
    public int WinningSeatIndex { get; set; }

    /// <summary>One of <c>"selfDraw"</c>, <c>"discard"</c>, <c>"robbingKong"</c>.</summary>
    [JsonPropertyName("winType")]
    public string WinType { get; set; } = string.Empty;

    /// <summary>Highest-precedence pattern (camelCase, e.g. <c>"sevenPairs"</c>).</summary>
    [JsonPropertyName("winPattern")]
    public string WinPattern { get; set; } = string.Empty;

    [JsonPropertyName("winningTileId")]
    public int WinningTileId { get; set; }

    [JsonPropertyName("sourceSeatIndex")]
    public int SourceSeatIndex { get; set; }

    /// <summary>Every Big Win pattern that fired (deterministic enum order).</summary>
    [JsonPropertyName("allPatterns")]
    public List<string> AllPatterns { get; set; } = [];

    /// <summary>True when this hand won by 抢杠胡 (robbing the added kong).</summary>
    [JsonPropertyName("isRobbedKong")]
    public bool IsRobbedKong { get; set; }

    /// <summary>
    /// Phase J Wave 3 — true when the winning tile arrived via a draw from the wall
    /// (regular OR kong-replacement) rather than by claiming another seat's discard.
    /// Mirrors <see cref="WinResult.IsSelfDraw"/>. Provided so the frontend banner /
    /// result-modal copy can distinguish self-draw from ron without parsing
    /// <see cref="WinType"/>.
    /// </summary>
    [JsonPropertyName("isSelfDraw")]
    public bool IsSelfDraw { get; set; }

    /// <summary>
    /// Phase J Wave 3 — true when the winning tile was drawn as a kong replacement
    /// (杠上开花). Mirrors <see cref="WinResult.IsKongReplacement"/>. Backward-compat:
    /// the corresponding <c>kongReplacementWin</c> entry is still emitted inside
    /// <see cref="AllPatterns"/>, so legacy clients that scan the pattern list keep
    /// working unchanged.
    /// </summary>
    [JsonPropertyName("isKongReplacement")]
    public bool IsKongReplacement { get; set; }
}

/// <summary>
/// Phase I Wave 1 — nested scoring metadata carried inside <see cref="HandResultEntry"/>.
/// Mirrors the SignalR <c>HandFinished</c> message scoreResult shape.
/// </summary>
public sealed class ScoreResultEntry
{
    /// <summary>One of <c>"smallWin"</c>, <c>"bigWin"</c>.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("basePoints")]
    public int BasePoints { get; set; }

    [JsonPropertyName("payments")]
    public List<ScorePaymentEntry> Payments { get; set; } = [];
}

public sealed class ScorePaymentEntry
{
    [JsonPropertyName("fromSeatIndex")]
    public int FromSeatIndex { get; set; }

    [JsonPropertyName("toSeatIndex")]
    public int ToSeatIndex { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Helpers for shaping outbound Changsha-collection entries.</summary>
public static class ChangshaCollectionEncoder
{
    /// <summary>
    /// Encodes a claim window for <paramref name="seat"/> as the <c>claim</c> collection
    /// entry the bundle expects. Caller decides timeout policy.
    /// </summary>
    public static CollectionEntry EncodeClaimWindow(
        int seat,
        IEnumerable<string> available,
        int sourceSeat,
        int tileId,
        long deadlineUnixMs)
        => new(ChangshaCollectionKinds.Claim, seat, new ClaimWindowEntry
        {
            Available = available.ToList(),
            DeadlineUnixMs = deadlineUnixMs,
            SourceSeat = sourceSeat,
            TileId = tileId
        });

    /// <summary>Encodes a tombstone for <paramref name="seat"/>'s claim window (window closed).</summary>
    public static CollectionEntry EncodeClaimWindowClosed(int seat)
        => new(ChangshaCollectionKinds.Claim, seat, null);

    /// <summary>Encodes the current hand's result as the <c>result["current"]</c> entry.</summary>
    public static CollectionEntry EncodeHandResult(HandResultEntry result)
        => new(ChangshaCollectionKinds.Result, "current", result);

    /// <summary>Encodes a tombstone for the current hand result (cleared on next deal).</summary>
    public static CollectionEntry EncodeHandResultCleared()
        => new(ChangshaCollectionKinds.Result, "current", null);

    // ── Phase F: pickup-state collection ──

    /// <summary>
    /// Encodes the current pickup cursor as the <c>pickup["current"]</c> entry. Emitted
    /// whenever the pickup phase or active seat changes during a Manual deal. Drives the
    /// autotable scene's "Take Tiles" button visibility per seat. Caller is expected to
    /// derive <paramref name="count"/> from <c>ChangshaGameStateMachine.ExpectedPickupCount(phase)</c>.
    /// </summary>
    public static CollectionEntry EncodePickup(PickupEntry pickup)
        => new(ChangshaCollectionKinds.Pickup, "current", pickup);

    /// <summary>Encodes a tombstone for the pickup cursor (manual deal complete).</summary>
    public static CollectionEntry EncodePickupCleared()
        => new(ChangshaCollectionKinds.Pickup, "current", null);
}

/// <summary>
/// Server-emitted snapshot of the pickup cursor while a Manual deal is in progress.
/// Maps to the <see cref="ChangshaCollectionKinds.Pickup"/> collection value under
/// key <c>"current"</c>.
/// </summary>
public sealed class PickupEntry
{
    /// <summary>One of <c>BreakPointMarked</c>, <c>PickupRound1</c>, <c>PickupRound2</c>,
    /// <c>PickupRound3</c>, <c>SingleTilePickup</c>, or <c>DealerExtra</c>.</summary>
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    /// <summary>Seat whose turn it is to take tiles from the wall (0..3).</summary>
    [JsonPropertyName("seatIndex")]
    public int SeatIndex { get; set; }

    /// <summary>Number of tiles the active seat should take from the wall front.
    /// 4 during PickupRound1..3, 1 during SingleTilePickup and DealerExtra.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>One of <c>"auto"</c>, <c>"manual"</c>. Echoed from
    /// <c>ChangshaGameState.DealMode</c> so the bundle can render the same cursor
    /// in both modes (auto-deal mode tombstones the entry immediately).</summary>
    [JsonPropertyName("dealMode")]
    public string DealMode { get; set; } = "manual";

    /// <summary>Break-point address (wall index / stack index / tile index within stack).
    /// Computed by <see cref="BreakPointService.ComputeBreakPoint"/> from the dice roll.
    /// Sent so the bundle can render the dealer's break point marker between rounds.</summary>
    [JsonPropertyName("breakPoint")]
    public BreakPointWire? BreakPoint { get; set; }

    /// <summary>Current zero-based offset into <c>ChangshaGameState.Wall</c> where the
    /// next pickup will draw from. Lets the bundle preview which slots will be removed
    /// next without re-deriving from <c>BreakPoint</c>.</summary>
    [JsonPropertyName("wallIndex")]
    public int WallIndex { get; set; }
}

/// <summary>JSON-friendly mirror of <c>Changsha.BreakPointResult</c> (record struct).
/// Lives in this protocol-layer file so the autotable WS contract is fully described
/// alongside the rest of the wire format.</summary>
public sealed class BreakPointWire
{
    [JsonPropertyName("wallIndex")]
    public int WallIndex { get; set; }

    [JsonPropertyName("stackIndex")]
    public int StackIndex { get; set; }

    [JsonPropertyName("tileIndex")]
    public int TileIndex { get; set; }
}

/// <summary>Outbound <c>JOINED</c> envelope.</summary>
public sealed class JoinedMessage
{
    [JsonPropertyName("type")]
    public string Type { get; } = "JOINED";

    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("isFirst")]
    public bool IsFirst { get; set; }
}

/// <summary>Outbound <c>UPDATE</c> envelope (full or incremental).</summary>
public sealed class UpdateMessage
{
    [JsonPropertyName("type")]
    public string Type { get; } = "UPDATE";

    [JsonPropertyName("entries")]
    public List<CollectionEntry> Entries { get; set; } = [];

    [JsonPropertyName("full")]
    public bool Full { get; set; }
}

/// <summary>Shared JSON options for the autotable protocol. CamelCase matches the rest of the API.</summary>
public static class AutotableJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Serializes <see cref="CollectionEntry"/> as the three-element array tuple
/// upstream expects (<c>[kind, key, value]</c>) and parses incoming arrays of the same shape.
/// </summary>
internal sealed class CollectionEntryJsonConverter : JsonConverter<CollectionEntry>
{
    public override CollectionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected JSON array for CollectionEntry.");

        reader.Read();
        var kind = reader.GetString() ?? throw new JsonException("CollectionEntry.kind must be a string.");

        reader.Read();
        object key = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            _ => throw new JsonException("CollectionEntry.key must be string or number.")
        };

        reader.Read();
        object? value;
        if (reader.TokenType == JsonTokenType.Null)
        {
            value = null;
        }
        else
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            value = doc.RootElement.Clone();
        }

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected end of CollectionEntry array.");

        return new CollectionEntry(kind, key, value);
    }

    public override void Write(Utf8JsonWriter writer, CollectionEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Kind);

        switch (value.Key)
        {
            case string s: writer.WriteStringValue(s); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case double d: writer.WriteNumberValue(d); break;
            default: JsonSerializer.Serialize(writer, value.Key, value.Key.GetType(), options); break;
        }

        if (value.Value is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Value, value.Value.GetType(), options);

        writer.WriteEndArray();
    }
}
