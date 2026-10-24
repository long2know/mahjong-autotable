using Mahjong.Autotable.Api.Changsha.Bot.Heuristics;
using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F "Medium" difficulty strategy — a direct port of the heuristics from the
/// legacy <see cref="ChangshaBotPolicy"/>. Scores each candidate discard by how much
/// it contributes to potential pairs, sequences, and the 2/5/8 bias common in
/// Changsha rule sets. Claim selection keeps Medium's greedy Hu/Kong/Pung/Chow
/// ordering, but now refuses any non-Hu claim that worsens Changsha small-win
/// (2/5/8) eye readiness.
/// </summary>
/// <remarks>
/// This is the default difficulty when none is specified. The legacy
/// <see cref="ChangshaBotPolicy"/> still exists as a thin facade over this strategy
/// so that <c>BotMatchHarness</c> and existing acceptance tests keep working without
/// modification.
/// </remarks>
public sealed class MediumStrategy : IChangshaBotStrategy
{
    private const int SingleSmallWinDiscardTolerance = 2;
    private const int OffRankPairDiscardTolerance = 2;
    private const int DangerousGenbutsuScoreTolerance = 1;
    private const int FullDangerousGenbutsuScoreTolerance = 2;

    private readonly record struct DiscardCandidate(int TileId, int Logical, int Score);

    public string Difficulty => "medium";

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
        return DecideDiscardPhase(state, hand, botSeatIndex);
    }

    public BotAction OnOtherDiscard(ChangshaGameState state, int botSeatIndex, int discarderSeat, int discardedTileId)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
        if (state.ClaimWindow is null) return BotAction.Pass();
        return DecideClaimPhase(state, hand, botSeatIndex);
    }

    public BotAction OnSelfDraw(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, botSeatIndex))
            return BotAction.DeclareWin();

        var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
        if (kongLogical >= 0)
            return BotAction.DeclareConcealedKong(kongLogical);

        var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
        if (addedKongTile >= 0)
            return BotAction.DeclareAddedKong(addedKongTile);

        return BotAction.Wait();
    }

    public BotAction OnPickupCue(ChangshaGameState state, int botSeatIndex)
    {
        // Phase F §3 — bots always take the expected wall slice; the runtime
        // translates Wait during a pickup phase into a TakeTilesFromWall call.
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
    /// Phase J Wave 10 — explainable variant. Medium populates a reasoning
    /// list with the keep-score tier, kong/win detection, and claim-tier
    /// acceptance ordering so the audit replay surfaces "why" alongside
    /// "what". Score is the keep-score of the chosen tile (lower = better
    /// to discard), useful for ranking decisions across hands.
    /// </summary>
    public BotDecision DecideWithReasoning(ChangshaGameState state, int botSeatIndex)
    {
        var reasoning = new List<string> { "strategy:medium" };

        if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == botSeatIndex)
        {
            var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
            if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, botSeatIndex))
            {
                reasoning.Add("winning hand detected on self-draw");
                return new BotDecision(BotAction.DeclareWin(), null, Score: 0, reasoning);
            }

            var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
            if (kongLogical >= 0)
            {
                reasoning.Add($"concealed kong candidate: logical={kongLogical}");
                return new BotDecision(BotAction.DeclareConcealedKong(kongLogical), null, Score: 0, reasoning);
            }
            var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
            if (addedKongTile >= 0)
            {
                reasoning.Add($"added kong candidate: tile={addedKongTile}");
                return new BotDecision(BotAction.DeclareAddedKong(addedKongTile), addedKongTile, Score: 0, reasoning);
            }

            var (tileId, droppedSmallWinSingleton) = SelectDiscardTileWithMeta(hand, state, botSeatIndex);
            var logicalCounts = hand.ConcealedTiles
                .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
                .ToDictionary(g => g.Key, g => g.Count());
            var score = ComputeKeepScore(tileId, logicalCounts);
            reasoning.Add($"keep-score primary: discard tile={tileId} score={score} (lower=better to discard)");
            reasoning.Add("tie-breaker: tile-id descending");
            if (droppedSmallWinSingleton)
                reasoning.Add("changsha 258 guard: discarded weak/near-dead 2/5/8 singleton to preserve stronger structure");
            return new BotDecision(BotAction.Discard(tileId), tileId, Score: score, reasoning);
        }

        if (state.Phase == ChangshaPhase.AwaitingClaim && state.ClaimWindow is not null)
        {
            var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
            var action = DecideClaimPhase(state, hand, botSeatIndex);
            if (action.Type == BotActionType.Claim)
            {
                reasoning.Add($"claim accepted: type={action.ClaimType} (Hu>Kong>Pung>Chow priority)");
            }
            else
            {
                reasoning.Add("no claim opportunity matched gate (chow cap, shanten guard, or 2/5/8-eye guard)");
            }
            return new BotDecision(action, action.TileId, Score: 0, reasoning);
        }

        if (ChangshaGameStateMachine.IsPickupPhase(state.Phase) && state.PickupSeatIndex == botSeatIndex)
        {
            reasoning.Add("pickup phase: take expected wall slice");
            return new BotDecision(BotAction.Wait(), null, Score: 0, reasoning);
        }

        reasoning.Add("no decision required this tick (wait)");
        return new BotDecision(BotAction.Wait(), null, Score: 0, reasoning);
    }

    private static BotAction DecideDiscardPhase(ChangshaGameState state, ChangshaHandState hand, int botSeatIndex)
    {
        if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, hand.SeatIndex))
            return BotAction.DeclareWin();

        var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
        if (kongLogical >= 0)
            return BotAction.DeclareConcealedKong(kongLogical);

        var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
        if (addedKongTile >= 0)
            return BotAction.DeclareAddedKong(addedKongTile);

        var tileId = SelectDiscardTile(hand, state, botSeatIndex);
        return BotAction.Discard(tileId);
    }

    private static BotAction DecideClaimPhase(ChangshaGameState state, ChangshaHandState hand, int botSeatIndex)
    {
        var claimWindow = state.ClaimWindow!;
        var opportunities = claimWindow.Opportunities
            .Where(o => o.SeatIndex == botSeatIndex)
            .OrderByDescending(o => o.Priority)
            .ToList();

        if (opportunities.Count == 0)
            return BotAction.Pass();

        var preShanten = HandEvaluator.MinShantenToHu(hand, Array.Empty<int>());
        var preSmallWinPairDistance = ComputeSmallWinPairDistance(hand.ConcealedTiles);
        var discardTileId = claimWindow.DiscardTileId;
        var discardLogical = ChangshaDeckBuilder.GetLogicalTile(discardTileId);

        ChangshaClaimOpportunity? best = null;
        int[]? bestClaimTileIds = null;
        var bestPostShanten = int.MaxValue;
        var bestPostSmallWinPairDistance = int.MaxValue;
        var bestLiveWinningOuts = int.MinValue;
        var bestClaimRank = int.MinValue;

        foreach (var opp in opportunities)
        {
            if (opp.ClaimType == TableClaimType.Hu)
                return BotAction.Claim(TableClaimType.Hu);

            if (opp.ClaimType == TableClaimType.Chow && hand.Melds.Count >= 3)
                continue;

            var (postShanten, postSmallWinPairDistance, postLiveWinningOuts, claimTileIds) = opp.ClaimType switch
            {
                TableClaimType.Kong => ProbeAfterExposedKongClaim(hand, discardLogical, discardTileId),
                TableClaimType.Pung => ProbeAfterPungClaim(hand, discardLogical, discardTileId),
                TableClaimType.Chow => ProbeAfterChowClaim(hand, discardLogical, discardTileId),
                _ => (int.MaxValue, int.MaxValue, int.MinValue, null)
            };

            if (postShanten > preShanten)
                continue;

            if (postSmallWinPairDistance > preSmallWinPairDistance)
                continue;

            var claimRank = ClaimPreferenceRank(opp.ClaimType);
            var isBetter = best is null
                || postShanten < bestPostShanten
                || (postShanten == bestPostShanten && postSmallWinPairDistance < bestPostSmallWinPairDistance)
                || (postShanten == bestPostShanten
                    && postSmallWinPairDistance == bestPostSmallWinPairDistance
                    && postLiveWinningOuts > bestLiveWinningOuts)
                || (postShanten == bestPostShanten
                    && postSmallWinPairDistance == bestPostSmallWinPairDistance
                    && postLiveWinningOuts == bestLiveWinningOuts
                    && claimRank > bestClaimRank);

            if (!isBetter)
                continue;

            best = opp;
            bestClaimTileIds = claimTileIds;
            bestPostShanten = postShanten;
            bestPostSmallWinPairDistance = postSmallWinPairDistance;
            bestLiveWinningOuts = postLiveWinningOuts;
            bestClaimRank = claimRank;
        }

        return best is null ? BotAction.Pass() : BotAction.Claim(best.ClaimType, bestClaimTileIds);
    }

    private static int ClaimPreferenceRank(TableClaimType claimType) => claimType switch
    {
        TableClaimType.Hu => 4,
        TableClaimType.Kong => 3,
        TableClaimType.Pung => 2,
        TableClaimType.Chow => 1,
        _ => 0
    };

    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterPungClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryRemoveByLogical(concealedAfter, discardLogical, 2))
            return (int.MaxValue, int.MaxValue, int.MinValue, null);

        var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.Pung, discardTileId);
        return (probe.Shanten, probe.SmallWinPairDistance, probe.LiveWinningOuts, null);
    }

    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterExposedKongClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId)
    {
        var concealedAfter = new List<int>(hand.ConcealedTiles);
        if (!TryRemoveByLogical(concealedAfter, discardLogical, 3))
            return (int.MaxValue, int.MaxValue, int.MinValue, null);

        var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.ExposedKong, discardTileId);
        return (probe.Shanten, probe.SmallWinPairDistance, probe.LiveWinningOuts, null);
    }

    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts, int[]? ClaimTileIds) ProbeAfterChowClaim(
        ChangshaHandState hand,
        int discardLogical,
        int discardTileId)
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

            var probe = ProbeClaimOutcomeWithExtraMeld(hand, concealedAfter, MeldKind.Chow, discardTileId);
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

    private static bool TryRemoveByLogical(List<int> tiles, int logical, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var idx = tiles.FindIndex(t => ChangshaDeckBuilder.GetLogicalTile(t) == logical);
            if (idx < 0)
                return false;

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

    private static (int Shanten, int SmallWinPairDistance, int LiveWinningOuts) ProbeClaimOutcomeWithExtraMeld(
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

        return (
            HandEvaluator.MinShantenToHu(probe, Array.Empty<int>()),
            ComputeSmallWinPairDistance(concealedAfter),
            CountLiveWinningOuts(probe));
    }

    private static int CountLiveWinningOuts(ChangshaHandState hand)
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
            var owned = ownedLogicalCounts.GetValueOrDefault(logical, 0);
            if (owned >= ChangshaDeckBuilder.CopiesPerTile)
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
                liveWinningOuts += ChangshaDeckBuilder.CopiesPerTile - owned;
        }

        return liveWinningOuts;
    }

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

    /// <summary>Public so the legacy <see cref="ChangshaBotPolicy.SelectDiscardTile"/>
    /// facade and harness tests can call into the same implementation.</summary>
    public static int SelectDiscardTile(ChangshaHandState hand)
    {
        var result = SelectDiscardTileWithMeta(hand, state: null, botSeatIndex: -1);
        return result.TileId;
    }

    private static int SelectDiscardTile(ChangshaHandState hand, ChangshaGameState state, int botSeatIndex)
    {
        var result = SelectDiscardTileWithMeta(hand, state, botSeatIndex);
        return result.TileId;
    }

    private static (int TileId, bool DroppedIsolatedSmallWin) SelectDiscardTileWithMeta(
        ChangshaHandState hand,
        ChangshaGameState? state,
        int botSeatIndex)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        IReadOnlyList<int>? dangerousOpponents = null;
        HashSet<int>? safetyDiscardLogicals = null;
        if (state is not null && botSeatIndex >= 0)
        {
            dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
            // Keep Medium's tie-break safety-aware even before a strict
            // dangerous-opponent signal exists: use opponent-discarded logicals
            // as the fallback safety set, and automatically switch to
            // dangerous-opponent genbutsu once tenpai pressure appears.
            safetyDiscardLogicals = CollectSafetyDiscardLogicals(state, botSeatIndex, dangerousOpponents);
        }

        var scored = hand.ConcealedTiles
            .Select(tileId => new DiscardCandidate(
                tileId,
                ChangshaDeckBuilder.GetLogicalTile(tileId),
                ComputeKeepScore(tileId, logicalCounts)))
            .ToList();

        var best = scored
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => safetyDiscardLogicals is null ? 0 : OpponentSafetyTieBreaker(entry.TileId, safetyDiscardLogicals))
            .ThenBy(entry => state is null || botSeatIndex < 0 || dangerousOpponents is null || dangerousOpponents.Count == 0
                ? 0
                : TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenByDescending(entry => entry.TileId)
            .First();

        if (state is not null && botSeatIndex >= 0)
        {
            var visibleLogicalCounts = CollectPublicLogicalCounts(state, hand, botSeatIndex);

            var deadSmallWinSingleton = scored
                .Where(entry =>
                    IsSmallWinRank(entry.Logical)
                    && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                    && IsLogicalDead(entry.Logical, visibleLogicalCounts))
                .OrderBy(entry => safetyDiscardLogicals is null ? 0 : OpponentSafetyTieBreaker(entry.TileId, safetyDiscardLogicals))
                .ThenBy(entry => state is null || botSeatIndex < 0 || dangerousOpponents is null || dangerousOpponents.Count == 0
                    ? 0
                    : TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
                .ThenBy(entry => entry.Score)
                .ThenByDescending(entry => entry.TileId)
                .Select(entry => (DiscardCandidate?)entry)
                .FirstOrDefault();

            if (deadSmallWinSingleton is not null)
            {
                if (TryPromoteFullDangerousGenbutsuOnScoreTolerance(
                        scored,
                        deadSmallWinSingleton.Value.TileId,
                        state,
                        botSeatIndex,
                        out var promotedFullSafetyTileId))
                {
                    return (promotedFullSafetyTileId, false);
                }

                // Prefer dead-singleton shedding by default, but do not force it when
                // dangerous-opponent defense would get strictly worse than the current
                // top-scored candidate (e.g. best is dangerous-opponent genbutsu).
                if (dangerousOpponents is not null
                    && dangerousOpponents.Count > 0
                    && safetyDiscardLogicals is not null)
                {
                    var bestOpponentSafety = OpponentSafetyTieBreaker(best.TileId, safetyDiscardLogicals);
                    var singletonOpponentSafety = OpponentSafetyTieBreaker(deadSmallWinSingleton.Value.TileId, safetyDiscardLogicals);
                    if (singletonOpponentSafety > bestOpponentSafety)
                        return (best.TileId, false);

                    if (singletonOpponentSafety == bestOpponentSafety)
                    {
                        var bestTenpaiSafety = TenpaiDetector.SafetyBias(best.TileId, state, botSeatIndex);
                        var singletonTenpaiSafety = TenpaiDetector.SafetyBias(deadSmallWinSingleton.Value.TileId, state, botSeatIndex);
                        if (singletonTenpaiSafety > bestTenpaiSafety)
                            return (best.TileId, false);

                        if (singletonTenpaiSafety == bestTenpaiSafety
                            && bestTenpaiSafety <= -2
                            && best.Score < deadSmallWinSingleton.Value.Score)
                            return (best.TileId, false);
                    }
                }

                if (TrySelectTenpaiSafeOffRankPair(
                        logicalCounts,
                        scored,
                        deadSmallWinSingleton.Value.TileId,
                        state,
                        botSeatIndex,
                        visibleLogicalCounts,
                        out var tenpaiSafePairTileId))
                {
                    return (tenpaiSafePairTileId, false);
                }

                return (deadSmallWinSingleton.Value.TileId, true);
            }

            var connectedSmallWinSingleton = scored
                .Where(entry =>
                    IsSmallWinRank(entry.Logical)
                    && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                    && IsBridgeBetweenOffRankPairs(entry.Logical, logicalCounts))
                .Select(entry => (DiscardCandidate?)entry)
                .FirstOrDefault();

            if (connectedSmallWinSingleton is not null)
            {
                var leftBridgeLogical = connectedSmallWinSingleton.Value.Logical - 1;
                var rightBridgeLogical = connectedSmallWinSingleton.Value.Logical + 1;
                var bestBreaksBridgePair = best.Logical == leftBridgeLogical || best.Logical == rightBridgeLogical;

                if (bestBreaksBridgePair)
                {
                    if (IsLogicalNearExhausted(connectedSmallWinSingleton.Value.Logical, visibleLogicalCounts))
                    {
                        if (TrySelectTenpaiSafeOffRankPair(
                                logicalCounts,
                                scored,
                                connectedSmallWinSingleton.Value.TileId,
                                state,
                                botSeatIndex,
                                visibleLogicalCounts,
                                out var tenpaiSafePairTileId))
                        {
                            return (tenpaiSafePairTileId, false);
                        }

                        return (connectedSmallWinSingleton.Value.TileId, true);
                    }
                }
            }
        }

        var hasSingleSmallWin = HasSingleSmallWinTile(logicalCounts);
        var offRankPairs = logicalCounts
            .Where(entry => entry.Value > 1 && !IsSmallWinRank(entry.Key))
            .Select(entry => entry.Key)
            .ToHashSet();
        var suppressSingleSmallWinPreserve = false;
        if (hasSingleSmallWin && offRankPairs.Count > 0)
        {
            var smallWinSingleton = scored
                .Where(entry =>
                    IsSmallWinRank(entry.Logical)
                    && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                    && (IsIsolatedSingleton(entry.Logical, logicalCounts)
                        || IsSingletonWithOnlyPairedNeighbors(entry.Logical, logicalCounts, offRankPairs)))
                .Select(entry => (DiscardCandidate?)entry)
                .FirstOrDefault();
            if (smallWinSingleton is not null)
            {
                var bestIsOffRankPair = logicalCounts.GetValueOrDefault(best.Logical, 0) > 1
                    && !IsSmallWinRank(best.Logical);
                if (bestIsOffRankPair && smallWinSingleton.Value.Score <= best.Score + OffRankPairDiscardTolerance)
                {
                    if (state is not null
                        && botSeatIndex >= 0
                        && dangerousOpponents is not null
                        && dangerousOpponents.Count > 0
                        && safetyDiscardLogicals is not null)
                    {
                        var visibleLogicalCounts = CollectPublicLogicalCounts(state, hand, botSeatIndex);
                        if (IsLogicalNearExhausted(smallWinSingleton.Value.Logical, visibleLogicalCounts)
                            && !IsBridgeBetweenOffRankPairs(smallWinSingleton.Value.Logical, logicalCounts))
                            return (smallWinSingleton.Value.TileId, true);

                        var bestOpponentSafety = OpponentSafetyTieBreaker(best.TileId, safetyDiscardLogicals);
                        var singletonOpponentSafety = OpponentSafetyTieBreaker(smallWinSingleton.Value.TileId, safetyDiscardLogicals);

                        if (singletonOpponentSafety > bestOpponentSafety)
                            return (best.TileId, false);

                        if (singletonOpponentSafety == bestOpponentSafety)
                        {
                            var bestTenpaiSafety = TenpaiDetector.SafetyBias(best.TileId, state, botSeatIndex);
                            var singletonTenpaiSafety = TenpaiDetector.SafetyBias(smallWinSingleton.Value.TileId, state, botSeatIndex);
                            if (singletonTenpaiSafety > bestTenpaiSafety)
                                return (best.TileId, false);
                        }
                    }

                    return (smallWinSingleton.Value.TileId, true);
                }

                // If the only 2/5/8 is isolated and off-rank pairs exist, never
                // invoke the fallback that preserves the singleton; keep the pair.
                suppressSingleSmallWinPreserve = true;
            }
        }

        var smallWinCandidates = scored
            .Where(entry => IsSmallWinRank(entry.Logical))
            .ToList();

        if (!suppressSingleSmallWinPreserve
            && smallWinCandidates.Count == 1
            && best.TileId == smallWinCandidates[0].TileId)
        {
            var alternate = scored
                .Where(entry => entry.TileId != best.TileId && !IsSmallWinRank(entry.Logical))
                .OrderBy(entry => entry.Score)
                .ThenByDescending(entry => entry.TileId)
                .Select(entry => (DiscardCandidate?)entry)
                .FirstOrDefault();

            if (alternate is not null && alternate.Value.Score <= best.Score + SingleSmallWinDiscardTolerance)
            {
                var alternateIsOffRankPair = logicalCounts.GetValueOrDefault(alternate.Value.Logical, 0) > 1
                    && !IsSmallWinRank(alternate.Value.Logical);
                var singletonIsWeak = IsIsolatedSingleton(best.Logical, logicalCounts)
                    || IsSingletonWithOnlyPairedNeighbors(best.Logical, logicalCounts, offRankPairs);

                // Keep Medium aligned with Hard/Master: when the only 2/5/8 tile is
                // a weak singleton and the alternate is an off-rank pair, discard the
                // singleton instead of breaking pair structure.
                if (alternateIsOffRankPair && singletonIsWeak)
                    return (best.TileId, true);

                return (alternate.Value.TileId, false);
            }
        }

        var selectedTileId = best.TileId;
        if (state is not null
            && botSeatIndex >= 0
            && TryPromoteSingleDangerousGenbutsuOnScoreTolerance(
                logicalCounts,
                scored,
                selectedTileId,
                state,
                botSeatIndex,
                out var dangerousSafetyTileId))
        {
            selectedTileId = dangerousSafetyTileId;
        }

        if (state is not null
            && botSeatIndex >= 0
            && TryPromoteFullDangerousGenbutsuOnScoreTolerance(
                scored,
                selectedTileId,
                state,
                botSeatIndex,
                out var fullSafetyTileId))
        {
            selectedTileId = fullSafetyTileId;
        }

        return (selectedTileId, false);
    }

    private static int ComputeKeepScore(int tileId, Dictionary<int, int> logicalCounts)
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
        var isSmallWinRank = humanRank is 2 or 5 or 8;
        if (isSmallWinRank)
            keepScore += 2;

        if (isSmallWinRank && HasSingleSmallWinTile(logicalCounts))
            keepScore += 5;

        return keepScore;
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

    private static bool TrySelectTenpaiSafeOffRankPair(
        Dictionary<int, int> logicalCounts,
        List<DiscardCandidate> scored,
        int selectedTileId,
        ChangshaGameState state,
        int botSeatIndex,
        Dictionary<int, int> visibleLogicalCounts,
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
        // purely on dangerous-opponent safety bias.
        if (IsLogicalNearExhausted(selected.Logical, visibleLogicalCounts)
            && !IsLogicalDead(selected.Logical, visibleLogicalCounts)
            && !IsBridgeBetweenOffRankPairs(selected.Logical, logicalCounts))
            return false;

        // Dead singleton (all four copies visible) has no future value, but it may
        // still be riskier than a dangerous-opponent genbutsu off-rank pair.
        // Keep the singleton unless the pair is strictly safer.
        var opponentDiscardLogicals = CollectSafetyDiscardLogicals(state, botSeatIndex, dangerousOpponents);

        var saferOffRankPair = scored
            .Where(entry => logicalCounts.GetValueOrDefault(entry.Logical, 0) > 1 && !IsSmallWinRank(entry.Logical))
            .OrderBy(entry => OpponentSafetyTieBreaker(entry.TileId, opponentDiscardLogicals))
            .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
            .ThenBy(entry => entry.Score)
            .ThenByDescending(entry => entry.TileId)
            .Select(entry => (DiscardCandidate?)entry)
            .FirstOrDefault();

        if (saferOffRankPair is null)
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

    private static bool TryPromoteSingleDangerousGenbutsuOnScoreTolerance(
        Dictionary<int, int> logicalCounts,
        List<DiscardCandidate> scored,
        int selectedTileId,
        ChangshaGameState state,
        int botSeatIndex,
        out int tileId)
    {
        tileId = -1;

        var dangerousOpponents = TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
        if (dangerousOpponents.Count != 1)
            return false;

        var selected = scored.FirstOrDefault(entry => entry.TileId == selectedTileId);
        if (selected.TileId != selectedTileId)
            return false;

        if (IsSmallWinRank(selected.Logical) && logicalCounts.GetValueOrDefault(selected.Logical, 0) == 1)
            return false;

        var selectedSafety = TenpaiDetector.SafetyBias(selected.TileId, state, botSeatIndex);
        if (selectedSafety <= -1)
            return false;

        DiscardCandidate? dangerousGenbutsuCandidate = null;
        foreach (var entry in scored
                     .Where(entry => entry.TileId != selectedTileId)
                     .OrderBy(entry => entry.Score)
                     .ThenByDescending(entry => entry.TileId))
        {
            if (TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex) <= -1)
            {
                dangerousGenbutsuCandidate = entry;
                break;
            }
        }

        if (dangerousGenbutsuCandidate is null)
            return false;

        if (dangerousGenbutsuCandidate.Value.Score > selected.Score + DangerousGenbutsuScoreTolerance)
            return false;

        tileId = dangerousGenbutsuCandidate.Value.TileId;
        return true;
    }

    private static bool TryPromoteFullDangerousGenbutsuOnScoreTolerance(
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
                     .Where(entry => entry.TileId != selectedTileId)
                     .OrderBy(entry => entry.Score)
                     .ThenBy(entry => TenpaiDetector.SafetyBias(entry.TileId, state, botSeatIndex))
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

    private static int OpponentSafetyTieBreaker(int tileId, HashSet<int> opponentDiscardLogicals)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        return opponentDiscardLogicals.Contains(logical) ? -1 : 0;
    }

    private static HashSet<int> CollectSafetyDiscardLogicals(
        ChangshaGameState state,
        int botSeatIndex,
        IReadOnlyList<int>? dangerousOpponents = null)
    {
        dangerousOpponents ??= TenpaiDetector.CollectDangerousOpponents(state, botSeatIndex);
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

    private static bool IsLogicalNearExhausted(int logical, Dictionary<int, int> visibleLogicalCounts)
        => visibleLogicalCounts.GetValueOrDefault(logical, 0) >= 3;

    private static bool IsLogicalDead(int logical, Dictionary<int, int> visibleLogicalCounts)
        => visibleLogicalCounts.GetValueOrDefault(logical, 0) >= 4;
}
