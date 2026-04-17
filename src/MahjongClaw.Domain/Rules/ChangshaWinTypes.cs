namespace MahjongClaw.Domain.Rules;

public enum ChangshaWinType
{
    PlainWin,
    BigFourHappiness,
    Banhu,
    MissingOneSuit,
    SixSixStraight,
    Heavenly,
    Earthly,
    AllPungs,
    AllTwoFiveEight,
    PureSuit,
    LastTileDraw,
    LastTileDiscard,
    SevenPairs,
    LuxurySevenPairs,
    WinAfterKong,
    RobbingKong,
    KongOnCannon,
    FullyReliant
}

public sealed record ChangshaWinEvaluation(IReadOnlyList<ChangshaWinType> WinTypes, bool IsBigWin, bool IsSmallWin);
