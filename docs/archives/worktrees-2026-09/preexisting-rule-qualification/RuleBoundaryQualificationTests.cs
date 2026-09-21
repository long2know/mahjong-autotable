using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class RuleBoundaryQualificationTests
{
    public static IEnumerable<object[]> PairCases()
    {
        foreach (var suit in Enum.GetValues<Suit>())
        for (var rank = 1; rank <= 9; rank++)
            yield return new object[] { suit, rank };
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "Standard258AllSuits")]
    [MemberData(nameof(PairCases))]
    public void StandardPairRule_AcceptsOnly258_InEverySuit(Suit suit, int rank)
    {
        var result = new ChangshaWinDetector().Detect(StandardHand(suit, rank));
        var accepted = rank is 2 or 5 or 8;

        Assert.Equal(accepted, result.IsWin);
        if (accepted)
        {
            Assert.Equal(WinPattern.Standard, result.Pattern);
            Assert.Equal(ScoreCategory.SmallWin, result.Category);
        }
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "AllPungsMeldKinds")]
    [InlineData(MeldKind.Pung, true)]
    [InlineData(MeldKind.ExposedKong, true)]
    [InlineData(MeldKind.ConcealedKong, true)]
    [InlineData(MeldKind.AddedKong, true)]
    [InlineData(MeldKind.Chow, false)]
    public void AllPungs_AcceptsPungsAndEveryKong_ButNotChow(MeldKind kind, bool accepted)
    {
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9),
            (Suit.Tong, 4), (Suit.Tong, 4), (Suit.Tong, 4),
            (Suit.Tiao, 3), (Suit.Tiao, 3));
        hand.Melds.Add(new Meld
        {
            Kind = kind,
            TileIds = kind switch
            {
                MeldKind.Chow => [84, 88, 92],
                MeldKind.Pung => [96, 97, 98],
                _ => [96, 97, 98, 99]
            }
        });

        var result = new ChangshaWinDetector().Detect(hand);

        Assert.Equal(accepted, result.IsAllPungs);
        Assert.Equal(accepted, result.IsWin);
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "SevenPairsBoundaries")]
    [InlineData("thirteen")]
    [InlineData("open-meld")]
    [InlineData("odd-counts")]
    public void SevenPairs_RejectsWrongCountOpenMeldOrUnpairedTile(string invalid)
    {
        var hand = SevenPairs();
        Assert.True(new ChangshaWinDetector().Detect(hand).IsSevenPairs);
        switch (invalid)
        {
            case "thirteen":
                hand.ConcealedTiles.RemoveAt(0);
                break;
            case "open-meld":
                hand.Melds.Add(new Meld { Kind = MeldKind.Chow, TileIds = [14, 16, 20] });
                break;
            case "odd-counts":
                hand.ConcealedTiles[^1] = Tid(Suit.Tiao, 8);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalid));
        }

        Assert.False(new ChangshaWinDetector().Detect(hand).IsSevenPairs);
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "NineTerminalsBoundaries")]
    [InlineData("missing-terminal")]
    [InlineData("non-terminal")]
    [InlineData("thirteen")]
    public void NineTerminals_RequiresFourteenTilesAllTerminalsAndAllSixKinds(string invalid)
    {
        var hand = NineTerminals();
        Assert.Contains(WinPattern.NineTerminals, new ChangshaWinDetector().Detect(hand).AllPatterns);
        switch (invalid)
        {
            case "missing-terminal":
                hand.ConcealedTiles[^1] = Tid(Suit.Tiao, 1, 2);
                break;
            case "non-terminal":
                hand.ConcealedTiles[^1] = Tid(Suit.Tiao, 5);
                break;
            case "thirteen":
                hand.ConcealedTiles.RemoveAt(0);
                Assert.Equal(6, hand.ConcealedTiles.Select(t => t / 4).Distinct().Count());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalid));
        }

        Assert.DoesNotContain(WinPattern.NineTerminals, new ChangshaWinDetector().Detect(hand).AllPatterns);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "FullFlushStructure")]
    public void FullFlush_RequiresWinningStructure_NotJustOneSuit()
    {
        var result = new ChangshaWinDetector().Detect(InvalidSingleSuitHand());

        Assert.False(result.IsFullFlush);
        Assert.False(result.IsWin);
    }

    public static IEnumerable<object[]> ContextCases()
    {
        foreach (var pattern in new[]
        {
            WinPattern.HeavenlyHand, WinPattern.EarthlyHand, WinPattern.LastTileFromWall,
            WinPattern.LastDiscardCatch, WinPattern.KongReplacementWin
        })
        foreach (var invalidShape in new[] { "non-258-pair", "no-decomposition" })
            yield return new object[] { pattern, invalidShape };
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "ContextRequiresValidShape")]
    [MemberData(nameof(ContextCases))]
    public void ContextualFlags_CannotTurnAnInvalidShapeIntoHu(WinPattern pattern, string invalidShape)
    {
        var hand = invalidShape == "non-258-pair"
            ? StandardHand(Suit.Tong, 3)
            : InvalidSingleSuitHand();
        var detector = new ChangshaWinDetector();
        Assert.False(detector.Detect(hand).IsWin);
        var context = new WinContext
        {
            IsHeavenlyHand = pattern == WinPattern.HeavenlyHand,
            IsEarthlyHand = pattern == WinPattern.EarthlyHand,
            IsLastTileFromWall = pattern == WinPattern.LastTileFromWall,
            IsLastDiscardCatch = pattern == WinPattern.LastDiscardCatch,
            IsKongReplacementWin = pattern == WinPattern.KongReplacementWin
        };

        var result = detector.Detect(hand, context: context);

        Assert.False(result.IsWin);
        Assert.Empty(result.AllPatterns);
    }

    [Fact, Trait("Category", "RulesQualificationGap"), Trait("Rule", "SevenPairsQuadInterpretation")]
    public void CurrentSevenPairsShape_CountsAQuadAsTwoOrdinaryPairs()
    {
        var hand = HandOf(0,
            (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
            (Suit.Wan, 4), (Suit.Wan, 4),
            (Suit.Tong, 1), (Suit.Tong, 1),
            (Suit.Tong, 3), (Suit.Tong, 3),
            (Suit.Tiao, 3), (Suit.Tiao, 3),
            (Suit.Tiao, 9), (Suit.Tiao, 9));

        var result = new ChangshaWinDetector().Detect(hand);

        Assert.True(result.IsSevenPairs);
        Assert.Contains(WinPattern.SevenPairs, result.AllPatterns);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "SelfDrawProvenance")]
    public void PungWithoutAnOwnDraw_CannotBypassPassHuAsSelfDraw()
    {
        var state = NewGameDealtTo(42);
        var wait = new[]
        {
            0, 1,
            12, 16, 20,
            36, 40, 44,
            84, 88, 92,
            52, 53
        };
        PlaceTiles(state, 1, wait);
        PlaceTiles(state, 0, [2]);
        state.MissedWinSeats.Add(1);

        ChangshaGameStateMachine.Discard(state, 0, 2);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Pung);
        Assert.DoesNotContain(state.ClaimWindow.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Hu);
        var wallBefore = state.Wall.ToArray();
        ChangshaGameStateMachine.ResolveClaim(state, 1, TableClaimType.Pung);
        Assert.Equal(wallBefore, state.Wall);
        Assert.Contains(1, state.MissedWinSeats);
        Assert.True(new ChangshaWinDetector().Detect(state.Hands[1]).IsWin);

        Assert.Throws<InvalidOperationException>(() =>
            ChangshaGameStateMachine.DeclareSelfDrawWin(state, 1));
    }

    private static ChangshaHandState StandardHand(Suit pairSuit, int pairRank) => HandOf(0,
        (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
        (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
        (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
        (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
        (pairSuit, pairRank), (pairSuit, pairRank));

    private static ChangshaHandState SevenPairs() => HandOf(0,
        (Suit.Wan, 1), (Suit.Wan, 1),
        (Suit.Wan, 3), (Suit.Wan, 3),
        (Suit.Wan, 4), (Suit.Wan, 4),
        (Suit.Tong, 6), (Suit.Tong, 6),
        (Suit.Tong, 7), (Suit.Tong, 7),
        (Suit.Tiao, 1), (Suit.Tiao, 1),
        (Suit.Tiao, 9), (Suit.Tiao, 9));

    private static ChangshaHandState NineTerminals() => HandOf(0,
        (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
        (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9),
        (Suit.Tong, 1), (Suit.Tong, 1), (Suit.Tong, 1),
        (Suit.Tong, 9), (Suit.Tong, 9),
        (Suit.Tiao, 1), (Suit.Tiao, 1), (Suit.Tiao, 9));

    private static ChangshaHandState InvalidSingleSuitHand() => HandOf(0,
        (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1), (Suit.Wan, 1),
        (Suit.Wan, 4), (Suit.Wan, 4), (Suit.Wan, 4), (Suit.Wan, 4),
        (Suit.Wan, 7), (Suit.Wan, 7), (Suit.Wan, 7),
        (Suit.Wan, 9), (Suit.Wan, 9), (Suit.Wan, 9));

    private static void PlaceTiles(ChangshaGameState state, int seat, int[] tiles)
    {
        var target = state.Hands[seat].ConcealedTiles;
        for (var index = 0; index < tiles.Length; index++)
        {
            var tile = tiles[index];
            if (target[index] == tile) continue;
            var owner = state.Hands.FirstOrDefault(h => h.ConcealedTiles.Contains(tile));
            if (owner is not null)
            {
                var source = owner.ConcealedTiles;
                var sourceIndex = source.IndexOf(tile);
                (target[index], source[sourceIndex]) = (source[sourceIndex], target[index]);
            }
            else
            {
                var sourceIndex = state.Wall.IndexOf(tile);
                Assert.InRange(sourceIndex, 0, state.Wall.Count - 1);
                (target[index], state.Wall[sourceIndex]) = (state.Wall[sourceIndex], target[index]);
            }
        }
        Assert.Equal(Enumerable.Range(0, 108),
            state.Wall.Concat(state.Hands.SelectMany(h => h.ConcealedTiles)).OrderBy(t => t));
    }
}
