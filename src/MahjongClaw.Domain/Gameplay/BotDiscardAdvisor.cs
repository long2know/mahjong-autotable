using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class BotDiscardAdvisor
{
    private static readonly IReadOnlyList<Tile> ChineseOfficialDrawTileTypes = BuildDrawTileTypes(MahjongRuleset.ChineseOfficial);
    private static readonly IReadOnlyList<Tile> Changsha258DrawTileTypes = BuildDrawTileTypes(MahjongRuleset.Changsha258);
    private static readonly IReadOnlyList<Tile> Changsha258SmallWinPairTiles = BuildChangshaSmallWinPairTiles();
    private static readonly IReadOnlyList<Tile> ChineseOfficialOrphanTileTypes = BuildChineseOfficialOrphanTileTypes();
    private const int LastSmallWinSingletonKeepBonus = 12;
    private const int OpponentKongFeedAvoidBonus = 12;
    private const int OpponentKongFeedOutsTolerance = 2;
    private const int DeadSmallWinPairOutsTolerance = 1;
    private const int ChangshaPureSuitSmallWinOutsTolerance = 1;
    private const int NearDeadSmallWinPairOutsTolerance = 1;
    private const int NearDeadOffRankPairOutsTolerance = 1;
    private const int NearDeadSingletonOutsTolerance = 1;
    private const int OffRankPairOutsTolerance = 1;
    private const int IsolatedOffRankPairOutsTolerance = 1;
    private const int DeadPairOutsTolerance = 1;
    private const int NearDeadPairOutsTolerance = 1;
    private const int DeadHonorSingletonOutsTolerance = 1;
    private const int NearDeadHonorSingletonOutsTolerance = 1;
    private const int DeadSuitedSingletonOutsTolerance = 1;
    private const int NearDeadSuitedSingletonOutsTolerance = 1;
    private const int OffRankConcealedKongOutsTolerance = 0;
    private const int ChangshaPureSuitBreakSmallWinPairThreshold = Changsha258Ruleset.BigWinPureSuitEyeFlexibleThreshold + 2;
    private const int ChangshaPureSuitBreakSmallWinPairHighPairRankThreshold = 5;
    private const int ChangshaPureSuitBreakSmallWinPairDistinctRankThreshold = 8;

    public static int SelectDiscardIndex(IReadOnlyList<Tile> handTiles, MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
        => SelectDiscardIndex(handTiles, Array.Empty<Tile>(), ruleset);

    public static int SelectDiscardIndex(IReadOnlyList<Tile> handTiles, IReadOnlyList<Tile> meldTiles, MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
        => SelectDiscardIndexInternal(handTiles, meldTiles, Array.Empty<Tile>(), Array.Empty<Tile>(), Array.Empty<Tile>(), ruleset);

    public static int SelectDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
        => SelectDiscardIndex(handTiles, meldTiles, visibleTiles, Array.Empty<Tile>(), ruleset);

    public static int SelectDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        IReadOnlyList<Tile> visibleMeldTiles,
        MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
    {
        ArgumentNullException.ThrowIfNull(visibleTiles);
        ArgumentNullException.ThrowIfNull(visibleMeldTiles);

        if (visibleTiles.Any(static tile => tile is null) || visibleMeldTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(visibleTiles));
        }

        return SelectDiscardIndexInternal(handTiles, meldTiles, visibleTiles, visibleMeldTiles, Array.Empty<Tile>(), ruleset);
    }

    public static int SelectDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        IReadOnlyList<Tile> visibleMeldTiles,
        IReadOnlyList<Tile> kongThreatTiles,
        MahjongRuleset ruleset = MahjongRuleset.ChineseOfficial)
    {
        ArgumentNullException.ThrowIfNull(visibleTiles);
        ArgumentNullException.ThrowIfNull(visibleMeldTiles);
        ArgumentNullException.ThrowIfNull(kongThreatTiles);

        if (visibleTiles.Any(static tile => tile is null)
            || visibleMeldTiles.Any(static tile => tile is null)
            || kongThreatTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(visibleTiles));
        }

        return SelectDiscardIndexInternal(handTiles, meldTiles, visibleTiles, visibleMeldTiles, kongThreatTiles, ruleset);
    }

    private static int SelectDiscardIndexInternal(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> visibleTiles,
        IReadOnlyList<Tile> visibleMeldTiles,
        IReadOnlyList<Tile> kongThreatTiles,
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

        if (ruleset == MahjongRuleset.Changsha258)
        {
            int? firstInvalidIndex = null;
            int? bonusInvalidIndex = null;

            for (var i = 0; i < handTiles.Count; i++)
            {
                if (!Changsha258Ruleset.IsAllowedTile(handTiles[i]))
                {
                    firstInvalidIndex ??= i;

                    if (handTiles[i].IsFlower)
                    {
                        bonusInvalidIndex = i;
                        break;
                    }
                }
            }

            if (bonusInvalidIndex.HasValue)
            {
                return bonusInvalidIndex.Value;
            }

            if (firstInvalidIndex.HasValue)
            {
                return firstInvalidIndex.Value;
            }
        }

        // visibleTiles should represent all visible tiles (discards + melds). visibleMeldTiles
        // is reserved for kong-threat weighting, so avoid double-counting meld visibility here.
        var visibleCounts = visibleTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var visibleMeldCounts = visibleMeldTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        if (visibleMeldCounts.Count > 0)
        {
            foreach (var entry in visibleMeldCounts)
            {
                visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                if (visibleCount < entry.Value)
                {
                    visibleCounts[entry.Key] = entry.Value;
                }
            }
        }

        var kongThreatCounts = kongThreatTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var handCounts = handTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedCounts = handTiles
            .Concat(meldTiles)
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownedTiles = handTiles.Concat(meldTiles).ToList();
        var discardOutSummaries = handTiles
            .Select((_, index) => CountWinningDrawOutsAfterDiscard(handTiles, meldTiles, visibleCounts, index, ruleset))
            .ToList();
        var maxWinningOuts = discardOutSummaries.Max(summary => summary.TileTypes);
        IReadOnlyList<WinningDrawOutSummary>? potentialOutSummaries = null;
        if (maxWinningOuts == 0 && visibleMeldCounts.Count > 0)
        {
            potentialOutSummaries = handTiles
                .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                    handTiles,
                    meldTiles,
                    visibleCounts,
                    index,
                    ruleset,
                    requireMinimumFan: false))
                .ToList();
        }

        var noPotentialOutsIgnoringMinimumFan = false;

        if (ruleset == MahjongRuleset.ChineseOfficial && maxWinningOuts == 0 && potentialOutSummaries is null)
        {
            potentialOutSummaries = handTiles
                .Select((_, index) => CountWinningDrawOutsAfterDiscard(
                    handTiles,
                    meldTiles,
                    visibleCounts,
                    index,
                    ruleset,
                    requireMinimumFan: false))
                .ToList();
        }

        if (potentialOutSummaries is not null)
        {
            noPotentialOutsIgnoringMinimumFan = potentialOutSummaries.Max(summary => summary.TileTypes) == 0;
        }

        var hasChangshaPureSuitLean = false;
        TileSuit? changshaDominantSuit = null;
        var changshaDominantSuitTileCount = 0;
        if (ruleset == MahjongRuleset.Changsha258)
        {
            var changshaSuitCounts = ownedTiles
                .Where(static tile => !tile.IsHonor)
                .GroupBy(static tile => tile.Suit)
                .ToDictionary(group => group.Key, group => group.Count());

            if (changshaSuitCounts.Count > 0)
            {
                var maxSuitCount = changshaSuitCounts.Values.Max();
                var dominantSuitCount = changshaSuitCounts.Count(entry => entry.Value == maxSuitCount);
                if (dominantSuitCount == 1 && maxSuitCount >= Changsha258Ruleset.BigWinPureSuitEyeFlexibleThreshold)
                {
                    hasChangshaPureSuitLean = true;
                    changshaDominantSuit = changshaSuitCounts.First(entry => entry.Value == maxSuitCount).Key;
                    changshaDominantSuitTileCount = maxSuitCount;
                }
            }
        }

        if (hasChangshaPureSuitLean && meldTiles.Count > 0 && changshaDominantSuit.HasValue)
        {
            if (meldTiles.Any(tile => tile.IsHonor || tile.Suit != changshaDominantSuit.Value))
            {
                hasChangshaPureSuitLean = false;
                changshaDominantSuit = null;
            }
        }

        var sevenPairsPairCount = meldTiles.Count == 0
            ? handCounts.Sum(static entry => entry.Value / 2)
            : 0;
        var hasChangshaSevenPairsLean = ruleset == MahjongRuleset.Changsha258
            && meldTiles.Count == 0
            && sevenPairsPairCount >= 6;

        Tile? soleChineseOfficialPairTile = null;
        var allowBreakingSolePairForTerminalHonorLean = false;
        if (ruleset == MahjongRuleset.ChineseOfficial)
        {
            var pairEntries = handCounts
                .Where(entry => entry.Value >= 2 && !entry.Key.IsFlower)
                .ToList();

            if (pairEntries.Count == 1)
            {
                soleChineseOfficialPairTile = pairEntries[0].Key;
            }
        }

        var bigWinCandidate = ruleset == MahjongRuleset.Changsha258
            && Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles);
        var enforceSmallWinPairBias = ruleset == MahjongRuleset.Changsha258 && !bigWinCandidate;
        var hasPotentialSmallWinPairCandidate = ruleset == MahjongRuleset.Changsha258
            && HasLiveSmallWinPairCandidate(handCounts, ownedCounts, visibleCounts, ruleset);

        if (ruleset == MahjongRuleset.Changsha258
            && !enforceSmallWinPairBias
            && hasPotentialSmallWinPairCandidate
            && discardOutSummaries.Max(summary => summary.BigWinTileTypes) == 0)
        {
            enforceSmallWinPairBias = true;
        }

        var hasLiveSmallWinPairCandidate = enforceSmallWinPairBias && hasPotentialSmallWinPairCandidate;

        if (enforceSmallWinPairBias && !hasLiveSmallWinPairCandidate)
        {
            enforceSmallWinPairBias = false;
        }

        Tile? soleChangshaSmallWinPairTile = null;
        if (enforceSmallWinPairBias)
        {
            var smallWinPairs = handCounts
                .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                .Select(entry => entry.Key)
                .ToList();

            if (smallWinPairs.Count == 1)
            {
                soleChangshaSmallWinPairTile = smallWinPairs[0];
            }
        }

        var hasChangshaDominantSuitTriplet = hasChangshaPureSuitLean
            && changshaDominantSuit.HasValue
            && ownedCounts.Any(entry => entry.Value >= 3 && entry.Key.Suit == changshaDominantSuit.Value);

        var hasChangshaDominantSuitHighPair = hasChangshaPureSuitLean
            && changshaDominantSuit.HasValue
            && handCounts.Any(entry => entry.Value >= 2
                && entry.Key.Suit == changshaDominantSuit.Value
                && entry.Key.Rank >= ChangshaPureSuitBreakSmallWinPairHighPairRankThreshold);

        var hasChangshaDominantSuitDistinctSpread = false;
        if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue)
        {
            var distinctRanks = ownedTiles
                .Where(tile => !tile.IsHonor && tile.Suit == changshaDominantSuit.Value)
                .Select(tile => tile.Rank)
                .Distinct()
                .Count();

            hasChangshaDominantSuitDistinctSpread = distinctRanks >= ChangshaPureSuitBreakSmallWinPairDistinctRankThreshold;
        }

        var allowBreakingSoleChangshaSmallWinPairForPureSuitLean = hasChangshaPureSuitLean
            && soleChangshaSmallWinPairTile is not null
            && changshaDominantSuit.HasValue
            && soleChangshaSmallWinPairTile.Suit != changshaDominantSuit.Value
            && (hasChangshaDominantSuitTriplet
                || (changshaDominantSuitTileCount >= ChangshaPureSuitBreakSmallWinPairThreshold
                    && hasChangshaDominantSuitHighPair));

        if (allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && enforceSmallWinPairBias
            && changshaDominantSuit.HasValue
            && visibleCounts.Count == 0
            && !handTiles.Any(tile => Changsha258Ruleset.IsSmallWinPair(tile)
                && tile.Suit == changshaDominantSuit.Value))
        {
            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
        }


        var hasSmallWinTileInHand = enforceSmallWinPairBias
            && handTiles.Any(static tile => Changsha258Ruleset.IsSmallWinPair(tile));

        var forcedDiscardIndex = TrySelectChangshaDeadSmallWinSingletonDiscardIndex(
            handTiles,
            handCounts,
            ownedCounts,
            visibleCounts,
            enforceSmallWinPairBias,
            hasChangshaPureSuitLean,
            changshaDominantSuit);

        if (forcedDiscardIndex.HasValue)
        {
            return forcedDiscardIndex.Value;
        }

        var offRankKongDiscardIndex = TrySelectChangshaIsolatedOffRankConcealedKongDiscardIndex(
            handTiles,
            handCounts,
            ownedCounts,
            visibleCounts,
            enforceSmallWinPairBias,
            hasChangshaPureSuitLean,
            changshaDominantSuit);

        if (offRankKongDiscardIndex.HasValue)
        {
            return offRankKongDiscardIndex.Value;
        }

        var hasStrongSmallWinPairCandidate = enforceSmallWinPairBias && visibleCounts.Count > 0
            ? HasStrongSmallWinPairCandidate(handCounts, ownedCounts, visibleCounts)
            : enforceSmallWinPairBias;

        var liveSmallWinPairCandidates = enforceSmallWinPairBias && visibleCounts.Count > 0
            ? BuildLiveSmallWinPairCandidates(handCounts, ownedCounts, visibleCounts)
            : null;

        var hasDominantSuitLiveSmallWinCandidate = hasChangshaPureSuitLean
            && changshaDominantSuit.HasValue
            && liveSmallWinPairCandidates is not null
            && liveSmallWinPairCandidates.Any(tile => tile.Suit == changshaDominantSuit.Value);

        Tile? lastSmallWinSingleton = null;
        var lastSmallWinSingletonRemainingCopies = 0;
        if (enforceSmallWinPairBias)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            if (smallWinPairCount == 0)
            {
                var smallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .Select(tile =>
                    {
                        ownedCounts.TryGetValue(tile, out var ownedCount);
                        visibleCounts.TryGetValue(tile, out var visibleCount);
                        return new
                        {
                            Tile = tile,
                            RemainingCopies = 4 - ownedCount - visibleCount
                        };
                    })
                    .Where(entry => entry.RemainingCopies > 0)
                    .ToList();

                if (smallWinSingletons.Count == 1)
                {
                    var pairCount = handCounts.Count(entry => entry.Value >= 2);
                    var candidate = smallWinSingletons[0];
                    var hasAlternateStrongSmallWinCandidate = HasAlternateStrongSmallWinPairCandidate(
                        candidate.Tile,
                        handCounts,
                        ownedCounts,
                        visibleCounts);

                    if (candidate.RemainingCopies >= 2 || pairCount == 0 || !hasAlternateStrongSmallWinCandidate)
                    {
                        if (!hasDominantSuitLiveSmallWinCandidate
                            || candidate.Tile.Suit == changshaDominantSuit.GetValueOrDefault())
                        {
                            lastSmallWinSingleton = candidate.Tile;
                            lastSmallWinSingletonRemainingCopies = candidate.RemainingCopies;
                        }
                    }
                }
            }
        }

        if (!allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && hasChangshaPureSuitLean
            && soleChangshaSmallWinPairTile is not null
            && changshaDominantSuit.HasValue
            && soleChangshaSmallWinPairTile.Suit != changshaDominantSuit.Value
            && liveSmallWinPairCandidates is not null
            && liveSmallWinPairCandidates.Any(tile => tile.Suit == changshaDominantSuit.Value))
        {
            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = true;
        }

        if (allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && !hasChangshaDominantSuitTriplet
            && liveSmallWinPairCandidates is not null
            && changshaDominantSuit.HasValue
            && !liveSmallWinPairCandidates.Any(tile => tile.Suit == changshaDominantSuit.Value))
        {
            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
        }

        if (!allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && hasChangshaPureSuitLean
            && soleChangshaSmallWinPairTile is not null
            && changshaDominantSuit.HasValue
            && soleChangshaSmallWinPairTile.Suit != changshaDominantSuit.Value
            && !hasDominantSuitLiveSmallWinCandidate
            && visibleCounts.Count > 0)
        {
            var dominantSuitSmallWinOutsDead = AreDominantSuitSmallWinOutsDead(
                changshaDominantSuit.Value,
                ownedCounts,
                visibleCounts);
            var hasDeadDominantSuitSmallWinSingleton = handCounts.Any(entry =>
            {
                if (entry.Value != 1
                    || !Changsha258Ruleset.IsSmallWinPair(entry.Key)
                    || entry.Key.Suit != changshaDominantSuit.Value)
                {
                    return false;
                }

                ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                visibleCounts.TryGetValue(entry.Key, out var visibleCount);

                return 4 - ownedCount - visibleCount == 0;
            });

            if (dominantSuitSmallWinOutsDead && !hasDeadDominantSuitSmallWinSingleton)
            {
                allowBreakingSoleChangshaSmallWinPairForPureSuitLean = true;
            }
        }

        if (allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && !hasDominantSuitLiveSmallWinCandidate
            && changshaDominantSuit.HasValue)
        {
            if (visibleCounts.Count > 0)
            {
                var dominantSuitSmallWinOutsDead = AreDominantSuitSmallWinOutsDead(
                    changshaDominantSuit.Value,
                    ownedCounts,
                    visibleCounts);
                var hasDeadDominantSuitSmallWinSingleton = handCounts.Any(entry =>
                {
                    if (entry.Value != 1
                        || !Changsha258Ruleset.IsSmallWinPair(entry.Key)
                        || entry.Key.Suit != changshaDominantSuit.Value)
                    {
                        return false;
                    }

                    ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                    visibleCounts.TryGetValue(entry.Key, out var visibleCount);

                    return 4 - ownedCount - visibleCount == 0;
                });

                if (!dominantSuitSmallWinOutsDead
                    || hasDeadDominantSuitSmallWinSingleton)
                {
                    allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
                }
            }
            else
            {
                var hasDeadDominantSuitSmallWinSingleton = handCounts.Any(entry =>
                {
                    if (entry.Value != 1
                        || !Changsha258Ruleset.IsSmallWinPair(entry.Key)
                        || entry.Key.Suit != changshaDominantSuit.Value)
                    {
                        return false;
                    }

                    ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                    visibleCounts.TryGetValue(entry.Key, out var visibleCount);

                    return 4 - ownedCount - visibleCount == 0;
                });

                if (hasDeadDominantSuitSmallWinSingleton)
                {
                    allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
                }
            }
        }

        if (allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && changshaDominantSuit.HasValue
            && visibleCounts.Count > 0
            && !HasDominantSuitStrongSmallWinCandidate(
                changshaDominantSuit.Value,
                handCounts,
                ownedCounts,
                visibleCounts)
            && (HasNearDeadDominantSuitSmallWinSingleton(
                    changshaDominantSuit.Value,
                    handCounts,
                    ownedCounts,
                    visibleCounts)
                || HasNearDeadDominantSuitSmallWinOuts(
                    changshaDominantSuit.Value,
                    ownedCounts,
                    visibleCounts)))
        {
            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
        }

        if (allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && enforceSmallWinPairBias
            && soleChangshaSmallWinPairTile is not null
            && liveSmallWinPairCandidates is not null
            && liveSmallWinPairCandidates.Count == 1
            && liveSmallWinPairCandidates.Contains(soleChangshaSmallWinPairTile))
        {
            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
        }

        var candidates = handTiles
            .Select((tile, index) =>
            {
                var winningOutSummary = discardOutSummaries[index];
                var potentialOutSummary = potentialOutSummaries is not null
                    ? potentialOutSummaries[index]
                    : default;
                ownedCounts.TryGetValue(tile, out var ownedCount);
                visibleCounts.TryGetValue(tile, out var visibleCount);
                var remainingCopies = Math.Max(0, 4 - ownedCount - visibleCount);
                var kongThreatIsDead = !IsKongThreatLive(tile, handCounts, ownedCounts, visibleCounts);
                var opponentKongFeedBonus = kongThreatIsDead
                    ? 0
                    : GetOpponentMeldKongAvoidBonus(tile, kongThreatCounts);
                var safeMeldExposure = 0;
                if (visibleMeldCounts.TryGetValue(tile, out var meldExposure) && meldExposure > 1)
                {
                    kongThreatCounts.TryGetValue(tile, out var threatExposure);
                    if (threatExposure == 0)
                    {
                        safeMeldExposure = meldExposure;
                    }
                }

                return new
                {
                    Tile = tile,
                    Index = index,
                    Score = GetBotTileKeepScore(
                            tile,
                            handTiles,
                            meldTiles,
                            ruleset,
                            enforceSmallWinPairBias,
                            hasLiveSmallWinPairCandidate,
                            hasStrongSmallWinPairCandidate,
                            liveSmallWinPairCandidates,
                            ownedCounts,
                            visibleCounts)
                        + GetTileAvailabilityPenalty(tile, handCounts, ownedCounts, visibleCounts)
                        + GetStrictDeadTilePenalty(tile, handCounts, ownedCounts, visibleCounts)
                        + GetDeadAdjacencyPenalty(tile, handCounts, ownedCounts, visibleCounts)
                        + GetChangshaSmallWinPairVisibilityAdjustment(
                            tile,
                            handCounts,
                            ownedCounts,
                            visibleCounts,
                            ruleset,
                            enforceSmallWinPairBias)
                        + opponentKongFeedBonus
                        + (lastSmallWinSingleton is not null && tile.Equals(lastSmallWinSingleton)
                            ? LastSmallWinSingletonKeepBonus
                            : 0),
                    RemainingCopies = remainingCopies,
                    SafeMeldExposure = safeMeldExposure,
                    WinningDrawOuts = winningOutSummary.TileTypes,
                    WinningDrawOutCopies = winningOutSummary.RemainingCopies,
                    PotentialWinningDrawOuts = potentialOutSummary.TileTypes,
                    PotentialWinningDrawOutCopies = potentialOutSummary.RemainingCopies,
                    SmallWinningDrawOuts = winningOutSummary.SmallWinTileTypes,
                    SmallWinningDrawOutCopies = winningOutSummary.SmallWinRemainingCopies,
                    BigWinningDrawOuts = winningOutSummary.BigWinTileTypes,
                    BigWinningDrawOutCopies = winningOutSummary.BigWinRemainingCopies
                };
            })
            .ToList();

        if (ruleset == MahjongRuleset.Changsha258
            && allowBreakingSoleChangshaSmallWinPairForPureSuitLean
            && soleChangshaSmallWinPairTile is not null)
        {
            var pairCandidates = candidates
                .Where(candidate => candidate.Tile.Equals(soleChangshaSmallWinPairTile))
                .ToList();

            if (pairCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestPairOuts = pairCandidates.Max(candidate => candidate.WinningDrawOuts);
                var losesAllWinningOuts = bestOverallOuts > 0 && bestPairOuts == 0;

                var nonPairCandidates = candidates
                    .Where(candidate => !candidate.Tile.Equals(soleChangshaSmallWinPairTile))
                    .ToList();

                var dominantSuitSmallWinOutsDead = changshaDominantSuit.HasValue
                    && visibleCounts.Count > 0
                    && AreDominantSuitSmallWinOutsDead(
                        changshaDominantSuit.Value,
                        ownedCounts,
                        visibleCounts);
                var allowZeroOutsBreak = hasChangshaDominantSuitDistinctSpread
                    || dominantSuitSmallWinOutsDead;

                if (nonPairCandidates.Count > 0)
                {
                    var bestNonPairSmallWinOuts = nonPairCandidates.Max(candidate => candidate.SmallWinningDrawOuts);
                    if (bestNonPairSmallWinOuts > 0)
                    {
                        var bestNonPairSmallWinCopies = nonPairCandidates
                            .Where(candidate => candidate.SmallWinningDrawOuts == bestNonPairSmallWinOuts)
                            .Max(candidate => candidate.SmallWinningDrawOutCopies);

                        var bestPairBigWinOuts = pairCandidates.Max(candidate => candidate.BigWinningDrawOuts);
                        var bestPairBigWinCopies = pairCandidates
                            .Where(candidate => candidate.BigWinningDrawOuts == bestPairBigWinOuts)
                            .Max(candidate => candidate.BigWinningDrawOutCopies);

                        var bigWinOutsMissing = bestPairBigWinOuts == 0 && bestNonPairSmallWinOuts > 0;
                        var bigWinLagsSmallWin = bestPairBigWinOuts + ChangshaPureSuitSmallWinOutsTolerance < bestNonPairSmallWinOuts
                            || (bestPairBigWinOuts + ChangshaPureSuitSmallWinOutsTolerance == bestNonPairSmallWinOuts
                                && bestPairBigWinCopies + ChangshaPureSuitSmallWinOutsTolerance < bestNonPairSmallWinCopies);

                        if ((bigWinOutsMissing && !allowZeroOutsBreak) || (bigWinLagsSmallWin && !allowZeroOutsBreak))
                        {
                            allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
                        }
                    }
                }

                if (losesAllWinningOuts && !allowZeroOutsBreak)
                {
                    allowBreakingSoleChangshaSmallWinPairForPureSuitLean = false;
                }
            }
        }

        var allCandidates = candidates;
        var filteredByPureSuit = false;
        var filteredByNearDeadOffRankPair = false;
        var filteredByNearDeadSmallWinSingleton = false;
        var filteredByDeadPair = false;
        var filteredByDeadSuitedSingleton = false;
        var filteredByKongThreatAvoidance = false;

        if (ruleset == MahjongRuleset.Changsha258 && enforceSmallWinPairBias && visibleCounts.Count > 0)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            if (smallWinPairCount == 0)
            {
                var smallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .ToList();

                if (smallWinSingletons.Count == 1)
                {
                    var singletonTile = smallWinSingletons[0];
                    ownedCounts.TryGetValue(singletonTile, out var ownedCount);
                    visibleCounts.TryGetValue(singletonTile, out var visibleCount);

                    var remainingCopies = 4 - ownedCount - visibleCount;
                    var hasAlternateStrongSmallWinCandidate = HasAlternateStrongSmallWinPairCandidate(
                        singletonTile,
                        handCounts,
                        ownedCounts,
                        visibleCounts);

                    var shouldProtectSingleton = (!hasDominantSuitLiveSmallWinCandidate
                        || singletonTile.Suit == changshaDominantSuit.GetValueOrDefault())
                        && !(remainingCopies == 1
                            && handTiles.Count >= 10
                            && hasAlternateStrongSmallWinCandidate
                            && !HasInHandSequenceAnchor(singletonTile, handCounts));

                    if (shouldProtectSingleton && remainingCopies > 0)
                    {
                        var nonProtectedCandidates = candidates
                            .Where(candidate => !candidate.Tile.Equals(singletonTile))
                            .ToList();

                        if (nonProtectedCandidates.Count > 0)
                        {
                            candidates = nonProtectedCandidates;
                        }
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258 && enforceSmallWinPairBias && visibleCounts.Count > 0)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            if (smallWinPairCount == 0)
            {
                var smallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry =>
                    {
                        ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                        visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                        return new
                        {
                            Tile = entry.Key,
                            RemainingCopies = 4 - ownedCount - visibleCount
                        };
                    })
                    .ToList();

                var liveSmallWinSingletons = smallWinSingletons
                    .Where(entry => entry.RemainingCopies > 0)
                    .ToList();
                var deadSmallWinSingletons = smallWinSingletons
                    .Where(entry => entry.RemainingCopies <= 0)
                    .ToList();

                if (liveSmallWinSingletons.Count == 0 && deadSmallWinSingletons.Count > 0)
                {
                    var deadTiles = deadSmallWinSingletons.Select(entry => entry.Tile).ToHashSet();
                    var deadSingletonCandidates = candidates
                        .Where(candidate => deadTiles.Contains(candidate.Tile)
                            && !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts))
                        .ToList();

                    if (deadSingletonCandidates.Count > 0)
                    {
                        candidates = deadSingletonCandidates;
                    }
                }

                if (liveSmallWinSingletons.Count == 1 && deadSmallWinSingletons.Count > 0)
                {
                    var pairCount = handCounts.Count(entry => entry.Value >= 2);
                    var liveSingleton = liveSmallWinSingletons[0];
                    if (liveSingleton.RemainingCopies >= 2 || pairCount == 0)
                    {
                        var deadTiles = deadSmallWinSingletons.Select(entry => entry.Tile).ToHashSet();
                        var deadCandidates = candidates
                            .Where(candidate => deadTiles.Contains(candidate.Tile)
                                && !HasCompleteChowInHand(handCounts, candidate.Tile))
                            .ToList();

                        if (deadCandidates.Count > 0)
                        {
                            candidates = deadCandidates;
                        }
                    }
                }

                if (liveSmallWinSingletons.Count > 0 && deadSmallWinSingletons.Count > 0)
                {
                    var deadTiles = deadSmallWinSingletons.Select(entry => entry.Tile).ToHashSet();
                    var deadSingletonCandidates = candidates
                        .Where(candidate => deadTiles.Contains(candidate.Tile)
                            && !HasCompleteChowInHand(handCounts, candidate.Tile))
                        .ToList();

                    if (deadSingletonCandidates.Count > 0)
                    {
                        candidates = deadSingletonCandidates;
                    }
                }

                if (liveSmallWinSingletons.Count > 1 && deadSmallWinSingletons.Count == 0)
                {
                    var bestSingletonCandidates = liveSmallWinSingletons;

                    if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue)
                    {
                        var dominantSuitSingletons = liveSmallWinSingletons
                            .Where(entry => entry.Tile.Suit == changshaDominantSuit.Value)
                            .ToList();

                        if (dominantSuitSingletons.Count > 0)
                        {
                            bestSingletonCandidates = dominantSuitSingletons;
                        }
                    }

                    var bestSingleton = bestSingletonCandidates
                        .OrderByDescending(entry => entry.RemainingCopies)
                        .ThenByDescending(entry => HasAdjacentSuitedTile(handCounts, entry.Tile) ? 1 : 0)
                        .First();

                    var nonProtectedCandidates = candidates
                        .Where(candidate => !candidate.Tile.Equals(bestSingleton.Tile))
                        .ToList();

                    if (nonProtectedCandidates.Count > 0)
                    {
                        candidates = nonProtectedCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && hasStrongSmallWinPairCandidate
            && visibleCounts.Count > 0)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            var pairCount = handCounts.Count(entry => entry.Value >= 2);
            if (smallWinPairCount == 0 && pairCount == 1)
            {
                var anchoredSmallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry => entry.Key)
                    .Where(tile =>
                    {
                        ownedCounts.TryGetValue(tile, out var ownedCount);
                        visibleCounts.TryGetValue(tile, out var visibleCount);
                        return 4 - ownedCount - visibleCount > 0
                            && HasCompleteChowInHand(handCounts, tile);
                    })
                    .ToList();

                if (anchoredSmallWinSingletons.Count > 0)
                {
                    var nonSmallWinPairCandidates = candidates
                        .Where(candidate => !Changsha258Ruleset.IsSmallWinPair(candidate.Tile)
                            && handCounts.TryGetValue(candidate.Tile, out var count)
                            && count >= 2)
                        .ToList();

                    if (nonSmallWinPairCandidates.Count > 0)
                    {
                        candidates = nonSmallWinPairCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && !hasStrongSmallWinPairCandidate
            && visibleCounts.Count > 0)
        {
            var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
            if (smallWinPairCount == 0)
            {
                var liveSmallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry =>
                    {
                        ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                        visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                        return new
                        {
                            Tile = entry.Key,
                            RemainingCopies = 4 - ownedCount - visibleCount
                        };
                    })
                    .Where(entry => entry.RemainingCopies > 0)
                    .ToList();

                if (liveSmallWinSingletons.Count == 1)
                {
                    var liveSingleton = liveSmallWinSingletons[0];
                    var pairCount = handCounts.Count(entry => entry.Value >= 2);
                    var hasAlternateLiveSmallWinCandidates = liveSmallWinPairCandidates is not null
                        && liveSmallWinPairCandidates.Count > 1;
                    var hasLiveNonSmallWinPair = handCounts
                        .Where(entry => entry.Value >= 2 && !Changsha258Ruleset.IsSmallWinPair(entry.Key))
                        .Any(entry =>
                        {
                            ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                            visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                            if (4 - ownedCount - visibleCount > 0)
                            {
                                return true;
                            }

                            return HasSequencePotential(entry.Key, handCounts, ownedCounts, visibleCounts);
                        });
                    var isOnlyLiveSmallWinCandidate = liveSmallWinPairCandidates is not null
                        && liveSmallWinPairCandidates.Count == 1
                        && liveSmallWinPairCandidates.Contains(liveSingleton.Tile);

                    var shouldProtectSingleton = !hasAlternateLiveSmallWinCandidates
                        && (liveSingleton.RemainingCopies >= 2
                            || pairCount == 0
                            || !hasLiveNonSmallWinPair
                            || (liveSingleton.RemainingCopies == 1 && isOnlyLiveSmallWinCandidate));

                    if (shouldProtectSingleton)
                    {
                        var protectedTile = liveSingleton.Tile;
                        var nonProtectedCandidates = candidates
                            .Where(candidate => !candidate.Tile.Equals(protectedTile))
                            .ToList();

                        if (nonProtectedCandidates.Count > 0)
                        {
                            candidates = nonProtectedCandidates;
                        }
                    }
                }
            }
        }

        var changshaSmallWinPairCount = ruleset == MahjongRuleset.Changsha258 && enforceSmallWinPairBias
            ? handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
            : 0;

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && changshaSmallWinPairCount == 0
            && visibleCounts.Count > 0)
        {
            var deadOffRankSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    if (candidate.RemainingCopies > 0)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (deadOffRankSingletonCandidates.Count > 0)
            {
                candidates = deadOffRankSingletonCandidates;
            }
            else
            {
                var liveSmallWinSingletons = handCounts
                    .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                    .Select(entry =>
                    {
                        ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                        visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                        return new
                        {
                            Tile = entry.Key,
                            RemainingCopies = 4 - ownedCount - visibleCount
                        };
                    })
                    .Where(entry => entry.RemainingCopies > 0)
                    .ToList();

                if (liveSmallWinSingletons.Count == 1 && liveSmallWinSingletons[0].RemainingCopies == 1)
                {
                    var liveSingleton = liveSmallWinSingletons[0];
                    var preserveDominantSuitSingleton = hasChangshaPureSuitLean
                        && changshaDominantSuit.HasValue
                        && liveSingleton.Tile.Suit == changshaDominantSuit.Value;
                    var hasAlternateStrongSmallWinCandidate = HasAlternateStrongSmallWinPairCandidate(
                        liveSingleton.Tile,
                        handCounts,
                        ownedCounts,
                        visibleCounts);
                    var shouldDiscardSingleton = !preserveDominantSuitSingleton
                        && handTiles.Count >= 10
                        && hasAlternateStrongSmallWinCandidate
                        && !HasInHandSequenceAnchor(liveSingleton.Tile, handCounts)
                        && !HasAdjacentSuitedTile(handCounts, liveSingleton.Tile);

                    if (shouldDiscardSingleton)
                    {
                        var deadOffRankPairCandidates = candidates
                            .Where(candidate =>
                            {
                                handCounts.TryGetValue(candidate.Tile, out var handCount);
                                return handCount == 2
                                    && !Changsha258Ruleset.IsSmallWinPair(candidate.Tile)
                                    && candidate.RemainingCopies <= 0
                                    && !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                            })
                            .ToList();

                        if (deadOffRankPairCandidates.Count > 0)
                        {
                            candidates = deadOffRankPairCandidates;
                        }
                        else
                        {
                            var singletonCandidates = candidates
                                .Where(candidate => candidate.Tile.Equals(liveSingleton.Tile))
                                .ToList();

                            if (singletonCandidates.Count > 0)
                            {
                                candidates = singletonCandidates;
                                filteredByNearDeadSmallWinSingleton = true;
                            }
                        }
                    }
                    else
                    {
                        var offRankPairCandidates = candidates
                            .Where(candidate =>
                            {
                                handCounts.TryGetValue(candidate.Tile, out var handCount);
                                return handCount == 2 && !Changsha258Ruleset.IsSmallWinPair(candidate.Tile);
                            })
                            .ToList();

                        if (offRankPairCandidates.Count > 0)
                        {
                            candidates = offRankPairCandidates;
                        }
                    }
                }
            }

            var nearDeadOffRankSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    if (candidate.RemainingCopies != 1)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (nearDeadOffRankSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestNearDeadOuts = nearDeadOffRankSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestNearDeadOutCopies = nearDeadOffRankSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadSingletonOutsTolerance;
                var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                    || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadSingletonOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = nearDeadOffRankSingletonCandidates;
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && changshaSmallWinPairCount == 0
            && !hasChangshaPureSuitLean
            && !filteredByNearDeadSmallWinSingleton)
        {
            var pairCount = handCounts.Count(entry => entry.Value >= 2);
            if (pairCount >= 2)
            {
                var offRankPairCandidates = candidates
                    .Where(candidate =>
                    {
                        handCounts.TryGetValue(candidate.Tile, out var handCount);
                        return handCount == 2 && !Changsha258Ruleset.IsSmallWinPair(candidate.Tile);
                    })
                    .ToList();

                if (offRankPairCandidates.Count > 0)
                {
                    if (offRankPairCandidates.Count > 1)
                    {
                        var protectedTiles = offRankPairCandidates
                            .Where(candidate => HasGapAdjacentSmallWinSingletonWithBridge(candidate.Tile, handCounts))
                            .Select(candidate => candidate.Tile)
                            .ToHashSet();

                        if (protectedTiles.Count > 0)
                        {
                            var unprotectedCandidates = offRankPairCandidates
                                .Where(candidate => !protectedTiles.Contains(candidate.Tile))
                                .ToList();

                            if (unprotectedCandidates.Count > 0)
                            {
                                offRankPairCandidates = unprotectedCandidates;
                            }
                        }
                    }

                    var offRankPairsWithoutSequence = offRankPairCandidates
                        .Where(candidate => !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts))
                        .ToList();

                    if (offRankPairsWithoutSequence.Count > 0)
                    {
                        offRankPairCandidates = offRankPairsWithoutSequence;
                    }

                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestOffRankOuts = offRankPairCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOffRankOutCopies = offRankPairCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOffRankOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestOffRankOuts >= bestOverallOuts - OffRankPairOutsTolerance;
                    var withinCopyTolerance = bestOffRankOuts < bestOverallOuts
                        || bestOffRankOutCopies >= bestOverallOutCopies - OffRankPairOutsTolerance;

                    if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                    {
                        candidates = offRankPairCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && changshaSmallWinPairCount == 0
            && visibleCounts.Count > 0)
        {
            var deadOffRankPairCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 2 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    if (candidate.RemainingCopies > 0)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (deadOffRankPairCandidates.Count > 0)
            {
                candidates = deadOffRankPairCandidates;
            }
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var offRankConcealedKongCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    return handCount >= 4 && !Changsha258Ruleset.IsSmallWinPair(candidate.Tile);
                })
                .ToList();

            if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue)
            {
                offRankConcealedKongCandidates = offRankConcealedKongCandidates
                    .Where(candidate => candidate.Tile.Suit != changshaDominantSuit.Value)
                    .ToList();
            }

            if (offRankConcealedKongCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestOffRankKongOuts = offRankConcealedKongCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOffRankKongOutCopies = offRankConcealedKongCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOffRankKongOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestOffRankKongOuts >= bestOverallOuts - OffRankConcealedKongOutsTolerance;
                var withinCopyTolerance = bestOffRankKongOuts < bestOverallOuts
                    || bestOffRankKongOutCopies >= bestOverallOutCopies - OffRankConcealedKongOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = offRankConcealedKongCandidates;
                }
            }
        }

        var filteredByDeadOffRankPair = false;
        var filteredByDeadSingleton = false;
        var filteredByIsolatedOffRankPair = false;

        if (changshaSmallWinPairCount >= 1)
        {
            var deadSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1)
                    {
                        return false;
                    }

                    ownedCounts.TryGetValue(candidate.Tile, out var ownedCount);
                    visibleCounts.TryGetValue(candidate.Tile, out var visibleCount);
                    if (4 - ownedCount - visibleCount > 0)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (deadSingletonCandidates.Count > 0)
            {
                candidates = deadSingletonCandidates;
                filteredByDeadSingleton = true;
            }
        }

        if (changshaSmallWinPairCount >= 1 && !filteredByDeadSingleton && !hasChangshaSevenPairsLean)
        {
            var deadOffRankPairCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 2 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    ownedCounts.TryGetValue(candidate.Tile, out var ownedCount);
                    visibleCounts.TryGetValue(candidate.Tile, out var visibleCount);

                    if (4 - ownedCount - visibleCount > 0)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (deadOffRankPairCandidates.Count > 0)
            {
                candidates = deadOffRankPairCandidates;
                filteredByDeadOffRankPair = true;
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount >= 1
            && !filteredByDeadOffRankPair
            && !filteredByDeadSingleton
            && !hasChangshaSevenPairsLean)
        {
            var nearDeadOffRankPairCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 2 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    if (candidate.RemainingCopies != 1)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            var skipNearDeadOffRankPairForPureSuit = hasChangshaPureSuitLean
                && changshaDominantSuit.HasValue
                && nearDeadOffRankPairCandidates.Count > 0
                && candidates.Any(candidate => candidate.Tile.Suit != changshaDominantSuit.Value)
                && nearDeadOffRankPairCandidates.All(candidate => candidate.Tile.Suit == changshaDominantSuit.Value);

            if (nearDeadOffRankPairCandidates.Count > 0 && !skipNearDeadOffRankPairForPureSuit)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.SmallWinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.SmallWinningDrawOutCopies);

                var bestNearDeadOuts = nearDeadOffRankPairCandidates.Max(candidate => candidate.SmallWinningDrawOuts);
                var bestNearDeadOutCopies = nearDeadOffRankPairCandidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == bestNearDeadOuts)
                    .Max(candidate => candidate.SmallWinningDrawOutCopies);

                var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadOffRankPairOutsTolerance;
                var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                    || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadOffRankPairOutsTolerance;
                var losesAllWinningOuts = bestOverallOuts > 0 && bestNearDeadOuts == 0;
                var hasNonTerminalNearDeadPair = nearDeadOffRankPairCandidates.Any(candidate =>
                    !candidate.Tile.IsHonor && !IsTerminalSuitedTile(candidate.Tile));

                if ((bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                    && (!losesAllWinningOuts || hasNonTerminalNearDeadPair))
                {
                    candidates = nearDeadOffRankPairCandidates;
                    filteredByNearDeadOffRankPair = true;
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount >= 1
            && !filteredByDeadOffRankPair
            && !filteredByNearDeadOffRankPair
            && !filteredByDeadSingleton
            && !hasChangshaSevenPairsLean)
        {
            var isolatedOffRankPairCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 2 || Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue && isolatedOffRankPairCandidates.Count > 0)
            {
                isolatedOffRankPairCandidates = isolatedOffRankPairCandidates
                    .Where(candidate => candidate.Tile.Suit != changshaDominantSuit.Value)
                    .ToList();
            }

            if (isolatedOffRankPairCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.SmallWinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.SmallWinningDrawOutCopies);

                var bestIsolatedOuts = isolatedOffRankPairCandidates.Max(candidate => candidate.SmallWinningDrawOuts);
                var bestIsolatedOutCopies = isolatedOffRankPairCandidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == bestIsolatedOuts)
                    .Max(candidate => candidate.SmallWinningDrawOutCopies);

                var withinOutsTolerance = bestIsolatedOuts >= bestOverallOuts - IsolatedOffRankPairOutsTolerance;
                var withinCopyTolerance = bestIsolatedOuts < bestOverallOuts
                    || bestIsolatedOutCopies >= bestOverallOutCopies - IsolatedOffRankPairOutsTolerance;
                var losesAllWinningOuts = bestOverallOuts > 0 && bestIsolatedOuts == 0;
                var hasOnlyNearDeadTerminalPairs = isolatedOffRankPairCandidates.All(candidate =>
                    candidate.RemainingCopies == 1
                    && (candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile)));

                if ((bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                    && (!losesAllWinningOuts || !hasOnlyNearDeadTerminalPairs))
                {
                    candidates = isolatedOffRankPairCandidates;
                    filteredByIsolatedOffRankPair = true;
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount > 1
            && !filteredByDeadOffRankPair)
        {
            var smallWinPairEntries = handCounts
                .Where(entry => entry.Value == 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                .Select(entry =>
                {
                    ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                    visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                    return new
                    {
                        Tile = entry.Key,
                        RemainingCopies = 4 - ownedCount - visibleCount
                    };
                })
                .ToList();

            var liveSmallWinPairs = smallWinPairEntries
                .Where(entry => entry.RemainingCopies > 0)
                .ToList();
            var deadSmallWinPairs = smallWinPairEntries
                .Where(entry => entry.RemainingCopies <= 0)
                .ToList();

            if (liveSmallWinPairs.Count > 0 && deadSmallWinPairs.Count > 0)
            {
                var deadTiles = deadSmallWinPairs.Select(entry => entry.Tile).ToHashSet();
                var deadPairCandidates = candidates
                    .Where(candidate => deadTiles.Contains(candidate.Tile))
                    .ToList();

                if (deadPairCandidates.Count > 0)
                {
                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestDeadOuts = deadPairCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestDeadOutCopies = deadPairCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestDeadOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestDeadOuts >= bestOverallOuts - DeadSmallWinPairOutsTolerance;
                    var withinCopyTolerance = bestDeadOuts < bestOverallOuts
                        || bestDeadOutCopies >= bestOverallOutCopies - DeadSmallWinPairOutsTolerance;

                    if (withinOutsTolerance && withinCopyTolerance)
                    {
                        candidates = deadPairCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount > 1
            && !filteredByDeadOffRankPair)
        {
            var nearDeadSmallWinPairs = handCounts
                .Where(entry => entry.Value == 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                .Select(entry =>
                {
                    ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                    visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                    return new
                    {
                        Tile = entry.Key,
                        RemainingCopies = 4 - ownedCount - visibleCount
                    };
                })
                .Where(entry => entry.RemainingCopies == 1)
                .ToList();

            if (nearDeadSmallWinPairs.Count > 0)
            {
                var nearDeadTiles = nearDeadSmallWinPairs.Select(entry => entry.Tile).ToHashSet();
                var nearDeadPairCandidates = candidates
                    .Where(candidate => nearDeadTiles.Contains(candidate.Tile))
                    .ToList();

                if (nearDeadPairCandidates.Count > 0)
                {
                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestNearDeadOuts = nearDeadPairCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestNearDeadOutCopies = nearDeadPairCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadSmallWinPairOutsTolerance;
                    var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                        || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadSmallWinPairOutsTolerance;

                    if (withinOutsTolerance && withinCopyTolerance)
                    {
                        candidates = nearDeadPairCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount >= 1
            && !filteredByDeadOffRankPair
            && !filteredByDeadSingleton)
        {
            var nearDeadSmallWinSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1 || candidate.RemainingCopies != 1)
                    {
                        return false;
                    }

                    if (!Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    return !HasInHandSequenceAnchor(candidate.Tile, handCounts);
                })
                .ToList();

            if (nearDeadSmallWinSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestNearDeadOuts = nearDeadSmallWinSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestNearDeadOutCopies = nearDeadSmallWinSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadSingletonOutsTolerance;
                var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                    || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadSingletonOutsTolerance;
                var losesAllWinningOuts = bestOverallOuts > 0 && bestNearDeadOuts == 0;

                if (bestOverallOuts == 0 || ((withinOutsTolerance && withinCopyTolerance) && !losesAllWinningOuts))
                {
                    candidates = nearDeadSmallWinSingletonCandidates;
                    filteredByNearDeadSmallWinSingleton = true;
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && visibleCounts.Count > 0
            && changshaSmallWinPairCount >= 1
            && !filteredByDeadOffRankPair
            && !filteredByDeadSingleton
            && !filteredByNearDeadSmallWinSingleton)
        {
            var nearDeadSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1 || candidate.RemainingCopies != 1)
                    {
                        return false;
                    }

                    if (Changsha258Ruleset.IsSmallWinPair(candidate.Tile))
                    {
                        return false;
                    }

                    return !HasInHandSequenceAnchor(candidate.Tile, handCounts);
                })
                .ToList();

            if (nearDeadSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestNearDeadOuts = nearDeadSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestNearDeadOutCopies = nearDeadSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadSingletonOutsTolerance;
                var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                    || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadSingletonOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = nearDeadSingletonCandidates;
                }
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial && visibleCounts.Count > 0 && sevenPairsPairCount < 6)
        {
            filteredByDeadSuitedSingleton = false;
            var deadSingletonCandidates = candidates
                .Where(candidate =>
                {
                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    if (handCount != 1 || candidate.Tile.IsHonor || candidate.RemainingCopies != 0)
                    {
                        return false;
                    }

                    return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                })
                .ToList();

            if (deadSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestDeadOuts = deadSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestDeadOutCopies = deadSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestDeadOuts >= bestOverallOuts - DeadSuitedSingletonOutsTolerance;
                var withinCopyTolerance = bestDeadOuts < bestOverallOuts
                    || bestDeadOutCopies >= bestOverallOutCopies - DeadSuitedSingletonOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = deadSingletonCandidates;
                    filteredByDeadSuitedSingleton = true;
                }
            }

            if (!filteredByDeadSuitedSingleton)
            {
                var nearDeadSingletonCandidates = candidates
                    .Where(candidate =>
                    {
                        handCounts.TryGetValue(candidate.Tile, out var handCount);
                        if (handCount != 1 || candidate.Tile.IsHonor || candidate.RemainingCopies != 1)
                        {
                            return false;
                        }

                        return !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts);
                    })
                    .ToList();

                if (nearDeadSingletonCandidates.Count > 0)
                {
                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestNearDeadOuts = nearDeadSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestNearDeadOutCopies = nearDeadSingletonCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadSuitedSingletonOutsTolerance;
                    var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                        || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadSuitedSingletonOutsTolerance;

                    if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                    {
                        candidates = nearDeadSingletonCandidates;
                        filteredByDeadSuitedSingleton = true;
                    }
                }
            }

            var pairCount = handCounts.Count(entry => entry.Value >= 2 && !entry.Key.IsFlower);
            filteredByDeadPair = false;
            if (!filteredByDeadSuitedSingleton && pairCount >= 2)
            {
                var deadPairCandidates = candidates
                    .Where(candidate =>
                    {
                        handCounts.TryGetValue(candidate.Tile, out var handCount);
                        if (handCount != 2 || candidate.RemainingCopies != 0)
                        {
                            return false;
                        }

                        return !HasInHandSequenceAnchor(candidate.Tile, handCounts)
                            && (IsTerminalSuitedTile(candidate.Tile)
                                || !HasSequencePotential(candidate.Tile, handCounts, ownedCounts, visibleCounts));
                    })
                    .ToList();

                if (deadPairCandidates.Count > 0)
                {
                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestDeadOuts = deadPairCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestDeadOutCopies = deadPairCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestDeadOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestDeadOuts >= bestOverallOuts - DeadPairOutsTolerance;
                    var withinCopyTolerance = bestDeadOuts < bestOverallOuts
                        || bestDeadOutCopies >= bestOverallOutCopies - DeadPairOutsTolerance;
                    var losesAllWinningOuts = bestOverallOuts > 0 && bestDeadOuts == 0;

                    var shouldPreferDeadPairs = bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance);
                    if (losesAllWinningOuts)
                    {
                        shouldPreferDeadPairs = false;
                    }

                    if (shouldPreferDeadPairs && bestOverallOuts == 0 && potentialOutSummaries is not null)
                    {
                        var bestPotentialOuts = candidates.Max(candidate => candidate.PotentialWinningDrawOuts);
                        if (bestPotentialOuts > 0)
                        {
                            var bestPotentialOutCopies = candidates
                                .Where(candidate => candidate.PotentialWinningDrawOuts == bestPotentialOuts)
                                .Max(candidate => candidate.PotentialWinningDrawOutCopies);

                            var bestDeadPotentialOuts = deadPairCandidates.Max(candidate => candidate.PotentialWinningDrawOuts);
                            var bestDeadPotentialOutCopies = deadPairCandidates
                                .Where(candidate => candidate.PotentialWinningDrawOuts == bestDeadPotentialOuts)
                                .Max(candidate => candidate.PotentialWinningDrawOutCopies);

                            var hasHonorDeadPairCandidate = deadPairCandidates.Any(candidate => candidate.Tile.IsHonor);
                            var withinPotentialTolerance = hasHonorDeadPairCandidate
                                ? bestDeadPotentialOuts >= bestPotentialOuts
                                : bestDeadPotentialOuts >= bestPotentialOuts - DeadPairOutsTolerance;
                            var withinPotentialCopyTolerance = bestDeadPotentialOuts < bestPotentialOuts
                                || bestDeadPotentialOutCopies >= bestPotentialOutCopies - DeadPairOutsTolerance;

                            shouldPreferDeadPairs = withinPotentialTolerance && withinPotentialCopyTolerance;
                        }
                    }

                    if (shouldPreferDeadPairs)
                    {
                        candidates = deadPairCandidates;
                        filteredByDeadPair = true;
                    }
                }

                if (!filteredByDeadPair)
                {
                    var nearDeadPairCandidates = candidates
                        .Where(candidate =>
                        {
                            handCounts.TryGetValue(candidate.Tile, out var handCount);
                            if (handCount != 2)
                            {
                                return false;
                            }

                            ownedCounts.TryGetValue(candidate.Tile, out var ownedCount);
                            visibleCounts.TryGetValue(candidate.Tile, out var visibleCount);
                            return 4 - ownedCount - visibleCount == 1;
                        })
                        .ToList();

                    if (nearDeadPairCandidates.Count > 0)
                    {
                        var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                        var bestOverallOutCopies = candidates
                            .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                            .Max(candidate => candidate.WinningDrawOutCopies);

                        var bestNearDeadOuts = nearDeadPairCandidates.Max(candidate => candidate.WinningDrawOuts);
                        var bestNearDeadOutCopies = nearDeadPairCandidates
                            .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                            .Max(candidate => candidate.WinningDrawOutCopies);

                        var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadPairOutsTolerance;
                        var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                            || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadPairOutsTolerance;

                        if (withinOutsTolerance && withinCopyTolerance)
                        {
                            candidates = nearDeadPairCandidates;
                        }
                    }
                }
            }
        }

        var concealedKongTiles = handCounts
            .Where(entry => entry.Value >= 4)
            .Select(entry => entry.Key)
            .ToHashSet();
        var promotableKongTiles = meldTiles
            .GroupBy(static tile => tile)
            .Where(group => group.Count() == 3)
            .Select(group => group.Key)
            .ToHashSet();

        if (visibleCounts.Count > 0)
        {
            var honorCandidatePool = candidates;
            var deadHonorSingletonCandidates = honorCandidatePool
                .Where(candidate =>
                {
                    if (!candidate.Tile.IsHonor || candidate.RemainingCopies > 0)
                    {
                        return false;
                    }

                    if (promotableKongTiles.Contains(candidate.Tile))
                    {
                        return false;
                    }

                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    return handCount == 1;
                })
                .ToList();

            if (deadHonorSingletonCandidates.Count == 0
                && (filteredByDeadSuitedSingleton
                    || filteredByDeadPair
                    || honorCandidatePool.All(candidate => !candidate.Tile.IsHonor)))
            {
                honorCandidatePool = allCandidates;
                deadHonorSingletonCandidates = honorCandidatePool
                    .Where(candidate =>
                    {
                        if (!candidate.Tile.IsHonor || candidate.RemainingCopies > 0)
                        {
                            return false;
                        }

                        if (promotableKongTiles.Contains(candidate.Tile))
                        {
                            return false;
                        }

                        handCounts.TryGetValue(candidate.Tile, out var handCount);
                        return handCount == 1;
                    })
                    .ToList();
            }

            if (deadHonorSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = honorCandidatePool.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = honorCandidatePool
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestDeadOuts = deadHonorSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestDeadOutCopies = deadHonorSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestDeadOuts >= bestOverallOuts - DeadHonorSingletonOutsTolerance;
                var withinCopyTolerance = bestDeadOuts < bestOverallOuts
                    || bestDeadOutCopies >= bestOverallOutCopies - DeadHonorSingletonOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = deadHonorSingletonCandidates;
                }
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial
            && visibleCounts.Count > 0
            && sevenPairsPairCount < 6)
        {
            var nearDeadHonorSingletonCandidates = candidates
                .Where(candidate =>
                {
                    if (!candidate.Tile.IsHonor || candidate.RemainingCopies != 1)
                    {
                        return false;
                    }

                    if (promotableKongTiles.Contains(candidate.Tile))
                    {
                        return false;
                    }

                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    return handCount == 1;
                })
                .ToList();

            if (nearDeadHonorSingletonCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestNearDeadOuts = nearDeadHonorSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestNearDeadOutCopies = nearDeadHonorSingletonCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadHonorSingletonOutsTolerance;
                var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                    || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadHonorSingletonOutsTolerance;

                if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                {
                    candidates = nearDeadHonorSingletonCandidates;
                }
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial
            && visibleCounts.Count > 0
            && sevenPairsPairCount < 6)
        {
            var honorSingletonCandidates = candidates
                .Where(candidate =>
                {
                    if (!candidate.Tile.IsHonor)
                    {
                        return false;
                    }

                    if (promotableKongTiles.Contains(candidate.Tile))
                    {
                        return false;
                    }

                    handCounts.TryGetValue(candidate.Tile, out var handCount);
                    return handCount == 1;
                })
                .ToList();

            if (honorSingletonCandidates.Count > 0)
            {
                var honorPairCount = ownedCounts.Count(entry => entry.Key.IsHonor && entry.Value >= 2);
                var honorCount = ownedTiles.Count(static tile => tile.IsHonor);
                var suitedCount = ownedTiles.Count(static tile => !tile.IsHonor);

                if (honorPairCount == 0 && honorCount <= 3 && suitedCount >= 9)
                {
                    var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestHonorOuts = honorSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestHonorOutCopies = honorSingletonCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestHonorOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var withinOutsTolerance = bestHonorOuts >= bestOverallOuts - NearDeadHonorSingletonOutsTolerance;
                    var withinCopyTolerance = bestHonorOuts < bestOverallOuts
                        || bestHonorOutCopies >= bestOverallOutCopies - NearDeadHonorSingletonOutsTolerance;

                    if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                    {
                        candidates = honorSingletonCandidates;
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258 && promotableKongTiles.Count > 0)
        {
            promotableKongTiles.RemoveWhere(tile =>
            {
                if (!Changsha258Ruleset.IsSmallWinPair(tile))
                {
                    return false;
                }

                ownedCounts.TryGetValue(tile, out var ownedCount);
                handCounts.TryGetValue(tile, out var handCount);
                var hasSmallWinPairInHand = handCounts.Any(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));

                if (ownedCount >= 4)
                {
                    return handCount == 1
                        && !hasSmallWinPairInHand
                        && enforceSmallWinPairBias;
                }

                visibleCounts.TryGetValue(tile, out var visibleCount);
                return 4 - ownedCount - visibleCount <= 0;
            });
        }

        if (promotableKongTiles.Count > 0)
        {
            var nonPromotableKongCandidates = candidates
                .Where(candidate => !promotableKongTiles.Contains(candidate.Tile))
                .ToList();

            if (nonPromotableKongCandidates.Count > 0)
            {
                candidates = nonPromotableKongCandidates;
            }
        }

        if (concealedKongTiles.Count > 0 || promotableKongTiles.Count > 0)
        {
            var protectedKongTiles = new HashSet<Tile>(concealedKongTiles);
            protectedKongTiles.UnionWith(promotableKongTiles);

            var nonKongCandidates = candidates
                .Where(candidate => !protectedKongTiles.Contains(candidate.Tile))
                .ToList();

            if (nonKongCandidates.Count > 0)
            {
                var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = candidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var bestNonKongOuts = nonKongCandidates.Max(candidate => candidate.WinningDrawOuts);
                var bestNonKongOutCopies = nonKongCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestNonKongOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var losesAllOutsByAvoidingKong = bestOverallOuts > 0 && bestNonKongOuts == 0;
                if (losesAllOutsByAvoidingKong)
                {
                    var kongCandidates = candidates
                        .Where(candidate => protectedKongTiles.Contains(candidate.Tile))
                        .ToList();

                    if (kongCandidates.Count > 0)
                    {
                        candidates = kongCandidates;
                    }
                }
                else if (bestNonKongOuts == bestOverallOuts
                    && bestNonKongOutCopies == bestOverallOutCopies)
                {
                    candidates = nonKongCandidates;
                }
            }
        }

        if (kongThreatCounts.Count > 0)
        {
            var opponentKongThreatTiles = kongThreatCounts
                .Where(entry => entry.Value == 3 || entry.Value >= 6)
                .Select(entry => entry.Key)
                .Where(tile => IsKongThreatLive(tile, handCounts, ownedCounts, visibleCounts))
                .ToHashSet();

            if (opponentKongThreatTiles.Count > 0)
            {
                var threatCandidatePool = allCandidates;
                if (concealedKongTiles.Count > 0 || promotableKongTiles.Count > 0)
                {
                    var protectedKongTiles = new HashSet<Tile>(concealedKongTiles);
                    protectedKongTiles.UnionWith(promotableKongTiles);
                    threatCandidatePool = threatCandidatePool
                        .Where(candidate => !protectedKongTiles.Contains(candidate.Tile))
                        .ToList();

                    if (threatCandidatePool.Count == 0)
                    {
                        threatCandidatePool = allCandidates;
                    }
                }

                var nonThreatCandidates = threatCandidatePool
                    .Where(candidate => !opponentKongThreatTiles.Contains(candidate.Tile))
                    .ToList();

                var bestOverallOuts = threatCandidatePool.Max(candidate => candidate.WinningDrawOuts);
                var bestOverallOutCopies = threatCandidatePool
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .Max(candidate => candidate.WinningDrawOutCopies);

                var multiCopyThreatCandidates = threatCandidatePool
                    .Where(candidate => opponentKongThreatTiles.Contains(candidate.Tile)
                        && handCounts.TryGetValue(candidate.Tile, out var threatCount)
                        && threatCount > 1)
                    .ToList();

                var allowThreatWhenNoWinningOuts = bestOverallOuts == 0
                    && multiCopyThreatCandidates.Count > 0
                    && !noPotentialOutsIgnoringMinimumFan;

                if (nonThreatCandidates.Count > 0)
                {
                    var bestNonThreatOuts = nonThreatCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestNonThreatOutCopies = nonThreatCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestNonThreatOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestThreatCandidate = threatCandidatePool
                        .Where(candidate => opponentKongThreatTiles.Contains(candidate.Tile))
                        .OrderByDescending(candidate => candidate.WinningDrawOuts)
                        .ThenByDescending(candidate => candidate.WinningDrawOutCopies)
                        .FirstOrDefault();

                    var singleCopyThreatCarriesOnlyWinningPath = false;
                    if (bestThreatCandidate is not null
                        && bestOverallOuts > 0
                        && bestNonThreatOuts == 0
                        && bestThreatCandidate.WinningDrawOuts == bestOverallOuts
                        && handCounts.TryGetValue(bestThreatCandidate.Tile, out var threatHandCount)
                        && threatHandCount == 1)
                    {
                        singleCopyThreatCarriesOnlyWinningPath = true;
                    }

                    var withinOutsTolerance = bestNonThreatOuts >= bestOverallOuts - OpponentKongFeedOutsTolerance;
                    var withinCopyTolerance = bestNonThreatOutCopies >= bestOverallOutCopies - OpponentKongFeedOutsTolerance;
                    var withinOutsAndCopies = withinOutsTolerance && withinCopyTolerance;
                    var matchesBestCopies = bestNonThreatOuts == bestOverallOuts
                        && bestNonThreatOutCopies == bestOverallOutCopies;
                    var losesAllWinningOuts = bestOverallOuts > 0 && bestNonThreatOuts == 0;

                    if (bestOverallOuts == 0)
                    {
                        var bestPotentialOuts = 0;
                        var bestPotentialOutCopies = 0;
                        var bestNonThreatPotentialOuts = 0;
                        var bestNonThreatPotentialOutCopies = 0;
                        var hasPotentialOuts = false;

                        if (ruleset == MahjongRuleset.ChineseOfficial)
                        {
                            bestPotentialOuts = threatCandidatePool.Max(candidate => candidate.PotentialWinningDrawOuts);
                            if (bestPotentialOuts > 0)
                            {
                                hasPotentialOuts = true;
                                bestPotentialOutCopies = threatCandidatePool
                                    .Where(candidate => candidate.PotentialWinningDrawOuts == bestPotentialOuts)
                                    .Max(candidate => candidate.PotentialWinningDrawOutCopies);
                                bestNonThreatPotentialOuts = nonThreatCandidates
                                    .Max(candidate => candidate.PotentialWinningDrawOuts);
                                bestNonThreatPotentialOutCopies = nonThreatCandidates
                                    .Where(candidate => candidate.PotentialWinningDrawOuts == bestNonThreatPotentialOuts)
                                    .Max(candidate => candidate.PotentialWinningDrawOutCopies);
                            }
                        }

                        if (hasPotentialOuts)
                        {
                            var losesAllPotentialOuts = bestPotentialOuts > 0 && bestNonThreatPotentialOuts == 0;
                            var withinPotentialTolerance = bestNonThreatPotentialOuts >= bestPotentialOuts - OpponentKongFeedOutsTolerance;
                            var matchesPotentialCopies = bestNonThreatPotentialOuts == bestPotentialOuts
                                && bestNonThreatPotentialOutCopies == bestPotentialOutCopies;

                            if (losesAllPotentialOuts)
                            {
                                var threatCandidates = threatCandidatePool
                                    .Where(candidate => opponentKongThreatTiles.Contains(candidate.Tile))
                                    .ToList();

                                if (threatCandidates.Count > 0)
                                {
                                    var hasSingleCopyThreat = threatCandidates.Any(candidate =>
                                        handCounts.TryGetValue(candidate.Tile, out var threatHandCount)
                                        && threatHandCount == 1);

                                    if (!hasSingleCopyThreat)
                                    {
                                        candidates = nonThreatCandidates;
                                        filteredByKongThreatAvoidance = true;
                                    }
                                    else
                                    {
                                        candidates = threatCandidates;
                                    }
                                }
                            }
                            else if (withinPotentialTolerance || matchesPotentialCopies)
                            {
                                candidates = nonThreatCandidates;
                                filteredByKongThreatAvoidance = true;
                            }
                        }
                        else
                        {
                            candidates = nonThreatCandidates;
                            filteredByKongThreatAvoidance = true;
                        }
                    }
                    else if (!losesAllWinningOuts
                        && ((withinOutsAndCopies && !singleCopyThreatCarriesOnlyWinningPath) || matchesBestCopies))
                    {
                        candidates = nonThreatCandidates;
                        filteredByKongThreatAvoidance = true;
                    }
                    else if (losesAllWinningOuts
                        && !singleCopyThreatCarriesOnlyWinningPath
                        && ruleset == MahjongRuleset.ChineseOfficial)
                    {
                        var bestPotentialOuts = threatCandidatePool.Max(candidate => candidate.PotentialWinningDrawOuts);
                        var bestPotentialOutCopies = threatCandidatePool
                            .Where(candidate => candidate.PotentialWinningDrawOuts == bestPotentialOuts)
                            .Max(candidate => candidate.PotentialWinningDrawOutCopies);

                        var bestNonThreatPotentialOuts = nonThreatCandidates.Max(candidate => candidate.PotentialWinningDrawOuts);
                        var bestNonThreatPotentialOutCopies = nonThreatCandidates
                            .Where(candidate => candidate.PotentialWinningDrawOuts == bestNonThreatPotentialOuts)
                            .Max(candidate => candidate.PotentialWinningDrawOutCopies);

                        var nonThreatImprovesPotential = bestNonThreatPotentialOuts > bestPotentialOuts
                            || (bestNonThreatPotentialOuts == bestPotentialOuts
                                && bestNonThreatPotentialOutCopies >= bestPotentialOutCopies);

                        if (nonThreatImprovesPotential)
                        {
                            candidates = nonThreatCandidates;
                            filteredByKongThreatAvoidance = true;
                        }
                    }

                    if (losesAllWinningOuts
                        && ruleset == MahjongRuleset.ChineseOfficial)
                    {
                        var threatCandidates = threatCandidatePool
                            .Where(candidate => opponentKongThreatTiles.Contains(candidate.Tile))
                            .ToList();

                        if (threatCandidates.Count > 0)
                        {
                            var bestThreatWinningOuts = threatCandidates.Max(candidate => candidate.WinningDrawOuts);
                            var bestThreatWinningOutCopies = threatCandidates
                                .Where(candidate => candidate.WinningDrawOuts == bestThreatWinningOuts)
                                .Max(candidate => candidate.WinningDrawOutCopies);

                            var bestNonThreatPotentialOuts = nonThreatCandidates.Max(candidate => candidate.PotentialWinningDrawOuts);

                            if (bestThreatWinningOuts > 0 && bestNonThreatPotentialOuts == 0)
                            {
                                var threatWinningPathCandidates = threatCandidates
                                    .Where(candidate => candidate.WinningDrawOuts == bestThreatWinningOuts
                                        && candidate.WinningDrawOutCopies == bestThreatWinningOutCopies)
                                    .ToList();

                                if (threatWinningPathCandidates.Count > 0)
                                {
                                    candidates = threatWinningPathCandidates;
                                }
                            }
                        }
                    }
                }
                else if (allowThreatWhenNoWinningOuts)
                {
                    candidates = multiCopyThreatCandidates;
                }
            }
        }

        var suitCounts = ownedTiles
            .Where(static tile => !tile.IsHonor)
            .GroupBy(static tile => tile.Suit)
            .ToDictionary(group => group.Key, group => group.Count());

        if (suitCounts.Count > 0)
        {
            var maxSuitCount = suitCounts.Values.Max();
            var dominantSuits = suitCounts
                .Where(entry => entry.Value == maxSuitCount)
                .Select(entry => entry.Key)
                .ToList();

            var pureSuitThreshold = ruleset == MahjongRuleset.Changsha258
                ? Changsha258Ruleset.BigWinPureSuitEyeFlexibleThreshold
                : 10;
            if (dominantSuits.Count == 1 && maxSuitCount >= pureSuitThreshold)
            {
                var dominantSuit = dominantSuits[0];
                var hasSevenPairsPotential = ruleset == MahjongRuleset.ChineseOfficial
                    && meldTiles.Count == 0
                    && sevenPairsPairCount >= 3
                    && (sevenPairsPairCount >= 6
                        || ownedTiles.Any(tile => !tile.IsHonor && tile.Suit != dominantSuit)
                        || ownedTiles
                            .GroupBy(static tile => tile)
                            .Any(group => group.Key.IsHonor && group.Count() >= 3));

                if (!hasSevenPairsPotential)
                {
                    var offSuitCandidates = candidates
                        .Where(candidate => candidate.Tile.IsHonor || candidate.Tile.Suit != dominantSuit)
                        .ToList();

                    if (ruleset == MahjongRuleset.Changsha258 && enforceSmallWinPairBias && offSuitCandidates.Count > 0)
                    {
                        var smallWinPairTiles = handCounts
                            .Where(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
                            .Select(entry => entry.Key)
                            .ToList();

                        if (smallWinPairTiles.Count == 1 && !allowBreakingSoleChangshaSmallWinPairForPureSuitLean)
                        {
                            var protectedTile = smallWinPairTiles[0];
                            offSuitCandidates = offSuitCandidates
                                .Where(candidate => !candidate.Tile.Equals(protectedTile))
                                .ToList();
                        }
                    }

                    if (offSuitCandidates.Count > 0)
                    {
                        candidates = offSuitCandidates;
                        filteredByPureSuit = true;

                        if (ruleset == MahjongRuleset.Changsha258
                            && allowBreakingSoleChangshaSmallWinPairForPureSuitLean
                            && soleChangshaSmallWinPairTile is not null
                            && changshaDominantSuit.HasValue
                            && !hasDominantSuitLiveSmallWinCandidate
                            && visibleCounts.Count > 0
                            && AreDominantSuitSmallWinOutsDead(
                                changshaDominantSuit.Value,
                                ownedCounts,
                                visibleCounts))
                        {
                            var offSuitSmallWinPairCandidates = candidates
                                .Where(candidate => candidate.Tile.Equals(soleChangshaSmallWinPairTile))
                                .ToList();

                            if (offSuitSmallWinPairCandidates.Count > 0)
                            {
                                candidates = offSuitSmallWinPairCandidates;
                            }
                        }
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial)
        {
            var terminalOrHonorCount = ownedTiles.Count(static tile => tile.IsHonor || IsTerminalSuitedTile(tile));
            var nonTerminalSuitedCount = ownedTiles.Count(static tile => !tile.IsHonor && !IsTerminalSuitedTile(tile));
            var distinctOwnedOrphanTypes = meldTiles.Count == 0
                ? ownedTiles
                    .Where(tile => tile.IsHonor || IsTerminalSuitedTile(tile))
                    .Distinct()
                    .Count()
                : 0;
            var nearThirteenOrphansShape = meldTiles.Count == 0
                && nonTerminalSuitedCount is >= 0 and <= 3
                && distinctOwnedOrphanTypes >= 8;

            if (terminalOrHonorCount >= 11 && nonTerminalSuitedCount > 0 && !nearThirteenOrphansShape)
            {
                allowBreakingSolePairForTerminalHonorLean = true;
                var protectSolePairBecauseExhausted = false;

                if (soleChineseOfficialPairTile is not null)
                {
                    ownedCounts.TryGetValue(soleChineseOfficialPairTile, out var ownedPairCount);
                    visibleCounts.TryGetValue(soleChineseOfficialPairTile, out var visiblePairCount);
                    protectSolePairBecauseExhausted = 4 - ownedPairCount - visiblePairCount <= 0;
                }

                var nonTerminalCandidates = candidates
                    .Where(candidate => !candidate.Tile.IsHonor && !IsTerminalSuitedTile(candidate.Tile))
                    .ToList();

                if (nonTerminalCandidates.Count > 0)
                {
                    var bestLeanOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestLeanOverallOutCopies = candidates
                        .Where(candidate => candidate.WinningDrawOuts == bestLeanOverallOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var bestLeanNonTerminalOuts = nonTerminalCandidates.Max(candidate => candidate.WinningDrawOuts);
                    var bestLeanNonTerminalOutCopies = nonTerminalCandidates
                        .Where(candidate => candidate.WinningDrawOuts == bestLeanNonTerminalOuts)
                        .Max(candidate => candidate.WinningDrawOutCopies);

                    var reducesWinningOuts = bestLeanOverallOuts > bestLeanNonTerminalOuts
                        || (bestLeanOverallOuts == bestLeanNonTerminalOuts
                            && bestLeanOverallOutCopies > bestLeanNonTerminalOutCopies);

                    var reducesPotentialOuts = false;
                    if (!reducesWinningOuts && bestLeanOverallOuts == 0 && potentialOutSummaries is not null)
                    {
                        var bestPotentialOuts = candidates
                            .Max(candidate => potentialOutSummaries[candidate.Index].TileTypes);
                        var bestPotentialOutCopies = candidates
                            .Where(candidate => potentialOutSummaries[candidate.Index].TileTypes == bestPotentialOuts)
                            .Max(candidate => potentialOutSummaries[candidate.Index].RemainingCopies);

                        var bestNonTerminalPotentialOuts = nonTerminalCandidates
                            .Max(candidate => potentialOutSummaries[candidate.Index].TileTypes);
                        var bestNonTerminalPotentialOutCopies = nonTerminalCandidates
                            .Where(candidate => potentialOutSummaries[candidate.Index].TileTypes == bestNonTerminalPotentialOuts)
                            .Max(candidate => potentialOutSummaries[candidate.Index].RemainingCopies);

                        reducesPotentialOuts = bestPotentialOuts > bestNonTerminalPotentialOuts
                            || (bestPotentialOuts == bestNonTerminalPotentialOuts
                                && bestPotentialOutCopies > bestNonTerminalPotentialOutCopies);
                    }

                    if (reducesWinningOuts || reducesPotentialOuts)
                    {
                        allowBreakingSolePairForTerminalHonorLean = false;

                        var honorTerminalCandidates = candidates
                            .Where(candidate => candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile))
                            .ToList();

                        if (honorTerminalCandidates.Count > 0)
                        {
                            candidates = honorTerminalCandidates;
                        }
                    }
                    else if (protectSolePairBecauseExhausted)
                    {
                        allowBreakingSolePairForTerminalHonorLean = false;
                    }
                    else if (soleChineseOfficialPairTile is not null
                        && !soleChineseOfficialPairTile.IsHonor
                        && !IsTerminalSuitedTile(soleChineseOfficialPairTile))
                    {
                        var nonTerminalSingletonCandidates = nonTerminalCandidates
                            .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 1)
                            .ToList();

                        if (nonTerminalSingletonCandidates.Count > 0)
                        {
                            allowBreakingSolePairForTerminalHonorLean = false;
                            candidates = nonTerminalSingletonCandidates;
                        }
                        else
                        {
                            if (visibleCounts.Count > 0)
                            {
                                var deadHonorSingletonCandidates = candidates
                                    .Where(candidate => (candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile))
                                        && handCounts.TryGetValue(candidate.Tile, out var count)
                                        && count == 1
                                        && candidate.RemainingCopies <= 0)
                                    .ToList();

                                if (deadHonorSingletonCandidates.Count > 0)
                                {
                                    allowBreakingSolePairForTerminalHonorLean = false;
                                    candidates = deadHonorSingletonCandidates;
                                }
                                else
                                {
                                    var nearDeadHonorSingletonCandidates = candidates
                                        .Where(candidate => (candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile))
                                            && handCounts.TryGetValue(candidate.Tile, out var count)
                                            && count == 1
                                            && candidate.RemainingCopies == 1)
                                        .ToList();

                                    if (nearDeadHonorSingletonCandidates.Count > 0)
                                    {
                                        var bestOverallOuts = candidates.Max(candidate => candidate.WinningDrawOuts);
                                        var bestOverallOutCopies = candidates
                                            .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                                            .Max(candidate => candidate.WinningDrawOutCopies);

                                        var bestNearDeadOuts = nearDeadHonorSingletonCandidates.Max(candidate => candidate.WinningDrawOuts);
                                        var bestNearDeadOutCopies = nearDeadHonorSingletonCandidates
                                            .Where(candidate => candidate.WinningDrawOuts == bestNearDeadOuts)
                                            .Max(candidate => candidate.WinningDrawOutCopies);

                                        var withinOutsTolerance = bestNearDeadOuts >= bestOverallOuts - NearDeadHonorSingletonOutsTolerance;
                                        var withinCopyTolerance = bestNearDeadOuts < bestOverallOuts
                                            || bestNearDeadOutCopies >= bestOverallOutCopies - NearDeadHonorSingletonOutsTolerance;

                                        if (bestOverallOuts == 0 || (withinOutsTolerance && withinCopyTolerance))
                                        {
                                            allowBreakingSolePairForTerminalHonorLean = false;
                                            candidates = nearDeadHonorSingletonCandidates;
                                        }
                                        else
                                        {
                                            candidates = nonTerminalCandidates;
                                        }
                                    }
                                    else
                                    {
                                        candidates = nonTerminalCandidates;
                                    }
                                }
                            }
                            else
                            {
                                candidates = nonTerminalCandidates;
                            }
                        }
                    }
                    else
                    {
                        candidates = nonTerminalCandidates;
                    }
                }
            }
            else
            {
                var honorCount = ownedTiles.Count(static tile => tile.IsHonor);
                var suitedCount = ownedTiles.Count(static tile => !tile.IsHonor);
                if (honorCount >= 9 && suitedCount is > 0 and <= 3)
                {
                    var nonTerminalCandidates = candidates
                        .Where(candidate => !candidate.Tile.IsHonor && !IsTerminalSuitedTile(candidate.Tile))
                        .ToList();

                    if (nonTerminalCandidates.Count > 0)
                    {
                        candidates = nonTerminalCandidates;
                    }
                    else
                    {
                        var honorSingletonCandidates = candidates
                            .Where(candidate => candidate.Tile.IsHonor
                                && handCounts.TryGetValue(candidate.Tile, out var count)
                                && count == 1)
                            .ToList();

                        if (honorSingletonCandidates.Count > 0)
                        {
                            candidates = honorSingletonCandidates;
                        }
                        else
                        {
                            var offHonorCandidates = candidates
                                .Where(candidate => !candidate.Tile.IsHonor)
                                .ToList();

                            if (offHonorCandidates.Count > 0)
                            {
                                candidates = offHonorCandidates;
                            }
                        }
                    }
                }
            }

            var totalHonorCount = ownedTiles.Count(static tile => tile.IsHonor);
            var totalSuitedCount = ownedTiles.Count(static tile => !tile.IsHonor);
            var nonTerminalSuitedCountForHonorBias = ownedTiles.Count(static tile => !tile.IsHonor && !IsTerminalSuitedTile(tile));
            if (totalSuitedCount == 0)
            {
                var honorSingletonCandidates = candidates
                    .Where(candidate => candidate.Tile.IsHonor
                        && handCounts.TryGetValue(candidate.Tile, out var count)
                        && count == 1)
                    .ToList();

                if (honorSingletonCandidates.Count > 0)
                {
                    candidates = honorSingletonCandidates;
                }
            }
            else if (totalHonorCount >= 7 && totalSuitedCount >= 5 && nonTerminalSuitedCountForHonorBias >= 3)
            {
                var hasHonorSingleton = handCounts.Any(entry => entry.Key.IsHonor && entry.Value == 1);
                if (hasHonorSingleton)
                {
                    var suitedSingletonCandidates = candidates
                        .Where(candidate => !candidate.Tile.IsHonor
                            && handCounts.TryGetValue(candidate.Tile, out var count)
                            && count == 1)
                        .ToList();

                    if (suitedSingletonCandidates.Count > 0)
                    {
                        var nonChowSingletonCandidates = suitedSingletonCandidates
                            .Where(candidate => !HasCompleteChowInHand(handCounts, candidate.Tile))
                            .ToList();

                        if (nonChowSingletonCandidates.Count > 0)
                        {
                            suitedSingletonCandidates = nonChowSingletonCandidates;
                        }

                        candidates = suitedSingletonCandidates;
                    }
                }
            }

            if (meldTiles.Count == 0)
            {
                var orphanTiles = ownedTiles
                    .Where(tile => tile.IsHonor || IsTerminalSuitedTile(tile))
                    .ToList();
                var nonOrphanCount = ownedTiles.Count - orphanTiles.Count;

                if (nonOrphanCount == 0)
                {
                    var duplicateOrphanCandidates = candidates
                        .Where(candidate =>
                        {
                            handCounts.TryGetValue(candidate.Tile, out var count);
                            return count >= 2 && (candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile));
                        })
                        .ToList();

                    if (duplicateOrphanCandidates.Count > 0)
                    {
                        if (visibleCounts.Count > 0)
                        {
                            var deadDuplicateOrphanCandidates = duplicateOrphanCandidates
                                .Where(candidate => candidate.RemainingCopies <= 0)
                                .ToList();

                            if (deadDuplicateOrphanCandidates.Count > 0)
                            {
                                duplicateOrphanCandidates = deadDuplicateOrphanCandidates;
                            }
                        }

                        candidates = duplicateOrphanCandidates;
                    }
                }

                var distinctOrphanTypes = orphanTiles.Distinct().Count();
                if (nonOrphanCount is > 0 and <= 3 && distinctOrphanTypes >= 8)
                {
                    var hasLiveThirteenOrphansPath = HasLiveThirteenOrphansPath(
                        ownedCounts,
                        visibleCounts,
                        distinctOrphanTypes,
                        nonOrphanCount);

                    if (hasLiveThirteenOrphansPath)
                    {
                        if (soleChineseOfficialPairTile is not null
                            && !soleChineseOfficialPairTile.IsHonor
                            && !IsTerminalSuitedTile(soleChineseOfficialPairTile))
                        {
                            allowBreakingSolePairForTerminalHonorLean = true;
                        }

                        var nonOrphanCandidates = candidates
                            .Where(candidate => !candidate.Tile.IsHonor && !IsTerminalSuitedTile(candidate.Tile))
                            .ToList();

                        if (nonOrphanCandidates.Count > 0)
                        {
                            var nonOrphanSingletonCandidates = nonOrphanCandidates
                                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 1)
                                .ToList();

                            candidates = nonOrphanSingletonCandidates.Count > 0
                                ? nonOrphanSingletonCandidates
                                : nonOrphanCandidates;
                        }
                    }
                    else
                    {
                        var orphanCandidates = candidates
                            .Where(candidate => candidate.Tile.IsHonor || IsTerminalSuitedTile(candidate.Tile))
                            .ToList();

                        if (orphanCandidates.Count > 0)
                        {
                            if (visibleCounts.Count > 0)
                            {
                                var deadOrphanSingletonCandidates = orphanCandidates
                                    .Where(candidate => candidate.RemainingCopies == 0
                                        && handCounts.TryGetValue(candidate.Tile, out var count)
                                        && count == 1)
                                    .ToList();

                                if (deadOrphanSingletonCandidates.Count > 0)
                                {
                                    candidates = deadOrphanSingletonCandidates;
                                }
                                else
                                {
                                    candidates = orphanCandidates;
                                }
                            }
                            else
                            {
                                candidates = orphanCandidates;
                            }
                        }
                    }
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258)
        {
            var twoFiveEightCount = ownedTiles.Count(tile => Changsha258Ruleset.SmallWinPairRanks.Contains(tile.Rank));
            var offRankCount = ownedTiles.Count - twoFiveEightCount;
            if (ownedTiles.Count >= 10 && offRankCount is > 0 and <= 2)
            {
                var offRankCandidates = candidates
                    .Where(candidate => !Changsha258Ruleset.SmallWinPairRanks.Contains(candidate.Tile.Rank))
                    .ToList();

                if (offRankCandidates.Count > 0)
                {
                    candidates = offRankCandidates;
                }
            }
        }

        var prioritizedCandidates = candidates;
        if (ruleset == MahjongRuleset.ChineseOfficial
            && sevenPairsPairCount >= 6
            && visibleCounts.Count > 0)
        {
            var deadSingletonCandidates = prioritizedCandidates
                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count)
                    && count == 1
                    && candidate.RemainingCopies == 0)
                .ToList();

            if (deadSingletonCandidates.Count > 0)
            {
                prioritizedCandidates = deadSingletonCandidates;
            }
            else
            {
                var nearDeadSingletonCandidates = prioritizedCandidates
                    .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count)
                        && count == 1
                        && candidate.RemainingCopies == 1)
                    .ToList();

                if (nearDeadSingletonCandidates.Count > 0)
                {
                    prioritizedCandidates = nearDeadSingletonCandidates;
                }
            }
        }

        var appliedSevenPairsTripletFocus = false;
        if (ruleset == MahjongRuleset.ChineseOfficial && sevenPairsPairCount >= 6)
        {
            var tripletCandidates = prioritizedCandidates
                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 3)
                .ToList();

            if (tripletCandidates.Count > 0)
            {
                prioritizedCandidates = tripletCandidates;
                appliedSevenPairsTripletFocus = true;
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial
            && sevenPairsPairCount >= 6
            && !appliedSevenPairsTripletFocus)
        {
            var singletonCandidates = prioritizedCandidates
                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 1)
                .ToList();

            if (singletonCandidates.Count > 0)
            {
                prioritizedCandidates = singletonCandidates;
            }
        }

        if (ruleset == MahjongRuleset.Changsha258 && hasChangshaSevenPairsLean)
        {
            var tripletCandidates = prioritizedCandidates
                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 3)
                .ToList();

            if (tripletCandidates.Count > 0)
            {
                prioritizedCandidates = tripletCandidates;
            }
            else
            {
                var singletonCandidates = prioritizedCandidates
                    .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count) && count == 1)
                    .ToList();

                if (singletonCandidates.Count > 0)
                {
                    prioritizedCandidates = singletonCandidates;
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && hasChangshaSevenPairsLean
            && visibleCounts.Count > 0)
        {
            var deadSingletonCandidates = prioritizedCandidates
                .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count)
                    && count == 1
                    && candidate.RemainingCopies == 0)
                .ToList();

            if (deadSingletonCandidates.Count > 0)
            {
                prioritizedCandidates = deadSingletonCandidates;
            }
            else
            {
                var nearDeadSingletonCandidates = prioritizedCandidates
                    .Where(candidate => handCounts.TryGetValue(candidate.Tile, out var count)
                        && count == 1
                        && candidate.RemainingCopies == 1)
                    .ToList();

                if (nearDeadSingletonCandidates.Count > 0)
                {
                    prioritizedCandidates = nearDeadSingletonCandidates;
                }
            }
        }

        var maxSmallWinOuts = 0;
        var overallMaxSmallWinOuts = 0;

        if (ruleset == MahjongRuleset.Changsha258 && enforceSmallWinPairBias)
        {
            overallMaxSmallWinOuts = allCandidates.Max(candidate => candidate.SmallWinningDrawOuts);
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
            else if (overallMaxSmallWinOuts > 0)
            {
                prioritizedCandidates = allCandidates
                    .Where(candidate => candidate.SmallWinningDrawOuts == overallMaxSmallWinOuts)
                    .ToList();
                maxSmallWinOuts = overallMaxSmallWinOuts;

                var maxSmallWinCopies = prioritizedCandidates.Max(candidate => candidate.SmallWinningDrawOutCopies);
                if (maxSmallWinCopies > 0)
                {
                    prioritizedCandidates = prioritizedCandidates
                        .Where(candidate => candidate.SmallWinningDrawOutCopies == maxSmallWinCopies)
                        .ToList();
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258 && !enforceSmallWinPairBias)
        {
            var maxBigWinOuts = prioritizedCandidates.Max(candidate => candidate.BigWinningDrawOuts);
            if (maxBigWinOuts > 0)
            {
                prioritizedCandidates = prioritizedCandidates
                    .Where(candidate => candidate.BigWinningDrawOuts == maxBigWinOuts)
                    .ToList();

                var maxBigWinCopies = prioritizedCandidates.Max(candidate => candidate.BigWinningDrawOutCopies);
                if (maxBigWinCopies > 0)
                {
                    prioritizedCandidates = prioritizedCandidates
                        .Where(candidate => candidate.BigWinningDrawOutCopies == maxBigWinCopies)
                        .ToList();
                }
            }
        }

        var allowOutsOverride = !(filteredByNearDeadOffRankPair
            || filteredByNearDeadSmallWinSingleton
            || filteredByIsolatedOffRankPair
            || filteredByDeadPair
            || filteredByDeadOffRankPair
            || filteredByDeadSingleton
            || filteredByDeadSuitedSingleton
            || filteredByKongThreatAvoidance);
        var allowWinningOutsOverride = allowOutsOverride && !filteredByPureSuit;
        var allowPureSuitOutsOverride = allowOutsOverride && filteredByPureSuit;

        if (allowPureSuitOutsOverride
            && ruleset == MahjongRuleset.Changsha258
            && hasChangshaPureSuitLean)
        {
            allowPureSuitOutsOverride = prioritizedCandidates.All(candidate => candidate.RemainingCopies <= 0);
        }

        var maxPrioritizedOuts = prioritizedCandidates.Max(candidate => candidate.WinningDrawOuts);

        if ((allowWinningOutsOverride || allowPureSuitOutsOverride) && maxPrioritizedOuts == 0)
        {
            var bestOverallOuts = allCandidates.Max(candidate => candidate.WinningDrawOuts);
            if (bestOverallOuts > 0)
            {
                prioritizedCandidates = allCandidates
                    .Where(candidate => candidate.WinningDrawOuts == bestOverallOuts)
                    .ToList();

                var bestOverallOutCopies = prioritizedCandidates.Max(candidate => candidate.WinningDrawOutCopies);
                prioritizedCandidates = prioritizedCandidates
                    .Where(candidate => candidate.WinningDrawOutCopies == bestOverallOutCopies)
                    .ToList();
            }
        }

        if (ruleset == MahjongRuleset.ChineseOfficial
            && potentialOutSummaries is not null
            && prioritizedCandidates.Max(candidate => candidate.WinningDrawOuts) == 0)
        {
            var maxPotentialOuts = prioritizedCandidates.Max(candidate => candidate.PotentialWinningDrawOuts);
            if (maxPotentialOuts > 0)
            {
                prioritizedCandidates = prioritizedCandidates
                    .Where(candidate => candidate.PotentialWinningDrawOuts == maxPotentialOuts)
                    .ToList();

                var maxPotentialCopies = prioritizedCandidates.Max(candidate => candidate.PotentialWinningDrawOutCopies);
                if (maxPotentialCopies > 0)
                {
                    prioritizedCandidates = prioritizedCandidates
                        .Where(candidate => candidate.PotentialWinningDrawOutCopies == maxPotentialCopies)
                        .ToList();
                }
            }
        }

        if (ruleset == MahjongRuleset.Changsha258
            && potentialOutSummaries is not null
            && prioritizedCandidates.Max(candidate => candidate.WinningDrawOuts) == 0)
        {
            var maxPotentialOuts = prioritizedCandidates.Max(candidate => candidate.PotentialWinningDrawOuts);
            if (maxPotentialOuts > 0)
            {
                prioritizedCandidates = prioritizedCandidates
                    .Where(candidate => candidate.PotentialWinningDrawOuts == maxPotentialOuts)
                    .ToList();

                var maxPotentialCopies = prioritizedCandidates.Max(candidate => candidate.PotentialWinningDrawOutCopies);
                if (maxPotentialCopies > 0)
                {
                    prioritizedCandidates = prioritizedCandidates
                        .Where(candidate => candidate.PotentialWinningDrawOutCopies == maxPotentialCopies)
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

        if (soleChineseOfficialPairTile is not null)
        {
            var protectSolePair = !allowBreakingSolePairForTerminalHonorLean
                || soleChineseOfficialPairTile.IsHonor
                || IsTerminalSuitedTile(soleChineseOfficialPairTile);

            if (protectSolePair)
            {
                var nonPairCandidates = prioritizedCandidates
                    .Where(candidate => !candidate.Tile.Equals(soleChineseOfficialPairTile))
                    .ToList();

                if (nonPairCandidates.Count > 0)
                {
                    prioritizedCandidates = nonPairCandidates;
                }
            }
        }

        if (soleChangshaSmallWinPairTile is not null)
        {
            handCounts.TryGetValue(soleChangshaSmallWinPairTile, out var solePairCount);
            if (solePairCount <= 2 && !allowBreakingSoleChangshaSmallWinPairForPureSuitLean)
            {
                var nonPairCandidates = prioritizedCandidates
                    .Where(candidate => !candidate.Tile.Equals(soleChangshaSmallWinPairTile))
                    .ToList();

                if (nonPairCandidates.Count > 0)
                {
                    prioritizedCandidates = nonPairCandidates;
                }
            }
        }

        if (lastSmallWinSingleton is not null
            && enforceSmallWinPairBias
            && lastSmallWinSingletonRemainingCopies >= 2)
        {
            var nonSingletonCandidates = prioritizedCandidates
                .Where(candidate => !candidate.Tile.Equals(lastSmallWinSingleton))
                .ToList();

            if (nonSingletonCandidates.Count > 0)
            {
                prioritizedCandidates = nonSingletonCandidates;
            }
        }

        if (promotableKongTiles.Count > 0)
        {
            var nonPromotableKongCandidates = prioritizedCandidates
                .Where(candidate => !promotableKongTiles.Contains(candidate.Tile))
                .ToList();

            if (nonPromotableKongCandidates.Count > 0)
            {
                prioritizedCandidates = nonPromotableKongCandidates;
            }
        }

        var lowestScore = prioritizedCandidates.Min(candidate => candidate.Score);
        var lowestScoreCandidates = prioritizedCandidates
            .Where(candidate => candidate.Score == lowestScore)
            .ToList();
        var lowestScoreHasSmallWinTile = ruleset == MahjongRuleset.Changsha258
            && enforceSmallWinPairBias
            && lowestScoreCandidates.Any(candidate => Changsha258Ruleset.IsSmallWinPair(candidate.Tile));

        var useRemainingCopyTieBreaker = visibleCounts.Count > 0
            && (ruleset == MahjongRuleset.ChineseOfficial
                || !enforceSmallWinPairBias
                || (maxSmallWinOuts == 0 && !hasSmallWinTileInHand)
                || (ruleset == MahjongRuleset.Changsha258 && !lowestScoreHasSmallWinTile));

        var orderedPrioritizedCandidates = prioritizedCandidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => useRemainingCopyTieBreaker ? candidate.RemainingCopies : 0)
            .ThenByDescending(candidate => candidate.SafeMeldExposure)
            .ThenBy(candidate => candidate.Tile.IsHonor ? 0 : 1)
            .ThenBy(candidate => IsTerminalSuitedTile(candidate.Tile) ? 0 : 1)
            .ThenBy(candidate => candidate.Tile.Suit)
            .ThenBy(candidate => candidate.Tile.Rank)
            .ThenBy(candidate => candidate.Tile.Wind.HasValue ? (int)candidate.Tile.Wind.Value : -1)
            .ThenBy(candidate => candidate.Tile.Dragon.HasValue ? (int)candidate.Tile.Dragon.Value : -1)
            .ThenBy(candidate => candidate.Index)
            .ToList();

        var selectedCandidate = orderedPrioritizedCandidates[0];
        if (promotableKongTiles.Count > 0 && promotableKongTiles.Contains(selectedCandidate.Tile))
        {
            var alternateCandidate = orderedPrioritizedCandidates
                .FirstOrDefault(candidate => !promotableKongTiles.Contains(candidate.Tile));

            alternateCandidate ??= allCandidates
                .OrderBy(candidate => candidate.Score)
                .ThenBy(candidate => useRemainingCopyTieBreaker ? candidate.RemainingCopies : 0)
                .ThenBy(candidate => candidate.Tile.IsHonor ? 0 : 1)
                .ThenBy(candidate => IsTerminalSuitedTile(candidate.Tile) ? 0 : 1)
                .ThenBy(candidate => candidate.Tile.Suit)
                .ThenBy(candidate => candidate.Tile.Rank)
                .ThenBy(candidate => candidate.Tile.Wind.HasValue ? (int)candidate.Tile.Wind.Value : -1)
                .ThenBy(candidate => candidate.Tile.Dragon.HasValue ? (int)candidate.Tile.Dragon.Value : -1)
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault(candidate => !promotableKongTiles.Contains(candidate.Tile));

            if (alternateCandidate is not null)
            {
                selectedCandidate = alternateCandidate;
            }
        }

        return selectedCandidate.Index;
    }

    private static int GetBotTileKeepScore(
        Tile tile,
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        MahjongRuleset ruleset,
        bool enforceSmallWinPairBias,
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
        var focusTiles = meldTiles.Count == 0
            ? handTiles
            : handTiles.Concat(meldTiles).ToList();
        var honorCount = focusTiles.Count(static other => other.IsHonor);
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

        var concealedKongBonus = tileMatchCount >= 4 ? 8 : 0;

        var pairCount = tileCounts.Count(static entry => entry.Value >= 2);
        var actualPairCount = tileCounts.Count(static entry => entry.Value == 2);
        var smallWinPairCount = ruleset == MahjongRuleset.Changsha258
            ? tileCounts.Count(static entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
            : 0;
        var sevenPairsPairCount = meldTiles.Count == 0
            ? tileCounts.Sum(static entry => entry.Value / 2)
            : 0;
        var hasChangshaSevenPairsLean = ruleset == MahjongRuleset.Changsha258
            && meldTiles.Count == 0
            && sevenPairsPairCount >= 6;
        if (tileMatchCount == 2 && pairCount == 1)
        {
            score += 2;
        }

        var applySmallWinPairBias = enforceSmallWinPairBias && ruleset == MahjongRuleset.Changsha258;

        var hasDeadSmallWinSingleton = applySmallWinPairBias
            && smallWinPairCount == 0
            && visibleCounts.Count > 0
            && tileCounts.Any(entry => entry.Value == 1
                && Changsha258Ruleset.IsSmallWinPair(entry.Key)
                && IsTileExhausted(entry.Key, ownedCounts, visibleCounts));

        var deadPairPenalty = 0;
        if (tileMatchCount == 2 && pairCount >= 2)
        {
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);
            var remainingCopies = 4 - ownedCount - visibleCount;

            if (remainingCopies <= 0)
            {
                if (ruleset == MahjongRuleset.ChineseOfficial)
                {
                    deadPairPenalty = -4;
                }
                else if (applySmallWinPairBias
                         && smallWinPairCount >= 1
                         && !Changsha258Ruleset.IsSmallWinPair(tile))
                {
                    // Once a valid 2/5/8 eye is already secured in Changsha,
                    // dead off-rank pairs are low-value baggage and should be
                    // broken before live, connected singletons (unless leaning
                    // on a seven-pairs finish).
                    if (!hasChangshaSevenPairsLean)
                    {
                        deadPairPenalty = -18;
                    }
                }
                else if (ruleset == MahjongRuleset.Changsha258
                         && actualPairCount >= 2
                         && !Changsha258Ruleset.IsSmallWinPair(tile))
                {
                    deadPairPenalty = -8;
                }
            }
        }

        var sevenPairsBias = 0;
        if (meldTiles.Count == 0 && sevenPairsPairCount >= 3)
        {
            if (tileMatchCount >= 2)
            {
                sevenPairsBias = sevenPairsPairCount switch
                {
                    >= 6 => 3,
                    5 => 2,
                    4 => 1,
                    _ => 1
                };
            }
            else
            {
                sevenPairsBias = sevenPairsPairCount switch
                {
                    >= 6 => -3,
                    5 => -2,
                    4 => -1,
                    _ => -1
                };
            }
        }

        var sevenPairsTripletBonus = 0;
        if (meldTiles.Count == 0 && sevenPairsPairCount >= 3 && tileMatchCount >= 3)
        {
            sevenPairsTripletBonus = tileMatchCount >= 4 ? 6 : 4;
        }
        var smallWinPairBonus = 0;
        if (applySmallWinPairBias && Changsha258Ruleset.IsSmallWinPair(tile))
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

        var requiredSmallWinPairBonus = applySmallWinPairBias
            && smallWinPairCount == 1
            && tileMatchCount >= 2
            && Changsha258Ruleset.IsSmallWinPair(tile)
            ? 6
            : 0;

        var missingSmallWinPairBonus = applySmallWinPairBias
            && smallWinPairCount == 0
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && hasLiveSmallWinPairCandidate
            ? 4
            : 0;

        var deadSmallWinPairPenalty = applySmallWinPairBias
            && smallWinPairCount == 0
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && !hasLiveSmallWinPairCandidate
            ? -3
            : 0;

        var nonSmallWinPairPenalty = applySmallWinPairBias
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && hasStrongSmallWinPairCandidate
            && !hasDeadSmallWinSingleton
            ? -14
            : 0;

        var fallbackNonSmallWinPairBonus = applySmallWinPairBias
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && !hasStrongSmallWinPairCandidate
            ? 6
            : 0;

        var deadFallbackNonSmallWinPairPenalty = 0;
        if (fallbackNonSmallWinPairBonus > 0 && visibleCounts.Count > 0)
        {
            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);
            if (4 - ownedCount - visibleCount <= 0)
            {
                fallbackNonSmallWinPairBonus = 0;
                deadFallbackNonSmallWinPairPenalty = -8;
            }
        }

        var hasAdjacentSmallWinSingleton = applySmallWinPairBias
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && hasStrongSmallWinPairCandidate
            && HasAdjacentSmallWinSingleton(tileCounts, ownedCounts, visibleCounts, tile);

        var adjacentSmallWinSingletonPenalty = hasAdjacentSmallWinSingleton ? -4 : 0;

        var deadAdjacentSmallWinPairRelief = applySmallWinPairBias
            && smallWinPairCount == 0
            && pairCount == 1
            && tileMatchCount >= 2
            && !Changsha258Ruleset.IsSmallWinPair(tile)
            && hasStrongSmallWinPairCandidate
            && visibleCounts.Count > 0
            && !hasAdjacentSmallWinSingleton
            && HasAdjacentSmallWinSingletonInHand(tileCounts, tile)
            ? 6
            : 0;

        var deadAdjacentSmallWinSingletonPenalty = applySmallWinPairBias
            && smallWinPairCount == 0
            && tileMatchCount == 1
            && Changsha258Ruleset.IsSmallWinPair(tile)
            && hasStrongSmallWinPairCandidate
            && visibleCounts.Count > 0
            && HasAdjacentOffRankPair(tileCounts, tile)
            && IsTileExhausted(tile, ownedCounts, visibleCounts)
            ? -6
            : 0;

        var soleSmallWinPairKeepBonus = 0;
        if (applySmallWinPairBias
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
        if (applySmallWinPairBias
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

        var nearPureSuitPenalty = 0;
        if (!tile.IsHonor && suitedCounts.Count > 1)
        {
            var maxSuitCount = suitedCounts.Values.Max();
            if (maxSuitCount >= 12 && focusSuitCount < maxSuitCount)
            {
                nearPureSuitPenalty = -6;
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

            var honorMajorityBonus = tileMatchCount == 1 && honorCount >= 7 && honorSingletonCount >= 3 ? 4 : 0;

            return score + suitClusterBonus + suitFocusPenalty + suitFocusBonus + honorFocusPenalty + sevenPairsBias + sevenPairsTripletBonus + honorMajorityBonus + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus + deadSmallWinPairPenalty + nonSmallWinPairPenalty + adjacentSmallWinSingletonPenalty + meldSuitFocusBonus + meldExtensionBonus + concealedKongBonus + soleSmallWinPairKeepBonus + soleSmallWinPairSingletonBonus + deadPairPenalty + deadFallbackNonSmallWinPairPenalty;
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

        return score + suitClusterBonus + suitSingletonPenalty + suitPairBonus + suitFocusPenalty + suitFocusBonus + dominantSuitPenalty + nearPureSuitPenalty + sevenPairsBias + sevenPairsTripletBonus + smallWinPairBonus + requiredSmallWinPairBonus + missingSmallWinPairBonus + deadSmallWinPairPenalty + nonSmallWinPairPenalty + fallbackNonSmallWinPairBonus + adjacentSmallWinSingletonPenalty + deadAdjacentSmallWinPairRelief + deadAdjacentSmallWinSingletonPenalty + meldSuitFocusBonus + meldExtensionBonus + concealedKongBonus + soleSmallWinPairKeepBonus + soleSmallWinPairSingletonBonus + deadPairPenalty + deadFallbackNonSmallWinPairPenalty;
    }

    private static WinningDrawOutSummary CountWinningDrawOutsAfterDiscard(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        int discardIndex,
        MahjongRuleset ruleset,
        bool requireMinimumFan = true)
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

            WinningHand? winningHand;
            if (!requireMinimumFan)
            {
                if (ruleset == MahjongRuleset.Changsha258)
                {
                    if (!Changsha258Ruleset.TryGetWinningHand(candidateHand, out winningHand))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!HandAnalyzer.TryGetWinningHand(candidateHand, out winningHand))
                    {
                        continue;
                    }
                }
            }
            else
            {
                if (!RulesetHandAnalyzer.TryGetWinningHand(candidateHand, ruleset, out winningHand))
                {
                    continue;
                }
            }

            if (winningHand is null)
            {
                continue;
            }

            if (requireMinimumFan && !ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset))
            {
                continue;
            }

            winningDrawOuts++;
            winningDrawOutCopies += remainingCopies;

            if (ruleset == MahjongRuleset.Changsha258)
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

    private static IReadOnlyList<Tile> BuildChineseOfficialOrphanTileTypes()
    {
        var suitedOrphans = new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboos }
            .SelectMany(static suit => new[] { 1, 9 }.Select(rank => new Tile(suit, rank)));
        var windTiles = Enum
            .GetValues<Wind>()
            .Select(wind => new Tile(TileSuit.Winds, 0, wind));
        var dragonTiles = Enum
            .GetValues<Dragon>()
            .Select(dragon => new Tile(TileSuit.Dragons, 0, null, dragon));

        return suitedOrphans
            .Concat(windTiles)
            .Concat(dragonTiles)
            .ToArray();
    }

    private static bool HasLiveThirteenOrphansPath(
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        int distinctOrphanTypes,
        int nonOrphanCount)
    {
        var orphanTypeCount = ChineseOfficialOrphanTileTypes.Count;
        var orphanTilesInHand = ChineseOfficialOrphanTileTypes
            .Where(tile => ownedCounts.TryGetValue(tile, out var count) && count > 0)
            .ToList();
        var hasOrphanPair = orphanTilesInHand.Any(tile => ownedCounts[tile] >= 2);

        bool HasRemainingCopy(Tile tile, int ownedCount)
        {
            visibleCounts.TryGetValue(tile, out var visibleCount);
            return 4 - ownedCount - visibleCount > 0;
        }

        var hasOwnedPairPotential = orphanTilesInHand.Any(tile =>
        {
            ownedCounts.TryGetValue(tile, out var ownedCount);
            return ownedCount == 1 && HasRemainingCopy(tile, ownedCount);
        });

        if (distinctOrphanTypes >= orphanTypeCount)
        {
            return hasOrphanPair || hasOwnedPairPotential;
        }

        var hasMissingOrphan = false;
        var hasMissingPairPotential = false;
        var hasNearDeadMissingOrphan = false;
        foreach (var tile in ChineseOfficialOrphanTileTypes)
        {
            if (ownedCounts.ContainsKey(tile))
            {
                continue;
            }

            hasMissingOrphan = true;
            visibleCounts.TryGetValue(tile, out var visibleCount);
            var remainingCopies = 4 - visibleCount;
            if (remainingCopies <= 0)
            {
                return false;
            }

            if (remainingCopies == 1)
            {
                hasNearDeadMissingOrphan = true;
            }

            if (remainingCopies >= 2)
            {
                hasMissingPairPotential = true;
            }
        }

        if (!hasMissingOrphan)
        {
            return hasOrphanPair || hasOwnedPairPotential;
        }

        if (hasNearDeadMissingOrphan && !hasOrphanPair && nonOrphanCount >= 2)
        {
            return false;
        }

        if (hasOrphanPair)
        {
            return true;
        }

        return hasOwnedPairPotential || hasMissingPairPotential;
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

    private static bool HasAlternateStrongSmallWinPairCandidate(
        Tile excludedTile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        var pairCount = handCounts.Count(entry => entry.Value >= 2);

        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            if (tile.Equals(excludedTile))
            {
                continue;
            }

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

                if (remainingCopies >= 1 && HasAdjacentSuitedTile(handCounts, tile) && pairCount == 0)
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

    private static bool HasStrongSmallWinPairCandidate(
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (visibleCounts.Count == 0)
        {
            return true;
        }

        var pairCount = handCounts.Count(entry => entry.Value >= 2);
        var hasSmallWinEvidence = handCounts.Keys.Any(Changsha258Ruleset.IsSmallWinPair)
            || visibleCounts.Keys.Any(Changsha258Ruleset.IsSmallWinPair);

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

                if (remainingCopies >= 1 && HasAdjacentSuitedTile(handCounts, tile))
                {
                    if (pairCount == 0)
                    {
                        return true;
                    }
                }

                continue;
            }

            if (remainingCopies >= 3 && (pairCount == 0 || hasSmallWinEvidence))
            {
                return true;
            }
        }

        return false;
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
            if (handCount >= 2)
            {
                return true;
            }

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

    private static bool HasDominantSuitStrongSmallWinCandidate(
        TileSuit dominantSuit,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            if (tile.Suit != dominantSuit)
            {
                continue;
            }

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

    private static bool HasNearDeadDominantSuitSmallWinSingleton(
        TileSuit dominantSuit,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            if (tile.Suit != dominantSuit)
            {
                continue;
            }

            if (!handCounts.TryGetValue(tile, out var handCount) || handCount != 1)
            {
                continue;
            }

            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            if (4 - ownedCount - visibleCount == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNearDeadDominantSuitSmallWinOuts(
        TileSuit dominantSuit,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        var remainingCopies = 0;

        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            if (tile.Suit != dominantSuit)
            {
                continue;
            }

            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            remainingCopies += Math.Max(0, 4 - ownedCount - visibleCount);
            if (remainingCopies > 1)
            {
                return false;
            }
        }

        return remainingCopies == 1;
    }

    private static bool AreDominantSuitSmallWinOutsDead(
        TileSuit dominantSuit,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        foreach (var tile in Changsha258SmallWinPairTiles)
        {
            if (tile.Suit != dominantSuit)
            {
                continue;
            }

            ownedCounts.TryGetValue(tile, out var ownedCount);
            visibleCounts.TryGetValue(tile, out var visibleCount);

            if (4 - ownedCount - visibleCount > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int? TrySelectChangshaDeadSmallWinSingletonDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        bool enforceSmallWinPairBias,
        bool hasChangshaPureSuitLean,
        TileSuit? changshaDominantSuit)
    {
        if (!enforceSmallWinPairBias || visibleCounts.Count == 0)
        {
            return null;
        }

        var smallWinSingletons = handCounts
            .Where(entry => entry.Value == 1 && Changsha258Ruleset.IsSmallWinPair(entry.Key))
            .Select(entry =>
            {
                ownedCounts.TryGetValue(entry.Key, out var ownedCount);
                visibleCounts.TryGetValue(entry.Key, out var visibleCount);
                return new
                {
                    Tile = entry.Key,
                    RemainingCopies = 4 - ownedCount - visibleCount
                };
            })
            .ToList();

        var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));

        bool ShouldIgnoreSequencePotential(Tile tile)
        {
            if (smallWinPairCount < 1 || !Changsha258Ruleset.IsSmallWinPair(tile))
            {
                return false;
            }

            if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue && tile.Suit == changshaDominantSuit.Value)
            {
                return false;
            }

            return !HasInHandSequenceAnchor(tile, handCounts);
        }

        var liveSmallWinSingletons = smallWinSingletons
            .Where(entry => entry.RemainingCopies > 0)
            .ToList();
        var deadSmallWinSingletons = smallWinSingletons
            .Where(entry => entry.RemainingCopies <= 0)
            .ToList();

        if (deadSmallWinSingletons.Count == 0)
        {
            return null;
        }

        var deadTiles = deadSmallWinSingletons.Select(entry => entry.Tile).ToHashSet();
        if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue)
        {
            for (var i = 0; i < handTiles.Count; i++)
            {
                var tile = handTiles[i];
                if (!deadTiles.Contains(tile) || tile.Suit == changshaDominantSuit.Value)
                {
                    continue;
                }

                var ignoreSequencePotential = ShouldIgnoreSequencePotential(tile);
                if (!ignoreSequencePotential && HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts))
                {
                    continue;
                }

                return i;
            }
        }

        for (var i = 0; i < handTiles.Count; i++)
        {
            var tile = handTiles[i];
            if (!deadTiles.Contains(tile))
            {
                continue;
            }

            var ignoreSequencePotential = ShouldIgnoreSequencePotential(tile);
            if (!ignoreSequencePotential && HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts))
            {
                continue;
            }

            return i;
        }

        if (liveSmallWinSingletons.Count > 0)
        {
            if (hasChangshaPureSuitLean && changshaDominantSuit.HasValue)
            {
                for (var i = 0; i < handTiles.Count; i++)
                {
                    var tile = handTiles[i];
                    if (!deadTiles.Contains(tile) || tile.Suit == changshaDominantSuit.Value)
                    {
                        continue;
                    }

                    var ignoreSequencePotential = ShouldIgnoreSequencePotential(tile);
                    if (!ignoreSequencePotential && HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts))
                    {
                        continue;
                    }

                    return i;
                }
            }

            for (var i = 0; i < handTiles.Count; i++)
            {
                var tile = handTiles[i];
                if (!deadTiles.Contains(tile))
                {
                    continue;
                }

                var ignoreSequencePotential = ShouldIgnoreSequencePotential(tile);
                if (!ignoreSequencePotential && HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts))
                {
                    continue;
                }

                return i;
            }
        }

        return null;
    }

    private static int? TrySelectChangshaIsolatedOffRankConcealedKongDiscardIndex(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        bool enforceSmallWinPairBias,
        bool hasChangshaPureSuitLean,
        TileSuit? changshaDominantSuit)
    {
        if (!enforceSmallWinPairBias)
        {
            return null;
        }

        var smallWinPairCount = handCounts.Count(entry => entry.Value >= 2 && Changsha258Ruleset.IsSmallWinPair(entry.Key));
        if (smallWinPairCount > 0)
        {
            return null;
        }

        var offRankKongTiles = handCounts
            .Where(entry => entry.Value >= 4 && !Changsha258Ruleset.IsSmallWinPair(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        if (offRankKongTiles.Count != 1)
        {
            return null;
        }

        var kongTile = offRankKongTiles[0];
        if (HasSequencePotential(kongTile, handCounts, ownedCounts, visibleCounts))
        {
            return null;
        }

        if (hasChangshaPureSuitLean
            && changshaDominantSuit.HasValue
            && kongTile.Suit == changshaDominantSuit.Value)
        {
            return null;
        }


        for (var i = 0; i < handTiles.Count; i++)
        {
            if (handTiles[i].Equals(kongTile))
            {
                return i;
            }
        }

        return null;
    }

    private static bool HasInHandSequenceAnchor(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts)
    {
        if (!IsSuitedTile(tile))
        {
            return false;
        }

        var suit = tile.Suit;
        var rank = tile.Rank;

        bool HasTileInHand(int targetRank)
        {
            if (targetRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            return handCounts.TryGetValue(new Tile(suit, targetRank), out var count) && count > 0;
        }

        var hasLowSequence = rank >= 3
            && HasTileInHand(rank - 2)
            && HasTileInHand(rank - 1);
        var hasMiddleSequence = rank is >= 2 and <= 8
            && HasTileInHand(rank - 1)
            && HasTileInHand(rank + 1);
        var hasHighSequence = rank <= 7
            && HasTileInHand(rank + 1)
            && HasTileInHand(rank + 2);

        return hasLowSequence || hasMiddleSequence || hasHighSequence;
    }

    private static bool HasAdjacentSuitedTile(
        IReadOnlyDictionary<Tile, int> handCounts,
        Tile tile)
    {
        if (tile.IsHonor)
        {
            return false;
        }

        var suit = tile.Suit;
        var rank = tile.Rank;

        foreach (var candidate in handCounts.Keys)
        {
            if (candidate.IsHonor || candidate.Suit != suit)
            {
                continue;
            }

            if (Math.Abs(candidate.Rank - rank) == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCompleteChowInHand(
        IReadOnlyDictionary<Tile, int> handCounts,
        Tile tile)
    {
        if (!IsSuitedTile(tile))
        {
            return false;
        }

        bool HasInHand(Tile candidate)
            => handCounts.TryGetValue(candidate, out var count) && count > 0;

        var suit = tile.Suit;
        var rank = tile.Rank;

        var hasLowSequence = rank >= 3
            && HasInHand(new Tile(suit, rank - 2))
            && HasInHand(new Tile(suit, rank - 1));
        var hasMiddleSequence = rank is >= 2 and <= 8
            && HasInHand(new Tile(suit, rank - 1))
            && HasInHand(new Tile(suit, rank + 1));
        var hasHighSequence = rank <= 7
            && HasInHand(new Tile(suit, rank + 1))
            && HasInHand(new Tile(suit, rank + 2));

        return hasLowSequence || hasMiddleSequence || hasHighSequence;
    }

    private static bool HasAdjacentSmallWinSingleton(
        IReadOnlyDictionary<Tile, int> tileCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        Tile tile)
    {
        if (!HasAdjacentSmallWinSingletonInHand(tileCounts, tile))
        {
            return false;
        }

        if (visibleCounts.Count == 0)
        {
            return true;
        }

        var suit = tile.Suit;

        foreach (var candidate in Changsha258SmallWinPairTiles)
        {
            if (candidate.Suit != suit)
            {
                continue;
            }

            if (Math.Abs(candidate.Rank - tile.Rank) != 1)
            {
                continue;
            }

            if (!tileCounts.TryGetValue(candidate, out var count) || count != 1)
            {
                continue;
            }

            ownedCounts.TryGetValue(candidate, out var ownedCount);
            visibleCounts.TryGetValue(candidate, out var visibleCount);
            var remainingCopies = 4 - ownedCount - visibleCount;

            if (remainingCopies > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAdjacentSmallWinSingletonInHand(
        IReadOnlyDictionary<Tile, int> tileCounts,
        Tile tile)
    {
        if (tile.IsHonor)
        {
            return false;
        }

        var suit = tile.Suit;

        foreach (var candidate in Changsha258SmallWinPairTiles)
        {
            if (candidate.Suit != suit)
            {
                continue;
            }

            if (Math.Abs(candidate.Rank - tile.Rank) != 1)
            {
                continue;
            }

            if (tileCounts.TryGetValue(candidate, out var count) && count == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTileExhausted(
        Tile tile,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        ownedCounts.TryGetValue(tile, out var ownedCount);
        visibleCounts.TryGetValue(tile, out var visibleCount);
        return 4 - ownedCount - visibleCount <= 0;
    }

    private static bool HasAdjacentOffRankPair(
        IReadOnlyDictionary<Tile, int> tileCounts,
        Tile tile)
    {
        if (!IsSuitedTile(tile))
        {
            return false;
        }

        var suit = tile.Suit;
        var rank = tile.Rank;

        bool HasPair(int candidateRank)
        {
            if (candidateRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            var candidate = new Tile(suit, candidateRank);
            if (!tileCounts.TryGetValue(candidate, out var count) || count < 2)
            {
                return false;
            }

            return !Changsha258Ruleset.IsSmallWinPair(candidate);
        }

        return HasPair(rank - 1) || HasPair(rank + 1);
    }

    private static bool HasGapAdjacentSmallWinSingletonWithBridge(
        Tile pairTile,
        IReadOnlyDictionary<Tile, int> handCounts)
    {
        if (!IsSuitedTile(pairTile) || Changsha258Ruleset.IsSmallWinPair(pairTile))
        {
            return false;
        }

        if (!handCounts.TryGetValue(pairTile, out var pairCount) || pairCount < 2)
        {
            return false;
        }

        var suit = pairTile.Suit;
        var rank = pairTile.Rank;

        bool HasSingletonSmallWinAt(int targetRank)
        {
            if (targetRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            var candidate = new Tile(suit, targetRank);
            return Changsha258Ruleset.IsSmallWinPair(candidate)
                && handCounts.TryGetValue(candidate, out var count)
                && count == 1;
        }

        bool HasBridgeAt(int bridgeRank)
        {
            if (bridgeRank is < Changsha258Ruleset.MinSuitedRank or > Changsha258Ruleset.MaxSuitedRank)
            {
                return false;
            }

            return handCounts.TryGetValue(new Tile(suit, bridgeRank), out var count) && count > 0;
        }

        if (HasSingletonSmallWinAt(rank + 2) && HasBridgeAt(rank + 1))
        {
            return true;
        }

        if (HasSingletonSmallWinAt(rank - 2) && HasBridgeAt(rank - 1))
        {
            return true;
        }

        return false;
    }

    private static int GetChangshaSmallWinPairVisibilityAdjustment(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts,
        MahjongRuleset ruleset,
        bool enforceSmallWinPairBias)
    {
        if (!enforceSmallWinPairBias || ruleset != MahjongRuleset.Changsha258 || visibleCounts.Count == 0 || !Changsha258Ruleset.IsSmallWinPair(tile))
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

    private static int GetOpponentMeldKongAvoidBonus(
        Tile tile,
        IReadOnlyDictionary<Tile, int> kongThreatCounts)
    {
        if (kongThreatCounts.Count == 0)
        {
            return 0;
        }

        kongThreatCounts.TryGetValue(tile, out var meldCount);

        if (meldCount == 3)
        {
            return OpponentKongFeedAvoidBonus;
        }

        if (meldCount >= 6)
        {
            return OpponentKongFeedAvoidBonus * (meldCount / 3);
        }

        return 0;
    }

    private static bool IsKongThreatLive(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        handCounts.TryGetValue(tile, out var handCount);
        visibleCounts.TryGetValue(tile, out var visibleCount);
        if (visibleCount >= 4)
        {
            return false;
        }

        ownedCounts.TryGetValue(tile, out var ownedCount);
        var remainingCopies = 4 - ownedCount - visibleCount;
        return remainingCopies > 0 || handCount > 0;
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

    private static int GetStrictDeadTilePenalty(
        Tile tile,
        IReadOnlyDictionary<Tile, int> handCounts,
        IReadOnlyDictionary<Tile, int> ownedCounts,
        IReadOnlyDictionary<Tile, int> visibleCounts)
    {
        if (visibleCounts.Count == 0)
        {
            return 0;
        }

        handCounts.TryGetValue(tile, out var handCount);
        if (handCount != 1)
        {
            return 0;
        }

        ownedCounts.TryGetValue(tile, out var ownedCount);
        visibleCounts.TryGetValue(tile, out var visibleCount);
        if (4 - ownedCount - visibleCount > 0)
        {
            return 0;
        }

        if (tile.IsHonor)
        {
            return -6;
        }

        if (!IsSuitedTile(tile))
        {
            return -4;
        }

        return HasSequencePotential(tile, handCounts, ownedCounts, visibleCounts)
            ? 0
            : -6;
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
