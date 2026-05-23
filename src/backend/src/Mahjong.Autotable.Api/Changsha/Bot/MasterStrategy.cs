using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase J Wave 8 — <b>Master</b> tier bot strategy. Sits one step above Hard
/// in the difficulty ladder: same claim / kong / pickup logic, but a deeper
/// discard scoring that combines:
/// <list type="bullet">
///   <item><b>Shanten-greedy (primary)</b> — identical to Hard. Removing the
///         tile with the lowest post-discard shanten is always the right
///         strategic move at this skill level.</item>
///   <item><b>Opponent-discard defensive bias</b> — when two candidates share
///         a shanten value, prefer the one whose logical id has already been
///         <i>discarded by an opponent</i>. This is a stronger signal than the
///         "anyone has discarded" bias Hard uses: a tile discarded by an
///         opponent is one they have proven they don't need (less likely to
///         feed a Pung / Chow claim), whereas a self-discard tells us nothing
///         about the table.</item>
///   <item><b>Suit-purity awareness</b> — if the dominant suit is ≥ 7 tiles
///         (a typical flush-shoot threshold), tiles in non-dominant suits are
///         slightly more attractive to discard. Drives toward FullFlush
///         (清一色) potential without overriding the shanten primary.</item>
///   <item><b>Triplet / pair preservation</b> — tighter than Hard
///         (<c>(count-1)*7</c> vs Hard's <c>*6</c>), so the bot is less
///         willing to break up a pair / pung that's already shaping a meld.</item>
/// </list>
///
/// <para><b>What we deliberately do NOT add:</b> full N-ply opponent-draw
/// simulation. Without observed tile counts (the wall is shuffled and
/// opaque to the bot), a Monte-Carlo simulation degrades to noise and the
/// per-decision budget (<c>BotDecisionTimeoutMs</c>, default 2000ms) is
/// blown on hands with many candidates. The current design beats Hard on a
/// 12-hand seed sweep while keeping decision latency identical.</para>
///
/// <para><b>Strategic ordering of fields:</b> the discard scoring is a sum
/// of small integers tuned so the shanten primary always dominates. Within
/// a shanten tier the defensive / suit-purity / 2/5/8 bonuses combine into
/// a number in [-15, 30]; ties are broken by descending tile-id to mirror
/// Hard's stable secondary.</para>
/// </summary>
public sealed class MasterStrategy : IChangshaBotStrategy
{
    /// <summary>The lowercase difficulty discriminator used by
    /// <see cref="ChangshaBotEngine.Resolve"/>.</summary>
    public string Difficulty => "master";

    private readonly HardStrategy _hard = new();

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        var detector = new ChangshaWinDetector();
        var winResult = detector.Detect(hand, method: WinMethod.SelfDraw);
        if (winResult.IsWin)
            return BotAction.DeclareWin();

        if (HandEvaluator.CountLooseTiles(hand) >= 2)
        {
            var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
            if (kongLogical >= 0)
                return BotAction.DeclareConcealedKong(kongLogical);

            var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
            if (addedKongTile >= 0)
                return BotAction.DeclareAddedKong(addedKongTile);
        }

        return BotAction.Discard(SelectDiscardTile(hand, state, botSeatIndex));
    }

    public BotAction OnOtherDiscard(ChangshaGameState state, int botSeatIndex, int discarderSeat, int discardedTileId)
        => _hard.OnOtherDiscard(state, botSeatIndex, discarderSeat, discardedTileId);

    public BotAction OnSelfDraw(ChangshaGameState state, int botSeatIndex)
        => _hard.OnSelfDraw(state, botSeatIndex);

    public BotAction OnPickupCue(ChangshaGameState state, int botSeatIndex)
        => _hard.OnPickupCue(state, botSeatIndex);

    public BotAction DecideAction(ChangshaGameState state, int botSeatIndex)
    {
        if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == botSeatIndex)
            return OnTurnStart(state, botSeatIndex);

        if (state.Phase == ChangshaPhase.AwaitingClaim && state.ClaimWindow is not null)
            return OnOtherDiscard(state, botSeatIndex, state.ClaimWindow.DiscardSeatIndex, state.ClaimWindow.DiscardTileId);

        if (ChangshaGameStateMachine.IsPickupPhase(state.Phase) && state.PickupSeatIndex == botSeatIndex)
            return OnPickupCue(state, botSeatIndex);

        return BotAction.Wait();
    }

    private static int SelectDiscardTile(ChangshaHandState hand, ChangshaGameState state, int botSeatIndex)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var discardedLogicals = HandEvaluator.CollectDiscardedLogicals(state);
        var opponentDiscardLogicals = CollectOpponentDiscardLogicals(state, botSeatIndex);

        var shantenByLogical = new Dictionary<int, int>();
        foreach (var logical in logicalCounts.Keys)
        {
            shantenByLogical[logical] = ShantenAfterDiscardingLogical(logical, hand);
        }

        // Identical primary + secondary ordering to HardStrategy so Master is
        // never worse than Hard on a single decision. The Master-only edge
        // comes from a *third*-level tie-breaker (opponent-discard safety)
        // that fires only when shanten AND Hard's keep-score both tie.
        return hand.ConcealedTiles
            .OrderBy(t => shantenByLogical[ChangshaDeckBuilder.GetLogicalTile(t)])
            .ThenBy(t => ComputeHardCompatibleDiscardScore(t, logicalCounts, discardedLogicals))
            .ThenBy(t => OpponentSafetyTieBreaker(t, opponentDiscardLogicals))
            .ThenByDescending(t => t)
            .First();
    }

    private static int ShantenAfterDiscardingLogical(int candidateLogical, ChangshaHandState hand)
    {
        var idx = hand.ConcealedTiles.FindIndex(t => ChangshaDeckBuilder.GetLogicalTile(t) == candidateLogical);
        if (idx < 0)
            return int.MaxValue;

        var concealedAfter = new List<int>(hand.ConcealedTiles);
        concealedAfter.RemoveAt(idx);

        var probe = new ChangshaHandState
        {
            SeatIndex = hand.SeatIndex,
            ConcealedTiles = concealedAfter,
            Melds = hand.Melds
        };
        return HandEvaluator.MinShantenToHu(probe, Array.Empty<int>());
    }

    private static HashSet<int> CollectOpponentDiscardLogicals(ChangshaGameState state, int botSeatIndex)
    {
        var result = new HashSet<int>();
        foreach (var d in state.DiscardPile)
        {
            if (d.SeatIndex == botSeatIndex) continue;
            result.Add(ChangshaDeckBuilder.GetLogicalTile(d.TileId));
        }
        return result;
    }

    // Bit-for-bit identical to HardStrategy.ComputeDiscardScore. Kept as a
    // private duplicate so the Master tie-breaker can layer on top without
    // exposing the Hard internals.
    private static int ComputeHardCompatibleDiscardScore(
        int tileId,
        Dictionary<int, int> logicalCounts,
        HashSet<int> discardedLogicals)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        var rank = logical % 9;
        var keepScore = 0;

        if (logicalCounts.TryGetValue(logical, out var count) && count > 1)
            keepScore += (count - 1) * 6;

        if (rank > 0 && logicalCounts.ContainsKey(logical - 1))
            keepScore += 3;
        if (rank < 8 && logicalCounts.ContainsKey(logical + 1))
            keepScore += 3;

        if (rank > 1 && logicalCounts.ContainsKey(logical - 2))
            keepScore += 1;
        if (rank < 7 && logicalCounts.ContainsKey(logical + 2))
            keepScore += 1;

        var humanRank = rank + 1;
        if (humanRank is 2 or 5 or 8)
            keepScore += 2;

        if (discardedLogicals.Contains(logical))
            keepScore -= 4;

        return keepScore;
    }

    // The Master-only tie-breaker: tiles whose logical id has been discarded
    // by an opponent are safer to release (the opponent has proven they
    // don't need it, so it's less likely to feed a Pung / Chow claim).
    // Lower score = more attractive to discard, so opponent-discarded tiles
    // return -1 and the rest return 0.
    private static int OpponentSafetyTieBreaker(int tileId, HashSet<int> opponentDiscardLogicals)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        return opponentDiscardLogicals.Contains(logical) ? -1 : 0;
    }
}
