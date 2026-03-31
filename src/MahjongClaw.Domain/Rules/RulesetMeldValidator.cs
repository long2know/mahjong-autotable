using System.Linq;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class RulesetMeldValidator
{
    public static bool IsValidMeld(IReadOnlyList<Tile> tiles, MeldType type, MahjongRuleset ruleset)
    {
        if (!MeldValidator.IsValidMeld(tiles, type))
        {
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258
            && tiles.Any(static tile => !Changsha258Ruleset.IsAllowedTile(tile)))
        {
            return false;
        }

        return true;
    }
}
