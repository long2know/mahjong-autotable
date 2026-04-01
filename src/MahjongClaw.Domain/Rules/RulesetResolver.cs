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

        if (normalized.All(char.IsDigit))
        {
            return MahjongRuleset.ChineseOfficial;
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
            || string.Equals(normalized, "258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CS258", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("Changsha", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("CS", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("258", StringComparison.Ordinal))
        {
            return ContainsChangsha258Digits(normalized);
        }

        return false;
    }

    private static bool ContainsChangsha258Digits(string normalized)
    {
        var index = 0;
        while (index < normalized.Length)
        {
            while (index < normalized.Length && !char.IsDigit(normalized[index]))
            {
                index++;
            }

            if (index >= normalized.Length)
            {
                break;
            }

            var start = index;
            while (index < normalized.Length && char.IsDigit(normalized[index]))
            {
                index++;
            }

            if (IsRepeated258(normalized[start..index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRepeated258(string digits)
    {
        if (digits.Length % 3 != 0)
        {
            return false;
        }

        for (var i = 0; i < digits.Length; i += 3)
        {
            if (!digits.AsSpan(i, 3).SequenceEqual("258"))
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string? ruleset)
        => string.Concat((ruleset ?? string.Empty).Trim().Where(char.IsLetterOrDigit));
}

