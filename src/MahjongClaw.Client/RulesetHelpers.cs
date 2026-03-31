namespace MahjongClaw.Client;

public static class RulesetHelpers
{
    public static bool IsChangsha258(string? ruleset)
    {
        if (string.IsNullOrWhiteSpace(ruleset))
        {
            return false;
        }

        var normalized = ruleset.Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return string.Equals(normalized, "Changsha258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Changsha", StringComparison.OrdinalIgnoreCase);
    }

    public static int GetWallTotal(string? ruleset)
        => IsChangsha258(ruleset) ? 108 : 144;

    public static string FormatLabel(string? ruleset)
        => IsChangsha258(ruleset) ? "Changsha 258" : "Chinese Official";
}
