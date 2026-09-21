import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';
import { EventEmitter } from 'node:events';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const ts = require('typescript');
const javascript = source => ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

class Element {
  textContent = '';
  style = {};
  children = [];
  dataset = {};
  attributes = {};
  hidden = false;
  setAttribute(name, value) { this.attributes[name] = value; }
  set innerHTML(value) { this.children = []; this.textContent = value; }
  appendChild(child) { this.children.push(child); }
}

const guiSource = ts.createSourceFile('game-ui.ts', fs.readFileSync(path.join(root, 'src/game-ui.ts'), 'utf8'), ts.ScriptTarget.Latest, true);
const gui = guiSource.statements.find(s => ts.isClassDeclaration(s) && s.name?.text === 'GameUi');
const renderer = gui.members.find(m => ts.isMethodDeclaration(m) && m.name.getText(guiSource) === 'renderResult');
const readiness = gui.members.find(m => ts.isMethodDeclaration(m) && m.name.getText(guiSource) === 'renderResultReadiness');
const renderSource = [renderer, readiness].map(method =>
  ts.createPrinter().printNode(ts.EmitHint.Unspecified, method, guiSource)).join('\n');
const nodes = new Map();
const en = JSON.parse(fs.readFileSync(path.join(root, 'src/i18n/en.json')));
const RenderFixture = runInNewContext(javascript('class RenderFixture {' + renderSource + '}\nRenderFixture;'), {
  document: { createElement: () => new Element(), getElementById: id => nodes.get(id) ?? null },
  t: (key, params = {}) => (en[key] ?? key).replace(/\{(\w+)\}/g, (whole, name) => String(params[name] ?? whole)),
});

function readinessUi() {
  nodes.clear();
  for (const id of ['result-dialog-status', 'result-dialog-error', 'result-refresh']) nodes.set(id, new Element());
  const ui = new RenderFixture();
  ui.elements = { resultNext: new Element(), resultModal: new Element() };
  ui.nickForSeat = seat => seat === 3 ? 'Human Three' : 'Human One';
  return ui;
}
const presentation = mode => ({ identity: 'hand-identity', visible: true, mode, waitingSeats: [3], error: null });

test('actual readiness DOM labels required Continue and observer Dismiss distinctly', () => {
  const ui = readinessUi();
  ui.renderResultReadiness(presentation('continue'));
  assert.equal(ui.elements.resultNext.textContent, 'Continue');
  assert.equal(ui.elements.resultNext.disabled, false);
  assert.match(nodes.get('result-dialog-status').textContent, /waits for every human/);
  ui.renderResultReadiness(presentation('observer'));
  assert.equal(ui.elements.resultNext.textContent, 'Dismiss');
  assert.equal(ui.elements.resultNext.disabled, false);
});

test('actual readiness DOM disables pending/acknowledged owners and names the remaining players', () => {
  const ui = readinessUi();
  for (const mode of ['pending', 'waiting', 'advancing']) {
    ui.renderResultReadiness(presentation(mode));
    assert.equal(ui.elements.resultNext.disabled, true);
    assert.equal(ui.elements.resultNext.attributes['aria-busy'], String(mode === 'pending'));
    if (mode === 'waiting') assert.match(nodes.get('result-dialog-status').textContent, /Waiting.*Human Three/);
  }
});

test('actual readiness DOM gives reconnect/invalid-data recovery without enabling Continue', () => {
  const ui = readinessUi();
  for (const mode of ['reconnecting', 'invalid']) {
    ui.renderResultReadiness(presentation(mode));
    assert.equal(ui.elements.resultNext.disabled, true);
    assert.equal(nodes.get('result-refresh').hidden, false);
    assert.equal(nodes.get('result-refresh').textContent, 'Refresh status');
  }
});

test('actual readiness DOM shows the exact rejection, retaining the result for an explicit retry', () => {
  const ui = readinessUi();
  ui.renderResultReadiness({ ...presentation('continue'), error: 'stale-hand-result' });
  assert.equal(nodes.get('result-dialog-error').hidden, false);
  assert.match(nodes.get('result-dialog-error').textContent, /stale-hand-result/);
  assert.equal(ui.elements.resultNext.disabled, false);
  assert.equal(ui.elements.resultModal.dataset.continuationState, 'continue');
  ui.renderResultReadiness(presentation('pending'));
  assert.equal(nodes.get('result-dialog-error').hidden, true);
});

for (const [description, type, winner, expected] of [
  ['human win', 'Hu', 0, 'Human One 赢了 wins!'],
  ['bot win', 'Hu', 2, 'Bot 2 赢了 wins!'],
  ['draw', 'Draw', -1, 'No winner this hand'],
  ['false win', 'ZhaHu', 0, 'Human One declared a false win'],
]) {
  test(`actual result renderer retains readable winner and authoritative score rows for ${description}`, () => {
    const ui = new RenderFixture();
    ui.elements = Object.fromEntries(['resultHeadline', 'resultWinner', 'resultScoreBody', 'resultHand'].map(key => [key, new Element()]));
    ui.nickForSeat = seat => seat === 0 ? 'Human One' : 'Bot ' + seat;
    ui.renderResultWinTypeBadge = ui.renderResultPatternChips = ui.renderResultScoreBreakdown = () => {};
    const score = Object.freeze([
      Object.freeze({ seat: 2, delta: 12 }), Object.freeze({ seat: 0, delta: -4 }),
      Object.freeze({ seat: 3, delta: -4 }), Object.freeze({ seat: 1, delta: -4 }),
    ]);
    const payload = Object.freeze({ type, winner, score, hand: Object.freeze([]), nextBanker: 2 });
    ui.renderResult(payload);
    assert.equal(ui.elements.resultWinner.textContent, expected);
    assert.equal(ui.elements.resultScoreBody.children.length, 4);
    assert.deepEqual(ui.elements.resultScoreBody.children.map(row => row.children[0].textContent), ['0', '1', '2', '3']);
    assert.deepEqual(ui.elements.resultScoreBody.children.map(row => row.children[2].textContent), ['-4', '-4', '+12', '-4']);
    assert.deepEqual(score.map(row => row.seat), [2, 0, 3, 1]);
  });
}

function transport() {
  const sockets = [];
  class Socket {
    static OPEN = 1;
    static CLOSED = 3;
    static CLOSING = 2;
    readyState = 0;
    sent = [];
    constructor() { sockets.push(this); }
    send(message) { this.sent.push(JSON.parse(message)); }
    open() { this.readyState = 1; this.onopen?.(); }
    receive(message) { this.onmessage?.({ data: JSON.stringify(message) }); }
    close() { this.readyState = 3; this.onclose?.(); }
  }
  const modules = {
    events: { EventEmitter },
    './reconnect': { clearSession() {}, saveSession() {} },
    './profile': { loadProfile: async () => ({ displayName: 'Human One' }), refreshProfile: async () => {},
      snapshotStatsForGame() {}, onProfile() {}, initProfileHubBindings() {} },
    './hub': { getHubConnection: async () => ({}) },
    './game-state': { clearGameState() {}, refreshGameState: async () => {}, setGameRoomConnected() {} },
  };
  const load = name => {
    const module = { exports: {} };
    runInNewContext(javascript(fs.readFileSync(path.join(root, 'src', name), 'utf8')), {
      module, exports: module.exports,
      require: id => {
        if (!(id in modules)) throw new Error('Unexpected source dependency: ' + id);
        return modules[id];
      },
      window: { location: { href: 'http://result.test/autotable/?variant=changsha' } },
      URL, URLSearchParams, WebSocket: Socket, setInterval: () => 1, clearInterval() {},
      console: { log() {}, error() {} },
    });
    return module.exports;
  };
  modules['./base-client'] = load('base-client.ts');
  const { Client } = load('client.ts');
  const client = new Client('changsha');
  const connect = (seat = 0) => {
    client.join('ws://result.test/autotable/ws?variant=changsha', 'browser-room-alias');
    const socket = sockets.at(-1);
    socket.open();
    socket.receive({ type: 'JOINED', gameId: 'browser-room-alias', playerId: 'signed-player', isFirst: true,
      viewer: { roomId: 'browser-room-alias', revision: 1, seat } });
    return socket;
  };
  return { client, connect };
}

test('actual Client collection emits only the frozen UPDATE/handResultAck envelope', () => {
  const h = transport();
  const socket = h.connect();
  assert.equal(socket.sent.flatMap(frame => frame.entries ?? []).some(([kind]) => kind === 'handResultAck'), false);
  const command = { gameId: 'canonical-runtime-guid', handNumber: 1, resultToken: 'opaque-token' };
  h.client.handResultAck.set('current', command);
  assert.deepEqual(socket.sent.at(-1), { type: 'UPDATE', entries: [['handResultAck', 'current', command]], full: false });
});

test('actual Client reconnect and full snapshots never replay or invent an acknowledgement', () => {
  const h = transport();
  const first = h.connect();
  h.client.handResultAck.set('current', { gameId: 'canonical-runtime-guid', handNumber: 1, resultToken: 'old-token' });
  h.client.disconnect();
  // Even a local disconnected cache write cannot turn into a reconnect command.
  h.client.handResultAck.set('current', { gameId: 'canonical-runtime-guid', handNumber: 1, resultToken: 'cached-token' });
  const second = h.connect();
  second.receive({ type: 'UPDATE', full: true, viewer: { roomId: 'browser-room-alias', revision: 1, seat: 0 },
    entries: [['result', 'current', { winner: 2, type: 'Hu', score: [], hand: [], nextBanker: 2,
      continuation: { gameId: 'canonical-runtime-guid', handNumber: 1, resultToken: 'held-token',
        requiredSeats: [0], acknowledgedSeats: [], waitingSeats: [0] } }]] });
  assert.equal(first.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'handResultAck').length, 1);
  assert.equal(second.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'handResultAck').length, 0);
  assert.equal(h.client.result.get('current').continuation.resultToken, 'held-token');
});
