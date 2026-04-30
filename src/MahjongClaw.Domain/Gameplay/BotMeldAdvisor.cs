using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotMeldAdvisor
{
    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
        => TrySelectConcealedKong(handTiles, Array.Empty<Tile>(), ruleset, out kongTile);

    public static bool TrySelectConcealedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(handTiles));
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var candidates = handCounts
            .Where(entry => entry.Value == 4)
            .Select(entry => entry.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            kongTile = null!;
            return false;
        }

        if (ShouldAvoidSevenPairsConcealedKong(handCounts, meldTiles))
        {
            kongTile = null!;
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
            {
                var smallWinPairs = handCounts
                    .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .ToHashSet();

                candidates = candidates
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var prioritizedCandidates = candidates
            .OrderBy(candidate => ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(candidate) ? 1 : 0)
            .ThenBy(candidate => candidate.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate) ? 0 : 1)
            .ThenBy(candidate => candidate.Suit)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Wind.HasValue ? (int)candidate.Wind.Value : -1)
            .ThenBy(candidate => candidate.Dragon.HasValue ? (int)candidate.Dragon.Value : -1)
            .ToList();

        kongTile = prioritizedCandidates[0];
        return true;
    }

    public static bool TrySelectPromotedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        out Tile kongTile)
    {
        ArgumentNullException.ThrowIfNull(handTiles);
        ArgumentNullException.ThrowIfNull(meldTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null) || meldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.");
        }

        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var meldCounts = meldTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var candidates = meldCounts
            .Where(entry => entry.Value == 3 && handCounts.ContainsKey(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            kongTile = null!;
            return false;
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var allowFlexibleEye = Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
            if (!allowFlexibleEye)
            {
                var smallWinPairs = handCounts
                    .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .ToHashSet();

                candidates = candidates
                    .Where(candidate => !ShouldAvoidChangshaSmallWinKong(candidate, smallWinPairs))
                    .ToList();

                if (candidates.Count == 0)
                {
                    kongTile = null!;
                    return false;
                }
            }
        }

        var prioritizedCandidates = candidates
            .OrderBy(candidate => ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(candidate) ? 1 : 0)
            .ThenBy(candidate => candidate.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate) ? 0 : 1)
            .ThenBy(candidate => candidate.Suit)
            .ThenBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Wind.HasValue ? (int)candidate.Wind.Value : -1)
            .ThenBy(candidate => candidate.Dragon.HasValue ? (int)candidate.Dragon.Value : -1)
            .ToList();

        kongTile = prioritizedCandidates[0];
        return true;
    }

    private static bool ShouldAvoidSevenPairsConcealedKong(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyList<Tile> meldTiles)
    {
        if (meldTiles.Count > 0)
        {
            return false;
        }

        var pairCount = handCounts.Values.Sum(static count => count / 2);
        return pairCount >= 6;
    }

    private static bool ShouldAvoidChangshaSmallWinKong(Tile candidate, IReadOnlySet<Tile> smallWinPairs)
    {
        if (!Changsha258Ruleset.IsSmallWinPair(candidate))
        {
            return false;
        }

        if (smallWinPairs.Count == 0)
        {
            return true;
        }

        if (smallWinPairs.Count == 1 && smallWinPairs.Contains(candidate))
        {
            return true;
        }

        return false;
    }

    private static bool IsTerminalSuitedTile(Tile tile)
        => !tile.IsHonor && tile.Rank is 1 or 9;
}
