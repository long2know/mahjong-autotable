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
  HANDS = 'HANDS',
  UNSHUFFLED = 'UNSHUFFLED',
}

export enum GameType {
  CHANGSHA = 'CHANGSHA',
}

interface GameTypeMeta {
  seats: Array<number>;
}

export const GAME_TYPES: Record<GameType, GameTypeMeta> = {
  CHANGSHA: { seats: [0, 1, 2, 3] },
};

export interface Conditions {
  gameType: GameType;
  dealType: DealType;
  baseUnit: number;
}

export namespace Conditions {
  export function initial(): Conditions {
    return {
      gameType: GameType.CHANGSHA,
      dealType: DealType.HANDS,
      baseUnit: 1,
    };
  }

  export function equals(a: Conditions, b: Conditions): boolean {
    return a.gameType === b.gameType && a.dealType === b.dealType && a.baseUnit === b.baseUnit;
  }

  export function describe(_ts: Conditions): string {
    return 'Changsha';
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
}

export enum TileVariant {
  NO_LABELS = 'NO_LABELS',
  LABELS = 'LABELS',
}
