namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F bot helpers that all difficulty strategies share. Tile-counting,
/// concealed/added kong detection, and very lightweight shanten-style heuristics
/// live here so Easy/Medium/Hard don't re-implement them.
/// </summary>
/// <remarks>
/// This is intentionally NOT a full shanten counter — Changsha's Big-Win patterns
/// (Seven Pairs, All Pungs, etc.) plus the standard 4-meld+pair winning shape make a
/// rigorous shanten search expensive. Hard difficulty uses a fast approximation:
/// count how many already-formed groups (pairs, triplets, runs) the hand contains
/// and treat tiles that contribute to none as "loose" / safe to discard.
/// </remarks>
public static class HandEvaluator
{
    /// <summary>
    /// Returns the logical tile id of a tile the bot holds four copies of (concealed
    /// kong candidate), or <c>-1</c> when none qualify.
    /// </summary>
    public static int FindConcealedKongCandidate(ChangshaHandState hand)
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

    /// <summary>
    /// Returns the tile id (physical) of a hand tile that, combined with an existing
    /// declared Pung, makes an added kong candidate. Returns <c>-1</c> when no Pung
    /// can be upgraded.
    /// </summary>
    public static int FindAddedKongCandidate(ChangshaHandState hand)
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
    /// Counts the number of "loose" tiles in a hand — tiles that aren't part of a
    /// pair, neighbour-pair, or gapped-pair. A higher loose count means the hand is
    /// further from a winning shape; used by Easy and Hard to bias their discards.
    /// </summary>
    public static int CountLooseTiles(ChangshaHandState hand)
    {
        var logicalCounts = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        var loose = 0;
        foreach (var tile in hand.ConcealedTiles)
        {
            var logical = ChangshaDeckBuilder.GetLogicalTile(tile);
            var rank = logical % 9;
            var hasPair = logicalCounts.GetValueOrDefault(logical, 0) > 1;
            var hasNeighbour = (rank > 0 && logicalCounts.ContainsKey(logical - 1))
                            || (rank < 8 && logicalCounts.ContainsKey(logical + 1))
                            || (rank > 1 && logicalCounts.ContainsKey(logical - 2))
                            || (rank < 7 && logicalCounts.ContainsKey(logical + 2));
            if (!hasPair && !hasNeighbour)
                loose++;
        }
        return loose;
    }

    /// <summary>
    /// Set of logical tile ids that have already been discarded by ANY seat in the
    /// current hand. Used by <see cref="HardStrategy"/> to bias discards toward
    /// tiles that opponents are statistically less likely to need (mahjong's
    /// "safe tile" heuristic — anything previously discarded is generally safer to
    /// repeat).
    /// </summary>
    public static HashSet<int> CollectDiscardedLogicals(ChangshaGameState state)
    {
        var result = new HashSet<int>();
        foreach (var discard in state.DiscardPile)
        {
            result.Add(ChangshaDeckBuilder.GetLogicalTile(discard.TileId));
        }
        return result;
    }

    /// <summary>
    /// Phase F §5.5 — fast approximate shanten distance to a Hu win shape. NOT a
    /// rigorous shanten counter (Changsha's Big-Win patterns make that expensive);
    /// this returns a coarse estimate that strictly does not increase when a "loose"
    /// tile is removed. Use case: bot discard validation (a discard that increases
    /// MinShantenToHu is a worse decision than one that does not).
    /// </summary>
    /// <remarks>
    /// Algorithm: count the meld-shaped groups already in the hand (declared melds +
    /// pairs + same-suit runs of length 3) and the remaining "loose" tiles. Shanten
    /// is then <c>(4 - groupsHeld) + max(0, 1 - pairsHeld)</c>, the number of
    /// additional groups + pair this hand needs to reach a winning shape, clamped to
    /// at least <c>looseCount / 2</c> so that strictly worse discards are penalised.
    /// </remarks>
    public static int MinShantenToHu(ChangshaHandState hand, IReadOnlyList<int> remainingWall)
    {
        var meldsHeld = hand.Melds.Count;
        var concealed = hand.ConcealedTiles;

        var logicalCounts = concealed
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(g => g.Key, g => g.Count());

        // Pairs we have.
        var pairsHeld = logicalCounts.Values.Count(c => c >= 2);
        // Triplets we have (concealed pungs).
        var tripletsHeld = logicalCounts.Values.Count(c => c >= 3);

        // Rough run estimate: for each suit-rank logical, count how many runs of
        // three consecutive logical tiles exist starting at this position. This
        // intentionally over-counts shared tiles but is bounded and monotonic for
        // the "drop a tile" comparison the tests need.
        var runsHeld = 0;
        foreach (var logical in logicalCounts.Keys)
        {
            var rank = logical % 9;
            if (rank > 6) continue;
            if (logicalCounts.ContainsKey(logical + 1) && logicalCounts.ContainsKey(logical + 2))
                runsHeld++;
        }

        var groupsHeld = meldsHeld + tripletsHeld + runsHeld;
        var pairScore = pairsHeld >= 1 ? 0 : 1;
        var shanten = Math.Max(0, (4 - groupsHeld) + pairScore - 1);

        // Floor: tiles with no neighbour and no pair partner are loose; each loose
        // tile costs ~half a shanten step to repair.
        var loose = CountLooseTiles(hand);
        var looseFloor = loose / 2;

        return Math.Max(shanten, looseFloor);
    }
}
