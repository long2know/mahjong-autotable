# Changsha Mahjong v1 — SignalR Contract

> **Authoritative contract for real-time communication between backend and frontend.**
> Written by Bishop (Backend Dev). Consumed by Hicks (Frontend Dev).
> Version: 1.0 · Branch: `stlong/changsha-v1`

## Hub endpoint

```
/hubs/changsha
```

Connection is authenticated per-seat via query string: `?gameId={guid}&seatIndex={0-3}`

---

## TypeScript type definitions

Copy these verbatim into your frontend types file.

```typescript
// ── Enums ──────────────────────────────────────────────────────────

type Suit = "wan" | "tong" | "tiao";
type Wind = "east" | "south" | "west" | "north";
type ClaimType = "hu" | "kong" | "pung" | "chow";
type MeldType = "chow" | "pung" | "exposedKong" | "concealedKong" | "addedKong";
type WinType = "selfDraw" | "discard" | "robbingKong";
type WinPattern = "standard" | "sevenPairs" | "allPungs" | "fullFlush";
type ScoreCategory = "smallWin" | "bigWin";

type GamePhase =
  | "lobby"
  | "seating"
  | "rollingDice"
  | "dealing"
  | "awaitingDiscard"
  | "awaitingClaim"
  | "declaringKong"
  | "drawingReplacement"
  | "scoring"
  | "endHand"
  | "rotatingBanker"
  | "endGame";

// ── Value types ────────────────────────────────────────────────────

interface Tile {
  id: number;        // 0–107
  suit: Suit;        // derived: Math.floor(id / 4 / 9)
  rank: number;      // derived: (Math.floor(id / 4) % 9) + 1
}

interface DiceResult {
  die1: number;      // 1–6
  die2: number;      // 1–6
  sum: number;       // 2–12
}

interface BreakPoint {
  wallIndex: number;       // 0–3 (which player's wall)
  stackIndex: number;      // stack position within that wall
  tileIndex: number;       // absolute index into the 108-tile ordered wall
}

interface MeldState {
  type: MeldType;
  tileIds: number[];
  claimedFrom?: number;    // seat index of discarder (undefined for concealed)
}

interface SeatState {
  seatIndex: number;
  wind: Wind;
  playerId: string;
  isBot: boolean;
  isDealer: boolean;
  tileCount: number;
  concealedTiles?: number[];  // only sent to the owning seat
  melds: MeldState[];
  discards: number[];
}

interface ClaimOpportunity {
  seatIndex: number;
  claimType: ClaimType;
  priority: number;
  tileIds?: number[];      // tiles that would form the meld (for chow)
}

interface WinResult {
  winningSeatIndex: number;
  winType: WinType;
  winPattern: WinPattern;
  winningTileId: number;
  sourceSeatIndex: number;  // same as winner for self-draw
}

interface ScoreResult {
  category: ScoreCategory;
  basePoints: number;
  payments: PaymentEntry[];
}

interface PaymentEntry {
  fromSeatIndex: number;
  toSeatIndex: number;
  amount: number;
  reason: string;           // e.g. "bigWin-selfDraw-dealer", "smallWin-discard"
}

interface HandSummary {
  handNumber: number;
  roundWind: Wind;
  dealerSeatIndex: number;
  winResult?: WinResult;
  scoreResult?: ScoreResult;
  isDraw: boolean;
}

interface GameSummary {
  gameId: string;
  totalHands: number;
  currentRound: number;
  roundWind: Wind;
  handInRound: number;
  dealerSeatIndex: number;
  scores: Record<number, number>;  // seatIndex → cumulative score
}

// ── Server → Client events ────────────────────────────────────────

interface ServerEvents {
  /** Game was created in lobby state */
  GameCreated: (payload: {
    gameId: string;
    ruleSet: "changsha-v1";
    seats: SeatState[];
  }) => void;

  /** A player took a seat */
  PlayerSeated: (payload: {
    gameId: string;
    seatIndex: number;
    playerId: string;
    isBot: boolean;
  }) => void;

  /** Game started — transitions from lobby to play */
  GameStarted: (payload: {
    gameId: string;
    dealerSeatIndex: number;
    roundWind: Wind;
    handNumber: number;
  }) => void;

  /** Dice were rolled to determine break point */
  DiceRolled: (payload: {
    gameId: string;
    rollerSeatIndex: number;
    dice: DiceResult;
  }) => void;

  /** Wall break point was computed from dice roll */
  BreakPointSet: (payload: {
    gameId: string;
    breakPoint: BreakPoint;
  }) => void;

  /** Tiles dealt (batched — one event per batch of 4) */
  TilesDealt: (payload: {
    gameId: string;
    seatIndex: number;
    tileIds: number[];       // only populated for the receiving seat
    tileCount: number;       // always populated
    batchNumber: number;     // 1–4
    isComplete: boolean;     // true on final batch
  }) => void;

  /** Active player's turn begins */
  TurnStarted: (payload: {
    gameId: string;
    seatIndex: number;
    turnNumber: number;
    wallRemaining: number;
    phase: GamePhase;
  }) => void;

  /** A tile was drawn from the wall */
  TileDrawn: (payload: {
    gameId: string;
    seatIndex: number;
    tileId?: number;         // only sent to the drawing seat
    wallRemaining: number;
    isReplacementDraw: boolean;
  }) => void;

  /** A tile was discarded */
  TileDiscarded: (payload: {
    gameId: string;
    seatIndex: number;
    tileId: number;
    turnNumber: number;
  }) => void;

  /** Claim window opened — other players may claim the discard */
  ClaimWindowOpen: (payload: {
    gameId: string;
    discardSeatIndex: number;
    discardTileId: number;
    opportunities: ClaimOpportunity[];
    timeoutMs: number;       // client-side timer hint
  }) => void;

  /** A claim was made (after adjudication) */
  ClaimMade: (payload: {
    gameId: string;
    claimingSeatIndex: number;
    claimType: ClaimType;
    tileId: number;
    meld: MeldState;
  }) => void;

  /** A kong replacement tile was drawn */
  KongReplacementDrawn: (payload: {
    gameId: string;
    seatIndex: number;
    tileId?: number;         // only sent to the drawing seat
    wallRemaining: number;
  }) => void;

  /** A win was declared */
  WinDeclared: (payload: {
    gameId: string;
    winResult: WinResult;
    hand: {
      concealedTiles: number[];
      melds: MeldState[];
    };
  }) => void;

  /** Scoring complete for the hand */
  ScoringComplete: (payload: {
    gameId: string;
    handSummary: HandSummary;
    gameSummary: GameSummary;
  }) => void;

  /** Banker seat rotated for next hand */
  BankerRotated: (payload: {
    gameId: string;
    previousDealerSeatIndex: number;
    newDealerSeatIndex: number;
    reason: "winnerBecomesDealer" | "drawRotation" | "dealerRetained";
  }) => void;

  /** Round wind changed */
  RoundChanged: (payload: {
    gameId: string;
    previousRoundWind: Wind;
    newRoundWind: Wind;
    roundNumber: number;
  }) => void;

  /** Hand finished (container event after scoring + rotation) */
  HandFinished: (payload: {
    gameId: string;
    handNumber: number;
    handSummary: HandSummary;
    nextHandNumber: number;
    nextDealerSeatIndex: number;
    nextRoundWind: Wind;
    isGameOver: boolean;
  }) => void;

  /** Game has ended — all rounds complete */
  GameEnded: (payload: {
    gameId: string;
    gameSummary: GameSummary;
    finalScores: Record<number, number>;
    winner: { seatIndex: number; score: number };
  }) => void;
}

// ── Client → Server commands ──────────────────────────────────────

interface ClientCommands {
  /** Create a new Changsha game */
  CreateGame: (payload: {
    ruleSet: "changsha-v1";
    botSeatIndexes?: number[];
    seed?: number;
  }) => Promise<{ gameId: string }>;

  /** Join an existing game lobby */
  JoinTable: (payload: {
    gameId: string;
  }) => Promise<{ success: boolean }>;

  /** Take a specific seat */
  TakeSeat: (payload: {
    gameId: string;
    seatIndex: number;
  }) => Promise<{ success: boolean; seatIndex: number }>;

  /** Start the game (only host/dealer can invoke) */
  StartGame: (payload: {
    gameId: string;
  }) => Promise<{ success: boolean }>;

  /** Roll dice for break point determination */
  RollDice: (payload: {
    gameId: string;
  }) => Promise<{ dice: DiceResult }>;

  /** Acknowledge that deal animation completed (client readiness) */
  AcknowledgeDeal: (payload: {
    gameId: string;
    seatIndex: number;
  }) => Promise<void>;

  /** Discard a tile from hand */
  Discard: (payload: {
    gameId: string;
    seatIndex: number;
    tileId: number;
  }) => Promise<void>;

  /** Submit a claim on the current discard */
  Claim: (payload: {
    gameId: string;
    seatIndex: number;
    type: ClaimType;
    tileIds?: number[];     // required for chow — specifies which tiles form the meld
  }) => Promise<void>;

  /** Declare a concealed or added kong */
  DeclareKong: (payload: {
    gameId: string;
    seatIndex: number;
    tileIds: number[];
  }) => Promise<void>;

  /** Declare win (self-draw or on discard during claim window) */
  DeclareWin: (payload: {
    gameId: string;
    seatIndex: number;
  }) => Promise<void>;

  /** Pass on current claim window */
  Pass: (payload: {
    gameId: string;
    seatIndex: number;
  }) => Promise<void>;
}
```

---

## Protocol notes

1. **Seat-scoped visibility**: `concealedTiles` and drawn `tileId` are only sent to the owning seat. Other seats receive `tileCount` and `undefined` for hidden tile fields.

2. **Claim window flow**:
   - Server emits `TileDiscarded` → `ClaimWindowOpen`
   - Clients respond with `Claim` or `Pass` (or timeout → auto-pass)
   - Server adjudicates priority: hu > kong = pung > chow
   - Server emits `ClaimMade` or advances turn

3. **Kong flow**:
   - Exposed kong from discard: handled via `Claim { type: "kong" }`
   - Concealed kong (4 in hand): `DeclareKong` during own turn (before discard)
   - Added kong (pung + drawn 4th): `DeclareKong` during own turn
   - All kongs trigger `KongReplacementDrawn` from back of wall

4. **Win declaration**:
   - Self-draw: `DeclareWin` during own turn (after draw, before discard)
   - Discard win: `Claim { type: "hu" }` during claim window
   - Server validates win patterns before accepting

5. **Game lifecycle**: `CreateGame` → `PlayerSeated` × 4 → `StartGame` → (per hand: `DiceRolled` → `TilesDealt` → turn loop → `WinDeclared`/draw → `ScoringComplete` → `BankerRotated`) × 16 → `GameEnded`

6. **Reconnection**: client re-sends `JoinTable` with same `gameId`; server replays missed events from the append-only event log.

7. **Error handling**: Invalid commands return a SignalR error with `{ code: string, message: string }`. The client should display the message and not retry automatically.
