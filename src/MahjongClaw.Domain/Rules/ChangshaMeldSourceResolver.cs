namespace MahjongClaw.Domain.Rules;

public static class ChangshaMeldSourceResolver
{
    public static TileSource Resolve(string meldType, string? meldKind)
    {
        ArgumentNullException.ThrowIfNull(meldType);

        if (string.Equals(meldType, "Kong", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(meldKind, "ConcealedKong", StringComparison.OrdinalIgnoreCase))
            {
                return TileSource.SelfDrawn;
            }

            if (string.Equals(meldKind, "PromotedKong", StringComparison.OrdinalIgnoreCase))
            {
                return TileSource.SelfDrawn;
            }

            return TileSource.ClaimedDiscard;
        }

        return TileSource.ClaimedDiscard;
    }
}
