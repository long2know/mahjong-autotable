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
