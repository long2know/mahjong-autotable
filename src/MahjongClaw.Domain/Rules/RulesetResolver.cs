using System.Text;

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
        if (ContainsDisallowedSpecialPatternKeywords(normalized))
        {
            return false;
        }

        if (IsChangsha258ChineseAlias(normalized))
        {
            return true;
        }

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
            || normalized.StartsWith("ErWuBa", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("XiaoHu", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("SmallHu", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("SmallWin", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("长沙", StringComparison.Ordinal))
        {
            return IsChangsha258WordAlias(normalized) || ContainsChangsha258Digits(normalized);
        }

        if (normalized.StartsWith("Minimal", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Mini", StringComparison.OrdinalIgnoreCase))
        {
            var hasDigits = normalized.Any(char.IsDigit);
            if (!hasDigits)
            {
                return IsChangsha258WordAlias(normalized)
                    || IsMinimalWordAlias(normalized)
                    || ContainsChangshaOrCsContext(normalized);
            }

            return ContainsChangsha258Digits(normalized)
                && (ContainsChangshaOrCsContext(normalized)
                    || normalized.Contains("258", StringComparison.Ordinal)
                    || normalized.Contains("108", StringComparison.Ordinal));
        }

        return false;
    }

    private static bool ContainsChangshaOrCsContext(string normalized)
        => normalized.Contains("Changsha", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CS258", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSRules", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSRuleset", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSDefault", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSDefaultRules", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSDefaultRuleset", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSErWuBa", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSSmallWin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSSmallHu", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CSXiaoHu", StringComparison.OrdinalIgnoreCase);

    private static bool IsMinimalWordAlias(string normalized)
    {
        const string minimalPrefix = "Minimal";
        const string miniPrefix = "Mini";

        string? suffix = null;
        if (normalized.StartsWith(minimalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            suffix = normalized[minimalPrefix.Length..];
        }
        else if (normalized.StartsWith(miniPrefix, StringComparison.OrdinalIgnoreCase))
        {
            suffix = normalized[miniPrefix.Length..];
        }

        return !string.IsNullOrEmpty(suffix)
            && IsChangsha258WordAlias(suffix);
    }

    private static bool ContainsDisallowedSpecialPatternKeywords(string normalized)
    {
        var hasUnsupportedPatternKeyword = normalized.Contains("SevenPairs", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("SevenPair", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ThirteenOrphans", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ThirteenOrphan", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NineGates", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NineGate", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllHonors", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllHonor", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllHonours", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllHonour", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminals", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminal", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminalsAndHonors", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminalsAndHonor", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminalsAndHonours", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllTerminalsAndHonour", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("AllGreen", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigThreeDragons", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigThreeDragon", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigFourWinds", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigFourWind", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LittleThreeDragons", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LittleThreeDragon", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LittleFourWinds", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LittleFourWind", StringComparison.OrdinalIgnoreCase);

        if (hasUnsupportedPatternKeyword)
        {
            return true;
        }

        var hasBigWinKeyword = normalized.Contains("BigWin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("DaHu", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("大胡", StringComparison.Ordinal);

        return hasBigWinKeyword && !ContainsExplicitBigWinOptOut(normalized);
    }

    private static bool ContainsExplicitBigWinOptOut(string normalized)
        => normalized.Contains("NoBigWin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoBigWins", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutBigWin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutBigWins", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("NoDaHu", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WithoutDaHu", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigWinOptionalLater", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("BigWinsOptionalLater", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("无大胡", StringComparison.Ordinal)
            || normalized.Contains("無大胡", StringComparison.Ordinal)
            || normalized.Contains("不要大胡", StringComparison.Ordinal)
            || normalized.Contains("不含大胡", StringComparison.Ordinal)
            || normalized.Contains("非大胡", StringComparison.Ordinal);

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
        => IsChangsha258ChineseAlias(normalized)
            || string.Equals(normalized, "XiaoHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "XiaoHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "XiaoHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "XiaoHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "SmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaXiaoHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaXiaoHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaXiaoHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaXiaoHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSXiaoHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSXiaoHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSXiaoHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSXiaoHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallHu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallHuPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallHuRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallHuPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "TwoFiveEightSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ErWuBaSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaTwoFiveEightSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSTwoFiveEightSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ChangshaErWuBaSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaSmallWin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaSmallWinPair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaSmallWinRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "CSErWuBaSmallWinPairRequired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MinimalTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MinimalTwoFiveEightRulesetVariant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MiniTwoFiveEight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MinimalErWuBa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MinimalErWuBaRulesetVariant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MiniErWuBa", StringComparison.OrdinalIgnoreCase);

    private static bool IsChangsha258ChineseAlias(string normalized)
        => string.Equals(normalized, "长沙", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙258", StringComparison.Ordinal)
            || string.Equals(normalized, "二五八", StringComparison.Ordinal)
            || string.Equals(normalized, "二五八将", StringComparison.Ordinal)
            || string.Equals(normalized, "二五八将对", StringComparison.Ordinal)
            || string.Equals(normalized, "小胡", StringComparison.Ordinal)
            || string.Equals(normalized, "小胡将", StringComparison.Ordinal)
            || string.Equals(normalized, "小胡将对", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙二五八", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙二五八将", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙二五八将对", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙小胡", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙小胡将", StringComparison.Ordinal)
            || string.Equals(normalized, "长沙小胡将对", StringComparison.Ordinal)
            || StartsWithValidChineseAliasPrefix(normalized, "二五八")
            || StartsWithValidChineseAliasPrefix(normalized, "小胡")
            || StartsWithValidChineseAliasPrefix(normalized, "长沙二五八")
            || StartsWithValidChineseAliasPrefix(normalized, "长沙小胡");

    private static bool StartsWithValidChineseAliasPrefix(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Length == prefix.Length)
        {
            return true;
        }

        return !IsNumericCharacter(value[prefix.Length]);
    }

    private static bool IsNumericCharacter(char character)
        => char.IsDigit(character)
            || character is '零' or '〇' or '一' or '二' or '三' or '四' or '五' or '六' or '七' or '八' or '九' or '十'
            or '百' or '千' or '万' or '萬' or '两' or '兩';

    private static bool ContainsChangsha258Digits(string normalized)
    {
        var foundDigitSequence = false;
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

            foundDigitSequence = true;
            var digits = normalized[start..index];
            if (!IsChangshaTokenSequence(digits))
            {
                return false;
            }
        }

        return foundDigitSequence;
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
    {
        var source = (ruleset ?? string.Empty).Trim();
        if (source.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (char.IsLetter(character))
            {
                builder.Append(character);
                continue;
            }

            if (!char.IsDigit(character))
            {
                continue;
            }

            var numericValue = (int)char.GetNumericValue(character);
            if ((uint)numericValue <= 9)
            {
                builder.Append((char)('0' + numericValue));
            }
        }

        return builder.ToString();
    }
}

