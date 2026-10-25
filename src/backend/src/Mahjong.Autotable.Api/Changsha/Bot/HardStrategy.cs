using Mahjong.Autotable.Api.Changsha.Bot.Heuristics;
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
/// key was investigated and rolled back at the time — the Phase F keep-score
/// heuristic was statistically stronger than naive shanten-greedy because
/// claim-acceptance still ran on the legacy heuristic path that occasionally
/// broke strong hand shapes, which made shanten-greedy discards lose ground.
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
///
/// <para><b>Phase J Wave 4</b> re-investigated promoting shanten to the primary
/// discard key — the question was whether the Wave 1 claim-acceptance gate had
/// neutralised the Wave 4 (Phase I) rollback rationale. The seed-40595 hang noted
/// in Vasquez's Wave 2 review no longer reproduces on any seed in the
/// <c>BotStrengthTests</c> i=0..19 range (verified with shanten-primary +
/// <c>ComputeDiscardScore</c> tie-breaker + tile-id stable tertiary), and a
/// Hard-vs-3×Medium probe at the same seeds measured 7 / 20 Hard wins under
/// shanten-primary vs 4 / 20 under keep-score-primary (a meaningful lift). The
/// <see cref="SelectDiscardTile"/> ordering is now
/// <c>shanten → keep-score → tile-id-desc</c> — the bot strictly minimises
/// post-discard shanten and uses the Phase F keep-score / safety bias as the
/// tie-breaker. <c>BotStrengthTests</c> + the full xunit suite remain green.
/// See <c>.squad/decisions/inbox/bishop-phase-j-wave-4.md</c> for the full
/// investigation memo.</para>
/// </summary>
public sealed class HardStrategy : IChangshaBotStrategy
{
    private const int OffRankPairShantenTolerance = 2;
    private const int OffRankPairScoreTolerance = 2;
    private const int ConnectedSmallWinBridgeShantenTolerance = 1;
    // Allow a slightly wider score gap when upgrading from partial to full
    // dangerous-opponent genbutsu on the same shanten tier. Full coverage
    // materially reduces deal-in risk and is worth a small shape concession.
    private const int FullDangerousGenbutsuScoreTolerance = 3;

    private readonly record struct DiscardCandidate(int TileId, int Logical, int Shanten, int Score);
    public string Difficulty => "hard";

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        // Hu when we can.
        if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, botSeatIndex))
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

        return BotAction.Discard(SelectDiscardTile(hand, state, botSeatIndex));
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
        if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, botSeatIndex))
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

    /// <summary>
    /// Phase J Wave 10 — explainable variant. Hard's reasoning surfaces
    /// the shanten-primary ordering and the defensive bias (discarded-
    /// logical penalty) that the Wave 1 acceptance gate enabled. Score
    /// is post-discard shanten when discarding; the audit replay surfaces
    /// it so an operator can spot "shanten stayed at 2 for three turns".
    /// </summary>
    public BotDecision DecideWithReasoning(ChangshaGameState state, int botSeatIndex)
    {
        var reasoning = new List<string> { "strategy:hard" };

        if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == botSeatIndex)
        {
            var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
            if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, botSeatIndex))
            {
                reasoning.Add("winning hand detected on self-draw");
                return new BotDecision(BotAction.DeclareWin(), null, Score: 0, reasoning);
            }

            if (HandEvaluator.CountLooseTiles(hand) >= 2)
            {
                var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
                if (kongLogical >= 0)
                {
                    reasoning.Add($"concealed kong (loose-tile guard ≥2): logical={kongLogical}");
                    return new BotDecision(BotAction.DeclareConcealedKong(kongLogical), null, Score: 0, reasoning);
                }
                var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
                if (addedKongTile >= 0)
                {
                    reasoning.Add($"added kong (loose-tile guard ≥2): tile={addedKongTile}");
                    return new BotDecision(BotAction.DeclareAddedKong(addedKongTile), addedKongTile, Score: 0, reasoning);
                }
            }

            var tileId = SelectDiscardTile(hand, state, botSeatIndex);
            var logicalCounts = hand.ConcealedTiles
                .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
                .ToDictionary(g => g.Key, g => g.Count());
            var safetyDiscardLogicals = CollectSafetyDiscardLogicals(state, botSeatIndex);
            var keepScore = ComputeDiscardScore(tileId, logicalCounts, safetyDiscardLogicals);
            var postShanten = ShantenAfterDiscardingLogical(ChangshaDeckBuilder.GetLogicalTile(tileId), hand);
            reasoning.Add($"shanten-primary: post-discard shanten={postShanten}");
            reasoning.Add($"keep-score tie-breaker={keepScore} (defensive bias: opponent-discarded logical -4)");
            if (safetyDiscardLogicals.Contains(ChangshaDeckBuilder.GetLogicalTile(tileId)))
            {
                reasoning.Add("safety: tile already in safety discard set (reduced claim risk)");
            }
            return new BotDecision(BotAction.Discard(tileId), tileId, Score: postShanten, reasoning);
        }

        if (state.Phase == ChangshaPhase.AwaitingClaim && state.ClaimWindow is not null)
        {
            var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
            var preShanten = HandEvaluator.MinShantenToHu(hand, Array.Empty<int>());
            reasoning.Add($"pre-claim shanten={preShanten}");
            var action = DecideClaimPhase(state, hand, botSeatIndex);
            if (action.Type == BotActionType.Claim)
            {
                reasoning.Add($"shanten gate: claim accepted (strict shanten drop) type={action.ClaimType}");
            }
            else
            {
                reasoning.Add("shanten gate: no claim drops shanten — pass");
            }
            return new BotDecision(action, action.TileId, Score: preShanten, reasoning);
        }

        if (ChangshaGameStateMachine.IsPickupPhase(state.Phase) && state.PickupSeatIndex == botSeatIndex)
        {
            reasoning.Add("pickup phase: take expected wall slice");
            return new BotDecision(BotAction.Wait(), null, Score: 0, reasoning);
        }

        reasoning.Add("no decision required this tick (wait)");
        return new BotDecision(BotAction.Wait(), null, Score: 0, reasoning);
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
        //
        // Phase K Wave 25: for non-Hu claims, avoid claims that worsen the
        // hand's 2/5/8-eye readiness (Changsha small-win pair route). This
        // prevents shape-improving but eye-breaking Chow/Pung/Kong claims from
        // being accepted on shanten alone.
        var preShanten = HandEvaluator.MinShantenToHu(hand, Array.Empty<int>());
        var preSmallWinPairDistance = ComputeSmallWinPairDistance(hand.ConcealedTiles);
        var externallyVisibleLogicalCounts = CollectExternallyVisibleLogicalCountsForClaimOuts(state, botSeatIndex);
        var discardTileId = state.ClaimWindow.DiscardTileId;
        var discardLogical = ChangshaDeckBuilder.GetLogicalTile(discardTileId);

        ChangshaClaimOpportunity? best = null;
        int[]? bestClaimTileIds = null;
        var bestPostShanten = int.MaxValue;
        var bestPostSmallWinPairDistance = int.MaxValue;
        var bestLiveWinningOuts = int.MinValue;
        var bestRank = int.MinValue;

        foreach (var opp in opportunities)
        {
            var (postShanten, postSmallWinPairDistance, postLiveWinningOuts, claimTileIds) = opp.ClaimType switch
            {
                TableClaimType.Kong => ProbeAfterExposedKongClaim(hand, discardLogical, discardTileId, externallyVisibleLogicalCounts),
                TableClaimType.Pung => ProbeAfterPungClaim(hand, discardLogical, discardTileId, externallyVisibleLogicalCounts),
                TableClaimType.Chow => ProbeAfterChowClaim(hand, discardLogical, discardTileId, externallyVisibleLogicalCounts),
                _ => (int.MaxValue, int.MaxValue, int.MinValue, null)
            };

            if (postShanten >= preShanten)
                continue;

            if (opp.ClaimType is TableClaimType.Chow or TableClaimType.Pung or TableClaimType.Kong
                && postSmallWinPairDistance > preSmallWinPairDistance)
                continue;

            // Phase K Wave 26.1: among claims that pass the strict-drop + small-win
            // gate, prefer stronger structural outcomes first: lower post-claim
            // shanten, then tighter 2/5/8-eye readiness, then higher immediate live
            // winning outs. Claim tier (Hu>Kong>Pung>Chow) remains the final tie-break.
            var rank = ClaimAcceptanceRank(opp.ClaimType);
            var isBetter = best is null
                || postShanten < bestPostShanten
                || (postShanten == bestPostShanten
                    && postSmallWinPairDistance < bestPostSmallWinPairDistance)
                || (postShanten == bestPostShanten
                    && postSmallWinPairDistance == bestPostSmallWinPairDistance
                    && postLiveWinningOuts > bestLiveWinningOuts)
                || (postShanten == bestPostShanten
                    && postSmallWinPairDistance == bestPostSmallWinPairDistance
                    && postLiveWinningOuts == bestLiveWinningOuts
                    && rank > bestRank);

            if (!isBetter)
                continue;

            best = opp;
            bestClaimTileIds = claimTileIds;
            bestPostShanten = postShanten;
            bestPostSmallWinPairDistance = postSmallWinPairDistance;
            bestLiveWinningOuts = postLiveWinningOuts;
            bestRank = rank;
        }

        return best is null ? BotAction.Pass() : BotAction.Claim(best.ClaimType, bestClaimTileIds);
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
    /// Probes the post-claim hand assuming the bot claims a Pung on
    /// <paramref name="discardTileId"/>: 2 concealed copies of
    /// <paramref name="discardLogical"/> are moved into a new declared meld alongside
    /// the discard. Returns (<see cref="int.MaxValue"/>, <see cref="int.MaxValue"/>)
    /// if the bot doesn't actually hold the matching pair (defensive; the
    /// adjudicator already filters).
    /// </summary>
    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterPungClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId,
        IReadOnlyDictionary<int, int> visibleLogicalCounts)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryTakeTilesByLogical(concealedAfter, discardLogical, 2, out var claimedTileIds))
            return (int.MaxValue, int.MaxValue, int.MinValue, null);

        claimedTileIds.Add(discardTileId);
        var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.Pung, claimedTileIds, visibleLogicalCounts);
        return (probe.Shanten, probe.SmallWinPairDistance, probe.LiveWinningOuts, null);
    }

    /// <summary>
    /// Probes the post-claim hand assuming the bot claims an exposed Kong on
    /// <paramref name="discardTileId"/>: 3 concealed copies move into a new declared
    /// meld alongside the discard. Note this models an <see cref="MeldKind.ExposedKong"/>
    /// (claim-from-discard) — concealed/added kongs come from the bot's own draw and
    /// don't flow through the claim-window opportunity list.
    /// </summary>
    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterExposedKongClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId,
        IReadOnlyDictionary<int, int> visibleLogicalCounts)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryTakeTilesByLogical(concealedAfter, discardLogical, 3, out var claimedTileIds))
            return (int.MaxValue, int.MaxValue, int.MinValue, null);

        claimedTileIds.Add(discardTileId);
        var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.ExposedKong, claimedTileIds, visibleLogicalCounts);
        return (probe.Shanten, probe.SmallWinPairDistance, probe.LiveWinningOuts, null);
    }

    /// <summary>
    /// Probes all mechanically legal Chow shapes for <paramref name="discardTileId"/>
    /// and returns the best post-claim outcome, together with the explicit concealed
    /// tile IDs for that Chow form. This keeps Hard/Master claim quality aligned with
    /// Medium's "best-shape" probing while still preserving Hard's claim-type ranking
    /// (Hu &gt; Kong &gt; Pung &gt; Chow) in <see cref="DecideClaimPhase"/>.
    /// </summary>
    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterChowClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId,
        IReadOnlyDictionary<int, int> visibleLogicalCounts)
    {
        var rank = discardLogical % 9;
        var patterns = new List<(int A, int B)>();
        if (rank >= 2) patterns.Add((discardLogical - 2, discardLogical - 1));
        if (rank >= 1 && rank <= 7) patterns.Add((discardLogical - 1, discardLogical + 1));
        if (rank <= 6) patterns.Add((discardLogical + 1, discardLogical + 2));

        var best = (Shanten: int.MaxValue, SmallWinPairDistance: int.MaxValue, LiveWinningOuts: int.MinValue, ClaimTileIds: (int[]?)null);
        foreach (var (a, b) in patterns)
        {
            var concealedAfter = new List<int>(hand.ConcealedTiles);
            if (!TryTakeTileByLogical(concealedAfter, a, out var tileA)
                || !TryTakeTileByLogical(concealedAfter, b, out var tileB))
                continue;

            var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.Chow, new[] { tileA, tileB, discardTileId }, visibleLogicalCounts);
            var isBetter = probe.Shanten < best.Shanten
                || (probe.Shanten == best.Shanten
                    && probe.SmallWinPairDistance < best.SmallWinPairDistance)
                || (probe.Shanten == best.Shanten
                    && probe.SmallWinPairDistance == best.SmallWinPairDistance
                    && probe.LiveWinningOuts > best.LiveWinningOuts);

            if (isBetter)
                best = (probe.Shanten, probe.SmallWinPairDistance, probe.LiveWinningOuts, new[] { tileA, tileB });
        }

        return best;
    }

    /// <summary>
    /// Removes <paramref name="count"/> tiles whose logical id matches
    /// <paramref name="logical"/> from <paramref name="tiles"/> in place. Returns
    /// <c>false</c> when fewer than <paramref name="count"/> matches exist (in which
    /// case the partial removal is harmless because the list is a throwaway clone).
    /// </summary>
    private static bool TryTakeTilesByLogical(List<int> tiles, int logical, int count, out List<int> takenTileIds)
    {
        takenTileIds = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            var idx = tiles.FindIndex(t => ChangshaDeckBuilder.GetLogicalTile(t) == logical);
            if (idx < 0) return false;
            takenTileIds.Add(tiles[idx]);
            tiles.RemoveAt(idx);
        }
        return true;
    }

    private static bool TryTakeTileByLogical(List<int> tiles, int logical, out int tileId)
    {
        tileId = -1;

        var idx = tiles.FindIndex(t => ChangshaDeckBuilder.GetLogicalTile(t) == logical);
        if (idx < 0)
            return false;

        tileId = tiles[idx];
        tiles.RemoveAt(idx);
        return true;
    }

    /// <summary>
    /// Constructs a probe <see cref="ChangshaHandState"/> with the supplied
    /// post-claim concealed list and an extra declared meld, then evaluates both
    /// shanten and 2/5/8-eye readiness for claim-gate decisions.
    /// </summary>
    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts) ProbeClaimOutcomeWithExtraMeld(
        ChangshaHandState hand,
        List<int> concealedAfter,
        MeldKind kind,
        IReadOnlyCollection<int> meldTileIds,
        IReadOnlyDictionary<int, int> visibleLogicalCounts)
    {
        var simulatedMelds = new List<Meld>(hand.Melds.Count + 1);
        simulatedMelds.AddRange(hand.Melds);
        simulatedMelds.Add(new Meld
        {
            Kind = kind,
            TileIds = new List<int>(meldTileIds)
        });
        var probe = new ChangshaHandState
        {
            SeatIndex = hand.SeatIndex,
            ConcealedTiles = concealedAfter,
            Melds = simulatedMelds
        };

        return (
            HandEvaluator.MinShantenToHu(probe, Array.Empty<int>()),
            ComputeSmallWinPairDistance(concealedAfter),
            CountLiveWinningOuts(probe, visibleLogicalCounts));
    }

    private static int CountLiveWinningOuts(ChangshaHandState hand, IReadOnlyDictionary<int, int> externallyVisibleLogicalCounts)
    {
        var currentShanten = HandEvaluator.MinShantenToHu(hand, Array.Empty<int>());
        var ownedLogicalCounts = new Dictionary<int, int>();

        void AddOwnedTile(int tileId)
        {
            var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
            ownedLogicalCounts[logical] = ownedLogicalCounts.GetValueOrDefault(logical, 0) + 1;
        }

        foreach (var tileId in hand.ConcealedTiles)
            AddOwnedTile(tileId);

        foreach (var meld in hand.Melds)
        {
            foreach (var tileId in meld.TileIds)
                AddOwnedTile(tileId);
        }

        var liveWinningOuts = 0;
        for (var logical = 0; logical < ChangshaDeckBuilder.LogicalTileCount; logical++)
        {
            var unavailableCopies = externallyVisibleLogicalCounts.GetValueOrDefault(logical, 0)
                                  + ownedLogicalCounts.GetValueOrDefault(logical, 0);
            var remainingCopies = ChangshaDeckBuilder.CopiesPerTile - unavailableCopies;
            if (remainingCopies <= 0)
                continue;

            var probe = new ChangshaHandState
            {
                SeatIndex = hand.SeatIndex,
                ConcealedTiles = new List<int>(hand.ConcealedTiles)
                {
                    logical * ChangshaDeckBuilder.CopiesPerTile
                },
                Melds = hand.Melds
            };

            var shantenAfterDraw = HandEvaluator.MinShantenToHu(probe, Array.Empty<int>());
            if (shantenAfterDraw < currentShanten)
                liveWinningOuts += remainingCopies;
        }

        return liveWinningOuts;
    }

    /// <summary>
    /// Small-win eye readiness distance for concealed tiles:
    /// 0 = already holds a 2/5/8 pair, 1 = holds a 2/5/8 singleton, 2 = no 2/5/8 tiles.
    /// </summary>
    private static int ComputeSmallWinPairDistance(IReadOnlyList<int> concealedTileIds)
    {
        var counts = concealedTileIds
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var hasSingleton = false;
        foreach (var (logical, count) in counts)
        {
            if (!IsSmallWinRank(logical))
                continue;

            if (count >= 2)
                return 0;

            if (count == 1)
                hasSingleton = true;
        }

        return hasSingleton ? 1 : 2;
    }

    private static int SelectDiscardTile(ChangshaHandState hand, ChangshaGameState state, int botSeatIndex)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var discardedLogicals = CollectSafetyDiscardLogicals(state, botSeatIndex);
        var visibleLogicalCounts = CollectPublicLogicalCounts(state, hand, botSeatIndex);

        // Phase J Wave 4 — shanten is now the primary discard key. The Phase F
        // keep-score (defensive bias + neighbour preservation), then tenpai-aware
        // safety bias, and finally stable tile-id serve as tie-breakers. Promotion
        // was safe-gated by the Phase J Wave 1
        // claim-acceptance shanten gate (see DecideClaimPhase): with the bot
        // refusing shape-breaking claims, shanten-greedy discards no longer lose
        // ground to opponents capitalising on broken meld shapes. The seed-40595
        // hang noted in Vasquez's Wave 2 review no longer reproduces on any seed
        // in the BotStrengthTests i=0..19 range. See bishop-phase-j-wave-4.md.
        var shantenByLogical = new Dictionary<int, int>();
        foreach (var logical in logicalCounts.Keys)
        {
            shantenByLogical[logical] = ShantenAfterDiscardingLogical(logical, hand);
        }

        var scored = hand.ConcealedTiles
            .Select(tileId =>
            {
                var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
                return new DiscardCandidate(
                    tileId,
                    logical,
                    shantenByLogical[logical],
                    ComputeDiscardScore(tileId, logicalCounts, discardedLogicals));
            })
            .ToList();

        var best = scored
            .OrderBy(entry => entry.Shanten)
            .ThenBy(entry => entry.Score)
            .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenByDescending(entry => entry.TileId)
            .First();

        var selectedTileId = best.TileId;

        if (TryKeepConnectedSmallWinSingleton(logicalCounts, scored, best, visibleLogicalCounts, state, botSeatIndex, discardedLogicals, out var preserveTile))
        {
            selectedTileId = preserveTile;
        }
        else if (TrySelectIsolatedSmallWinDiscard(logicalCounts, scored, best, discardedLogicals, out var overrideTile))
        {
            selectedTileId = overrideTile;
        }

        if (TrySelectTenpaiSafeOffRankPair(
                logicalCounts,
                scored,
                selectedTileId,
                state,
                botSeatIndex,
                visibleLogicalCounts,
                discardedLogicals,
                out var saferTile))
        {
            selectedTileId = saferTile;
        }

        if (TryPromoteFullDangerousGenbutsuOnShantenTie(
                scored,
                selectedTileId,
                state,
                botSeatIndex,
                out var fullSafetyTileId))
        {
            selectedTileId = fullSafetyTileId;
        }

        if (TryPromoteDeadSmallWinSingletonDiscard(
                logicalCounts,
                visibleLogicalCounts,
                scored,
                selectedTileId,
                state,
                botSeatIndex,
                discardedLogicals,
                out var deadSmallWinTileId))
        {
            selectedTileId = deadSmallWinTileId;
        }

        return selectedTileId;
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
    /// defensive bonus for tiles already discarded by opponents.
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
        // this tile is already in opponent discards — reduced claim/feed risk.
        if (discardedLogicals.Contains(logical))
            keepScore -= 4;

        return keepScore;
    }

    private static bool TryKeepConnectedSmallWinSingleton(
        Dictionary<int, int> logicalCounts,
        List<DiscardCandidate> scored,
        DiscardCandidate best,
        Dictionary<int, int> visibleLogicalCounts,
        ChangshaGameState state,
        int botSeatIndex,
        HashSet<int> opponentDiscardLogicals,
        out int tileId)
    {
        tileId = -1;

        if (!HasSingleSmallWinTile(logicalCounts))
            return false;

        if (!IsSmallWinRank(best.Logical) || logicalCounts.GetValueOrDefault(best.Logical, 0) != 1)
            return false;

        if (!IsBridgeBetweenOffRankPairs(best.Logical, logicalCounts))
            return false;

        // If three copies of the bridge singleton are already publicly visible,
        // only one live draw remains. That near-dead lane is too thin to justify
        // breaking both neighboring off-rank pairs.
        if (IsLogicalNearExhausted(best.Logical, visibleLogicalCounts))
            return false;

        var alternate = scored
            .Where(entry => entry.TileId != best.TileId && !IsSmallWinRank(entry.Logical))
            .OrderBy(entry => entry.Shanten)
            .ThenBy(entry => entry.Score)
            .ThenBy(entry => OpponentSafetyTieBreaker(entry.TileId, opponentDiscardLogicals))
            .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenByDescending(entry => entry.TileId)
            .Select(entry => (DiscardCandidate?)entry)
            .FirstOrDefault();

        if (alternate is null)
            return false;

        if (alternate.Value.Shanten > best.Shanten + ConnectedSmallWinBridgeShantenTolerance)
            return false;

        var dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
        if (dangerousOpponents.Count > 0)
        {
            var bestTenpaiSafety = TenpaiDetector.SafetyBias(best.TileId, state, botSeatIndex);
            var alternateTenpaiSafety = TenpaiDetector.SafetyBias(alternate.Value.TileId, state, botSeatIndex);
            if (alternateTenpaiSafety > bestTenpaiSafety)
                return false;

            if (alternateTenpaiSafety == bestTenpaiSafety)
            {
                var bestOpponentSafety = OpponentSafetyTieBreaker(best.TileId, opponentDiscardLogicals);
                var alternateOpponentSafety = OpponentSafetyTieBreaker(alternate.Value.TileId, opponentDiscardLogicals);
                if (alternateOpponentSafety > bestOpponentSafety)
                    return false;
            }
        }

        tileId = alternate.Value.TileId;
        return true;
    }

    private static bool TrySelectIsolatedSmallWinDiscard(
        Dictionary<int, int> logicalCounts,
        List<DiscardCandidate> scored,
        DiscardCandidate best,
        HashSet<int> discardedLogicals,
        out int tileId)
    {
        tileId = -1;
        if (!HasSingleSmallWinTile(logicalCounts)) return false;

        var offRankPairs = logicalCounts
            .Where(entry => entry.Value > 1 && !IsSmallWinRank(entry.Key))
            .Select(entry => entry.Key)
            .ToHashSet();
        if (offRankPairs.Count == 0) return false;

        var smallWinSingleton = default(DiscardCandidate);
        var hasSmallWinSingleton = false;
        foreach (var entry in scored)
        {
            if (IsSmallWinRank(entry.Logical)
                && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                && (IsIsolatedSingleton(entry.Logical, logicalCounts)
                    || IsSingletonWithOnlyPairedNeighbors(entry.Logical, logicalCounts, offRankPairs)))
            {
                smallWinSingleton = entry;
                hasSmallWinSingleton = true;
                break;
            }
        }
        if (!hasSmallWinSingleton) return false;

        var bestIsOffRankPair = logicalCounts.GetValueOrDefault(best.Logical, 0) > 1
            && !IsSmallWinRank(best.Logical);
        if (!bestIsOffRankPair) return false;

        if (smallWinSingleton.Shanten > best.Shanten + OffRankPairShantenTolerance)
            return false;

        var adjustedBestScore = best.Score + (discardedLogicals.Contains(best.Logical) ? 4 : 0);
        var adjustedSmallWinScore = smallWinSingleton.Score + (discardedLogicals.Contains(smallWinSingleton.Logical) ? 4 : 0);
        if (adjustedSmallWinScore > adjustedBestScore + OffRankPairScoreTolerance)
            return false;

        tileId = smallWinSingleton.TileId;
        return true;
    }

    private static bool TrySelectTenpaiSafeOffRankPair(
        Dictionary<int, int> logicalCounts,
        List<DiscardCandidate> scored,
        int selectedTileId,
        ChangshaGameState state,
        int botSeatIndex,
        Dictionary<int, int> visibleLogicalCounts,
        HashSet<int> opponentDiscardLogicals,
        out int tileId)
    {
        tileId = -1;

        var dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
        if (dangerousOpponents.Count == 0)
            return false;

        var selected = scored.FirstOrDefault(entry => entry.TileId == selectedTileId);
        if (selected.TileId != selectedTileId)
            return false;

        if (!IsSmallWinRank(selected.Logical) || logicalCounts.GetValueOrDefault(selected.Logical, 0) != 1)
            return false;

        // Near-dead singleton (three copies already visible) has only one live
        // draw left. Keep that discard instead of swapping to an off-rank pair
        // purely for dangerous-opponent genbutsu bias.
        if (IsLogicalNearExhausted(selected.Logical, visibleLogicalCounts)
            && !IsLogicalDead(selected.Logical, visibleLogicalCounts)
            && !IsBridgeBetweenOffRankPairs(selected.Logical, logicalCounts))
            return false;

        // Even when a 2/5/8 singleton is dead for improvement (all copies are
        // already accounted for from our perspective), discarding that last copy
        // can still deal in. Allow this path to swap to an off-rank pair when the
        // pair is strictly safer against dangerous opponents.
        var saferOffRankPair = scored
            .Where(entry => logicalCounts.GetValueOrDefault(entry.Logical, 0) > 1 && !IsSmallWinRank(entry.Logical))
            .Where(entry => entry.Shanten <= selected.Shanten + OffRankPairShantenTolerance)
            .OrderBy(entry => OpponentSafetyTieBreaker(entry.TileId, opponentDiscardLogicals))
            .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenBy(entry => entry.Shanten)
            .ThenBy(entry => entry.Score)
            .ThenByDescending(entry => entry.TileId)
            .Select(entry => (DiscardCandidate?)entry)
            .FirstOrDefault();

        if (saferOffRankPair is null)
            return false;

        if (saferOffRankPair.Value.Shanten > selected.Shanten + OffRankPairShantenTolerance)
            return false;

        var selectedOpponentSafety = OpponentSafetyTieBreaker(selected.TileId, opponentDiscardLogicals);
        var pairOpponentSafety = OpponentSafetyTieBreaker(saferOffRankPair.Value.TileId, opponentDiscardLogicals);
        if (pairOpponentSafety > selectedOpponentSafety)
            return false;

        var selectedTenpaiSafety = TenpaiDetector.SafetyBias(selected.TileId, state, botSeatIndex);
        var pairTenpaiSafety = TenpaiDetector.SafetyBias(saferOffRankPair.Value.TileId, state, botSeatIndex);
        if (pairOpponentSafety == selectedOpponentSafety && pairTenpaiSafety > selectedTenpaiSafety)
            return false;

        if (pairOpponentSafety == selectedOpponentSafety && pairTenpaiSafety == selectedTenpaiSafety)
            return false;

        tileId = saferOffRankPair.Value.TileId;
        return true;
    }

    private static bool TryPromoteFullDangerousGenbutsuOnShantenTie(
        List<DiscardCandidate> scored,
        int selectedTileId,
        ChangshaGameState state,
        int botSeatIndex,
        out int tileId)
    {
        tileId = -1;

        var dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
        if (dangerousOpponents.Count < 2)
            return false;

        var selected = scored.FirstOrDefault(entry => entry.TileId == selectedTileId);
        if (selected.TileId != selectedTileId)
            return false;

        var selectedSafety = TenpaiDetector.SafetyBias(selected.TileId, state, botSeatIndex);
        if (selectedSafety <= -2)
            return false;

        DiscardCandidate? fullSafetyCandidate = null;
        foreach (var entry in scored
                     .Where(entry => entry.TileId != selectedTileId && entry.Shanten == selected.Shanten)
                     .OrderBy(entry => entry.Score)
                     .ThenByDescending(entry => entry.TileId))
        {
            if (TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex) <= -2)
            {
                fullSafetyCandidate = entry;
                break;
            }
        }

        if (fullSafetyCandidate is null)
            return false;

        if (fullSafetyCandidate.Value.Score > selected.Score + FullDangerousGenbutsuScoreTolerance)
            return false;

        tileId = fullSafetyCandidate.Value.TileId;
        return true;
    }

    private static bool TryPromoteDeadSmallWinSingletonDiscard(
        Dictionary<int, int> logicalCounts,
        Dictionary<int, int> visibleLogicalCounts,
        List<DiscardCandidate> scored,
        int selectedTileId,
        ChangshaGameState state,
        int botSeatIndex,
        HashSet<int> opponentDiscardLogicals,
        out int tileId)
    {
        tileId = -1;

        var selected = scored.FirstOrDefault(entry => entry.TileId == selectedTileId);
        if (selected.TileId != selectedTileId)
            return false;

        var deadSmallWinSingleton = scored
            .Where(entry =>
                entry.TileId != selectedTileId
                && IsSmallWinRank(entry.Logical)
                && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                && IsLogicalDead(entry.Logical, visibleLogicalCounts))
            .OrderBy(entry => entry.Shanten)
            .ThenBy(entry => OpponentSafetyTieBreaker(entry.TileId, opponentDiscardLogicals))
            .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenBy(entry => entry.Score)
            .ThenByDescending(entry => entry.TileId)
            .Select(entry => (DiscardCandidate?)entry)
            .FirstOrDefault();

        if (deadSmallWinSingleton is null)
            return false;

        var candidate = deadSmallWinSingleton.Value;
        if (candidate.Shanten > selected.Shanten)
            return false;

        var selectedOpponentSafety = OpponentSafetyTieBreaker(selected.TileId, opponentDiscardLogicals);
        var candidateOpponentSafety = OpponentSafetyTieBreaker(candidate.TileId, opponentDiscardLogicals);
        if (candidateOpponentSafety > selectedOpponentSafety)
            return false;

        var selectedTenpaiSafety = TenpaiDetector.SafetyBias(selected.TileId, state, botSeatIndex);
        var candidateTenpaiSafety = TenpaiDetector.SafetyBias(candidate.TileId, state, botSeatIndex);
        if (candidateTenpaiSafety > selectedTenpaiSafety)
            return false;

        if (candidateOpponentSafety == selectedOpponentSafety
            && candidateTenpaiSafety == selectedTenpaiSafety
            && selectedTenpaiSafety <= -2
            && candidate.Score > selected.Score)
            return false;

        if (candidate.Score > selected.Score + FullDangerousGenbutsuScoreTolerance)
            return false;

        tileId = candidate.TileId;
        return true;
    }

    private static bool IsSmallWinRank(int logical)
    {
        var rank = logical % 9 + 1;
        return rank is 2 or 5 or 8;
    }

    private static bool HasSingleSmallWinTile(Dictionary<int, int> logicalCounts)
    {
        var total = 0;
        foreach (var entry in logicalCounts)
        {
            if (IsSmallWinRank(entry.Key))
            {
                total += entry.Value;
                if (total > 1)
                    return false;
            }
        }

        return total == 1;
    }

    private static bool IsIsolatedSingleton(int logical, Dictionary<int, int> logicalCounts)
    {
        if (logicalCounts.GetValueOrDefault(logical, 0) != 1)
            return false;

        var rank = logical % 9;
        if (rank > 0 && logicalCounts.ContainsKey(logical - 1))
            return false;
        if (rank < 8 && logicalCounts.ContainsKey(logical + 1))
            return false;
        if (rank > 1 && logicalCounts.ContainsKey(logical - 2))
            return false;
        if (rank < 7 && logicalCounts.ContainsKey(logical + 2))
            return false;

        return true;
    }

    private static bool IsSingletonWithOnlyPairedNeighbors(
        int logical,
        Dictionary<int, int> logicalCounts,
        HashSet<int> offRankPairs)
    {
        if (logicalCounts.GetValueOrDefault(logical, 0) != 1)
            return false;

        var rank = logical % 9;
        var hasNeighbor = false;
        var neighbors = new List<int>();
        if (rank > 0) neighbors.Add(logical - 1);
        if (rank < 8) neighbors.Add(logical + 1);
        if (rank > 1) neighbors.Add(logical - 2);
        if (rank < 7) neighbors.Add(logical + 2);

        foreach (var neighbor in neighbors)
        {
            if (!logicalCounts.ContainsKey(neighbor))
                continue;
            hasNeighbor = true;
            if (!offRankPairs.Contains(neighbor))
                return false;
        }

        return hasNeighbor;
    }

    private static bool IsBridgeBetweenOffRankPairs(int logical, Dictionary<int, int> logicalCounts)
    {
        var rank = logical % 9;
        if (rank <= 0 || rank >= 8)
            return false;

        var left = logical - 1;
        var right = logical + 1;

        return logicalCounts.GetValueOrDefault(left, 0) > 1
            && logicalCounts.GetValueOrDefault(right, 0) > 1
            && !IsSmallWinRank(left)
            && !IsSmallWinRank(right);
    }

    private static Dictionary<int, int> CollectPublicLogicalCounts(
        ChangshaGameState state,
        ChangshaHandState hand,
        int botSeatIndex)
    {
        var counts = new Dictionary<int, int>();

        void AddTile(int tileId)
        {
            var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
            counts[logical] = counts.GetValueOrDefault(logical, 0) + 1;
        }

        foreach (var tileId in hand.ConcealedTiles)
            AddTile(tileId);

        foreach (var meld in hand.Melds)
        {
            foreach (var tileId in meld.TileIds)
                AddTile(tileId);
        }

        foreach (var discard in state.DiscardPile)
            AddTile(discard.TileId);

        foreach (var otherHand in state.Hands)
        {
            if (otherHand.SeatIndex == botSeatIndex)
                continue;

            foreach (var meld in otherHand.Melds)
            {
                if (meld.Kind == MeldKind.ConcealedKong)
                    continue;

                foreach (var tileId in meld.TileIds)
                    AddTile(tileId);
            }
        }

        return counts;
    }

    private static Dictionary<int, int> CollectExternallyVisibleLogicalCountsForClaimOuts(
        ChangshaGameState state,
        int botSeatIndex)
    {
        var counts = new Dictionary<int, int>();

        void AddTile(int tileId)
        {
            var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
            counts[logical] = counts.GetValueOrDefault(logical, 0) + 1;
        }

        foreach (var discard in state.DiscardPile)
            AddTile(discard.TileId);

        foreach (var otherHand in state.Hands)
        {
            if (otherHand.SeatIndex == botSeatIndex)
                continue;

            foreach (var meld in otherHand.Melds)
            {
                if (meld.Kind == MeldKind.ConcealedKong)
                    continue;

                foreach (var tileId in meld.TileIds)
                    AddTile(tileId);
            }
        }

        return counts;
    }

    private static bool IsLogicalNearExhausted(int logical, Dictionary<int, int> visibleLogicalCounts)
        => visibleLogicalCounts.GetValueOrDefault(logical, 0) >= 3;

    private static bool IsLogicalDead(int logical, Dictionary<int, int> visibleLogicalCounts)
        => visibleLogicalCounts.GetValueOrDefault(logical, 0) >= 4;

    // Lower is safer in ThenBy ordering.
    private static int OpponentSafetyTieBreaker(int tileId, HashSet<int> opponentDiscardLogicals)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        return opponentDiscardLogicals.Contains(logical) ? -1 : 0;
    }

    private static HashSet<int> CollectSafetyDiscardLogicals(ChangshaGameState state, int botSeatIndex)
    {
        var dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
        if (dangerousOpponents.Count > 0)
            return TenpaiDetector.CollectGenbutsuLogicals(state, dangerousOpponents);

        return CollectOpponentDiscardLogicals(state, botSeatIndex);
    }

    private static HashSet<int> CollectOpponentDiscardLogicals(ChangshaGameState state, int botSeatIndex)
    {
        var result = new HashSet<int>();
        foreach (var discard in state.DiscardPile)
        {
            if (discard.SeatIndex == botSeatIndex)
                continue;

            result.Add(ChangshaDeckBuilder.GetLogicalTile(discard.TileId));
        }

        return result;
    }
}
