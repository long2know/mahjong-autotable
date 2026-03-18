using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public static class MeldValidator
{
    public static bool IsValidMeld(IReadOnlyList<Tile> tiles, MeldType type)
    {
        return type switch
        {
            MeldType.Chow => IsValidChow(tiles),
            MeldType.Pung => IsValidPung(tiles),
            MeldType.Kong => IsValidKong(tiles),
            _ => false
        };
    }

    public static bool IsValidChow(IReadOnlyList<Tile> tiles)
    {
        if (tiles.Count != 3)
        {
            return false;
        }

        if (tiles.Any(tile => tile.IsHonor || tile.IsFlower))
        {
            return false;
        }

        var suit = tiles[0].Suit;
        if (tiles.Any(tile => tile.Suit != suit))
        {
            return false;
        }

        var ordered = tiles.Select(tile => tile.Rank).OrderBy(rank => rank).ToArray();
        return ordered[0] + 1 == ordered[1] && ordered[1] + 1 == ordered[2];
    }

    public static bool IsValidPung(IReadOnlyList<Tile> tiles)
    {
        if (tiles.Count != 3)
        {
            return false;
        }

        if (tiles.Any(static tile => tile.IsFlower))
        {
            return false;
        }

        return tiles.All(tile => tile == tiles[0]);
    }

    public static bool IsValidKong(IReadOnlyList<Tile> tiles)
    {
        if (tiles.Count != 4)
        {
            return false;
        }

        if (tiles.Any(static tile => tile.IsFlower))
        {
            return false;
        }

        return tiles.All(tile => tile == tiles[0]);
    }
}
