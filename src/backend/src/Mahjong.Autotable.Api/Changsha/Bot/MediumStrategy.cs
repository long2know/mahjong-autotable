using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F "Medium" difficulty strategy — a direct port of the heuristics from the
/// legacy <see cref="ChangshaBotPolicy"/>. Scores each candidate discard by how much
/// it contributes to potential pairs, sequences, and the 2/5/8 bias common in
/// Changsha rule sets, and claims Hu/Kong/Pung greedily plus Chow when the hand has
/// fewer than 3 melds.
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

    public string Difficulty => "medium";

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
        return DecideDiscardPhase(state, hand);
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

            var (tileId, droppedIsolatedSmallWin) = SelectDiscardTileWithMeta(hand);
            var logicalCounts = hand.ConcealedTiles
                .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
                .ToDictionary(g => g.Key, g => g.Count());
            var score = ComputeKeepScore(tileId, logicalCounts);
            reasoning.Add($"keep-score primary: discard tile={tileId} score={score} (lower=better to discard)");
            reasoning.Add("tie-breaker: tile-id descending");
            if (droppedIsolatedSmallWin)
                reasoning.Add("changsha 258 guard: preserved off-rank pair by discarding isolated 2/5/8 singleton");
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
                reasoning.Add("no claim opportunity matched gate (chow requires <3 melds)");
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

    private static BotAction DecideDiscardPhase(ChangshaGameState state, ChangshaHandState hand)
    {
        if (ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, hand.SeatIndex))
            return BotAction.DeclareWin();

        var kongLogical = HandEvaluator.FindConcealedKongCandidate(hand);
        if (kongLogical >= 0)
            return BotAction.DeclareConcealedKong(kongLogical);

        var addedKongTile = HandEvaluator.FindAddedKongCandidate(hand);
        if (addedKongTile >= 0)
            return BotAction.DeclareAddedKong(addedKongTile);

        var tileId = SelectDiscardTile(hand);
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

        foreach (var opp in opportunities)
        {
            if (opp.ClaimType == TableClaimType.Hu)
                return BotAction.Claim(TableClaimType.Hu);

            if (opp.ClaimType == TableClaimType.Kong)
                return BotAction.Claim(TableClaimType.Kong);

            if (opp.ClaimType == TableClaimType.Pung)
                return BotAction.Claim(TableClaimType.Pung);

            if (opp.ClaimType == TableClaimType.Chow && hand.Melds.Count < 3)
                return BotAction.Claim(TableClaimType.Chow);
        }

        return BotAction.Pass();
    }

    /// <summary>Public so the legacy <see cref="ChangshaBotPolicy.SelectDiscardTile"/>
    /// facade and harness tests can call into the same implementation.</summary>
    public static int SelectDiscardTile(ChangshaHandState hand)
    {
        var result = SelectDiscardTileWithMeta(hand);
        return result.TileId;
    }

    private static (int TileId, bool DroppedIsolatedSmallWin) SelectDiscardTileWithMeta(ChangshaHandState hand)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var scored = hand.ConcealedTiles
            .Select(tileId => new
            {
                TileId = tileId,
                Logical = ChangshaDeckBuilder.GetLogicalTile(tileId),
                Score = ComputeKeepScore(tileId, logicalCounts)
            })
            .ToList();

        var best = scored
            .OrderBy(entry => entry.Score)
            .ThenByDescending(entry => entry.TileId)
            .First();

        var hasSingleSmallWin = HasSingleSmallWinTile(logicalCounts);
        var offRankPairs = logicalCounts
            .Where(entry => entry.Value > 1 && !IsSmallWinRank(entry.Key))
            .Select(entry => entry.Key)
            .ToHashSet();
        var suppressSingleSmallWinPreserve = false;
        if (hasSingleSmallWin && offRankPairs.Count > 0)
        {
            var smallWinSingleton = scored.FirstOrDefault(entry =>
                IsSmallWinRank(entry.Logical)
                && logicalCounts.GetValueOrDefault(entry.Logical, 0) == 1
                && (IsIsolatedSingleton(entry.Logical, logicalCounts)
                    || IsSingletonWithOnlyPairedNeighbors(entry.Logical, logicalCounts, offRankPairs)));
            if (smallWinSingleton is not null)
            {
                var bestIsOffRankPair = logicalCounts.GetValueOrDefault(best.Logical, 0) > 1
                    && !IsSmallWinRank(best.Logical);
                if (bestIsOffRankPair && smallWinSingleton.Score <= best.Score + OffRankPairDiscardTolerance)
                    return (smallWinSingleton.TileId, true);

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
                .FirstOrDefault();

            if (alternate is not null && alternate.Score <= best.Score + SingleSmallWinDiscardTolerance)
            {
                var alternateIsOffRankPair = logicalCounts.GetValueOrDefault(alternate.Logical, 0) > 1
                    && !IsSmallWinRank(alternate.Logical);
                var singletonIsWeak = IsIsolatedSingleton(best.Logical, logicalCounts)
                    || IsSingletonWithOnlyPairedNeighbors(best.Logical, logicalCounts, offRankPairs);

                // Keep Medium aligned with Hard/Master: when the only 2/5/8 tile is
                // a weak singleton and the alternate is an off-rank pair, discard the
                // singleton instead of breaking pair structure.
                if (alternateIsOffRankPair && singletonIsWeak)
                    return (best.TileId, true);

                return (alternate.TileId, false);
            }
        }

        return (best.TileId, false);
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
}
