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
            .GroupBy(static entry => new { entry.PlayerId, entry.Tile })
            .Where(group => group.Count() == 3)
            .SelectMany(group => Enumerable.Repeat(group.Key.Tile, group.Count()))
            .ToArray();
    }
}

public readonly record struct OpponentMeldTileView(Guid PlayerId, Tile Tile);
