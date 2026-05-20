using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F "Easy" difficulty strategy. Plays legally but blindly:
/// <list type="bullet">
///   <item>Always declares Hu when winning is available.</item>
///   <item>Discards the highest-rank "loose" tile (any tile that isn't part of a
///   pair or adjacency); falls back to the highest-rank tile when none are loose.</item>
///   <item>Claims Hu and obvious Pung opportunities; skips Chow.</item>
///   <item>No concealed/added kong declarations — keeps the hand small.</item>
/// </list>
/// Suitable as a "training wheels" opponent for new players.
/// </summary>
public sealed class EasyStrategy : IChangshaBotStrategy
{
    public string Difficulty => "easy";

    public BotAction OnTurnStart(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        // Hu when we can.
        var detector = new ChangshaWinDetector();
        var winResult = detector.Detect(hand, method: WinMethod.SelfDraw);
        if (winResult.IsWin)
            return BotAction.DeclareWin();

        // Discard the highest-rank loose tile; fall back to highest-rank overall.
        return BotAction.Discard(SelectDiscardTile(hand));
    }

    public BotAction OnOtherDiscard(ChangshaGameState state, int botSeatIndex, int discarderSeat, int discardedTileId)
    {
        if (state.ClaimWindow is null) return BotAction.Pass();
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        var opportunities = state.ClaimWindow.Opportunities
            .Where(o => o.SeatIndex == botSeatIndex)
            .OrderByDescending(o => o.Priority)
            .ToList();

        foreach (var opp in opportunities)
        {
            // Hu always.
            if (opp.ClaimType == TableClaimType.Hu)
                return BotAction.Claim(TableClaimType.Hu);

            // Pung always (it's "obvious" — we already hold the pair).
            if (opp.ClaimType == TableClaimType.Pung)
                return BotAction.Claim(TableClaimType.Pung);

            // Kong sometimes — only when the hand is large and we'd otherwise be sitting on
            // a slow triplet. Easy keeps it simple: take the kong if we're past the early
            // game.
            if (opp.ClaimType == TableClaimType.Kong && hand.ConcealedTiles.Count >= 10)
                return BotAction.Claim(TableClaimType.Kong);

            // Chow — never, per Vasquez audit §10 Easy.2.
        }

        return BotAction.Pass();
    }

    public BotAction OnSelfDraw(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);
        var detector = new ChangshaWinDetector();
        var winResult = detector.Detect(hand, method: WinMethod.SelfDraw);
        return winResult.IsWin ? BotAction.DeclareWin() : BotAction.Wait();
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

    private static int SelectDiscardTile(ChangshaHandState hand)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        // Score loose tiles low (high priority to discard); pairs/adjacencies score higher.
        return hand.ConcealedTiles
            .OrderBy(t => IsLoose(t, logicalCounts) ? 0 : 1)
            .ThenByDescending(t => ChangshaDeckBuilder.GetLogicalTile(t) % 9)
            .ThenByDescending(t => t)
            .First();
    }

    private static bool IsLoose(int tileId, Dictionary<int, int> logicalCounts)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        var rank = logical % 9;

        if (logicalCounts.GetValueOrDefault(logical, 0) > 1) return false;
        if (rank > 0 && logicalCounts.ContainsKey(logical - 1)) return false;
        if (rank < 8 && logicalCounts.ContainsKey(logical + 1)) return false;
        if (rank > 1 && logicalCounts.ContainsKey(logical - 2)) return false;
        if (rank < 7 && logicalCounts.ContainsKey(logical + 2)) return false;
        return true;
    }
}
