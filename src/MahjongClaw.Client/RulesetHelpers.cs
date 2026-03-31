using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Client;

public static class RulesetHelpers
{
    public readonly record struct RulesetOption(string Value, string Label);

    public static IReadOnlyList<RulesetOption> GetRulesetOptions()
        => new[]
        {
            new RulesetOption(nameof(MahjongRuleset.ChineseOfficial), FormatLabel(nameof(MahjongRuleset.ChineseOfficial))),
            new RulesetOption(nameof(MahjongRuleset.Changsha258), FormatLabel(nameof(MahjongRuleset.Changsha258)))
        };

    public static bool IsChangsha258(string? ruleset)
        => ResolveRuleset(ruleset) == MahjongRuleset.Changsha258;

    public static int GetWallTotal(string? ruleset)
    {
        var resolved = ResolveRuleset(ruleset);
        return WallBuilder.GetWallTotal(resolved);
    }

    public static string FormatLabel(string? ruleset)
    {
        var resolved = ResolveRuleset(ruleset);
        return resolved == MahjongRuleset.Changsha258
            ? "Changsha 258 (2/5/8 pair)"
            : "Chinese Official";
    }

    public static string GetDescription(string? ruleset)
    {
        var resolved = ResolveRuleset(ruleset);
        var wallTotal = WallBuilder.GetWallTotal(resolved);
        return resolved == MahjongRuleset.Changsha258
            ? $"{wallTotal} suited tiles only (no winds/dragons/flowers/seasons). Standard hands only (4 melds + pair). Small win requires a 2/5/8 pair. Kongs add extra tiles (up to {Changsha258Ruleset.MaxTiles} tiles). Big-win patterns not modeled yet. No minimum fan gate."
            : $"{wallTotal} tiles with honors/flowers. Standard Chinese Official rules (8-fan minimum to win).";
    }

    private static MahjongRuleset ResolveRuleset(string? ruleset)
        => RulesetResolver.Resolve(ruleset);
}
