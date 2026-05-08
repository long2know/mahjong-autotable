/**
 * Changsha Mahjong state types.
 * Reconciled with Bishop's SignalR contract (docs/rules/changsha-signalr-contract.md).
 * Server event/command types are from Bishop's contract verbatim.
 * ChangshaGameState is a client-side aggregate for rendering.
 */

// ── Enums (from SignalR contract) ─────────────────────────────────

export type Suit = 'wan' | 'tong' | 'tiao';
export type Wind = 'east' | 'south' | 'west' | 'north';
export type ClaimType = 'hu' | 'kong' | 'pung' | 'chow';
export type MeldType = 'chow' | 'pung' | 'exposedKong' | 'concealedKong' | 'addedKong';
export type WinType = 'selfDraw' | 'discard' | 'robbingKong';
export type WinPattern = 'standard' | 'sevenPairs' | 'allPungs' | 'fullFlush';
export type ScoreCategory = 'smallWin' | 'bigWin';

export type GamePhase =
  | 'lobby'
  | 'seating'
  | 'rollingDice'
  | 'dealing'
  | 'awaitingDiscard'
  | 'awaitingClaim'
  | 'declaringKong'
  | 'drawingReplacement'
  | 'scoring'
  | 'endHand'
  | 'rotatingBanker'
  | 'endGame';

// ── Value types (from SignalR contract) ───────────────────────────

export interface Tile {
  id: number;       // 0–107
  suit: Suit;       // derived: Math.floor(id / 4 / 9)
  rank: number;     // derived: (Math.floor(id / 4) % 9) + 1
}

export interface DiceResult {
  die1: number;     // 1–6
  die2: number;     // 1–6
  sum: number;      // 2–12
}

export interface BreakPoint {
  wallIndex: number;      // 0–3 (which player's wall)
  stackIndex: number;     // stack position within that wall
  tileIndex: number;      // absolute index into the 108-tile ordered wall
}

export interface MeldState {
  type: MeldType;
  tileIds: number[];
  claimedFrom?: number;   // seat index of discarder (undefined for concealed)
}

export interface SeatState {
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

export interface ClaimOpportunity {
  seatIndex: number;
  claimType: ClaimType;
  priority: number;
  tileIds?: number[];     // tiles that would form the meld (for chow)
}

export interface WinResult {
  winningSeatIndex: number;
  winType: WinType;
  winPattern: WinPattern;
  winningTileId: number;
  sourceSeatIndex: number;
}

export interface ScoreResult {
  category: ScoreCategory;
  basePoints: number;
  payments: PaymentEntry[];
}

export interface PaymentEntry {
  fromSeatIndex: number;
  toSeatIndex: number;
  amount: number;
  reason: string;
}

export interface HandSummary {
  handNumber: number;
  roundWind: Wind;
  dealerSeatIndex: number;
  winResult?: WinResult;
  scoreResult?: ScoreResult;
  isDraw: boolean;
}

export interface GameSummary {
  gameId: string;
  totalHands: number;
  currentRound: number;
  roundWind: Wind;
  handInRound: number;
  dealerSeatIndex: number;
  scores: Record<number, number>;
}

// ── Client-side aggregate state (for rendering) ──────────────────
// Assembled from SignalR events; Phase 2 will build this from real events.

export type SeatIndex = 0 | 1 | 2 | 3;

export interface SeatInfo {
  index: SeatIndex;
  nick: string;
  isBot: boolean;
  seatWind: Wind;
  score: number;
}

export interface SeatHand {
  seatIndex: SeatIndex;
  concealed: Tile[];
  melds: MeldState[];
}

export interface PendingClaim {
  seatIndex: SeatIndex;
  type: ClaimType;
}

export interface ChangshaGameState {
  gameId: string;
  bankerSeat: SeatIndex;
  prevalentWind: Wind;
  currentRound: number;
  currentHand: number;
  seats: SeatInfo[];
  phase: GamePhase;
  lastDice?: DiceResult;
  breakPoint?: BreakPoint;
  hands: SeatHand[];
  wallRemaining: number;
  discardPile: Tile[];
  activeSeat?: SeatIndex;
  pendingClaims?: PendingClaim[];
  lastWin?: WinResult;
  lastScore?: ScoreResult;
}
