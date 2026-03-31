namespace MahjongClaw.Client;

public static class RulesetHelpers
{
    public static bool IsChangsha258(string? ruleset)
    {
        if (string.IsNullOrWhiteSpace(ruleset))
        {
            return false;
        }

        var normalized = string.Concat(ruleset.Trim().Where(char.IsLetterOrDigit));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return string.Equals(normalized, "Changsha", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Changsha258", StringComparison.OrdinalIgnoreCase)
            || (normalized.Contains("Changsha", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("258", StringComparison.Ordinal));
    }

    public static int GetWallTotal(string? ruleset)
        => IsChangsha258(ruleset) ? 108 : 144;

    public static string FormatLabel(string? ruleset)
        => IsChangsha258(ruleset) ? "Changsha 258 (2/5/8 pair)" : "Chinese Official";
}
