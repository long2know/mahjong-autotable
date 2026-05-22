namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Detects winning hands for Changsha Mahjong.
/// Supported patterns:
///   1. Standard: 4 melds + 1 pair — pair must be rank 2, 5, or 8 (258 pair rule)
///   2. Seven Pairs: 7 distinct pairs (no 258 restriction)
///   3. All Pungs: 4 pungs/kongs + pair (no chows)
///   4. Full Flush: entire hand is single suit
///   5. Nine Terminals (Phase H Wave 2 — 九幺): every tile rank 1 or 9, any suit,
///      with a valid mahjong structure (4 sets + pair OR 7 pairs).
/// </summary>
public interface IWinDetector
{
    WinDetectionResult Detect(ChangshaHandState hand, int? winningTileId = null, WinMethod method = WinMethod.SelfDraw);
}

public sealed class WinDetectionResult
{
    public required bool IsWin { get; init; }
    public WinPattern? Pattern { get; init; }
    public ScoreCategory? Category { get; init; }
    public bool IsFullFlush { get; init; }
    public bool IsAllPungs { get; init; }
    public bool IsSevenPairs { get; init; }
}

public sealed class ChangshaWinDetector : IWinDetector
{
    private static readonly HashSet<int> ValidPairRanks = [2, 5, 8];

    public WinDetectionResult Detect(ChangshaHandState hand, int? winningTileId = null, WinMethod method = WinMethod.SelfDraw)
    {
        var concealedTileIds = new List<int>(hand.ConcealedTiles);
        if (winningTileId.HasValue && !concealedTileIds.Contains(winningTileId.Value))
            concealedTileIds.Add(winningTileId.Value);

        var isFlush = CheckFullFlush(concealedTileIds, hand.Melds);
        var isSevenPairs = CheckSevenPairs(concealedTileIds, hand.Melds);
        var isAllPungs = CheckAllPungs(concealedTileIds, hand.Melds);
        var isNineTerminals = CheckNineTerminals(concealedTileIds, hand.Melds);
        var isStandard = CheckStandardWin(concealedTileIds, hand.Melds);

        WinPattern? pattern = null;
        var category = ScoreCategory.SmallWin;

        if (isSevenPairs)
        {
            pattern = WinPattern.SevenPairs;
            category = ScoreCategory.BigWin;
        }

        if (isAllPungs)
        {
            pattern = WinPattern.AllPungs;
            category = ScoreCategory.BigWin;
        }

        if (isFlush)
        {
            if (pattern is null)
                pattern = WinPattern.FullFlush;
            category = ScoreCategory.BigWin;
        }

        if (isNineTerminals)
        {
            if (pattern is null)
                pattern = WinPattern.NineTerminals;
            category = ScoreCategory.BigWin;
        }

        if (pattern is null && isStandard)
        {
            pattern = WinPattern.Standard;
            category = ScoreCategory.SmallWin;
        }

        return new WinDetectionResult
        {
            IsWin = pattern is not null,
            Pattern = pattern,
            Category = category,
            IsFullFlush = isFlush,
            IsAllPungs = isAllPungs,
            IsSevenPairs = isSevenPairs
        };
    }

    public static bool IsWinningWith(ChangshaHandState hand, int tileId)
    {
        var detector = new ChangshaWinDetector();
        var concealedTileIds = new List<int>(hand.ConcealedTiles) { tileId };

        var tempHand = new ChangshaHandState
        {
            SeatIndex = hand.SeatIndex,
            ConcealedTiles = concealedTileIds,
            Melds = hand.Melds
        };

        var result = detector.Detect(tempHand);
        return result.IsWin;
    }

    private static bool CheckStandardWin(List<int> concealedTileIds, List<Meld> melds)
    {
        var counts = BuildLogicalCounts(concealedTileIds);
        var totalConcealed = counts.Sum();
        var meldCount = melds.Count;
        var concealedMeldsNeeded = 4 - meldCount;
        var expectedConcealed = concealedMeldsNeeded * 3 + 2;

        if (totalConcealed != expectedConcealed)
            return false;

        return TryStandardDecomposition(counts, concealedMeldsNeeded);
    }

    private static bool TryStandardDecomposition(int[] counts, int meldsNeeded)
    {
        for (var logical = 0; logical < 27; logical++)
        {
            if (counts[logical] < 2)
                continue;

            var rank = logical % 9 + 1;
            if (!ValidPairRanks.Contains(rank))
                continue;

            counts[logical] -= 2;
            var canForm = CanFormMelds(counts, meldsNeeded);
            counts[logical] += 2;

            if (canForm)
                return true;
        }
        return false;
    }

    private static bool CheckSevenPairs(List<int> concealedTileIds, List<Meld> melds)
    {
        if (melds.Count > 0)
            return false;

        if (concealedTileIds.Count != 14)
            return false;

        var logicalCounts = BuildLogicalCounts(concealedTileIds);
        var pairCount = 0;
        foreach (var count in logicalCounts)
        {
            if (count % 2 != 0)
                return false;
            pairCount += count / 2;
        }

        return pairCount == 7;
    }

    private static bool CheckAllPungs(List<int> concealedTileIds, List<Meld> melds)
    {
        foreach (var meld in melds)
        {
            if (meld.Kind is MeldKind.Chow)
                return false;
        }

        var counts = BuildLogicalCounts(concealedTileIds);
        var totalConcealed = counts.Sum();
        var concealedMeldsNeeded = 4 - melds.Count;
        var expectedConcealed = concealedMeldsNeeded * 3 + 2;

        if (totalConcealed != expectedConcealed)
            return false;

        return TryAllPungs(counts);
    }

    private static bool TryAllPungs(int[] counts)
    {
        for (var logical = 0; logical < 27; logical++)
        {
            if (counts[logical] < 2)
                continue;

            counts[logical] -= 2;
            var canForm = CanFormPungsOnly(counts);
            counts[logical] += 2;

            if (canForm)
                return true;
        }
        return false;
    }

    private static bool CanFormPungsOnly(int[] counts)
    {
        for (var i = 0; i < 27; i++)
        {
            if (counts[i] % 3 != 0)
                return false;
        }
        return true;
    }

    private static bool CheckFullFlush(List<int> concealedTileIds, List<Meld> melds)
    {
        var allTileIds = new List<int>(concealedTileIds);
        foreach (var meld in melds)
            allTileIds.AddRange(meld.TileIds);

        if (allTileIds.Count == 0)
            return false;

        var firstSuit = ChangshaDeckBuilder.GetSuit(allTileIds[0]);
        if (!allTileIds.All(t => ChangshaDeckBuilder.GetSuit(t) == firstSuit))
            return false;

        var counts = BuildLogicalCounts(concealedTileIds);
        var totalConcealed = counts.Sum();

        if (melds.Count == 0 && totalConcealed == 14)
        {
            if (CheckSevenPairsShape(counts))
                return true;
        }

        var concealedMeldsNeeded = 4 - melds.Count;
        var expectedConcealed = concealedMeldsNeeded * 3 + 2;

        if (totalConcealed != expectedConcealed)
            return false;

        return TryDecompositionAnyPair(counts, concealedMeldsNeeded);
    }

    /// <summary>
    /// Phase H Wave 2 — 九幺 (Nine Terminals) check. Returns true iff EVERY tile in the
    /// hand (concealed + meld tiles) is rank 1 or rank 9 of any suit AND the hand forms
    /// a valid mahjong structure: either 4 sets + 1 pair (any-rank pair, NOT the 258
    /// rule — that's Standard's restriction), OR 7 pairs.
    /// Treated as a Big Win at the same precedence tier as FullFlush.
    /// See Ripley's Phase H design memo §2.1 for the rationale (Changsha analog to
    /// ThirteenOrphans, which is structurally impossible in a no-honors deck).
    /// </summary>
    private static bool CheckNineTerminals(List<int> concealedTileIds, List<Meld> melds)
    {
        var allTileIds = new List<int>(concealedTileIds);
        foreach (var meld in melds)
            allTileIds.AddRange(meld.TileIds);

        if (allTileIds.Count == 0)
            return false;

        // Every tile must be rank 1 or rank 9.
        if (!allTileIds.All(t =>
        {
            var rank = ChangshaDeckBuilder.GetRank(t);
            return rank == 1 || rank == 9;
        }))
        {
            return false;
        }

        // Validate any-pair meld decomposition (4 sets + pair) OR a 14-tile seven-pairs shape.
        // Tiles already pre-filtered to rank 1 or 9, so the recursive search inside
        // CanFormMelds is only walking 6 logical positions per suit's terminals — very cheap.
        var counts = BuildLogicalCounts(concealedTileIds);
        var totalConcealed = counts.Sum();

        if (melds.Count == 0 && totalConcealed == 14 && CheckSevenPairsShape(counts))
            return true;

        var concealedMeldsNeeded = 4 - melds.Count;
        var expectedConcealed = concealedMeldsNeeded * 3 + 2;
        if (totalConcealed != expectedConcealed)
            return false;

        return TryDecompositionAnyPair(counts, concealedMeldsNeeded);
    }

    private static bool CheckSevenPairsShape(int[] counts)
    {
        var pairs = 0;
        foreach (var c in counts)
        {
            if (c % 2 != 0) return false;
            pairs += c / 2;
        }
        return pairs == 7;
    }

    private static bool TryDecompositionAnyPair(int[] counts, int meldsNeeded)
    {
        for (var logical = 0; logical < 27; logical++)
        {
            if (counts[logical] < 2)
                continue;

            counts[logical] -= 2;
            var canForm = CanFormMelds(counts, meldsNeeded);
            counts[logical] += 2;

            if (canForm)
                return true;
        }
        return false;
    }

    private static bool CanFormMelds(int[] counts, int needed)
    {
        if (needed == 0)
            return counts.All(c => c == 0);

        var logical = Array.FindIndex(counts, c => c > 0);
        if (logical < 0)
            return needed == 0;

        if (counts[logical] >= 3)
        {
            counts[logical] -= 3;
            if (CanFormMelds(counts, needed - 1))
            {
                counts[logical] += 3;
                return true;
            }
            counts[logical] += 3;
        }

        if (logical < 27 && logical % 9 <= 6
            && counts[logical + 1] > 0 && counts[logical + 2] > 0)
        {
            counts[logical]--;
            counts[logical + 1]--;
            counts[logical + 2]--;
            if (CanFormMelds(counts, needed - 1))
            {
                counts[logical]++;
                counts[logical + 1]++;
                counts[logical + 2]++;
                return true;
            }
            counts[logical]++;
            counts[logical + 1]++;
            counts[logical + 2]++;
        }

        return false;
    }

    private static int[] BuildLogicalCounts(List<int> tileIds)
    {
        var counts = new int[27];
        foreach (var tileId in tileIds)
        {
            var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
            if (logical >= 0 && logical < 27)
                counts[logical]++;
        }
        return counts;
    }
}
