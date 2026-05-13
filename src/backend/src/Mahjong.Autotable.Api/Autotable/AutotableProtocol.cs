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
