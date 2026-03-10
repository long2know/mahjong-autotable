using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public sealed record Meld(MeldType Type, IReadOnlyList<Tile> Tiles);

public enum WinningPattern
{
    Standard,
    SevenPairs,
    ThirteenOrphans,
    AllHonors,
    AllTerminals,
    AllTerminalsAndHonors,
    AllGreen,
    BigThreeDragons
}

public sealed record WinningHand(IReadOnlyList<Meld> Melds, Tile Pair, WinningPattern Pattern = WinningPattern.Standard)
{
    public int BasicScore => Pattern switch
    {
        WinningPattern.SevenPairs => 24,
        WinningPattern.ThirteenOrphans => 88,
        WinningPattern.AllHonors => 64,
        WinningPattern.AllTerminals => 64,
        WinningPattern.AllTerminalsAndHonors => 64,
        WinningPattern.AllGreen => 88,
        WinningPattern.BigThreeDragons => 88,
        _ => Melds.Sum(meld => meld.Type switch
        {
            MeldType.Pung => 2,
            MeldType.Kong => 8,
            _ => 0
        })
    };
}
