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
    /// <param name="claimWindowTimeoutMs">Frost 2026-05-29 — total claim-window duration (matches
    /// <see cref="Changsha.Runtime.ChangshaRuntimeOptions.ClaimWindowTimeoutMs"/>). When &gt; 0
    /// the translator emits an absolute deadline = <c>state.ClaimWindow.OpenedAtUnixMs + claimWindowTimeoutMs</c>
    /// so the autotable client can render a meaningful countdown. When 0 (default), the deadline
    /// stays 0 — clients must treat that as "no client-side countdown" (server enforces the timeout)
    /// rather than "already expired".</param>
    public static IReadOnlyList<CollectionEntry> Translate(
        ChangshaGameState? state,
        int? viewerSeat = null,
        string? viewerPlayerId = null,
        int claimWindowTimeoutMs = 0)
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

        // claim window — one entry per seat that currently has an opportunity.
        // The bundle's claim collection drives the 碰/吃/杠/胡 buttons (Phase B scene).
        if (state.ClaimWindow is { } window && state.Phase == ChangshaPhase.AwaitingClaim)
        {
            // Frost 2026-05-29 — emit a real deadline (epoch ms) so the autotable
            // overlay + side-panel show a meaningful countdown instead of treating
            // the value as "expired now" and auto-passing.  Falls back to 0 when:
            //   • caller didn't supply ClaimWindowTimeoutMs (back-compat for tests
            //     that don't care about the deadline), OR
            //   • OpenedAtUnixMs is 0 (rehydrated state from before this field
            //     existed) — in which case the client must treat 0 as "no client
            //     timer" rather than "expired".
            var deadlineUnixMs = (claimWindowTimeoutMs > 0 && window.OpenedAtUnixMs > 0)
                ? window.OpenedAtUnixMs + claimWindowTimeoutMs
                : 0L;
            foreach (var seatGroup in window.Opportunities.GroupBy(o => o.SeatIndex))
            {
                var available = seatGroup
                    .Select(o => ClaimTypeToWire(o.ClaimType))
                    .Distinct()
                    .ToList();
                entries.Add(ChangshaCollectionEncoder.EncodeClaimWindow(
                    seatGroup.Key,
                    available,
                    window.DiscardSeatIndex,
                    window.DiscardTileId,
                    deadlineUnixMs: deadlineUnixMs));
            }
        }

        // result — populated once the hand has scored (or washed out).
        // Phase EndHand is the marker: CurrentWin set + CurrentScore set OR draw-hand event.
        if (state.Phase == ChangshaPhase.EndHand)
        {
            entries.Add(ChangshaCollectionEncoder.EncodeHandResult(BuildHandResult(state)));
        }

        // gameComplete — #116/#122 (Hudson P0). The authoritative end-of-match signal that
        // makes the bundle's #game-complete-modal reachable through real play. Emitted once the
        // game reaches its terminal phase (IsGameComplete, set by RotateBanker at MaxHands). The
        // existing StateChanged → translator → ApplyUpdate(Runtime) broadcast (PersistSnapshotAsync
        // fires StateChanged after RotateBanker) delivers this entry to every bound WS client;
        // there is no separate/synthetic emission path.
        if (state.IsGameComplete)
        {
            entries.Add(ChangshaCollectionEncoder.EncodeGameComplete(BuildGameComplete(state)));
        }

        // ── Phase F: pickup collection ──
        // Emitted while the manual-deal state machine is parked in any pickup phase;
        // drives the autotable scene's "Take Tiles" affordance. When the deal completes
        // and the runtime hands off to AwaitingDiscard, the entry is tombstoned by the
        // runtime-emitted snapshot via <see cref="ChangshaCollectionEncoder.EncodePickupCleared"/>
        // so the scene clears its UI.
        if (ChangshaGameStateMachine.IsPickupPhase(state.Phase))
        {
            entries.Add(ChangshaCollectionEncoder.EncodePickup(BuildPickupEntry(state)));
        }

        return entries;
    }

    private static PickupEntry BuildPickupEntry(ChangshaGameState state)
    {
        BreakPointWire? bp = null;
        if (state.BreakPoint is { } b)
        {
            bp = new BreakPointWire
            {
                WallIndex = b.WallIndex,
                StackIndex = b.StackIndex,
                TileIndex = b.TileIndex
            };
        }

        return new PickupEntry
        {
            Phase = state.Phase.ToString(),
            SeatIndex = state.PickupSeatIndex ?? state.DealerSeatIndex,
            Count = ChangshaGameStateMachine.ExpectedPickupCount(state.Phase),
            DealMode = state.DealMode == DealMode.Manual ? "manual" : "auto",
            BreakPoint = bp,
            // Wall front is always index 0 after BreakPointToWall rotation, but expose
            // the actual remaining-wall count so the bundle can decide UI affordances
            // (e.g., "Wall: 55 tiles left").
            WallIndex = 0
        };
    }

    internal static HandResultEntry BuildHandResult(ChangshaGameState state)
    {
        var win = state.CurrentWin;
        var winnerSeat = win?.WinningSeatIndex ?? -1;
        string type;
        List<int> winningHand = [];

        if (win is not null)
        {
            type = "Hu";
            var hand = state.Hands.FirstOrDefault(h => h.SeatIndex == winnerSeat);
            if (hand is not null)
            {
                winningHand.AddRange(hand.ConcealedTiles);
                foreach (var meld in hand.Melds)
                    winningHand.AddRange(meld.TileIds);
            }
        }
        else
        {
            // Wall exhaustion or false-Hu only — caller distinguishes via FalseHuPenalties.
            type = state.FalseHuPenalties.Count > 0 ? "ZhaHu" : "Draw";
        }

        // nextBanker mirrors the runtime's RotateBanker policy: winner becomes dealer;
        // washout retains current dealer. Match the rule here without mutating state.
        var nextBanker = win is not null ? win.WinningSeatIndex : state.DealerSeatIndex;

        // Phase I Wave 1 — surface winResult + scoreResult on the bundle WS path
        // so the frontend result modal (chip strip + multiplier breakdown +
        // RobbingKong badge) renders without a second SignalR subscription.
        // Null on draw/false-Hu — frontend already handles the null case.
        var winResult = win is null ? null : new WinResultEntry
        {
            WinningSeatIndex = win.WinningSeatIndex,
            WinType = WinMethodToWire(win.Method),
            WinPattern = WinPatternToWire(win.Pattern),
            WinningTileId = win.WinningTileId,
            SourceSeatIndex = win.SourceSeatIndex,
            AllPatterns = win.AllPatterns.Select(WinPatternToWire).ToList(),
            IsRobbedKong = win.IsRobbedKong,
            // Phase J Wave 3 — explicit IsSelfDraw + IsKongReplacement axes mirrored
            // onto the bundle WS path so the autotable collection-entry payload
            // matches the SignalR WinDeclared shape (Hicks's UI consumes both
            // transports). See WinResult.IsSelfDraw / IsKongReplacement.
            IsSelfDraw = win.IsSelfDraw,
            IsKongReplacement = win.IsKongReplacement
        };

        var score = state.CurrentScore;
        var scoreResult = (win is null || score is null) ? null : new ScoreResultEntry
        {
            Category = score.Category switch
            {
                ScoreCategory.SmallWin => "smallWin",
                ScoreCategory.BigWin => "bigWin",
                _ => score.Category.ToString().ToLowerInvariant()
            },
            BasePoints = score.BasePoints,
            Payments = score.Payments.Select(p => new ScorePaymentEntry
            {
                FromSeatIndex = p.FromSeatIndex,
                ToSeatIndex = p.ToSeatIndex,
                Amount = p.Amount,
                Reason = p.Reason
            }).ToList(),
            // Post-W23 — Frost's fan catalog breakdown surfaced on the bundle WS path
            // so the win-screen modal can render per-fan chips with Chinese/Pinyin/English
            // labels without a second round-trip. Empty list = no fans applied (legacy
            // 258-pair Standard win off a discard, etc.).
            Fans = score.Fans.Select(f =>
            {
                var info = Mahjong.Autotable.Api.Changsha.Scoring.FanCatalog.Get(f.Fan);
                return new FanEntry
                {
                    Fan = ChangshaGameStateMachine.FanWireName(f.Fan),
                    Points = f.Points,
                    Chinese = info.Chinese,
                    Pinyin = info.Pinyin,
                    English = info.English,
                };
            }).ToList(),
            FanPoints = score.FanPoints,
        };

        return new HandResultEntry
        {
            Winner = winnerSeat,
            Type = type,
            // Project Dictionary<int,int> CumulativeScores → ordered List<ScoreDeltaEntry>.
            // The wire contract is an ARRAY of { seat, delta } so the frontend's
            // `[...result.score]` spread + sort works directly. Emitting the dict
            // serializes as a JSON object {"0":..., "1":...} which is NOT iterable —
            // see game-ui.ts:renderResult + ScoreDeltaEntry XML doc. OrderBy(seat)
            // gives deterministic wire ordering across snapshots; the frontend
            // re-sorts anyway, so this is purely defensive.
            Score = state.CumulativeScores
                .OrderBy(kv => kv.Key)
                .Select(kv => new ScoreDeltaEntry { Seat = kv.Key, Delta = kv.Value })
                .ToList(),
            Hand = winningHand,
            NextBanker = nextBanker,
            WinResult = winResult,
            ScoreResult = scoreResult
        };
    }

    /// <summary>
    /// #116/#122 — builds the authoritative end-of-match payload from the terminal state.
    /// Mirrors the SignalR <c>GameCompleted</c> event's data (cumulative scores + hand cap) onto
    /// the autotable collection wire shape the bundle consumes. Winner is derived client-side from
    /// the highest total, so no winner field is required here.
    /// </summary>
    internal static GameCompleteEntry BuildGameComplete(ChangshaGameState state) => new()
    {
        IsComplete = true,
        MaxHands = state.MaxHands,
        TotalScores = state.CumulativeScores
            .OrderBy(kv => kv.Key)
            .ToDictionary(
                kv => kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                kv => kv.Value)
    };

    // Phase I Wave 1 — wire mappings mirror Runtime/ChangshaGameRuntime.cs so
    // the bundle path emits identical strings to the SignalR path.
    private static string WinMethodToWire(WinMethod m) => m switch
    {
        WinMethod.SelfDraw => "selfDraw",
        WinMethod.Discard => "discard",
        WinMethod.RobbingKong => "robbingKong",
        _ => m.ToString().ToLowerInvariant()
    };

    private static string WinPatternToWire(WinPattern p) => p switch
    {
        WinPattern.Standard => "standard",
        WinPattern.SevenPairs => "sevenPairs",
        WinPattern.AllPungs => "allPungs",
        WinPattern.FullFlush => "fullFlush",
        WinPattern.NineTerminals => "nineTerminals",
        WinPattern.HeavenlyHand => "heavenlyHand",
        WinPattern.EarthlyHand => "earthlyHand",
        WinPattern.LastTileFromWall => "lastTileFromWall",
        WinPattern.LastDiscardCatch => "lastDiscardCatch",
        WinPattern.KongReplacementWin => "kongReplacementWin",
        _ => "standard"
    };

    private static string ClaimTypeToWire(Tables.TableClaimType t) => t switch
    {
        Tables.TableClaimType.Hu => "Hu",
        Tables.TableClaimType.Kong => "Kong",
        Tables.TableClaimType.Pung => "Pung",
        Tables.TableClaimType.Chow => "Chow",
        _ => "Pass"
    };

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
        // 14/14/13/13 order. After deal there are 55 wall tiles; during the
        // manual-pickup ceremony there are 108..55 wall tiles depending on
        // pickup progress. In Seating (before any deal) and RollingDice
        // (manual: after StartGame, before BeginManualDeal materializes the
        // shuffled wall) state.Wall is empty.
        //
        // Stephen 2026-05-27 face-down-walls directive: the bundle MUST show
        // four canonical face-down walls from the moment the user connects
        // through the full pickup ceremony. When the authoritative wall is
        // empty AND no tiles have been dealt out yet (no hands, no melds,
        // no discards) we synthesize a 108-tile face-down placement so the
        // bundle's local "HANDS"-style dealType animation is overridden by
        // the authoritative snapshot the moment the deal click round-trips
        // to the server. Tile ids are emitted in canonical order (0..107);
        // the actual shuffled ordering is materialized later by
        // BeginManualDeal — at which point state.Wall takes over.
        var wallTiles = ShouldSynthesizeWall(state)
            ? Enumerable.Range(0, AutotableSlotMap.TotalTiles)
            : (IEnumerable<int>)state.Wall;

        using var slotEnumerator = AutotableSlotMap.EnumerateWallSlotsInOrder().GetEnumerator();
        foreach (var tileId in wallTiles)
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

    /// <summary>
    /// True when the authoritative wall is empty but the table has not yet
    /// distributed any tiles, so the translator should synthesize a 108-tile
    /// face-down wall placement to keep the four canonical walls visible.
    /// Gates strictly on <see cref="ChangshaPhase.Seating"/> and
    /// <see cref="ChangshaPhase.RollingDice"/> — the only two pre-deal phases
    /// where state.Wall can legitimately be empty. End-of-hand / wall-exhausted
    /// / game-complete states also have an empty wall but already have hands,
    /// discards, or melds, so they fall through to the authoritative path.
    /// </summary>
    private static bool ShouldSynthesizeWall(ChangshaGameState state)
    {
        if (state.Wall.Count != 0) return false;
        if (state.Phase is not (ChangshaPhase.Seating or ChangshaPhase.RollingDice)) return false;
        if (state.DiscardPile.Count != 0) return false;
        foreach (var hand in state.Hands)
        {
            if (hand.ConcealedTiles.Count != 0) return false;
            if (hand.Melds.Count != 0) return false;
        }
        return true;
    }

    private static CollectionEntry BuildThingEntry(int changshaTileId, string slotName, int rotationIndex)
    {
        // thing-index intentionally equals the Changsha tile id (locked at
        // fives='000'). See class-level remarks.
        //
        // Typed ThingInfo (not an anonymous object) so claimedBy / shiftSlotName carry an EXPLICIT
        // null on the wire — the C-1 contract types them number|null / string|null (present), and
        // the relay path already emits explicit null. An anonymous object's nulls are dropped by the
        // shared WhenWritingNull serializer; ThingInfo's [JsonIgnore(Never)] overrides that.
        return new CollectionEntry("things", changshaTileId, new ThingInfo
        {
            SlotName = slotName,
            RotationIndex = rotationIndex,
            ClaimedBy = null,
            HeldRotation = IdentityQuaternion,
            ShiftSlotName = null
        });
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static string SeatPlayerKey(ChangshaSeatState seat) =>
        string.IsNullOrEmpty(seat.PlayerId) ? $"seat-{seat.SeatIndex}" : seat.PlayerId;

    private static string SeatNickname(ChangshaSeatState seat) =>
        seat.IsBot ? $"Bot {seat.SeatIndex}" : $"Seat {seat.SeatIndex}";
}
