namespace Mahjong.Autotable.Api.Commentary;

/// <summary>
/// Phase K Wave 6 — Bishop. DI seam for the eventual LLM-driven
/// play-by-play commentary surface. Wave 6 ships the contract +
/// the <see cref="StubCommentaryGenerator"/> default implementation
/// so the controller is testable end-to-end without an LLM
/// dependency. The real generator lands in Phase L behind the same
/// interface; the controller does not change.
///
/// <para>The generator returns a <see cref="CommentaryReplay"/>
/// envelope keyed by <see cref="CommentaryReplay.GameId"/>. The
/// stub always returns a single deterministic
/// <see cref="CommentaryItem"/> bearing the canonical "not yet
/// available" message so downstream UIs can render without
/// branching on null.</para>
/// </summary>
public interface ICommentaryGenerator
{
    /// <summary>Triggers commentary generation for
    /// <paramref name="gameId"/>. The Wave-6 stub returns
    /// synchronously; Phase L's implementation will likely enqueue a
    /// background job and return immediately with a "pending"
    /// status (the envelope shape is forward-compatible — the stub
    /// reports <c>status: "stub"</c>).</summary>
    Task<CommentaryReplay> GenerateAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Returns the previously-generated commentary for
    /// <paramref name="gameId"/>, or a fresh stub envelope when no
    /// prior generation exists. The stub does not persist between
    /// requests — Phase L will store generated commentary in the
    /// audit table; today the surface is purely the contract
    /// pin.</summary>
    Task<CommentaryReplay> GetAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Phase K Wave 7 — Bishop. Returns the per-turn
    /// <see cref="CommentaryRecord"/> list backing the
    /// <c>GET /api/games/{gameId}/commentary/replay</c> endpoint.
    /// This is the canonical Phase-L LLM contract: each record is a
    /// single speaker utterance tied to a specific game turn +
    /// phase, with structured emotion intensity + tile references
    /// for downstream UI rendering.
    /// <para>Distinct from <see cref="GetAsync"/> which returns the
    /// W6 envelope (one summary item per call) — the records path
    /// is the streaming-friendly shape Phase L emits one-record-per-
    /// notable-event. The stub returns a single placeholder record
    /// so the wire contract is verifiable today.</para>
    /// </summary>
    Task<IReadOnlyList<CommentaryRecord>> GetRecordsAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Identifier of the active generator implementation.
    /// The stub returns <c>"stub"</c>; production implementations
    /// return their model identifier (e.g. <c>"gpt-4-mahjong-v1"</c>).
    /// Surfaced in the <see cref="CommentaryReplay.Generator"/> field.</summary>
    string GeneratorId { get; }
}

/// <summary>
/// Phase K Wave 6 — Bishop. Result envelope returned from
/// <see cref="ICommentaryGenerator.GenerateAsync"/> /
/// <see cref="ICommentaryGenerator.GetAsync"/>. The shape is
/// pinned by the Wave-6 contract test — the controller passes the
/// envelope through unchanged.
/// </summary>
public sealed record CommentaryReplay(
    Guid GameId,
    string Generator,
    string Status,
    IReadOnlyList<CommentaryItem> Items);

/// <summary>
/// Phase K Wave 6 — Bishop. Single commentary entry. The Wave-6
/// stub emits one of these per call; Phase L will emit one per
/// notable game event (riichi, win, dora kan, etc.).
/// </summary>
public sealed record CommentaryItem(
    int Sequence,
    string Text,
    int? RoundOrdinal,
    string Tone);

/// <summary>
/// Phase K Wave 7 — Bishop. Finalised JSON contract for the
/// Phase-L LLM-driven commentary records. One record per speaker
/// utterance tied to a specific game turn + phase. The fields are
/// pinned by <c>Phase_K_W7/Bishop/CommentaryRecordContractTests</c>;
/// downstream LLM generators MUST emit records that round-trip
/// through this exact shape.
///
/// <list type="bullet">
///   <item><b>GameId</b> — opaque string id (matches the persistence
///         column shape; Phase L emitter may use the
///         <c>Guid.ToString("N")</c> form or an alternative if the
///         backing store changes).</item>
///   <item><b>TurnNumber</b> — 1-based turn number inside the game.
///         Zero for pre-deal commentary; negative values are
///         reserved (and rejected by the contract test).</item>
///   <item><b>Phase</b> — one of <c>"draw"</c>, <c>"discard"</c>,
///         <c>"claim"</c>, or <c>"win"</c>. Other values fail the
///         contract — downstream UI dispatch keys on this field.</item>
///   <item><b>Speaker</b> — one of <c>"play-by-play"</c>,
///         <c>"color"</c>, or <c>"analyst"</c>. Three personas keep
///         the broadcast tone varied without an unbounded namespace.</item>
///   <item><b>Text</b> — the utterance text. Plain UTF-8; no
///         HTML/Markdown.</item>
///   <item><b>EmotionIntensity</b> — 0.0..1.0 inclusive. Downstream
///         renderers map this to font weight, voice tempo, or color
///         saturation. Values outside [0,1] fail the contract.</item>
///   <item><b>TileReferences</b> — wire-name tile ids referenced by
///         <see cref="Text"/> (e.g. <c>"man5"</c>, <c>"pin3"</c>,
///         <c>"sou9"</c>). Empty array when the utterance carries no
///         tile reference; never null.</item>
///   <item><b>GeneratedAt</b> — UTC timestamp the record was minted.
///         <see cref="DateTimeOffset"/> so the offset is wire-visible
///         and replay tooling can sort across generator runs.</item>
/// </list>
/// </summary>
public sealed record CommentaryRecord(
    string GameId,
    int TurnNumber,
    string Phase,
    string Speaker,
    string Text,
    double EmotionIntensity,
    string[] TileReferences,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Phase K Wave 7 — Bishop. Canonical wire-string vocabulary for
/// <see cref="CommentaryRecord.Phase"/>. Contract tests pin the set
/// so a typo in a downstream emitter is caught at validation time
/// rather than silently producing unrenderable records.
/// </summary>
public static class CommentaryPhases
{
    public const string Draw = "draw";
    public const string Discard = "discard";
    public const string Claim = "claim";
    public const string Win = "win";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Draw, Discard, Claim, Win,
    };
}

/// <summary>
/// Phase K Wave 7 — Bishop. Canonical wire-string vocabulary for
/// <see cref="CommentaryRecord.Speaker"/>.
/// </summary>
public static class CommentarySpeakers
{
    public const string PlayByPlay = "play-by-play";
    public const string Color = "color";
    public const string Analyst = "analyst";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        PlayByPlay, Color, Analyst,
    };
}
