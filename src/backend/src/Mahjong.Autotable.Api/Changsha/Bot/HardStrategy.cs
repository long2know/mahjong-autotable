using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F "Hard" difficulty strategy. Combines Medium's keep-score with a defensive
/// penalty for tiles that opponents are likely to need.
/// <list type="bullet">
///   <item>Discards prefer "safe" tiles — anything already present in the discard pile
///   is heavily prioritised for discard since opponents have demonstrated they don't
///   need it.</item>
///   <item>Claims Hu unconditionally. Pung/Kong/Chow are gated on a strict shanten
///   drop (see Phase J Wave 1 note below); naive "fussy Chow" heuristics from
///   Phase F have been retired in favour of the shanten gate.</item>
///   <item>Declares concealed/added kong opportunistically but only when the resulting
///   hand state still has enough "loose" tiles to absorb a kong replacement draw.</item>
/// </list>
/// Phase I Wave 4 swapped the underlying shanten counter (<see cref="HandEvaluator.MinShantenToHu"/>)
/// for a rigorous backtracking implementation AND wired it into
/// <see cref="SelectDiscardTile"/> as the keep-score tie-breaker: when two
/// candidate discards have identical keep-scores, the one whose removal keeps
/// post-discard shanten lowest wins. Promoting shanten to the primary ordering
/// key was investigated and rolled back — the Phase F keep-score heuristic was
/// already statistically stronger than naive shanten-greedy for Changsha's mix
/// of Big Win patterns, and BotStrengthTests pins this ordering as the no-regression
/// baseline (see <c>vasquez-phase-i-wave-4.md</c>).
///
/// <para><b>Phase J Wave 1</b> promoted the rigorous shanten counter again — from
/// "discard tie-breaker only" to "claim acceptance gate". <see cref="DecideClaimPhase"/>
/// now consults <see cref="HandEvaluator.MinShantenToHu"/> on every non-Hu claim
/// opportunity (Pung / Kong / Chow) and only accepts the claim when the simulated
/// post-claim hand reports a strictly lower shanten than the current hand. A claim
/// that leaves shanten unchanged (or pushes it higher) is refused, eliminating the
/// prior heuristic-only path that occasionally turned strong hands into broken shapes
/// — most visibly the Phase F Chow heuristic that accepted shape-breaking chows
/// whenever the bot had &lt; 2 melds. Hu remains the unconditional fast-path (never
/// refused, irrespective of the shanten check). Among multiple shanten-dropping
/// claims the tie-breaker prefers Hu &gt; Kong &gt; Pung &gt; Chow, matching
/// <see cref="ChangshaClaimPriority.TierOf"/> ordering with an explicit Kong-over-Pung
/// preference since both share tier 2 there. The Chow simulation mirrors
/// <c>ChangshaGameStateMachine.RemoveChowTilesByLowestPattern</c> (the runtime's
/// chow-form picker when a bot supplies no explicit tile IDs) so the gate decision
/// reflects the chow shape that will actually be played.</para>
/// </summary>
public sealed class HardStrategy : IChangshaBotStrategy
{
    public string Difficulty => "hard";

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        // Hu when we can.
        var detector = new ChangshaWinDetector();
        var winResult = detector.Detect(hand, method: WinMethod.SelfDraw);
        if (winResult.IsWin)
            return BotAction.DeclareWin();

        // Conservative kongs — only when the hand still has room.
        if (HandEvaluator.CountLooseTiles(hand) >= 2)
        {
            var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
            if (kongLogical >= 0)
                return BotAction.DeclareConcealedKong(kongLogical);

            var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
            if (addedKongTile >= 0)
                return BotAction.DeclareAddedKong(addedKongTile);
        }

        return BotAction.Discard(SelectDiscardTile(hand, state));
    }

    public BotAction OnOtherDiscard(ChangshaGameState state, int botSeatIndex, int discarderSeat, int discardedTileId)
    {
        if (state.ClaimWindow is null) return BotAction.Pass();
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
        return DecideClaimPhase(state, hand, botSeatIndex);
    }

    public BotAction OnSelfDraw(ChangshaGameState state, int botSeatIndex)
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

        return BotAction.Wait();
    }

    public BotAction OnPickupCue(ChangshaGameState state, int botSeatIndex)
    {
        return BotAction.Wait();
    }

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

    private static BotAction DecideClaimPhase(ChangshaGameState state, ChangshaHandState hand, int botSeatIndex)
    {
        var opportunities = state.ClaimWindow!.Opportunities
            .Where(o => o.SeatIndex == botSeatIndex)
            .ToList();

        if (opportunities.Count == 0)
            return BotAction.Pass();

        // Hu is unconditional — a winning claim is never refused regardless of the
        // shanten gate below. (The gate would also accept Hu since post-Hu shanten
        // is by definition 0, but treating it as a fast-path keeps the gate code
        // pure and avoids an unnecessary simulation for the most common claim.)
        if (opportunities.Any(o => o.ClaimType == TableClaimType.Hu))
            return BotAction.Claim(TableClaimType.Hu);

        // Phase J Wave 1 shanten gate: only accept a Pung/Kong/Chow claim when
        // accepting strictly drops the bot's shanten (post-claim < pre-claim).
        // Same-or-worse shanten claims are refused — this eliminates the prior
        // heuristic-only path that occasionally turned strong hands into broken
        // shapes (most visibly the Phase F "fussy chow" rule).
        var preShanten = HandEvaluator.MinShantenToHu(hand, Array.Empty<int>());
        var discardTileId = state.ClaimWindow.DiscardTileId;
        var discardLogical = ChangshaDeckBuilder.GetLogicalTile(discardTileId);

        ChangshaClaimOpportunity? best = null;
        var bestRank = -1;

        foreach (var opp in opportunities)
        {
            var postShanten = opp.ClaimType switch
            {
                TableClaimType.Kong => ShantenAfterExposedKongClaim(hand, discardLogical, discardTileId),
                TableClaimType.Pung => ShantenAfterPungClaim(hand, discardLogical, discardTileId),
                TableClaimType.Chow => ShantenAfterChowClaim(hand, discardLogical, discardTileId),
                _ => int.MaxValue
            };

            if (postShanten >= preShanten)
                continue;

            // Tie-breaker: Hu > Kong > Pung > Chow. Hu was handled above; among the
            // remaining tiers Kong and Pung share TierOf == 2, so we lift Kong above
            // Pung explicitly via ClaimAcceptanceRank.
            var rank = ClaimAcceptanceRank(opp.ClaimType);
            if (rank > bestRank)
            {
                best = opp;
                bestRank = rank;
            }
        }

        return best is null ? BotAction.Pass() : BotAction.Claim(best.ClaimType);
    }

    /// <summary>
    /// Phase J Wave 1 tie-breaker ordering for claim acceptance: Hu &gt; Kong &gt;
    /// Pung &gt; Chow. Matches <see cref="ChangshaClaimPriority.TierOf"/> except that
    /// Kong is lifted strictly above Pung (both share tier 2 in the resolver because
    /// the runtime breaks Kong/Pung ties by CCW seat distance, but the bot's
    /// acceptance gate has no such constraint — when both shanten-drop, Kong is the
    /// stronger structural improvement since it commits four tiles instead of three).
    /// </summary>
    private static int ClaimAcceptanceRank(TableClaimType claimType) => claimType switch
    {
        TableClaimType.Hu => 4,
        TableClaimType.Kong => 3,
        TableClaimType.Pung => 2,
        TableClaimType.Chow => 1,
        _ => 0
    };

    /// <summary>
    /// Probes the shanten of the hand assuming the bot claims a Pung on
    /// <paramref name="discardTileId"/>: 2 concealed copies of
    /// <paramref name="discardLogical"/> are moved into a new declared meld alongside
    /// the discard. Returns <see cref="int.MaxValue"/> if the bot doesn't actually
    /// hold the matching pair (defensive; the adjudicator already filters).
    /// </summary>
    private static int ShantenAfterPungClaim(ChangshaHandState hand, int discardLogical, int discardTileId)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryRemoveByLogical(concealedAfter, discardLogical, 2))
            return int.MaxValue;
        return ProbeShantenWithExtraMeld(hand, concealedAfter, MeldKind.Pung, discardTileId);
    }

    /// <summary>
    /// Probes the shanten of the hand assuming the bot claims an exposed Kong on
    /// <paramref name="discardTileId"/>: 3 concealed copies move into a new declared
    /// meld alongside the discard. Note this models an <see cref="MeldKind.ExposedKong"/>
    /// (claim-from-discard) — concealed/added kongs come from the bot's own draw and
    /// don't flow through the claim-window opportunity list.
    /// </summary>
    private static int ShantenAfterExposedKongClaim(ChangshaHandState hand, int discardLogical, int discardTileId)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryRemoveByLogical(concealedAfter, discardLogical, 3))
            return int.MaxValue;
        return ProbeShantenWithExtraMeld(hand, concealedAfter, MeldKind.ExposedKong, discardTileId);
    }

    /// <summary>
    /// Probes the shanten of the hand assuming the bot claims a Chow on
    /// <paramref name="discardTileId"/>. Mirrors
    /// <c>ChangshaGameStateMachine.RemoveChowTilesByLowestPattern</c> — the runtime
    /// resolves bot chow claims (which never supply explicit tile IDs) by walking
    /// the three possible chow shapes in lowest-rank-first order and taking the
    /// first viable one. Replicating that selection here means the gate decision
    /// reflects the chow shape that will actually be played, not an idealised best
    /// case. Returns <see cref="int.MaxValue"/> if no chow form is mechanically
    /// possible (defensive; the adjudicator only surfaces Chow when at least one is).
    /// </summary>
    private static int ShantenAfterChowClaim(ChangshaHandState hand, int discardLogical, int discardTileId)
    {
        var rank = discardLogical % 9;
        var patterns = new List<(int A, int B)>();
        if (rank >= 2) patterns.Add((discardLogical - 2, discardLogical - 1));
        if (rank >= 1 && rank <= 7) patterns.Add((discardLogical - 1, discardLogical + 1));
        if (rank <= 6) patterns.Add((discardLogical + 1, discardLogical + 2));

        foreach (var (a, b) in patterns)
        {
            var concealedAfter = new List<int>(hand.ConcealedTiles);
            if (TryRemoveByLogical(concealedAfter, a, 1) && TryRemoveByLogical(concealedAfter, b, 1))
            {
                return ProbeShantenWithExtraMeld(hand, concealedAfter, MeldKind.Chow, discardTileId);
            }
        }
        return int.MaxValue;
    }

    /// <summary>
    /// Removes <paramref name="count"/> tiles whose logical id matches
    /// <paramref name="logical"/> from <paramref name="tiles"/> in place. Returns
    /// <c>false</c> when fewer than <paramref name="count"/> matches exist (in which
    /// case the partial removal is harmless because the list is a throwaway clone).
    /// </summary>
    private static bool TryRemoveByLogical(List<int> tiles, int logical, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var idx = tiles.FindIndex(t => ChangshaDeckBuilder.GetLogicalTile(t) == logical);
            if (idx < 0) return false;
            tiles.RemoveAt(idx);
        }
        return true;
    }

    /// <summary>
    /// Constructs a probe <see cref="ChangshaHandState"/> with the supplied
    /// post-claim concealed list and an extra declared meld, then runs the rigorous
    /// shanten counter. Only <c>Melds.Count</c> influences
    /// <see cref="HandEvaluator.MinShantenToHu"/>'s standard-shape decomposition (the
    /// groupsNeeded budget) and its SevenPairs path (which disqualifies any hand
    /// with declared melds), so the placeholder meld is content-free apart from
    /// carrying the discard tile for traceability.
    /// </summary>
    private static int ProbeShantenWithExtraMeld(
        ChangshaHandState hand,
        List<int> concealedAfter,
        MeldKind kind,
        int discardTileId)
    {
        var simulatedMelds = new List<Meld>(hand.Melds.Count + 1);
        simulatedMelds.AddRange(hand.Melds);
        simulatedMelds.Add(new Meld
        {
            Kind = kind,
            TileIds = new List<int> { discardTileId }
        });
        var probe = new ChangshaHandState
        {
            SeatIndex = hand.SeatIndex,
            ConcealedTiles = concealedAfter,
            Melds = simulatedMelds
        };
        return HandEvaluator.MinShantenToHu(probe, Array.Empty<int>());
    }

    private static int SelectDiscardTile(ChangshaHandState hand, ChangshaGameState state)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var discardedLogicals = HandEvaluator.CollectDiscardedLogicals(state);

        // Phase I Wave 4 — keep-score remains the primary discard heuristic
        // (defensive bias + neighbour preservation has been the production
        // baseline since Phase F and is what BotStrengthTests pins). The proper
        // shanten counter from this wave breaks ties between equally-attractive
        // candidates: when keep-score is identical, prefer the discard whose
        // removal keeps post-discard shanten lowest.
        var shantenByLogical = new Dictionary<int, int>();
        foreach (var logical in logicalCounts.Keys)
        {
            shantenByLogical[logical] = ShantenAfterDiscardingLogical(logical, hand);
        }

        return hand.ConcealedTiles
            .OrderBy(t => ComputeDiscardScore(t, logicalCounts, discardedLogicals))
            .ThenBy(t => shantenByLogical[ChangshaDeckBuilder.GetLogicalTile(t)])
            .ThenByDescending(t => t)
            .First();
    }

    /// <summary>
    /// Probes the shanten of the hand assuming one tile of the given logical id is
    /// discarded. Clones the concealed list to avoid mutating the live hand; melds
    /// are reference-shared because <see cref="HandEvaluator.MinShantenToHu"/> only
    /// reads <c>Melds.Count</c>. Returns <see cref="int.MaxValue"/> if no tile of
    /// that logical id is present (defensive; the caller already filters).
    /// </summary>
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

    /// <summary>
    /// Lower score = more attractive to discard. Combines Medium's keep score with a
    /// defensive bonus for tiles already in the discard pile.
    /// </summary>
    private static int ComputeDiscardScore(
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

        // Defensive bonus: subtract from keep score (i.e., bias toward discard) when
        // this tile is already on the table — opponents have demonstrated they don't
        // need it.
        if (discardedLogicals.Contains(logical))
            keepScore -= 4;

        return keepScore;
    }
}
