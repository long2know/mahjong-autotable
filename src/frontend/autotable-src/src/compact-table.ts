import { Box3, Vector3 } from 'three';
import { Size, type Place } from './types';

const CENTER = new Vector3(87, 87, 0);
const UP = new Vector3(0, 0, 1);
const SCALE = 2;
const WALL_SCALE = 2.2;
const FOREIGN_HAND_SCALE = 2;
const TILE_GAP = 0.08;
const GROUP_GAP = 0.8;
const HALF_GAP = 2;
const RIVER_WIDTH = 6 * Size.TILE.x + 5 * TILE_GAP;
const ROW_HALF_WIDTH = 108;
const HAND_MELD_GAP = 6;
const WALL_HAND_GAP = 2;

// Reserve four flat kongs before play. Wider sideways sets expand only the
// latched perimeter, not the camera on every draw or discard.
export const COMPACT_CAPACITY_WIDTH = 4 * (4 * Size.TILE.x + 3 * TILE_GAP) + 3 * GROUP_GAP;

export interface CompactSlot {
  readonly name: string;
  readonly group: string;
  readonly seat: number | null;
  readonly indexes: readonly number[];
}

export interface CompactSource {
  readonly slot: CompactSlot;
  readonly place: Place;
}

export interface CompactTableCounts {
  wall: number;
  seats: Array<{ seat: number; hand: number; discards: number; melds: number }>;
}

interface CompactOptions {
  ownSeat: number | null;
  handScale: number;
  hiddenHandSeat?: number | null;
  capacityWidth?: number;
}

interface MeldGroup {
  group: number;
  count: number;
  width: number;
  active: boolean;
}

function seatPosition(x: number, y: number, z: number, seat: number): Vector3 {
  return new Vector3(x, y, z).applyAxisAngle(UP, seat * Math.PI / 2).add(CENTER);
}

function corners(left: number, right: number, bottom: number, top: number, height: number, seat: number): Vector3[] {
  const result = [];
  for (const x of [left, right]) for (const y of [bottom, top]) for (const z of [0, height]) {
    result.push(seatPosition(x, y, z, seat));
  }
  return result;
}

/** Display geometry only: no IDs, tile faces, handles, slot writes or DOM. */
export class CompactTableProfile {
  readonly publicScale = SCALE;
  readonly fitCorners: readonly Vector3[];
  readonly feltBounds: Box3;
  readonly capacityWidth: number;
  readonly requiredWidth: number;
  private readonly ownSeat: number | null;
  private readonly handScale: number;
  private readonly wallScale: number;
  private readonly rowHalfWidth: number;
  private readonly handOffsets = new Map<number, number>();
  private readonly rowRadii = new Map<number, number>();
  private readonly positions = new Map<string, Vector3>();

  constructor(sources: readonly CompactSource[], options: CompactOptions) {
    this.ownSeat = options.ownSeat;
    this.handScale = options.handScale;
    const byName = new Map(sources.map(source => [source.slot.name, source]));
    const meldRows: MeldGroup[][] = [];
    const widthOf = (name: string, seat: number): number => {
      const size = byName.get(name)?.place.size;
      return size ? (seat % 2 === 0 ? size.x : size.y) : Size.TILE.x;
    };
    const depthOf = (name: string, seat: number): number => {
      const size = byName.get(name)?.place.size;
      return size ? (seat % 2 === 0 ? size.y : size.x) : Size.TILE.y;
    };
    let requiredWidth = COMPACT_CAPACITY_WIDTH;

    for (let seat = 0; seat < 4; seat++) {
      for (let row = 0; row < 3; row++) {
        let right = -HALF_GAP;
        for (let column = 5; column >= 0; column--) {
          const name = `discard.${row}.${column}@${seat}`;
          const width = widthOf(name, seat);
          this.positions.set(name, new Vector3(
            (right - width / 2) * SCALE,
            (-HALF_GAP - row * (Size.TILE.y + TILE_GAP) - depthOf(name, seat) / 2) * SCALE, 0));
          right -= width + TILE_GAP;
        }
        const rowWidth = -right - HALF_GAP - TILE_GAP;
        if (rowWidth > RIVER_WIDTH + 1e-6) {
          requiredWidth = Math.max(requiredWidth, rowWidth / RIVER_WIDTH * COMPACT_CAPACITY_WIDTH);
        }
      }

      const groups: MeldGroup[] = [];
      for (let group = 0; group < 4; group++) {
        const count = byName.has(`meld.${group}.3@${seat}`) ? 4 : 3;
        const active = [0, 1, 2, 3, 4].some(tile => byName.has(`meld.${group}.${tile}@${seat}`));
        let width = (count - 1) * TILE_GAP;
        for (let tile = 0; tile < count; tile++) width += widthOf(`meld.${group}.${tile}@${seat}`, seat);
        groups.push({ group, count, width, active });
      }
      const active = groups.filter(group => group.active);
      requiredWidth = Math.max(requiredWidth, active.reduce((sum, group) => sum + group.width, 0)
        + Math.max(0, active.length - 1) * GROUP_GAP);
      meldRows.push(groups);
    }
    this.requiredWidth = requiredWidth > COMPACT_CAPACITY_WIDTH + 1e-6 ? requiredWidth : COMPACT_CAPACITY_WIDTH;
    this.capacityWidth = options.capacityWidth ?? this.requiredWidth;
    const capacityRatio = this.capacityWidth / COMPACT_CAPACITY_WIDTH;
    this.wallScale = WALL_SCALE * capacityRatio;
    this.rowHalfWidth = ROW_HALF_WIDTH * capacityRatio;

    const fit = [];
    for (let seat = 0; seat < 4; seat++) {
      const groups = meldRows[seat];
      const active = groups.filter(group => group.active);
      const handScale = seat === this.ownSeat ? this.handScale : FOREIGN_HAND_SCALE;
      const visibleHand = seat !== options.hiddenHandSeat;
      const rowRadius = Math.max(this.rowHalfWidth,
        (this.capacityWidth * SCALE + (visibleHand ? 2 * Size.TILE.x * handScale + HAND_MELD_GAP : 0)) / 2,
        seat === this.ownSeat ? 7 * Size.TILE.x * handScale : 0);
      this.rowRadii.set(seat, rowRadius);
      const handWidth = visibleHand ? (14 - 3 * active.length) * Size.TILE.x * handScale : 0;
      const meldWidth = active.reduce((sum, group) => sum + group.width, 0)
        + Math.max(0, active.length - 1) * GROUP_GAP;
      const handGap = handWidth > 0 && active.length > 0 ? HAND_MELD_GAP : 0;
      const rowWidth = handWidth + handGap + meldWidth * SCALE;
      this.handOffsets.set(seat, -rowWidth / 2 + meldWidth * SCALE + handGap + 7 * Size.TILE.x * handScale);
      let left = -rowWidth / 2;
      for (const { group, count, active: occupied } of groups) {
        let edge = left;
        for (let tile = 0; tile < 4; tile++) {
          const name = `meld.${group}.${tile}@${seat}`;
          const width = widthOf(name, seat) * SCALE;
          this.positions.set(name, new Vector3(edge + width / 2,
            -rowRadius - WALL_HAND_GAP - depthOf(name, seat) * SCALE / 2, 0));
          if (tile < count) edge += width + TILE_GAP * SCALE;
        }
        const middle = this.positions.get(`meld.${group}.1@${seat}`)!;
        const base = byName.get(`meld.${group}.1@${seat}`)?.place;
        const upper = byName.get(`meld.${group}.4@${seat}`)?.place;
        this.positions.set(`meld.${group}.4@${seat}`, new Vector3(middle.x, middle.y,
          ((base?.position.z ?? Size.TILE.z / 2) + (base?.size.z ?? Size.TILE.z) / 2
            + (upper?.size.z ?? Size.TILE.z) / 2) * SCALE));
        if (occupied) left = edge - TILE_GAP * SCALE + GROUP_GAP * SCALE;
      }
      const depth = Math.max(Size.TILE.y * SCALE, Size.TILE.y * handScale);
      const height = Math.max(2 * Size.TILE.z * this.wallScale, Size.TILE.y * handScale);
      fit.push(...corners(-rowRadius, rowRadius,
        -rowRadius - WALL_HAND_GAP - depth, -rowRadius - WALL_HAND_GAP, height, seat));
    }
    this.fitCorners = fit;
    this.feltBounds = new Box3().setFromPoints(fit);
    this.feltBounds.min.z = 0;
    this.feltBounds.max.z = 0;
  }

  present(slot: CompactSlot, place: Place): Place {
    if (slot.seat === null) return place;
    if (slot.group === 'wall') {
      return {
        ...place,
        position: place.position.clone().sub(CENTER).multiplyScalar(this.wallScale).add(CENTER),
        size: place.size.clone().multiplyScalar(this.wallScale),
        scale: (place.scale ?? 1) * this.wallScale,
      };
    }
    if (slot.group === 'hand') {
      const own = slot.seat === this.ownSeat;
      const scale = own ? 1 : FOREIGN_HAND_SCALE;
      const size = place.size.clone().multiplyScalar(scale);
      const local = place.position.clone().sub(CENTER).applyAxisAngle(UP, -slot.seat * Math.PI / 2);
      if (!own || this.handScale === 1) local.x -= 1;
      local.multiplyScalar(scale);
      local.x += this.handOffsets.get(slot.seat)!;
      local.y = -this.rowRadii.get(slot.seat)! - WALL_HAND_GAP - (slot.seat % 2 === 0 ? size.y : size.x) / 2;
      return {
        ...place,
        position: local.applyAxisAngle(UP, slot.seat * Math.PI / 2).add(CENTER),
        size,
        scale: (place.scale ?? 1) * scale,
      };
    }
    if (slot.group !== 'discard' && slot.group !== 'meld') return place;
    const target = this.positions.get(slot.name);
    if (target === undefined) return place;
    // The upper tile follows the actual middle body's footprint and height,
    // including sideways/face-down rotations; its quaternion is never changed.
    const z = slot.group === 'meld' && slot.indexes[1] === 4 ? target.z : place.position.z * SCALE;
    return {
      ...place,
      position: seatPosition(target.x, target.y, z, slot.seat),
      size: place.size.clone().multiplyScalar(SCALE),
      scale: (place.scale ?? 1) * SCALE,
    };
  }
}
