using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class ChangshaWinTypeEvaluator
{
    private static readonly IReadOnlySet<ChangshaWinType> BigWinTypes = new HashSet<ChangshaWinType>
    {
        ChangshaWinType.Heavenly,
        ChangshaWinType.Earthly,
        ChangshaWinType.AllPungs,
        ChangshaWinType.AllTwoFiveEight,
        ChangshaWinType.PureSuit,
        ChangshaWinType.LastTileDraw,
        ChangshaWinType.LastTileDiscard,
        ChangshaWinType.SevenPairs,
        ChangshaWinType.LuxurySevenPairs,
        ChangshaWinType.WinAfterKong,
        ChangshaWinType.RobbingKong,
        ChangshaWinType.KongOnCannon,
        ChangshaWinType.FullyReliant
    };

    public static ChangshaWinEvaluation Evaluate(
        IReadOnlyList<Tile> allTiles,
        WinningHand winningHand,
        ChangshaWinContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(allTiles);
        ArgumentNullException.ThrowIfNull(winningHand);

        if (allTiles.Any(static tile => tile is null))
        {
            throw new ArgumentException("Tiles cannot contain null values.", nameof(allTiles));
        }

        context ??= new ChangshaWinContext();

        var winTypes = new HashSet<ChangshaWinType>();

        if (context.IsInitialDeal)
        {
            if (HasAnyQuad(allTiles))
            {
                winTypes.Add(ChangshaWinType.BigFourHappiness);
            }

            if (IsBanhu(allTiles))
            {
                winTypes.Add(ChangshaWinType.Banhu);
            }

            if (IsMissingOneSuit(allTiles))
            {
                winTypes.Add(ChangshaWinType.MissingOneSuit);
            }

            if (HasAtLeastTwoTriplets(allTiles))
            {
                winTypes.Add(ChangshaWinType.SixSixStraight);
            }
        }

        if (winningHand.Pattern == WinningPattern.AllPungs)
        {
            winTypes.Add(ChangshaWinType.AllPungs);
        }

        if (IsAllTwoFiveEight(allTiles))
        {
            winTypes.Add(ChangshaWinType.AllTwoFiveEight);
        }

        if (IsPureSuit(allTiles))
        {
            winTypes.Add(ChangshaWinType.PureSuit);
        }

        if (winningHand.Pattern == WinningPattern.SevenPairs)
        {
            winTypes.Add(ChangshaWinType.SevenPairs);

            if (HasAnyQuad(allTiles))
            {
                winTypes.Add(ChangshaWinType.LuxurySevenPairs);
            }
        }

        if (context.IsHeavenly)
        {
            winTypes.Add(ChangshaWinType.Heavenly);
        }

        if (context.IsEarthly)
        {
            winTypes.Add(ChangshaWinType.Earthly);
        }

        if (context.IsLastTileDraw)
        {
            winTypes.Add(ChangshaWinType.LastTileDraw);
        }

        if (context.IsLastTileDiscard)
        {
            winTypes.Add(ChangshaWinType.LastTileDiscard);
        }

        if (context.IsWinAfterKong)
        {
            winTypes.Add(ChangshaWinType.WinAfterKong);
        }

        if (context.IsRobbingKong)
        {
            winTypes.Add(ChangshaWinType.RobbingKong);
        }

        if (context.IsKongOnCannon)
        {
            winTypes.Add(ChangshaWinType.KongOnCannon);
        }

        if (context.IsFullyReliant)
        {
            winTypes.Add(ChangshaWinType.FullyReliant);
        }

        if (winTypes.Count == 0)
        {
            winTypes.Add(ChangshaWinType.PlainWin);
        }

        var orderedTypes = winTypes
            .OrderBy(GetSortOrder)
            .ThenBy(static type => type.ToString(), StringComparer.Ordinal)
            .ToList();

        var isBigWin = orderedTypes.Any(type => BigWinTypes.Contains(type));
        var isSmallWin = orderedTypes.Any(type => !BigWinTypes.Contains(type));

        return new ChangshaWinEvaluation(orderedTypes, isBigWin, isSmallWin);
    }

    private static int GetSortOrder(ChangshaWinType winType)
        => winType switch
        {
            ChangshaWinType.Heavenly => 10,
            ChangshaWinType.Earthly => 20,
            ChangshaWinType.AllPungs => 30,
            ChangshaWinType.AllTwoFiveEight => 40,
            ChangshaWinType.PureSuit => 50,
            ChangshaWinType.LastTileDraw => 60,
            ChangshaWinType.LastTileDiscard => 70,
            ChangshaWinType.SevenPairs => 80,
            ChangshaWinType.LuxurySevenPairs => 90,
            ChangshaWinType.WinAfterKong => 100,
            ChangshaWinType.RobbingKong => 110,
            ChangshaWinType.KongOnCannon => 120,
            ChangshaWinType.FullyReliant => 130,
            ChangshaWinType.BigFourHappiness => 140,
            ChangshaWinType.Banhu => 150,
            ChangshaWinType.MissingOneSuit => 160,
            ChangshaWinType.SixSixStraight => 170,
            _ => 999
        };

    private static bool HasAnyQuad(IReadOnlyList<Tile> tiles)
        => tiles
            .GroupBy(static tile => tile)
            .Any(group => group.Count() >= 4);

    private static bool IsBanhu(IReadOnlyList<Tile> tiles)
        => tiles.All(static tile => !Changsha258Ruleset.SmallWinPairRanks.Contains(tile.Rank));

    private static bool IsMissingOneSuit(IReadOnlyList<Tile> tiles)
    {
        var suitedTiles = tiles.Where(static tile => Changsha258Ruleset.AllowedSuits.Contains(tile.Suit)).ToList();
        if (suitedTiles.Count == 0)
        {
            return false;
        }

        var suitedPresence = suitedTiles
            .Select(static tile => tile.Suit)
            .Distinct()
            .Count();

        return suitedPresence <= 2;
    }

    private static bool HasAtLeastTwoTriplets(IReadOnlyList<Tile> tiles)
        => tiles
            .GroupBy(static tile => tile)
            .Count(group => group.Count() >= 3) >= 2;

    private static bool IsAllTwoFiveEight(IReadOnlyList<Tile> tiles)
        => tiles.All(static tile => Changsha258Ruleset.SmallWinPairRanks.Contains(tile.Rank));

    private static bool IsPureSuit(IReadOnlyList<Tile> tiles)
    {
        var suitedTiles = tiles.Where(static tile => Changsha258Ruleset.AllowedSuits.Contains(tile.Suit)).ToList();
        if (suitedTiles.Count == 0)
        {
            return false;
        }

        return suitedTiles.All(tile => tile.Suit == suitedTiles[0].Suit);
    }
}
