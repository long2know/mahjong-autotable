namespace MahjongClaw.Domain.Gameplay;

public static class TurnFlowValidator
{
    public static bool CanDraw(int handTileCount, int meldTileCount)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, requiredOwnedTiles: 13, requiredHandTiles: 1);

    public static bool CanDiscard(int handTileCount, int meldTileCount)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, requiredOwnedTiles: 14, requiredHandTiles: 1);

    public static bool CanDeclareMeld(int handTileCount, int meldTileCount, int meldTileRequirement)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, requiredOwnedTiles: 14, requiredHandTiles: meldTileRequirement);

    public static bool CanDeclareWin(int handTileCount, int meldTileCount)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, requiredOwnedTiles: 14, requiredHandTiles: 1);

    private static bool HasValidOwnedTileShape(
        int handTileCount,
        int meldTileCount,
        int requiredOwnedTiles,
        int requiredHandTiles)
    {
        if (handTileCount < requiredHandTiles)
        {
            return false;
        }

        var totalOwnedTiles = handTileCount + meldTileCount;
        return totalOwnedTiles == requiredOwnedTiles;
    }
}
