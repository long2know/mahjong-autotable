import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runInNewContext } from 'node:vm';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const ts = require('typescript');
const THREE = require('three');
const compile = filename => ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
require.extensions['.ts'] = (module, filename) => module._compile(compile(filename), filename);
const { CompactTableProfile, COMPACT_CAPACITY_WIDTH } = require(path.join(root, 'src/compact-table.ts'));
const policy = require(path.join(root, 'src/table-layout.ts'));
const { makeSlots } = require(path.join(root, 'src/setup-slots.ts'));
const { Slot } = require(path.join(root, 'src/slot.ts'));
const { Setup } = require(path.join(root, 'src/setup.ts'));
const { TileThingGroup } = require(path.join(root, 'src/thing-group.ts'));
const types = require(path.join(root, 'src/types.ts'));
const EPS = 1e-5;
const noop = () => {};
const viewport = { innerWidth: 412, innerHeight: 915, location: { search: '' } };
const bodyAttributes = new Map();
const document = {
  body: {
    setAttribute: (key, value) => bodyAttributes.set(key, value),
    removeAttribute: key => bodyAttributes.delete(key),
    classList: { contains: () => false },
  },
  documentElement: {}, getElementById: () => null, querySelector: () => null, querySelectorAll: () => [],
  createElement: () => ({
    style: {}, dataset: {}, textContent: '', children: [],
    setAttribute(key, value) { this[key] = value; },
    appendChild(child) { this.children.push(child); },
  }),
};
const load = (name, stubs = {}) => {
  const module = { exports: {} };
  runInNewContext(compile(path.join(root, 'src', name + '.ts')), {
    module, exports: module.exports, console, URLSearchParams, window: viewport, document,
    getComputedStyle: () => ({ getPropertyValue: () => '0', visibility: 'visible' }),
    require: id => stubs[id] ?? (id === 'three' ? THREE : require(path.join(root, 'src', id))),
  });
  return module.exports;
};
const { World } = load('world', {
  './client-ui': { readSpectatorFromUrl: () => false }, './toast': { showToast: noop },
});
const { MainView } = load('main-view', {
  './world': { World }, './render/custom-outline': { CustomOutline: class {} },
});
const { ObjectView } = load('object-view', {
  './world': { World }, './client-ui': { readVariantFromUrl: () => 'CHANGSHA' },
  './center': { Center: class {
    mesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshLambertMaterial());
    setScores() {}
    draw() {}
  } },
});
let statusLocale = 'en';
const { CompactTableStatus } = load('compact-table-status', {
  './i18n': { getActiveLocale: () => statusLocale },
});
const { Game } = load('game', Object.fromEntries([
  'object-view', 'world', 'client', 'base-client', 'asset-loader', 'utils',
  'mouse-ui', 'main-view', 'client-ui', 'sound-player',
].map(id => ['./' + id, {}])));
const { MouseUi } = load('mouse-ui', { './world': { World } });

const { GLTFLoader } = await import(pathToFileURL(require.resolve('three/examples/jsm/loaders/GLTFLoader.js')));
const loader = new GLTFLoader();
loader.register(() => ({
  name: 'CPU_GEOMETRY_NO_TEXTURE_IO',
  loadMaterial: () => Promise.resolve(new THREE.MeshLambertMaterial()),
}));
const bytes = fs.readFileSync(path.join(root, 'img/models.auto.glb'));
const model = await new Promise((resolve, reject) => loader.parse(
  bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '', resolve, reject));
const original = model.scene.getObjectByName('tile');
original.geometry.computeBoundingBox();
const faceVertices = [];
const positions = original.geometry.attributes.position, uv = original.geometry.attributes.uv;
for (let i = 0; i < positions.count; i++) {
  if (uv.getX(i) <= 32 / 256 + 1e-6 && uv.getY(i) <= 40 / 256 + 1e-6) {
    faceVertices.push(new THREE.Vector3().fromBufferAttribute(positions, i));
  }
}
const clone = name => {
  const mesh = model.scene.getObjectByName(name).clone();
  mesh.geometry = mesh.geometry.clone();
  mesh.material = mesh.material.clone();
  return mesh;
};
const assets = {
  meshes: { tile: clone('tile'), stick: clone('stick') }, textures: { tilesLabels: null },
  make: clone, makeMarker: () => clone('marker'),
  makeTable: () => new THREE.Mesh(new THREE.PlaneGeometry(183, 183), new THREE.MeshLambertMaterial()),
};
const matrixFor = place => new THREE.Matrix4().compose(place.position, place.rotation,
  new THREE.Vector3().setScalar(place.scale ?? 1));
const boundsFor = place => original.geometry.boundingBox.clone().applyMatrix4(matrixFor(place));
const penetrates = (a, b) => ['x', 'y', 'z'].every(axis =>
  Math.min(a.max[axis], b.max[axis]) - Math.max(a.min[axis], b.min[axis]) > EPS);
const vectorNear = (a, b) => assert(a.distanceTo(b) < EPS, `${a.toArray()} != ${b.toArray()}`);
const slotMap = () => {
  const slots = new Map(makeSlots('CHANGSHA').map(slot => [slot.name, slot]));
  Slot.setLinks(slots);
  return slots;
};
const source = (slots, name, rotation = 0) => ({ slot: slots.get(name), place: slots.get(name).placeWithOffset(rotation) });

test('the cold-bootstrap parser is Three-free, exact, singular and fail-closed', () => {
  const imports = [];
  const module = { exports: {} };
  runInNewContext(compile(path.join(root, 'src/table-layout.ts')), {
    module, exports: module.exports, URLSearchParams,
    require: name => { imports.push(name); throw new Error('Cold parser imported a dependency'); },
  });
  assert.deepEqual(imports, []);
  for (const search of ['', '?tableLayout', '?tableLayout=Compact', '?tableLayout=compact%20',
    '?tableLayout=unknown', '?tableLayout=compact&tableLayout=compact',
    '?tableLayout=compact&tableLayout=', '?TABLELAYOUT=compact']) {
    assert.equal(module.exports.parseTableLayout(search), null, search);
  }
  assert.equal(module.exports.parseTableLayout('?mobileLayout=hybrid&tableLayout=compact'), 'compact');
  assert.equal(module.exports.parseTableLayout('?tableLayout=compact&unrelated=1'), 'compact');
});

test('only live, room-bound, post-deal Changsha 3D context can activate the profile', () => {
  const good = {
    connected: true, connectionMode: 'changsha', changsha: true,
    snapshotGameId: 'room-a', lastGameId: 'room-a', threeDimensional: true, phase: 'AwaitingDiscard',
  };
  for (const phase of ['AwaitingDiscard', 'AwaitingClaim', 'DeclaringKong', 'DrawingReplacement',
    'Scoring', 'EndHand', 'RotatingBanker', 'WallExhausted', 'GameComplete']) {
    assert.equal(policy.resolveTableLayout('compact', { ...good, phase }), 'compact');
  }
  for (const phase of [undefined, null, 3, {}, '', 'Seating', 'RollingDice', 'Dealing', 'BreakPointMarked',
    'PickupRound1', 'PickupRound2', 'PickupRound3', 'SingleTilePickup', 'DealerExtra', 'inPlay', 'unknown']) {
    assert.equal(policy.resolveTableLayout('compact', { ...good, phase }), null, phase);
  }
  for (const changes of [
    { connected: false }, { connectionMode: 'relay' }, { connectionMode: 'offline' }, { changsha: false },
    { snapshotGameId: null }, { lastGameId: null }, { snapshotGameId: 'old-room' }, { threeDimensional: false },
  ]) assert.equal(policy.resolveTableLayout('compact', { ...good, ...changes }), null);
  assert.equal(policy.resolveTableLayout(null, good), null);
});

for (const seat of [0, 1, 2, 3]) {
  for (const kind of ['flat', 'concealed', 'added', 'sideways']) {
    test(`seat ${seat}: 18 rivers + four ${kind} melds use actual GLB bounds without unrelated collisions`, () => {
      const slots = slotMap(), sources = [];
      for (let row = 0; row < 3; row++) for (let column = 0; column < 6; column++) {
        sources.push(source(slots, `discard.${row}.${column}@${seat}`));
      }
      for (let group = 0; group < 4; group++) {
        for (let tile = 0; tile < (kind === 'flat' || kind === 'concealed' ? 4 : 3); tile++) {
          sources.push(source(slots, `meld.${group}.${tile}@${seat}`,
            kind === 'concealed' ? 2 : kind === 'sideways' && tile === 1 ? 1 : 0));
        }
        if (kind === 'added') sources.push(source(slots, `meld.${group}.4@${seat}`));
      }
      // Each neighbouring seat has one public set and one discard, not an
      // impossible simultaneous four-seat maximum inventory.
      for (const other of [0, 1, 2, 3].filter(s => s !== seat)) {
        sources.push(source(slots, `discard.0.0@${other}`));
        for (let tile = 0; tile < 3; tile++) sources.push(source(slots, `meld.0.${tile}@${other}`));
      }
      assert(sources.length <= 46);
      const before = sources.map(s => JSON.stringify({
        name: s.slot.name, origin: s.slot.origin, offset: s.slot.offset, rotations: s.slot.rotations,
        position: s.place.position, rotation: s.place.rotation, size: s.place.size,
      }));
      const profile = new CompactTableProfile(sources, { ownSeat: null, handScale: 1.7 });
      assert(profile.requiredWidth <= profile.capacityWidth + EPS);
      const bodies = sources.map(s => ({ ...s, presented: profile.present(s.slot, s.place) }));
      for (const body of bodies) {
        body.bounds = boundsFor(body.presented);
        assert.equal(body.presented.rotation, body.place.rotation);
        assert.equal(body.presented.scale, 2);
        vectorNear(body.bounds.getSize(new THREE.Vector3()), body.presented.size);
        assert(body.bounds.min.x >= profile.feltBounds.min.x - EPS);
        assert(body.bounds.max.x <= profile.feltBounds.max.x + EPS);
        assert(body.bounds.min.y >= profile.feltBounds.min.y - EPS);
        assert(body.bounds.max.y <= profile.feltBounds.max.y + EPS);
      }
      for (let i = 0; i < bodies.length; i++) for (let j = i + 1; j < bodies.length; j++) {
        assert(!penetrates(bodies[i].bounds, bodies[j].bounds),
          `${bodies[i].slot.name} intersects ${bodies[j].slot.name}`);
      }
      for (const perspective of [true, false]) {
        const h = harness({ perspective, seat });
        h.view.setCompactTable(profile);
        h.view.updateCamera(seat, 0, 0, null, 'CHANGSHA');
        const area = h.view.playArea();
        for (const body of bodies) {
          for (const x of [body.bounds.min.x, body.bounds.max.x]) {
            for (const y of [body.bounds.min.y, body.bounds.max.y]) for (const z of [body.bounds.min.z, body.bounds.max.z]) {
              const p = new THREE.Vector3(x, y, z).project(h.view.camera);
              const px = (p.x + 1) * h.view.width / 2, py = (1 - p.y) * h.view.height / 2;
              assert(px >= area.left - EPS && px <= area.right + EPS, `${body.slot.name}: clipped x=${px}`);
              assert(py >= area.top - EPS && py <= area.bottom + EPS, `${body.slot.name}: clipped y=${py}`);
              assert(p.z >= -1 && p.z <= 1);
            }
          }
        }
      }
      if (kind === 'added') for (let group = 0; group < 4; group++) {
        const base = bodies.find(b => b.slot.name === `meld.${group}.1@${seat}`);
        const top = bodies.find(b => b.slot.name === `meld.${group}.4@${seat}`);
        assert(Math.abs(base.bounds.max.z - top.bounds.min.z) < EPS);
        const a = base.bounds.getCenter(new THREE.Vector3()), b = top.bounds.getCenter(new THREE.Vector3());
        assert(Math.hypot(a.x - b.x, a.y - b.y) < EPS);
      }
      sources.forEach((s, index) => assert.equal(JSON.stringify({
        name: s.slot.name, origin: s.slot.origin, offset: s.slot.offset, rotations: s.slot.rotations,
        position: s.place.position, rotation: s.place.rotation, size: s.place.size,
      }), before[index]));
    });
  }
}

test('profile uses no private fields and composes an existing uniform scale instead of replacing it', () => {
  const slots = slotMap();
  const input = source(slots, 'meld.0.0@0', 2);
  Object.defineProperties(input, {
    face: { get() { throw new Error('private face read'); } },
    hiddenHandle: { get() { throw new Error('opaque handle decoded'); } },
  });
  const originalPlace = input.place;
  input.place = {
    ...originalPlace, scale: 1.2,
    size: originalPlace.size.clone().multiplyScalar(1.2),
    position: originalPlace.position.clone().multiplyScalar(1.2),
  };
  const profile = new CompactTableProfile([input], { ownSeat: null, handScale: 1 });
  const place = profile.present(input.slot, input.place);
  assert.equal(place.scale, 2.4);
  assert.equal(place.rotation, input.place.rotation);
  vectorNear(place.size, input.place.size.clone().multiplyScalar(2));
  const wall = slots.get('wall.0.0@0').places[0];
  const wallBefore = JSON.stringify(wall);
  const shownWall = profile.present(slots.get('wall.0.0@0'), wall);
  assert.equal(shownWall.rotation, wall.rotation);
  assert.equal(shownWall.scale, 2.2, 'actual wall bodies remain part of the 3D table');
  assert.equal(JSON.stringify(wall), wallBefore);
});

test('sideways added kongs contact the actual middle body in every source-seat rotation', () => {
  for (let seat = 0; seat < 4; seat++) {
    const slots = slotMap();
    const sources = [source(slots, `meld.0.0@${seat}`), source(slots, `meld.0.1@${seat}`, 1),
      source(slots, `meld.0.2@${seat}`), source(slots, `meld.0.4@${seat}`, 1)];
    const profile = new CompactTableProfile(sources, { ownSeat: null, handScale: 1 });
    const base = boundsFor(profile.present(sources[1].slot, sources[1].place));
    const upper = boundsFor(profile.present(sources[3].slot, sources[3].place));
    assert(Math.abs(base.max.z - upper.min.z) < EPS);
    vectorNear(base.getCenter(new THREE.Vector3()).setZ(0), upper.getCenter(new THREE.Vector3()).setZ(0));
    for (let i = 0; i < 3; i++) assert(!penetrates(upper, boundsFor(profile.present(sources[i].slot, sources[i].place))));
  }
});

function harness({ width = 412, height = 915, perspective = true, seat = 0, domHand = true } = {}) {
  const setup = new Setup();
  setup.setup({ ...types.Conditions.initial(), dealType: 'INITIAL', dealMode: 'auto' });
  for (const thing of setup.things.values()) {
    thing.prepareMove();
    thing.slot = setup.hiddenParkSlot;
    thing.rotationIndex = 0;
    thing.hidden = true;
    thing.sent = true;
  }
  const entries = new Map();
  const client = {
    seat, online: true, connectionMode: 'changsha', lastGameId: 'room-a', serverSnapshotGameId: 'room-a',
    connected() { return this.online; },
    things: { entries: () => entries.entries(), get: id => entries.get(id) ?? null },
    claim: { get: () => null }, phase: 'AwaitingDiscard',
    turn: { get: () => ({ phase: client.phase }) },
  };
  const objectView = new ObjectView(new THREE.Group(), assets, client);
  const world = Object.create(World.prototype);
  Object.assign(world, {
    setup, slots: setup.slots, things: setup.things, conditions: setup.conditions, pushes: setup.pushes,
    client, objectView, authoritySeat: seat, viewpointAuthoritySeat: seat, viewSeat: seat, handSnapshotSeat: seat,
    handMode: 'groups', handScale: 1, handPointerDown: false, handPlaces: new Map(),
    presentedSeat: null, presentedHand: [], handSignature: '', handDirty: true, drawnTile: null,
    selected: [], hovered: null, highlightedThing: null, movement: null, ownHandDomSeat: null,
    compactTableActive: false, compactTable: null, compactSignature: '', compactCapacityWidth: COMPACT_CAPACITY_WIDTH,
  });
  let nextId = 0;
  const put = (name, rotation = 0, back = false) => {
    const slot = world.slots.get(name);
    assert(slot, name);
    assert.equal(slot.thing, null, `duplicate fixture slot ${name}`);
    const id = nextId++;
    assert(id < 108, 'fixture must use the real 108-tile inventory');
    const thing = world.things.get(id);
    thing.moveTo(slot, rotation);
    thing.hidden = false;
    thing.sent = true;
    if (back) {
      thing.hiddenHandle = `opaque-${id}`;
      thing.typeIndex = 0;
    }
    entries.set(back ? `opaque-${id}` : id, {
      slotName: name, rotationIndex: rotation, face: back ? null : thing.typeIndex,
    });
    world.handDirty = true;
    return thing;
  };
  const view = Object.create(MainView.prototype);
  const camera = perspective ? new THREE.PerspectiveCamera(30, width / height, .1, 1000)
    : new THREE.OrthographicCamera(-1, 1, 1, -1, .1, 1000);
  const rig = new THREE.Group();
  rig.position.set(87, 87, 0);
  rig.add(camera);
  const fitCorners = [];
  for (const x of [-4.5, 178.5]) for (const y of [-4.5, 178.5]) for (const z of [0, 9]) {
    fitCorners.push(new THREE.Vector3(x, y, z));
  }
  Object.assign(view, {
    camera, perspective, viewGroup: rig, width, height, fitCorners, compactTable: null,
    main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) },
    mobileLayoutMode: null, resolvedMobileLayout: null, mobileHandActive: false, mobileFooterInset: 0,
    playArea: () => ({ left: 8, right: view.width - 8, top: 114 + (view.compactTable ? 24 : 0),
      bottom: view.height - 8 - (world.ownHandDomSeat !== null ? 184 : 0) }),
    updateViewport: noop, updateOutline: noop, updateHighlight: noop, render: noop,
  });
  const mouse = Object.create(MouseUi.prototype);
  Object.assign(mouse, {
    world, mouse2: null, raycastObjects: Array.from({ length: world.slots.size },
      () => new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1))),
    setCamera: noop, update: noop, updateCursors: noop,
  });
  const game = Object.create(Game.prototype);
  const parent = document.createElement('div');
  const status = new CompactTableStatus(parent);
  Object.assign(game, {
    world, client, mainView: view, objectView, mouseUi: mouse, compactStatus: status,
    lookDown: { pos: 0, update: noop }, zoom: { pos: 0, update: noop },
    gameUi: { refreshMobileHand: () => ({ active: domHand && view.width <= 600 && view.height > view.width, insetPx: 184 }),
      publicBoardActive: false },
  });
  const frame = (search = '?tableLayout=compact&mobileLayout=hybrid') => {
    Object.assign(viewport, { innerWidth: view.width, innerHeight: view.height });
    viewport.location.search = search;
    game.update();
  };
  world.setupView();
  const snapshot = () => JSON.stringify({
    slots: [...world.slots.values()].map(slot => ({
      name: slot.name, indexes: slot.indexes, origin: slot.origin, direction: slot.direction,
      rotations: slot.rotations, places: slot.places, offset: slot.offset, occupant: slot.thing?.index,
    })),
    things: [...world.things.values()].map(thing => ({
      id: thing.index, face: thing.typeIndex, slot: thing.slot.name, rotation: thing.rotationIndex,
      hidden: thing.hidden, handle: thing.hiddenHandle, sent: thing.sent,
    })), entries: [...entries],
  });
  return { world, objectView, view, mouse, client, game, put, frame, snapshot, labels: parent.children[0] };
}

function renderedMatrix(group, id) {
  const mesh = group.meshes[id - group.startIndex];
  if (mesh.visible) return mesh.matrixWorld.clone();
  const matrix = new THREE.Matrix4();
  group.instancedMesh.getMatrixAt(id - group.startIndex, matrix);
  return matrix.premultiply(group.instancedMesh.matrixWorld);
}

function faceRect(place, view) {
  return faceRectMatrix(matrixFor(place), view);
}

function faceRectMatrix(matrix, view) {
  const points = faceVertices.map(p => p.clone().applyMatrix4(matrix).project(view.camera));
  const left = Math.min(...points.map(p => (p.x + 1) * view.width / 2));
  const right = Math.max(...points.map(p => (p.x + 1) * view.width / 2));
  const top = Math.min(...points.map(p => (1 - p.y) * view.height / 2));
  const bottom = Math.max(...points.map(p => (1 - p.y) * view.height / 2));
  return { left, right, top, bottom, width: right - left, height: bottom - top };
}

test('compact status follows the active locale without changing counts, arrows or framing space', () => {
  const parent = document.createElement('div');
  const status = new CompactTableStatus(parent), root = parent.children[0];
  const counts = {
    wall: 21,
    seats: [0, 1, 2, 3].map(seat => ({ seat, hand: 8 + seat, discards: 9, melds: 2 })),
  };
  const area = { left: 8, right: 404, top: 138, bottom: 724 };
  const expected = [
    ['en', 'S0↙ Hand8', 'Wall 21', 'Seat 0, lower left: Hand 8, Discards 9, Melds 2', 'upper right'],
    ['zh-Hans', '座0↙ 手牌8', '牌墙 21', '座位 0, 左下：手牌 8，弃牌 9，副露 2', '右上'],
    ['zh-Hant', '座0↙ 手牌8', '牌牆 21', '座位 0, 左下：手牌 8，棄牌 9，副露 2', '右上'],
    ['en', 'S0↙ Hand8', 'Wall 21', 'Seat 0, lower left: Hand 8, Discards 9, Melds 2', 'upper right'],
  ];
  let geometry, metadata;
  try {
    for (const [locale, seatText, wallText, description, upperRight] of expected) {
      statusLocale = locale;
      status.update(true, counts, 0, area);
      assert.equal(root.children[0].textContent, seatText);
      assert.equal(root.children[4].textContent, wallText);
      const aria = root.children[0]['aria-label'];
      assert.equal(aria.replaceAll('：', ': ').replaceAll('，', ', '), description.replaceAll('：', ': ').replaceAll('，', ', '));
      assert(root.children[2]['aria-label'].includes(upperRight));
      assert.equal(root.style.height, '22px');
      assert.equal(root.style.whiteSpace, 'nowrap');
      geometry ??= JSON.stringify(root.style);
      metadata ??= JSON.stringify(root.children.map(child => child.dataset));
      assert.equal(JSON.stringify(root.style), geometry);
      assert.equal(JSON.stringify(root.children.map(child => child.dataset)), metadata);
      assert.equal(bodyAttributes.get('data-table-layout'), 'compact');
    }
    status.update(true, counts, 2, area);
    assert.equal(root.children[0].textContent, 'S0↗ Hand8');
    assert(root.children[0]['aria-label'].includes('upper right'));
    status.update(false, null, null, null);
    statusLocale = 'zh-Hant';
    status.update(false, null, null, null);
    assert.equal(root.style.display, 'none');
    assert.equal(bodyAttributes.has('data-table-layout'), false);
    status.update(true, counts, 0, area);
    assert.equal(root.children[4].textContent, '牌牆 21');
  } finally {
    statusLocale = 'en';
  }
});

for (const perspective of [true, false]) {
  for (const seat of [0, 1, 2, 3]) {
    test(`actual render/pick/shadows use the same public profile, perspective=${perspective}, seat=${seat}`, () => {
      const h = harness({ perspective, seat });
      const own = h.put(`hand.0@${seat}`, 1);
      const foreign = h.put(`hand.0@${(seat + 1) % 4}`, 2, true);
      const wall = h.put('wall.0.0@0', 0, true);
      const publicTiles = [];
      for (const other of [0, 1, 2, 3]) {
        publicTiles.push(h.put(`discard.0.0@${other}`));
        for (let i = 0; i < 4; i++) publicTiles.push(h.put(`meld.0.${i}@${other}`, 2, other !== seat));
      }
      const before = h.snapshot();
      h.frame();
      assert.equal(bodyAttributes.get('data-table-layout'), 'compact');
      assert.equal(h.snapshot(), before, 'presentation cannot rewrite authoritative metadata');
      const group = h.objectView.thingGroups.get('TILE'), picks = h.world.toSelect();
      assert(!picks.some(pick => pick.id === own.index), 'only the dock-owned hand is hidden');
      for (const thing of [foreign, wall]) assert(picks.some(pick => pick.id === thing.index));
      for (const thing of publicTiles) {
        const pick = picks.find(p => p.id === thing.index);
        const rendered = original.geometry.boundingBox.clone().applyMatrix4(renderedMatrix(group, thing.index));
        const select = new THREE.Box3().setFromCenterAndSize(pick.position, pick.size);
        vectorNear(rendered.min, select.min);
        vectorNear(rendered.max, select.max);
        assert.equal(pick.rotation, thing.place().rotation);
        const ray = new THREE.Raycaster();
        const center = pick.position.clone().project(h.view.camera);
        ray.setFromCamera(new THREE.Vector2(center.x, center.y), h.view.camera);
        assert.equal(ray.intersectObjects(h.mouse.currentObjects)[0]?.object.userData.id, thing.index);
      }
      assert.equal(renderedMatrix(group, own.index).determinant(), 0);
      for (const thing of [foreign, wall]) assert(renderedMatrix(group, thing.index).determinant() > 0);
      const wallShadows = [...h.world.slots.values()].filter(slot => slot.group === 'wall' && slot.drawShadow).length;
      assert.equal(h.objectView.shadowObject.count, wallShadows + 72 + 1,
        'wall tracks, four full discard grids, and the actual foreign hand remain visible');
      const label = h.labels.children.find(element => element.dataset.compactSeat === String((seat + 1) % 4));
      assert.equal(label.dataset.compactHandCount, '1');
      assert.equal(label.dataset.compactDiscardCount, '1');
      assert.equal(label.dataset.compactMeldCount, '1');
      assert.equal(h.labels.children.at(-1).dataset.compactWallCount, '1');
      for (const label of h.labels.children.filter(element => element.dataset.compactSeat !== undefined)) {
        assert.equal(label.dataset.compactRelative, String((Number(label.dataset.compactSeat) - seat + 4) % 4));
      }
      h.world.hovered = publicTiles[0];
      h.world.selected = [publicTiles[0]];
      h.frame();
      const custom = group.meshes[publicTiles[0].index];
      assert.equal(custom.visible, true);
      assert.equal(custom.scale.x, 2);
      const customPick = h.world.toSelect().find(p => p.id === publicTiles[0].index);
      vectorNear(custom.position, customPick.position);
      h.world.hovered = null;
      h.world.selected = [];
      h.frame();
      assert.equal(group.meshes[publicTiles[0].index].visible, false);
      assert.equal(h.snapshot(), before);
    });
  }
}

test('flag-off, ceremony, relay, 2D and disconnect restore the exact default scene and active DOM marker', () => {
  const h = harness();
  h.put('hand.0@0', 1);
  h.put('hand.0@1', 2, true);
  h.put('wall.0.0@0', 0, true);
  h.put('discard.0.0@0');
  const table = h.objectView.tableMesh;
  h.frame('?mobileLayout=hybrid');
  const before = {
    table: table.matrixWorld.toArray(), camera: h.view.camera.projectionMatrix.toArray(),
    shadow: Array.from(h.objectView.shadowObject.instanceMatrix.array),
  };
  const sourceBefore = h.snapshot();
  for (const disable of ['flag', 'manual', 'relay', 'world-type', '2D', 'disconnect', 'unknown', 'stale-room']) {
    h.client.phase = 'AwaitingDiscard';
    h.client.connectionMode = 'changsha';
    h.world.conditions.gameType = 'CHANGSHA';
    h.client.online = true;
    h.client.serverSnapshotGameId = 'room-a';
    h.game.gameUi.publicBoardActive = false;
    h.frame();
    assert(h.world.tableLayoutProfile);
    assert.notDeepEqual(table.matrixWorld.toArray(), before.table);
    if (disable === 'manual') h.client.phase = 'PickupRound3';
    if (disable === 'relay') h.client.connectionMode = 'relay';
    if (disable === 'world-type') h.world.conditions.gameType = 'FOUR_PLAYER';
    if (disable === '2D') h.game.gameUi.publicBoardActive = true;
    if (disable === 'disconnect') h.client.online = false;
    if (disable === 'unknown') h.client.phase = 'unknown';
    if (disable === 'stale-room') h.client.serverSnapshotGameId = 'old-room';
    h.frame(disable === 'flag' ? '?mobileLayout=hybrid' : undefined);
    assert.equal(h.world.tableLayoutProfile, null, disable);
    assert.equal(bodyAttributes.has('data-table-layout'), false, disable);
    assert.deepEqual(table.matrixWorld.toArray(), before.table, disable);
    assert.equal(h.snapshot(), sourceBefore, disable);
  }
  h.client.online = true;
  h.client.serverSnapshotGameId = 'room-a';
  h.client.phase = 'AwaitingDiscard';
  h.frame('?mobileLayout=hybrid');
  assert.deepEqual(h.view.camera.projectionMatrix.toArray(), before.camera);
  assert.deepEqual(Array.from(h.objectView.shadowObject.instanceMatrix.array), before.shadow);
});

test('real anonymous back pool and owner concealed kongs retain privacy rotations, flags and materials', () => {
  const h = harness();
  h.world.hiddenBackPool = null;
  h.world.handleToBack = new Map();
  h.world.ensureHiddenBackPool();
  for (let i = 0; i < 4; i++) h.put(`meld.0.${i}@0`, 2);
  const backs = h.world.hiddenBackPool.slice(0, 6);
  backs.forEach((back, index) => {
    const name = index < 4 ? `meld.0.${index}@1` : index === 4 ? 'hand.0@2' : 'wall.0.0@0';
    back.moveTo(h.world.slots.get(name), index === 5 ? 0 : 2);
    back.hiddenHandle = `opaque-pool-${index}`;
    back.hidden = false;
    back.sent = true;
  });
  const before = h.snapshot();
  const group = h.objectView.thingGroups.get('TILE');
  const materials = [...Array(4).keys(), ...backs.map(back => back.index)].map(id => {
    const material = group.meshes[id].material;
    return { id, transparent: material.transparent, opacity: material.opacity, depthTest: material.depthTest };
  });
  h.frame();
  assert.equal(h.snapshot(), before);
  for (const id of [...Array(4).keys(), ...backs.slice(0, 4).map(back => back.index)]) {
    const thing = h.world.things.get(id), pick = h.world.toSelect().find(p => p.id === id);
    assert.equal(pick.rotation, thing.place().rotation, 'layout never flips a concealed kong to reveal its face');
    const normal = new THREE.Vector3(0, 0, 1).applyQuaternion(pick.rotation);
    assert(normal.z < -.999999, 'private fronts remain facing down');
  }
  for (const beforeMaterial of materials) {
    const material = group.meshes[beforeMaterial.id].material;
    assert.equal(material.transparent, beforeMaterial.transparent);
    assert.equal(material.opacity, beforeMaterial.opacity);
    assert.equal(material.depthTest, beforeMaterial.depthTest);
  }
  for (const back of backs.slice(4)) assert(renderedMatrix(group, back.index).determinant() > 0);
  assert.equal(h.world.compactTableCounts().wall, 1);
  assert.equal(h.world.compactTableCounts().seats[2].hand, 1);
});

for (const perspective of [true, false]) {
  test(`a fresh dealt table keeps every real wall/back and four empty discard grids, perspective=${perspective}`, () => {
    const h = harness({ perspective });
    for (let seat = 0; seat < 4; seat++) for (let tile = 0; tile < (seat === 0 ? 14 : 13); tile++) {
      h.put(`hand.${tile}@${seat}`, seat === 0 ? 1 : 2, seat !== 0);
    }
    const walls = [...h.world.slots.values()].filter(slot => slot.group === 'wall').slice(0, 55)
      .map(slot => h.put(slot.name, 0, true));
    const before = h.snapshot();
    h.frame();
    const group = h.objectView.thingGroups.get('TILE');
    const backs = [...h.world.things.values()].filter(thing => !thing.hidden && thing.hiddenHandle !== null);
    assert.equal(walls.length, 55);
    assert.equal(backs.length, 94);
    for (const thing of backs) {
      assert(renderedMatrix(group, thing.index).determinant() > EPS, `${thing.slot.name} was removed from the table`);
      const pick = h.world.toSelect().find(place => place.id === thing.index);
      assert(pick, `${thing.slot.name} is missing its real 3D body`);
      assert.equal(pick.rotation, thing.place().rotation);
      const body = original.geometry.boundingBox.clone().applyMatrix4(renderedMatrix(group, thing.index));
      const target = new THREE.Box3().setFromCenterAndSize(pick.position, pick.size);
      vectorNear(body.min, target.min);
      vectorNear(body.max, target.max);
    }
    const shadows = Array.from({ length: h.objectView.shadowObject.count }, (_, index) => {
      const matrix = new THREE.Matrix4();
      h.objectView.shadowObject.getMatrixAt(index, matrix);
      return { position: new THREE.Vector3().setFromMatrixPosition(matrix),
        size: new THREE.Vector3().setFromMatrixScale(matrix) };
    });
    const rivers = [...h.world.slots.values()].filter(slot => slot.group === 'discard' && slot.drawShadow);
    assert.equal(rivers.length, 72);
    const positions = new Set();
    for (const slot of rivers) {
      const place = h.world.tableLayoutProfile.present(slot, slot.places[slot.shadowRotation]);
      positions.add(`${place.position.x.toFixed(4)},${place.position.y.toFixed(4)}`);
      assert(shadows.some(shadow => Math.abs(shadow.position.x - place.position.x) < EPS
        && Math.abs(shadow.position.y - place.position.y) < EPS
        && Math.abs(shadow.size.x - place.size.x) < EPS && Math.abs(shadow.size.y - place.size.y) < EPS),
      `${slot.name} has no visible empty discard position`);
    }
    assert.equal(positions.size, 72, 'the four river grids must not be stacked on top of one another');
    assert(h.world.compactTableCounts().seats.every(seat => seat.discards === 0));
    assert.equal(h.snapshot(), before, 'restoring the table must not invent discarded tiles or wall bodies');
    const projection = h.view.camera.projectionMatrix.toArray();
    const shadowCount = h.objectView.shadowObject.count;
    const drawn = walls[0], previousSlot = drawn.slot;
    drawn.prepareMove();
    drawn.moveTo(h.world.slots.get('hand.13@1'));
    h.client.things.get(drawn.hiddenHandle).slotName = drawn.slot.name;
    h.frame();
    assert.equal(h.objectView.shadowObject.count, shadowCount + 1, 'a real concealed draw updates the visible hand shadow');
    assert.deepEqual(h.view.camera.projectionMatrix.toArray(), projection, 'concealed draws cannot refit the camera');
    drawn.prepareMove();
    drawn.moveTo(previousSlot);
    h.client.things.get(drawn.hiddenHandle).slotName = previousSlot.name;
    h.frame();
    assert.equal(h.objectView.shadowObject.count, shadowCount);
    assert.deepEqual(h.view.camera.projectionMatrix.toArray(), projection);
  });
}

test('normal discards, hover, sorting, a flat quad per row and added kongs never change the authored fit', () => {
  const h = harness({ domHand: false });
  for (let i = 0; i < 14; i++) h.put(`hand.${i}@0`, 1);
  const first = h.put('discard.0.0@1');
  h.frame();
  const corners = JSON.stringify(h.view.presentedFitCorners);
  const projection = h.view.camera.projectionMatrix.toArray();
  const fixed = () => {
    h.frame();
    assert.equal(JSON.stringify(h.view.presentedFitCorners), corners);
    assert.deepEqual(h.view.camera.projectionMatrix.toArray(), projection);
  };
  h.world.hovered = first;
  fixed();
  h.world.selected = [first];
  fixed();
  h.world.setHandSortMode('suit');
  fixed();
  h.put('discard.0.1@1');
  fixed();
  for (let group = 0; group < 4; group++) {
    for (let i = 0; i < (group % 2 === 0 ? 4 : 3); i++) h.put(`meld.${group}.${i}@2`);
    fixed();
  }
  for (let i = 0; i < 3; i++) h.put(`meld.0.${i}@3`);
  h.put('meld.0.4@3');
  fixed();
  const moved = h.world.things.get(0);
  moved.prepareMove();
  moved.moveTo(h.world.slots.get('discard.0.2@0'));
  h.world.handDirty = true;
  fixed();
});

test('rare sideways-quad capacity expansion is latched and waits for pointer release', () => {
  const h = harness();
  h.frame();
  const capacity = h.world.tableLayoutProfile.capacityWidth;
  h.world.setHandPointerDown(true);
  for (let group = 0; group < 4; group++) for (let tile = 0; tile < 4; tile++) {
    h.put(`meld.${group}.${tile}@2`, tile === 1 ? 1 : 0);
  }
  h.frame();
  assert.equal(h.world.tableLayoutProfile.capacityWidth, capacity);
  assert(h.world.tableLayoutProfile.requiredWidth > capacity);
  h.world.setHandPointerDown(false);
  h.frame();
  const expanded = h.world.tableLayoutProfile.capacityWidth;
  assert(expanded > capacity);
  for (const thing of h.world.things.values()) if (thing.slot.group === 'meld') thing.rotationIndex = 0;
  h.frame();
  assert.equal(h.world.tableLayoutProfile.capacityWidth, expanded, 'ordinary updates cannot shrink/refit it');
  h.frame('');
  h.frame();
  assert.equal(h.world.tableLayoutProfile.capacityWidth, h.world.tableLayoutProfile.requiredWidth);
  assert(h.world.tableLayoutProfile.capacityWidth < expanded, 'a fresh post-deal activation resets the capacity latch');
});

test('public drop shadows use the identical rotated, scaled footprint and stacked height', () => {
  const h = harness();
  const base = h.put('meld.0.0@0');
  h.put('meld.0.1@0', 1);
  h.put('meld.0.2@0');
  const upper = h.put('meld.0.4@0', 1);
  h.frame();
  for (const thing of [base, upper]) {
    h.world.movement = { valid: () => true, slots: () => [thing.slot] };
    h.world.updateViewDropShadows();
    const shadow = h.objectView.dropShadowObjects[0];
    const place = h.world.presentedPlace(thing);
    assert.equal(shadow.position.x, place.position.x);
    assert.equal(shadow.position.y, place.position.y);
    assert.equal(shadow.position.z, place.position.z - place.size.z / 2 + .2);
    assert.equal(shadow.scale.x, place.size.x);
    assert.equal(shadow.scale.y, place.size.y);
  }
});

for (const perspective of [true, false]) {
  for (const seat of [0, 1, 2, 3]) {
    test(`resize/full 3D own hand/user controls, perspective=${perspective}, seat=${seat}`, () => {
    const h = harness({ domHand: false, perspective, seat });
    for (let i = 0; i < 14; i++) h.put(`hand.${i}@${seat}`, 1);
    h.put(`discard.0.0@${seat}`);
    for (const [width, height] of [[412, 915], [1280, 900], [915, 412], [500, 1000]]) {
      Object.assign(h.view, { width, height });
      h.frame('?mobileLayout=hybrid');
      const previous = h.world.toSelect().filter(p => h.world.things.get(p.id).slot.group === 'hand')
        .map(p => faceRect(p, h.view));
      const previousWidth = Math.min(...previous.map(rect => rect.width));
      const previousHeight = Math.min(...previous.map(rect => rect.height));
      h.frame();
      const area = h.view.playArea();
      for (const corner of h.view.presentedFitCorners) {
        const p = corner.clone().project(h.view.camera);
        assert(p.x >= area.left * 2 / width - 1 - EPS && p.x <= area.right * 2 / width - 1 + EPS);
        assert(p.y <= 1 - area.top * 2 / height + EPS && p.y >= 1 - area.bottom * 2 / height - EPS);
        assert(p.z >= -1 && p.z <= 1);
      }
      const hand = h.world.toSelect().filter(p => h.world.things.get(p.id).slot.group === 'hand');
      assert.equal(hand.length, 14);
      for (const place of hand) {
        const baseScale = width <= 900 || height <= 520 ? 1.7 : 1;
        const handBoost = width > height ? (height <= 520 ? 2 : 1.9) : 1.45;
        assert.equal(place.scale ?? 1, baseScale * handBoost);
        assert(!penetrates(boundsFor(place), boundsFor(h.world.presentedPlace(h.world.things.get(14)))));
        const rect = faceRect(place, h.view);
        assert(rect.width >= 18 && rect.height >= 18, `own hand too small at ${width}x${height}: ${JSON.stringify(rect)}`);
        assert(rect.width >= previousWidth * .95 && rect.height >= previousHeight * .95,
          `own hand regressed at ${width}x${height}: ${JSON.stringify({ rect, previousWidth, previousHeight })}`);
        const ray = new THREE.Raycaster(), center = place.position.clone().project(h.view.camera);
        ray.setFromCamera(new THREE.Vector2(center.x, center.y), h.view.camera);
        assert.equal(ray.intersectObjects(h.mouse.currentObjects)[0]?.object.userData.id, place.id);
      }
      const before = h.view.camera.projectionMatrix.toArray(), pose = h.view.camera.position.clone();
      h.game.lookDown.pos = 1;
      h.frame();
      assert.deepEqual(h.view.camera.projectionMatrix.toArray(), before);
      h.game.lookDown.pos = 0;
      h.game.zoom.pos = 1;
      h.frame();
      assert(h.view.camera.position.distanceTo(pose) > 1 || h.view.camera.scale.x !== 1);
      assert.deepEqual(h.view.camera.projectionMatrix.toArray(), before);
      h.game.zoom.pos = 0;
    }
  });
  }
}

for (const [width, height] of [[1280, 900], [412, 924], [500, 954], [915, 412]]) {
for (const perspective of [true, false]) {
  for (const quads of [0, 2, 4]) {
    test(`actual own-hand label floor at ${width}x${height}, quads=${quads}, perspective=${perspective}`, t => {
      const h = harness({ width, height, perspective, domHand: false });
      const hand = Array.from({ length: 14 }, (_, i) => h.put(`hand.${i}@0`, 1));
      h.put('discard.0.0@0');
      if (quads > 0) {
        for (let group = 0; group < quads; group++) for (let tile = 0; tile < 4; tile++) {
          h.put(`meld.${group}.${tile}@2`, tile === 1 ? 1 : 0);
        }
      }
      const before = h.snapshot();
      const group = h.objectView.thingGroups.get('TILE');
      const faces = () => hand.map(thing => faceRectMatrix(renderedMatrix(group, thing.index), h.view));
      const minimum = rects => ({
        width: Math.min(...rects.map(rect => rect.width)),
        height: Math.min(...rects.map(rect => rect.height)),
      });
      const scene = () => JSON.stringify({
        handScale: h.world.handScale, camera: h.view.camera.matrixWorld.toArray(),
        projection: h.view.camera.projectionMatrix.toArray(), table: h.objectView.tableMesh.matrixWorld.toArray(),
        shadowCount: h.objectView.shadowObject.count, shadows: Array.from(h.objectView.shadowObject.instanceMatrix.array),
        tiles: [...h.world.things.values()].filter(thing => !thing.hidden)
          .map(thing => renderedMatrix(group, thing.index).toArray()),
      });
      h.frame('?mobileLayout=hybrid');
      const baseline = minimum(faces());
      const defaultScene = scene();
      h.frame();
      const compact = minimum(faces());
      const widthRatio = compact.width / baseline.width, heightRatio = compact.height / baseline.height;
      t.diagnostic(`actual GLB own-hand minimum face ratios: ${widthRatio.toFixed(4)} width / ${heightRatio.toFixed(4)} height`);
      assert(widthRatio >= .95 && heightRatio >= .95,
        `own-hand face floor: ${JSON.stringify({ baseline, compact, widthRatio, heightRatio })}`);
      const area = h.view.playArea();
      for (const rect of faces()) {
        assert(rect.left >= area.left - EPS && rect.right <= area.right + EPS);
        assert(rect.top >= area.top - EPS && rect.bottom <= area.bottom + EPS);
      }
      assert.equal(h.snapshot(), before);
      h.frame('?mobileLayout=hybrid');
      assert.equal(h.world.tableLayoutProfile, null);
      assert.equal(scene(), defaultScene, 'flag-off restores every submitted tile, shadow, table, camera and hand scale');
    });
  }
}
}

for (const perspective of [true, false]) {
  test(`expanded own-hand compensation latches with capacity after pointer/claim release, perspective=${perspective}`, () => {
    const h = harness({ width: 1280, height: 900, perspective, domHand: false });
    const hand = Array.from({ length: 14 }, (_, i) => h.put(`hand.${i}@0`, 1));
    h.put('discard.0.0@0');
    h.frame();
    const group = h.objectView.thingGroups.get('TILE');
    const framing = () => JSON.stringify({
      corners: h.view.presentedFitCorners, projection: h.view.camera.projectionMatrix.toArray(),
      camera: h.view.camera.matrixWorld.toArray(),
    });
    const handFrame = () => JSON.stringify({
      framing: framing(), scale: h.world.handScale,
      tiles: hand.map(thing => renderedMatrix(group, thing.index).toArray()),
      shadows: Array.from(h.objectView.shadowObject.instanceMatrix.array),
    });
    const normal = handFrame(), normalScale = h.world.handScale;
    h.world.setHandPointerDown(true);
    const quads = [];
    for (let meld = 0; meld < 4; meld++) for (let tile = 0; tile < 4; tile++) {
      quads.push(h.put(`meld.${meld}.${tile}@2`, tile === 1 ? 1 : 0));
    }
    const before = h.snapshot();
    const deferred = () => {
      h.frame();
      assert.equal(h.world.tableLayoutProfile.capacityWidth, COMPACT_CAPACITY_WIDTH);
      assert(h.world.tableLayoutProfile.requiredWidth > COMPACT_CAPACITY_WIDTH);
      assert.equal(handFrame(), normal, 'a deferred capacity cannot change the hand, shadows or camera');
    };
    deferred();
    h.world.setHandPointerDown(false);
    h.client.claim.get = () => ({ available: ['Pung'], deadline: Date.now() + 1000 });
    deferred();
    h.client.claim.get = () => null;
    quads[0].claimedBy = 0;
    deferred();
    quads[0].claimedBy = null;
    h.frame();
    const profile = h.world.tableLayoutProfile;
    assert(Math.abs(profile.capacityWidth - 111.36) < EPS);
    assert.equal(h.world.handScale, normalScale * (profile.capacityWidth / COMPACT_CAPACITY_WIDTH),
      'the first released frame must already compensate the expanded reserved row');
    assert.equal(h.snapshot(), before);
    const expanded = handFrame(), expandedFraming = framing();
    h.frame();
    assert.equal(h.world.tableLayoutProfile, profile, 'the compensated profile must be cache-stable');
    assert.equal(handFrame(), expanded);
    const picks = h.world.toSelect(), bodies = [];
    const area = h.view.playArea();
    for (const place of picks) {
      const body = original.geometry.boundingBox.clone().applyMatrix4(renderedMatrix(group, place.id));
      const pick = new THREE.Box3().setFromCenterAndSize(place.position, place.size);
      vectorNear(body.min, pick.min);
      vectorNear(body.max, pick.max);
      bodies.push(body);
      for (const x of [body.min.x, body.max.x]) for (const y of [body.min.y, body.max.y]) {
        for (const z of [body.min.z, body.max.z]) {
          const p = new THREE.Vector3(x, y, z).project(h.view.camera);
          const px = (p.x + 1) * h.view.width / 2, py = (1 - p.y) * h.view.height / 2;
          assert(px >= area.left - EPS && px <= area.right + EPS);
          assert(py >= area.top - EPS && py <= area.bottom + EPS);
          assert(p.z >= -1 && p.z <= 1);
        }
      }
    }
    for (let i = 0; i < bodies.length; i++) for (let j = i + 1; j < bodies.length; j++) {
      assert(!penetrates(bodies[i], bodies[j]), `${picks[i].id} intersects ${picks[j].id}`);
    }
    const shadows = Array.from({ length: h.objectView.shadowObject.count }, (_, i) => {
      const matrix = new THREE.Matrix4();
      h.objectView.shadowObject.getMatrixAt(i, matrix);
      return matrix;
    });
    for (const thing of hand) {
      const place = picks.find(pick => pick.id === thing.index);
      assert(place, `own-hand tile ${thing.index} remains selectable`);
      const shadow = shadows.find(matrix => {
        const center = new THREE.Vector3().setFromMatrixPosition(matrix);
        return Math.abs(center.x - place.position.x) < EPS && Math.abs(center.y - place.position.y) < EPS;
      });
      assert(shadow, `own-hand tile ${thing.index} uses its presented shadow position`);
      vectorNear(new THREE.Vector3().setFromMatrixScale(shadow), new THREE.Vector3(place.size.x, place.size.y, 1));
      const ray = new THREE.Raycaster(), center = place.position.clone().project(h.view.camera);
      ray.setFromCamera(new THREE.Vector2(center.x, center.y), h.view.camera);
      assert.equal(ray.intersectObjects(h.mouse.currentObjects)[0]?.object.userData.id, thing.index);
    }
    quads.forEach(thing => { thing.rotationIndex = 0; });
    h.frame();
    assert.equal(h.world.tableLayoutProfile.capacityWidth, profile.capacityWidth);
    assert.equal(handFrame(), expanded, 'narrower melds cannot shrink the latched compensation or fit');
    const discarded = hand.at(-1);
    discarded.prepareMove();
    discarded.moveTo(h.world.slots.get('discard.0.1@0'));
    h.world.handDirty = true;
    h.frame();
    assert.equal(h.world.handScale, normalScale * (profile.capacityWidth / COMPACT_CAPACITY_WIDTH));
    assert.equal(framing(), expandedFraming, 'a shorter hand still reserves all fourteen compensated positions');
  });
}

for (const fixture of [
  { name: '52 public tiles', rivers: [9, 9, 8, 8], melds: [2, 2, 1, 1], hands: [8, 7, 10, 10], wall: 21 },
  { name: '52 rivers + eight melds', rivers: [13, 13, 13, 13], melds: [2, 2, 2, 2], hands: [8, 7, 7, 7], wall: 3 },
]) {
for (const [width, height] of [[412, 924], [500, 954], [1280, 900]]) {
  for (const perspective of [true, false]) {
    test(`CPU actual label vertices, ${fixture.name} at ${width}x${height}, perspective=${perspective}`, t => {
      const h = harness({ width, height, perspective });
      const publicTiles = [];
      for (let seat = 0; seat < 4; seat++) {
        for (let i = 0; i < fixture.rivers[seat]; i++) publicTiles.push(h.put(`discard.${Math.floor(i / 6)}.${i % 6}@${seat}`));
        for (let group = 0; group < fixture.melds[seat]; group++) for (let tile = 0; tile < 3; tile++) {
          publicTiles.push(h.put(`meld.${group}.${tile}@${seat}`));
        }
        for (let i = 0; i < fixture.hands[seat]; i++) h.put(`hand.${i}@${seat}`, seat === 0 ? 1 : 2, seat !== 0);
      }
      for (let i = 0; i < fixture.wall; i++) h.put(`wall.${Math.floor(i / 2)}.${i % 2}@0`, 0, true);
      assert.equal(h.world.compactTableCounts().wall, fixture.wall, 'legal 108-tile case, not four impossible maxima');
      h.frame('?mobileLayout=hybrid');
      const group = h.objectView.thingGroups.get('TILE');
      const baseline = publicTiles.map(thing => faceRectMatrix(renderedMatrix(group, thing.index), h.view));
      h.frame();
      const rows = publicTiles.map((thing, i) => {
        const rect = faceRectMatrix(renderedMatrix(group, thing.index), h.view);
        return { zone: thing.slot.group, seat: thing.slot.seat, rect, old: baseline[i],
          widthGain: rect.width / baseline[i].width, heightGain: rect.height / baseline[i].height };
      });
      const visible = [...h.world.things.values()].filter(thing => !thing.hidden && thing.slot.thing === thing
        && (thing.slot.group !== 'hand' || thing.slot.seat !== h.world.ownHandDomSeat));
      assert.equal(visible.length, 108 - (h.world.ownHandDomSeat === 0 ? fixture.hands[0] : 0));
      const bodies = visible.map(thing => {
        const matrix = renderedMatrix(group, thing.index);
        assert(matrix.determinant() > EPS, `${thing.slot.name} cannot be removed to enlarge the public tiles`);
        return { thing, bounds: original.geometry.boundingBox.clone().applyMatrix4(matrix) };
      });
      for (let i = 0; i < bodies.length; i++) for (let j = i + 1; j < bodies.length; j++) {
        assert(!penetrates(bodies[i].bounds, bodies[j].bounds),
          `${bodies[i].thing.slot.name} intersects ${bodies[j].thing.slot.name}`);
      }
      t.diagnostic('restored public-face minima: ' + JSON.stringify(Object.fromEntries(['discard', 'meld'].map(zone => {
        const zoneRows = rows.filter(row => row.zone === zone);
        return [zone, {
          width: Math.min(...zoneRows.map(row => row.rect.width)),
          height: Math.min(...zoneRows.map(row => row.rect.height)),
        }];
      }))));
      for (const zone of ['discard', 'meld']) {
        const zoneRows = rows.filter(row => row.zone === zone);
        const minimum = Math.min(...zoneRows.map(row => Math.min(row.rect.width, row.rect.height)));
        const median = key => zoneRows.map(row => row[key]).sort((a, b) => a - b)[Math.floor(zoneRows.length / 2)];
        const widthGain = Math.min(...zoneRows.map(row => row.rect.width)) / Math.min(...zoneRows.map(row => row.old.width));
        const heightGain = Math.min(...zoneRows.map(row => row.rect.height)) / Math.min(...zoneRows.map(row => row.old.height));
        assert(minimum >= (width === 412 ? 14 : width === 500 ? 17 : 25), `${zone}: ${minimum}`);
        assert(widthGain >= (perspective ? 1.1 : 1) && heightGain >= 1.3,
          `${zone}: full-table minimum face gains ${widthGain}, ${heightGain}`);
        t.diagnostic(`${zone}: min ${minimum.toFixed(2)} CSS px; minimum-metric gains `
          + `${widthGain.toFixed(2)}× / ${heightGain.toFixed(2)}×; median face gains `
          + `${median('widthGain').toFixed(2)}× width / ${median('heightGain').toFixed(2)}× height; browser gate still required`);
      }
      const union = rects => ({
        left: Math.min(...rects.map(r => r.left)), right: Math.max(...rects.map(r => r.right)),
        top: Math.min(...rects.map(r => r.top)), bottom: Math.max(...rects.map(r => r.bottom)),
      });
      const gap = (a, b) => Math.hypot(Math.max(0, a.left - b.right, b.left - a.right),
        Math.max(0, a.top - b.bottom, b.top - a.bottom));
      const gutterRatios = [];
      for (const seat of [0, 1, 2, 3]) {
        const seatRows = rows.filter(row => row.seat === seat);
        const normalized = field => {
          const river = union(seatRows.filter(row => row.zone === 'discard').map(row => row[field]));
          const meld = union(seatRows.filter(row => row.zone === 'meld').map(row => row[field]));
          return gap(river, meld) / Math.min(...seatRows.map(row => Math.min(row[field].width, row[field].height)));
        };
        const ratio = normalized('rect') / normalized('old');
        gutterRatios.push(ratio);
        assert(ratio < .7, `seat ${seat}: intervening gap retained ${(ratio * 100).toFixed(1)}% of baseline`);
      }
      t.diagnostic(`Maximum normalized per-seat river/meld gutter: ${(100 * Math.max(...gutterRatios)).toFixed(1)}% of baseline`);
    });

  }
}
}

test('an off/relay frame never asks the compact status label to inspect Changsha framing', () => {
  const h = harness();
  h.client.connectionMode = 'relay';
  h.world.conditions.gameType = 'FOUR_PLAYER';
  Object.defineProperty(h.view, 'tablePlayArea', {
    get() { throw new Error('Inactive compact status inspected the table framing'); },
  });
  h.frame();
  assert.equal(bodyAttributes.has('data-table-layout'), false);
});

test('all default and relay canonical slot hashes are identical before and after public presentation', () => {
  const hash = slots => crypto.createHash('sha256').update(JSON.stringify(slots.map(slot => ({
    name: slot.name, origin: slot.origin, direction: slot.direction, rotations: slot.rotations,
    indexes: slot.indexes, places: slot.places, links: slot.linkDesc, drawShadow: slot.drawShadow,
  })))).digest('hex');
  for (const variant of Object.values(types.GameType)) {
    const slots = makeSlots(variant), before = hash(slots);
    const sources = slots.filter(slot => ['discard', 'meld'].includes(slot.group))
      .map(slot => ({ slot, place: slot.places[0] }));
    const profile = new CompactTableProfile(sources, { ownSeat: 0, handScale: 1.7 });
    sources.forEach(s => profile.present(s.slot, s.place));
    assert.equal(hash(slots), before, variant);
  }
});

test('standalone instanced/custom cache receives the identical compact scale and restores default scale', () => {
  const slots = slotMap(), input = source(slots, 'discard.0.0@0');
  const profile = new CompactTableProfile([input], { ownSeat: null, handScale: 1 });
  const group = new TileThingGroup(assets, new THREE.Group());
  group.replace(0, [{ index: 0, type: 'TILE', typeIndex: 0 }]);
  const compact = profile.present(input.slot, input.place);
  for (const place of [compact, input.place, compact]) {
    group.setSimple(0, place.position, place.rotation, place.scale);
    vectorNear(new THREE.Vector3().setFromMatrixScale(renderedMatrix(group, 0)),
      new THREE.Vector3().setScalar(place.scale ?? 1));
    const custom = group.setCustom(0, place.position, place.rotation, place.scale);
    custom.updateMatrix();
    custom.updateMatrixWorld();
    vectorNear(custom.scale, new THREE.Vector3().setScalar(place.scale ?? 1));
    group.hide(0);
    assert.equal(renderedMatrix(group, 0).determinant(), 0);
  }
});
