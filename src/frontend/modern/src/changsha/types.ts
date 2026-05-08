/**
 * Changsha Mahjong state types — Phase 1 (straw-man).
 * When Bishop publishes docs/rules/changsha-signalr-contract.md,
 * reconcile these interfaces to match exactly.
 */

export type Suit = 'wan' | 'tong' | 'tiao';

export interface Tile {
  suit: Suit;
  rank: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;
  id: string;
}

export type SeatIndex = 0 | 1 | 2 | 3;
export type Wind = 'east' | 'south' | 'west' | 'north';

export interface SeatInfo {
  index: SeatIndex;
  nick: string;
  isBot: boolean;
  seatWind: Wind;
  score: number;
}

export type GamePhase =
  | 'seating'
  | 'rolling'
  | 'dealing'
  | 'play'
  | 'claim-window'
  | 'scoring'
  | 'end-hand'
  | 'ended';

export interface Meld {
  type: 'pung' | 'kong' | 'chow' | 'pair';
  tiles: Tile[];
  concealed: boolean;
}

export interface SeatHand {
  seatIndex: SeatIndex;
  concealed: Tile[];
  melds: Meld[];
}

export interface PendingClaim {
  seatIndex: SeatIndex;
  type: 'pung' | 'kong' | 'chow' | 'win';
}

export interface Payment {
  from: SeatIndex;
  to: SeatIndex;
  amount: number;
}

export interface WinResult {
  seatIndex: SeatIndex;
  pattern: string;
  payments: Payment[];
}

export interface ChangshaGameState {
  gameId: string;
  bankerSeat: SeatIndex;
  prevalentWind: Wind;
  currentRound: 1 | 2 | 3 | 4;
  currentHand: 1 | 2 | 3 | 4;
  seats: SeatInfo[];
  phase: GamePhase;
  lastDice?: [number, number];
  breakPoint?: { wallIndex: 0 | 1 | 2 | 3; stackIndex: number };
  hands: SeatHand[];
  wallRemaining: number;
  discardPile: Tile[];
  activeSeat?: SeatIndex;
  pendingClaims?: PendingClaim[];
  lastWin?: WinResult;
}
