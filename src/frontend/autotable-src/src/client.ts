/* eslint no-console: 0 */

import { EventEmitter } from 'events';

import { Entry } from '../server/protocol';

import { BaseClient, Game } from './base-client';
import {
  ThingInfo,
  MatchInfo,
  MouseInfo,
  SoundInfo,
  SeatInfo,
  DiceInfo,
  ClaimWindowEntry,
  HandResultEntry,
  PickupEntry,
} from './types';


// Phase J Wave 2 — Server-pushed end-of-game payload.  Bishop's runtime
// emits this when the configured hand count (default 4) is exhausted.
//
// The exact key/field names live behind Bishop's memo
// (.squad/decisions/inbox/bishop-phase-j-wave-2.md); the bundle accepts a
// flexible superset so the modal renders regardless of which subset the
// backend ends up shipping:
//
//   • `isComplete`        — required boolean flag (true → game over).
//   • `totalScores`       — optional Map<seat,points> for the per-seat
//                           summary table.  When absent we derive totals
//                           client-side by summing the `result.current`
//                           updates seen during the match.
//   • `handHistory`       — optional Array<HandResultEntry> recap.  When
//                           absent we use the client-side history built
//                           from `result.current`.
//   • `maxHands`          — optional number echoing the runtime's
//                           configured limit so the modal can show "4/4".
//
// Tombstone (value=null) on a new game so the modal hides automatically.
export interface GameCompleteEntry {
  isComplete: boolean;
  totalScores?: Record<string, number>;
  handHistory?: Array<HandResultEntry>;
  maxHands?: number;
  // Tolerated PascalCase / alt-name fallbacks for forward-compat with
  // whatever wire vocabulary Bishop settles on:
  IsComplete?: boolean;
  TotalScores?: Record<string, number>;
  HandHistory?: Array<HandResultEntry>;
  MaxHands?: number;
  isGameComplete?: boolean;
  IsGameComplete?: boolean;
}


export class Client extends BaseClient {
  match: Collection<number, MatchInfo>;
  seats: Collection<string, SeatInfo>;
  things: Collection<number, ThingInfo>;
  nicks: Collection<string, string>;
  mouse: Collection<string, MouseInfo>;
  sound: Collection<number, SoundInfo>;
  // dice was numeric-keyed in upstream; widened to string|number so Bishop's
  // `'current'` key (Phase D protocol) and the legacy local-deal key 0 both
  // work without a collection rename or a parallel event system.
  dice: Collection<string | number, DiceInfo>;
  // Phase D — Changsha protocol extensions emitted by AutotableWsEndpoint.
  claim: Collection<string, ClaimWindowEntry>;
  result: Collection<string, HandResultEntry>;
  // Phase F — manual-pickup state machine.  Singleton (key=0) carrying the
  // currently-expected pickup affordance pushed by ChangshaToAutotableTranslator.
  //   • Inbound  : ["pickup", 0, { phase, seatIndex, count, dealMode, breakPoint, wallIndex }]
  //   • Outbound : ["pickup", "rollDice", { seatIndex }]  (dealer clicks dice)
  //                ["pickup", "take",     { seatIndex, wallTileIds: number[] }]  (player picks N tiles)
  // The collection is ephemeral — it never participates in connect-time
  // replay (the backend pushes the live phase on JOIN/NEW).  Keys widened to
  // string|number so the singleton snapshot and the command-shaped outbound
  // entries share one collection without a parallel event system.
  pickup: Collection<string | number, PickupEntry>;

  // Phase J Wave 2 — singleton key="current" carrying the end-of-game
  // payload Bishop's runtime emits when MaxHands is exhausted.  We treat
  // this as a server-authoritative signal: when the singleton's
  // `isComplete`-style flag flips true the GameUi renders the end-of-game
  // modal.  Ephemeral by design — a fresh game wipes it on the new JOIN.
  gameComplete: Collection<string, GameCompleteEntry>;

  seat: number | null = 0;
  seatPlayers: Array<string | null> = new Array(4).fill(null);

  constructor() {
    super();

    // Make sure match is first, as it triggers reorganization of slots and things.
    this.match = new Collection('match', this, { sendOnConnect: true }),

    this.seats = new Collection('seats', this, { unique: 'seat', perPlayer: true });
    this.things = new Collection('things', this, { unique: 'slotName', sendOnConnect: true });
    this.nicks = new Collection('nicks', this, { perPlayer: true });
    this.mouse = new Collection('mouse', this, { rateLimit: 100, perPlayer: true });
    this.sound = new Collection('sound', this, { ephemeral: true });
    this.dice = new Collection('dice', this, { ephemeral: true });
    this.claim = new Collection('claim', this, { ephemeral: true });
    this.result = new Collection('result', this);
    this.pickup = new Collection('pickup', this, { ephemeral: true });
    this.gameComplete = new Collection('gameComplete', this, { ephemeral: true });
    this.seats.on('update', this.onSeats.bind(this));
  }

  private onSeats(): void {
    this.seat = null;
    this.seatPlayers.fill(null);
    for (const [playerId, seatInfo] of this.seats.entries()) {
      if (playerId === this.playerId()) {
        this.seat = seatInfo.seat;
      }
      if (seatInfo.seat !== null) {
        this.seatPlayers[seatInfo.seat] = playerId;
      }
    }
  }
}

interface CollectionOptions {
  // Key that has to be kept unique. Enforced by the server.
  // For example, for 'things', the unique key is 'slotName', and if you
  // attempt to store two things with the same slots, server will reject the
  // update.
  unique?: string;

  // Updates will be sent to other players, but not stored on the server (new
  // will not receive them on connection).
  ephemeral?: boolean;

  // This is a collection indexed by player ID, and values will be deleted
  // when a player disconnect.
  perPlayer?: boolean;

  // The server will not send all updates, but limit to N per second.
  rateLimit?: number;

  // If we are initializing the server (i.e. we're the first player), send
  // our value.
  sendOnConnect?: boolean;
}

export class Collection<K extends string | number, V> {
  private kind: string;
  private client: Client;
  private map: Map<K, V> = new Map();
  private pending: Map<K, V | null> = new Map();
  private events: EventEmitter = new EventEmitter();
  private options: CollectionOptions;
  private intervalId: any | null = null;
  private lastUpdate: number = 0;

  constructor(
    kind: string,
    client: Client,
    options?: CollectionOptions) {

    this.kind = kind;
    this.client = client;
    this.options = options ?? {};

    this.client.on('update', this.onUpdate.bind(this));
    this.client.on('connect', this.onConnect.bind(this));
    this.client.on('disconnect', this.onDisconnect.bind(this));
  }

  entries(): Iterable<[K, V]> {
    return this.map.entries();
  }

  get(key: K): V | null {
    return this.map.get(key) ?? null;
  }

  update(localEntries: Array<[K, V | null]>): void {
    if (!this.client.connected()) {
      for (const [key, value] of localEntries) {
        if (value !== null) {
          this.map.set(key, value);
        } else {
          this.map.delete(key);
        }
      }
      this.events.emit('update', localEntries, false);
    } else {
      const now = new Date().getTime();
      for (const [key, value] of localEntries) {
        this.pending.set(key, value);
      }
      if (!this.options.rateLimit || now > this.lastUpdate + this.options.rateLimit) {
        this.sendPending();
      }
    }
  }

  set(key: K, value: V | null): void {
    this.update([[key, value]]);
  }

  on(what: 'update', handler: (localEntries: Array<[K, V | null]>, full: boolean) => void): void;
  on(what: string, handler: (...args: any[]) => void): void {
    this.events.on(what, handler);
  }

  private onUpdate(entries: Array<Entry>, full: boolean): void {
    if (full) {
      this.map.clear();
    }
    const localEntries = [];
    for (const [kind, key, value] of entries) {
      if (kind === this.kind) {
        localEntries.push([key, value]);
        if (value !== null) {
          this.map.set(key as K, value);
        } else {
          this.map.delete(key as K);
        }
      }
    }
    if (full || localEntries.length > 0) {
      console.log(full ? 'full update' : 'update', this.kind, localEntries.length);
      this.events.emit('update', localEntries, full);
    }
  }

  private onConnect(game: Game, isFirst: boolean): void {
    if (isFirst) {
      if (this.options.unique) {
        this.client.update([['unique', this.kind, this.options.unique]]);
      }
      if (this.options.ephemeral) {
        this.client.update([['ephemeral', this.kind, true]]);
      }
      if (this.options.perPlayer) {
        this.client.update([['perPlayer', this.kind, true]]);
      }
      if (this.options.sendOnConnect) {
        const entries: Array<Entry> = [];
        for (const [key, value] of this.map.entries()) {
          entries.push([this.kind, key, value]);
        }
        this.client.update(entries);
      }
    }
    if (this.options.rateLimit) {
      this.intervalId = setInterval(this.sendPending.bind(this), this.options.rateLimit);
    }
  }

  private onDisconnect(game: Game | null): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
    if (game && this.options.perPlayer) {
      const localEntries: Array<Entry> = [];
      for (const [key, value] of this.map.entries()) {
        localEntries.push([this.kind, key, null]);
        if (key === game.playerId) {
          localEntries.push([this.kind, 'offline', value]);
        }
      }
      this.onUpdate(localEntries, true);
    }
  }

  private sendPending(): void {
    if (this.pending.size > 0) {
      const entries: Array<Entry> = [];
      for (const [k, v] of this.pending.entries()) {
        entries.push([this.kind, k, v]);
      }
      this.client.update(entries);
      this.lastUpdate = new Date().getTime();
      this.pending.clear();
    }
  }
}
