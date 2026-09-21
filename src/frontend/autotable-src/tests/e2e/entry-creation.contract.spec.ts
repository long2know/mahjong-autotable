// Actual entry callbacks, ClientUi and BaseClient run with Node-only DOM/socket
// doubles. These controls do not launch browsers or claim live recovery proof.
import { createHash, randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import * as ts from 'typescript';
import { test, expect } from '@playwright/test';
import { BaseClient } from '../../src/base-client';
import { hideEl, showEl, setElHidden } from '../../src/dom-utils';
import { isNewGameActivation, NEW_GAME_ACTION_SELECTOR } from '../../src/new-game-action';
import { parseBaseUnit, MAX_BASE_UNIT } from '../../src/base-unit';

const SOURCE = process.env.ENTRY_CREATION_SOURCE_ROOT ?? resolve(__dirname, '../..');
const sourceText = (name: string): string => readFileSync(resolve(SOURCE, name), 'utf8');
const sourceHashes = Object.fromEntries(['src/client-ui.ts', 'src/lobby.ts', 'src/session-url.ts'].map(name =>
  [name, createHash('sha256').update(sourceText(name)).digest('hex')],
));
const COMPILE = { target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.CommonJS };
type WireEntry = [string, string | number, unknown];

class Classes {
  private readonly names = new Set<string>();
  add(...names: string[]): void { for (const name of names) this.names.add(name); }
  remove(...names: string[]): void { for (const name of names) this.names.delete(name); }
  contains(name: string): boolean { return this.names.has(name); }
  toggle(name: string, on: boolean): void { if (on) this.add(name); else this.remove(name); }
}

class ElementDouble {
  value = '';
  hidden = false;
  disabled = false;
  checked = false;
  textContent = '';
  innerText = '';
  className = '';
  readonly style = { display: '' };
  readonly classList = new Classes();
  readonly handlers = new Map<string, Array<() => void>>();
  onclick: (() => void) | null = null;
  onchange: (() => void) | null = null;
  oninput: (() => void) | null = null;
  constructor(readonly id: string) {}
  addEventListener(event: string, callback: () => void): void {
    this.handlers.set(event, [...this.handlers.get(event) ?? [], callback]);
  }
  click(): void { if (!this.disabled) { this.onclick?.(); for (const fn of this.handlers.get('click') ?? []) fn(); } }
  focus(): void {}
  setAttribute(_name: string, _value: string): void {}
  closest(selector: string): ElementDouble | null {
    return this.id === 'new-game' && selector === NEW_GAME_ACTION_SELECTOR ? this : null;
  }
  matches(_selector: string): boolean { return this.disabled; }
}

class SocketDouble {
  static readonly OPEN = 1;
  static readonly sockets: SocketDouble[] = [];
  readyState = 0;
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  readonly sent: Array<Record<string, unknown>> = [];
  constructor(readonly url: string) { SocketDouble.sockets.push(this); }
  open(): void { this.readyState = SocketDouble.OPEN; this.onopen?.(); }
  send(data: string): void {
    if (this.readyState !== SocketDouble.OPEN) throw new Error('Socket is not open');
    this.sent.push(JSON.parse(data));
  }
  receive(message: object): void { this.onmessage?.({ data: JSON.stringify(message) }); }
  close(): void { this.readyState = 3; this.onclose?.(); }
}

class CollectionDouble {
  private readonly values = new Map<string | number, unknown>();
  private readonly listeners: Array<(entries: Array<[string | number, unknown]>) => void> = [];
  get(key: string | number): unknown { return this.values.get(key) ?? null; }
  on(_event: string, listener: (entries: Array<[string | number, unknown]>) => void): void { this.listeners.push(listener); }
  set(_key: string, _value: unknown): void {}
  apply(entries: WireEntry[], kind: string, full: boolean): void {
    if (full) this.values.clear();
    const local: Array<[string | number, unknown]> = [];
    for (const [entryKind, key, value] of entries) {
      if (entryKind !== kind) continue;
      if (value === null) this.values.delete(key);
      else this.values.set(key, value);
      local.push([key, value]);
    }
    if (full || local.length > 0) for (const listener of this.listeners) listener(local);
  }
}

class EntryClient extends BaseClient {
  seat: number | null = null;
  seatPlayers: Array<string | null> = [null, null, null, null];
  lastGameId: string | null = null;
  serverSnapshotGameId: string | null = null;
  readonly turn = new CollectionDouble();
  readonly seats = new CollectionDouble();
  readonly nicks = new CollectionDouble();
  readonly actionRejected = new CollectionDouble();
  cleared = 0;
  constructor() {
    super();
    this.on('connect', game => { this.lastGameId = game.gameId; this.serverSnapshotGameId = null; });
    this.on('update', (entries, full) => {
      this.turn.apply(entries, 'turn', full);
      this.seats.apply(entries, 'seats', full);
      this.actionRejected.apply(entries, 'actionRejected', full);
      if (full) this.serverSnapshotGameId = this.lastGameId;
    });
    this.on('disconnect', () => { this.serverSnapshotGameId = null; });
  }
  clearReconnectSession(): void { this.cleared++; this.lastGameId = null; }
}

interface UiPort {
  start(): void;
  connect(): void;
  newGame(): void;
  gameIdInput: ElementDouble;
}

function entryHarness(search: string): {
  location: URL;
  navigations: string[];
  historyChanges: string[];
  element(id: string): ElementDouble;
  coldNewGame(): void;
  apply(state: object, quick?: boolean): void;
  boot(): { ui: UiPort; client: EntryClient; socket: SocketDouble };
  fireTimer(): void;
} {
  const location = new URL(`https://example.test/autotable/${search}`);
  const navigations: string[] = [];
  const historyChanges: string[] = [];
  const elements = new Map<string, ElementDouble>();
  const element = (id: string): ElementDouble => {
    let value = elements.get(id);
    if (value === undefined) { value = new ElementDouble(id); elements.set(id, value); }
    return value;
  };
  const storage = new Map<string, string>();
  const timers = new Map<number, () => void>();
  let timerId = 0;
  const clicks: Array<(event: { target: ElementDouble; defaultPrevented: boolean; preventDefault(): void }) => void> = [];
  const browserLocation = {
    get href(): string { return location.href; },
    get search(): string { return location.search; },
    get pathname(): string { return location.pathname; },
    get hash(): string { return location.hash; },
    get host(): string { return location.host; },
    get protocol(): string { return location.protocol; },
    replace(url: string): void { navigations.push(url); location.href = new URL(url, location).href; },
  };
  const globals = {
    URL, URLSearchParams, location: browserLocation,
    process: { env: { NODE_ENV: 'production' } },
    localStorage: {
      getItem: (key: string): string | null => storage.get(key) ?? null,
      setItem: (key: string, value: string): void => { storage.set(key, value); },
    },
    window: {
      location: browserLocation, crypto: { randomUUID },
      setTimeout(callback: () => void): number { timers.set(++timerId, callback); return timerId; },
      clearTimeout(id: number): void { timers.delete(id); },
    },
    history: {
      state: null,
      replaceState(_state: unknown, _title: string, url: string): void {
        historyChanges.push(url); location.href = new URL(url, location).href;
      },
    },
    document: {
      body: { classList: new Classes() },
      getElementById: (id: string): ElementDouble | null => id === 'game-complete-stats-delta' ? null : element(id),
      getElementsByTagName: (): ElementDouble[] => [element('title')],
      addEventListener: (_type: string, callback: typeof clicks[number]): void => { clicks.push(callback); },
    },
  };
  const load = (name: string, require: (module: string) => unknown): Record<string, unknown> => {
    const exported: Record<string, unknown> = {};
    runInNewContext(ts.transpileModule(sourceText(name), { compilerOptions: COMPILE }).outputText,
      { ...globals, exports: exported, require });
    return exported;
  };
  const urlModule = load('src/session-url.ts', name => {
    if (name === './base-unit') return { DEFAULT_BASE_UNIT: 1 };
    throw new Error(`Unexpected URL module dependency: ${name}`);
  });
  const lobbySource = ts.createSourceFile('lobby.ts', sourceText('src/lobby.ts'), ts.ScriptTarget.Latest, true);
  const functionText = (name: string): string => {
    const node = lobbySource.statements.find((item): item is ts.FunctionDeclaration =>
      ts.isFunctionDeclaration(item) && item.name?.text === name);
    if (node === undefined) throw new Error(`Actual lobby function missing: ${name}`);
    return node.getText(lobbySource).replace(/^export /, '');
  };
  const run = (text: string, expression: string, extra: Record<string, unknown> = {}): unknown =>
    runInNewContext(ts.transpileModule(`${text}\n${expression}`, { compilerOptions: COMPILE }).outputText,
      { ...globals, ...urlModule, ...extra });
  const bind = run(
    `let newGameControlsBound=false; let newGameNavigationPending=false; let newGameAction=null;\n${functionText('bindNewGameControls')}`,
    'bindNewGameControls;',
    { isNewGameActivation, NEW_GAME_ACTION_SELECTOR },
  );
  if (typeof bind !== 'function') throw new Error('Actual eager entry binding did not compile');
  const bindNewGameControls = bind as (action?: () => void) => void;
  bindNewGameControls();
  const builder = run(functionText('buildUrl'), 'buildUrl;', { mintFreshGameId: urlModule.mintFreshGameId });
  if (typeof builder !== 'function') throw new Error('Actual lobby URL builder did not compile');
  const callback = (name: 'apply' | 'quickMatchBtn'): ts.Node => {
    let found: ts.Node | undefined;
    const visit = (node: ts.Node): void => {
      if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)
          && node.expression.name.text === 'addEventListener'
          && node.expression.expression.getText(lobbySource) === name
          && node.arguments[0]?.getText(lobbySource) === "'click'") found = node.arguments[1];
      ts.forEachChild(node, visit);
    };
    visit(lobbySource);
    if (found === undefined) throw new Error(`Actual ${name} click callback missing`);
    return found;
  };
  const uiModule = load('src/client-ui.ts', name => {
    switch (name) {
      case './session-url': return urlModule;
      case './lobby': return { bindNewGameControls };
      case './dom-utils': return { hideEl, showEl, setElHidden };
      case './base-unit': return { MAX_BASE_UNIT, parseBaseUnit };
      case './i18n': return { t: (key: string, params?: unknown): string => `${key}:${JSON.stringify(params)}` };
      case './profile': return {};
      case './stats': return {};
      case './reconnect': return {};
      case './ui/rule-action-controls': return { getRuleActionControls: (): void => {} };
      default: throw new Error(`Unexpected entry dependency: ${name}`);
    }
  });
  const ctor = uiModule.ClientUi;
  if (typeof ctor !== 'function') throw new Error('Actual ClientUi did not compile');
  const ClientUi = ctor as new (client: EntryClient) => UiPort;
  // Toast rendering is unrelated; entry status/banner behavior remains real.
  Object.defineProperty(ClientUi.prototype, 'showToast', { value: (): void => {} });
  test.info().annotations.push({ type: 'actual-entry-source-hashes', description: JSON.stringify(sourceHashes) });
  return {
    location, navigations, historyChanges, element,
    coldNewGame(): void {
      for (const click of clicks) click({ target: element('new-game'), defaultPrevented: false, preventDefault(): void {} });
    },
    apply(state: object, quick = false): void {
      const call = run('', `(${callback(quick ? 'quickMatchBtn' : 'apply').getText(lobbySource)});`, {
        buildUrl: builder, readPickers: (): object => state,
        refreshSeedValidity: (): boolean => true, refreshBaseUnitValidity: (): boolean => true,
        seedInput: element('lobby-seed'), baseUnitInput: element('lobby-base-unit'), saveDefaultsInput: { checked: false },
        writeLocalStorageDefaults: (): void => {}, hidePanel: (): void => {}, markSkipOpenOnLoadFlag: (): void => {},
      });
      if (typeof call !== 'function') throw new Error('Actual entry callback did not compile');
      call();
    },
    boot(): { ui: UiPort; client: EntryClient; socket: SocketDouble } {
      const client = new EntryClient();
      const ui = new ClientUi(client);
      ui.start();
      const socket = SocketDouble.sockets[SocketDouble.sockets.length - 1];
      if (socket === undefined) throw new Error('Entry did not open a normal WebSocket');
      return { client, ui, socket };
    },
    fireTimer(): void {
      const next = timers.entries().next();
      if (next.done) throw new Error('Expected an ordinary reconnect timer');
      const [id, callback] = next.value;
      timers.delete(id); callback();
    },
  };
}

function joined(socket: SocketDouble, alias: string): void {
  socket.receive({ type: 'JOINED', gameId: alias, playerId: 'same-owner', isFirst: false });
}

function full(socket: SocketDouble, bound: boolean): void {
  socket.receive({
    type: 'UPDATE', full: true, entries: [
      ['match', 0, { conditions: { baseUnit: bound ? 10 : 1 } }],
      ...(bound ? [['turn', 'current', { phase: 'Seating', activeSeat: null, awaitingDiscard: false }]] : []),
    ],
  });
}

test.describe('Explicit application creation entry — source/normal-protocol controls', () => {
  const originalSocket = Object.getOwnPropertyDescriptor(globalThis, 'WebSocket');
  test.beforeEach(() => {
    SocketDouble.sockets.length = 0;
    Object.defineProperty(globalThis, 'WebSocket', { configurable: true, writable: true, value: SocketDouble });
  });
  test.afterEach(() => {
    if (originalSocket !== undefined) Object.defineProperty(globalThis, 'WebSocket', originalSocket);
    else Reflect.deleteProperty(globalThis, 'WebSocket');
  });

  test('actual cold header callback creates one fresh explicit NEW intent before ClientUi exists', () => {
    const h = entryHarness('?seat=0');
    h.coldNewGame(); h.coldNewGame();
    expect(h.navigations).toHaveLength(1);
    const id = h.location.searchParams.get('gameId');
    expect(id).toMatch(/^changsha-[0-9a-f]{8}$/);
    expect(h.location.searchParams.get('createGame')).toBe(id);
    const { socket } = h.boot();
    socket.open();
    expect(socket.sent[0]).toEqual({ type: 'NEW' });
    const wire = new URL(socket.url).searchParams;
    expect(wire.get('gameId')).toBe(id);
    expect(wire.get('createGame')).toBeNull();
    expect(wire.get('seat')).toBe('0');
  });

  for (const dealMode of ['manual', 'auto']) {
    test(`actual Quick Match callback preserves ${dealMode}/unit/hand config and uses NEW`, () => {
      const h = entryHarness('?variant=changsha&gameId=prior-room');
      h.apply({ variant: 'changsha', dealMode, botCount: 0, botDifficulty: 'Hard', handCount: 8, baseUnit: 10, seed: 42, seat: null }, true);
      const id = h.location.searchParams.get('gameId');
      expect(id).not.toBe('prior-room');
      expect(h.location.searchParams.get('createGame')).toBe(id);
      const { socket } = h.boot(); socket.open();
      expect(socket.sent[0]).toEqual({ type: 'NEW' });
      const wire = new URL(socket.url).searchParams;
      expect(Object.fromEntries(['dealMode','baseUnit','handCount','botCount','botDifficulty','seat'].map(k => [k, wire.get(k)])))
        .toEqual({ dealMode, baseUnit: '10', handCount: '8', botCount: '3', botDifficulty: 'Medium', seat: '0' });
    });
  }

  test('ordinary existing Apply remains JOIN; changed configuration starts a fresh NEW', () => {
    const state = { variant: 'changsha', dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 8, baseUnit: 10, seed: 94209, seat: 0 };
    for (const changed of [false, true]) {
      const h = entryHarness('?gameId=existing-room&variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=8&baseUnit=10&seed=94209&seat=0');
      h.apply({ ...state, baseUnit: changed ? 100 : 10 });
      const id = h.location.searchParams.get('gameId');
      expect(id === 'existing-room').toBe(!changed);
      expect(h.location.searchParams.get('createGame')).toBe(changed ? id : null);
      const { socket } = h.boot(); socket.open();
      expect(socket.sent[0]).toEqual(changed ? { type: 'NEW' } : { type: 'JOIN', gameId: 'existing-room' });
    }
  });

  test('JOINED/unbound FULL is not creation confirmation; reload/retry retains the same alias intent', () => {
    const h = entryHarness('?gameId=fresh-room&createGame=fresh-room&variant=changsha&baseUnit=10&seat=0');
    const { socket } = h.boot(); socket.open();
    expect(socket.sent[0]).toEqual({ type: 'NEW' });
    joined(socket, 'fresh-room'); full(socket, false);
    expect(h.location.searchParams.get('createGame')).toBe('fresh-room');
    socket.close();
    h.fireTimer();
    const retry = SocketDouble.sockets[1]; retry.open();
    expect(retry.sent[0]).toEqual({ type: 'NEW' });
    expect(new URL(retry.url).searchParams.get('gameId')).toBe('fresh-room');
    const reloaded = entryHarness(h.location.search).boot(); reloaded.socket.open();
    expect(reloaded.socket.sent[0]).toEqual({ type: 'NEW' });
    expect(new URL(reloaded.socket.url).searchParams.get('gameId')).toBe('fresh-room');
  });

  test('runtime FULL consumes only the intent; subsequent reconnect/reload is JOIN without config changes', () => {
    const h = entryHarness('?gameId=fresh-room&createGame=fresh-room&variant=changsha&dealMode=manual&baseUnit=10&handCount=8&seed=94209&botCount=3&botDifficulty=Medium&seat=2#retained');
    const { socket, ui } = h.boot(); ui.connect(); socket.open();
    expect(SocketDouble.sockets).toHaveLength(1);
    expect(socket.sent).toHaveLength(1);
    expect(socket.sent[0]).toEqual({ type: 'NEW' });
    joined(socket, 'fresh-room'); full(socket, true);
    expect(h.location.searchParams.has('createGame')).toBe(false);
    expect(h.location.hash).toBe('#retained');
    expect(h.location.searchParams.get('baseUnit')).toBe('10');
    expect(h.location.searchParams.get('seed')).toBe('94209');
    socket.close(); h.fireTimer();
    const retry = SocketDouble.sockets[1]; retry.open();
    expect(retry.sent[0]).toEqual({ type: 'JOIN', gameId: 'fresh-room' });
    const reloaded = entryHarness(h.location.search).boot(); reloaded.socket.open();
    expect(reloaded.socket.sent[0]).toEqual({ type: 'JOIN', gameId: 'fresh-room' });
  });

  test('unchanged Apply preserves an unconfirmed explicit creation rather than downgrading it to JOIN', () => {
    const h = entryHarness('?gameId=fresh-room&createGame=fresh-room&variant=changsha&dealMode=auto&baseUnit=10&handCount=4&botCount=3&botDifficulty=Medium&seat=0');
    h.apply({ variant: 'changsha', dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4, baseUnit: 10, seed: null, seat: 0 });
    expect(h.location.searchParams.get('gameId')).toBe('fresh-room');
    expect(h.location.searchParams.get('createGame')).toBe('fresh-room');
    const { socket } = h.boot(); socket.open();
    expect(socket.sent[0]).toEqual({ type: 'NEW' });
  });

  test('ready New Game preserves the real owned seat/config and uses a different explicit alias', () => {
    const h = entryHarness('?gameId=old-room&variant=changsha&dealMode=manual&baseUnit=10&handCount=8&botCount=0&seed=94209&seat=0');
    const { client, socket } = h.boot(); socket.open(); joined(socket, 'old-room'); full(socket, true);
    client.seat = 2;
    h.coldNewGame(); h.coldNewGame();
    expect(client.cleared).toBe(1);
    expect(socket.readyState).toBe(3);
    expect(h.navigations).toHaveLength(1);
    expect(h.location.searchParams.get('gameId')).not.toBe('old-room');
    expect(h.location.searchParams.get('createGame')).toBe(h.location.searchParams.get('gameId'));
    expect(h.location.searchParams.get('seat')).toBe('2');
    expect(h.location.searchParams.get('baseUnit')).toBe('10');
    expect(h.location.searchParams.get('seed')).toBe('94209');
  });

  for (const variant of ['four-player', 'three-player', 'bamboo', 'minefield']) {
    test(`relay ${variant} keeps its JOIN path and carries no new creation marker`, () => {
      const h = entryHarness(`?variant=${variant}&gameId=old-room&createGame=old-room`);
      h.apply({ variant, dealMode: 'auto', botCount: 0, botDifficulty: 'Medium', handCount: 4, baseUnit: 1, seed: null, seat: null }, true);
      expect(h.location.searchParams.has('createGame')).toBe(false);
      const { socket } = h.boot(); socket.open();
      expect(socket.sent[0]).toEqual({ type: 'JOIN', gameId: h.location.searchParams.get('gameId') });
    });
  }

  test('a marker for another alias cannot turn an ordinary JOIN into creation', () => {
    const h = entryHarness('?gameId=existing-room&createGame=other-room&variant=changsha');
    const { socket } = h.boot(); socket.open();
    expect(socket.sent[0]).toEqual({ type: 'JOIN', gameId: 'existing-room' });
  });

  test('room rejection is visible, never becomes an automatic NEW or substitutes an alias', () => {
    const h = entryHarness('?gameId=unknown-legacy-room&variant=changsha');
    const { socket } = h.boot(); socket.open();
    socket.receive({ type: 'UPDATE', full: false, entries: [
      ['actionRejected', 'current', { action: 'room', reason: 'legacy-room-binding-unavailable' }],
    ] });
    expect(h.element('status-text').innerText).toContain('legacy-room-binding-unavailable');
    expect(h.element('connection-banner').hidden).toBe(false);
    expect(socket.sent).toEqual([{ type: 'JOIN', gameId: 'unknown-legacy-room' }]);
    expect(h.location.searchParams.get('gameId')).toBe('unknown-legacy-room');
    expect(h.location.searchParams.has('createGame')).toBe(false);
    expect(h.navigations).toEqual([]);
  });
});
