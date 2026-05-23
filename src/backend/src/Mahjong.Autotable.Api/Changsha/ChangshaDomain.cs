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
    FullFlush,     // 清一色 — single suit

    /// <summary>
    /// 九幺 (jiǔ-yāo) — Changsha-adapted "Nine Terminals" Big Win. Every tile in the
    /// 14-tile hand is rank 1 or rank 9 of any suit. The hand must still form a valid
    /// mahjong structure (4 sets + pair, OR 7 pairs). Introduced in Phase H Wave 2
    /// (per Ripley's design memo §2.1) as the Changsha analog to the classical
    /// ThirteenOrphans (十三幺) Big Win, which is structurally impossible in Changsha
    /// because the 108-tile deck has no honor tiles. Same precedence tier as FullFlush.
    /// </summary>
    NineTerminals,

    // ── Phase I Wave 1 — contextual Big Win flags (spec §4.3) ──
    // These are bonuses layered on top of a structurally valid mahjong hand
    // (Standard / SevenPairs / AllPungs / FullFlush / NineTerminals). They never
    // promote an otherwise-invalid hand to a win — see WinContext + WinDetector.
    // They DO participate in AllPatterns stacking (per Wave 2 multiplier contract).

    /// <summary>
    /// 天和 (tiān-hé) — "Heavenly Hand". Dealer wins by self-draw on their initial
    /// 14-tile hand, before any discards/claims/kong replacements have occurred this
    /// hand. <see cref="ChangshaGameStateMachine.DeclareSelfDrawWin"/> constructs the
    /// <see cref="WinContext"/> with <c>IsHeavenlyHand</c> when
    /// <see cref="ChangshaGameState.DiscardPile"/> is empty, the winning seat is the
    /// dealer, the dealer's hand has no melds, and <c>LastDrawWasKongReplacement</c>
    /// is false.
    /// </summary>
    HeavenlyHand,

    /// <summary>
    /// 地和 (dì-hé) — "Earthly Hand". Non-dealer wins by claiming Hu on the dealer's
    /// very first discard, with no intervening actions. <see cref="ChangshaGameStateMachine.ResolveHuClaim"/>
    /// constructs the <see cref="WinContext"/> with <c>IsEarthlyHand</c> when the
    /// discard pile has exactly one entry (dealer's first), the claimant is not the
    /// dealer, the claimant has no melds, and the claim is a regular discard Hu
    /// (not a robbing-the-added-kong window).
    /// </summary>
    EarthlyHand,

    /// <summary>
    /// 海底捞月 (hǎi-dǐ-lāo-yuè) — "Last Tile from the Wall". Self-draw on the very
    /// last tile of the wall (wall count == 0 immediately after the draw). Fires when
    /// <see cref="WinMethod.SelfDraw"/> and <see cref="ChangshaGameState.Wall"/> is
    /// empty at win-declaration time.
    /// </summary>
    LastTileFromWall,

    /// <summary>
    /// 河底捞鱼 (hé-dǐ-lāo-yú) — "Last Discard Catch". Wins by claiming Hu on a
    /// discard made when the wall is already exhausted (no more draws possible).
    /// Fires when <see cref="WinMethod.Discard"/> and <see cref="ChangshaGameState.Wall"/>
    /// is empty at the time of the claim. Robbing-the-added-kong wins are intentionally
    /// excluded — the kong target tile was never in the river.
    /// </summary>
    LastDiscardCatch,

    /// <summary>
    /// 杠上开花 (gàng-shàng-kāi-huā) — "Win on Kong Replacement". Self-draw win on
    /// the replacement tile drawn after declaring a concealed, added, or exposed kong.
    /// Tracked via the transient <see cref="ChangshaGameState.LastDrawWasKongReplacement"/>
    /// flag, which is set true on every kong-replacement draw and cleared on the next
    /// regular <see cref="ChangshaGameStateMachine.DrawTile"/> or
    /// <see cref="ChangshaGameStateMachine.Discard"/>.
    /// </summary>
    KongReplacementWin
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

    /// <summary>
    /// Phase H Wave 2 — true when this win was declared by 抢杠胡 (Robbing the Added Kong).
    /// The winning tile was captured mid-kong-declaration from another seat's added-kong
    /// (補杠 — 4th tile being added to an already-exposed pung). Tagged in addition to
    /// <c>Method == WinMethod.RobbingKong</c> so scoring/auditing can branch without
    /// re-parsing the method enum. Concealed kongs (暗杠) are never robbable per
    /// spec §3.4.3, so this flag is always paired with Added-kind kongs.
    /// </summary>
    public bool IsRobbedKong { get; init; }

    /// <summary>
    /// Phase J Wave 3 — explicit boolean axis indicating the win arrived via a draw
    /// from the wall (regular front-of-wall draw OR kong-replacement draw) rather
    /// than by claiming another seat's discard. Equivalent to
    /// <c>Method == <see cref="WinMethod.SelfDraw"/></c> but lifted to a top-level
    /// flag so downstream consumers (UI banners, replay, JSON wire surface, tests)
    /// don't have to re-derive it from the method enum. Mirrors the SignalR
    /// <c>WinDeclared.winResult</c> serialization shape — public auto-property so
    /// the default <see cref="System.Text.Json"/> camelCase contract picks it up
    /// as <c>isSelfDraw</c>. See <see cref="ChangshaGameStateMachine.DeclareSelfDrawWin"/>
    /// (sets true) and <see cref="ChangshaGameStateMachine.ResolveHuClaim"/> (sets
    /// false — both <see cref="WinMethod.Discard"/> and <see cref="WinMethod.RobbingKong"/>
    /// are NOT self-draws even when the winning tile was reached via a kong-window).
    /// </summary>
    public bool IsSelfDraw { get; init; }

    /// <summary>
    /// Phase J Wave 3 — explicit boolean axis indicating the winning tile was drawn
    /// as a kong replacement (杠上开花). Equivalent to
    /// <c>AllPatterns.Contains(<see cref="WinPattern.KongReplacementWin"/>)</c> but
    /// lifted to a top-level flag so downstream consumers (UI banner, scoring audit,
    /// replay) don't have to scan <see cref="AllPatterns"/>. The pattern record is
    /// retained in <see cref="AllPatterns"/> for backward compatibility (Phase H/I
    /// callers that consult AllPatterns continue to work unchanged). Set true only
    /// when <see cref="Method"/> == <see cref="WinMethod.SelfDraw"/> AND the most
    /// recent hand mutation was a kong-replacement draw — robbing-kong wins
    /// (<see cref="WinMethod.RobbingKong"/>) are NOT kong-replacement wins, the
    /// winning seat captured the kong rather than drawing its replacement.
    /// </summary>
    public bool IsKongReplacement { get; init; }

    /// <summary>
    /// Phase H Wave 2 — every Big Win pattern satisfied by the winning hand, mirrored
    /// from <see cref="WinDetectionResult.AllPatterns"/> at win-declaration time so the
    /// stacking multiplier survives the detector → state → scoring boundary. Order is
    /// deterministic (enum-declaration order). Empty list = single-pattern win (×1
    /// multiplier). <see cref="ScoringService.CalculateScore(WinResult,int,bool,int)"/>
    /// uses <c>AllPatterns.Count</c> (clamped to [1, 3]) as the multiplier.
    /// </summary>
    public IReadOnlyList<WinPattern> AllPatterns { get; init; } = [];
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
    EndGame,

    /// <summary>
    /// Phase J Wave 2 — terminal phase reached when
    /// <see cref="ChangshaGameState.HandNumber"/> exceeds
    /// <see cref="ChangshaGameState.MaxHands"/>. Distinct from
    /// <see cref="EndGame"/> (which is the legacy 16-hand / 4-round terminal):
    /// the autotable / lobby surface defaults to a 4-hand rotation, and
    /// <see cref="ChangshaGameStateMachine.RotateBanker"/> sets this phase
    /// + <see cref="ChangshaGameState.IsGameComplete"/> when the cap is hit.
    /// Like <see cref="EndGame"/>, downstream phase-guards
    /// (<see cref="ChangshaGameStateMachine.RollDice"/>,
    /// <see cref="ChangshaGameStateMachine.StartGame"/>, etc.) reject any
    /// further mutation: the only valid exit is creating a new game.
    /// </summary>
    GameComplete
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

    /// <summary>
    /// Phase J Wave 2 — cap on the number of hands played before the game is
    /// considered complete. <see cref="ChangshaGameStateMachine.RotateBanker"/>
    /// checks <c>HandNumber &gt; MaxHands</c> after the post-hand increment and
    /// sets <see cref="Phase"/> to <see cref="ChangshaPhase.GameComplete"/> +
    /// <see cref="IsGameComplete"/> to <c>true</c>. Default is <c>4</c> — one
    /// full east-wind rotation, the standard solo / autotable bot-match length.
    /// Tournament play can override via the runtime <c>CreateGame</c> /
    /// <c>?maxHands=</c> WS query param. The legacy 16-hand (4 × 4) terminal in
    /// <see cref="ChangshaPhase.EndGame"/> remains for tests that explicitly
    /// raise <c>MaxHands</c> above 16.
    /// </summary>
    public int MaxHands { get; set; } = 4;

    /// <summary>
    /// Phase J Wave 2 — terminal flag flipped to <c>true</c> by
    /// <see cref="ChangshaGameStateMachine.RotateBanker"/> when the game reaches
    /// <see cref="ChangshaPhase.GameComplete"/> (new N-hand cap) or
    /// <see cref="ChangshaPhase.EndGame"/> (legacy 16-hand terminal). Provides a
    /// phase-agnostic predicate for callers that need a single "is the game
    /// over?" signal — used by the runtime's <see cref="ChangshaGameRuntime"/>
    /// to gate the <c>GameCompleted</c> SignalR event and by the autotable
    /// translator to emit the <c>gameComplete</c> collection entry that drives
    /// Hicks's end-of-game summary modal.
    /// </summary>
    public bool IsGameComplete { get; set; } = false;

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

    /// <summary>
    /// Phase I Wave 1 — transient flag tracking whether the most recent tile added to
    /// the active seat's concealed hand came from a kong replacement draw (concealed,
    /// added, or exposed kong). Set to <c>true</c> by
    /// <see cref="ChangshaGameStateMachine.DeclareConcealedKong"/>,
    /// <see cref="ChangshaGameStateMachine.DeclareAddedKong"/> (via <c>CompleteAddedKong</c>),
    /// and the Kong branch of <see cref="ChangshaGameStateMachine.ResolveClaim"/>
    /// whenever the replacement draw succeeds. Cleared to <c>false</c> by
    /// <see cref="ChangshaGameStateMachine.DrawTile"/> (regular front-of-wall draw) and
    /// <see cref="ChangshaGameStateMachine.Discard"/> (player has discarded — the
    /// kong-replacement chain is broken). Used to detect <see cref="WinPattern.KongReplacementWin"/>
    /// in <see cref="ChangshaGameStateMachine.DeclareSelfDrawWin"/>; also gates
    /// <see cref="WinPattern.HeavenlyHand"/> detection (a dealer who declared a kong
    /// before declaring Hu is on a kong-replacement win, not a heavenly hand).
    /// </summary>
    public bool LastDrawWasKongReplacement { get; set; } = false;
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

    /// <summary>
    /// Phase H Wave 2 — true when the window was opened by an added-kong (補杠)
    /// declaration rather than a regular discard. In this mode the window only
    /// surfaces Hu opportunities (Pung/Kong/Chow are illegal — the tile is being
    /// added to a kong, not discarded into the river). If any seat claims Hu the
    /// resolver tags <see cref="WinResult.Method"/> as <see cref="WinMethod.RobbingKong"/>
    /// with <see cref="WinResult.IsRobbedKong"/> = true; if all seats pass, the
    /// state machine completes the kong normally (replacement draw + DrawingReplacement).
    /// See <see cref="ChangshaGameStateMachine.DeclareAddedKong"/> +
    /// <see cref="ChangshaGameStateMachine.CompleteAddedKongAfterPass"/>.
    /// </summary>
    public bool IsKongRobbing { get; set; }

    /// <summary>
    /// Phase H Wave 2 — when <see cref="IsKongRobbing"/> is true, this is the seat
    /// declaring the added-kong (i.e. the seat whose pung is being upgraded). It is
    /// the <see cref="WinResult.SourceSeatIndex"/> for a successful robbing-kong Hu.
    /// Mirrors <see cref="DiscardSeatIndex"/> semantically — kept as a separate field
    /// for clarity in serialised state.
    /// </summary>
    public int? KongDeclarerSeatIndex { get; set; }
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
