using System.Collections.Concurrent;
using System.Text.Json;

namespace Mahjong.Autotable.Api.Autotable;

/// <summary>
/// Per-game collaborative state store for the autotable WS relay.
///
/// <para><b>Phase C-relay role:</b> the .NET backend acts as a broadcast hub —
/// each bundle mutates its local collections (<c>things</c>, <c>seats</c>,
/// <c>nicks</c>, <c>match</c>, <c>mouse</c>, <c>sound</c>, <c>dice</c>) and ships
/// the delta as an <c>UPDATE</c>. This class stores the latest value of each
/// (kind, key) pair so that a late-joining bundle can replay the full state
/// on <c>JOINED</c>. The relay broadcasts deltas to other connections in the
/// same gameId — see <see cref="AutotableConnectionManager"/>.</para>
///
/// <para><b>Upstream parity (server/game.ts):</b> mirrors upstream's
/// <c>Map&lt;string, Map&lt;string|number, any&gt;&gt; collections</c> with the
/// same <c>ephemeral</c> / <c>unique</c> / <c>perPlayer</c> meta-collection
/// semantics. Ephemeral collections (<c>sound</c>, <c>dice</c>) are
/// broadcast-only — never stored — so a late joiner never replays a stale
/// dice-roll animation. <c>unique</c> field tracking is preserved for the
/// snapshot (so the bundle can re-establish slot uniqueness) but the relay
/// does not enforce conflicts in Phase C-relay (Phase D-backend will own
/// rules-level adjudication).</para>
///
/// <para><b>Concurrency:</b> instances are thread-safe via per-game lock around
/// <see cref="ApplyUpdate"/> and <see cref="Snapshot"/>. Broadcast to other
/// connections is performed outside the lock by the caller.</para>
/// </summary>
public sealed class AutotableGameState
{
    private readonly Dictionary<string, Dictionary<object, JsonElement>> _collections = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ephemeralKinds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _uniqueFields = new(StringComparer.Ordinal);
    private readonly HashSet<string> _perPlayerKinds = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public string GameId { get; }

    public AutotableGameState(string gameId)
    {
        GameId = gameId;
    }

    /// <summary>
    /// Applies the given entries to the stored collections and returns the
    /// list to broadcast. Ephemeral kinds are NOT stored but ARE included in
    /// the broadcast list. Entries with <c>value == null</c> remove the key
    /// (upstream's tombstone semantics).
    /// </summary>
    /// <remarks>
    /// Mirrors upstream <c>server/game.ts:update()</c>. The meta-collections
    /// (<c>ephemeral</c>, <c>unique</c>, <c>perPlayer</c>) themselves are
    /// stored as ordinary collections so a late joiner re-receives the
    /// metadata declarations — that lets a non-first bundle still know which
    /// kinds are ephemeral, unique, or per-player. Their side-effect on the
    /// meta-tables takes effect immediately for subsequent entries in the
    /// same call.
    /// </remarks>
    public IReadOnlyList<CollectionEntry> ApplyUpdate(IEnumerable<CollectionEntry> entries)
    {
        if (entries is null) return Array.Empty<CollectionEntry>();

        var applied = new List<CollectionEntry>();
        lock (_lock)
        {
            foreach (var entry in entries)
            {
                if (entry is null || string.IsNullOrEmpty(entry.Kind)) continue;

                // Meta side-effects come first so a single transaction that
                // declares (ephemeral, X) and then writes to X correctly
                // treats X as ephemeral for the latter entry.
                ApplyMetaSideEffect(entry);

                // Ephemeral collections are broadcast but not stored.
                if (!_ephemeralKinds.Contains(entry.Kind))
                {
                    var collection = GetOrCreate(entry.Kind);
                    var normalizedKey = NormalizeKey(entry.Key);

                    if (entry.Value is null
                        || (entry.Value is JsonElement je && je.ValueKind == JsonValueKind.Null))
                    {
                        collection.Remove(normalizedKey);
                    }
                    else
                    {
                        collection[normalizedKey] = CloneValue(entry.Value);
                    }
                }

                applied.Add(entry);
            }
        }
        return applied;
    }

    /// <summary>
    /// Returns the full snapshot of all stored collections as a flat entry
    /// list suitable for an outbound <c>UPDATE { full: true }</c>.
    /// Ephemeral collections (which were never stored) are omitted.
    /// </summary>
    public IReadOnlyList<CollectionEntry> Snapshot()
    {
        lock (_lock)
        {
            var result = new List<CollectionEntry>();
            foreach (var (kind, collection) in _collections)
            {
                foreach (var (key, value) in collection)
                {
                    result.Add(new CollectionEntry(kind, key, value));
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Removes <paramref name="playerId"/> from every per-player collection
    /// (<c>seats</c>, <c>nicks</c>, <c>mouse</c>, …) and returns the resulting
    /// tombstone entries to broadcast. Mirrors upstream <c>leave()</c>.
    /// </summary>
    public IReadOnlyList<CollectionEntry> RemovePlayerEntries(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return Array.Empty<CollectionEntry>();

        var tombstones = new List<CollectionEntry>();
        lock (_lock)
        {
            foreach (var kind in _perPlayerKinds)
            {
                if (!_collections.TryGetValue(kind, out var collection)) continue;
                if (!collection.ContainsKey(playerId)) continue;
                collection.Remove(playerId);
                tombstones.Add(new CollectionEntry(kind, playerId, null));
            }
        }
        return tombstones;
    }

    // ── helpers ──────────────────────────────────────────────────────

    private void ApplyMetaSideEffect(CollectionEntry entry)
    {
        if (entry.Kind == "ephemeral" && entry.Key is string ephemeralKind)
        {
            var truthy = IsTruthy(entry.Value);
            if (truthy) _ephemeralKinds.Add(ephemeralKind);
            else _ephemeralKinds.Remove(ephemeralKind);
        }
        else if (entry.Kind == "perPlayer" && entry.Key is string perPlayerKind)
        {
            var truthy = IsTruthy(entry.Value);
            if (truthy) _perPlayerKinds.Add(perPlayerKind);
            else _perPlayerKinds.Remove(perPlayerKind);
        }
        else if (entry.Kind == "unique" && entry.Key is string uniqueKind)
        {
            if (entry.Value is JsonElement je && je.ValueKind == JsonValueKind.String)
            {
                _uniqueFields[uniqueKind] = je.GetString() ?? string.Empty;
            }
            else if (entry.Value is string field)
            {
                _uniqueFields[uniqueKind] = field;
            }
        }
    }

    private Dictionary<object, JsonElement> GetOrCreate(string kind)
    {
        if (!_collections.TryGetValue(kind, out var collection))
        {
            collection = new Dictionary<object, JsonElement>();
            _collections[kind] = collection;
        }
        return collection;
    }

    /// <summary>
    /// Normalises keys so that integer keys parsed from the wire compare
    /// equal regardless of whether they arrived as <c>long</c>, <c>int</c>,
    /// or numeric <c>JsonElement</c>. The autotable bundle uses both numeric
    /// (<c>things[42]</c>) and string keys (<c>seats["abc123"]</c>) — we
    /// preserve that distinction but coerce all integral numerics to
    /// <c>long</c> internally so the dictionary lookup is stable.
    /// </summary>
    private static object NormalizeKey(object key) => key switch
    {
        int i => (long)i,
        long l => l,
        short s => (long)s,
        double d when d == Math.Truncate(d) => (long)d,
        string s => s,
        JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt64(out var l) => l,
        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
        _ => key
    };

    private static JsonElement CloneValue(object? value)
    {
        if (value is JsonElement je) return je.Clone();
        // Fallback path — re-serialise so the stored form is a JsonElement.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => false,
            JsonValueKind.Number => je.TryGetDouble(out var d) && d != 0,
            JsonValueKind.String => !string.IsNullOrEmpty(je.GetString()),
            _ => true
        },
        _ => true
    };
}
