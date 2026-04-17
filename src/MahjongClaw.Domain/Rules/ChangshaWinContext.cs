namespace MahjongClaw.Domain.Rules;

public sealed record ChangshaWinContext(
    bool IsInitialDeal = false,
    bool IsHeavenly = false,
    bool IsEarthly = false,
    bool IsLastTileDraw = false,
    bool IsLastTileDiscard = false,
    bool IsWinAfterKong = false,
    bool IsRobbingKong = false,
    bool IsKongOnCannon = false,
    bool IsFullyReliant = false);
