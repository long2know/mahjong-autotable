using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class OpponentMeldThreatAnalyzer
{
    public static IReadOnlyList<Tile> BuildPromotableKongThreatTiles(IReadOnlyList<OpponentMeldTileView> opponentMeldTiles)
    {
        ArgumentNullException.ThrowIfNull(opponentMeldTiles);

        if (opponentMeldTiles.Any(static entry => entry.Tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(opponentMeldTiles));
        }

        return opponentMeldTiles
            .GroupBy(static entry => new { entry.PlayerId, entry.MeldId })
            .Where(static meldGroup =>
            {
                if (meldGroup.Count() != 3)
                {
                    return false;
                }

                var firstTile = meldGroup.First().Tile;
                return meldGroup.All(entry => entry.Tile == firstTile);
            })
            .SelectMany(static meldGroup => Enumerable.Repeat(meldGroup.First().Tile, meldGroup.Count()))
            .ToArray();
    }
}

public readonly record struct OpponentMeldTileView(Guid PlayerId, Guid MeldId, Tile Tile);
