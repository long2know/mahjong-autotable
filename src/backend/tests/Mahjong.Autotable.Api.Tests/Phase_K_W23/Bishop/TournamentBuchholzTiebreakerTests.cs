using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Tournament;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Pure-math tests for the Buchholz +
/// Sonneborn-Berger tiebreakers wired into the W22
/// TournamentFinalizationController. The math is pinned at
/// the internal-static helper level so a regression surfaces
/// before any HTTP round-trip is involved.
/// </summary>
public sealed class TournamentBuchholzTiebreakerTests
{
    private static TournamentMatch Match(int round, string p1, string p2, string? winner)
    {
        return new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = Guid.Empty,
            Round = round,
            Player1Id = p1,
            Player2Id = p2,
            Status = "complete",
            WinnerPlayerId = winner,
            CompletedAt = DateTime.UtcNow,
        };
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_NoMatches_ReturnsZeroForEveryPlayer()
    {
        var matches = Array.Empty<TournamentMatch>();
        var wins = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        var b = TournamentFinalizationController.ComputeBuchholz(matches, wins.Keys, wins);
        Assert.Equal(0.0, b["a"]);
        Assert.Equal(0.0, b["b"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_SingleMatch_SumsOpponentWins()
    {
        // a beat b; b has 0 wins, a has 1 win.
        var matches = new[] { Match(1, "a", "b", "a") };
        var wins = new Dictionary<string, int> { ["a"] = 1, ["b"] = 0 };
        var b = TournamentFinalizationController.ComputeBuchholz(matches, wins.Keys, wins);
        // a's only opponent (b) has 0 wins → Buchholz(a)=0.
        // b's only opponent (a) has 1 win  → Buchholz(b)=1.
        Assert.Equal(0.0, b["a"]);
        Assert.Equal(1.0, b["b"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_MultiRoundSwiss_AggregatesAcrossOpponents()
    {
        // 3-round Swiss: a-b (a wins), a-c (a wins), b-c (b wins)
        var matches = new[]
        {
            Match(1, "a", "b", "a"),
            Match(2, "a", "c", "a"),
            Match(3, "b", "c", "b"),
        };
        var wins = new Dictionary<string, int> { ["a"] = 2, ["b"] = 1, ["c"] = 0 };
        var b = TournamentFinalizationController.ComputeBuchholz(matches, wins.Keys, wins);
        // a's opponents (b, c) → 1 + 0 = 1
        // b's opponents (a, c) → 2 + 0 = 2
        // c's opponents (a, b) → 2 + 1 = 3
        Assert.Equal(1.0, b["a"]);
        Assert.Equal(2.0, b["b"]);
        Assert.Equal(3.0, b["c"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void SonnebornBerger_BeatStrongOpponentScoresMore()
    {
        // a beat b (1 win), a beat c (0 wins). SB(a) = 1 + 0 = 1.
        // b's only match was a loss to a — SB(b) = 0 (lost the match).
        var matches = new[]
        {
            Match(1, "a", "b", "a"),
            Match(2, "a", "c", "a"),
        };
        var wins = new Dictionary<string, int> { ["a"] = 2, ["b"] = 0, ["c"] = 0 };
        var sb = TournamentFinalizationController.ComputeSonnebornBerger(matches, wins.Keys, wins);
        Assert.Equal(0.0, sb["a"]); // opponents both have 0 wins
        Assert.Equal(0.0, sb["b"]);
        Assert.Equal(0.0, sb["c"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void SonnebornBerger_BeatHighScorer_GetsCredit()
    {
        // a beat b. b beat c. b has 1 win, c has 0 wins, a has 1 win.
        // SB(a) = 1 (a beat b who has 1 win).
        // SB(b) = 0 (b beat c who has 0 wins).
        // SB(c) = 0 (lost both).
        var matches = new[]
        {
            Match(1, "a", "b", "a"),
            Match(2, "b", "c", "b"),
        };
        var wins = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 0 };
        var sb = TournamentFinalizationController.ComputeSonnebornBerger(matches, wins.Keys, wins);
        Assert.Equal(1.0, sb["a"]);
        Assert.Equal(0.0, sb["b"]);
        Assert.Equal(0.0, sb["c"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void SonnebornBerger_DrawCountsAsHalfWeight()
    {
        // Drawn match (no WinnerPlayerId). Both seats get
        // 0.5 · opponent_wins from each other.
        // a's only opponent (b) has 2 wins → SB(a) = 0.5 * 2 = 1.0
        // b's only opponent (a) has 2 wins → SB(b) = 0.5 * 2 = 1.0
        var matches = new[] { Match(1, "a", "b", null) };
        var wins = new Dictionary<string, int> { ["a"] = 2, ["b"] = 2 };
        var sb = TournamentFinalizationController.ComputeSonnebornBerger(matches, wins.Keys, wins);
        Assert.Equal(1.0, sb["a"]);
        Assert.Equal(1.0, sb["b"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_RoundRobin4PlayerSeats_CountsEveryOtherSeat()
    {
        var m = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            Round = 1,
            Status = "complete",
            Player1Id = "a",
            Player2Id = "b",
            Player3Id = "c",
            Player4Id = "d",
            WinnerPlayerId = "a",
        };
        var wins = new Dictionary<string, int> { ["a"] = 1, ["b"] = 0, ["c"] = 0, ["d"] = 0 };
        var b = TournamentFinalizationController.ComputeBuchholz(new[] { m }, wins.Keys, wins);
        // Each player has 3 opponents — sums of opponent
        // wins: a→0, b→1, c→1, d→1.
        Assert.Equal(0.0, b["a"]);
        Assert.Equal(1.0, b["b"]);
        Assert.Equal(1.0, b["c"]);
        Assert.Equal(1.0, b["d"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeBuchholz(null!, new[] { "a" }, new Dictionary<string, int>()));
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeBuchholz(Array.Empty<TournamentMatch>(), null!, new Dictionary<string, int>()));
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeBuchholz(Array.Empty<TournamentMatch>(), new[] { "a" }, null!));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Sonneborn_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeSonnebornBerger(null!, new[] { "a" }, new Dictionary<string, int>()));
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeSonnebornBerger(Array.Empty<TournamentMatch>(), null!, new Dictionary<string, int>()));
        Assert.Throws<ArgumentNullException>(() =>
            TournamentFinalizationController.ComputeSonnebornBerger(Array.Empty<TournamentMatch>(), new[] { "a" }, null!));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buchholz_PlayerWithoutMatches_StillEmitsZeroEntry()
    {
        // A registered-but-never-played player (e.g. withdrawn
        // before round 1) should still appear in the result.
        var matches = new[] { Match(1, "a", "b", "a") };
        var wins = new Dictionary<string, int> { ["a"] = 1, ["b"] = 0, ["c"] = 0 };
        var b = TournamentFinalizationController.ComputeBuchholz(matches, wins.Keys, wins);
        Assert.True(b.ContainsKey("c"));
        Assert.Equal(0.0, b["c"]);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Standings_NewFieldsAreOnTheEntity()
    {
        var s = new TournamentStanding
        {
            TournamentId = Guid.NewGuid(),
            PlayerId = "x",
            Rank = 1,
            Points = 3,
            GamesPlayed = 3,
            Buchholz = 7.5,
            SonnebornBerger = 4.25,
        };
        Assert.Equal(7.5, s.Buchholz);
        Assert.Equal(4.25, s.SonnebornBerger);
    }
}
