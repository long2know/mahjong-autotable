using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Client;

public static class RulesetHelpers
{
    public readonly record struct RulesetOption(string Value, string Label);

    public static IReadOnlyList<RulesetOption> GetRulesetOptions()
        => new[]
        {
            new RulesetOption(nameof(MahjongRuleset.Changsha258), FormatLabel(nameof(MahjongRuleset.Changsha258))),
            new RulesetOption(nameof(MahjongRuleset.ChineseOfficial), FormatLabel(nameof(MahjongRuleset.ChineseOfficial)))
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
        var wallTotal = WallBuilder.GetWallTotal(resolved);
        if (resolved == MahjongRuleset.Changsha258)
        {
            return $"Changsha 258 ({wallTotal} suited tiles, {Changsha258Ruleset.RequiredMelds} melds + pair, {FormatSmallWinPairRanks()} pair required)";
        }

        return $"Chinese Official ({wallTotal} tiles)";
    }

    public static string GetDescription(string? ruleset)
    {
        var resolved = ResolveRuleset(ruleset);
        var wallTotal = WallBuilder.GetWallTotal(resolved);
        return resolved == MahjongRuleset.Changsha258
            ? $"{wallTotal} suited tiles only (characters/dots/bamboos, no honors, flowers, or seasons). Supports standard hand evaluation ({Changsha258Ruleset.RequiredMelds} melds + pair, {Changsha258Ruleset.MinTiles}-{Changsha258Ruleset.MaxTiles} tiles with kongs) with suited {FormatSmallWinPairRanks()} pair requirement for small wins, plus seven pairs and all-pungs big-win recognition. Random-eye wins are allowed for all-pungs and pure-suit hands. Exposes Changsha win-type classification output for xiao hu/da hu tagging (including Big Four Happiness, Banhu, Missing One Suit, Six Six Straight, All 2/5/8, Pure Suit, Luxury Seven Pairs, and event-context flags such as Heavenly/Earthly/last-tile/kong variants). Kongs add one tile each (up to {Changsha258Ruleset.MaxKongs}, {Changsha258Ruleset.MaxTiles} tiles total). Includes Changsha settlement accounting for small/big wins, bird catch payouts, dealer progression, and per-player score deltas. No minimum fan gate. Default ruleset."
            : $"{wallTotal} tiles with honors/flowers. Standard Chinese Official rules (8-fan minimum to win).";
    }

    private static string FormatSmallWinPairRanks()
        => string.Join("/", Changsha258Ruleset.SortedSmallWinPairRanks);

    private static MahjongRuleset ResolveRuleset(string? ruleset)
        => RulesetResolver.Resolve(ruleset);
}
