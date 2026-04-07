namespace MahjongClaw.Domain.Gameplay;

public static class TurnFlowValidator
{
    public static bool CanDraw(int handTileCount, int meldTileCount)
    {
        if (handTileCount <= 0)
        {
            return false;
        }

        var totalOwnedTiles = handTileCount + meldTileCount;
        return totalOwnedTiles == 13;
    }

    public static bool CanDiscard(int handTileCount, int meldTileCount)
    {
        if (handTileCount <= 0)
        {
            return false;
        }

        var totalOwnedTiles = handTileCount + meldTileCount;
        return totalOwnedTiles == 14;
    }
}
