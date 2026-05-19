import { shuffle } from "./utils";
import { Conditions, DealType, ThingType, GameType, GAME_TYPES } from "./types";
import { DEALS, DealPart } from "./setup-deal";
import { makeSlots } from "./setup-slots";
import { Slot } from "./slot";
import { Thing } from "./thing";


export class Setup {
  slots: Map<string, Slot> = new Map();
  slotNames: Array<string> = [];
  things: Map<number, Thing> = new Map();
  counters: Map<ThingType, number> = new Map();
  start: Record<ThingType, number> = {
    'TILE': 0,
    'STICK': 1000,
    'MARKER': 2000,
  }
  pushes: Array<[Slot, Slot]> = [];
  conditions!: Conditions;

  setup(conditions: Conditions): void {
    this.conditions = conditions;

    this.addSlots(conditions.gameType);
    this.addTiles(conditions);
    this.addMarker();
    this.deal(0);
  }

  private wallSlots(): Array<Slot> {
    return [...this.slots.values()].filter(
      slot => slot.name.startsWith('wall'));
  }

  private addTiles(conditions: Conditions): void {
    const wallSlots = this.wallSlots().map(slot => slot.name);
    this.maybeShuffle(wallSlots, conditions);
    let j = 0;
    for (let i = 0; i < 108; i++) {
      const tileIndex = this.tileIndex(i);
      this.addThing(ThingType.TILE, tileIndex, wallSlots[j++]);
    }
  }

  private maybeShuffle<T>(array: Array<T>, conditions?: Conditions): void {
    if ((conditions ?? this.conditions).dealType !== DealType.UNSHUFFLED) {
      shuffle(array);
    }
  }

  replace(conditions: Conditions): void {
    const whatReplace: Record<ThingType, boolean> = {
      TILE: conditions.gameType !== this.conditions.gameType,
      STICK: false,
      MARKER: conditions.gameType !== this.conditions.gameType,
    };

    const map = new Map<number, string>();
    for (const thing of [...this.things.values()]) {
      thing.prepareMove();
      if (whatReplace[thing.type]) {
        this.things.delete(thing.index);
      } else {
        map.set(thing.index, thing.slot.name);
      }
    }
    this.addSlots(conditions.gameType);
    if (whatReplace.TILE) {
      this.counters.set(ThingType.TILE, 0);
      this.addTiles(conditions);
    }
    if (whatReplace.MARKER) {
      this.counters.set(ThingType.MARKER, 0);
      this.addMarker();
    }

    for (const thing of this.things.values()) {
      if (!whatReplace[thing.type]) {
        const slotName = map.get(thing.index);
        if (slotName === undefined) {
          throw `couldn't recover slot name for thing ${thing.index}`;
        }
        const slot = this.slots.get(slotName);
        if (slot === undefined) {
          throw `trying to move thing to slot ${slotName}, but it doesn't exist`;
        }
        thing.moveTo(slot, thing.rotationIndex);
      }
    }
    this.conditions = conditions;
  }

  private tileIndex(i: number): number {
    // Changsha: logical tile = floor(i / 4) over 108 tiles → ids 0..26
    // (3 suits × 9 ranks × 4 copies). No honors, no red fives, no back colors.
    return Math.floor(i / 4);
  }

  deal(seat: number): [number, number] {
    const gameType = this.conditions.gameType;
    const dealType = this.conditions.dealType;

    const dice: [number, number] = [
      Math.floor(Math.random() * 6 + 1),
      Math.floor(Math.random() * 6 + 1)
    ];

    if (GAME_TYPES[gameType].seats.indexOf(seat) === -1) {
      seat = 0;
    }

    const dealParts = DEALS[gameType][dealType]!;

    const tiles = [...this.things.values()].filter(thing => thing.type === ThingType.TILE);
    for (const thing of tiles) {
      thing.prepareMove();
    }

    this.maybeShuffle(tiles);

    const roll = dice[0] + dice[1];
    for (const part of dealParts) {
      this.dealPart(part, tiles, roll, seat);
    }

    if (tiles.length !== 0) {
      throw `bad deal: ${tiles.length} remaining`;
    }

    return dice;
  }

  usesDice(): boolean {
    const gameType = this.conditions.gameType;
    const dealType = this.conditions.dealType;
    const dealParts = DEALS[gameType][dealType]!;
    for (const part of dealParts) {
      if (part.roll) return true;
    }
    return false;
  }

  private dealPart(dealPart: DealPart, tiles: Array<Thing>, roll: number, seat: number): void {
    if (dealPart.roll !== undefined && dealPart.roll !== roll) {
      return;
    }
    if (dealPart.tiles !== undefined) {
      const searched = [...dealPart.tiles];
      this.maybeShuffle(searched);

      for (let i = 0; i < searched.length; i++) {
        const idx = tiles.findIndex(tile => tile.typeIndex === searched[i]);
        if (idx === -1) {
          throw `not found: ${searched[i]}`;
        }
        const targetIdx = tiles.length - i - 1;
        const temp = tiles[targetIdx];
        tiles[targetIdx] = tiles[idx];
        tiles[idx] = temp;
      }
    }

    for (const [slotName, slotSeat, n] of dealPart.ranges) {
      if (tiles.length < n) {
        throw `tile underflow at ${slotName}`;
      }

      const idx = this.slotNames.indexOf(slotName);
      if (idx === -1) {
        throw `slot not found: ${slotName}`;
      }
      const effectiveSeat = dealPart.absolute ? slotSeat : (slotSeat + seat) % 4;
      for (let i = idx; i < idx + n; i++) {
        const targetSlotName = this.slotNames[i] + '@' + effectiveSeat;
        const slot = this.slots.get(targetSlotName);
        if (slot === undefined) {
          throw `slot not found: ${targetSlotName}`;
        }
        if (slot.thing !== null) {
          throw `slot occupied: ${targetSlotName}`;
        }

        const thing = tiles.pop()!;
        thing.moveTo(slot, dealPart.rotationIndex);
      }
    }
  }

  private addMarker(): void {
    this.addThing(ThingType.MARKER, 0, 'marker@0');
  }

  private addThing(
    type: ThingType,
    typeIndex: number,
    slotName: string,
    rotationIndex?: number
  ): void {
    if (this.slots.get(slotName) === undefined) {
      throw `Unknown slot: ${slotName}`;
    }

    const counter = this.counters.get(type) ?? 0;
    this.counters.set(type, counter + 1);
    const thingIndex = this.start[type] + counter;
    const slot = this.slots.get(slotName)!;

    const thing = new Thing(thingIndex, type, typeIndex, slot);
    this.things.set(thingIndex, thing);
    if (rotationIndex !== undefined) {
      thing.rotationIndex = rotationIndex;
    }
  }

  private addSlots(gameType: GameType): void {
    this.slots.clear();
    this.slotNames.splice(0);
    this.pushes.splice(0);

    const slotNames: Set<string> = new Set();
    for (const slot of makeSlots(gameType)) {
      this.slots.set(slot.name, slot);
      const shortName = slot.name.replace(/@.*/, '');
      if (!slotNames.has(shortName)) {
        slotNames.add(shortName);
      }
    }
    this.slotNames.push(...slotNames.values());
    Slot.setLinks(this.slots);

    this.pushes.push(...Slot.computePushes([...this.slots.values()]));
  }

  // Changsha scoring is server-authoritative numeric units, not physical sticks
  // (Vasquez §1.14). Phase B has no score state, so we return all nulls and the
  // center renderer skips the score draw (see center.ts:drawScore).
  // Phase C/D will replace this with a numeric-units readout sourced from the
  // changsha.scoring collection.
  getScores(): Array<number | null> {
    return [null, null, null, null, null];
  }
}
