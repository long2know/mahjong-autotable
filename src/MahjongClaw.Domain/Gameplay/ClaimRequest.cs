using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed record ClaimRequest(
    Guid PlayerId,
    ClaimType ClaimType,
    IReadOnlyList<Tile>? MeldTiles = null,
    ChangshaWinContext? WinContext = null)
{
    public int Priority => ClaimType switch
    {
        ClaimType.Win => 100,
        ClaimType.Pung => 50,
        ClaimType.ExposedKong => 50,
        ClaimType.Chow => 10,
        ClaimType.Pass => 0,
        _ => 0
    };
}
