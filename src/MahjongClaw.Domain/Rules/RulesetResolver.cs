namespace MahjongClaw.Domain.Rules;

public static class RulesetResolver
{
    public static MahjongRuleset Resolve(string? ruleset)
    {
        if (string.IsNullOrWhiteSpace(ruleset))
        {
            return MahjongRuleset.Changsha258;
        }

        var normalized = Normalize(ruleset);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return MahjongRuleset.Changsha258;
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
            return true;
        }

        return IsChangsha258Normalized(normalized);
    }

    private static bool IsChangsha258Normalized(string normalized)
    {
        if (string.Equals(normalized, "Changsha", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Changsha258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CS258", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaRules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaRuleset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaDefault", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaDefaultRules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaDefaultRuleset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaMinimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaMini", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSRules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSRuleset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSDefault", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSDefaultRules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSDefaultRuleset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSMinimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSMini", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("Changsha", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("CS", StringComparison.OrdinalIgnoreCase))
        {
            if (IsChangsha258WordAlias(normalized))
            {
                return true;
            }

            if (ContainsChangshaKeywords(normalized))
            {
                return !normalized.Any(char.IsDigit) || ContainsChangsha258Digits(normalized);
            }
        }

        if (normalized.StartsWith("Changsha", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("CS", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("258", StringComparison.Ordinal)
            || normalized.StartsWith("108", StringComparison.Ordinal)
            || normalized.StartsWith("TwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("ErWuBa", StringComparison.OrdinalIgnoreCase))
        {
            return IsChangsha258WordAlias(normalized) || ContainsChangsha258Digits(normalized);
        }

        return false;
    }

    private static bool ContainsChangshaKeywords(string normalized)
        => normalized.Contains("NoHonors", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoHonor", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoHonours", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoHonour", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutHonors", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutHonor", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutHonours", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutHonour", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoFlowers", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoFlower", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutFlowers", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutFlower", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoSeasons", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoSeason", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutSeasons", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutSeason", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoBonus", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoBonuses", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutBonus", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutBonuses", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("SuitedOnly", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("SuitedTiles", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Suited", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Ruleset", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Rules", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Rule", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Default", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Minimal", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Mini", StringComparison.OrdinalIgnoreCase);

    private static bool IsChangsha258WordAlias(string normalized)
        => string.Equals(normalized, "TwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaPairRequired", StringComparison.OrdinalIgnoreCase);

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

            var digits = normalized[start..index];
            if (IsChangshaTokenSequence(digits))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsChangshaTokenSequence(string digits)
    {
        if (digits.Length % 3 != 0)
        {
            return false;
        }

        for (var i = 0; i < digits.Length; i += 3)
        {
            var token = digits.AsSpan(i, 3);
            if (!token.SequenceEqual("258") && !token.SequenceEqual("108"))
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string? ruleset)
        => string.Concat((ruleset ?? string.Empty).Trim().Where(char.IsLetterOrDigit));
}

