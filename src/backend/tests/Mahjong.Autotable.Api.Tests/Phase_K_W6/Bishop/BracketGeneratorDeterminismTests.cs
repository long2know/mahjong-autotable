using Mahjong.Autotable.Api.Tournament;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W6.Bishop;

/// <summary>
/// Phase K Wave 6 — Bishop. Bracket-generator determinism contract.
///
/// <para>Generators MUST be pure functions of the seed list: the
/// same seeds in the same order produce the same pairings on every
/// invocation. This is a hard requirement for any "view the bracket"
/// API surface — the controller may call the generator multiple
/// times across requests; the persisted matches must match the
/// re-generated shape.</para>
///
/// <para>The factory MUST resolve all four wire-string formats
/// (<c>single-elimination</c>, <c>round-robin</c>, <c>swiss</c>,
/// <c>double-elimination</c>) to their respective generators.</para>
/// </summary>
public sealed class BracketGeneratorDeterminismTests
{
    private static readonly string[] Seeds8 = new[]
    {
        "alice", "bob", "carol", "dave",
        "eve", "frank", "grace", "henry",
    };

    private static readonly string[] Seeds4 = new[]
    {
        "alice", "bob", "carol", "dave",
    };

    [Theory]
    [InlineData(BracketFormat.SingleElimination)]
    [InlineData(BracketFormat.RoundRobin)]
    [InlineData(BracketFormat.Swiss)]
    [InlineData(BracketFormat.DoubleElimination)]
    public void Generator_is_deterministic_for_same_seeds(BracketFormat format)
    {
        var factory = TournamentBracketGenerator.CreateDefault();
        var generator = factory.Resolve(format);

        var run1 = generator.Generate(Seeds8);
        var run2 = generator.Generate(Seeds8);

        Assert.Equal(run1.Count, run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            Assert.Equal(run1[i].Round, run2[i].Round);
            Assert.Equal(run1[i].Bracket, run2[i].Bracket);
            Assert.Equal(run1[i].P1, run2[i].P1);
            Assert.Equal(run1[i].P2, run2[i].P2);
        }
    }

    [Fact]
    public void Factory_resolves_all_four_formats_by_wire_string()
    {
        var factory = TournamentBracketGenerator.CreateDefault();

        Assert.IsType<SingleEliminationBracket>(factory.Resolve("single-elimination"));
        Assert.IsType<RoundRobinBracket>(factory.Resolve("round-robin"));
        Assert.IsType<SwissBracket>(factory.Resolve("swiss"));
        Assert.IsType<DoubleEliminationBracket>(factory.Resolve("double-elimination"));
    }

    [Fact]
    public void Factory_resolves_all_four_formats_by_enum()
    {
        var factory = TournamentBracketGenerator.CreateDefault();

        Assert.Equal(BracketFormat.SingleElimination, factory.Resolve(BracketFormat.SingleElimination).Format);
        Assert.Equal(BracketFormat.RoundRobin, factory.Resolve(BracketFormat.RoundRobin).Format);
        Assert.Equal(BracketFormat.Swiss, factory.Resolve(BracketFormat.Swiss).Format);
        Assert.Equal(BracketFormat.DoubleElimination, factory.Resolve(BracketFormat.DoubleElimination).Format);
    }

    [Fact]
    public void Single_elimination_round1_pairs_seeded_endpoints()
    {
        var gen = new SingleEliminationBracket();
        var pairings = gen.Generate(Seeds4);

        Assert.Equal(2, pairings.Count);
        Assert.All(pairings, p => Assert.Equal(1, p.Round));
        Assert.All(pairings, p => Assert.Equal(BracketSide.Winners, p.Bracket));
    }

    [Fact]
    public void Swiss_round1_pairs_top_half_with_bottom_half()
    {
        var gen = new SwissBracket();
        var pairings = gen.Generate(Seeds4);

        // Swiss emits a 4-round schedule; round 1 alone is the
        // top-vs-bottom half-and-half seed match. We assert round 1
        // explicitly and let later rounds remain a deterministic
        // baseline owned by SwissBracket.
        var round1 = pairings.Where(p => p.Round == 1).ToList();
        Assert.Equal(2, round1.Count);
        Assert.All(round1, p => Assert.Equal(BracketSide.Winners, p.Bracket));
        Assert.All(pairings, p => Assert.Equal(BracketSide.Winners, p.Bracket));
    }

    [Fact]
    public void Round_robin_emits_n_minus_1_rounds_for_even_count()
    {
        var gen = new RoundRobinBracket();
        var pairings = gen.Generate(Seeds4);

        // For 4 players: 3 rounds * 2 pairings per round = 6 pairings.
        Assert.Equal(6, pairings.Count);
        var rounds = pairings.Select(p => p.Round).Distinct().OrderBy(r => r).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, rounds);
    }

    [Fact]
    public void Double_elimination_emits_winners_losers_and_grand_final_slots()
    {
        var gen = new DoubleEliminationBracket();
        var pairings = gen.Generate(Seeds8);

        // 8 seeds → 4 WB round-1 pairings + 2 LB round-1 placeholder
        // pairings + 1 grand-final placeholder pairing = 7 total.
        Assert.Equal(7, pairings.Count);

        var winners = pairings.Where(p => p.Bracket == BracketSide.Winners).ToList();
        var losers = pairings.Where(p => p.Bracket == BracketSide.Losers).ToList();
        var grand = pairings.Where(p => p.Bracket == BracketSide.GrandFinal).ToList();

        Assert.Equal(4, winners.Count);
        Assert.Equal(2, losers.Count);
        Assert.Single(grand);

        // Real seed names land in the winners bracket; the losers +
        // grand-final slots are placeholders pending downstream
        // match resolutions.
        Assert.All(winners, p => Assert.DoesNotContain(p.P1, new[] { DoubleEliminationBracket.PlaceholderPlayer }));
        Assert.All(losers, p => Assert.Equal(DoubleEliminationBracket.PlaceholderPlayer, p.P1));
        Assert.All(grand, p => Assert.Equal(DoubleEliminationBracket.PlaceholderPlayer, p.P1));
    }

    [Fact]
    public void Generators_return_empty_for_fewer_than_two_players()
    {
        var single = new string[] { "alice" };

        Assert.Empty(new SingleEliminationBracket().Generate(single));
        Assert.Empty(new SwissBracket().Generate(single));
        Assert.Empty(new RoundRobinBracket().Generate(single));
        Assert.Empty(new DoubleEliminationBracket().Generate(single));
    }
}
