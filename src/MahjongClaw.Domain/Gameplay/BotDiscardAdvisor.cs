using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotDiscardAdvisor
{
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);

    public static int SelectDiscardIndex(IReadOnlyList<Tile> handTiles, MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
    {
        ArgumentNullException.ThrowIfNull(handTiles);

        if (handTiles.Count == 0)
        {
            throw new ArgumentException("Hand cannot be empty.", nameof(handTiles));
        }

        if (handTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(handTiles));
        }

        var candidates = handTiles
            .Select((tile, index) =>
            {
                var winningOutSummary = CountWinningDrawOutsAfterDiscard(handTiles, index, ruleset);
                return new
                {
                    Tile = tile,
                    Index = index,
                    Score = GetBotTileKeepScore(tile, handTiles, ruleset),
                    WinningDrawOuts = winningOutSummary.TileTypes,
                    WinningDrawOutCopies = winningOutSummary.RemainingCopies
                };
            })
            .ToList();

        var maxWinningDrawOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
        var prioritizedCandidates = maxWinningDrawOuts > 0
            ? candidates.Where(candidate => candidate.WinningDrawOuts == maxWinningDrawOuts).ToList()
            : candidates;

        if (maxWinningDrawOuts > 0)
        {
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

    private static int GetBotTileKeepScore(Tile tile, IReadOnlyList<Tile> handTiles, MahjongRuleset ruleset)
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
        if (ruleset != MahjongRuleset.Changsha258 && pairCount >= 3)
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
            smallWinPairBonus = tileMatchCount switch
            {
                >= 2 => 3,
                1 => 1,
                _ => 0
            };
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
            ? 4
            : 0;

        var suitedPairCount = handTiles
            .Where(static other => !other.IsHonor)
            .GroupBy(static other => (other.Suit, other.Rank))
            .Count(group => group.Count() >= 2);

        var suitTiles = handTiles
            .Where(other => other.Suit == tile.Suit)
            .ToList();
        var suitCount = suitTiles.Count;
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
        var suitedCounts = handTiles
            .Where(static other => !other.IsHonor)
            .GroupBy(static other => other.Suit)
            .ToDictionary(group => group.Key, group => group.Count());
        var suitFocusPenalty = 0;
        if (!tile.IsHonor && suitedCounts.Count > 1)
        {
            var minSuitCount = suitedCounts.Values.Min();
            if (suitCount == minSuitCount)
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
            if (suitCount == maxSuitCount && maxSuitCount >= 6)
            {
                suitFocusBonus = maxSuitCount >= 8 ? 2 : 1;
            }
        }

        var dominantSuitPenalty = 0;
        if (!tile.IsHonor && suitedCounts.Count > 1)
        {
            var maxSuitCount = suitedCounts.Values.Max();
            if (suitCount < maxSuitCount)
            {
                dominantSuitPenalty = maxSuitCount >= 8 ? -2 : maxSuitCount >= 7 ? -1 : 0;
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

            return score + suitClusterBonus + suitFocusPenalty + suitFocusBonus + honorFocusPenalty + sevenPairsBias + honorMajorityBonus + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus;
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

        return score + suitClusterBonus + suitSingletonPenalty + suitPairBonus + suitFocusPenalty + suitFocusBonus + dominantSuitPenalty + sevenPairsBias + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus;
    }

    private static WinningDrawOutSummary CountWinningDrawOutsAfterDiscard(IReadOnlyList<Tile> handTiles, int discardIndex, MahjongRuleset ruleset)
    {
        var drawTileTypes = ruleset == MahjongRuleset.Changsha258
            ? Changsha258DrawTileTypes
            : ChineseOfficialDrawTileTypes;

        var reducedHand = handTiles
            .Where((_, index) => index != discardIndex)
            .ToList();
        var reducedCounts = reducedHand
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningDrawOuts = 0;
        var winningDrawOutCopies = 0;

        foreach (var drawTileType in drawTileTypes)
        {
            reducedCounts.TryGetValue(drawTileType, out var drawTileCount);
            if (drawTileCount >= 4)
            {
                continue;
            }

            var candidateHand = reducedHand
                .Append(drawTileType)
                .ToList();

            if (RulesetHandAnalyzer.IsWinningHand(candidateHand, ruleset))
            {
                winningDrawOuts++;
                winningDrawOutCopies += 4 - drawTileCount;
            }
        }

        return new WinningDrawOutSummary(winningDrawOuts, winningDrawOutCopies);
    }

    private readonly record struct WinningDrawOutSummary(int TileTypes, int RemainingCopies);

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

    private static bool IsTerminalSuitedTile(Tile tile)
        => !tile.IsHonor && tile.Rank is 1 or 9;
}
