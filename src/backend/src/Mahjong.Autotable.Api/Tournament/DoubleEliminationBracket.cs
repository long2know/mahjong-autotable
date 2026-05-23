namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase K Wave 6 — Bishop. <see cref="IBracketGenerator"/> for
/// double-elimination tournaments. Emits the winners-bracket
/// round-1 + a placeholder losers-bracket round-1 + the eventual
/// grand-final slot.
///
/// <para><b>Topology recap.</b> Double-elim runs two parallel
/// brackets:</para>
/// <list type="bullet">
///   <item><b>Winners' bracket</b> — standard single-elim. Lose
///         once and you drop to the losers' bracket.</item>
///   <item><b>Losers' bracket</b> — players dropped from winners'
///         keep playing until a second loss eliminates them.</item>
///   <item><b>Grand final</b> — winner of WB faces the winner of
///         LB. The WB winner enters with a one-loss buffer (must
///         be beaten twice in the grand final); the LB winner has
///         already lost once and must win the first grand-final
///         match to force a "reset" grand-final.</item>
/// </list>
///
/// <para><b>Wave-6 emission.</b> The factory is invoked at
/// tournament start; at that point only the winners-bracket
/// round-1 pairings are knowable from the seed list alone. The
/// losers-bracket and grand-final slots are emitted as placeholder
/// pairings with empty player ids (the service fills them in once
/// downstream matches resolve). The placeholder rows let operators
/// see the full bracket shape in the API surface today; the
/// <see cref="TournamentService"/> advance-round path is the
/// canonical owner of slot-filling.</para>
///
/// <para>The full bracket extends to <c>ceil(log2(N))</c> winners-
/// bracket rounds + <c>2*ceil(log2(N)) - 1</c> losers-bracket
/// rounds + 1 (or 2) grand-final games. Wave 6 ships the round-1
/// emission — the service grows the bracket lazily as matches
/// complete, mirroring the single-elim flow.</para>
/// </summary>
public sealed class DoubleEliminationBracket : IBracketGenerator
{
    public const string PlaceholderPlayer = "__pending__";

    public BracketFormat Format => BracketFormat.DoubleElimination;

    public IReadOnlyList<BracketPairing> Generate(IReadOnlyList<string> seededPlayers)
    {
        ArgumentNullException.ThrowIfNull(seededPlayers);
        if (seededPlayers.Count < 2) return Array.Empty<BracketPairing>();

        var results = new List<BracketPairing>();

        // Winners bracket round 1 — same shape as single-elim seeding.
        var wbR1 = TournamentPairing.SingleEliminationFirstRound(seededPlayers);
        foreach (var pair in wbR1)
        {
            results.Add(new BracketPairing(
                Round: 1,
                Bracket: BracketSide.Winners,
                P1: pair.P1,
                P2: pair.P2));
        }

        // Losers bracket round 1 — placeholder slots. The losers-
        // bracket starts populating after winners-bracket round 1
        // finishes; the placeholder shape exists so the API surface
        // pre-emits the slot count.
        var lbSlots = wbR1.Count / 2;
        for (var i = 0; i < lbSlots; i++)
        {
            results.Add(new BracketPairing(
                Round: 1,
                Bracket: BracketSide.Losers,
                P1: PlaceholderPlayer,
                P2: PlaceholderPlayer));
        }

        // Grand final — single placeholder pairing. The bracket
        // generator emits one slot today; the service grows the
        // surface to a second "reset" pairing if the LB winner
        // beats the WB winner in the first grand-final game.
        results.Add(new BracketPairing(
            Round: 1,
            Bracket: BracketSide.GrandFinal,
            P1: PlaceholderPlayer,
            P2: PlaceholderPlayer));

        return results;
    }
}

/// <summary>
/// Phase K Wave 6 — Bishop. Type alias preserved for the W6 contract
/// tests that check by class name. <see cref="DoubleEliminationBracket"/>
/// is the canonical name; <c>DoubleElimBracket</c> aliases it.
/// </summary>
public sealed class DoubleElimBracket : IBracketGenerator
{
    private readonly DoubleEliminationBracket _inner = new();

    public BracketFormat Format => _inner.Format;

    public IReadOnlyList<BracketPairing> Generate(IReadOnlyList<string> seededPlayers)
        => _inner.Generate(seededPlayers);
}
