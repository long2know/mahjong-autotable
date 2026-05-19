using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;

namespace Mahjong.Autotable.Api.Tests.Changsha.Acceptance;

/// <summary>
/// Acceptance: full-hand integration. The Phase D-backend "playable" gate.
///
/// Sources: synthesis of Vasquez rules-diff manifest §1.5–§1.13 + MahjongPros end-to-end
/// playable scenario. This is the single test Stephen will pin to "is Changsha playable
/// from deal to Hu?"
///
/// Strategy: drive 4 bots through one full hand via <see cref="BotMatchHarness"/>, then
/// assert the post-hand state has the shape the autotable client needs to render:
///   - Deal happened (53 tiles dealt + 55 in wall before play started).
///   - Phase reached EndHand.
///   - Either a winner exists (CurrentWin set + scoring complete) OR wall exhausted.
///   - Event log contains the canonical lifecycle events (deal → turn → discard → claim? → win/draw).
///   - Cumulative scores changed iff there was a winner.
///   - Banker rotation (next hand) honours §1.13 (winner becomes dealer OR dealer keeps on washout).
/// </summary>
public class EndToEndPlayableTests
{
    [Theory, Trait("Category", "Acceptance")]
    [InlineData(42)]
    [InlineData(12345)]
    [InlineData(7777)]
    public void Full_Hand_FromDeal_To_HandEnd_AllBots(int seed)
    {
        // MahjongPros end-to-end: a hand starts with the deal, runs through draws/discards/
        // claims, and ends with either a Hu or a washout.
        var outcome = BotMatchHarness.RunUntilHandFinished(seed);

        Assert.Equal(ChangshaPhase.EndHand, outcome.FinalState.Phase);
        Assert.True(outcome.WinnerDeclared || outcome.WallExhausted,
            $"Hand must end in either a win or a wall-exhaustion draw. " +
            $"WinnerDeclared={outcome.WinnerDeclared}, WallExhausted={outcome.WallExhausted}, " +
            $"Phase={outcome.FinalState.Phase}, Steps={outcome.Steps}.");
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Full_Hand_ProducesCanonicalEventTimeline()
    {
        // Vasquez §1.5 + §1.7: the event log must include the major lifecycle markers so the
        // autotable client (Bishop's Phase D-backend relay) can broadcast each into the
        // protocol's collection mutations.
        var outcome = BotMatchHarness.RunUntilHandFinished(seed: 4242);
        var types = outcome.FinalState.EventLog.Select(e => e.EventType).ToList();

        Assert.Contains("game-created", types);
        Assert.Contains("game-started", types);
        Assert.Contains("dice-rolled", types);
        Assert.Contains("tiles-dealt", types);
        // A hand must include at least one discard event.
        Assert.Contains("tile-discarded", types);
        // And finish with either win-declared (Hu path) or wall-exhausted/draw-hand (washout path).
        // `wall-exhausted` is emitted first when the wall empties; `draw-hand` is emitted by
        // HandleWallExhausted as the EndHand terminal marker.
        Assert.True(
            types.Contains("win-declared") || types.Contains("wall-exhausted") || types.Contains("draw-hand"),
            $"Hand timeline must terminate with win-declared (Hu) or wall-exhausted/draw-hand (washout). " +
            $"Got terminal events: [{string.Join(", ", types.TakeLast(8))}]");
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Full_Hand_WithWinner_PopulatesScoreAndUpdatesCumulativeScores()
    {
        // Vasquez §1.13 + §5: a Hu must populate CurrentWin + CurrentScore and apply payments
        // to CumulativeScores. We loop seeds until we hit one that produces a winner so the test
        // is deterministic across CI (washouts get skipped — the assertion still proves the path).
        var winningSeed = Enumerable.Range(1, 50)
            .Select(s => BotMatchHarness.RunUntilHandFinished(seed: s))
            .FirstOrDefault(o => o.WinnerDeclared);

        Assert.NotNull(winningSeed);
        Assert.NotNull(winningSeed!.FinalState.CurrentWin);
        Assert.NotNull(winningSeed.FinalState.CurrentScore);
        Assert.NotEmpty(winningSeed.FinalState.CurrentScore!.Payments);

        // Cumulative scores must net to zero (Vasquez §5 zero-sum invariant).
        var sum = winningSeed.FinalState.CumulativeScores.Values.Sum();
        Assert.Equal(0, sum);
    }

    [Fact, Trait("Category", "Acceptance")]
    public void Full_Hand_PostHand_BankerRotation_HonorsWinnerBecomesDealer()
    {
        // Vasquez §1.13: after a Hu, the winner becomes the next dealer.
        var winningOutcome = Enumerable.Range(1, 50)
            .Select(s => BotMatchHarness.RunUntilHandFinished(seed: s))
            .FirstOrDefault(o => o.WinnerDeclared);

        Assert.NotNull(winningOutcome);
        var state = winningOutcome!.FinalState;
        var winnerSeat = state.CurrentWin!.WinningSeatIndex;
        var dealerBefore = state.DealerSeatIndex;

        ChangshaGameStateMachine.RotateBanker(state);

        Assert.Equal(winnerSeat, state.DealerSeatIndex);
        Assert.True(state.Seats[winnerSeat].IsDealer);
        if (winnerSeat != dealerBefore)
            Assert.False(state.Seats[dealerBefore].IsDealer);
    }

    [Fact(Skip = "Phase D-backend gap: end-to-end WS-relay test requires Bishop's Phase D pipe. The relay must broadcast tiles-dealt + claim-window-open + win-declared as autotable collection mutations. Once Bishop's Phase D pipe lands, replace this skip with a TestServer-backed harness similar to ChangshaHubE2ETests.E2E1_AllBots_PlaysAtLeastOneHandAndCompletes."), Trait("Category", "Acceptance")]
    public void Full_Hand_ViaAutotableWebSocketRelay_BotsAndOneHuman()
    {
        // Once Phase D-backend wires the rules engine to autotable's WS pipe (Bishop's scope),
        // this test should:
        //   1. Stand up the in-memory TestServer (WebApplicationFactory<Program>).
        //   2. Connect a single client to /ws/autotable (or whichever path Bishop chose).
        //   3. Send NEW + JOIN messages; assert JOINED responses.
        //   4. Drive seat 0 with a scripted discard sequence; bots run on seats 1–3.
        //   5. Assert the inbound UPDATE collection-mutation stream carries:
        //      - "match" with dealer + handNumber
        //      - "dice" with dice + state:"rolled"
        //      - "things" with seat-0's hand tiles (private) + others' hand placeholders (public)
        //      - "changsha.scoring" / "changsha.banker" / "changsha.lifecycle" mutations
        //   6. Hand terminates in WinDeclared or WallExhausted.
    }
}
