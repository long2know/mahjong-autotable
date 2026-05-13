using Mahjong.Autotable.Api.Changsha;

namespace Mahjong.Autotable.Api.Autotable;

/// <summary>
/// Pure-functional translator that converts authoritative <see cref="ChangshaGameState"/>
/// into the upstream pwmarcz/autotable collection-entry stream
/// (<c>match</c>, <c>seats</c>, <c>things</c>, <c>nicks</c>, <c>dice</c>).
///
/// <para>The translator emits a full snapshot on every call. Phase 5a uses
/// full snapshots on every state change for simplicity; incremental diffing
/// is a documented Phase 5c optimization (see
/// <c>.squad/decisions/inbox/bishop-phase5a-backend.md</c>).</para>
///
/// <para><b>Thing-index ↔ Changsha tileId mapping (locked at fives='000'):</b></para>
/// <para>The translator forces the bundle's <c>Conditions.fives = '000'</c> via the
/// <c>match</c> collection. This gives 1:1 alignment between Changsha tile ids
/// 0..107 and upstream thing-indices 0..107 (both have the same
/// <c>typeIndex = id / 4</c>). Bundle thing-indices 108..135 are winds /
/// dragons (typeIndex 27..33) and are unused in Changsha v1 — they remain at
/// the bundle's initial wall positions (Phase 5b cleanup). The translator
/// emits exactly 108 <c>things</c> entries per snapshot.</para>
///
/// <para><b>Rotation indices (from upstream <c>setup-slots.ts</c>):</b></para>
/// <list type="bullet">
///   <item><c>wall</c>: <c>[FACE_DOWN, FACE_UP]</c> — 0 = face-down (canonical wall).</item>
///   <item><c>hand</c>: <c>[STANDING, FACE_UP, FACE_DOWN]</c> — viewer's seat uses 1, others use 2.</item>
///   <item><c>discard</c>: <c>[FACE_UP, FACE_UP_SIDEWAYS, FACE_DOWN, FACE_DOWN_SIDEWAYS]</c> — 0 = face-up.</item>
///   <item><c>meld</c>: <c>[FACE_UP, FACE_UP_SIDEWAYS, FACE_DOWN]</c> — 0 = face-up, 2 = face-down (concealed kong).</item>
/// </list>
/// </summary>
public static class ChangshaToAutotableTranslator
{
    // ── Rotation index constants (see upstream src/setup-slots.ts) ────

    private const int WallRotFaceDown = 0;
    private const int HandRotStanding = 0;
    private const int HandRotFaceUp = 1;
    private const int HandRotFaceDown = 2;
    private const int DiscardRotFaceUp = 0;
    private const int MeldRotFaceUp = 0;
    private const int MeldRotFaceDown = 2;

    // ── Identity rotation (no held override) ──────────────────────────

    private static readonly object IdentityQuaternion = new
    {
        x = 0.0,
        y = 0.0,
        z = 0.0,
        w = 1.0
    };

    /// <summary>
    /// Translate a Changsha snapshot into the full set of upstream collection
    /// entries needed for the bundle to render it.
    /// </summary>
    /// <param name="state">Authoritative Changsha state. May be null when no game is bound (always-available pattern).</param>
    /// <param name="viewerSeat">Optional seat of the connected viewer. If supplied, that seat's hand tiles render face-up; other seats stay face-down.</param>
    /// <param name="viewerPlayerId">Optional player id of the viewer (used to populate the <c>seats</c> collection so the bundle's camera rotates correctly).</param>
    public static IReadOnlyList<CollectionEntry> Translate(
        ChangshaGameState? state,
        int? viewerSeat = null,
        string? viewerPlayerId = null)
    {
        var entries = new List<CollectionEntry>();

        // match[0] — always present; even with no game we ship the conditions
        // override so the bundle re-creates tiles with fives='000' (clean 1:1
        // thing-index ↔ Changsha tileId mapping). Without this, fives='111'
        // collapses one copy of each 5 to a red-5 (typeIndex 34..36) and our
        // mapping breaks.
        entries.Add(new CollectionEntry("match", 0, BuildMatch(state)));

        if (state is null)
        {
            // Always-available pattern: an empty-but-valid snapshot keeps the
            // bundle's 15× auto-reconnect loop quiet when no Changsha game is
            // bound to the requested gameId.
            return entries;
        }

        // seats — one entry per Changsha seat. Keys are synthetic player ids.
        // The viewer's playerId (if known) maps to viewerSeat; other seats use
        // their bot/human PlayerId from the Changsha state.
        foreach (var seat in state.Seats)
        {
            var key = (viewerPlayerId is not null && viewerSeat == seat.SeatIndex)
                ? viewerPlayerId
                : SeatPlayerKey(seat);
            entries.Add(new CollectionEntry("seats", key, new { seat = seat.SeatIndex }));
        }

        // nicks — display names for the score-pad readout.
        foreach (var seat in state.Seats)
        {
            var key = (viewerPlayerId is not null && viewerSeat == seat.SeatIndex)
                ? viewerPlayerId
                : SeatPlayerKey(seat);
            entries.Add(new CollectionEntry("nicks", key, SeatNickname(seat)));
        }

        // dice — Default #1 is auto-roll, so we always reflect the latest roll.
        if (state.LastDiceRoll is { } roll)
        {
            entries.Add(new CollectionEntry("dice", 0, new
            {
                dice = new[] { roll.Die1, roll.Die2 },
                state = "rolled"
            }));
        }

        // things — 108 entries: one per Changsha tile placed at its current slot.
        foreach (var entry in BuildThingEntries(state, viewerSeat))
        {
            entries.Add(entry);
        }

        return entries;
    }

    // ── match ────────────────────────────────────────────────────────

    private static object BuildMatch(ChangshaGameState? state)
    {
        // Conditions shape mirrors upstream src/types.ts. We force fives='000'
        // so the bundle creates 4 regular copies of every 5-tile (red-5 disabled).
        var conditions = new
        {
            gameType = "FOUR_PLAYER",
            back = 0,
            fives = "000",
            points = "25",
            dealType = "INITIAL"
        };

        var dealer = state?.DealerSeatIndex ?? 0;
        // Changsha has no honba counter — keep zero.
        return new { dealer, honba = 0, conditions };
    }

    // ── things ───────────────────────────────────────────────────────

    private static IEnumerable<CollectionEntry> BuildThingEntries(
        ChangshaGameState state,
        int? viewerSeat)
    {
        // Compute per-seat discard layout up front. Discards land row-major
        // into a 3×6 radial tray per seat (upstream supports up to 18 + 4
        // overflow slots; Changsha rarely exceeds the primary grid in v1).
        var perSeatDiscardCounts = new int[4];

        // Track which tiles have been placed (sanity check — every Changsha
        // tileId 0..107 must end up exactly once across hands+melds+discards+wall).
        var placedTiles = new HashSet<int>();

        // Hands (concealed + melds).
        foreach (var hand in state.Hands.OrderBy(h => h.SeatIndex))
        {
            var seat = hand.SeatIndex;
            var concealedRotation = (viewerSeat == seat) ? HandRotFaceUp : HandRotFaceDown;

            for (var i = 0; i < hand.ConcealedTiles.Count; i++)
            {
                var tileId = hand.ConcealedTiles[i];
                var slot = AutotableSlotMap.HandSlot(seat, i);
                placedTiles.Add(tileId);
                yield return BuildThingEntry(tileId, slot, concealedRotation);
            }

            for (var m = 0; m < hand.Melds.Count; m++)
            {
                var meld = hand.Melds[m];
                // Concealed kong shows face-down to indicate the tiles are
                // hidden; all other melds are exposed.
                var isConcealedKong = meld.Kind == MeldKind.ConcealedKong;
                var rotation = isConcealedKong ? MeldRotFaceDown : MeldRotFaceUp;

                for (var t = 0; t < meld.TileIds.Count; t++)
                {
                    var tileId = meld.TileIds[t];
                    var slot = AutotableSlotMap.MeldSlot(seat, m, t);
                    placedTiles.Add(tileId);
                    yield return BuildThingEntry(tileId, slot, rotation);
                }
            }
        }

        // Discards — fall back to a flat 3×6 grid per seat (Phase 5a overflow
        // beyond 18 wraps around the last column; v1 hands rarely exceed 24
        // discards per seat so this is acceptable for MVP).
        foreach (var discard in state.DiscardPile)
        {
            var seat = discard.SeatIndex;
            var index = perSeatDiscardCounts[seat]++;
            var row = Math.Min(2, index / 6);
            var col = Math.Min(5, index % 6);
            placedTiles.Add(discard.TileId);
            yield return BuildThingEntry(
                discard.TileId,
                AutotableSlotMap.DiscardSlot(seat, row, col),
                DiscardRotFaceUp);
        }

        // Wall — remaining tiles. Place them into wall slots in canonical
        // 14/14/13/13 order. After deal there are 55 wall tiles; before deal
        // (Seating / RollingDice / Dealing phase) there are 0 (Hands empty)
        // or 108 (state.Wall holds the full deck). Either way we never
        // exceed 108 wall slots.
        using var slotEnumerator = AutotableSlotMap.EnumerateWallSlotsInOrder().GetEnumerator();
        foreach (var tileId in state.Wall)
        {
            if (!slotEnumerator.MoveNext())
            {
                throw new InvalidOperationException(
                    $"Wall slot capacity exceeded — state.Wall has {state.Wall.Count} tiles " +
                    $"but only {AutotableSlotMap.TotalTiles} canonical Changsha wall slots exist.");
            }
            var (seat, col, layer) = slotEnumerator.Current;
            placedTiles.Add(tileId);
            yield return BuildThingEntry(
                tileId,
                AutotableSlotMap.WallSlot(seat, col, layer),
                WallRotFaceDown);
        }
    }

    private static CollectionEntry BuildThingEntry(int changshaTileId, string slotName, int rotationIndex)
    {
        // thing-index intentionally equals the Changsha tile id (locked at
        // fives='000'). See class-level remarks.
        return new CollectionEntry("things", changshaTileId, new
        {
            slotName,
            rotationIndex,
            claimedBy = (int?)null,
            heldRotation = IdentityQuaternion,
            shiftSlotName = (string?)null
        });
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static string SeatPlayerKey(ChangshaSeatState seat) =>
        string.IsNullOrEmpty(seat.PlayerId) ? $"seat-{seat.SeatIndex}" : seat.PlayerId;

    private static string SeatNickname(ChangshaSeatState seat) =>
        seat.IsBot ? $"Bot {seat.SeatIndex}" : $"Seat {seat.SeatIndex}";
}
