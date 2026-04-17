namespace MahjongClaw.Domain.Rules;

public static class ChangshaFullyReliantDetector
{
    public static bool IsFullyReliant(
        IReadOnlyList<MeldInfo> melds,
        TileSource winningTileSource)
    {
        ArgumentNullException.ThrowIfNull(melds);

        if (winningTileSource == TileSource.SelfDrawn)
        {
            return false;
        }

        return melds.All(meld => meld.Source == TileSource.ClaimedDiscard);
    }
}

public sealed record MeldInfo(TileSource Source);

public enum TileSource
{
    SelfDrawn,
    ClaimedDiscard
}
