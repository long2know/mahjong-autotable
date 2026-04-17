using FluentAssertions;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ChangshaWinTypeEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsPlainWin_ForBaselineWinningHand()
    {
        List<Tile> tiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Dots, 1), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5)
        ];

        var hand = ResolveWinningHand(tiles);
        var evaluation = ChangshaWinTypeEvaluator.Evaluate(tiles, hand);

        evaluation.WinTypes.Should().Equal(ChangshaWinType.PlainWin);
        evaluation.IsSmallWin.Should().BeTrue();
        evaluation.IsBigWin.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_DetectsXiaoHuPatterns_WhenInitialDealContextProvided()
    {
        List<Tile> fourHappinessTiles =
        [
            new Tile(TileSuit.Bamboos, 1), new Tile(TileSuit.Bamboos, 1), new Tile(TileSuit.Bamboos, 1), new Tile(TileSuit.Bamboos, 1),
            new Tile(TileSuit.Bamboos, 2), new Tile(TileSuit.Bamboos, 2), new Tile(TileSuit.Bamboos, 2),
            new Tile(TileSuit.Bamboos, 3), new Tile(TileSuit.Bamboos, 3), new Tile(TileSuit.Bamboos, 3),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5)
        ];
        var fourHappinessHand = ResolveWinningHand(fourHappinessTiles);
        var fourHappinessEval = ChangshaWinTypeEvaluator.Evaluate(
            fourHappinessTiles,
            fourHappinessHand,
            new ChangshaWinContext(IsInitialDeal: true));
        fourHappinessEval.WinTypes.Should().Contain(ChangshaWinType.BigFourHappiness);

        List<Tile> banhuTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1),
            new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Bamboos, 6), new Tile(TileSuit.Bamboos, 6), new Tile(TileSuit.Bamboos, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 7)
        ];
        var banhuHand = ResolveWinningHand(banhuTiles);
        var banhuEval = ChangshaWinTypeEvaluator.Evaluate(
            banhuTiles,
            banhuHand,
            new ChangshaWinContext(IsInitialDeal: true));
        banhuEval.WinTypes.Should().Contain(ChangshaWinType.Banhu);

        List<Tile> missingSuitTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5)
        ];
        var missingSuitHand = ResolveWinningHand(missingSuitTiles);
        var missingSuitEval = ChangshaWinTypeEvaluator.Evaluate(
            missingSuitTiles,
            missingSuitHand,
            new ChangshaWinContext(IsInitialDeal: true));
        missingSuitEval.WinTypes.Should().Contain(ChangshaWinType.MissingOneSuit);

        List<Tile> sixSixStraightTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1),
            new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 6),
            new Tile(TileSuit.Bamboos, 2), new Tile(TileSuit.Bamboos, 2)
        ];
        var sixSixHand = ResolveWinningHand(sixSixStraightTiles);
        var sixSixEval = ChangshaWinTypeEvaluator.Evaluate(
            sixSixStraightTiles,
            sixSixHand,
            new ChangshaWinContext(IsInitialDeal: true));
        sixSixEval.WinTypes.Should().Contain(ChangshaWinType.SixSixStraight);
    }

    [Fact]
    public void Evaluate_DetectsAllTwoFiveEight_AndPureSuit()
    {
        List<Tile> allTwoFiveEightTiles =
        [
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 2),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Bamboos, 8), new Tile(TileSuit.Bamboos, 8)
        ];
        var allTwoFiveEightHand = ResolveWinningHand(allTwoFiveEightTiles);
        var allTwoFiveEightEval = ChangshaWinTypeEvaluator.Evaluate(allTwoFiveEightTiles, allTwoFiveEightHand);
        allTwoFiveEightEval.WinTypes.Should().Contain(ChangshaWinType.AllTwoFiveEight);

        List<Tile> pureSuitTiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5)
        ];
        var pureSuitHand = ResolveWinningHand(pureSuitTiles);
        var pureSuitEval = ChangshaWinTypeEvaluator.Evaluate(pureSuitTiles, pureSuitHand);
        pureSuitEval.WinTypes.Should().Contain(ChangshaWinType.PureSuit);
    }

    [Fact]
    public void Evaluate_DetectsSevenPairsAndLuxurySevenPairs_WhenQuadExists()
    {
        List<Tile> tiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 1),
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 2),
            new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 4),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5),
            new Tile(TileSuit.Bamboos, 6), new Tile(TileSuit.Bamboos, 6)
        ];

        var hand = ResolveWinningHand(tiles);
        hand.Pattern.Should().Be(WinningPattern.SevenPairs);

        var evaluation = ChangshaWinTypeEvaluator.Evaluate(tiles, hand);

        evaluation.WinTypes.Should().Contain(ChangshaWinType.SevenPairs);
        evaluation.WinTypes.Should().Contain(ChangshaWinType.LuxurySevenPairs);
        evaluation.IsBigWin.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_UsesContextFlags_ForEventDrivenBigWins()
    {
        List<Tile> tiles =
        [
            new Tile(TileSuit.Characters, 1), new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3),
            new Tile(TileSuit.Characters, 4), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 6),
            new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8), new Tile(TileSuit.Characters, 9),
            new Tile(TileSuit.Dots, 1), new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 3),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5)
        ];

        var hand = ResolveWinningHand(tiles);
        var context = new ChangshaWinContext(
            IsHeavenly: true,
            IsLastTileDraw: true,
            IsWinAfterKong: true,
            IsFullyReliant: true);

        var evaluation = ChangshaWinTypeEvaluator.Evaluate(tiles, hand, context);

        evaluation.WinTypes.Should().Contain(ChangshaWinType.Heavenly);
        evaluation.WinTypes.Should().Contain(ChangshaWinType.LastTileDraw);
        evaluation.WinTypes.Should().Contain(ChangshaWinType.WinAfterKong);
        evaluation.WinTypes.Should().Contain(ChangshaWinType.FullyReliant);
        evaluation.IsBigWin.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_DetectsSanTong_WhenThreeTripletsSameRankAcrossSuitsAtInitialDeal()
    {
        List<Tile> sanTongTiles =
        [
            new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5),
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 2)
        ];

        var hand = ResolveWinningHand(sanTongTiles);
        var evaluation = ChangshaWinTypeEvaluator.Evaluate(
            sanTongTiles,
            hand,
            new ChangshaWinContext(IsInitialDeal: true));

        evaluation.WinTypes.Should().Contain(ChangshaWinType.SanTong);
        evaluation.IsBigWin.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_DoesNotDetectSanTong_WhenNotInitialDeal()
    {
        List<Tile> tiles =
        [
            new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5), new Tile(TileSuit.Bamboos, 5),
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 2)
        ];

        var hand = ResolveWinningHand(tiles);
        var evaluation = ChangshaWinTypeEvaluator.Evaluate(tiles, hand);

        evaluation.WinTypes.Should().NotContain(ChangshaWinType.SanTong);
    }

    [Fact]
    public void Evaluate_DoesNotDetectSanTong_WhenOnlyTwoSameRankTriplets()
    {
        List<Tile> tiles =
        [
            new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5),
            new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5), new Tile(TileSuit.Dots, 5),
            new Tile(TileSuit.Bamboos, 3), new Tile(TileSuit.Bamboos, 3), new Tile(TileSuit.Bamboos, 3),
            new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 4),
            new Tile(TileSuit.Dots, 2), new Tile(TileSuit.Dots, 2)
        ];

        var hand = ResolveWinningHand(tiles);
        var evaluation = ChangshaWinTypeEvaluator.Evaluate(
            tiles,
            hand,
            new ChangshaWinContext(IsInitialDeal: true));

        evaluation.WinTypes.Should().NotContain(ChangshaWinType.SanTong);
    }

    private static WinningHand ResolveWinningHand(IReadOnlyList<Tile> tiles)
    {
        Changsha258Ruleset.TryGetWinningHand(tiles, out var hand).Should().BeTrue();
        hand.Should().NotBeNull();
        return hand!;
    }
}
