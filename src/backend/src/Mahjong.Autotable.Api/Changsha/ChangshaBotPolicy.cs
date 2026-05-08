namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Bot policy for Changsha Mahjong. Plays legally with simple heuristics:
///   - Discards: prefer isolated tiles, keep pairs and sequences
///   - Claims: declare win when possible, kong if held, pung if beneficial, chow if next-seat and helpful
///   - Always declares win when hand is winning
///   - Never makes illegal moves
/// </summary>
public sealed class ChangshaBotPolicy
{
    /// <summary>
    /// Given the current game state and the bot's seat, determine the next action.
    /// Returns a BotAction describing what the bot should do.
    /// </summary>
    public BotAction DecideAction(ChangshaGameState state, int botSeatIndex)
    {
        var hand = state.Hands.Single(h => h.SeatIndex == botSeatIndex);

        if (state.Phase == ChangshaPhase.AwaitingDiscard && state.ActiveSeatIndex == botSeatIndex)
        {
            return DecideDiscardPhase(state, hand, botSeatIndex);
        }

        if (state.Phase == ChangshaPhase.AwaitingClaim && state.ClaimWindow is not null)
        {
            return DecideClaimPhase(state, hand, botSeatIndex);
        }

        return BotAction.Wait();
    }

    private static BotAction DecideDiscardPhase(ChangshaGameState state, ChangshaHandState hand, int botSeatIndex)
    {
        // Check for self-draw win
        var detector = new ChangshaWinDetector();
        var winResult = detector.Detect(hand, method: WinMethod.SelfDraw);
        if (winResult.IsWin)
        {
            return BotAction.DeclareWin();
        }

        // Check for concealed kong opportunity
        var kongLogical = FindConcealedKongCandidate(hand);
        if (kongLogical >= 0)
        {
            return BotAction.DeclareConcealedKong(kongLogical);
        }

        // Check for added kong opportunity
        var addedKongTile = FindAddedKongCandidate(hand);
        if (addedKongTile >= 0)
        {
            return BotAction.DeclareAddedKong(addedKongTile);
        }

        // Discard — use heuristic scoring
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
            // Always claim Hu
            if (opp.ClaimType == Tables.TableClaimType.Hu)
            {
                return BotAction.Claim(Tables.TableClaimType.Hu);
            }

            // Claim Kong if we have 3 matching
            if (opp.ClaimType == Tables.TableClaimType.Kong)
            {
                return BotAction.Claim(Tables.TableClaimType.Kong);
            }

            // Claim Pung if it would leave us close to winning
            if (opp.ClaimType == Tables.TableClaimType.Pung)
            {
                // Simple heuristic: always pung
                return BotAction.Claim(Tables.TableClaimType.Pung);
            }

            // Claim Chow only if it brings us closer to winning
            if (opp.ClaimType == Tables.TableClaimType.Chow)
            {
                // Simple heuristic: chow if hand has few melds
                if (hand.Melds.Count < 3)
                {
                    return BotAction.Claim(Tables.TableClaimType.Chow);
                }
            }
        }

        return BotAction.Pass();
    }

    private static int FindConcealedKongCandidate(ChangshaHandState hand)
    {
        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (logical, count) in logicalCounts)
        {
            if (count >= 4)
                return logical;
        }
        return -1;
    }

    private static int FindAddedKongCandidate(ChangshaHandState hand)
    {
        foreach (var meld in hand.Melds)
        {
            if (meld.Kind != MeldKind.Pung)
                continue;

            var meldLogical = ChangshaDeckBuilder.GetLogicalTile(meld.TileIds[0]);
            var matchingTile = hand.ConcealedTiles
                .FirstOrDefault(t => ChangshaDeckBuilder.GetLogicalTile(t) == meldLogical, -1);
            if (matchingTile >= 0)
                return matchingTile;
        }
        return -1;
    }

    /// <summary>
    /// Select tile to discard using scoring heuristic.
    /// Lower score = more likely to discard.
    /// Prefers keeping pairs, sequences, and 258 ranks.
    /// </summary>
    public static int SelectDiscardTile(ChangshaHandState hand)
    {
        if (hand.ConcealedTiles.Count == 0)
            throw new InvalidOperationException("Cannot discard from empty hand.");

        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        return hand.ConcealedTiles
            .OrderBy(t => ComputeKeepScore(t, logicalCounts))
            .ThenByDescending(t => t)
            .First();
    }

    private static int ComputeKeepScore(int tileId, Dictionary<int, int> logicalCounts)
    {
        var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
        var rank = logical % 9;
        var keepScore = 0;

        // Pairs and triplets are valuable
        if (logicalCounts.TryGetValue(logical, out var count) && count > 1)
            keepScore += (count - 1) * 6;

        // Adjacent tiles form potential sequences
        if (rank > 0 && logicalCounts.ContainsKey(logical - 1))
            keepScore += 3;
        if (rank < 8 && logicalCounts.ContainsKey(logical + 1))
            keepScore += 3;

        // Gap sequences
        if (rank > 1 && logicalCounts.ContainsKey(logical - 2))
            keepScore += 1;
        if (rank < 7 && logicalCounts.ContainsKey(logical + 2))
            keepScore += 1;

        // 258 pair ranks are more valuable (for winning)
        var humanRank = rank + 1;
        if (humanRank is 2 or 5 or 8)
            keepScore += 2;

        return keepScore;
    }
}

public sealed class BotAction
{
    public BotActionType Type { get; init; }
    public int? TileId { get; init; }
    public int? LogicalTile { get; init; }
    public Tables.TableClaimType? ClaimType { get; init; }

    public static BotAction Wait() => new() { Type = BotActionType.Wait };
    public static BotAction Discard(int tileId) => new() { Type = BotActionType.Discard, TileId = tileId };
    public static BotAction DeclareWin() => new() { Type = BotActionType.DeclareWin };
    public static BotAction DeclareConcealedKong(int logicalTile) =>
        new() { Type = BotActionType.DeclareConcealedKong, LogicalTile = logicalTile };
    public static BotAction DeclareAddedKong(int tileId) =>
        new() { Type = BotActionType.DeclareAddedKong, TileId = tileId };
    public static BotAction Claim(Tables.TableClaimType claimType) =>
        new() { Type = BotActionType.Claim, ClaimType = claimType };
    public static BotAction Pass() => new() { Type = BotActionType.Pass };
}

public enum BotActionType
{
    Wait,
    Discard,
    DeclareWin,
    DeclareConcealedKong,
    DeclareAddedKong,
    Claim,
    Pass
}
