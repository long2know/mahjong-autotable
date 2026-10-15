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
const javascript = file => ts.transpileModule(fs.readFileSync(path.join(root, file), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const layoutModule = { exports: {} };
runInNewContext(javascript('src/mobile-layout.ts'), {
  module: layoutModule, exports: layoutModule.exports, URLSearchParams,
});
const { parseMobileLayout, resolveMobileLayout } = layoutModule.exports;
const tableLayoutModule = { exports: {} };
runInNewContext(javascript('src/table-layout.ts'), {
  module: tableLayoutModule, exports: tableLayoutModule.exports, URLSearchParams,
  require: id => { throw new Error(`Table-layout parsing must not import ${id}`); },
});
const { parseTableLayout } = tableLayoutModule.exports;

class NodeDouble {
  constructor(tagName = '') {
    this.tagName = tagName.toUpperCase();
    this.children = [];
    this.parentNode = null;
    this.id = '';
    this.className = '';
    this.dataset = {};
    this.style = { display: '' };
    this.hidden = false;
    this.disabled = false;
    this.textContent = '';
    this.attributes = new Map();
    this.listeners = new Map();
    this.classList = {
      contains: name => this.className.split(' ').includes(name),
      add: name => { if (!this.classList.contains(name)) this.className = `${this.className} ${name}`.trim(); },
      remove: name => { this.className = this.className.split(' ').filter(value => value !== name).join(' '); },
    };
  }
  get nextSibling() {
    return this.parentNode?.children[this.parentNode.children.indexOf(this) + 1] ?? null;
  }
  setAttribute(name, value) { this.attributes.set(name, String(value)); }
  getAttribute(name) { return this.attributes.get(name) ?? null; }
  appendChild(node) {
    node.remove();
    this.children.push(node);
    node.parentNode = this;
    return node;
  }
  append(...nodes) { for (const node of nodes) this.appendChild(node); }
  insertBefore(node, before) {
    node.remove();
    const index = before === null ? this.children.length : this.children.indexOf(before);
    this.children.splice(index, 0, node);
    node.parentNode = this;
  }
  remove() {
    if (this.parentNode !== null) {
      this.parentNode.children = this.parentNode.children.filter(child => child !== this);
      this.parentNode = null;
    }
  }
  replaceChildren(...nodes) {
    for (const child of [...this.children]) child.remove();
    this.append(...nodes);
    this.textContent = '';
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
  addEventListener(event, listener) { this.listeners.set(event, [...this.listeners.get(event) ?? [], listener]); }
  dispatch(event, detail = {}) {
    const value = { key: '', stopPropagation() {}, preventDefault() {}, ...detail };
    if (event === 'click') this.onclick?.(value);
    for (const listener of this.listeners.get(event) ?? []) listener(value);
  }
  click() { if (!this.disabled) this.dispatch('click'); }
  getBoundingClientRect() { return this.measure?.(this) ?? { left: 6, top: 600, right: 406, bottom: 800, width: 400, height: 200 }; }
}

class CollectionDouble {
  constructor() { this.values = new Map(); }
  get(key) { return this.values.get(key) ?? null; }
  entries() { return this.values.entries(); }
  set(key, value) {
    if (value === null) this.values.delete(key);
    else this.values.set(key, value);
  }
}

class ClientDouble {
  seat = 0;
  seatPlayers = ['owner', null, null, null];
  connectionMode = 'changsha';
  lastGameId = 'room';
  serverSnapshotGameId = 'room';
  online = true;
  sent = [];
  listeners = new Map();
  things = new CollectionDouble();
  claim = new CollectionDouble();
  ownTurn = new CollectionDouble();
  turn = new CollectionDouble();
  gameComplete = new CollectionDouble();
  actionRejected = new CollectionDouble();
  connected() { return this.online; }
  playerId() { return 'owner'; }
  on(event, listener) { this.listeners.set(event, [...this.listeners.get(event) ?? [], listener]); }
  update(entries) { this.sent.push(...entries); }
  emit(event, entries = [], full = false) {
    for (const listener of this.listeners.get(event) ?? []) listener(entries, full);
  }
  receive(entries, full = false) {
    if (full) {
      for (const value of Object.values(this)) {
        if (value instanceof CollectionDouble) value.values.clear();
      }
    }
    for (const [kind, key, value] of entries) {
      this[kind]?.set(key, kind === 'things' && value !== null
        ? { rotationIndex: 0, claimedBy: null, shiftSlotName: null, ...value } : value);
    }
    this.emit('update', entries, full);
  }
}

function fixture(count = 14, drawn = null) {
  const document = {
    body: new NodeDouble('body'),
    createElement: tag => {
      const node = new NodeDouble(tag);
      node.measure = measure;
      return node;
    },
    createComment: () => new NodeDouble(),
    getElementById: id => document.body.querySelector('#' + id),
    querySelector: selector => document.body.querySelector(selector),
  };
  const timers = new Map();
  let timerId = 0, reloads = 0;
  const location = { search: '?gameId=room&variant=changsha&mobileLayout=hybrid', reload() { reloads++; } };
  const view = {
    innerWidth: 412, innerHeight: 915, location,
    getComputedStyle: () => ({ position: 'fixed', display: view.visible ? 'grid' : 'none', visibility: 'visible' }),
    visible: true,
    setTimeout(fn) { timers.set(++timerId, fn); return timerId; },
    clearTimeout(id) { timers.delete(id); },
  };
  function measure(node) {
    if (node.id === 'mobile-discard-recovery') {
      const top = Number.parseFloat(node.style.top) || 112;
      const width = Math.min(440, view.innerWidth - 16);
      return { left: 8, top, right: 8 + width, bottom: top + 62, width, height: 62 };
    }
    const bounds = { left: 6, top: view.innerHeight - 206, right: view.innerWidth - 6,
      bottom: view.innerHeight - 6, width: view.innerWidth - 12, height: 200 };
    if (node.classList.contains('mobile-hand-tile')) {
      const index = node.parentNode.children.indexOf(node);
      const left = 12 + index % 7 * 54, top = bounds.top + 44 + Math.floor(index / 7) * 48;
      return { left, top, right: left + 44, bottom: top + 44, width: 44, height: 44 };
    }
    return bounds;
  }
  const sourceNodes = new Map();
  for (const id of [
    'own-turn-actions', 'own-turn-hu', 'own-turn-concealed-kong', 'own-turn-added-kong', 'own-turn-action-status',
    'own-turn-actions-title', 'rule-action-recovery', 'rule-action-recovery-title',
    'rule-action-recovery-message', 'rule-action-reload', 'pickup-hud', 'roll-dice',
  ]) {
    const element = document.createElement(id.includes('hu') || id.includes('kong') || id.includes('reload') ? 'button' : 'div');
    element.id = id;
    document.body.appendChild(element);
    sourceNodes.set(id, element);
  }
  const overlay = document.createElement('div');
  overlay.className = 'ferro-claim-overlay';
  const timer = document.createElement('span');
  timer.className = 'ferro-claim-timer-value';
  timer.textContent = '—';
  overlay.appendChild(timer);
  document.body.appendChild(overlay);
  const client = new ClientDouble();
  client.ownTurn.set(0, { gameId: 'runtime-room', stateVersion: 7, hu: false, concealedKongs: [], addedKongs: [] });
  client.turn.set('current', { phase: 'AwaitingDiscard', activeSeat: 0, awaitingDiscard: true });
  const notices = [];
  const chooser = { contextKey: null, close() { this.contextKey = null; }, localize() {}, show(_kind, context) { this.contextKey = context; } };
  const domUtils = {
    hideEl(element) { element.hidden = true; element.style.display = ''; },
    setElHidden(element, hidden) { element.hidden = hidden; element.style.display = ''; },
  };
  const translation = { onLanguageChange: () => () => {}, t: (key, values) => key === 'hand.tile'
    ? `${values.rank} ${values.suit}` : ({ 'hand.suit_0': 'Characters', 'hand.suit_1': 'Circles', 'hand.suit_2': 'Bamboo' }[key] ?? key) };
  function load(file, imports) {
    const module = { exports: {} };
    runInNewContext(javascript(file), {
      module, exports: module.exports, URLSearchParams, Date, document, window: view, location,
      require: name => {
        if (!(name in imports)) throw new Error(`Unexpected production import: ${name}`);
        return imports[name];
      },
    });
    return module.exports;
  }
  const { RuleActionControls } = load('src/ui/rule-action-controls.ts', {
    '../dom-utils': domUtils, '../i18n': translation,
    '../toast': { showToast: message => notices.push(message) },
    './rule-action-choice': { RuleActionChoice: class { constructor() { return chooser; } } },
    './rule-action-controls.css': {},
  });
  const actions = new RuleActionControls(client);
  let hand = [], drawnId = drawn;
  const suppressions = [], attempted = [];
  const world = {
    seat: 0, conditions: { gameType: 'CHANGSHA' }, things: new Map(), slots: new Map(),
    ownHandPresentation() { return { tiles: hand.filter(tile => this.ownHandThing(tile.id) !== null), drawn: drawnId }; },
    ownHandThing(id) {
      const thing = this.things.get(id), info = client.things.get(id);
      return thing?.slot.group === 'hand' && thing.slot.seat === client.seat && thing.slot.thing === thing
        && info?.slotName === thing.slot.name && info.face !== null && !thing.hidden && thing.confirmed ? thing : null;
    },
    setOwnHandDomActive(active) { suppressions.push(active); },
    setMobileDiscardGuard(guard) { this.discardGuard = guard; },
    emitDiscard(thing) {
      if (this.discardGuard?.() === false) return false;
      assert.equal(typeof thing, 'object', 'canonical Thing route, never a numeric-ID route');
      attempted.push(thing);
      client.sent.push(['discard', client.seat, { tileId: thing.index }]);
      return true;
    },
  };
  function put(id, face, slotName, hidden = false) {
    const seat = Number(slotName.split('@')[1]);
    const group = slotName.split('.')[0];
    const thing = { index: id, type: 'TILE', typeIndex: face, hidden, hiddenHandle: null, confirmed: true,
      rotationIndex: 0, claimedBy: null, shiftSlot: null,
      slot: { name: slotName, seat, group } };
    thing.slot.thing = thing;
    world.things.set(id, thing);
    world.slots.set(slotName, thing.slot);
    client.things.set(id, { slotName, face, rotationIndex: 0, claimedBy: null, shiftSlotName: null });
    return thing;
  }
  function setHand(items, nextDrawn = null) {
    for (const [id, thing] of world.things) {
      if (thing.slot.group === 'hand') { world.things.delete(id); client.things.set(id, null); }
    }
    hand = items.map(({ id, face }, index) => {
      const slotName = `hand.${index}@0`;
      put(id, face, slotName);
      return { id, face, slotName };
    }).sort((a, b) => a.face - b.face || a.id - b.id);
    drawnId = nextDrawn;
  }
  setHand(Array.from({ length: count }, (_, index) => ({ id: index + 20, face: (index * 7) % 27 })), drawn);
  const { MobileHandController } = load('src/ui/mobile-hand-controller.ts', {
    '../i18n': translation, '../mobile-layout': { resolveMobileLayout },
    '../toast': { showToast: message => notices.push(message) },
    '../types': { GameType: { CHANGSHA: 'CHANGSHA' }, ThingType: { TILE: 'TILE' } },
    './mobile-hand.css': {},
  });
  const controller = new MobileHandController(client, world, actions);
  const dock = () => document.getElementById('mobile-hand-dock');
  const standalone = () => document.getElementById('mobile-discard-recovery');
  const buttons = () => dock()?.querySelectorAll('.mobile-hand-tile') ?? [];
  const discard = () => dock().querySelector('.mobile-hand-discard');
  const refresh = () => controller.refresh('hybrid');
  const snapshot = () => client.receive(Object.entries(client).flatMap(([kind, value]) =>
    value instanceof CollectionDouble ? [...value.entries()].map(([key, entry]) => [kind, key, entry]) : []), true);
  return { document, view, location, client, world, controller, actions, chooser, notices, sourceNodes,
    overlay, timer, timers, suppressions, attempted, put, setHand, dock, standalone, buttons, discard, refresh, snapshot,
    reloads: () => reloads };
}

test('default/camera, landscape, desktop, relay, unbound room and spectator never mount a playable DOM hand', () => {
  for (const change of [
    f => f.controller.refresh(null),
    f => f.controller.refresh('camera'),
    f => { f.view.innerWidth = 915; f.view.innerHeight = 412; return f.refresh(); },
    f => { f.view.innerWidth = 820; f.view.innerHeight = 1180; return f.refresh(); },
    f => { f.client.connectionMode = 'relay'; f.world.conditions.gameType = 'FOUR_PLAYER'; return f.refresh(); },
    f => { f.client.serverSnapshotGameId = null; return f.refresh(); },
    f => { f.client.seat = null; f.document.body.classList.add('spectating'); return f.refresh(); },
    f => { f.client.seatPlayers[0] = 'other'; return f.refresh(); },
    f => { f.client.online = false; return f.refresh(); },
  ]) {
    const f = fixture();
    assert.equal(change(f).active, false);
    assert.equal(f.dock(), null);
    assert.equal(f.standalone(), null);
    assert.deepEqual(f.client.sent, []);
    assert.equal(f.suppressions.at(-1), false);
  }
});

test('thirteen tiles are seven + six, preserving sorted physical IDs and authoritative state', () => {
  const f = fixture(13);
  const before = JSON.stringify([...f.client.things.entries()]);
  assert.equal(f.refresh().active, true);
  const ids = f.buttons().map(button => Number(button.dataset.tileId));
  assert.deepEqual(ids, f.world.ownHandPresentation().tiles.map(tile => tile.id));
  assert.equal(f.buttons().slice(0, 7).length, 7);
  assert.equal(f.buttons().slice(7).length, 6);
  assert.equal(f.buttons().filter(button => button.dataset.drawn === 'true').length, 0);
  assert.equal(JSON.stringify([...f.client.things.entries()]), before);
  assert.deepEqual(f.client.sent, []);
  assert.equal(f.suppressions.at(-1), true);
});

test('fourteen tiles separate the reported drawn ID, never a guessed tile or fabricated extra', () => {
  const f = fixture(14, 20);
  f.refresh();
  assert.equal(f.buttons().length, 14);
  assert.equal(f.buttons().at(-1).dataset.tileId, '20');
  assert.equal(f.buttons().at(-1).classList.contains('mobile-hand-drawn'), true);
  assert.equal(f.buttons().filter(button => button.dataset.drawn === 'true').length, 1);
  f.setHand(Array.from({ length: 14 }, (_, index) => ({ id: index + 20, face: index % 27 })), null);
  f.refresh();
  assert.equal(f.buttons().filter(button => button.dataset.drawn === 'true').length, 0,
    'a full snapshot without draw evidence must not invent a drawn physical ID');
});

test('a release selects; a second tap and Escape cancel without commands; rapid confirmations send one intended Thing', () => {
  const f = fixture();
  f.refresh();
  const chosen = f.buttons()[4], id = Number(chosen.dataset.tileId);
  chosen.dispatch('pointerdown');
  chosen.dispatch('pointerup');
  assert.equal(chosen.getAttribute('aria-pressed'), 'false');
  chosen.click();
  assert.equal(chosen.getAttribute('aria-pressed'), 'true');
  assert.deepEqual(f.client.sent, []);
  chosen.click();
  assert.equal(chosen.getAttribute('aria-pressed'), 'false');
  chosen.click();
  f.dock().dispatch('keydown', { key: 'Escape' });
  assert.equal(chosen.getAttribute('aria-pressed'), 'false');
  assert.deepEqual(f.client.sent, []);
  chosen.click();
  assert.equal(f.discard().dataset.tileId, String(id));
  assert.match(f.discard().getAttribute('aria-label'), /^Confirm discard of /);
  const before = JSON.stringify([...f.client.things.entries()]);
  const button = f.discard();
  button.dispatch('click');
  button.dispatch('click');
  assert.deepEqual(f.client.sent, [['discard', 0, { tileId: id }]]);
  assert.equal(f.attempted[0], f.world.things.get(id));
  assert.equal(f.dock().getAttribute('aria-busy'), 'true');
  assert.equal(f.discard().disabled, true);
  assert.equal(JSON.stringify([...f.client.things.entries()]), before);
});

test('authoritative departure confirms a pending discard, without optimistic tile movement', () => {
  const f = fixture();
  f.refresh();
  const chosen = f.buttons()[0], id = Number(chosen.dataset.tileId);
  chosen.click();
  f.discard().click();
  assert.equal(f.buttons().length, 14);
  const hand = f.world.ownHandPresentation().tiles.filter(tile => tile.id !== id);
  f.setHand(hand);
  f.put(id, 0, 'discard.0.0@0');
  f.client.receive([['things', id, { slotName: 'discard.0.0@0', face: 0 }]]);
  assert.equal(f.dock().getAttribute('aria-busy'), 'false');
  assert.equal(f.buttons().length, 13);
  assert.equal(f.discard().disabled, true);
  assert.deepEqual(f.client.sent.filter(([kind]) => kind === 'discard'), [['discard', 0, { tileId: id }]]);
});

test('selection is revalidated against current phase, version, tile owner and confirmed Thing before emit', () => {
  for (const change of [
    f => f.client.turn.set('current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false }),
    f => f.client.ownTurn.set(0, { ...f.client.ownTurn.get(0), stateVersion: 8 }),
    (f, id) => { f.world.things.get(id).slot.thing = null; },
    (f, id) => { f.world.things.get(id).confirmed = false; },
    (f, id) => f.client.things.set(id, { slotName: 'hand.0@1', face: 0 }),
  ]) {
    const f = fixture();
    f.refresh();
    const chosen = f.buttons()[0], id = Number(chosen.dataset.tileId);
    chosen.click();
    const confirm = f.discard();
    change(f, id);
    confirm.dispatch('click');
    assert.deepEqual(f.client.sent, []);
    assert.equal(f.attempted.length, 0);
  }
});

test('partial and empty manual hands remain visible but non-discardable with existing Roll/Take nodes', () => {
  for (let count = 0; count <= 12; count++) {
    const f = fixture(count);
    f.client.turn.set('current', { phase: 'PickupRound1', activeSeat: null, awaitingDiscard: false });
    f.client.ownTurn.set(0, null);
    f.client.emit('update', [], true);
    assert.equal(f.refresh().active, true);
    assert.equal(f.buttons().length, count);
    assert(f.buttons().every(button => button.disabled));
    assert.equal(f.discard().disabled, true);
    assert(f.dock().contains(f.sourceNodes.get('pickup-hud')));
    assert(f.dock().contains(f.sourceNodes.get('roll-dice')));
    assert.deepEqual(f.client.sent, []);
  }
});

test('pending commands survive timeout and reconnect, never automatically unlock, expire or resend', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  for (const [id, callback] of [...f.timers]) { f.timers.delete(id); callback(); }
  assert.equal(f.discard().disabled, true);
  assert.equal(f.dock().querySelector('.mobile-hand-recovery').hidden, false);
  assert.match(f.dock().querySelector('.mobile-hand-status').textContent, /will not be resent/);
  f.client.online = false;
  f.client.emit('disconnect');
  assert.equal(f.dock(), null);
  assert.equal(f.suppressions.at(-1), false);
  f.client.online = true;
  f.client.emit('connect');
  assert.equal(f.refresh().active, false, 'JOIN ack alone is not a new authoritative snapshot');
  f.client.emit('update', [], true);
  assert.equal(f.dock().getAttribute('aria-busy'), 'true');
  f.discard().dispatch('click');
  assert.equal(f.client.sent.length, 1);
  f.dock().querySelector('.mobile-hand-recovery').click();
  assert.equal(f.reloads(), 1);
  assert.equal(f.client.sent.length, 1);
});

test('an unresolved DOM discard also locks the restored canvas, and confirmation unlocks it while the layout is off', () => {
  const f = fixture();
  f.refresh();
  const selected = Number(f.buttons()[0].dataset.tileId);
  f.buttons()[0].click();
  f.discard().click();
  f.view.innerWidth = 915;
  f.view.innerHeight = 412;
  assert.equal(f.refresh().active, false);
  const other = f.world.ownHandPresentation().tiles.find(tile => tile.id !== selected);
  assert.equal(f.world.emitDiscard(f.world.things.get(other.id)), false);
  assert.equal(f.client.sent.length, 1);
  f.client.online = false;
  f.client.emit('disconnect');
  f.client.online = true;
  f.client.emit('connect');
  f.client.emit('update', [], true);
  assert.equal(f.world.emitDiscard(f.world.things.get(other.id)), false);
  assert.equal(f.client.sent.length, 1);
  f.setHand(f.world.ownHandPresentation().tiles.filter(tile => tile.id !== selected));
  f.put(selected, 0, 'discard.0.0@0');
  f.refresh();
  assert.equal(f.dock(), null);
  assert.equal(f.world.emitDiscard(f.world.things.get(other.id)), true);
  assert.equal(f.client.sent.length, 2);
});

for (const failure of ['timeout', 'transport failure']) {
  for (const exit of ['landscape', 'off']) {
    test(`${failure} recovery survives ${exit} and an unchanged reconnect FULL until authoritative confirmation`, () => {
      const f = fixture();
      const emit = f.world.emitDiscard;
      if (failure === 'transport failure') {
        f.world.emitDiscard = function(thing) {
          const sent = emit.call(this, thing);
          if (sent) throw new Error('transport confirmation lost');
          return sent;
        };
      }
      f.refresh();
      const selected = Number(f.buttons()[0].dataset.tileId);
      f.buttons()[0].click();
      const oldConfirm = f.discard();
      oldConfirm.click();
      f.world.emitDiscard = emit;
      if (failure === 'timeout') {
        for (const [id, callback] of [...f.timers]) { f.timers.delete(id); callback(); }
      }
      if (exit === 'landscape') {
        f.view.innerWidth = 924;
        f.view.innerHeight = 412;
      }
      const mode = exit === 'off' ? null : 'hybrid';
      assert.equal(f.controller.refresh(mode).active, false);
      assert.equal(f.dock(), null);
      assert(f.standalone(), 'pending recovery is independent of the concealed-hand dock');
      const recovery = () => f.standalone().querySelector('.mobile-hand-recovery');
      assert.equal(recovery().hidden, false);
      assert.match(f.standalone().querySelector('.mobile-hand-status').textContent, /will not be resent/);
      assert.equal(f.actions.canDiscard, true, 'production RuleActionControls alone has no mobile flight');
      assert.equal(f.sourceNodes.get('rule-action-recovery').hidden, true);
      const other = f.world.ownHandPresentation().tiles.find(tile => tile.id !== selected);
      oldConfirm.dispatch('click');
      assert.equal(f.world.emitDiscard(f.world.things.get(other.id)), false);
      assert.equal(f.client.sent.length, 1);
      assert.equal(f.attempted.length, 1);
      const oldRecovery = recovery();
      f.client.online = false;
      f.client.emit('disconnect');
      assert.equal(f.standalone(), null, 'disconnected ownership is not validated');
      oldRecovery.dispatch('click');
      assert.equal(f.reloads(), 0);
      f.client.online = true;
      f.client.emit('connect');
      assert.equal(f.standalone(), null, 'a JOIN acknowledgement is not a fresh owned snapshot');
      f.snapshot();
      assert(f.standalone());
      const root = f.standalone();
      f.controller.refresh(mode);
      assert.equal(f.standalone(), root, 'frame refresh preserves the focused recovery button');
      assert.equal(f.actions.canDiscard, true);
      assert.equal(f.world.emitDiscard(f.world.things.get(other.id)), false);
      let stopped = 0;
      for (const event of ['keydown', 'keyup']) {
        for (const key of ['Enter', ' ']) recovery().dispatch(event, { key, stopPropagation() { stopped++; } });
        recovery().dispatch(event, { key: 'p', stopPropagation() { stopped++; } });
      }
      assert.equal(stopped, 4, 'only recovery activation consumes the existing game shortcuts');
      recovery().click();
      assert.equal(f.reloads(), 1);
      assert.equal(f.client.sent.length, 1, 'Refresh never resends the uncertain discard');
      f.setHand(f.world.ownHandPresentation().tiles.filter(tile => tile.id !== selected));
      f.put(selected, 0, 'discard.0.0@0');
      f.snapshot();
      assert.equal(f.standalone(), null);
      assert.equal(f.dock(), null);
      assert.equal(f.world.discardGuard(), true, 'only authoritative departure unlocks the flight');
      oldRecovery.dispatch('click');
      assert.equal(f.reloads(), 1, 'detached recovery cannot reload a resolved or different table');
      assert.equal(f.client.sent.length, 1);
      assert.equal(f.attempted.length, 1);
    });
  }
}

test('standalone pending status and Refresh are available on deactivation even before the confirmation timeout', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.controller.refresh(null);
  assert(f.standalone());
  assert.match(f.standalone().querySelector('.mobile-hand-status').textContent, /Waiting for the server/);
  const recovery = f.standalone().querySelector('.mobile-hand-recovery');
  assert.equal(recovery.hidden, false);
  recovery.click();
  assert.equal(f.reloads(), 1);
  assert.equal(f.client.sent.length, 1);
  assert.equal(f.world.discardGuard(), false);
});

test('standalone recovery never follows lost ownership or an unvalidated room, and stale controls cannot refresh another owner', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.controller.refresh(null);
  const oldRecovery = f.standalone().querySelector('.mobile-hand-recovery');
  f.client.seatPlayers[0] = 'other';
  f.controller.refresh(null);
  assert.equal(f.standalone(), null);
  oldRecovery.dispatch('click');
  assert.equal(f.reloads(), 0);
  assert.equal(f.world.discardGuard(), false, 'lost ownership is not a receipt');
  f.client.seatPlayers[0] = 'owner';
  f.snapshot();
  assert(f.standalone(), 'the same owned flight is still unresolved');
  f.location.search = '?gameId=another-room&variant=changsha';
  f.client.lastGameId = 'another-room';
  f.controller.refresh(null);
  assert.equal(f.standalone(), null);
  oldRecovery.dispatch('click');
  assert.equal(f.reloads(), 0);
  assert.equal(f.world.discardGuard(), false);
  f.client.serverSnapshotGameId = 'another-room';
  f.snapshot();
  assert.equal(f.standalone(), null);
  assert.equal(f.world.discardGuard(), true, 'a different validated owner has no old flight');
  oldRecovery.dispatch('click');
  assert.equal(f.reloads(), 0);
  assert.equal(f.client.sent.length, 1);
});

test('only an owner-matched authoritative discard rejection clears standalone recovery while the layout is off', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.controller.refresh(null);
  f.client.receive([['actionRejected', 'current', {
    action: 'discard', requestedSeat: 1, ownedSeat: 1, reason: 'wrong-phase',
  }]]);
  assert(f.standalone());
  assert.equal(f.world.discardGuard(), false);
  f.client.receive([['actionRejected', 'current', {
    action: 'discard', requestedSeat: 0, ownedSeat: 0, reason: 'wrong-phase',
  }]]);
  assert.equal(f.standalone(), null);
  assert.equal(f.world.discardGuard(), true);
  assert.equal(f.client.sent.length, 1);
});

test('standalone recovery clears restored legal own-turn controls instead of covering their actuators', () => {
  const f = fixture();
  f.sourceNodes.get('own-turn-actions').measure = () =>
    ({ left: 12, top: 104, right: 400, bottom: 220, width: 388, height: 116 });
  f.client.receive([['ownTurn', 0, {
    gameId: 'runtime-room', stateVersion: 7, hu: true, concealedKongs: [], addedKongs: [],
  }]]);
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.controller.refresh(null);
  assert.equal(f.sourceNodes.get('own-turn-actions').hidden, false);
  assert.equal(f.standalone().getBoundingClientRect().top, 228);
  assert.equal(f.world.discardGuard(), false);
  f.client.receive([['ownTurn', 0, null]]);
  assert.equal(f.sourceNodes.get('own-turn-actions').hidden, true);
  assert.equal(f.standalone().style.top, '');
  assert.equal(f.client.sent.length, 1);
});

test('an earlier hybrid flight never changes relay discard behavior', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.client.connectionMode = 'relay';
  f.world.conditions.gameType = 'FOUR_PLAYER';
  f.refresh();
  assert.equal(f.dock(), null);
  assert.equal(f.standalone(), null);
  assert.equal(f.world.discardGuard(), true);
});

test('explicit discard rejection visibly recovers, clears selection, and requires a new deliberate selection', () => {
  const f = fixture();
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  f.client.receive([['actionRejected', 'current', {
    action: 'discard', requestedSeat: 0, ownedSeat: 0, reason: 'wrong-phase',
  }]]);
  assert.equal(f.dock().getAttribute('aria-busy'), 'false');
  assert.equal(f.discard().disabled, true);
  assert.match(f.dock().querySelector('.mobile-hand-status').textContent, /wrong-phase/);
  assert.equal(f.notices.length, 1);
  f.discard().dispatch('click');
  assert.equal(f.client.sent.length, 1);
  f.buttons()[1].click();
  f.discard().click();
  assert.equal(f.client.sent.length, 2);
});

test('a transport exception stays visibly unresolved, not silently swallowed or retried', () => {
  const f = fixture();
  f.world.emitDiscard = () => { throw new Error('socket unavailable'); };
  f.refresh();
  f.buttons()[0].click();
  f.discard().click();
  assert.equal(f.notices.length, 1);
  assert.match(f.notices[0], /socket unavailable/);
  assert.equal(f.dock().getAttribute('aria-busy'), 'true');
  assert.equal(f.discard().disabled, true);
  f.discard().dispatch('click');
  assert.equal(f.notices.length, 1);
  assert.deepEqual(f.client.sent, []);
});

test('the source HUD and timer nodes move reversibly; viewport-only footer inset ignores status/countdown/selection', () => {
  const f = fixture();
  const before = [...f.document.body.children];
  const first = f.refresh();
  assert(f.dock().contains(f.overlay));
  assert.equal(f.overlay.querySelector('.ferro-claim-timer-value'), f.timer);
  f.timer.textContent = '0.1';
  f.buttons()[0].click();
  assert.equal(f.refresh().insetPx, first.insetPx);
  f.client.receive([['claim', '0', { available: ['Pung'], deadline: 0, tile: 4, source: 1 }],
    ['turn', 'current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false }]]);
  assert.equal(f.refresh().insetPx, first.insetPx);
  assert.equal(f.client.sent.length, 0, 'deadline zero creates no local claim/pass');
  f.controller.refresh(null);
  assert.equal(f.dock(), null);
  assert.deepEqual(f.document.body.children, before);
  assert.equal(f.overlay.querySelector('.ferro-claim-timer-value'), f.timer);
  assert.equal(f.timer.textContent, '0.1');
  assert.equal(f.suppressions.at(-1), false);
});

test('reparenting preserves claim/view keyboard shortcuts; only hand activation and selection cancellation consume keys', () => {
  const f = fixture();
  f.refresh();
  let stopped = 0;
  const key = (name, target) => f.dock().dispatch('keydown', {
    key: name, target, stopPropagation() { stopped++; },
  });
  for (const name of ['c', 'k', 'h', 'Escape', 'p']) key(name, f.overlay);
  assert.equal(stopped, 0, 'original claim and perspective shortcuts still reach their existing owner');
  key('Enter', f.buttons()[0]);
  assert.equal(stopped, 1);
  f.buttons()[0].click();
  key('Escape', f.buttons()[0]);
  assert.equal(stopped, 2);
  assert(f.buttons().every(button => button.getAttribute('aria-pressed') === 'false'));
  assert.deepEqual(f.client.sent, []);
});

test('resize/off/spectator/ownership/disconnect clean up the old playable hand and stale selection', () => {
  for (const change of [
    f => { f.view.innerWidth = 915; f.view.innerHeight = 412; },
    f => { f.client.seat = null; },
    f => { f.client.seatPlayers[0] = 'other'; },
    f => { f.client.connectionMode = 'relay'; },
    f => { f.client.online = false; },
  ]) {
    const f = fixture();
    f.refresh();
    f.buttons()[0].click();
    const oldButton = f.discard();
    change(f);
    assert.equal(f.refresh().active, false);
    oldButton.dispatch('click');
    assert.deepEqual(f.client.sent, []);
    assert.equal(f.dock(), null);
    assert.equal(f.suppressions.at(-1), false);
    assert.equal(f.overlay.parentNode, f.document.body);
  }
});

test('no visible complete presentation means no renderer suppression, including missing CSS and overflow', () => {
  const invisible = fixture();
  invisible.view.visible = false;
  assert.equal(invisible.refresh().active, false);
  assert.equal(invisible.suppressions.at(-1), false);
  const overflow = fixture();
  overflow.view.innerWidth = 320;
  assert.equal(overflow.refresh().active, false);
  assert.equal(overflow.suppressions.at(-1), false);
  const unconfirmed = fixture();
  unconfirmed.world.things.get(20).confirmed = false;
  assert.equal(unconfirmed.refresh().active, false, 'an incomplete confirmed presentation must not replace the scene hand');
  assert.equal(unconfirmed.suppressions.at(-1), false);
  const opaque = fixture(0);
  opaque.client.things.set('opaque-own-tile', { slotName: 'hand.0@0', face: null });
  assert.equal(opaque.refresh().active, false);
  assert.equal(opaque.dock(), null);
});

test('public context uses only an explicit claim or one observed discard; initial snapshots and ambiguity are unavailable', () => {
  const f = fixture();
  f.client.emit('joining');
  f.put(4, 1, 'discard.0.0@1');
  f.client.emit('update', [], true);
  f.refresh();
  const context = () => f.dock().querySelector('.mobile-hand-context-label').textContent;
  assert.match(context(), /unavailable/);
  f.client.receive([['claim', '0', { available: ['Pung'], deadline: 0, tile: 4, source: 1 }],
    ['turn', 'current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false }]]);
  assert.match(context(), /^Current claim: 2 Characters · Seat 1/);
  f.client.receive([['claim', '0', null]]);
  f.put(8, 2, 'discard.0.1@1');
  f.client.receive([['things', 8, { slotName: 'discard.0.1@1', face: 2 }]]);
  assert.match(context(), /^Last observed discard: 3 Characters · Seat 1/);
  f.put(12, 3, 'discard.0.2@1');
  f.put(16, 4, 'discard.0.0@2');
  f.client.receive([['things', 12, { slotName: 'discard.0.2@1', face: 3 }],
    ['things', 16, { slotName: 'discard.0.0@2', face: 4 }]]);
  assert.match(context(), /unavailable/);
});

test('continuous FULL snapshots retain an observed discard and identify only a single new public transition', () => {
  const f = fixture();
  f.refresh();
  f.snapshot();
  const context = () => f.dock().querySelector('.mobile-hand-context-label').textContent;
  f.put(4, 1, 'discard.2.5@1');
  f.client.receive([['things', 4, { slotName: 'discard.2.5@1', face: 1 }]]);
  assert.match(context(), /^Last observed discard: 2 Characters · Seat 1/);
  for (let i = 0; i < 3; i++) f.snapshot();
  assert.match(context(), /^Last observed discard: 2 Characters · Seat 1/);
  f.put(8, 2, 'discard.0.0@2');
  f.snapshot();
  assert.match(context(), /^Last observed discard: 3 Characters · Seat 2/,
    'one new discard in a continuous snapshot, not the highest slot, establishes the observed tile');
  f.put(8, 2, 'discard.0.1@2');
  f.snapshot();
  assert.match(context(), /^Last observed discard: 3 Characters · Seat 2/, 'river reflow preserves identity, not order');
  f.put(12, 3, 'discard.0.2@2');
  f.put(16, 4, 'discard.0.0@3');
  f.snapshot();
  assert.match(context(), /unavailable/, 'multiple new discards have no reliable order');
  f.snapshot();
  assert.match(context(), /unavailable/, 'a repeated FULL must not resolve missing ordering');
  assert.deepEqual(f.client.sent, []);
});

test('an explicit public discard claim survives subsequent FULL snapshots, but a robbed kong is not discard history', () => {
  const f = fixture();
  f.refresh();
  f.client.emit('joining');
  f.put(4, 1, 'discard.0.0@1');
  f.client.claim.set('0', { available: ['Pung'], deadline: 0, tile: 4, source: 1 });
  f.client.turn.set('current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false });
  f.snapshot();
  const context = () => f.dock().querySelector('.mobile-hand-context-label').textContent;
  assert.match(context(), /^Current claim: 2 Characters · Seat 1/);
  f.client.claim.set('0', null);
  f.client.turn.set('current', { phase: 'AwaitingDiscard', activeSeat: 0, awaitingDiscard: true });
  f.snapshot();
  assert.match(context(), /^Last observed discard: 2 Characters · Seat 1/);
  assert.equal(f.dock().querySelector('.mobile-hand-context-tile').dataset.tileId, '4');
  assert.equal(f.overlay.querySelector('.ferro-claim-timer-value'), f.timer);
  f.client.things.set(4, null);
  f.put(8, 2, 'meld.0.4@1');
  f.client.claim.set('0', { available: ['Hu'], deadline: 0, tile: 8, source: 1 });
  f.client.turn.set('current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false });
  f.snapshot();
  assert.match(context(), /^Current claim: 3 Characters · Seat 1/);
  f.client.claim.set('0', null);
  f.snapshot();
  assert.match(context(), /unavailable/, 'an exposed robbed-kong claim is not a discard');
  assert.equal(f.dock().querySelector('.mobile-hand-context-tile'), null);
});

test('discard history loses continuity on reconnect or room change and revalidates current public identity', () => {
  for (const change of [
    f => { f.client.emit('disconnect'); f.client.emit('connect'); },
    f => {
      f.location.search = '?gameId=other-room&variant=changsha&mobileLayout=hybrid';
      f.client.lastGameId = f.client.serverSnapshotGameId = 'other-room';
    },
    f => { f.client.things.set(4, null); },
    f => { f.put(4, 1, 'discard.0.0@2'); },
    f => { f.put(4, 2, 'discard.0.0@1'); },
    f => { f.put(4, 1, 'meld.0.0@1'); },
    f => { f.world.things.get(4).hidden = true; },
    f => { f.put(8, 2, 'discard.0.1@1', true); },
  ]) {
    const f = fixture();
    f.refresh();
    f.snapshot();
    f.put(4, 1, 'discard.0.0@1');
    f.client.receive([['things', 4, { slotName: 'discard.0.0@1', face: 1 }]]);
    assert.match(f.dock().querySelector('.mobile-hand-context-label').textContent, /^Last observed discard:/);
    change(f);
    f.snapshot();
    const label = f.dock().querySelector('.mobile-hand-context-label').textContent;
    assert.doesNotMatch(label, /^(Current claim|Last observed discard):/);
    assert.equal(f.dock().querySelector('.mobile-hand-context-tile'), null);
    assert.deepEqual(f.client.sent, []);
  }
});

test('an authoritative empty discard turn says waiting, without claiming first-discard history or hiding unknown ordering', () => {
  for (const count of [0, 14]) {
    const f = fixture(count);
    f.refresh();
    const context = () => f.dock().querySelector('.mobile-hand-context-label').textContent;
    assert.equal(context(), 'Waiting for a discard');
    f.put(4, 1, 'meld.0.0@1');
    f.snapshot();
    assert.equal(context(), 'Waiting for a discard', 'waiting does not assert that no earlier discard was claimed');
    for (const turn of [
      { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false },
      { phase: 'PickupRound1', activeSeat: null, awaitingDiscard: false },
      { phase: 'AwaitingDiscard', activeSeat: null, awaitingDiscard: true },
      { phase: 'AwaitingDiscard', activeSeat: 0, awaitingDiscard: false },
    ]) {
      f.client.turn.set('current', turn);
      f.snapshot();
      assert.match(context(), /unavailable/);
    }
    f.put(8, 2, 'discard.0.0@2');
    f.client.emit('joining');
    f.client.turn.set('current', { phase: 'AwaitingDiscard', activeSeat: 0, awaitingDiscard: true });
    f.snapshot();
    assert.match(context(), /unavailable/, 'an initial river snapshot supplies no latest-discard ordering');
  }
});

test('a public discard reflow is not fresh last-discard evidence and a visible robbed-kong tile is usable claim context', () => {
  const f = fixture();
  f.client.emit('joining');
  f.put(4, 1, 'discard.0.1@1');
  f.client.emit('update', [], true);
  f.put(4, 1, 'discard.0.0@1');
  f.client.receive([['things', 4, { slotName: 'discard.0.0@1', face: 1 }]]);
  f.refresh();
  assert.match(f.dock().querySelector('.mobile-hand-context-label').textContent, /unavailable/);
  f.put(8, 2, 'meld.0.4@1');
  f.client.receive([['claim', '0', { available: ['Hu'], deadline: 0, tile: 8, source: 1 }],
    ['turn', 'current', { phase: 'AwaitingClaim', activeSeat: null, awaitingDiscard: false }]]);
  assert.match(f.dock().querySelector('.mobile-hand-context-label').textContent, /^Current claim: 3 Characters/);
});

test('readonly canDiscard reuses authoritative own-turn context, phase, ownership, snapshot and pending validations', () => {
  for (const change of [
    f => f.client.turn.set('current', { phase: 'PickupRound1', activeSeat: 0, awaitingDiscard: true }),
    f => f.client.turn.set('current', { phase: 'AwaitingDiscard', activeSeat: 1, awaitingDiscard: true }),
    f => { f.client.serverSnapshotGameId = null; },
    f => { f.client.seatPlayers[0] = 'other'; },
    f => { f.client.online = false; },
    f => { f.client.connectionMode = 'relay'; },
    f => { f.client.gameComplete.set('current', { isComplete: true }); },
    f => { f.chooser.contextKey = 'open-choice'; },
  ]) {
    const f = fixture();
    assert.equal(f.actions.canDiscard, true);
    change(f);
    assert.equal(f.actions.canDiscard, false);
  }
  const f = fixture();
  const descriptor = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(f.actions), 'canDiscard');
  assert.equal(descriptor.set, undefined);
  f.client.receive([['ownTurn', 0, { gameId: 'runtime-room', stateVersion: 8, hu: true, concealedKongs: [], addedKongs: [] }]]);
  f.sourceNodes.get('own-turn-hu').click();
  assert.equal(f.actions.canDiscard, false);
});

test('ordinary authoritative discard turns do not require Hu/Kong metadata that the server legitimately omits', () => {
  const f = fixture();
  f.client.ownTurn.set(0, null);
  f.snapshot();
  assert.equal(f.actions.canDiscard, true);
  f.refresh();
  assert.equal(f.buttons().length, 14);
  assert(f.buttons().every(button => !button.disabled));
  f.buttons()[0].click();
  f.discard().click();
  assert.equal(f.client.sent.length, 1);
});

test('the actual lobby URL builder preserves one valid layout flag and drops invalid or repeated flags', () => {
  const source = ts.createSourceFile('lobby.ts', fs.readFileSync(path.join(root, 'src/lobby.ts'), 'utf8'), ts.ScriptTarget.Latest, true);
  const builder = source.statements.find(node => ts.isFunctionDeclaration(node) && node.name?.text === 'buildUrl');
  const body = ts.createPrinter().printNode(ts.EmitHint.Unspecified, builder, source);
  const state = { variant: 'changsha', dealMode: 'auto', botCount: 3, botDifficulty: 'Easy', handCount: 1, baseUnit: 1, seed: 7, seat: 0 };
  for (const [search, expected, expectedTable = null] of [
    ['?gameId=room&mobileLayout=camera', 'camera'], ['?gameId=room&mobileLayout=hybrid', 'hybrid'],
    ['?gameId=room&mobileLayout=readable', 'readable'],
    ['?gameId=room', null], ['?mobileLayout=hybrid&mobileLayout=hybrid', null],
    ['?mobileLayout=hybrid&mobileLayout=camera', null], ['?mobileLayout=Hybrid', null], ['?mobileLayout=unknown', null],
    ['?mobileLayout=readable&mobileLayout=hybrid', null],
    ['?mobileLayout=hybrid&tableLayout=compact', 'hybrid', 'compact'],
    ['?tableLayout=compact', null, 'compact'],
    ['?tableLayout=compact&tableLayout=compact', null],
    ['?tableLayout=Compact', null],
  ]) {
    const build = runInNewContext(ts.transpileModule(`${body}\nbuildUrl;`, {
      compilerOptions: { target: ts.ScriptTarget.ES2022 },
    }).outputText, {
      URLSearchParams, parseMobileLayout, parseTableLayout, window: { location: { search, pathname: '/autotable/' } },
      isJoinOnly: () => false, mintFreshGameId: () => 'new-room', resolveApplyGameId: () => 'room',
      readConcreteGameId: () => 'room', hasNewGameIntent: () => false, setNewGameIntent: () => {},
    });
    const url = new URL(build(state), 'http://localhost');
    assert.equal(url.searchParams.get('mobileLayout'), expected);
    assert.equal(url.searchParams.get('tableLayout'), expectedTable);
    assert.equal(url.searchParams.get('variant'), 'changsha');
    assert.equal(url.searchParams.get('gameId'), 'room');
    assert.equal(url.searchParams.get('seat'), '0');
  }
});

test('readable mode reuses the same validated hand and explicit-confirm path, and restores the scene when disabled', () => {
  const f = fixture();
  f.location.search = '?gameId=room&variant=changsha&mobileLayout=readable';
  assert.equal(f.controller.refresh('readable').active, true);
  assert.equal(f.dock().dataset.mobileLayout, 'readable');
  assert.equal(f.buttons().length, 14);
  f.buttons()[0].click();
  assert.equal(f.client.sent.length, 0);
  f.discard().click();
  assert.equal(f.client.sent.length, 1);
  assert.equal(f.controller.refresh(null).active, false);
  assert.equal(f.dock(), null);
  assert.equal(f.suppressions.at(-1), false);
});

test('readonly public context reuses verified history and never derives a face from an identifier', () => {
  const f = fixture();
  f.refresh();
  assert.equal(f.controller.publicTileContext.kind, 'waiting');
  f.put(4, 1, 'discard.0.0@1');
  f.client.receive([['things', 4, { slotName: 'discard.0.0@1', face: 1 }]]);
  assert.deepEqual(JSON.parse(JSON.stringify(f.controller.publicTileContext)), {
    kind: 'discard', seat: 1, face: 1, slot: 'discard.0.0@1',
  });
  const wire = { ...f.client.things.get(4) };
  delete wire.face;
  f.client.things.set(4, wire);
  assert.equal(f.controller.publicTileContext.kind, 'discard',
    'a missing legacy face uses the confirmed public Thing that already has entitlement');
  for (const face of [null, 2, NaN, 27]) {
    f.client.things.set(4, { ...wire, face });
    assert.equal(f.controller.publicTileContext.kind, 'unavailable');
  }
  f.client.things.set(4, { ...wire, face: 1 });
  f.client.emit('disconnect');
  assert.equal(f.controller.publicTileContext.kind, 'unavailable');
  assert.deepEqual(f.client.sent, []);
});

test('readable projection keeps the owner concealed kong face-down and refuses incoherent public poses', () => {
  const f = fixture();
  f.refresh();
  f.put(4, 1, 'meld.0.4@0');
  f.world.things.get(4).rotationIndex = 2;
  const masked = { ...f.client.things.get(4), rotationIndex: 2 };
  delete masked.face;
  f.client.things.set(4, masked);
  assert.equal(new Map(f.controller.publicBoardEntries).get(4).face, null,
    'even the entitled owner sees a back for the public representation of a concealed kong');
  assert.equal(f.client.things.get(4).face, undefined);
  for (const change of [
    fixture => fixture.world.slots.delete('discard.0.0@1'),
    fixture => { fixture.world.things.get(8).claimedBy = 0; },
    fixture => { fixture.world.things.get(8).shiftSlot = { name: 'meld.0.0@1' }; },
    fixture => { fixture.client.things.get(8).rotationIndex = 1; },
    fixture => { fixture.client.things.get(8).face = 3; },
    fixture => { fixture.world.things.get(8).hiddenHandle = 'masked'; },
  ]) {
    const current = fixture();
    current.refresh();
    current.put(8, 2, 'discard.0.0@1');
    change(current);
    assert.equal(new Map(current.controller.publicBoardEntries).get(8).face, undefined);
    assert.deepEqual(current.client.sent, []);
  }
});

test('readable entries hydrate only missing entitled public faces from coherent Things, never private or masked tiles', () => {
  const f = fixture();
  f.refresh();
  f.put(4, 1, 'discard.0.0@1');
  f.put(8, 2, 'meld.0.0@2');
  for (const id of [4, 8]) {
    const entry = f.client.things.get(id);
    delete entry.face;
  }
  const entries = () => new Map(f.controller.publicBoardEntries);
  assert.equal(entries().get(4).face, 1);
  assert.equal(entries().get(8).face, 2);
  assert.equal(f.client.things.get(4).face, undefined, 'the authoritative collection is never mutated');
  f.put(12, 3, 'hand.0@1');
  delete f.client.things.get(12).face;
  assert.equal(entries().get(12).face, undefined, 'an opponent hand is never hydrated');
  f.client.things.set('private-kong', { slotName: 'meld.0.1@2', face: null });
  assert.equal(entries().get('private-kong').face, null);
  f.client.things.set(4, { slotName: 'discard.0.0@1', face: null });
  assert.equal(entries().get(4).face, null);
  f.world.things.get(8).hidden = true;
  assert.equal(entries().get(8).face, undefined);
  f.client.emit('disconnect');
  assert.equal(entries().size, 0);
  assert.deepEqual(f.client.sent, []);
});
