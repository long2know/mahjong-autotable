namespace MahjongClaw.Domain.Tiles;

public sealed record Tile
{
    public TileSuit Suit { get; }
    public int Rank { get; }
    public Wind? Wind { get; }
    public Dragon? Dragon { get; }

    public bool IsHonor => Suit is TileSuit.Winds or TileSuit.Dragons;
    public bool IsFlower => Suit is TileSuit.Flowers or TileSuit.Seasons;

    public Tile(TileSuit suit, int rank, Wind? wind = null, Dragon? dragon = null)
    {
        Suit = suit;
        Rank = rank;
        Wind = wind;
        Dragon = dragon;

        switch (Suit)
        {
            case TileSuit.Characters:
            case TileSuit.Dots:
            case TileSuit.Bamboos:
                if (Rank < 1 || Rank > 9)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank), "Suited tile rank must be between 1 and 9.");
                }

                if (Wind is not null || Dragon is not null)
                {
                    throw new ArgumentException("Suited tiles cannot include wind or dragon values.");
                }

                break;

            case TileSuit.Winds:
                if (Rank != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank), "Wind tiles must use rank 0.");
                }

                if (Wind is null || Dragon is not null)
                {
                    throw new ArgumentException("Wind tiles require a wind value and no dragon value.");
                }

                break;

            case TileSuit.Dragons:
                if (Rank != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank), "Dragon tiles must use rank 0.");
                }

                if (Dragon is null || Wind is not null)
                {
                    throw new ArgumentException("Dragon tiles require a dragon value and no wind value.");
                }

                break;

            case TileSuit.Flowers:
            case TileSuit.Seasons:
                if (Rank < 1 || Rank > 4)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank), "Flower/Season tile rank must be between 1 and 4.");
                }

                if (Wind is not null || Dragon is not null)
                {
                    throw new ArgumentException("Flower/Season tiles cannot include wind or dragon values.");
                }

                break;
        }
    }
}
