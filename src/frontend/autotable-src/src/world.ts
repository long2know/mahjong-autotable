import { Vector3 } from "three";
import { Movement } from "./movement";
import { Client } from "./client";
import { readSpectatorFromUrl, readDealModeFromUrl } from "./client-ui";
import { mostCommon, rectangleOverlap, filterMostCommon, compareZYX } from "./utils";
import { MouseTracker } from "./mouse-tracker";
import { Setup } from './setup';
import { ObjectView, Render } from "./object-view";
import { SoundPlayer } from "./sound-player";
import {
  Conditions, ThingInfo, SoundType, Place, ThingType, Size, DealType,
  DiceInfo, GameType, PickupEntry,
} from "./types";
import { Slot } from "./slot";
import { Thing } from "./thing";


interface Select extends Place {
  id: any;
}

const SHIFT_TIME = 100;

export class World {
  private setup: Setup;

  private objectView: ObjectView;

  slots: Map<string, Slot>;
  things: Map<number, Thing>;
  private pushes: Array<[Slot, Slot]>;

  private hovered: Thing | null = null;
  private selected: Array<Thing> = [];
  private mouse: Vector3 | null = null;

  private movement: Movement | null = null;
  private heldMouse: Vector3 | null = null;
  mouseTracker: MouseTracker;

  soundPlayer: SoundPlayer;

  // Phase J Wave 1 — initial seat is 0 for the standard local-deal/non-
  // connected path (first-person view from seat 0).  For a page that
  // declares ?seat=-1 (spectator) we start at null so main-view.ts's
  // existing `fromTop = (seat === null)` branch puts the camera in the
  // top-down overhead pose from the very first frame, instead of briefly
  // flashing the seat-0 view until the WS sends the first seats update.
  // Non-spectator behaviour is unchanged.
  seat: number | null = readSpectatorFromUrl() ? null : 0;

  static WIDTH = 174;

  private client: Client;

  conditions: Conditions;

  // Phase F — latest server-pushed pickup affordance.  Drives the "your turn
  // to pick" gate: when set and `seatIndex === this.seat`, drag-start on a
  // wall tile is intercepted and `pickup.take` is emitted instead.
  private pickup: PickupEntry | null = null;
  private onPickupChanged: ((p: PickupEntry | null) => void) | null = null;

  // Phase K Wave 9 — Commentary tile-ref → 3D mesh outline.  W8 wired a
  // 2D CSS overlay; W9 adds the actual mesh-outline pulse alongside it.
  // The CSS overlay stays for spec parity (`commentary-tile-ref-latency`
  // observes `data-highlight-tile-id`) and as a fallback when the
  // requested tile is currently inside an InstancedMesh batch that the
  // ObjectView hasn't promoted to a per-tile mesh yet.
  //
  // `highlightedThing` is the Thing currently flagged for outline; the
  // World re-evaluates it every frame in `updateViewThings` and clears
  // it once `HIGHLIGHT_DURATION_MS` elapses.  Re-entry (a second
  // highlight while the first is in flight) resets the timer — the
  // most-recently-clicked chip wins.
  private highlightedThing: Thing | null = null;
  private highlightStartMs: number = 0;
  static readonly HIGHLIGHT_DURATION_MS = 2000;

  // Hicks playability iter2 — throttle for stale-occupant log lines (see
  // onThings).  Static across world instances; we only ever construct one
  // World at a time but a module-level epoch makes the suppression robust
  // to remount scenarios.
  private static _lastSlotConflictLogMs: number = 0;

  // Bishop W23 — generation counter for the manual-deal pickup chain.  Each
  // call to deal('HANDS') in manual mode bumps this; an in-flight chain
  // bails out as soon as its generation is stale, so back-to-back deals (or
  // re-entry from the playtest harness) don't fight each other for the
  // pickup wire.
  private manualDealChainGen: number = 0;

  constructor(objectView: ObjectView, soundPlayer: SoundPlayer, client: Client) {
    this.setup = new Setup();
    this.slots = this.setup.slots;
    this.things = this.setup.things;
    this.pushes = this.setup.pushes;
    this.conditions = Conditions.initial();
    this.setup.setup(this.conditions);

    this.objectView = objectView;
    this.setupView();

    this.client = client;
    this.mouseTracker = new MouseTracker(this.client);

    this.soundPlayer = soundPlayer;

    this.client.seats.on('update', this.onSeat.bind(this));
    this.client.things.on('update', this.onThings.bind(this));
    this.client.match.on('update', this.onMatch.bind(this));
    this.client.dice.on('update', this.onDice.bind(this));
    this.client.pickup.on('update', this.onPickup.bind(this));
    this.sendUpdate();
  }

  // TODO Phase D: banker rotation becomes server-authoritative; this client-side
  // dealer cycler must collapse to display-only once the changsha.banker
  // collection arrives.
  toggleDealer(): void {
    const match = this.client.match.get(0) ?? { dealer: 3, honba: 0, conditions: Conditions.initial()};
    match.dealer = (match.dealer + 1) % 4;
    this.client.match.set(0, match);
  }

  // Phase F — restored from upstream (98d4cca^).  Riichi honba counter
  // rotates through 0..7.  Hidden in Changsha (the button is `display: none`
  // when conditions.gameType === CHANGSHA).
  toggleHonba(): void {
    const match = this.client.match.get(0) ?? { dealer: 0, honba: 0, conditions: Conditions.initial() };
    match.honba = (match.honba + 1) % 8;
    this.client.match.set(0, match);
  }

  private onSeat(): void {
    this.seat = this.client.seat;
  }

  private onThings(entries: Array<[number, ThingInfo | null]>): void {
    const now = new Date().getTime();

    // Hicks playability iter2 — collect indexes that ARE in this batch so
    // the second pre-pass below can tell stale occupants apart from
    // intentional swaps within the batch.
    const batchIds = new Set<number>();
    for (const [thingIndex, thingInfo] of entries) {
      if (thingInfo !== null) batchIds.add(thingIndex);
    }

    for (const [thingIndex, thingInfo] of entries) {
      // TODO handle deletion
      if (thingInfo === null) {
        continue;
      }

      const thing = this.things.get(thingIndex);
      if (!thing) continue;
      thing.prepareMove();
    }

    // Hicks playability iter2 — defensive pre-pass for incremental batches.
    // The render queue can throw "slot not empty: <thing> <slot>" (~140
    // errors per playtest run) when an incremental UPDATE moves a tile
    // into a slot whose previous occupant isn't mentioned in the same
    // batch.  We force-displace that stale occupant here so moveTo()
    // below can succeed; the next things UPDATE will re-bind the
    // displaced tile to its true slot.
    for (const [thingIndex, thingInfo] of entries) {
      if (thingInfo === null) continue;
      const slot = this.slots.get(thingInfo.slotName);
      if (!slot || slot.thing === null) continue;
      // Already-cleared (its own thing moved away) or about to be reassigned
      // to the same tile? Leave it alone.
      if (slot.thing.index === thingIndex) continue;
      if (batchIds.has(slot.thing.index)) continue;
      // Stale occupant — force-displace.
      slot.thing.prepareMove();
    }

    for (const [thingIndex, thingInfo] of entries) {
      if (thingInfo === null) {
        continue;
      }

      const thing = this.things.get(thingIndex);
      if (!thing) continue;
      const slot = this.slots.get(thingInfo.slotName);
      if (!slot) continue;
      // Phase D — tile face privacy. Bishop's WS endpoint strips `face` to
      // null on tiles concealed from this viewer. The bundle defends against
      // a backend that forgets to flip rotationIndex by coercing the visible
      // rotation to a face-down index (the slot's last rotation, which by
      // setup-slots convention is the back-up orientation for hand slots).
      let rotationIndex = thingInfo.rotationIndex;
      if (thingInfo.face === null && slot.rotations.length > 1) {
        rotationIndex = slot.rotations.length - 1;
      }
      // Hicks playability iter2 — final guard.  If the slot is *still*
      // occupied at this point (e.g., two batch entries collided), skip the
      // move rather than throw.  We log once per ~second so the diagnostic
      // is preserved without spamming the console.
      if (slot.thing !== null && slot.thing !== thing) {
        if (now - World._lastSlotConflictLogMs > 1000) {
          World._lastSlotConflictLogMs = now;
          console.warn(
            `autotable: skipped stale moveTo ${thing.index} -> ${slot.name}`,
            `(occupant=${slot.thing.index})`,
          );
        }
        continue;
      }
      thing.moveTo(slot, rotationIndex);
      thing.sent = true;

      thing.claimedBy = thingInfo.claimedBy;
      thing.heldRotation.set(
        thingInfo.heldRotation.x,
        thingInfo.heldRotation.y,
        thingInfo.heldRotation.z,
        thingInfo.heldRotation.w,
      );

      const shiftSlot = thingInfo.shiftSlotName ? this.slots.get(thingInfo.shiftSlotName)! : null;
      if (thing.shiftSlot !== shiftSlot) {
        thing.lastShiftSlot = thing.shiftSlot;
        thing.lastShiftSlotTime = now;
        thing.shiftSlot = shiftSlot;
      }
    }
    this.checkPushes();
    this.sendUpdate();
  }

  private onMatch(): void {
    const match = this.client.match.get(0);
    if (!match) {
      return;
    }

    const conditions = match.conditions;
    if (!Conditions.equals(conditions, this.conditions)) {
      this.updateConditions(conditions);

      // Prevent selection persisting after deal
      this.selected.splice(0);
    }
  }

  private onDice(): void {
    const diceInfo = this.client.dice.get(0);
    if (!diceInfo) {
      return;
    }

    this.objectView;
  }

  // Phase F — runtime-pushed pickup affordance.  Stored locally so subsequent
  // drag-starts can intercept wall-tile clicks.  Game-ui registers a callback
  // via setPickupListener() so the HUD can render the "Take N" banner.
  private onPickup(): void {
    // The pickup collection is a singleton on key "current" (matching the
    // result/claim conventions in Phase F).  Outbound command keys
    // ('rollDice' / 'take' — ours to write, never to read back).  Only
    // "current" is authoritative.
    const entry = this.client.pickup.get('current') ?? null;
    this.pickup = entry;
    if (this.onPickupChanged) this.onPickupChanged(entry);
  }

  setPickupListener(fn: ((p: PickupEntry | null) => void) | null): void {
    this.onPickupChanged = fn;
  }

  /** True if the runtime currently expects this client's seat to click. */
  isMyPickupTurn(): boolean {
    return this.pickup !== null
        && this.seat !== null
        && this.pickup.seatIndex === this.seat
        && this.pickup.count > 0;
  }

  /** Convenience snapshot for the HUD. */
  currentPickup(): PickupEntry | null {
    return this.pickup;
  }

  /**
   * Phase F — dealer clicks the dice button.  Server-side this transitions
   * the RollingDice phase into BreakPointMarked + emits the dice roll.  The
   * bundle does NOT optimistically render dice; it waits for the server's
   * `dice` UPDATE.
   */
  emitRollDice(): void {
    if (this.seat === null) return;
    // Cast: the pickup collection is typed as PickupEntry but command-shaped
    // outbound entries deliberately differ (see client.ts comment).
    this.client.pickup.set('rollDice', { seatIndex: this.seat } as any);
  }

  /**
   * Phase F — player takes their next N wall tiles.  Backend interprets the
   * `count` against `state.PickupSeatIndex` and the current phase's expected
   * count; `wallTileIds` is informational (the runtime owns wall ordering).
   * Returns true if the emit was attempted.
   */
  emitTakePickup(): boolean {
    if (!this.isMyPickupTurn()) return false;
    const seatIndex = this.seat!;
    const count = this.pickup!.count;
    const wallTileIds = this.peekNextWallTileIds(count);
    this.client.pickup.set('take', { seatIndex, count, wallTileIds } as any);
    return true;
  }

  /**
   * Hicks playability iter2 — human click-to-discard.  Sends a discard
   * command on behalf of the local player.  The backend validates phase +
   * active seat; an out-of-turn click is a no-op server-side.  The resulting
   * tile-move animation comes back through the normal `things` UPDATE
   * channel (matching the bot autoplay visual).
   *
   * Accepts either a {@link Thing} (canonical, validated against the local
   * hand slot) or a numeric tileId (Vasquez Gap 4 informational backdoor:
   * playtest harnesses and external drivers can pass the raw id without
   * needing to look up the Thing first).  When a numeric id is provided we
   * look up the Thing locally for the same hand-slot validation; if the
   * tile isn't in our local view we still emit the discard (the backend's
   * `TryHandleDiscardActionAsync` is the authoritative validator).
   */
  emitDiscard(tileOrId: Thing | number): boolean {
    if (this.seat === null) return false;
    let tileId: number;
    let tile: Thing | undefined;
    if (typeof tileOrId === 'number') {
      tileId = tileOrId;
      tile = this.things.get(tileId);
    } else {
      tile = tileOrId;
      tileId = tile.index;
    }
    if (tile && (tile.slot.group !== 'hand' || tile.slot.seat !== this.seat)) {
      return false;
    }
    this.client.discard.set(this.seat, { tileId });
    return true;
  }

  /**
   * Hicks playability iter2 — heuristic: this seat has an extra tile in
   * hand (more than 13 concealed) AND no pickup affordance is pending,
   * so the player must discard to continue the turn.  Used to gate the
   * click-to-discard intercept in {@link onDragStart} so a casual drag
   * of a tile in-place between draws doesn't accidentally discard.
   */
  hasExtraHandTile(): boolean {
    if (this.seat === null) return false;
    if (this.isMyPickupTurn()) return false;
    let count = 0;
    for (const thing of this.things.values()) {
      if (thing.slot.group === 'hand' && thing.slot.seat === this.seat) {
        count++;
        if (count > 13) return true;
      }
    }
    return false;
  }

  /**
   * Best-effort snapshot of the next-N tile IDs in our local wall ordering.
   * Walks the canonical wall slot order (`wall.<col>.<row>@<seat>`) and
   * returns the first N occupied slots' `thing.index` values.  The backend
   * is authoritative for what those tiles actually are — this is purely
   * informational so test scripts and orchestration logs can confirm the
   * client picked the expected tile group.
   */
  private peekNextWallTileIds(count: number): number[] {
    if (count <= 0) return [];
    // Sort wall slot names so we walk a deterministic order.  Slot names
    // look like `wall.<col>.<row>@<seat>`; sorting lexicographically gets
    // us close-enough to the original deal order.
    const wallSlots = [...this.slots.values()]
      .filter(s => s.group === 'wall' && s.thing !== null)
      .sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
    const ids: number[] = [];
    for (const slot of wallSlots) {
      if (slot.thing) {
        ids.push(slot.thing.index);
        if (ids.length >= count) break;
      }
    }
    return ids;
  }

  updateConditions(conditions: Conditions): void {
    this.conditions = conditions;
    this.setup.replace(conditions);
    this.setupView();
  }

  private sendUpdate(full?: boolean): void {
    const entries: Array<[number, ThingInfo | null]> = [];
    if (full) {
      for (const thing of this.things.values()) {
        entries.push([thing.index, this.describeThing(thing)]);
        thing.sent = true;
      }
      for (const [index,] of this.client.things.entries()) {
        if (!this.things.has(index)) {
          entries.push([index, null]);
        }
      }
      this.client.things.update(entries);
    } else {
      for (const thing of this.things.values()) {
        if (!thing.sent) {
          const desc = this.describeThing(thing);
          if (JSON.stringify(desc) !== JSON.stringify(this.client.things.get(thing.index))) {
            entries.push([thing.index, desc]);
          }
          thing.sent = true;
        }
      }
      if (entries.length > 0) {
        this.client.things.update(entries);
      }
    }
  }

  private sendMouse(): void {
    if (this.seat !== null) {
      this.mouseTracker.update(this.mouse, this.heldMouse);
    }
  }

  private describeThing(thing: Thing): ThingInfo {
    return {
      slotName: thing.slot.name,
      rotationIndex: thing.rotationIndex,
      claimedBy: thing.claimedBy,
      heldRotation:
        {
          x: thing.heldRotation.x,
          y: thing.heldRotation.y,
          z: thing.heldRotation.z,
          w: thing.heldRotation.w,
      },
      shiftSlotName: thing.shiftSlot?.name ?? null,
    };
  }

  /**
   * Phase F — Deal entry point.  Game-ui passes any conditions overrides
   * (gameType / fives / points / dealMode) so the dropdown UI can mutate the
   * conditions before the deal happens in one transaction.
   */
  deal(dealType: DealType, overrides: Partial<Conditions> = {}): void {
    if (this.seat === null) {
      return;
    }

    for (const thing of this.things.values()) {
      thing.release();
    }
    this.selected.splice(0);
    this.checkPushes();

    // Phase F — upstream's deal toggles the tile-back colour every deal so
    // shuffling order is visible to humans.  Carry the toggle forward for
    // non-Changsha variants; Changsha pins back=0 always.
    const isChangsha = (overrides.gameType ?? this.conditions.gameType) === GameType.CHANGSHA;
    const nextBack = isChangsha ? 0 : (1 - this.conditions.back);

    const conditions: Conditions = {
      ...this.conditions,
      ...overrides,
      back: nextBack,
      dealType,
    };

    // Changsha has no honba (Vasquez §1.9); pin to 0 so the center renderer
    // never paints it.  Riichi variants get the upstream's running honba.
    const existingMatch = this.client.match.get(0);
    const honba = isChangsha ? 0 : (existingMatch?.honba ?? 0);
    const match = { dealer: this.seat, honba, conditions };

    this.updateConditions(conditions);
    const dice = this.setup.deal(this.seat);
    const diceInfo: DiceInfo = { dice, state: this.setup.usesDice() ? 'rolled' : 'ignore' };

    this.client.transaction(() => {
      this.client.match.set(0, match);
      this.client.dice.set(0, diceInfo);
      this.sendUpdate(true);
    });

    // Bishop W23 — manual-deal pickup chain.  In manual mode Bishop's runtime
    // parks in RollingDice after the implicit Deal trigger and waits for
    // per-round `pickup` emissions from the seated client(s).  Drive the
    // dealer-side chain (1 × rollDice + 4 × take: 3 rounds of 4 tiles +
    // 1 final round of 1 tile per Changsha v1.2 §6.3) so the human-led
    // table actually reaches the play loop.  Auto-mode deals are unchanged.
    // Spectators (seat === null guard above) and non-HANDS deals skip the
    // chain too.  The runtime auto-handles bot seats between our turns
    // (ChangshaGameRuntime.ScheduleBotIfNeededAsync), so we only emit for
    // our own seat.
    //
    // We accept either source-of-truth for dealMode: the local conditions
    // object (if the picker / overrides set it) OR the URL `?dealMode=`
    // param (which is what the WS connection forwards to the runtime).
    // The round-tripped match snapshot from the server STRIPS dealMode
    // (ChangshaToAutotableTranslator.BuildMatch only emits gameType/back/
    // fives/points/dealType), so once the first server match push lands
    // `this.conditions.dealMode` becomes undefined.  The URL fallback
    // keeps the chain firing regardless.  The runtime will silently
    // reject `rollDice` if the table is in auto mode, so blind-firing
    // is safe.
    const urlDealMode = readDealModeFromUrl();
    const effectiveDealMode = conditions.dealMode ?? urlDealMode;
    if (dealType === DealType.HANDS && effectiveDealMode === 'manual') {
      const gen = ++this.manualDealChainGen;
      void this.driveManualDealChain(gen);
    }
  }

  /**
   * Bishop W23 — drive the dealer-side pickup chain for the local seat in
   * manual-deal mode.  After the implicit Deal trigger the runtime parks
   * in RollingDice with NO pickup affordance broadcast (the translator
   * gates pickup emissions on {@link IsPickupPhase} which excludes
   * RollingDice — see ChangshaToAutotableTranslator.cs §pickup).  So we
   * blind-emit `rollDice` first; the runtime rejects it server-side if
   * we aren't the dealer (silent debug log).  Once it accepts, pickup
   * state pushes through and we drive 4 `take` rounds (3 × 4 tiles +
   * 1 × 1 tile per Changsha v1.2 §6.3).  Bots autoplay their own pickup
   * rounds server-side via ScheduleBotIfNeededAsync between our turns,
   * so this loop only handles the local seat.
   *
   * Cancels itself if a newer chain has bumped {@link manualDealChainGen}.
   */
  private async driveManualDealChain(gen: number): Promise<void> {
    const seat = this.seat;
    if (seat === null) return;

    // 1) Give the server time to process the implicit Deal trigger
    //    (match push → ApplyDealMode(manual) → StartGameAsync parks the
    //    runtime in RollingDice).  Without this gap the rollDice emit
    //    races the runtime's transition and is dropped as
    //    "wrong-phase" on the server.
    await new Promise<void>(r => setTimeout(r, 300));
    if (this.manualDealChainGen !== gen) return;

    // 2) Emit rollDice unconditionally.  On hand 1 the dealer is seat 0
    //    (Changsha §6.2) and the human-led playtest takes the first
    //    visible seat, so the local seat IS the dealer in the common
    //    path.  If we aren't the dealer the runtime's RollDiceAsync
    //    throws and TryHandlePickupActionAsync swallows it at debug
    //    level — no client-visible side effect.
    this.emitRollDice();

    // 3) Four take rounds: 3 × 4-tile (PickupRound1..3) + 1 × 1-tile
    //    (SingleTilePickup or DealerExtra; the runtime collapses both
    //    into a single 1-tile pickup affordance for our seat).  Between
    //    our turns the runtime cycles through the bot seats via
    //    ScheduleBotIfNeededAsync; we just re-wait for our seat to come
    //    up again.  The 12s timeout per round accommodates the bot
    //    pickup delay (BotPickupDelayMs default) × 3 bots + slack.
    for (let round = 0; round < 4; round++) {
      const myTurn = await this.waitForPickup(
        gen,
        p => p !== null && p.seatIndex === seat && p.count > 0,
        12000,
      );
      if (!myTurn) return;
      if (this.manualDealChainGen !== gen) return;
      const ok = this.emitTakePickup();
      if (!ok) return;
      await new Promise<void>(r => setTimeout(r, 120));
    }
  }

  /**
   * Bishop W23 — poll-based wait for a pickup-state predicate.  Returns
   * true when the predicate becomes truthy, false on timeout or if a
   * newer chain has cancelled this one.  Collection doesn't expose an
   * `off` for one-shot listeners so we poll the locally-cached
   * `this.pickup` (refreshed by {@link onPickup}) at ~60ms cadence.
   */
  private waitForPickup(
    gen: number,
    pred: (p: PickupEntry | null) => boolean,
    timeoutMs: number,
  ): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      if (this.manualDealChainGen !== gen) return resolve(false);
      if (pred(this.pickup)) return resolve(true);
      const startMs = Date.now();
      const timer = setInterval(() => {
        if (this.manualDealChainGen !== gen) {
          clearInterval(timer);
          resolve(false);
          return;
        }
        if (pred(this.pickup)) {
          clearInterval(timer);
          resolve(true);
          return;
        }
        if (Date.now() - startMs > timeoutMs) {
          clearInterval(timer);
          resolve(false);
        }
      }, 60);
    });
  }

  /**
   * Phase F — restored from upstream (98d4cca^).  Force-replaces the per-seat
   * stick trays for a new starting-points selection.  Changsha is a no-op
   * (the variant doesn't render sticks).
   */
  resetPoints(points: Conditions['points']): void {
    if (this.conditions.gameType === GameType.CHANGSHA) return;

    for (const thing of this.things.values()) {
      thing.release();
    }
    this.selected.splice(0);
    this.checkPushes();

    const conditions: Conditions = { ...this.conditions, points };
    this.setup.replace(conditions, /*replacePoints*/ true);
    this.setupView();
    this.conditions = conditions;

    const existingMatch = this.client.match.get(0)
      ?? { dealer: 0, honba: 0, conditions: this.conditions };
    const match = { ...existingMatch, conditions };

    this.client.transaction(() => {
      this.client.match.set(0, match);
      this.sendUpdate(true);
    });
  }

  private isHolding(): boolean {
    if (this.seat === null) {
      return false;
    }

    for (const thing of this.things.values()) {
      if (thing.claimedBy === this.seat) {
        return true;
      }
    }
    return false;
  }

  onHover(id: any): void {
    if (!this.isHolding()) {
      this.hovered = id === null ? null : this.things.get(id as number)!;

      if (this.hovered !== null && !this.canSelect(this.hovered, [])) {
        this.hovered = null;
      }
    }
  }

  onSelect(ids: Array<any>): void {
    this.selected = ids.map(id => this.things.get(id as number)!);
    this.selected = this.selected.filter(
      thing => this.canSelect(thing, this.selected));

    if (this.selected.length === 0) {
      return;
    }

    this.selected = filterMostCommon(this.selected, thing => thing.slot.group + '@' + thing.slot.seat);
  }

  onMove(mouse: Vector3 | null): void {
    if ((this.mouse === null && mouse === null) ||
        (this.mouse !== null && mouse !== null && this.mouse.equals(mouse))) {
      return;
    }

    this.mouse = mouse;
    this.sendMouse();

    this.drag();
    this.sendUpdate();
  }

  private drag(): void {
    if (this.mouse === null || this.heldMouse === null) {
      return;
    }

    this.movement = new Movement();

    const held: Array<Thing> = [];

    for (const thing of this.things.values()) {
      if (thing.claimedBy === this.seat) {
        if (thing.shiftSlot !== null) {
          thing.release();
        } else {
          held.push(thing);
        }
      }
    }
    // this.things.filter(thing => thing.claimedBy === this.seat);
    held.sort((a, b) => compareZYX(a.slot.origin, b.slot.origin));

    for (let i = 0; i < held.length; i++) {
      const thing = held[i];
      const place = thing.place();
      const x = place.position.x + this.mouse.x - this.heldMouse.x;
      const y = place.position.y + this.mouse.y - this.heldMouse.y;

      const targetSlot = this.findSlot(x, y, place.size.x, place.size.y, thing.type);
      if (targetSlot === null) {
        this.movement = null;
        return;
      }
      this.movement.move(thing, targetSlot);
    }

    const relevantThings = [...this.things.values()].filter(thing =>
      thing.type === held[0].type
    );
    if (!this.movement.findShift(relevantThings, [
      slot => slot.links.shiftLeft ?? null,
      slot => slot.links.shiftRight ?? null,
    ])) {
      this.movement = null;
      return;
    }
    this.movement.rotateHeld();
    this.movement.applyShift(this.seat!);
  }

  private canSelect(thing: Thing, otherSelected: Array<Thing>): boolean {
    const upSlot = thing.slot.links.up;
    if (upSlot && upSlot.thing !== null) {
      if (otherSelected.indexOf(upSlot.thing) !== -1) {
        // the player is also selecting the tile above, let them pick it up
        return true;
      }
      if (upSlot.thing.claimedBy !== null) {
        // someone else is holding this tile
        return true;
      }
      return false;
    }
    return true;
  }

  private findSlot(x: number, y: number, w: number, h: number, thingType: ThingType): Slot | null {
    const minOverlap = 1;
    let bestOverlap = minOverlap ;
    let bestSlot = null;

    // Empty slots
    for (const slot of this.slots.values()) {
      if (slot.type !== thingType) {
        continue;
      }

      if (slot.thing !== null && slot.thing.claimedBy !== this.seat) {
        // Occupied. But can it be potentially shifted?
        if (!slot.links.shiftLeft && !slot.links.shiftRight) {
          continue;
        }
      }
      // Already proposed for another thing
      if (this.movement?.hasSlot(slot)) {
        continue;
      }
      // The slot requires other slots to be occupied first
      if (slot.links.requires && slot.links.requires.thing === null) {
        continue;
      }

      const place = slot.placeWithOffset(0);

      const margin = Size.TILE.x / 2;
      const overlap1 = rectangleOverlap(
        x, y, w, h,
        place.position.x, place.position.y, place.size.x, place.size.y,
      );
      const overlap2 = rectangleOverlap(
        x, y, w + margin, h + margin,
        place.position.x, place.position.y, place.size.x + margin, place.size.y + margin,
      );
      const overlap = overlap1 + overlap2 * 0.5;
      if (overlap > bestOverlap) {
        bestOverlap = overlap;
        bestSlot = slot;
      }
    }
    return bestSlot;
  }

  onDragStart(): boolean {
    if (this.seat === null) {
      return false;
    }

    // Phase F — manual-pickup intercept.  When the runtime expects this seat
    // to pick the next N wall tiles, a drag-start on a wall tile becomes a
    // pickup.take emit instead of a free-drag.  We do NOT optimistically
    // move the tile — the backend will respond with a `things` UPDATE that
    // places the tile into the hand.
    if (this.hovered !== null
        && this.hovered.slot.group === 'wall'
        && this.isMyPickupTurn()) {
      this.emitTakePickup();
      this.hovered = null;
      this.selected.splice(0);
      return false;
    }

    // Hicks playability iter2 — click-to-discard.  When the local player has
    // an extra tile in hand (>13 concealed) and clicks on one of their own
    // hand tiles, treat the click as a single-action discard instead of a
    // drag.  Matches "playing in person" semantics: tap a tile, it goes to
    // the discard area.  The backend validates phase + active-seat — an
    // off-turn click is silently dropped server-side.
    if (this.hovered !== null
        && this.hovered.slot.group === 'hand'
        && this.hovered.slot.seat === this.seat
        && this.hasExtraHandTile()) {
      const tile = this.hovered;
      this.emitDiscard(tile);
      this.hovered = null;
      this.selected.splice(0);
      return false;
    }

    if (this.hovered !== null && !this.isHolding()) {
      let toHold;
      if (this.selected.indexOf(this.hovered) !== -1) {
        toHold = [...this.selected];
      } else {
        toHold = [this.hovered];
        this.selected.splice(0);
      }

      toHold = toHold.filter(thing => thing.claimedBy === null);

      for (const thing of toHold) {
        thing.hold(this.seat);
      }
      this.hovered = null;
      this.heldMouse = this.mouse;

      this.drag();
      this.sendMouse();
      this.sendUpdate();

      return true;
    }
    return false;
  }

  onDragEnd(): void {
    if (this.isHolding()) {
      if (this.heldMouse !== null && this.mouse !== null &&
          this.heldMouse.equals(this.mouse)) {

        // No movement; unselect
        this.selected.splice(0);
        this.dropInPlace();
        // if (this.hovered !== null) {
        //   this.selected.push(this.hovered);
        // }
      } else if (this.canDrop()) {
        // Successful movement
        this.drop();
      } else {
        this.dropInPlace();
      }
    }

  }

  onFlip(direction: number, animated?: boolean): void {
    if (this.isHolding()) {
      return;
    }

    if (this.selected.length > 0) {
      const rotationIndex = mostCommon(this.selected, thing => thing.rotationIndex)!;
      const toFlip = [];
      for (const thing of this.selected) {
        if (this.selected.length === 1 || thing.slot.canFlipMultiple) {
          toFlip.push(thing);
        }
      }
      if (toFlip.length > 1 && animated) {
        toFlip.sort((a, b) => a.slot.name.localeCompare(b.slot.name, undefined, { numeric: true }));
        this.flipAnimated(toFlip, 0, rotationIndex + direction);
      } else {
        for (const thing of toFlip) {
          thing.flip(rotationIndex + direction);
        }
        this.checkPushes();
        this.selected.splice(0);
      }
    } else if (this.hovered !== null) {
      this.hovered.flip(this.hovered.rotationIndex + direction);
      this.sendUpdate();
      this.checkPushes();
    }
    this.sendUpdate();

  }

  private flipAnimated(things: Array<Thing>, i: number, rotationIndex: number): void {
    const thing = things[i];
    if (this.selected.indexOf(things[i]) === -1) {
      this.selected.splice(0);
      return;
    }
    thing.flip(rotationIndex);
    this.sendUpdate();
    if (i + 1 < things.length) {
      setTimeout(() => this.flipAnimated(things, i + 1, rotationIndex), 100);
    } else {
      this.selected.splice(0);
    }
  }

  private drop(): void {
    if(!this.movement) {
      return;
    }

    const sourceSlots = [];
    let discardSide = null;
    for (const thing of this.movement.things()) {
      const source = thing.slot;
      const target = this.movement.get(thing)!;
      if (target.group === 'discard' &&
        !(source.group === 'discard' && source.seat === target.seat)) {
        discardSide = target.seat;
      }
      sourceSlots.push(source);
    }

    this.movement.apply();
    this.checkPushes();
    this.finishDrop(sourceSlots);

    if (discardSide !== null) {
      this.soundPlayer.play(SoundType.DISCARD, discardSide);
    }
  }

  private dropInPlace(): void {
    this.finishDrop([]);
  }

  private finishDrop(sourceSlots: Array<Slot>): void {
    const targetSlots = [];
    for (const thing of this.things.values()) {
      if (thing.claimedBy === this.seat) {
        thing.release();
        targetSlots.push(thing.slot);
      }
    }
    this.selected.splice(0);
    this.heldMouse = null;
    this.movement = null;

    for (const slot of sourceSlots) {
      if (slot.links.up) {
        this.dropDown(slot.links.up);
      }
    }
    for (const slot of targetSlots) {
      this.dropDown(slot);
    }

    this.sendUpdate();
    this.sendMouse();
  }

  private dropDown(slot: Slot): void {
    const thing = slot.thing;
    if (thing && thing.claimedBy === null) {
      const downSlot = slot.links.down;
      if (downSlot && downSlot.thing === null) {
        thing.prepareMove();
        thing.moveTo(downSlot);
      }
    }
  }

  private canDrop(): boolean {
    return this.movement ? this.movement.valid() : false;
  }

  private checkPushes(): void {
    for (const [source, target] of this.pushes) {
      target.handlePush(source);
    }
  }

  updateView(): void {
    this.updateViewThings();
    this.updateViewDropShadows();
    this.objectView.updateScores(this.setup.getScores());
  }

  private updateViewThings(): void {
    const toRender: Array<Render> = [];
    const canDrop = this.canDrop();
    const now = new Date().getTime();

    // Phase K Wave 9 — Compute the commentary highlight envelope once
    // per frame.  When the elapsed time exceeds the pulse window we
    // clear `highlightedThing` so the Render flag below stays in
    // sync with `objectView.highlightIntensity` and the outline pool
    // drops the mesh next frame.
    let highlightIntensity = 0;
    if (this.highlightedThing !== null) {
      const elapsed = now - this.highlightStartMs;
      if (elapsed >= 0 && elapsed < World.HIGHLIGHT_DURATION_MS) {
        // Two sin-wave cycles over the 2 s window with a linear
        // fade-out envelope so the pulse decays as it expires.  The
        // 0.5 baseline keeps the outline visible at the troughs
        // (avoids a flicker-to-zero that would read as a bug).
        const t = elapsed / World.HIGHLIGHT_DURATION_MS;
        const wave = 0.5 + 0.5 * Math.sin(t * Math.PI * 4);
        highlightIntensity = wave * (1 - t);
      } else {
        this.highlightedThing = null;
      }
    }
    this.objectView.highlightIntensity = highlightIntensity;

    for (const thing of this.things.values()) {
      let place = thing.place();

      if (thing.claimedBy !== null && thing.shiftSlot === null) {
        let mouse = null, heldMouse = null;
        if (thing.claimedBy === this.seat) {
          mouse = this.mouse;
          heldMouse = this.heldMouse;
        } else {
          mouse = this.mouseTracker.getMouse(thing.claimedBy, now);
          heldMouse = this.mouseTracker.getHeld(thing.claimedBy);
        }

        if (mouse && heldMouse) {
          place = {
            ...place,
            position: place.position.clone(),
            rotation: thing.heldRotation.clone(),
          };
          place.position.x += mouse.x - heldMouse.x;
          place.position.y += mouse.y - heldMouse.y;
        }
      } else if (thing.lastShiftSlotTime >= now - SHIFT_TIME) {
        if (thing.lastShiftSlot !== null) {
          place = thing.lastShiftSlot.places[thing.rotationIndex];
        }
      } else if (thing.claimedBy !== null && thing.shiftSlot !== null) {
        place = thing.shiftSlot.places[thing.rotationIndex];
      }

      const held = thing.claimedBy !== null && thing.shiftSlot === null;
      const selected = this.selected.indexOf(thing) !== -1;
      const hovered = thing === this.hovered ||
        (selected && this.selected.indexOf(this.hovered!) !== -1);
      const temporary = held && thing.claimedBy === this.seat && !canDrop;

      const slot = thing.slot;

      const bottom =
        !held &&
        slot.links.up !== undefined &&
        (slot.links.up.thing === null ||
         slot.links.up.thing.claimedBy !== null);

      // Phase K Wave 9 — Flag the commentary-highlighted Thing so the
      // ObjectView force-promotes it to a per-tile Mesh (the outline
      // hull needs a stable Mesh reference; an InstancedMesh draw call
      // has no per-tile Object3D to attach to).
      const highlighted = thing === this.highlightedThing
        && highlightIntensity > 0;

      toRender.push({
        type: thing.type,
        thingIndex: thing.index,
        place,
        selected,
        hovered,
        held,
        temporary,
        bottom,
        highlighted,
      });
    }
    this.objectView.updateThings(toRender);
  }

  private updateViewDropShadows(): void {
    const places = [];
    if (this.canDrop()) {
      for (const slot of this.movement!.slots()) {
        places.push(slot.placeWithOffset(0));
      }
    }
    this.objectView.updateDropShadows(places);
  }

  toSelect(): Array<Select> {
    const result = [];
    if (this.seat !== null && !this.isHolding()) {
      for (const thing of this.things.values()) {
        if (thing.claimedBy === null) {
          const place = thing.place();
          result.push({...place, id: thing.index});
        }
      }
    }
    return result;
  }

  setupView(): void {
    this.objectView.replaceThings(this.things);

    const places = [];
    for (const slot of this.slots.values()) {
      if (slot.drawShadow) {
        places.push(slot.places[slot.shadowRotation]);
      }
    }
    this.objectView.replaceShadows(places);
  }

  /**
   * Phase K Wave 9 — Map a commentary-record tile id (wire format
   * `"man5"`, `"pin3"`, `"sou9"`, `"east"`, `"red"`, `"3b"`, etc.)
   * to the first Thing on the table whose face matches.  Returns
   * `null` when the id is unparsable OR no in-play tile carries
   * that face (e.g. the tile is currently in a dead-wall slot the
   * spectator can't observe).
   *
   * The face-id format mirrors Bishop's `CommentaryRecord
   * .TileReferences` doc-comment: `<suit><rank>` for numbered tiles
   * (`man`/`pin`/`sou` × 1..9 — also accepts the legacy single-
   * letter `m`/`p`/`s` notation `"5m"` / `"3b"`) and a fixed
   * vocabulary for honors (`east|south|west|north|white|green|red`
   * — also accepts `wind-e` / `dragon-w` shorthands).  The red-five
   * tiles map to the dedicated typeIndex slots 34/35/36 (set up by
   * `setup.ts:tileIndex` under `fives='121'`).
   *
   * Lookup is `typeIndex % 37` to ignore the per-deal back-color
   * cycling (W8 setup adds `37 * conditions.back` to mix the two
   * tile-back textures).
   */
  findThingByFace(tileId: string): Thing | null {
    const face = parseTileFace(tileId);
    if (face === null) return null;
    for (const thing of this.things.values()) {
      if (thing.type !== ThingType.TILE) continue;
      if ((thing.typeIndex % 37) === face) {
        return thing;
      }
    }
    return null;
  }

  /**
   * Phase K Wave 9 — Set (or clear) the commentary-highlight target.
   * The pulse runs for `HIGHLIGHT_DURATION_MS` (2 s) from the moment
   * this is called; re-entry resets the timer so the
   * most-recently-clicked chip wins.  Passing `null` cancels the
   * highlight on the next frame.
   */
  setHighlightedThing(thing: Thing | null): void {
    this.highlightedThing = thing;
    this.highlightStartMs = thing === null ? 0 : new Date().getTime();
  }
}

/**
 * Phase K Wave 9 — Parse a commentary wire-string tile id to its
 * canonical face index (`0..36`).  Returns `null` for unknown / empty
 * input.  Mirrors the standard mahjong notation Bishop's commentary
 * generator emits per `ICommentaryGenerator.cs:110-113`.
 *
 * Layout (set up by `setup.ts:tileIndex`, mirrors the texture atlas
 * row-major order at 8 cols/row):
 *
 *   • 0..8   → `man1`..`man9`   (characters / wan-zu)
 *   • 9..17  → `pin1`..`pin9`   (dots / pin-zu)
 *   • 18..26 → `sou1`..`sou9`   (bamboo / sou-zu)
 *   • 27..30 → `east|south|west|north`   (winds, ESWN order)
 *   • 31..33 → `white|green|red`         (dragons, WGR order)
 *   • 34..36 → `red-man5|red-pin5|red-sou5` (aka-dora red fives)
 *
 * Tolerated shorthands (commentary text is LLM-generated and not
 * pinned to one spelling):
 *   • Suit-first: `"man5"`, `"pin3"`, `"sou9"`
 *   • Rank-first: `"5m"`, `"3p"`, `"9s"`, `"3b"` (b = bamboo = sou)
 *   • Single-letter suits: `m`, `p`, `s`, `b` (bamboo), `c` (man)
 *   • Honour aliases: `east|east-wind|wind-e|e`, `white|haku`, etc.
 */
export function parseTileFace(tileId: string): number | null {
  if (typeof tileId !== 'string') return null;
  const id = tileId.trim().toLowerCase();
  if (id === '') return null;

  // Honor tiles first — fixed vocabulary, no rank suffix.
  const winds: Record<string, number> = {
    east: 27, e: 27, 'wind-e': 27, 'east-wind': 27, ton: 27,
    south: 28, s: 28, 'wind-s': 28, 'south-wind': 28, nan: 28,
    west: 29, w: 29, 'wind-w': 29, 'west-wind': 29, sha: 29,
    north: 30, n: 30, 'wind-n': 30, 'north-wind': 30, pei: 30,
  };
  if (winds[id] !== undefined) return winds[id];
  const dragons: Record<string, number> = {
    white: 31, haku: 31, 'dragon-w': 31, 'white-dragon': 31,
    green: 32, hatsu: 32, 'dragon-g': 32, 'green-dragon': 32,
    red: 33, chun: 33, 'dragon-r': 33, 'red-dragon': 33,
  };
  if (dragons[id] !== undefined) return dragons[id];

  // Red-five aka-dora aliases.
  if (id === 'red-man5' || id === 'aka-man5' || id === '0m' || id === 'man0') return 34;
  if (id === 'red-pin5' || id === 'aka-pin5' || id === '0p' || id === 'pin0') return 35;
  if (id === 'red-sou5' || id === 'aka-sou5' || id === '0s' || id === 'sou0'
      || id === 'red-bam5' || id === '0b') return 36;

  // Suit + rank — try suit-first then rank-first.
  // Suit-first: e.g. "man5", "pin3", "sou9".
  const suitFirst = /^(man|pin|sou|bam|wan|crak|crack|character|dot|bamboo)\s*-?\s*([1-9])$/.exec(id);
  if (suitFirst !== null) {
    const suit = normalizeSuit(suitFirst[1]);
    const rank = Number.parseInt(suitFirst[2], 10);
    if (suit !== null) return suit * 9 + (rank - 1);
  }
  // Rank-first: e.g. "5m", "3p", "9s", "3b".
  const rankFirst = /^([1-9])\s*-?\s*(m|p|s|b|c|man|pin|sou|bam|wan)$/.exec(id);
  if (rankFirst !== null) {
    const rank = Number.parseInt(rankFirst[1], 10);
    const suit = normalizeSuit(rankFirst[2]);
    if (suit !== null) return suit * 9 + (rank - 1);
  }
  return null;
}

function normalizeSuit(token: string): 0 | 1 | 2 | null {
  switch (token) {
    case 'm':
    case 'c':
    case 'man':
    case 'wan':
    case 'crak':
    case 'crack':
    case 'character':
      return 0;
    case 'p':
    case 'pin':
    case 'dot':
      return 1;
    case 's':
    case 'b':
    case 'sou':
    case 'bam':
    case 'bamboo':
      return 2;
    default:
      return null;
  }
}
