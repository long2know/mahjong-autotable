using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public sealed record Meld(MeldType Type, IReadOnlyList<Tile> Tiles);

public sealed record WinningHand(IReadOnlyList<Meld> Melds, Tile Pair)
{
    public int BasicScore => Melds.Sum(meld => meld.Type switch
    {
        MeldType.Pung => 2,
        MeldType.Kong => 8,
        _ => 0
    });
}
