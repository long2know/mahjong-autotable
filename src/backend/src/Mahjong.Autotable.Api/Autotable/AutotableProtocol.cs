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
/// </list>
/// </summary>
public static class ChangshaCollectionKinds
{
    public const string Claim = "claim";
    public const string Result = "result";
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
