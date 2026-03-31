using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Client;

public static class RulesetHelpers
{
    public static bool IsChangsha258(string? ruleset)
        => RulesetResolver.IsChangsha258(ruleset);

    public static int GetWallTotal(string? ruleset)
        => IsChangsha258(ruleset) ? 108 : 144;

    public static string FormatLabel(string? ruleset)
        => IsChangsha258(ruleset) ? "Changsha 258 (2/5/8 pair)" : "Chinese Official";

    public static string GetDescription(string? ruleset)
        => IsChangsha258(ruleset)
            ? $"{GetWallTotal(ruleset)} suited tiles, no honors/flowers. Win requires 4 melds + a 2/5/8 pair."
            : $"{GetWallTotal(ruleset)} tiles with honors/flowers. Standard Chinese Official rules.";
}
