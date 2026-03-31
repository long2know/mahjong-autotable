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
}
