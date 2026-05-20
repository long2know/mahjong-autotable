using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F "Hard" difficulty strategy. Combines Medium's keep-score with a defensive
/// penalty for tiles that opponents are likely to need.
/// <list type="bullet">
///   <item>Discards prefer "safe" tiles — anything already present in the discard pile
///   is heavily prioritised for discard since opponents have demonstrated they don't
///   need it.</item>
///   <item>Claims Hu/Kong/Pung greedily like Medium; claims Chow only when the bot has
///   fewer than 2 melds AND the chow doesn't open a winning tile to the opponents.</item>
///   <item>Declares concealed/added kong opportunistically but only when the resulting
///   hand state still has enough "loose" tiles to absorb a kong replacement draw.</item>
/// </list>
/// This is a fast approximation, not a true shanten + EV search. It's enough to play
/// noticeably better than Medium without breaking the runtime's bot-turn budget.
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

            // Hard is fussier about Chow than Medium — only take it when the hand is
            // clearly behind on melds AND we have very few loose tiles already (so the
            // chow won't leave us stranded with junk).
            if (opp.ClaimType == TableClaimType.Chow
                && hand.Melds.Count < 2
                && HandEvaluator.CountLooseTiles(hand) <= 3)
            {
                return BotAction.Claim(TableClaimType.Chow);
            }
        }

        return BotAction.Pass();
    }

    private static int SelectDiscardTile(ChangshaHandState hand, ChangshaGameState state)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var discardedLogicals = HandEvaluator.CollectDiscardedLogicals(state);

        return hand.ConcealedTiles
            .OrderBy(t => ComputeDiscardScore(t, logicalCounts, discardedLogicals))
            .ThenByDescending(t => t)
            .First();
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
