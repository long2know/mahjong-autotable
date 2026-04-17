using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class ChangshaRobbingKongDetector
{
    public static bool CanRobPromotedKong(
        IReadOnlyList<Tile> opponentHandTiles,
        Tile promotedKongTile,
        int existingKongCount = 0)
    {
        ArgumentNullException.ThrowIfNull(opponentHandTiles);
        ArgumentNullException.ThrowIfNull(promotedKongTile);

        var tilesWithPromotedTile = opponentHandTiles.Append(promotedKongTile).ToList();

        if (!Changsha258Ruleset.TryGetRequiredKongCount(tilesWithPromotedTile.Count, out var requiredKongs))
        {
            return false;
        }

        if (existingKongCount > requiredKongs)
        {
            return false;
        }

        return Changsha258Ruleset.TryGetWinningHand(tilesWithPromotedTile, out _);
    }
}
