namespace Mahjong.Autotable.Api.Autotable;

/// <summary>
/// Translates Changsha tile positions to upstream pwmarcz/autotable slot names.
/// Upstream's renderer is driven entirely by these slot strings — a tile at
/// <c>hand.0@2</c> renders at seat 2's hand slot index 0; move it to
/// <c>discard.0.0@2</c> and the renderer animates the move.
///
/// Slot reference (see upstream <c>src/setup-slots.ts</c>):
/// <list type="bullet">
///   <item><c>hand.{i}@{s}</c> — 14 slots per seat (i ∈ [0,13])</item>
///   <item><c>wall.{c}.{l}@{s}</c> — 19 cols × 2 layers per seat (152 total upstream slots)</item>
///   <item><c>discard.{r}.{c}@{s}</c> — 3 rows × 6 cols radial trays per seat</item>
///   <item><c>meld.{m}.{t}@{s}</c> — 4 melds × 4 tiles per seat</item>
/// </list>
///
/// <para><b>Changsha wall split (Phase 5a Default #6 — locked):</b></para>
/// <para>108 tiles split 14/14/13/13 across the four walls.</para>
/// <list type="bullet">
///   <item>Seats 0, 1 → 14 stacks × 2 tiers = 28 tiles each</item>
///   <item>Seats 2, 3 → 13 stacks × 2 tiers = 26 tiles each</item>
///   <item>Total: 28 + 28 + 26 + 26 = <b>108</b></item>
/// </list>
///
/// <para>Within a seat, slots are populated col-major, layer-minor (col 0 layer 0,
/// col 0 layer 1, col 1 layer 0, …) so visual "stacks" match the canonical wall.</para>
///
/// <para><b>Upstream typeIndex:</b> <c>changshaTileId / 4</c> per spike §5.1.
/// Changsha uses tiles 0..107 (3 suits × 9 ranks × 4 copies); upstream's atlas
/// row 0 col 0 is 1-wan; suits man/pin/sou occupy typeIndex 0..26. Winds /
/// dragons / red-fives at 27..36 are unused in Changsha v1.</para>
/// </summary>
public static class AutotableSlotMap
{
    /// <summary>Total tiles in the Changsha deck.</summary>
    public const int TotalTiles = 108;

    /// <summary>Upstream stacks per seat for the canonical 14/14/13/13 Changsha wall.</summary>
    /// <param name="seat">0..3</param>
    /// <returns>14 for seats 0,1; 13 for seats 2,3.</returns>
    public static int WallStackCount(int seat)
    {
        if (seat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "seat must be in [0,3].");
        return seat is 0 or 1 ? 14 : 13;
    }

    /// <summary>Total wall slot capacity for a seat (2 tiers per stack).</summary>
    public static int WallTileCapacity(int seat) => WallStackCount(seat) * 2;

    /// <summary>Slot name for a wall tile: <c>wall.{col}.{layer}@{seat}</c>.</summary>
    public static string WallSlot(int seat, int col, int layer)
    {
        if (seat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "seat must be in [0,3].");
        if (col < 0 || col >= WallStackCount(seat))
            throw new ArgumentOutOfRangeException(nameof(col), col, $"col must be in [0,{WallStackCount(seat) - 1}] for seat {seat}.");
        if (layer is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "layer must be 0 or 1.");
        return $"wall.{col}.{layer}@{seat}";
    }

    /// <summary>Slot name for a concealed-hand tile: <c>hand.{handIdx}@{seat}</c>.</summary>
    public static string HandSlot(int seat, int handIdx)
    {
        if (seat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "seat must be in [0,3].");
        if (handIdx is < 0 or > 13)
            throw new ArgumentOutOfRangeException(nameof(handIdx), handIdx, "handIdx must be in [0,13].");
        return $"hand.{handIdx}@{seat}";
    }

    /// <summary>Slot name for a per-seat radial discard tile: <c>discard.{row}.{col}@{seat}</c>.</summary>
    public static string DiscardSlot(int seat, int row, int col)
    {
        if (seat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "seat must be in [0,3].");
        if (row is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(row), row, "row must be in [0,2].");
        if (col is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(col), col, "col must be in [0,5].");
        return $"discard.{row}.{col}@{seat}";
    }

    /// <summary>Slot name for a meld tile: <c>meld.{meldIdx}.{tileIdx}@{seat}</c>.</summary>
    public static string MeldSlot(int seat, int meldIdx, int tileIdx)
    {
        if (seat is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "seat must be in [0,3].");
        if (meldIdx is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(meldIdx), meldIdx, "meldIdx must be in [0,3].");
        if (tileIdx is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(tileIdx), tileIdx, "tileIdx must be in [0,3].");
        return $"meld.{meldIdx}.{tileIdx}@{seat}";
    }

    /// <summary>
    /// Maps a Changsha tile id (0..107) to upstream's atlas <c>typeIndex</c>.
    /// Per spike §5.1: Changsha and upstream agree on suit / rank ordering.
    /// </summary>
    public static int UpstreamTypeIndex(int changshaTileId)
    {
        if (changshaTileId < 0 || changshaTileId >= TotalTiles)
            throw new ArgumentOutOfRangeException(nameof(changshaTileId), changshaTileId, $"tileId must be in [0,{TotalTiles - 1}].");
        return changshaTileId / 4;
    }

    /// <summary>
    /// Enumerates every (seat, col, layer) wall slot in canonical placement order
    /// (seats 0..3, cols ascending, layer 0 then layer 1). Total = 108 tuples
    /// for the 14/14/13/13 split.
    /// </summary>
    public static IEnumerable<(int Seat, int Col, int Layer)> EnumerateWallSlotsInOrder()
    {
        for (var seat = 0; seat < 4; seat++)
        {
            var stacks = WallStackCount(seat);
            for (var col = 0; col < stacks; col++)
            {
                for (var layer = 0; layer < 2; layer++)
                {
                    yield return (seat, col, layer);
                }
            }
        }
    }
}
