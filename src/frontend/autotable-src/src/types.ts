import { Vector3, Quaternion } from "three";

export enum ThingType {
  TILE = 'TILE',
  STICK = 'STICK',
  MARKER = 'MARKER',
}

export const Size = {
  TILE: new Vector3(6, 9, 4),
  STICK: new Vector3(20, 2, 1),
  MARKER: new Vector3(12, 6, 1),
};

export interface Place {
  position: Vector3;
  rotation: Quaternion;
  size: Vector3;
}

export interface ThingInfo {
  slotName: string;
  rotationIndex: number;
  claimedBy: number | null;
  heldRotation: { x: number; y: number; z: number, w: number };
  shiftSlotName: string | null;
  // Phase D: per-viewer privacy. Bishop's WS endpoint strips `face` to null
  // for concealed tiles in other players' hands. The bundle defends against
  // a backend that forgets to flip rotationIndex face-down by treating any
  // entry with face === null as "must render as a back" (world.ts coerces
  // rotation accordingly). When `face` is omitted entirely the field is
  // ignored — preserves Phase B sandbox behaviour.
  face?: number | null;
}

export interface MatchInfo {
  dealer: number;
  honba: number;
  conditions: Conditions;
}

export interface Game {
  gameId: string;
  num: number;
}

export enum DealType {
  INITIAL = 'INITIAL',
  WINDS = 'WINDS',
  HANDS = 'HANDS',
  UNSHUFFLED = 'UNSHUFFLED',
}

export type Fives = '000' | '111' | '121';
export type Points = '25' | '30' | '35' | '40' | '100';
export type DealMode = 'manual' | 'auto';

// Phase F — full variant matrix restored.  Changsha is the Phase F default
// (runtime-authoritative); the four upstream variants restore byte-identical
// behaviour with pwmarcz/autotable's relay mode.  FOUR_PLAYER_DEMO was dropped
// per Ripley Phase F design §1.1: it was a cosmetic dupe of FOUR_PLAYER and
// was never wired to a real flow upstream.
export enum GameType {
  CHANGSHA = 'CHANGSHA',           // Phase F default — runtime-authoritative
  FOUR_PLAYER = 'FOUR_PLAYER',     // Upstream Riichi 4-player
  THREE_PLAYER = 'THREE_PLAYER',   // Upstream Riichi 3-player (sanma)
  BAMBOO = 'BAMBOO',               // Upstream 2-player bamboo
  MINEFIELD = 'MINEFIELD',         // Upstream 2-player minefield
}

interface GameTypeMeta {
  points: Points;
  seats: Array<number>;
}

export const GAME_TYPES: Record<GameType, GameTypeMeta> = {
  CHANGSHA:     { points: '25',  seats: [0, 1, 2, 3] },
  FOUR_PLAYER:  { points: '25',  seats: [0, 1, 2, 3] },
  THREE_PLAYER: { points: '35',  seats: [0, 1, 2]    },
  BAMBOO:       { points: '100', seats: [0, 2]       },
  MINEFIELD:    { points: '25',  seats: [0, 2]       },
};

export interface Conditions {
  gameType: GameType;
  // Upstream fields — restored from pre-Phase-B for non-Changsha variants.
  // For Changsha these stay pinned (back=0, fives='000', points='25') by the
  // backend translator regardless of UI input (ChangshaToAutotableTranslator
  // line 204-209). They're carried in the type so the upstream Setup.setup
  // path can render the original Riichi scene unchanged.
  back: number;            // 0 or 1 — toggles tile-back colour cycle
  fives: Fives;            // red-five density: 000 / 111 / 121
  points: Points;          // starting points (drives addSticks)
  dealType: DealType;
  // Phase F additions.
  baseUnit: number;        // Changsha scoring base unit (server pins to 1)
  dealMode: DealMode;      // manual = click-driven pickup, auto = one-shot deal
}

export namespace Conditions {
  export function initial(): Conditions {
    return defaultsFor(GameType.CHANGSHA);
  }

  /** Phase F — variant-aware defaults. Changsha boots in manual-pickup mode
   *  with 108-tile / fives='000' (translator pins these anyway); upstream
   *  variants boot in auto-deal mode with their canonical Riichi defaults. */
  export function defaultsFor(gameType: GameType): Conditions {
    if (gameType === GameType.CHANGSHA) {
      return {
        gameType: GameType.CHANGSHA,
        back: 0,
        fives: '000',
        points: '25',
        dealType: DealType.HANDS,
        baseUnit: 1,
        dealMode: 'manual',
      };
    }
    return {
      gameType,
      back: 0,
      fives: '111',
      points: GAME_TYPES[gameType].points,
      dealType: DealType.HANDS,
      baseUnit: 1,
      dealMode: 'auto',
    };
  }

  export function equals(a: Conditions, b: Conditions): boolean {
    return a.gameType === b.gameType
        && a.back === b.back
        && a.fives === b.fives
        && a.points === b.points
        && a.dealType === b.dealType
        && a.baseUnit === b.baseUnit
        && a.dealMode === b.dealMode;
  }

  export function describe(ts: Conditions): string {
    if (ts.gameType === GameType.CHANGSHA) {
      return ts.dealMode === 'manual' ? 'Changsha (manual)' : 'Changsha (auto)';
    }
    const game = {
      'FOUR_PLAYER':  '4p',
      'THREE_PLAYER': '3p',
      'BAMBOO':       'b',
      'MINEFIELD':    'm',
      'CHANGSHA':     'Changsha', // unreachable — handled above
    }[ts.gameType];
    const fives = {'000': 'no red', '111': '1-1-1', '121': '1-2-1'}[ts.fives];
    return `${game}, ${fives}`;
  }
}

export interface MouseInfo {
  held: {x: number; y: number; z: number} | null;
  mouse: {x: number; y: number; z: number; time: number} | null;
}

export enum SoundType {
  DISCARD = 'DISCARD',
  STICK = 'STICK',
};

export interface SoundInfo {
  type: SoundType;
  seat: number;
  side: number | null;
}

export interface SeatInfo {
  seat: number | null;
}

export interface DiceInfo {
  dice: [number, number];
  state: 'ignore' | 'rolled';
  // Phase D additions (Bishop's `dice` collection extension): server can push
  // either legacy {dice,state} or a richer shape with explicit d1/d2 and the
  // computed break-point column. Both shapes coexist; game-ui.ts adapts.
  d1?: number;
  d2?: number;
  breakPoint?: number;
}

export interface ClaimWindowEntry {
  // Names the server pushes; render in 中文 primary + pinyin sublabel per
  // Default #5 (Vasquez Q5).
  available: Array<'Pung' | 'Chow' | 'Kong' | 'Hu'>;
  // Epoch ms when the claim window closes. Client auto-passes on expiry.
  deadline: number;
  // Discarding seat (0..3).
  source: number;
  // Tile ID being claimed (0..26 in Changsha).
  tile: number;
}

export interface ScoreDelta {
  seat: number;
  delta: number;
}

export interface HandResultEntry {
  // Winning seat (0..3) — only meaningful when type === 'Hu' or 'ZhaHu'.
  winner: number;
  type: 'Hu' | 'Draw' | 'ZhaHu';
  score: Array<ScoreDelta>;
  hand: Array<number>;
  nextBanker: number;
}

export interface DiceEntry {
  d1: number;
  d2: number;
  breakPoint: number;
}

// Phase F — server-pushed pickup affordance entry (singleton key=0).
// Drives the manual-pickup HUD, dice click visibility and per-tile click gate
// in world.ts. See Ripley Phase F design §2.4 for the protocol contract.
export interface PickupEntry {
  // 'rollDice' | 'breakPointMarked' | 'pickup-r1' | 'pickup-r2' | 'pickup-r3'
  //   | 'single' | 'dealer-extra' | 'inPlay'
  // Server emits Pascal-cased phase names (BreakPointMarked, PickupRound1, ...);
  // the bundle accepts either spelling — lowercase + dashes preferred for the
  // wire-format normalised by AutotableProtocol.PickupEntry, Pascal accepted
  // as a fallback so the bundle keeps working if the backend emits raw enum
  // names directly.
  phase: string;
  seatIndex: number;            // whose turn to click next (0..3)
  count: number;                // tiles to take this click (1 or 4)
  dealMode: DealMode;
  breakPoint?: number | null;   // wall column index of the break (for marker rendering)
  wallIndex?: number | null;    // which side of the table the break landed on (0..3)
}

export enum TileVariant {
  NO_LABELS = 'NO_LABELS',
  LABELS = 'LABELS',
}
