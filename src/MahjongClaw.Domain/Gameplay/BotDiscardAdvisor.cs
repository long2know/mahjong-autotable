using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotDiscardAdvisor
{
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);
    private static readonly IReadOnlyList<Tile> Changsha258SmallWinPairTiles = BuildChangshaSmallWinPairTiles();
    private const int LastSmallWinSingletonKeepBonus = 12;

    public static int SelectDiscardIndex(IReadOnlyList<Tile> handTiles, MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
        => SelectDiscardIndex(handTiles, Array.Empty<Tile>(), ruleset);

    public static int SelectDiscardIndex(IReadOnlyList<Tile> handTiles, IReadOnlyList<Tile> meldTiles, MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
        => SelectDiscardIndexInternal(handTiles, meldTiles, Array.Empty<Tile>(), ruleset);

    public static int SelectDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
    {
        ArgumentNullException.ThrowIfNull(visibleTiles);

        if (visibleTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(visibleTiles));
        }

        return SelectDiscardIndexInternal(handTiles, meldTiles, visibleTiles, ruleset);
    }

    private static int SelectDiscardIndexInternal(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset)
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

        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        Tile? lastSmallWinSingleton = null;
        if (ruleset == MahjongRuleset.Changsha258)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            var smallWinTileCount = handTiles.Count(static tile => Changsha258Ruleset.IsSmallWinPair(tile));

            if (smallWinPairCount == 0 && smallWinTileCount == 1)
            {
                var candidate = handTiles.First(static tile => Changsha258Ruleset.IsSmallWinPair(tile));
                ownedCounts.TryGetValue(candidate, out var ownedCount);
                visibleCounts.TryGetValue(candidate, out var visibleCount);
                var remainingCopies = 4 - ownedCount - visibleCount;

                if (remainingCopies > 0)
                {
                    lastSmallWinSingleton = candidate;
                }
            }
        }

        var hasLiveSmallWinPairCandidate = HasLiveSmallWinPairCandidate(handCounts, ownedCounts, visibleCounts, ruleset);
        var hasStrongSmallWinPairCandidate = ruleset == MahjongRuleset.Changsha258 && visibleCounts.Count > 0
            ? HasStrongSmallWinPairCandidate(handCounts, ownedCounts, visibleCounts)
            : hasLiveSmallWinPairCandidate;
        var liveSmallWinPairCandidates = ruleset == MahjongRuleset.Changsha258 && visibleCounts.Count > 0
            ? BuildLiveSmallWinPairCandidates(handCounts, ownedCounts, visibleCounts)
            : null;

        var candidates = handTiles
            .Select((tile, index) =>
            {
                var winningOutSummary = CountWinningDrawOutsAfterDiscard(handTiles, meldTiles, visibleCounts, index, ruleset);
                return new
                {
                    Tile = tile,
                    Index = index,
                    Score = GetBotTileKeepScore(
                            tile,
                            handTiles,
                            meldTiles,
                            ruleset,
                            hasLiveSmallWinPairCandidate,
                            hasStrongSmallWinPairCandidate,
                            liveSmallWinPairCandidates,
                            ownedCounts,
                            visibleCounts)
                        + GetTileAvailabilityPenalty(tile, handCounts, ownedCounts, visibleCounts)
                        + GetDeadAdjacencyPenalty(tile, handCounts, ownedCounts, visibleCounts)
                        + GetChangshaSmallWinPairVisibilityAdjustment(tile, handCounts, ownedCounts, visibleCounts, ruleset)
                        + (lastSmallWinSingleton is not null && tile.Equals(lastSmallWinSingleton)
                            ? LastSmallWinSingletonKeepBonus
                            : 0),
                    WinningDrawOuts = winningOutSummary.TileTypes,
                    WinningDrawOutCopies = winningOutSummary.RemainingCopies,
                    SmallWinningDrawOuts = winningOutSummary.SmallWinTileTypes,
                    SmallWinningDrawOutCopies = winningOutSummary.SmallWinRemainingCopies,
                    BigWinningDrawOuts = winningOutSummary.BigWinTileTypes,
                    BigWinningDrawOutCopies = winningOutSummary.BigWinRemainingCopies
                };
            })
            .ToList();

        var prioritizedCandidates = candidates;
        var maxSmallWinOuts = 0;

        if (ruleset == MahjongRuleset.Changsha258)
        {
            maxSmallWinOuts = prioritizedCandidates.Max(candidate => candidate.SmallWinningDrawOuts);
            if (maxSmallWinOuts > 0)
            {
                prioritizedCandidates = prioritizedCandidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == maxSmallWinOuts)
                    .ToList();

                var maxSmallWinCopies = prioritizedCandidates.Max(candidate => candidate.SmallWinningDrawOutCopies);
                if (maxSmallWinCopies > 0)
                {
                    prioritizedCandidates = prioritizedCandidates
                        .Where(candidate => candidate.SmallWinningDrawOutCopies == maxSmallWinCopies)
                        .ToList();
                }
            }
        }

        var maxWinningDrawOuts = prioritizedCandidates.Max(candidate => candidate.WinningDrawOuts);
        if (maxWinningDrawOuts > 0)
        {
            prioritizedCandidates = prioritizedCandidates
                .Where(candidate => candidate.WinningDrawOuts == maxWinningDrawOuts)
                .ToList();

            var maxWinningDrawOutCopies = prioritizedCandidates.Max(candidate => candidate.WinningDrawOutCopies);
            prioritizedCandidates = prioritizedCandidates
                .Where(candidate => candidate.WinningDrawOutCopies == maxWinningDrawOutCopies)
                .ToList();
        }

        return prioritizedCandidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Tile.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate.Tile) ? 0 : 1)
            .ThenBy(candidate => candidate.Tile.Suit)
            .ThenBy(candidate => candidate.Tile.Rank)
            .ThenBy(candidate => candidate.Tile.Wind.HasValue ? (int)candidate.Tile.Wind.Value : -1)
            .ThenBy(candidate => candidate.Tile.Dragon.HasValue ? (int)candidate.Tile.Dragon.Value : -1)
            .ThenBy(candidate => candidate.Index)
            .First()
            .Index;
    }

    private static int GetBotTileKeepScore(
        Tile tile,
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        bool hasLiveSmallWinPairCandidate,
        bool hasStrongSmallWinPairCandidate,
        IReadOnlySet<Tile>? liveSmallWinPairCandidates,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (ruleset == MahjongRuleset.Changsha258 && !Changsha258Ruleset.IsAllowedTile(tile))
        {
            return -200;
        }

        if (tile.IsFlower)
        {
            return -100;
        }

        var score = 0;
        var tileCounts = handTiles
            .GroupBy(static other => other)
            .ToDictionary(group => group.Key, group => group.Count());
        var honorCount = handTiles.Count(static other => other.IsHonor);
        tileCounts.TryGetValue(tile, out var tileMatchCount);
        var meldMatchCount = meldTiles.Count(other => other.Equals(tile));
        var meldExtensionBonus = meldMatchCount >= 3 ? 6 : 0;
        if (tileMatchCount >= 2)
        {
            score += tileMatchCount switch
            {
                2 => 2,
                3 => 3,
                _ => 4
            };
        }

        if (tileMatchCount >= 4)
        {
            score += 2;
        }

        var pairCount = tileCounts.Count(static entry => entry.Value >= 2);
        var smallWinPairCount = ruleset == MahjongRuleset.Changsha258
            ? tileCounts.Count(static entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
            : 0;
        if (tileMatchCount == 2 && pairCount == 1)
        {
            score += 2;
        }

        var sevenPairsBias = 0;
        if (meldTiles.Count == 0 && pairCount >= 3)
        {
            if (tileMatchCount >= 2)
            {
                sevenPairsBias = pairCount switch
                {
                    >= 6 => 3,
                    5 => 2,
                    4 => 1,
                    _ => 1
                };
            }
            else
            {
                sevenPairsBias = pairCount switch
                {
                    >= 6 => -3,
                    5 => -2,
                    4 => -1,
                    _ => -1
                };
            }
        }

        var smallWinPairBonus = 0;
        if (ruleset == MahjongRuleset.Changsha258 && Changsha258Ruleset.IsSmallWinPair(tile))
        {
            if (smallWinPairCount >= 1 && tileMatchCount == 1)
            {
                smallWinPairBonus = 0;
            }
            else
            {
                smallWinPairBonus = tileMatchCount switch
                {
                    >= 2 => 3,
                    1 => 1,
                    _ => 0
                };
            }
        }

        var requiredSmallWinPairBonus = ruleset == MahjongRuleset.Changsha258
            && smallWinPairCount == 1
            && tileMatchCount >= 2
            && Changsha258Ruleset.IsSmallWinPair(tile)
            ? 6
            : 0;

        var missingSmallWinPairBonus = ruleset == MahjongRuleset.Changsha258
            && smallWinPairCount == 0
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && hasLiveSmallWinPairCandidate
            ? 4
            : 0;

        var deadSmallWinPairPenalty = ruleset == MahjongRuleset.Changsha258
            && smallWinPairCount == 0
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && !hasLiveSmallWinPairCandidate
            ? -3
            : 0;

        var nonSmallWinPairPenalty = ruleset == MahjongRuleset.Changsha258
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && hasStrongSmallWinPairCandidate
            ? -10
            : 0;

        var fallbackNonSmallWinPairBonus = ruleset == MahjongRuleset.Changsha258
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && !hasStrongSmallWinPairCandidate
            ? 6
            : 0;

        var soleSmallWinPairKeepBonus = 0;
        if (ruleset == MahjongRuleset.Changsha258
            && tileMatchCount == 2
            && smallWinPairCount == 1
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && liveSmallWinPairCandidates is not null
            && liveSmallWinPairCandidates.Count == 1
            && liveSmallWinPairCandidates.Contains(tile))
        {
            soleSmallWinPairKeepBonus = 8;
        }

        var soleSmallWinPairSingletonBonus = 0;
        if (ruleset == MahjongRuleset.Changsha258
            && tileMatchCount == 1
            && smallWinPairCount == 0
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && liveSmallWinPairCandidates is not null
            && liveSmallWinPairCandidates.Count == 1
            && liveSmallWinPairCandidates.Contains(tile))
        {
            soleSmallWinPairSingletonBonus = 14;

            if (visibleCounts.Count > 0)
            {
                ownedCounts.TryGetValue(tile, out var ownedCount);
                visibleCounts.TryGetValue(tile, out var visibleCount);
                var remainingCopies = 4 - ownedCount - visibleCount;

                soleSmallWinPairSingletonBonus = remainingCopies switch
                {
                    <= 1 => 8,
                    2 => 11,
                    _ => 14
                };
            }
        }

        var suitedPairCount = handTiles
            .Where(static other => !other.IsHonor)
            .GroupBy(static other => (other.Suit, other.Rank))
            .Count(group => group.Count() >= 2);

        var focusTiles = meldTiles.Count == 0
            ? handTiles
            : handTiles.Concat(meldTiles).ToList();

        var suitTiles = handTiles
            .Where(other => other.Suit == tile.Suit)
            .ToList();
        var suitCount = suitTiles.Count;
        var focusSuitCount = !tile.IsHonor
            ? focusTiles.Count(other => other.Suit == tile.Suit)
            : suitCount;
        var suitClusterBonus = !tile.IsHonor
            ? Math.Max(0, (suitCount - 1) / 2)
            : 0;
        var suitSingletonPenalty = !tile.IsHonor
            ? suitCount switch
            {
                1 => -2,
                2 => -1,
                _ => 0
            }
            : 0;
        var suitPairCount = !tile.IsHonor
            ? suitTiles
                .GroupBy(other => other.Rank)
                .Count(group => group.Count() >= 2)
            : 0;
        var suitPairBonus = suitPairCount switch
        {
            >= 3 => 2,
            2 => 1,
            _ => 0
        };
        var suitedCounts = focusTiles
            .Where(static other => !other.IsHonor)
            .GroupBy(static other => other.Suit)
            .ToDictionary(group => group.Key, group => group.Count());
        var suitFocusPenalty = 0;
        if (!tile.IsHonor && suitedCounts.Count > 1)
        {
            var minSuitCount = suitedCounts.Values.Min();
            if (focusSuitCount == minSuitCount)
            {
                suitFocusPenalty = minSuitCount switch
                {
                    1 => -2,
                    2 => -1,
                    _ => 0
                };
            }
        }

        var suitFocusBonus = 0;
        if (!tile.IsHonor && suitedCounts.Count > 0)
        {
            var maxSuitCount = suitedCounts.Values.Max();
            if (focusSuitCount == maxSuitCount && maxSuitCount >= 6)
            {
                suitFocusBonus = maxSuitCount >= 8 ? 2 : 1;
            }
        }

        var dominantSuitPenalty = 0;
        if (!tile.IsHonor && suitedCounts.Count > 1)
        {
            var maxSuitCount = suitedCounts.Values.Max();
            if (focusSuitCount < maxSuitCount)
            {
                dominantSuitPenalty = maxSuitCount >= 8 ? -2 : maxSuitCount >= 7 ? -1 : 0;
            }
        }

        var meldSuitFocusBonus = 0;
        if (!tile.IsHonor && meldTiles.Count > 0)
        {
            var meldSuitCounts = meldTiles
                .Where(static other => !other.IsHonor)
                .GroupBy(static other => other.Suit)
                .ToDictionary(group => group.Key, group => group.Count());

            if (meldSuitCounts.Count > 0)
            {
                var maxMeldSuitCount = meldSuitCounts.Values.Max();
                var dominantSuitCandidates = meldSuitCounts
                    .Where(entry => entry.Value == maxMeldSuitCount)
                    .Select(entry => entry.Key)
                    .ToList();

                if (dominantSuitCandidates.Count == 1 && maxMeldSuitCount >= 3)
                {
                    var dominantSuit = dominantSuitCandidates[0];
                    if (tile.Suit == dominantSuit)
                    {
                        meldSuitFocusBonus = maxMeldSuitCount >= 6 ? 3 : 2;
                    }
                    else
                    {
                        meldSuitFocusBonus = maxMeldSuitCount >= 6 ? -2 : -1;
                    }
                }
            }
        }

        var honorSingletonCount = handTiles
            .Where(static other => other.IsHonor)
            .GroupBy(static other => other)
            .Count(group => group.Count() == 1);

        var hasHonorMatch = false;
        var honorMatchCount = 0;

        foreach (var other in suitTiles)
        {
            if (ReferenceEquals(tile, other))
            {
                continue;
            }

            if (tile.IsHonor)
            {
                if (tile.Wind == other.Wind && tile.Dragon == other.Dragon)
                {
                    score += 3;
                    hasHonorMatch = true;
                    honorMatchCount++;
                }

                continue;
            }

            if (other.Rank == tile.Rank)
            {
                score += 2;
                continue;
            }

            var diff = Math.Abs(other.Rank - tile.Rank);
            if (diff == 1)
            {
                score += 2;
            }
            else if (diff == 2)
            {
                score += 1;
            }
        }

        if (tile.IsHonor)
        {
            var honorFocusPenalty = 0;
            if (!hasHonorMatch)
            {
                score -= suitCount == 1 ? 3 : 2;

                if (suitedCounts.Count > 0)
                {
                    var maxSuitCount = suitedCounts.Values.Max();
                    if (maxSuitCount >= 6)
                    {
                        honorFocusPenalty = maxSuitCount >= 8 ? -2 : -1;
                    }
                }

                score += honorSingletonCount switch
                {
                    >= 5 => -2,
                    >= 3 => -1,
                    _ => 0
                };

                if (suitedPairCount >= 2)
                {
                    honorFocusPenalty += suitedPairCount >= 3 ? -2 : -1;
                }
            }
            else
            {
                var matchSetSize = honorMatchCount + 1;
                score += matchSetSize switch
                {
                    >= 4 => 3,
                    3 => 2,
                    2 => 1,
                    _ => 0
                };
            }

            var honorMajorityBonus = tileMatchCount == 1 && honorCount >= 7 ? 4 : 0;

            return score + suitClusterBonus + suitFocusPenalty + suitFocusBonus + honorFocusPenalty + sevenPairsBias + honorMajorityBonus + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus + deadSmallWinPairPenalty + nonSmallWinPairPenalty + meldSuitFocusBonus + meldExtensionBonus + soleSmallWinPairKeepBonus + soleSmallWinPairSingletonBonus;
        }

        var rankCounts = suitTiles
            .GroupBy(other => other.Rank)
            .ToDictionary(group => group.Key, group => group.Count());

        rankCounts.TryGetValue(tile.Rank, out var sameRankCount);
        if (sameRankCount >= 2)
        {
            score += 2;
        }

        if (sameRankCount >= 3)
        {
            score += 2;
        }

        var minus1Count = rankCounts.TryGetValue(tile.Rank - 1, out var minus1) ? minus1 : 0;
        var plus1Count = rankCounts.TryGetValue(tile.Rank + 1, out var plus1) ? plus1 : 0;
        var minus2Count = rankCounts.TryGetValue(tile.Rank - 2, out var minus2) ? minus2 : 0;
        var plus2Count = rankCounts.TryGetValue(tile.Rank + 2, out var plus2) ? plus2 : 0;
        var hasMinus1 = minus1Count > 0;
        var hasPlus1 = plus1Count > 0;
        var hasMinus2 = minus2Count > 0;
        var hasPlus2 = plus2Count > 0;
        var hasNeighbor = hasMinus1 || hasPlus1 || hasMinus2 || hasPlus2;
        var hasDirectNeighbor = hasMinus1 || hasPlus1;
        var hasGapNeighbor = hasMinus2 || hasPlus2;

        if (tile.Rank is >= 3 and <= 7 && hasDirectNeighbor)
        {
            score += 1;
        }

        if (hasMinus1 && hasPlus1)
        {
            score += 2;

            if (minus1Count > 1 || plus1Count > 1)
            {
                score += 1;
            }

            if (minus1Count > 1 && plus1Count > 1)
            {
                score += 1;
            }
        }

        if (sameRankCount >= 2 && (hasMinus1 || hasPlus1))
        {
            score += hasMinus1 && hasPlus1 ? 2 : 1;
        }

        if (sameRankCount >= 2 && hasMinus1 && hasPlus1)
        {
            score += 1;
        }

        if (hasMinus2 && hasMinus1)
        {
            score += 1;
        }

        if (hasPlus1 && hasPlus2)
        {
            score += 1;
        }

        if (sameRankCount <= 1)
        {
            if (hasMinus1 && minus1Count >= 2)
            {
                score += 1;
            }

            if (hasPlus1 && plus1Count >= 2)
            {
                score += 1;
            }
        }

        if ((tile.Rank == 1 && hasPlus1) || (tile.Rank == 9 && hasMinus1))
        {
            score += 1;
        }

        if (sameRankCount <= 1 && !hasNeighbor)
        {
            score -= 1;
        }

        if (sameRankCount <= 1 && !hasDirectNeighbor && hasGapNeighbor)
        {
            score -= 1;
        }

        if ((tile.Rank == 1 || tile.Rank == 9) && sameRankCount <= 1 && !hasMinus1 && !hasPlus1)
        {
            score -= suitCount <= 2 ? 2 : 1;

            if (suitCount == 1)
            {
                score -= 1;
            }
        }

        if (suitFocusPenalty < 0 && sameRankCount <= 1 && !hasNeighbor)
        {
            score -= 1;
        }

        if (suitCount <= 2 && sameRankCount <= 1 && !hasNeighbor)
        {
            score -= 1;
        }

        return score + suitClusterBonus + suitSingletonPenalty + suitPairBonus + suitFocusPenalty + suitFocusBonus + dominantSuitPenalty + sevenPairsBias + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus + deadSmallWinPairPenalty + nonSmallWinPairPenalty + fallbackNonSmallWinPairBonus + meldSuitFocusBonus + meldExtensionBonus + soleSmallWinPairKeepBonus + soleSmallWinPairSingletonBonus;
    }

    private static WinningDrawOutSummary CountWinningDrawOutsAfterDiscard(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        int discardIndex,
        MahjongRuleset ruleset)
    {
        var drawTileTypes = ruleset == MahjongRuleset.Changsha258
            ? Changsha258DrawTileTypes
            : ChineseOfficialDrawTileTypes;

        var discardTile = handTiles[discardIndex];
        var reducedHand = handTiles
            .Where((_, index) => index != discardIndex)
            .ToList();
        var ownedTiles = reducedHand
            .Concat(meldTiles)
            .ToList();
        var ownedCounts = ownedTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningDrawOuts = 0;
        var winningDrawOutCopies = 0;
        var smallWinDrawOuts = 0;
        var smallWinDrawOutCopies = 0;
        var bigWinDrawOuts = 0;
        var bigWinDrawOutCopies = 0;

        foreach (var drawTileType in drawTileTypes)
        {
            ownedCounts.TryGetValue(drawTileType, out var drawTileCount);
            visibleCounts.TryGetValue(drawTileType, out var visibleCount);
            if (drawTileType.Equals(discardTile))
            {
                visibleCount++;
            }

            var remainingCopies = 4 - drawTileCount - visibleCount;

            if (remainingCopies <= 0)
            {
                continue;
            }

            var candidateHand = ownedTiles
                .Append(drawTileType)
                .ToList();

            if (RulesetHandAnalyzer.TryGetWinningHand(candidateHand, ruleset, out var winningHand))
            {
                winningDrawOuts++;
                winningDrawOutCopies += remainingCopies;

                if (ruleset == MahjongRuleset.Changsha258 && winningHand is not null)
                {
                    var evaluation = ChangshaWinTypeEvaluator.Evaluate(candidateHand, winningHand);
                    if (evaluation.IsBigWin)
                    {
                        bigWinDrawOuts++;
                        bigWinDrawOutCopies += remainingCopies;
                    }
                    else
                    {
                        smallWinDrawOuts++;
                        smallWinDrawOutCopies += remainingCopies;
                    }
                }
            }
        }

        return new WinningDrawOutSummary(
            winningDrawOuts,
            winningDrawOutCopies,
            smallWinDrawOuts,
            smallWinDrawOutCopies,
            bigWinDrawOuts,
            bigWinDrawOutCopies);
    }

    private readonly record struct WinningDrawOutSummary(
        int TileTypes,
        int RemainingCopies,
        int SmallWinTileTypes,
        int SmallWinRemainingCopies,
        int BigWinTileTypes,
        int BigWinRemainingCopies);

    private static IReadOnlyList<Tile> BuildDrawTileTypes(MahjongRuleset ruleset)
    {
        IEnumerable<TileSuit> suitedSuits = ruleset == MahjongRuleset.Changsha258
            ? Changsha258Ruleset.AllowedSuits
            : new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos };

        var suitedTiles = suitedSuits
            .SelectMany(static suit => Enumerable.Range(1, 9).Select(rank => new Tile(suit, rank)));

        if (ruleset == MahjongRuleset.Changsha258)
        {
            return suitedTiles.ToArray();
        }

        var windTiles = Enum
            .GetValues<Wind>()
            .Select(wind => new Tile(TileSuit.Winds, 0, wind));
        var dragonTiles = Enum
            .GetValues<Dragon>()
            .Select(dragon => new Tile(TileSuit.Dragons, 0, null, dragon));

        return suitedTiles
            .Concat(windTiles)
            .Concat(dragonTiles)
            .ToArray();
    }

    private static IReadOnlySet<Tile> BuildLiveSmallWinPairCandidates(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        var candidates = new HashSet<Tile>();

        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            handCounts.TryGetValue(tile, out var handCount);
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            var remainingCopies = 4 - ownedCount - visibleCount;

            if (handCount >= 2)
            {
                candidates.Add(tile);
                continue;
            }

            if (handCount == 1)
            {
                if (remainingCopies >= 1)
                {
                    candidates.Add(tile);
                }

                continue;
            }

            if (remainingCopies >= 2)
            {
                candidates.Add(tile);
            }
        }

        return candidates;
    }

    private static bool HasLiveSmallWinPairCandidate(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset)
    {
        if (ruleset != MahjongRuleset.Changsha258 || visibleCounts.Count == 0)
        {
            return true;
        }

        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            handCounts.TryGetValue(tile, out var handCount);
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            var remainingCopies = 4 - ownedCount - visibleCount;
            if (handCount > 0)
            {
                if (remainingCopies >= 1)
                {
                    return true;
                }
            }
            else if (remainingCopies >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStrongSmallWinPairCandidate(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (visibleCounts.Count == 0)
        {
            return true;
        }

        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            handCounts.TryGetValue(tile, out var handCount);
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            var remainingCopies = 4 - ownedCount - visibleCount;
            if (handCount >= 2)
            {
                return true;
            }

            if (handCount == 1)
            {
                if (remainingCopies >= 2)
                {
                    return true;
                }

                continue;
            }

            if (remainingCopies >= 3)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetChangshaSmallWinPairVisibilityAdjustment(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset)
    {
        if (ruleset != MahjongRuleset.Changsha258 || visibleCounts.Count == 0 || !Changsha258Ruleset.IsSmallWinPair(tile))
        {
            return 0;
        }

        handCounts.TryGetValue(tile, out var handCount);
        if (handCount >= 2)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            if (smallWinPairCount <= 1)
            {
                return 0;
            }

            ownedCounts.TryGetValue(tile, out var ownedPairCount);
            visibleCounts.TryGetValue(tile, out var visiblePairCount);
            var remainingPairCopies = 4 - ownedPairCount - visiblePairCount;

            return remainingPairCopies switch
            {
                <= 0 => -6,
                1 => -4,
                2 => -2,
                _ => 0
            };
        }

        if (handCount != 1)
        {
            return 0;
        }

        ownedCounts.TryGetValue(tile, out var ownedCount);
        visibleCounts.TryGetValue(tile, out var visibleCount);
        var remainingCopies = 4 - ownedCount - visibleCount;

        if (ownedCount >= 3)
        {
            return remainingCopies switch
            {
                <= 0 => -20,
                1 => -10,
                2 => -5,
                _ => 0
            };
        }

        bool HasAdjacentRank(int targetRank)
        {
            if (targetRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            return handCounts.TryGetValue(new Tile(tile.Suit, targetRank), out var count) && count > 0;
        }

        var hasAdjacent = HasAdjacentRank(tile.Rank - 1) || HasAdjacentRank(tile.Rank + 1);
        if (hasAdjacent)
        {
            return remainingCopies switch
            {
                <= 0 => -6,
                1 => -4,
                2 => -2,
                _ => 0
            };
        }

        return remainingCopies switch
        {
            <= 0 => -20,
            1 => -10,
            2 => -5,
            _ => 0
        };
    }

    private static int GetDeadAdjacencyPenalty(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (!IsSuitedTile(tile))
        {
            return 0;
        }

        var suit = tile.Suit;
        var rank = tile.Rank;
        var penalty = 0;

        bool HasTileInHand(int targetRank)
        {
            if (targetRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            return handCounts.TryGetValue(new Tile(suit, targetRank), out var count) && count > 0;
        }

        bool HasLiveTile(int targetRank)
        {
            if (targetRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            var targetTile = new Tile(suit, targetRank);
            if (handCounts.TryGetValue(targetTile, out var handCount) && handCount > 0)
            {
                return true;
            }

            ownedCounts.TryGetValue(targetTile, out var ownedCount);
            visibleCounts.TryGetValue(targetTile, out var visibleCount);
            return 4 - ownedCount - visibleCount > 0;
        }

        var hasLowerNeighbor = HasTileInHand(rank - 1);
        var hasUpperNeighbor = HasTileInHand(rank + 1);
        var hasLowerGap = HasTileInHand(rank - 2);
        var hasUpperGap = HasTileInHand(rank + 2);

        if (hasLowerNeighbor)
        {
            var hasCompletion = HasLiveTile(rank - 2) || HasLiveTile(rank + 1);
            if (!hasCompletion)
            {
                penalty -= 4;
            }
        }

        if (hasUpperNeighbor)
        {
            var hasCompletion = HasLiveTile(rank - 1) || HasLiveTile(rank + 2);
            if (!hasCompletion)
            {
                penalty -= 4;
            }
        }

        if (!hasLowerNeighbor && !hasUpperNeighbor && (hasLowerGap || hasUpperGap))
        {
            var hasLiveLowerMiddle = hasLowerGap && HasLiveTile(rank - 1);
            var hasLiveUpperMiddle = hasUpperGap && HasLiveTile(rank + 1);

            if (!hasLiveLowerMiddle && !hasLiveUpperMiddle)
            {
                penalty -= 3;
            }
        }

        return penalty;
    }

    private static int GetTileAvailabilityPenalty(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        ownedCounts.TryGetValue(tile, out var ownedCount);
        visibleCounts.TryGetValue(tile, out var visibleCount);
        var remainingSelf = 4 - ownedCount - visibleCount;

        if (ownedCount >= 2)
        {
            return 0;
        }

        if (tile.IsHonor)
        {
            return remainingSelf switch
            {
                <= 0 => -4,
                1 => -2,
                2 => -1,
                _ => 0
            };
        }

        if (!IsSuitedTile(tile))
        {
            return 0;
        }

        if (!HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts))
        {
            return remainingSelf switch
            {
                <= 0 => -4,
                1 => -3,
                2 => -2,
                _ => -1
            };
        }

        return remainingSelf <= 0 ? -1 : 0;
    }

    private static bool HasSequencePotential(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (!IsSuitedTile(tile))
        {
            return false;
        }

        bool IsTileAvailable(Tile candidate)
        {
            handCounts.TryGetValue(candidate, out var handCount);
            if (handCount > 0)
            {
                return true;
            }

            ownedCounts.TryGetValue(candidate, out var ownedCount);
            visibleCounts.TryGetValue(candidate, out var visibleCount);
            return 4 - ownedCount - visibleCount > 0;
        }

        var rank = tile.Rank;
        var suit = tile.Suit;

        var hasLowSequence = rank >= 3
            && IsTileAvailable(new Tile(suit, rank - 2))
            && IsTileAvailable(new Tile(suit, rank - 1));
        var hasMiddleSequence = rank is >= 2 and <= 8
            && IsTileAvailable(new Tile(suit, rank - 1))
            && IsTileAvailable(new Tile(suit, rank + 1));
        var hasHighSequence = rank <= 7
            && IsTileAvailable(new Tile(suit, rank + 1))
            && IsTileAvailable(new Tile(suit, rank + 2));

        return hasLowSequence || hasMiddleSequence || hasHighSequence;
    }

    private static bool IsSuitedTile(Tile tile)
        => tile.Suit is TileSuit.Characters or TileSuit.Dots or TileSuit.Bamboos;

    private static bool IsTerminalSuitedTile(Tile tile)
        => !tile.IsHonor && tile.Rank is 1 or 9;

    private static IReadOnlyList<Tile> BuildChangshaSmallWinPairTiles()
        => Changsha258Ruleset.AllowedSuits
            .SelectMany(suit => Changsha258Ruleset.SortedSmallWinPairRanks.Select(rank => new Tile(suit, rank)))
            .ToArray();
}
