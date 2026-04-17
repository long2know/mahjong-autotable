namespace MahjongClaw.Domain.Gameplay;

public enum KongKind
{
    Concealed,
    Exposed,
    Promoted
}

public static class KongKindParser
{
    public static KongKind Parse(string kindString)
    {
        return kindString switch
        {
            "ConcealedKong" => KongKind.Concealed,
            "ExposedKong" => KongKind.Exposed,
            "PromotedKong" => KongKind.Promoted,
            _ => throw new ArgumentException($"Unknown kong kind: {kindString}", nameof(kindString))
        };
    }

    public static string ToMeldTypeString(KongKind kind)
    {
        return kind switch
        {
            KongKind.Concealed => "ConcealedKong",
            KongKind.Exposed => "ExposedKong",
            KongKind.Promoted => "PromotedKong",
            _ => throw new ArgumentException($"Unknown kong kind: {kind}", nameof(kind))
        };
    }

    public static bool TryParse(string kindString, out KongKind kind)
    {
        switch (kindString)
        {
            case "ConcealedKong":
                kind = KongKind.Concealed;
                return true;
            case "ExposedKong":
                kind = KongKind.Exposed;
                return true;
            case "PromotedKong":
                kind = KongKind.Promoted;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
