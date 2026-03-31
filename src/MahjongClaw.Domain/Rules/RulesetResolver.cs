namespace MahjongClaw.Domain.Rules;

public static class RulesetResolver
{
    public static MahjongRuleset Resolve(string? ruleset)
    {
        if (string.IsNullOrWhiteSpace(ruleset))
        {
            return MahjongRuleset.ChineseOfficial;
        }

        var normalized = Normalize(ruleset);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return MahjongRuleset.ChineseOfficial;
        }

        if (IsChangsha258Normalized(normalized))
        {
            return MahjongRuleset.Changsha258;
        }

        return Enum.TryParse<MahjongRuleset>(normalized, true, out var parsed)
            ? parsed
            : MahjongRuleset.ChineseOfficial;
    }

    public static bool IsChangsha258(string? ruleset)
    {
        var normalized = Normalize(ruleset);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return IsChangsha258Normalized(normalized);
    }

    private static bool IsChangsha258Normalized(string normalized)
    {
        if (string.Equals(normalized, "Changsha", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Changsha258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "258", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalized.StartsWith("Changsha", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Contains("258", StringComparison.Ordinal);
    }

    private static string Normalize(string? ruleset)
        => string.Concat((ruleset ?? string.Empty).Trim().Where(char.IsLetterOrDigit));
}
