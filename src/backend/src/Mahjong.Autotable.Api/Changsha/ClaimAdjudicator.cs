using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Resolves claim priority for a discarded tile.
/// Priority: Hu > Kong = Pung > Chow > Pass
/// Multiple Hu: closest counterclockwise from discarder wins.
/// Chow: only allowed from next seat (counterclockwise from discarder).
/// </summary>
public interface IClaimAdjudicator
{
    ChangshaClaimOpportunity? Adjudicate(
        int discardSeatIndex,
        int discardTileId,
        IReadOnlyList<ChangshaHandState> hands,
        IReadOnlyList<Meld> existingMelds);

    List<ChangshaClaimOpportunity> GetOpportunities(
        int discardSeatIndex,
        int discardTileId,
        IReadOnlyList<ChangshaHandState> hands);
}

public sealed class ClaimAdjudicator : IClaimAdjudicator
{
    private const int SeatCount = 4;

    public ChangshaClaimOpportunity? Adjudicate(
        int discardSeatIndex,
        int discardTileId,
        IReadOnlyList<ChangshaHandState> hands,
        IReadOnlyList<Meld> existingMelds)
    {
        var opportunities = GetOpportunities(discardSeatIndex, discardTileId, hands);
        return SelectWinner(opportunities, discardSeatIndex);
    }

    public List<ChangshaClaimOpportunity> GetOpportunities(
        int discardSeatIndex,
        int discardTileId,
        IReadOnlyList<ChangshaHandState> hands)
    {
        var opportunities = new List<ChangshaClaimOpportunity>();
        var discardLogical = ChangshaDeckBuilder.GetLogicalTile(discardTileId);

        foreach (var hand in hands)
        {
            if (hand.SeatIndex == discardSeatIndex)
                continue;

            var logicalTiles = hand.ConcealedTiles.Select(ChangshaDeckBuilder.GetLogicalTile).ToList();
            var matchingCount = logicalTiles.Count(t => t == discardLogical);

            // Check Hu
            if (ChangshaWinDetector.IsWinningWith(hand, discardTileId))
            {
                opportunities.Add(new ChangshaClaimOpportunity
                {
                    SeatIndex = hand.SeatIndex,
                    ClaimType = TableClaimType.Hu,
                    Priority = 4
                });
            }

            // Check Kong (3 matching in hand)
            if (matchingCount >= 3)
            {
                opportunities.Add(new ChangshaClaimOpportunity
                {
                    SeatIndex = hand.SeatIndex,
                    ClaimType = TableClaimType.Kong,
                    Priority = 3
                });
            }
            // Check Pung (2 matching in hand)
            else if (matchingCount >= 2)
            {
                opportunities.Add(new ChangshaClaimOpportunity
                {
                    SeatIndex = hand.SeatIndex,
                    ClaimType = TableClaimType.Pung,
                    Priority = 2
                });
            }

            // Check Chow (next seat only)
            if (IsNextSeat(discardSeatIndex, hand.SeatIndex)
                && IsChowCandidate(logicalTiles, discardLogical))
            {
                opportunities.Add(new ChangshaClaimOpportunity
                {
                    SeatIndex = hand.SeatIndex,
                    ClaimType = TableClaimType.Chow,
                    Priority = 1
                });
            }
        }

        return opportunities;
    }

    private static ChangshaClaimOpportunity? SelectWinner(
        List<ChangshaClaimOpportunity> opportunities,
        int discardSeatIndex)
    {
        if (opportunities.Count == 0)
            return null;

        return opportunities
            .OrderByDescending(o => o.Priority)
            .ThenBy(o => GetCounterClockwiseDistance(discardSeatIndex, o.SeatIndex))
            .ThenBy(o => o.SeatIndex)
            .First();
    }

    private static bool IsNextSeat(int discardSeatIndex, int seatIndex) =>
        (discardSeatIndex + 1) % SeatCount == seatIndex;

    private static int GetCounterClockwiseDistance(int from, int to) =>
        (to - from + SeatCount) % SeatCount;

    private static bool IsChowCandidate(IReadOnlyCollection<int> logicalTiles, int discardLogical)
    {
        if (discardLogical is < 0 or >= 27)
            return false;

        var suitOffset = discardLogical / 9;
        var rank = discardLogical % 9;
        var suitedRanks = logicalTiles
            .Where(t => t / 9 == suitOffset)
            .Select(t => t % 9)
            .ToHashSet();

        return (rank >= 2 && suitedRanks.Contains(rank - 2) && suitedRanks.Contains(rank - 1))
            || (rank >= 1 && rank <= 7 && suitedRanks.Contains(rank - 1) && suitedRanks.Contains(rank + 1))
            || (rank <= 6 && suitedRanks.Contains(rank + 1) && suitedRanks.Contains(rank + 2));
    }
}
