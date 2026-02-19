namespace MahjongClaw.Domain.Tiles;

public sealed record Tile(
    TileSuit Suit,
    int Rank,
    Wind? Wind = null,
    Dragon? Dragon = null)
{
    public bool IsHonor => Suit is TileSuit.Winds or TileSuit.Dragons;
    public bool IsFlower => Suit is TileSuit.Flowers or TileSuit.Seasons;
}
