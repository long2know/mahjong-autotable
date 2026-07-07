namespace MahjongClaw.Domain.Gameplay;

public static class TurnFlowValidator
{
    public static bool CanDraw(int handTileCount, int meldTileCount, int declaredKongCount = 0)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, declaredKongCount, requiredOwnedTiles: 13, requiredHandTiles: 1);

    public static bool CanDiscard(int handTileCount, int meldTileCount, int declaredKongCount = 0)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, declaredKongCount, requiredOwnedTiles: 14, requiredHandTiles: 1);

    public static bool CanDeclareMeld(int handTileCount, int meldTileCount, int meldTileRequirement, int declaredKongCount = 0)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, declaredKongCount, requiredOwnedTiles: 14, requiredHandTiles: meldTileRequirement);

    public static bool CanDeclareWin(int handTileCount, int meldTileCount, int declaredKongCount = 0)
        => HasValidOwnedTileShape(handTileCount, meldTileCount, declaredKongCount, requiredOwnedTiles: 14, requiredHandTiles: 1);

    private static bool HasValidOwnedTileShape(
        int handTileCount,
        int meldTileCount,
        int declaredKongCount,
        int requiredOwnedTiles,
        int requiredHandTiles)
    {
        if (declaredKongCount < 0)
        {
            return false;
        }

        if (handTileCount < 0 || meldTileCount < 0)
        {
            return false;
        }

        if (handTileCount < requiredHandTiles)
        {
            return false;
        }

        var totalOwnedTiles = handTileCount + meldTileCount;
        var requiredOwnedWithKongs = requiredOwnedTiles + declaredKongCount;
        return totalOwnedTiles == requiredOwnedWithKongs;
    }
}
