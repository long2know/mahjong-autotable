import type { ThingInfo } from './types';

export type Seat = 0 | 1 | 2 | 3;

export type PublicTile = Readonly<{ slot: string } & (
  { kind: 'face'; face: number } | { kind: 'back' | 'unknown'; face: null }
)>;

export type PublicMeld = Readonly<{ group: number; tiles: readonly PublicTile[] }>;

export type PublicSeat = Readonly<{
  seat: Seat;
  discards: readonly PublicTile[];
  melds: readonly PublicMeld[];
}>;

export type PublicBoardSnapshot = Readonly<{
  viewerSeat: Seat | null;
  activeSeat: Seat | null;
  seats: readonly PublicSeat[];
  wallCount: number;
}>;

export type ReadonlyPublicContext = Readonly<
  { kind: 'claim' | 'discard'; seat: Seat; face: number; slot?: string } |
  { kind: 'waiting' | 'unavailable' }
>;

const SEATS = [0, 1, 2, 3] as const;
const SLOT = /^(discard|meld|wall)\.(0|[1-9]\d?)\.([0-5])@([0-3])$/;

function isSeat(value: unknown): value is Seat {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 3;
}

function projectTile(key: string | number, info: ThingInfo): PublicTile {
  const slot = info.slotName;
  if (typeof key === 'string') return Object.freeze({ slot, kind: 'back', face: null });

  const face = Object.prototype.hasOwnProperty.call(info, 'face') ? info.face : undefined;
  if (face === null) return Object.freeze({ slot, kind: 'back', face: null });
  if (Number.isInteger(key) && key >= 0 && key <= 107
    && typeof face === 'number' && Number.isInteger(face) && face >= 0 && face <= 26) {
    return Object.freeze({ slot, kind: 'face', face });
  }
  return Object.freeze({ slot, kind: 'unknown', face: null });
}

function addTile(tiles: Map<number, PublicTile>, position: number, tile: PublicTile): void {
  const previous = tiles.get(position);
  // Repeated occupancy is ambiguous even if the faces agree. Preserve one
  // unknown and an existing public slot, independently of iteration order.
  tiles.set(position, previous === undefined ? tile : Object.freeze({
    slot: previous.slot < tile.slot ? previous.slot : tile.slot,
    kind: 'unknown',
    face: null,
  }));
}

function orderedTiles(tiles: ReadonlyMap<number, PublicTile>): readonly PublicTile[] {
  return Object.freeze([...tiles].sort(([a], [b]) => a - b).map(([, tile]) => tile));
}

export function buildPublicBoard(
  entries: Iterable<[string | number, ThingInfo]>,
  viewerSeat: number,
  activeSeat: number | null,
): PublicBoardSnapshot {
  if (!isSeat(viewerSeat)) {
    return Object.freeze({
      viewerSeat: null, activeSeat: null, seats: Object.freeze([]), wallCount: 0,
    });
  }

  const boards = SEATS.map(seat => ({
    seat,
    discards: new Map<number, PublicTile>(),
    melds: new Map<number, Map<number, PublicTile>>(),
  }));
  const wallSlots = new Set<string>();

  for (const entry of entries) {
    if (!Array.isArray(entry) || entry.length !== 2) continue;
    const [key, info] = entry;
    if ((typeof key !== 'string' && typeof key !== 'number')
      || info === null || typeof info !== 'object' || typeof info.slotName !== 'string') continue;

    const slot = info.slotName;
    const match = SLOT.exec(slot);
    if (match === null || match[0] !== slot) continue;
    const first = Number(match[2]);
    const second = Number(match[3]);
    const seat = Number(match[4]) as Seat;

    if (match[1] === 'wall') {
      // AutotableSlotMap: 14/14/13/13 stacks, two layers; never inspect a wall face.
      if (first < (seat < 2 ? 14 : 13) && second <= 1) wallSlots.add(slot);
    } else if (match[1] === 'discard') {
      if (first <= 2) addTile(boards[seat].discards, first * 6 + second, projectTile(key, info));
    } else if (first <= 3 && second <= 4) {
      const melds = boards[seat].melds;
      let tiles = melds.get(first);
      if (tiles === undefined) {
        tiles = new Map<number, PublicTile>();
        melds.set(first, tiles);
      }
      // Added-kong slot .4 is the fourth tile, not a fifth; retain its real
      // public slot name so precise context highlights can still match it.
      addTile(tiles, Math.min(second, 3), projectTile(key, info));
    }
  }

  const seats = boards.map(({ seat, discards, melds }): PublicSeat => Object.freeze({
    seat,
    discards: orderedTiles(discards),
    melds: Object.freeze([...melds].sort(([a], [b]) => a - b)
      .map(([group, tiles]): PublicMeld => Object.freeze({ group, tiles: orderedTiles(tiles) }))),
  }));
  return Object.freeze({
    viewerSeat,
    activeSeat: isSeat(activeSeat) ? activeSeat : null,
    seats: Object.freeze(seats),
    wallCount: wallSlots.size,
  });
}
