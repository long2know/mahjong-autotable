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
        if (resolved == MahjongRuleset.Changsha258)
        {
            var wallTotal = WallBuilder.GetWallTotal(resolved);
            var pairRanks = FormatSmallWinPairRanks();
            return $"Changsha 258 ({pairRanks} pair, {wallTotal} tiles)";
        }

        return "Chinese Official";
    }

    public static string GetDescription(string? ruleset)
    {
        var resolved = ResolveRuleset(ruleset);
        var wallTotal = WallBuilder.GetWallTotal(resolved);
        return resolved == MahjongRuleset.Changsha258
            ? $"{wallTotal} suited tiles only (no honors or bonus tiles). Standard hands only ({Changsha258Ruleset.RequiredMelds} melds + pair, {Changsha258Ruleset.MinTiles}-{Changsha258Ruleset.MaxTiles} tiles with kongs; no seven pairs or special patterns). Small win requires a suited {FormatSmallWinPairRanks()} pair. Kongs add one tile each (up to {Changsha258Ruleset.MaxKongs}). Big-win patterns not modeled yet. No minimum fan gate."
            : $"{wallTotal} tiles with honors/flowers. Standard Chinese Official rules (8-fan minimum to win).";
    }

    private static string FormatSmallWinPairRanks()
        => string.Join("/", Changsha258Ruleset.SmallWinPairRanks.OrderBy(rank => rank));

    private static MahjongRuleset ResolveRuleset(string? ruleset)
        => RulesetResolver.Resolve(ruleset);
}
