namespace Mahjong.Autotable.Api.Changsha;

// ── Tile domain ────────────────────────────────────────────────────

public enum Suit
{
    Wan = 0,   // 萬 Characters
    Tong = 1,  // 筒 Dots
    Tiao = 2   // 条 Bamboo
}

public readonly record struct Tile(Suit Suit, int Rank)
{
    /// <summary>Rank must be 1–9.</summary>
    public int LogicalId => (int)Suit * 9 + (Rank - 1);
}

public enum Wind
{
    East = 0,
    South = 1,
    West = 2,
    North = 3
}

// ── Meld types ─────────────────────────────────────────────────────

public enum MeldKind
{
    Chow,
    Pung,
    ExposedKong,
    ConcealedKong,
    AddedKong
}

public sealed class Meld
{
    public required MeldKind Kind { get; init; }
    public required List<int> TileIds { get; init; }
    public int? ClaimedFromSeatIndex { get; init; }
}

// ── Win / Score types ──────────────────────────────────────────────

public enum WinPattern
{
    Standard,      // 4 sets + 1 pair (258 pair rule)
    SevenPairs,    // 7 distinct pairs
    AllPungs,      // 碰碰胡 — 4 pungs/kongs + pair
    FullFlush      // 清一色 — single suit
}

public enum WinMethod
{
    SelfDraw,      // 自摸
    Discard,       // 点炮
    RobbingKong    // 抢杠胡
}

public enum ScoreCategory
{
    SmallWin,
    BigWin
}

public sealed class WinResult
{
    public required int WinningSeatIndex { get; init; }
    public required WinMethod Method { get; init; }
    public required WinPattern Pattern { get; init; }
    public required int WinningTileId { get; init; }
    public required int SourceSeatIndex { get; init; }
    public bool IsFullFlush { get; init; }
}

/// <summary>
/// 诈胡 (false-Hu) penalty assessment per Baidu §诈胡处罚 — a seat that declared Hu
/// on a non-winning hand pays a Big-Win-equivalent penalty to each opponent. The
/// per-opponent amount is fixed at <see cref="ScoringService.FalseHuPenaltyPerOpponent"/>.
/// </summary>
public sealed class FalseHuPenalty
{
    public required int OffendingSeatIndex { get; init; }
    public required int PenaltyPerOpponent { get; init; }
    public required List<PaymentEntry> Payments { get; init; }
}

public sealed class PaymentEntry
{
    public required int FromSeatIndex { get; init; }
    public required int ToSeatIndex { get; init; }
    public required int Amount { get; init; }
    public required string Reason { get; init; }
}

public sealed class ScoreResult
{
    public required ScoreCategory Category { get; init; }
    public required int BasePoints { get; init; }
    public required List<PaymentEntry> Payments { get; init; }
}

// ── Dice ───────────────────────────────────────────────────────────

public readonly record struct DiceRoll(int Die1, int Die2)
{
    public int Sum => Die1 + Die2;
}

public readonly record struct BreakPointResult(
    int WallIndex,
    int StackIndex,
    int TileIndex);

// ── Game / Round / Hand state ──────────────────────────────────────

public enum ChangshaPhase
{
    Seating,
    RollingDice,
    Dealing,                // Auto-deal one-shot path (DealMode.Auto)

    // ── Phase F: manual-pickup sub-phases between Dealing and AwaitingDiscard ──
    // Grafted into the existing state machine — DealMode.Auto path skips them and
    // jumps directly Dealing → AwaitingDiscard. DealMode.Manual path walks each
    // pickup phase under runtime-driven (RollDice + TakeTilesFromWall) commands.
    BreakPointMarked,       // dice rolled, break point set, awaiting first round-1 pickup
    PickupRound1,           // round 1 — each seat takes 4 (cursor rotates clockwise from dealer)
    PickupRound2,           // round 2 — each seat takes 4 (cumulative 8)
    PickupRound3,           // round 3 — each seat takes 4 (cumulative 12)
    SingleTilePickup,       // each seat takes 1 single tile (cumulative 13)
    DealerExtra,            // dealer takes the 14th tile, must discard next

    AwaitingDiscard,
    AwaitingClaim,
    DeclaringKong,
    DrawingReplacement,
    Scoring,
    EndHand,
    RotatingBanker,
    WallExhausted,
    EndGame
}

/// <summary>
/// Phase F deal modes. <c>Auto</c> is the existing one-shot path (Wave-3 behaviour:
/// <c>RollDice → Deal</c> deposits 14/13/13/13 atomically). <c>Manual</c> activates the
/// pickup state machine (§2 of Ripley's Phase F design): the dealer clicks a Roll Dice
/// affordance, then each seat clicks to take 4/4/4/1 tiles per Chinese custom, then the
/// dealer takes a 14th tile and the hand begins.
/// </summary>
public enum DealMode
{
    /// <summary>Default for Changsha when started via tests / non-WS code paths.</summary>
    Auto = 0,
    /// <summary>Default for Changsha when started via the autotable WS endpoint (Phase F).</summary>
    Manual = 1
}

public sealed class ChangshaGameState
{
    public string GameId { get; set; } = Guid.NewGuid().ToString();
    public int Seed { get; set; }
    public ChangshaPhase Phase { get; set; } = ChangshaPhase.Seating;

    // Round tracking
    public Wind RoundWind { get; set; } = Wind.East;
    public int RoundNumber { get; set; } = 1;
    public int HandNumber { get; set; } = 1;
    public int HandInRound { get; set; } = 1;

    // Seats
    public int DealerSeatIndex { get; set; }
    public int ActiveSeatIndex { get; set; }
    public List<ChangshaSeatState> Seats { get; set; } = [];

    // Wall
    public List<int> Wall { get; set; } = [];
    public int WallDrawIndex { get; set; }
    public int WallBackIndex { get; set; }

    // Hands
    public List<ChangshaHandState> Hands { get; set; } = [];

    // Discard pile
    public List<ChangshaDiscard> DiscardPile { get; set; } = [];

    // Claim window
    public ChangshaClaimWindow? ClaimWindow { get; set; }

    // Turn tracking
    public int TurnNumber { get; set; } = 1;

    // Win / Score
    public WinResult? CurrentWin { get; set; }
    public ScoreResult? CurrentScore { get; set; }

    /// <summary>
    /// Seats that have declined a winning discard during the current hand. Per spec §3.6
    /// (missed-win 过胡), a seat that passes on a winnable discard is forbidden from
    /// claiming Win on subsequent discards within the same hand. Self-draw wins are still
    /// allowed. Per Baidu §过水 the lockout decays "until your next draw" — see
    /// <see cref="ChangshaGameStateMachine.DrawTile"/>. Cleared on every new hand by
    /// <see cref="ChangshaGameStateMachine.Deal"/>.
    /// </summary>
    public HashSet<int> MissedWinSeats { get; set; } = new();

    /// <summary>
    /// Append-only log of 诈胡 (false-Hu) penalties applied during this game. Each entry
    /// records the offending seat plus the payments that were applied to
    /// <see cref="CumulativeScores"/>. See <see cref="ChangshaGameStateMachine.RecordFalseHu"/>.
    /// </summary>
    public List<FalseHuPenalty> FalseHuPenalties { get; set; } = new();

    // Cumulative scores
    public Dictionary<int, int> CumulativeScores { get; set; } = new();

    // Dice
    public DiceRoll? LastDiceRoll { get; set; }
    public BreakPointResult? BreakPoint { get; set; }

    // ── Phase F: manual-pickup cursor ──
    /// <summary>Deal mode for the current hand. <see cref="DealMode.Auto"/> means the existing
    /// one-shot <see cref="ChangshaGameStateMachine.Deal"/> path; <see cref="DealMode.Manual"/>
    /// activates the multi-phase pickup state machine driven by
    /// <see cref="ChangshaGameStateMachine.BeginManualDeal"/> and
    /// <see cref="ChangshaGameStateMachine.TakeTilesFromWall"/>.</summary>
    public DealMode DealMode { get; set; } = DealMode.Auto;
    /// <summary>The seat whose turn it is to pick up tiles. Non-null only while
    /// <see cref="Phase"/> is one of the pickup phases.</summary>
    public int? PickupSeatIndex { get; set; }
    /// <summary>Zero-based offset into the pickup round. Resets on every new round.
    /// Used by <see cref="ChangshaGameStateMachine.AdvancePickupCursor"/> to know when a
    /// round is complete (offset reaches 4) and the next phase should begin.</summary>
    public int PickupRoundIndex { get; set; }

    // Event log (append-only)
    public List<ChangshaEvent> EventLog { get; set; } = [];
    public long EventSequence { get; set; }

    // State versioning
    // Phase H Wave 1 — StateVersion starts at 0 and monotonically increments by 1
    // on every successful mutation via ChangshaStateMachine.CreateEvent. Used as the
    // optimistic-concurrency token by IChangshaGameRuntime's `expectedVersion`
    // parameter — a stale token throws ChangshaConcurrencyException before any mutation.
    public int StateVersion { get; set; } = 0;
}

public sealed class ChangshaSeatState
{
    public int SeatIndex { get; set; }
    public Wind Wind { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public bool IsBot { get; set; }
    public bool IsDealer { get; set; }
}

public sealed class ChangshaHandState
{
    public int SeatIndex { get; set; }
    public List<int> ConcealedTiles { get; set; } = [];
    public List<Meld> Melds { get; set; } = [];
}

public sealed class ChangshaDiscard
{
    public int SeatIndex { get; set; }
    public int TileId { get; set; }
    public int TurnNumber { get; set; }
}

public sealed class ChangshaClaimWindow
{
    public int DiscardSeatIndex { get; set; }
    public int DiscardTileId { get; set; }
    public List<ChangshaClaimOpportunity> Opportunities { get; set; } = [];
}

public sealed class ChangshaClaimOpportunity
{
    public int SeatIndex { get; set; }
    public Tables.TableClaimType ClaimType { get; set; }
    public int Priority { get; set; }
}

public sealed class ChangshaEvent
{
    public long Sequence { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public int TurnNumber { get; set; }
    public int? TileId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
}
