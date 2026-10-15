import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const ts = require('typescript');
const sourceFile = path.join(root, 'src/ui/readable-board-controller.ts');
const source = fs.readFileSync(sourceFile, 'utf8');
const javascript = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const clone = value => JSON.parse(JSON.stringify(value));

class NodeDouble {
  constructor(tagName, document) {
    this.tagName = tagName.toUpperCase();
    this.ownerDocument = document;
    this.children = [];
    this.parentNode = null;
    this.id = '';
    this.className = '';
    this.dataset = {};
    this.attributes = new Map();
    this.listeners = new Map();
    this.hidden = false;
    this.scrollTop = 0;
    this.tabIndex = -1;
    this.replacements = 0;
    this.measurements = 0;
    this._text = '';
    const properties = new Map();
    const priorities = new Map();
    this.style = {
      display: '', visibility: '', opacity: '',
      setProperty: (name, value, priority = '') => {
        properties.set(name, value);
        priorities.set(name, priority);
      },
      getPropertyValue: name => properties.get(name) ?? '',
      getPropertyPriority: name => priorities.get(name) ?? '',
      removeProperty: name => {
        properties.delete(name);
        priorities.delete(name);
      },
    };
    this.classList = {
      contains: name => this.className.split(/\s+/).includes(name),
      add: name => {
        if (!this.classList.contains(name)) this.className = `${this.className} ${name}`.trim();
      },
      remove: name => { this.className = this.className.split(/\s+/).filter(value => value !== name).join(' '); },
      toggle: (name, force) => {
        const add = force ?? !this.classList.contains(name);
        if (add) this.classList.add(name);
        else this.classList.remove(name);
        return add;
      },
    };
  }
  get isConnected() { return this === this.ownerDocument.body || (this.parentNode?.isConnected ?? false); }
  get inert() { return this.attributes.has('inert'); }
  set inert(value) {
    if (value) this.attributes.set('inert', '');
    else this.attributes.delete('inert');
  }
  get textContent() { return this._text + this.children.map(child => child.textContent).join(''); }
  set textContent(value) {
    for (const child of [...this.children]) child.remove();
    this._text = String(value);
  }
  setAttribute(name, value) { this.attributes.set(name, String(value)); }
  getAttribute(name) { return this.attributes.get(name) ?? null; }
  removeAttribute(name) { this.attributes.delete(name); }
  appendChild(node) {
    node.remove();
    this.children.push(node);
    node.parentNode = this;
    return node;
  }
  append(...nodes) { for (const node of nodes) this.appendChild(node); }
  remove() {
    if (this.parentNode !== null) {
      this.parentNode.children = this.parentNode.children.filter(child => child !== this);
      this.parentNode = null;
    }
  }
  replaceChildren(...nodes) {
    this.replacements++;
    for (const child of [...this.children]) child.remove();
    this._text = '';
    this.append(...nodes);
  }
  contains(node) { return node === this || this.children.some(child => child.contains(node)); }
  querySelectorAll(selector) {
    const matches = node => selector.startsWith('#') ? node.id === selector.slice(1)
      : selector.startsWith('.') ? node.classList.contains(selector.slice(1))
        : node.tagName === selector.toUpperCase();
    return this.children.flatMap(child => [
      ...(matches(child) ? [child] : []), ...child.querySelectorAll(selector),
    ]);
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  addEventListener(event, listener) {
    this.listeners.set(event, [...this.listeners.get(event) ?? [], listener]);
  }
  dispatch(event, detail = {}) {
    const value = {
      target: this, key: '', stopped: false,
      stopPropagation() { this.stopped = true; },
      preventDefault() {},
      ...detail,
    };
    for (const listener of this.listeners.get(event) ?? []) listener(value);
    return value;
  }
  click() { this.dispatch('click'); }
  focus(options) {
    this.ownerDocument.activeElement = this;
    this.ownerDocument.focuses.push({ node: this, options });
  }
  getBoundingClientRect() {
    this.measurements++;
    return this.ownerDocument.measure(this);
  }
}

class CollectionDouble {
  values = new Map();
  get(key) { return this.values.get(key) ?? null; }
  set(key, value) { this.values.set(key, value); }
  entries() { return this.values.entries(); }
}

class ClientDouble {
  seat = 0;
  seatPlayers = ['owner', 'right', 'opposite', 'left'];
  connectionMode = 'changsha';
  lastGameId = 'room';
  serverSnapshotGameId = 'room';
  online = true;
  player = 'owner';
  sent = [];
  listeners = new Map();
  things = new CollectionDouble();
  turn = new CollectionDouble();
  connected() { return this.online; }
  playerId() { return this.player; }
  on(event, listener) {
    this.listeners.set(event, [...this.listeners.get(event) ?? [], listener]);
  }
  emit(event) { for (const listener of this.listeners.get(event) ?? []) listener(); }
  update(entries) { this.sent.push(...entries); }
}

const face = (slot, value) => ({ slot, kind: 'face', face: value });
const marker = (slot, kind = 'back') => ({ slot, kind, face: null });

function projection(viewerSeat = 0) {
  return {
    viewerSeat, activeSeat: 0, wallCount: 42,
    seats: [
      { seat: 0, discards: [face('discard.0.0@0', 5), face('discard.1.0@0', 5)], melds: [] },
      { seat: 1, discards: [face('discard.0.0@1', 8)], melds: [
        { group: 2, tiles: Array.from({ length: 3 }, (_, index) => face(`meld.2.${index}@1`, 9)) },
        { group: 10, tiles: Array.from({ length: 4 }, (_, index) => face(`meld.10.${index}@1`, 18)) },
      ] },
      { seat: 2, discards: [face('discard.0.0@2', 26), marker('discard.1.0@2', 'unknown')], melds: [] },
      { seat: 3, discards: [], melds: [
        { group: 0, tiles: Array.from({ length: 4 }, (_, index) => marker(`meld.0.${index}@3`)) },
      ] },
    ],
  };
}

function freeze(value) {
  if (value !== null && typeof value === 'object') {
    for (const child of Object.values(value)) freeze(child);
    Object.freeze(value);
  }
  return value;
}

function fixture(options = {}) {
  let href = 'https://table.example/autotable/?gameId=room&variant=changsha&mobileLayout=readable&botCount=3#table';
  const location = {
    get href() { return href; },
    get search() { return new URL(href).search; },
    set search(search) {
      const url = new URL(href);
      url.search = search;
      href = url.href;
    },
  };
  const document = {
    focuses: [],
    createElement: tag => new NodeDouble(tag, document),
    getElementById: id => document.body.querySelector('#' + id),
    querySelector: selector => document.body.querySelector(selector),
  };
  document.body = new NodeDouble('body', document);
  document.activeElement = document.body;
  const listeners = new Map(), replacements = [];
  const view = {
    innerWidth: options.width ?? 412, innerHeight: options.height ?? 915,
    dockHeight: 200, dockBottom: 6,
    chatFollowsDock: options.chatFollowsDock ?? false,
    chatInset: 208, locale: 'en', visible: true, opacity: '1', position: 'fixed',
    location,
    history: {
      state: { preserve: 'history-state' },
      replaceState(state, _title, url) {
        replacements.push({ state, url: String(url) });
        href = String(url);
        this.state = state;
      },
    },
    addEventListener(event, listener) {
      listeners.set(event, [...listeners.get(event) ?? [], listener]);
    },
    emit(event) { for (const listener of listeners.get(event) ?? []) listener(); },
    getComputedStyle(node) {
      const board = node.id === 'readable-board';
      return {
        position: board ? view.position : 'fixed',
        display: node.hidden || (board && !view.visible) ? 'none' : node.style.display || 'grid',
        visibility: node.style.visibility || 'visible',
        opacity: board ? view.opacity : '1',
      };
    },
  };
  const rect = (left, top, width, height) => ({ left, top, width, height, right: left + width, bottom: top + height });
  const boardRect = () => {
    const board = document.getElementById('readable-board');
    const top = Math.max(132, Number.parseFloat(board.style.getPropertyValue('--readable-board-header-bottom')) || 0);
    const bottom = view.innerHeight - (Number.parseFloat(board.style.getPropertyValue('--readable-board-footer-inset')) || 0) - 6;
    return rect(6, top, view.boardWidth ?? view.innerWidth - 12, bottom - top);
  };
  document.measure = node => {
    if (node.id === 'readable-board') return boardRect();
    if (node.classList.contains('readable-board-public')) {
      const bounds = boardRect();
      return rect(bounds.left + 1, bounds.top + 83, bounds.width - 2, bounds.height - 84);
    }
    if (node.id === 'mobile-hand-dock') {
      return rect(6, view.innerHeight - view.dockBottom - view.dockHeight, view.innerWidth - 12, view.dockHeight);
    }
    if (node.id === 'chat-panel') {
      const inset = Number.parseFloat(document.body.style.getPropertyValue('--readable-board-dock-inset'));
      const chatInset = view.chatFollowsDock
        ? document.body.classList.contains('readable-board-active') && Number.isFinite(inset)
          ? inset : recovery.hidden ? 208 : 384
        : view.chatInset;
      return rect(view.innerWidth - 220, view.innerHeight - chatInset - 44, 208, 44);
    }
    if (node.id === 'turn-banner') return rect(8, 62, view.innerWidth - 88, 42);
    if (node.id === 'lobby-toggle') return rect(view.innerWidth - 70, 62, 62, 44);
    if (node.id === 'new-game-primary') return rect(12, 12, 108, 44);
    if (node.id === 'settings-button') return rect(view.innerWidth - 52, 8, 44, 44);
    if (node.id === 'bot-banner') return rect(8, 112, 180, 32);
    return rect(0, 0, 0, 0);
  };
  function add(tag, id, className = '') {
    const node = document.createElement(tag);
    node.id = id;
    node.className = className;
    document.body.appendChild(node);
    return node;
  }
  const main = add('div', 'main'), canvas = document.createElement('canvas');
  main.appendChild(canvas);
  const dock = add('section', 'mobile-hand-dock');
  const recovery = document.createElement('button');
  recovery.className = 'mobile-hand-recovery';
  recovery.hidden = true;
  dock.appendChild(recovery);
  const lobby = add('button', 'lobby-toggle');
  add('button', 'new-game-primary');
  add('button', 'settings-button');
  const turnBanner = add('div', 'turn-banner');
  const chat = add('div', 'chat-panel', 'chat-panel chat-panel-collapsed');
  const botBanner = add('div', 'bot-banner');
  botBanner.hidden = true;
  const connection = add('div', 'connection-banner');
  connection.hidden = true;
  const client = new ClientDouble(), calls = [];
  client.turn.set('current', { phase: 'AwaitingDiscard', activeSeat: 0, awaitingDiscard: true });
  client.things.set('opaque-private-handle', { slotName: 'hand.0@1', face: 22 });
  client.things.set(107, { slotName: 'wall.0.0@0', face: 21 });
  client.things.set(41, { slotName: 'discard.0.0@0', face: 5 });
  let output = freeze(projection(options.viewer ?? 0)), failure = false;
  if (options.viewer !== undefined) {
    client.seatPlayers[0] = 'other';
    client.seat = options.viewer;
    client.seatPlayers[options.viewer] = 'owner';
  }
  const module = { exports: {} };
  runInNewContext(javascript, {
    module, exports: module.exports, document, window: view, URL, URLSearchParams,
    require: name => {
      if (name === './readable-board.css') return {};
      if (name === '../i18n') return {
        getActiveLocale: () => view.locale,
        t: (key, values) => key === 'hand.tile' ? `${values.rank} ${values.suit}`
          : ({ 'hand.suit_0': 'characters', 'hand.suit_1': 'circles', 'hand.suit_2': 'bamboo' }[key] ?? key),
      };
      if (name === '../public-board-model') return {
        buildPublicBoard(entries, viewerSeat, activeSeat) {
          calls.push({ entries: [...entries], viewerSeat, activeSeat });
          if (failure) throw new Error('Invalid projection');
          return output;
        },
      };
      throw new Error(`Unexpected production import: ${name}`);
    },
  });
  const adapted = options.adapt === undefined ? undefined : () => options.adapt([...client.things.entries()]);
  const controller = new module.exports.ReadableBoardController(client, adapted);
  const board = () => document.getElementById('readable-board');
  const pane = () => board()?.querySelector('.readable-board-public');
  const status = () => board()?.querySelector('.readable-board-context');
  const tiles = () => pane()?.querySelectorAll('.readable-board-tile') ?? [];
  return {
    document, view, location, main, canvas, dock, recovery, lobby, turnBanner, chat, botBanner, client, calls, replacements,
    controller, board, pane, status, tiles,
    snapshot: () => output,
    project(value) { output = freeze(value); },
    failProjection() { failure = true; },
    refresh: (context = { kind: 'waiting' }, footer = 208, active = true) => controller.refresh(active, footer, context),
  };
}

test('the controller type-checks against the locked readonly model/client contract, even before the model lands', () => {
  const files = new Map([
    [path.join(root, 'src/public-board-model.ts'), `
      import type { ThingInfo } from './types';
      export type Seat = 0 | 1 | 2 | 3;
      export type PublicTile = Readonly<{ slot: string } & (
        { kind: 'face'; face: number } | { kind: 'back' | 'unknown'; face: null })>;
      export type PublicMeld = { group: number; tiles: readonly PublicTile[] };
      export type PublicSeat = { seat: Seat; discards: readonly PublicTile[]; melds: readonly PublicMeld[] };
      export type PublicBoardSnapshot = {
        viewerSeat: Seat | null; activeSeat: Seat | null; seats: readonly PublicSeat[]; wallCount: number;
      };
      export type ReadonlyPublicContext = Readonly<
        { kind: 'claim' | 'discard'; seat: Seat; face: number; slot?: string }
        | { kind: 'waiting' | 'unavailable' }
      >;
      export declare function buildPublicBoard(
        entries: Iterable<[string | number, ThingInfo]>, viewerSeat: number, activeSeat: number | null,
      ): PublicBoardSnapshot;
    `],
    [path.join(root, 'src/types.ts'), `export interface ThingInfo { slotName: string; face?: number | null }`],
    [path.join(root, 'src/client.ts'), `
      import type { ThingInfo } from './types';
      export interface Client {
        seat: number | null; seatPlayers: Array<string | null>; connectionMode: string;
        lastGameId: string | null; serverSnapshotGameId: string | null;
        playerId(): string; connected(): boolean;
        on(event: 'joining', callback: () => void): void;
        on(event: 'connect', callback: (game: unknown) => void): void;
        on(event: 'disconnect', callback: () => void): void;
        on(event: 'update', callback: () => void): void;
        things: { entries(): IterableIterator<[string | number, ThingInfo]> };
        turn: { get(key: string): { activeSeat: number | null } | null };
      }
    `],
    [path.join(root, 'src/i18n.ts'), `
      export type LocaleTag = 'en' | 'zh-Hans' | 'zh-Hant';
      export declare function getActiveLocale(): LocaleTag;
      export declare function t(key: string, params?: Record<string, string | number>): string;
    `],
    [path.join(root, 'tests/node/readable-board-assets.d.ts'), 'declare module "*.css";'],
  ]);
  const options = {
    noEmit: true, strict: true, target: ts.ScriptTarget.ES2015, module: ts.ModuleKind.ESNext,
    moduleResolution: ts.ModuleResolutionKind.Bundler, types: [],
    lib: ['lib.es2017.d.ts', 'lib.dom.d.ts', 'lib.dom.iterable.d.ts'], skipLibCheck: true,
  };
  const host = ts.createCompilerHost(options), read = host.readFile, exists = host.fileExists;
  host.readFile = file => files.get(file) ?? read(file);
  host.fileExists = file => files.has(file) || exists(file);
  host.getSourceFile = (file, languageVersion) => {
    const text = host.readFile(file);
    return text === undefined ? undefined : ts.createSourceFile(file, text, languageVersion, true);
  };
  const program = ts.createProgram([sourceFile, path.join(root, 'tests/node/readable-board-assets.d.ts')], options, host);
  const diagnostics = ts.getPreEmitDiagnostics(program).map(diagnostic =>
    ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'));
  assert.deepEqual(diagnostics, []);
});

test('renders only the model projection, all four relative seats, separate melds and four-tile kongs without sends', () => {
  const f = fixture(), before = clone([...f.client.things.entries()]);
  assert.equal(f.refresh(), true);
  assert.equal(f.calls[0].viewerSeat, 0);
  assert.equal(f.calls[0].activeSeat, 0);
  assert.deepEqual(f.calls[0].entries, before);
  const groups = f.pane().querySelectorAll('.readable-board-seat');
  assert.deepEqual(groups.map(group => group.dataset.seat), ['2', '3', '1', '0']);
  assert.deepEqual(groups.map(group => group.querySelector('h3').textContent),
    ['Opposite · Seat 2', 'Left · Seat 3', 'Right · Seat 1', 'You · Seat 0']);
  assert.equal(groups.find(group => group.dataset.seat === '0').dataset.active, 'true');
  assert.equal(f.pane().getAttribute('role'), 'region');
  assert.equal(f.pane().tabIndex, 0);
  assert.match(f.pane().getAttribute('aria-label'), /Scroll/);
  assert.equal(f.tiles().length, 16);
  assert.equal(f.tiles().filter(tile => tile.dataset.kind === 'face').length, 11);
  assert(f.tiles().every(tile => tile.dataset.face !== '21' && tile.dataset.face !== '22'));
  assert(!f.board().textContent.includes('opaque-private-handle'));
  assert(f.tiles().every(tile => !('tileId' in tile.dataset)));
  assert(f.tiles().every(tile => tile.dataset.slot === tile.dataset.publicSlot));
  assert(f.tiles().filter(tile => tile.dataset.kind === 'face').every(tile => tile.classList.contains('readable-board-face')));
  assert(f.pane().classList.contains('readable-board-scroll'));
  const melds = f.pane().querySelectorAll('.readable-board-meld');
  assert.deepEqual(melds.map(meld => meld.dataset.group), ['0', '2', '10']);
  assert.deepEqual(melds.map(meld => meld.querySelectorAll('.readable-board-tile').length), [4, 3, 4]);
  assert.equal(melds[2].getAttribute('aria-label'), '4 tiles');
  assert.match(melds[0].querySelector('.readable-board-meld-caption').textContent, /Faces hidden/);
  const backs = f.tiles().filter(tile => tile.dataset.kind === 'back');
  assert.equal(backs.length, 4);
  assert(backs.every(tile => tile.getAttribute('aria-label') === 'Concealed kong tile — face hidden'));
  const unknown = f.tiles().find(tile => tile.dataset.kind === 'unknown');
  assert.equal(unknown.textContent, '?');
  assert.match(unknown.getAttribute('aria-label'), /face unavailable/);
  assert.match(groups[0].textContent, /Face unavailable/);
  assert.equal(f.board().querySelector('.readable-board-wall').textContent, 'Wall: 42');
  assert.deepEqual([...f.client.things.entries()], before);
  assert.deepEqual(f.client.sent, []);
});

test('meld headers count sets while group captions count localized tiles without guessing a meld kind', () => {
  for (const [locale, heading, captions] of [
    ['en', 'Melds · 2', ['3 tiles', '4 tiles']],
    ['zh-Hans', '副露 · 2', ['3 张牌', '4 张牌']],
    ['zh-Hant', '副露 · 2', ['3 張牌', '4 張牌']],
  ]) {
    const f = fixture();
    f.view.locale = locale;
    f.refresh();
    const seat = f.pane().querySelectorAll('.readable-board-seat').find(node => node.dataset.seat === '1');
    assert.equal(seat.querySelector('h4').textContent, heading);
    assert.deepEqual(seat.querySelectorAll('.readable-board-meld-caption').map(node => node.textContent), captions);
    assert.deepEqual(seat.querySelectorAll('.readable-board-meld').map(node => node.getAttribute('aria-label')), captions);
    assert.deepEqual(f.client.sent, []);
  }
});

test('an optional caller-owned safe entry adapter is passed to the pure model without mutation or local ID decoding', () => {
  const entries = freeze([
    [80, { slotName: 'discard.0.0@0', face: 5 }],
    ['opaque-hidden', { slotName: 'meld.0.0@1', face: null }],
  ]);
  const f = fixture({ adapt: () => entries });
  const before = clone([...f.client.things.entries()]);
  assert.equal(f.refresh(), true);
  assert.deepEqual(f.calls[0].entries, entries);
  assert.deepEqual([...f.client.things.entries()], before);
  assert.deepEqual(f.client.sent, []);
});

test('owner concealed-kong backs and unknown faces remain model markers even when the underlying numeric key has a decoded face', () => {
  const f = fixture(), snapshot = clone(f.snapshot());
  snapshot.seats[0].melds = [{
    group: 0, tiles: Array.from({ length: 4 }, (_, index) => marker(`meld.0.${index}@0`)),
  }];
  for (let index = 0; index < 4; index++) {
    f.client.things.set(index + 72, { slotName: `meld.0.${index}@0`, face: 18, rotationIndex: 2 });
  }
  f.project(snapshot);
  f.refresh();
  const own = f.pane().querySelectorAll('.readable-board-seat').find(seat => seat.dataset.seat === '0');
  const kong = own.querySelector('.readable-board-meld');
  assert.equal(kong.querySelectorAll('.readable-board-tile').length, 4);
  assert(kong.querySelectorAll('.readable-board-tile').every(tile =>
    tile.dataset.kind === 'back' && tile.dataset.face === undefined));
  assert.match(kong.textContent, /Faces hidden/);
  const failed = fixture({ adapt: () => { throw new Error('Public lookup unavailable'); } });
  assert.equal(failed.refresh(), false);
  assert.equal(failed.calls.length, 0, 'an unavailable adapter cannot fall back to raw numeric entries');
  assert.equal(failed.main.inert, false);
  assert.deepEqual(failed.client.sent, []);
});

test('the face atlas uses explicit revealed faces, upright 4:5 sizes and the same 800% / 640% sprite math as the hand', () => {
  const f = fixture(), snapshot = clone(f.snapshot());
  snapshot.seats[0].discards = [0, 7, 8, 26].map((value, index) => face(`discard.${index}.0@0`, value));
  f.project(snapshot);
  f.refresh();
  const own = f.pane().querySelectorAll('.readable-board-seat').find(seat => seat.dataset.seat === '0');
  for (const tile of own.querySelectorAll('.readable-board-tile')) {
    const value = Number(tile.dataset.face);
    assert.equal(tile.style.backgroundPosition, `${(value % 8) * 100 / 7}% ${Math.floor(value / 8) * 100 / 5.4}%`);
    assert.equal(tile.getAttribute('role'), 'img');
    assert.equal(tile.tabIndex, -1);
  }
  const css = fs.readFileSync(path.join(root, 'src/ui/readable-board.css'), 'utf8');
  assert.match(css, /tiles-labels\.auto\.png/);
  assert.match(css, /background-size:\s*800% 640%/);
  assert.match(css, /--readable-board-tile-width:\s*32px/);
  assert.match(css, /--readable-board-tile-height:\s*40px/);
  assert.match(css, /min-width:\s*400px/);
  assert.match(css, /--readable-board-tile-width:\s*40px/);
  assert.match(css, /--readable-board-tile-height:\s*50px/);
  assert.match(css, /aspect-ratio:\s*4\s*\/\s*5/);
  assert.match(css, /readable-board-context-face\s*\{\s*width:\s*56px;\s*height:\s*70px/);
  assert.match(css, /grid-template-areas:\s*"opposite opposite" "left right" "you you"/);
  assert.match(css, /overflow-y:\s*auto/);
  assert.match(css, /min-height:\s*44px/);
  assert.doesNotMatch(css, /#main[^{]*\{[^}]*display:\s*none/s);
});

test('context comes only from the supplied history getter and highlights an exact public slot, never a same-face guess', () => {
  const f = fixture();
  f.refresh({ kind: 'discard', seat: 0, face: 5, slot: 'discard.1.0@0' });
  const highlighted = () => f.tiles().filter(tile => tile.classList.contains('readable-board-context-match'));
  assert.deepEqual(highlighted().map(tile => tile.dataset.publicSlot), ['discard.1.0@0']);
  assert.equal(f.status().dataset.kind, 'discard');
  assert.match(f.status().textContent, /Last observed discard/);
  assert.match(f.status().textContent, /From You · Seat 0/);
  assert.equal(f.status().querySelector('.readable-board-context-face').dataset.face, '5');
  f.refresh({ kind: 'discard', seat: 0, face: 5 });
  assert.equal(highlighted().length, 0);
  f.refresh({ kind: 'claim', seat: 1, face: 8, slot: 'discard.0.0@1' });
  assert.equal(f.status().dataset.kind, 'claim');
  assert.match(f.status().textContent, /Current claim/);
  assert.match(f.status().textContent, /From Right · Seat 1/);
  assert.deepEqual(highlighted().map(tile => tile.dataset.publicSlot), ['discard.0.0@1']);
  f.refresh({ kind: 'claim', seat: 0, face: 8, slot: 'discard.0.0@1' });
  assert.equal(highlighted().length, 0, 'a slot belonging to a different source is not highlighted');
  f.refresh({ kind: 'claim', seat: 1, face: 9, slot: 'discard.0.0@1' });
  assert.equal(highlighted().length, 0, 'a stale slot/face mismatch is not highlighted');
  f.refresh({ kind: 'waiting' });
  assert.equal(f.status().textContent, '…Waiting for a discard');
  assert.equal(f.status().querySelector('.readable-board-context-face'), null);
  f.refresh({ kind: 'unavailable' });
  assert.match(f.status().textContent, /last discard unavailable/);
  assert.equal(f.status().querySelector('.readable-board-context-face'), null);
  assert.equal(highlighted().length, 0);
  for (const invalid of [
    { kind: 'claim', seat: 0, face: 27 }, { kind: 'discard', seat: 8, face: 4 },
    { kind: 'discard', seat: 0, face: null },
  ]) {
    f.refresh(invalid);
    assert.equal(f.status().dataset.kind, 'unavailable');
    assert.equal(f.status().querySelector('.readable-board-context-face'), null);
  }
  assert.deepEqual(f.client.sent, []);
});

test('relative seat labels and context source localize without changing authority or resetting the scroll region', () => {
  const f = fixture({ viewer: 2 });
  f.refresh({ kind: 'claim', seat: 3, face: 8 });
  assert.deepEqual(f.pane().querySelectorAll('.readable-board-seat').map(seat => seat.querySelector('h3').textContent),
    ['Opposite · Seat 0', 'Left · Seat 1', 'Right · Seat 3', 'You · Seat 2']);
  const pane = f.pane();
  pane.scrollTop = 123;
  f.view.locale = 'zh-Hant';
  f.refresh({ kind: 'claim', seat: 3, face: 8 });
  assert.match(f.status().textContent, /目前待認領牌/);
  assert.match(f.status().textContent, /來自 右家 · 座位 3/);
  assert.match(f.board().getAttribute('aria-label'), /公開牌桌/);
  assert.equal(f.pane(), pane);
  assert.equal(pane.scrollTop, 123);
  f.view.locale = 'zh-Hans';
  f.refresh({ kind: 'waiting' });
  assert.match(f.status().textContent, /等待弃牌/);
});

test('stable refreshes, countdowns, changed public tiles and viewport/footer updates preserve scroll, focus and stable nodes', () => {
  const f = fixture();
  f.refresh();
  const board = f.board(), pane = f.pane(), button = board.querySelector('.readable-board-switch');
  const context = f.status(), originalTile = f.tiles()[0];
  pane.scrollTop = 215;
  pane.focus();
  const focuses = f.document.focuses.length, measurements = board.measurements, headerMeasures = f.turnBanner.measurements;
  const replacements = pane.replacements, statusReplacements = context.querySelector('.readable-board-context-image').replacements;
  for (let second = 10; second >= 0; second--) {
    f.turnBanner.textContent = `Claim countdown ${second}`;
    assert.equal(f.refresh(), true);
  }
  assert.equal(pane.scrollTop, 215);
  assert.equal(f.document.activeElement, pane);
  assert.equal(f.document.focuses.length, focuses);
  assert.equal(board.measurements, measurements, 'no root layout-measure loop');
  assert.equal(f.turnBanner.measurements, headerMeasures, 'countdown text does not remeasure the header');
  assert.equal(pane.replacements, replacements);
  assert.equal(context.querySelector('.readable-board-context-image').replacements, statusReplacements);
  const next = clone(f.snapshot());
  next.activeSeat = 1;
  next.wallCount = 41;
  next.seats[0].discards.push(face('discard.2.0@0', 7));
  f.project(next);
  button.focus();
  f.refresh({ kind: 'discard', seat: 0, face: 7, slot: 'discard.2.0@0' });
  assert.equal(f.pane(), pane);
  assert.equal(f.status(), context);
  assert.equal(f.board().querySelector('.readable-board-switch'), button);
  assert.equal(f.tiles()[0], originalTile, 'unchanged seats retain their tile nodes');
  assert.equal(pane.scrollTop, 215);
  assert.equal(f.document.activeElement, button);
  f.view.innerWidth = 360;
  f.view.innerHeight = 800;
  f.refresh({ kind: 'waiting' }, 300);
  assert.equal(pane.scrollTop, 215);
  assert.equal(f.document.activeElement, button);
  assert.equal(board.style.getPropertyValue('--readable-board-footer-inset'), '300px');
  assert(board.measurements > measurements);
  assert.equal(f.refresh({ kind: 'waiting' }, 300, false), false);
  assert.equal(pane.scrollTop, 215);
  assert.equal(f.refresh({ kind: 'waiting' }, 300), true);
  assert.equal(pane.scrollTop, 215, 'temporarily inactive in the same room does not erase scroll');
  assert.deepEqual(f.client.sent, []);
});

for (const [width, height] of [[360, 640], [412, 915]]) {
  test(`actual dock growth from 200 to 282px at fixed ${width}px width updates only readable geometry`, () => {
    const f = fixture({ width, height }), cameraInset = 208;
    f.chat.hidden = true;
    f.dock.style.setProperty = () => { throw new Error('Readable layout must not change the hand or camera inset'); };
    assert.equal(f.refresh({ kind: 'waiting' }, cameraInset), true);
    const board = f.board(), pane = f.pane(), tile = f.tiles()[0];
    const initialBounds = board.getBoundingClientRect();
    assert(initialBounds.bottom <= f.dock.getBoundingClientRect().top);
    pane.scrollTop = 175;
    pane.focus();
    const focuses = f.document.focuses.length, replacements = pane.replacements;

    f.view.dockHeight = 282;
    f.recovery.hidden = false;
    assert.equal(f.refresh({ kind: 'waiting' }, cameraInset), true);
    assert.equal(board.style.getPropertyValue('--readable-board-footer-inset'), '288px');
    assert(board.getBoundingClientRect().bottom <= f.dock.getBoundingClientRect().top);
    assert.equal(f.pane(), pane);
    assert.equal(f.tiles()[0], tile);
    assert.equal(pane.scrollTop, 175);
    assert.equal(pane.replacements, replacements);
    assert.equal(f.document.activeElement, pane);
    assert.equal(f.document.focuses.length, focuses);
    assert.equal(f.main.inert, true);
    const measurements = board.measurements, headerMeasures = f.turnBanner.measurements;
    for (let second = 5; second >= 0; second--) {
      f.turnBanner.textContent = `Claim countdown ${second}`;
      assert.equal(f.refresh({ kind: 'waiting' }, cameraInset), true);
    }
    assert.equal(board.measurements, measurements, 'countdown text does not pump readable geometry');
    assert.equal(f.turnBanner.measurements, headerMeasures);

    f.recovery.hidden = true;
    f.view.dockHeight = 200;
    assert.equal(f.refresh({ kind: 'waiting' }, cameraInset), true);
    assert.equal(board.style.getPropertyValue('--readable-board-footer-inset'), '208px');
    assert.equal(f.tiles()[0], tile);
    assert.equal(pane.scrollTop, 175);
    assert.equal(f.document.activeElement, pane);
    assert.deepEqual(f.client.sent, []);
  });
}

test('a recovery dock leaving insufficient space immediately restores the exact 3D state without touching recovery', () => {
  for (const [inert, aria] of [[null, null], ['already-inert', 'false']]) {
    const f = fixture({ width: 360, height: 640 });
    f.chat.hidden = true;
    if (inert !== null) f.main.setAttribute('inert', inert);
    if (aria !== null) f.main.setAttribute('aria-hidden', aria);
    const authority = clone([...f.client.things.entries()]);
    assert.equal(f.refresh(), true);
    const pane = f.pane(), tile = f.tiles()[0];
    pane.scrollTop = 160;
    pane.focus();
    f.recovery.hidden = false;
    f.view.dockHeight = 440;
    assert.equal(f.refresh(), false);
    assert.equal(f.board().hidden, true);
    assert.equal(f.document.body.classList.contains('readable-board-active'), false);
    assert.equal(f.main.getAttribute('inert'), inert);
    assert.equal(f.main.getAttribute('aria-hidden'), aria);
    assert.equal(f.canvas.parentNode, f.main);
    assert.equal(f.document.activeElement, f.lobby);
    assert.equal(f.recovery.hidden, false);
    assert.equal(f.recovery.parentNode, f.dock);
    assert.equal(f.dock.hidden, false);
    assert.deepEqual([...f.client.things.entries()], authority);
    assert.deepEqual(f.client.sent, []);
    f.view.dockHeight = 200;
    assert.equal(f.refresh(), true);
    assert.equal(f.pane(), pane);
    assert.equal(f.tiles()[0], tile);
    assert.equal(pane.scrollTop, 160);
  }
});

test('a 360px recovery keeps focus and scroll when collapsed chat follows the actual dock instead of its 384px 3D envelope', () => {
  const css = fs.readFileSync(path.join(root, 'src/ui/readable-board.css'), 'utf8');
  assert.match(css, /body\.readable-board-active #chat-panel\.chat-panel-collapsed\s*\{\s*bottom:\s*var\(--readable-board-dock-inset,\s*208px\)\s*!important/);
  const f = fixture({ width: 360, height: 640, chatFollowsDock: true });
  const property = '--readable-board-dock-inset';
  assert.equal(f.refresh(), true);
  const pane = f.pane(), tile = f.tiles()[0];
  pane.scrollTop = 40;
  pane.focus();
  f.view.dockHeight = 282;
  f.recovery.hidden = false;
  assert.equal(f.refresh(), true);
  assert.equal(f.board().style.getPropertyValue('--readable-board-footer-inset'), '332px');
  assert.equal(f.document.body.style.getPropertyValue(property), '288px');
  assert.equal(f.board().getBoundingClientRect().height, 170);
  assert(f.board().getBoundingClientRect().bottom <= f.chat.getBoundingClientRect().top - 6);
  assert(f.chat.getBoundingClientRect().bottom <= f.dock.getBoundingClientRect().top);
  assert.equal(f.pane(), pane);
  assert.equal(f.tiles()[0], tile);
  assert.equal(f.document.activeElement, pane);
  assert.equal(pane.scrollTop, 40);
  assert.equal(f.main.inert, true);
  assert.equal(f.refresh({ kind: 'waiting' }, 208, false), false);
  assert.equal(f.document.body.style.getPropertyValue(property), '');
  assert.equal(f.main.inert, false);
  assert.deepEqual(f.client.sent, []);
  const restored = fixture();
  restored.document.body.style.setProperty(property, '19px', 'important');
  assert.equal(restored.refresh(), true);
  assert.equal(restored.refresh({ kind: 'waiting' }, 208, false), false);
  assert.equal(restored.document.body.style.getPropertyValue(property), '19px');
  assert.equal(restored.document.body.style.getPropertyPriority(property), 'important');
});

test('only an actual room or owned-seat change resets public scroll', () => {
  const f = fixture();
  f.refresh();
  const pane = f.pane();
  pane.scrollTop = 199;
  f.client.online = false;
  f.client.emit('disconnect');
  f.client.online = true;
  f.client.emit('connect');
  f.refresh();
  assert.equal(pane.scrollTop, 199, 'same-room reconnect retains public scroll');
  f.location.search = '?gameId=next-room&variant=changsha&mobileLayout=readable';
  f.client.lastGameId = f.client.serverSnapshotGameId = 'next-room';
  f.refresh();
  assert.equal(pane.scrollTop, 0);
  pane.scrollTop = 77;
  f.client.seat = 1;
  f.client.seatPlayers = [null, 'owner', null, null];
  f.project(projection(1));
  f.refresh();
  assert.equal(pane.scrollTop, 0);
  assert.equal(f.board().dataset.viewerSeat, '1');
});

test('inert and aria attributes restore exactly while canvas geometry, parentage, status and dock stay untouched', () => {
  for (const [inert, aria] of [[null, null], ['', 'false'], ['already-inert', 'true']]) {
    const f = fixture();
    if (inert !== null) f.main.setAttribute('inert', inert);
    if (aria !== null) f.main.setAttribute('aria-hidden', aria);
    f.main.style.visibility = 'collapse';
    const turnText = f.turnBanner.textContent = 'Your authoritative turn';
    assert.equal(f.refresh(), true);
    assert.equal(f.main.inert, true);
    assert.equal(f.main.getAttribute('aria-hidden'), 'true');
    assert.equal(f.main.style.visibility, 'collapse', 'visibility is scoped CSS, not an inline mutation');
    assert.equal(f.canvas.parentNode, f.main);
    assert.equal(f.main.children.length, 1);
    assert.equal(f.turnBanner.textContent, turnText);
    assert.equal(f.controller.refresh(false, 208, { kind: 'unavailable' }), false);
    assert.equal(f.board().hidden, true);
    assert.equal(f.main.getAttribute('inert'), inert);
    assert.equal(f.main.getAttribute('aria-hidden'), aria);
    assert.equal(f.main.style.visibility, 'collapse');
    assert.equal(f.document.body.classList.contains('readable-board-active'), false);
    assert.equal(f.canvas.parentNode, f.main);
    f.controller.refresh(false, 208, { kind: 'unavailable' });
    assert.equal(f.main.getAttribute('inert'), inert, 'repeated inactive refreshes remain reversible');
  }
});

test('disconnect, joining, connect, invalid update and landscape immediately release the hidden/inert scene', () => {
  for (const leave of [
    f => { f.client.online = false; f.client.emit('disconnect'); },
    f => f.client.emit('joining'),
    f => f.client.emit('connect'),
    f => { f.client.seatPlayers[0] = 'replacement'; f.client.emit('update'); },
    f => { f.view.innerWidth = 915; f.view.innerHeight = 412; f.view.emit('resize'); },
  ]) {
    const f = fixture();
    f.refresh();
    leave(f);
    assert.equal(f.main.inert, false);
    assert.equal(f.main.getAttribute('aria-hidden'), null);
    assert.equal(f.document.body.classList.contains('readable-board-active'), false);
    assert.equal(f.board().hidden, true);
    assert.equal(f.canvas.parentNode, f.main);
    assert.deepEqual(f.client.sent, []);
  }
});

test('viewport, ownership, room, flag, lobby, panel and footer guards fail closed without hiding 3D', () => {
  for (const change of [
    f => { f.view.innerWidth = 601; },
    f => { f.view.innerWidth = 915; f.view.innerHeight = 412; },
    f => { f.view.innerWidth = 412; f.view.innerHeight = 412; },
    f => { f.view.innerWidth = 0; },
    f => { f.view.innerHeight = NaN; },
    f => { f.client.connectionMode = 'relay'; },
    f => { f.client.online = false; },
    f => { f.client.serverSnapshotGameId = null; },
    f => { f.client.lastGameId = 'other-room'; },
    f => { f.client.seatPlayers[0] = 'other-owner'; },
    f => { f.client.seat = null; },
    f => { f.client.seat = 4; },
    f => { f.document.body.classList.add('spectating'); },
    f => { f.document.body.classList.add('lobby-active'); },
    f => { f.document.body.classList.add('move-log-open'); },
    f => { f.chat.classList.remove('chat-panel-collapsed'); },
    f => {
      const drawer = f.document.createElement('div');
      drawer.id = 'settings-drawer-v2';
      drawer.className = 'settings-drawer-v2-open';
      f.document.body.appendChild(drawer);
    },
    f => { f.location.search = '?gameId=room&variant=four_player&mobileLayout=readable'; },
    f => { f.location.search = '?gameId=room&mobileLayout=hybrid'; },
    f => { f.location.search = '?gameId=room&mobileLayout=readable&mobileLayout=readable'; },
    f => { f.location.search = '?gameId=room&gameId=other&mobileLayout=readable'; },
    f => { f.location.search = '?mobileLayout=readable'; },
    f => { f.main.remove(); },
  ]) {
    const f = fixture();
    change(f);
    assert.equal(f.refresh(), false);
    assert.equal(f.board()?.hidden ?? true, true);
    assert.equal(f.main.inert, false);
    assert.equal(f.main.getAttribute('aria-hidden'), null);
    assert.equal(f.document.body.classList.contains('readable-board-active'), false);
    assert.deepEqual(f.client.sent, []);
  }
  for (const footer of [-1, NaN, Infinity, 915, 1000]) {
    const f = fixture();
    assert.equal(f.refresh({ kind: 'waiting' }, footer), false);
    assert.equal(f.main.inert, false);
  }
});

test('a short portrait reserves the pinned 56x70 context and collapsed chat, while an unusable stage or missing CSS restores 3D', () => {
  const short = fixture({ width: 360, height: 640 });
  assert.equal(short.refresh(), true);
  const bounds = short.board().getBoundingClientRect(), pane = short.pane().getBoundingClientRect();
  assert.equal(short.board().style.getPropertyValue('--readable-board-footer-inset'), '252px');
  assert(bounds.bottom <= short.chat.getBoundingClientRect().top - 6);
  assert(pane.height >= 64);
  for (const change of [
    f => { f.view.visible = false; },
    f => { f.view.opacity = '0'; },
    f => { f.view.position = 'static'; },
    f => { f.view.boardWidth = 0; },
  ]) {
    const f = fixture();
    change(f);
    assert.equal(f.refresh(), false);
    assert.equal(f.main.inert, false);
    assert.equal(f.document.body.classList.contains('readable-board-active'), false);
  }
  assert.equal(short.refresh({ kind: 'waiting' }, 480), false);
  assert.equal(short.main.inert, false);
  assert.equal(short.board().hidden, true);
});

test('optional panels restore the scene and hide, rather than cover, the public board without losing public scroll', () => {
  const f = fixture();
  f.refresh();
  const pane = f.pane();
  pane.scrollTop = 93;
  f.chat.classList.remove('chat-panel-collapsed');
  assert.equal(f.refresh(), false);
  assert.equal(f.main.inert, false);
  f.chat.classList.add('chat-panel-collapsed');
  assert.equal(f.refresh(), true);
  assert.equal(pane.scrollTop, 93);
  f.chat.hidden = true;
  f.refresh();
  assert.equal(f.board().style.getPropertyValue('--readable-board-footer-inset'), '208px');
  f.chat.hidden = false;
  f.refresh();
  assert.equal(f.board().style.getPropertyValue('--readable-board-footer-inset'), '252px');
});

test('zero wall count is not replaced with a decorative wall, unknown counts and invalid projections stay honest', () => {
  const f = fixture(), zero = clone(f.snapshot());
  zero.wallCount = 0;
  f.project(zero);
  assert.equal(f.refresh(), true);
  assert.equal(f.board().querySelector('.readable-board-wall').textContent, 'Wall: 0');
  f.project({ ...zero, wallCount: NaN });
  assert.equal(f.refresh(), true);
  assert.equal(f.board().querySelector('.readable-board-wall').textContent, 'Wall count unavailable');
  for (const corrupt of [
    snapshot => { snapshot.viewerSeat = null; },
    snapshot => { snapshot.activeSeat = 8; },
    snapshot => { snapshot.seats.pop(); },
    snapshot => { snapshot.seats[0].seat = 1; },
    snapshot => { snapshot.seats[0].discards[0].face = 27; },
    snapshot => { snapshot.seats[3].melds[0].tiles[0].face = 4; },
  ]) {
    const broken = fixture(), snapshot = clone(broken.snapshot());
    corrupt(snapshot);
    broken.project(snapshot);
    assert.equal(broken.refresh(), false);
    assert.equal(broken.main.inert, false);
    assert.equal(broken.document.body.classList.contains('readable-board-active'), false);
  }
  f.failProjection();
  assert.equal(f.refresh(), false);
  assert.equal(f.main.inert, false);
});

test('3D table is an exact reversible history-only flag change that preserves room and never reloads, rejoins or sends actions', () => {
  const f = fixture();
  f.refresh();
  const button = f.board().querySelector('.readable-board-switch');
  button.focus();
  assert.match(button.getAttribute('aria-label'), /keeping your hand controls/);
  assert.equal(button.dispatch('keydown', { key: 'Enter' }).stopped, true);
  assert.equal(button.dispatch('keyup', { key: ' ' }).stopped, true);
  button.click();
  const url = new URL(f.location.href);
  assert.equal(url.searchParams.get('gameId'), 'room');
  assert.equal(url.searchParams.get('variant'), 'changsha');
  assert.equal(url.searchParams.get('botCount'), '3');
  assert.deepEqual(url.searchParams.getAll('mobileLayout'), ['hybrid']);
  assert.equal(url.hash, '#table');
  assert.equal(f.replacements.length, 1);
  assert.equal(f.replacements[0].state, f.view.history.state);
  assert.equal(f.board().hidden, true);
  assert.equal(f.main.inert, false);
  assert.equal(f.main.getAttribute('aria-hidden'), null);
  assert.equal(f.document.activeElement, f.lobby);
  assert.equal(f.refresh(), false, 'a stale caller active flag cannot override the local switch');
  assert.deepEqual(f.client.sent, []);
});
