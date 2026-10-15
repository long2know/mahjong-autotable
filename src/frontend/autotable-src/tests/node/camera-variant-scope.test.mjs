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
const THREE = require('three');
require.extensions['.ts'] = (module, filename) => module._compile(ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, filename);
const types = require(path.join(root, 'src/types.ts'));
const fit = require(path.join(root, 'src/table-fit.ts'));
const mobileLayout = require(path.join(root, 'src/mobile-layout.ts'));
const module = { exports: {} };
const chrome = new Map();
const bodyClasses = new Set();
const viewport = { innerWidth: 960, innerHeight: 540 };
runInNewContext(ts.transpileModule(fs.readFileSync(path.join(root, 'src/main-view.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, {
  module, exports: module.exports,
  require: id => ({
    three: THREE,
    './world': { World: { WIDTH: 174 } },
    './types': types,
    './table-fit': fit,
    './mobile-layout': mobileLayout,
    './render/custom-outline': { CustomOutline: class {} },
  })[id],
  document: {
    body: { classList: { contains: name => bodyClasses.has(name) } }, documentElement: {},
    getElementById: id => chrome.get(id),
    querySelector: selector => chrome.get(selector) ?? null, querySelectorAll: () => [],
  },
  window: viewport,
  getComputedStyle: () => ({ getPropertyValue: () => '0', visibility: 'visible' }),
});

function view(perspective) {
  const v = Object.create(module.exports.MainView.prototype);
  const camera = perspective ? new THREE.PerspectiveCamera(30, 960 / 540, .1, 1000)
    : new THREE.OrthographicCamera(-1, 1, 1, -1, .1, 1000);
  const group = new THREE.Group();
  group.position.set(87, 87, 0);
  group.add(camera);
  const corners = [];
  for (const x of [-4.5, 178.5]) for (const y of [-4.5, 178.5]) for (const z of [0, 9]) {
    corners.push(new THREE.Vector3(x, y, z));
  }
  Object.assign(v, {
    camera, perspective, viewGroup: group, width: 960, height: 540, fitCorners: corners,
    mobileLayoutMode: null, resolvedMobileLayout: null, mobileHandActive: false, mobileFooterInset: 0,
  });
  return v;
}

for (const variant of ['FOUR_PLAYER', 'THREE_PLAYER', 'BAMBOO', 'MINEFIELD']) {
  for (const perspective of [true, false]) {
    test(`${variant} ${perspective ? 'perspective' : 'flat'} retains legacy projection despite a Changsha HUD`, () => {
      const v = view(perspective);
      v.playArea = () => { throw new Error('Relay must never inspect or fit Changsha chrome'); };
      v.updateCameraProjection(960, 540);
      const reference = v.camera.projectionMatrix.clone();
      v.updateCamera(0, 0, 0, null, variant);
      assert.deepEqual(v.camera.projectionMatrix.elements, reference.elements);
      if (perspective) {
        assert.deepEqual(v.camera.position.toArray(), [0, -174 * 1.44, 174 * 1.05]);
      } else {
        assert.deepEqual(v.camera.position.toArray(), [0, -174, 174]);
      }

    });
  }
}

function mobileView(mode, {
    active = false, insetPx = 0, width = 390, height = 844,
    canvasWidth = width, canvasHeight = height, perspective = true,
  } = {}) {
    chrome.clear();
    const v = view(perspective);
    Object.assign(v, {
      width: canvasWidth, height: canvasHeight,
      main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) },
    });
    Object.assign(viewport, { innerWidth: width, innerHeight: height });
    v.setMobileLayout(mode, active, insetPx);
    return v;
  }

  function projectedCorners(v, corners = v.fitCorners) {
    return corners.map(corner => {
      const p = corner.clone().project(v.camera);
      return { x: (p.x + 1) * v.width / 2, y: (1 - p.y) * v.height / 2, z: p.z };
    });
  }

  function assertFitted(v, corners) {
    const area = v.playArea();
    for (const p of projectedCorners(v, corners)) {
      assert(p.x >= area.left - 1e-6 && p.x <= area.right + 1e-6, `x=${p.x}`);
      assert(p.y >= area.top - 1e-6 && p.y <= area.bottom + 1e-6, `y=${p.y}`);
      assert(p.z >= -1 && p.z <= 1, 'authored bounds remain within camera clip planes');
    }
  }

  for (const mode of ['camera', 'hybrid', 'readable']) {
    for (const seat of [0, 1, 2, 3]) {
      test(`${mode} portrait seat ${seat} has a coherent 60-degree baseline fitted at that elevation`, () => {
        const v = mobileView(mode);
        v.updateCamera(seat, 0, 0, null, 'CHANGSHA');
        const elevation = Math.PI / 2 - v.camera.rotation.x;
        assert(Math.abs(elevation - Math.PI / 3) < 1e-12);
        const legacyPitch = Math.PI * .3;
        const target = new THREE.Vector3(0, 174 * (-1.44 + 1.05 * Math.tan(legacyPitch)), 0);
        const distance = 174 * 1.05 / Math.cos(legacyPitch);
        assert(Math.abs(v.camera.position.distanceTo(target) - distance) < 1e-10);
        const direction = target.clone().sub(v.camera.position).normalize().transformDirection(v.viewGroup.matrixWorld);
        assert(v.camera.getWorldDirection(new THREE.Vector3()).dot(direction) > 1 - 1e-12);
        assertFitted(v, v.fitCorners.concat(fit.localHandFitCorners(seat, 1.7)));
        const reference = projectedCorners(v);
        v.camera.updateProjectionMatrix();
        assert.deepEqual(projectedCorners(v), reference, 'input projection rebuild keeps the candidate fit');
        for (let frame = 0; frame < 10; frame++) {
          v.updateCamera(seat, 0, 0, null, 'CHANGSHA');
          assert.deepEqual(projectedCorners(v), reference, 'fit does not accumulate between frames');
        }
      });
    }
  }

  test('the standalone camera candidate increases projected table depth, not merely camera pitch', context => {
    const legacy = mobileView(null);
    legacy.updateCamera(0, 0, 0, null, 'CHANGSHA');
    const candidate = mobileView('camera');
    candidate.updateCamera(0, 0, 0, null, 'CHANGSHA');
    const depth = v => {
      const ys = projectedCorners(v).map(p => p.y);
      return Math.max(...ys) - Math.min(...ys);
    };
    assert(depth(candidate) > depth(legacy) * 1.2, `candidate depth ${depth(candidate)} vs baseline ${depth(legacy)} CSS px`);
    assert(candidate.camera.position.z > legacy.camera.position.z);
    context.diagnostic(`390x844 CPU-projected table depth: baseline ${depth(legacy).toFixed(2)}px, camera ${depth(candidate).toFixed(2)}px; browser legibility remains unverified`);
  });

  for (const mode of ['camera', 'hybrid', 'readable']) {
    test(`${mode} is inert on desktop, tablet, landscape, square, invalid viewport and relay worlds`, () => {
      for (const [width, height, gameType] of [
        [1280, 900, 'CHANGSHA'], [820, 1180, 'CHANGSHA'], [601, 844, 'CHANGSHA'],
        [844, 390, 'CHANGSHA'], [390, 390, 'CHANGSHA'], [0, 844, 'CHANGSHA'],
        [390, Infinity, 'CHANGSHA'],
        ...['FOUR_PLAYER', 'THREE_PLAYER', 'BAMBOO', 'MINEFIELD'].map(variant => [390, 844, variant]),
      ]) {
        const options = { width, height, canvasWidth: 390, canvasHeight: 844 };
        const legacy = mobileView(null, options);
        legacy.updateCamera(0, 0, 0, null, gameType);
        const candidate = mobileView(mode, { ...options, active: true, insetPx: 180 });
        if (gameType !== 'CHANGSHA') candidate.playArea = () => { throw new Error('Relay must not inspect experimental chrome'); };
        candidate.updateCamera(0, 0, 0, null, gameType);
        assert.deepEqual(candidate.camera.position.toArray(), legacy.camera.position.toArray(), `${width}x${height} ${gameType}`);
        assert.deepEqual(candidate.camera.rotation.toArray(), legacy.camera.rotation.toArray());
        assert.deepEqual(candidate.camera.projectionMatrix.elements, legacy.camera.projectionMatrix.elements);
        assert.equal(candidate.mobileDomHandActive, false);
      }
    });
  }

  test('activation uses the full CSS viewport, never a footer-reduced canvas aspect', () => {
    const portrait = mobileView('hybrid', { active: true, insetPx: 180, canvasHeight: 320 });
    portrait.updateCamera(0, 0, 0, null, 'CHANGSHA');
    assert(Math.abs(portrait.camera.rotation.x - Math.PI / 6) < 1e-12);
    assert.equal(portrait.mobileDomHandActive, true);
    const landscape = mobileView('hybrid', {
      active: true, insetPx: 180, width: 844, height: 390, canvasWidth: 390, canvasHeight: 844,
    });
    landscape.updateCamera(0, 0, 0, null, 'CHANGSHA');
    assert.equal(landscape.camera.rotation.x, Math.PI * .3);
    assert.equal(landscape.mobileDomHandActive, false);
  });

  test('only an active hybrid DOM hand reserves its stable footer and excludes enlarged hand fit corners', () => {
    for (const [mode, active] of [[null, true], ['camera', true], ['hybrid', false], ['hybrid', true]]) {
      const v = mobileView(mode, { active, insetPx: 184 });
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      const domHand = mode === 'hybrid' && active;
      assert.equal(v.playArea().bottom, domHand ? 652 : 836);
      const corners = v.fitCorners.concat(domHand ? [] : fit.localHandFitCorners(0, 1.7));
      assertFitted(v, corners);
      const expected = v.camera.clone();
      const group = new THREE.Group();
      group.position.copy(v.viewGroup.position);
      group.rotation.copy(v.viewGroup.rotation);
      group.add(expected);
      group.updateMatrixWorld(true);
      fit.fitTableProjection(expected, corners, v.width, v.height, v.playArea());
      assert.deepEqual(v.camera.projectionMatrix.elements, expected.projectionMatrix.elements);
      const reference = projectedCorners(v);
      chrome.set('pickup-hud', { getClientRects: () => [1], getBoundingClientRect: () => ({ top: 400, bottom: 690 }) });
      chrome.set('turn-banner', { getClientRects: () => [1], getBoundingClientRect: () => ({ top: 60, bottom: 200 }) });
      chrome.set('.ferro-claim-overlay-visible', { getClientRects: () => [1], getBoundingClientRect: () => ({ top: 500, bottom: 844 }) });
      for (let frame = 0; frame < 8; frame++) {
        v.setMobileLayout(mode, active, 184);
        v.updateCamera(0, 0, 0, null, 'CHANGSHA');
        assert.deepEqual(projectedCorners(v), reference, 'selection/claim/pickup chrome cannot resize the fit');
      }
    }
    chrome.clear();
  });

  test('hybrid hand deactivation restores the complete fixed hand envelope and footer-free fit', () => {
    const v = mobileView('hybrid', { active: true, insetPx: 184 });
    v.updateCamera(0, 0, 0, null, 'CHANGSHA');
    const hybridProjection = v.camera.projectionMatrix.clone();
    v.setMobileLayout('hybrid', false, 184);
    v.updateCamera(0, 0, 0, null, 'CHANGSHA');
    assert.equal(v.playArea().bottom, 836);
    assertFitted(v, v.fitCorners.concat(fit.localHandFitCorners(0, 1.7)));
    assert.notDeepEqual(v.camera.projectionMatrix.elements, hybridProjection.elements);
    const cameraOnly = mobileView('camera');
    cameraOnly.updateCamera(0, 0, 0, null, 'CHANGSHA');
    assert.deepEqual(v.camera.projectionMatrix.elements, cameraOnly.camera.projectionMatrix.elements);
  });

  test('invalid footer measurements are finite and cannot poison the camera projection', () => {
    for (const insetPx of [-1, NaN, Infinity]) {
      const v = mobileView('hybrid', { active: true, insetPx });
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert.equal(v.playArea().bottom, 836);
      assert(v.camera.projectionMatrix.elements.every(Number.isFinite));
    }
  });

  for (const mode of ['camera', 'hybrid']) {
    test(`${mode} preserves flat-view geometry and the spectator overhead pose`, () => {
      const flat = mobileView(mode, { perspective: false });
      flat.updateCamera(0, 0, 0, null, 'CHANGSHA');
      const legacyFlat = mobileView(null, { perspective: false });
      legacyFlat.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert.deepEqual(flat.camera.position.toArray(), legacyFlat.camera.position.toArray());
      assert.deepEqual(flat.camera.rotation.toArray(), legacyFlat.camera.rotation.toArray());
      assert.deepEqual(flat.camera.projectionMatrix.elements, legacyFlat.camera.projectionMatrix.elements);
      const spectator = mobileView(mode);
      spectator.updateCamera(null, 0, 0, null, 'CHANGSHA');
      assert.deepEqual(spectator.camera.position.toArray(), [0, 0, 400]);
      assert.equal(spectator.camera.rotation.x, 0);
      assertFitted(spectator, spectator.fitCorners);
    });

    test(`${mode} preserves independent intentional look-down, zoom and bounded pan`, () => {
      const v = mobileView(mode, { active: mode !== 'camera', insetPx: 184 });
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      const before = projectedCorners(v);
      const position = v.camera.position.clone();
      const projection = v.camera.projectionMatrix.clone();
      v.updateCamera(0, 1, 0, null, 'CHANGSHA');
      assert(Math.abs(v.camera.rotation.x - (Math.PI / 6 - .2)) < 1e-12);
      assert.deepEqual(v.camera.position.toArray(), position.toArray(), 'look-down does not silently orbit/refit');
      assert.deepEqual(v.camera.projectionMatrix.elements, projection.elements);
      const deviation = () => Math.max(...projectedCorners(v).map((p, i) => Math.hypot(p.x - before[i].x, p.y - before[i].y)));
      assert(deviation() > 10);
      v.updateCamera(0, 0, 1, null, 'CHANGSHA');
      assert(Math.abs(v.camera.position.distanceTo(position) - 55 * Math.hypot(1.37, 1)) < 1e-10);
      const zoomed = v.camera.position.clone();
      v.updateCamera(0, 0, 1, new THREE.Vector2(.2, -.2), 'CHANGSHA');
      assert(Math.abs(v.camera.position.x - zoomed.x - .2 * 174 * .5) < 1e-12);
      assert(Math.abs(v.camera.position.y - zoomed.y + .2 * 174 * .5) < 1e-12);
      assert(deviation() > 10);
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert.deepEqual(projectedCorners(v), before);
      v.updateCamera(0, 0, 5, null, 'CHANGSHA');
      assert.deepEqual(v.camera.position.toArray(), zoomed.toArray(), 'Changsha zoom remains bounded');
    });
  }

test('readable mode reserves the same stable validated hand footer as hybrid while retaining scene geometry', () => {
  const readable = mobileView('readable', { active: true, insetPx: 208 });
  readable.updateCamera(0, 0, 0, null, 'CHANGSHA');
  const hybrid = mobileView('hybrid', { active: true, insetPx: 208 });
  hybrid.updateCamera(0, 0, 0, null, 'CHANGSHA');
  assert.equal(readable.playArea().bottom, 628);
  assert.deepEqual(readable.camera.projectionMatrix.elements, hybrid.camera.projectionMatrix.elements);
  assert.deepEqual(readable.camera.position.toArray(), hybrid.camera.position.toArray());
});

for (const [width, height] of [[390, 844], [844, 390]]) {
  test(`compact ${width}x${height} keeps transient turn/pickup chrome out of camera framing`, () => {
    chrome.clear();
    const v = view(true);
    Object.assign(v, { width, height, main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) } });
    chrome.set('lobby-toggle', {
      offsetTop: 62, offsetHeight: 44, offsetParent: null,
      getClientRects: () => [1], getBoundingClientRect: () => ({ top: 62, bottom: 106 }),
    });
    const area = v.playArea();
    assert.equal(area.top, 114);
    assert.equal(area.bottom, height - 8, 'no hand-only canvas inset');
    chrome.set('turn-banner', {
      getClientRects: () => [1], getBoundingClientRect: () => ({ top: 62, bottom: 170 }),
    });
    chrome.set('.ferro-claim-overlay-visible', {
      getClientRects: () => [1], getBoundingClientRect: () => ({ top: height - 210, bottom: height - 8 }),
    });
    chrome.set('pickup-hud', {
      getClientRects: () => [1], getBoundingClientRect: () => ({ top: height - 100 }),
    });
    assert.equal(v.playArea().bottom, height - 8, 'pickup text is not a viewport resize');
    assert.equal(v.playArea().top, 114, 'wrapping turn text is not a viewport resize');
    if (width > height) {
      chrome.set('pickup-hud', {
        getClientRects: () => [1], getBoundingClientRect: () => ({ top: 62, bottom: 110 }),
      });
      assert.equal(v.playArea().top, 114);
      assert.equal(v.playArea().bottom, height - 8, 'landscape pickup keeps the board bottom available');
    }
    chrome.clear();
  });
}

test('decorative desktop gear hover transforms do not resize the camera toolbar region', () => {
  chrome.clear();
  const v = view(true);
  Object.assign(v, { width: 1280, height: 900, main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) } });
  chrome.set('new-game', {
    offsetTop: 12, offsetHeight: 44, offsetParent: null,
    getClientRects: () => [1], getBoundingClientRect: () => ({ top: 12, bottom: 56 }),
  });
  const gear = {
    offsetTop: 12, offsetHeight: 40, offsetParent: null,
    getClientRects: () => [1], getBoundingClientRect: () => ({ top: 12, bottom: 52 }),
  };
  chrome.set('settings-button', gear);
  const before = v.playArea();
  gear.getBoundingClientRect = () => ({ top: 3.715727, bottom: 60.284273 });
  assert.deepEqual(v.playArea(), before);
  assert.equal(v.playArea().top, 64);
  chrome.clear();
});

test('toolbar layout accounts for the canvas visual-viewport offset once', () => {
  chrome.clear();
  const v = view(true);
  Object.assign(v, { width: 390, height: 824, main: { getBoundingClientRect: () => ({ left: 0, top: 20 }) } });
  chrome.set('lobby-toggle', {
    offsetTop: 62, offsetHeight: 44,
    offsetParent: { getBoundingClientRect: () => ({ top: 20 }) },
    getClientRects: () => [1],
  });
  assert.equal(v.playArea().top, 114);
  chrome.clear();
});

for (const perspective of [true, false]) {
  test(`desktop status retraction keeps the exact ${perspective ? 'perspective' : 'flat'} fit but permits resize, zoom and real seat changes`, () => {
    chrome.clear();
    const v = view(perspective);
    Object.assign(v, { width: 1280, height: 900, main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) } });
    chrome.set('bot-banner', {
      getClientRects: () => [1],
      getBoundingClientRect: () => ({ top: 774.203125, bottom: 880 }),
    });
    v.updateCamera(0, 0, 0, null, 'CHANGSHA');
    const area = v.playArea(), projection = v.camera.projectionMatrix.clone();
    assert.equal(area.bottom, 766.203125, 'the measured CI status footprint, not a rounded approximation');
    chrome.delete('bot-banner');
    for (let frame = 0; frame < 183; frame++) {
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert.deepEqual(v.playArea(), area);
      assert.deepEqual(v.camera.projectionMatrix.elements, projection.elements);
    }
    v.height = 1000;
    v.updateCamera(0, 0, 0, null, 'CHANGSHA');
    assert.equal(v.playArea().bottom, 866.203125, 'the reservation is an inset, not a frozen viewport');
    assert.notDeepEqual(v.camera.projectionMatrix.elements, projection.elements);
    const position = v.camera.position.clone(), scale = v.camera.scale.clone();
    v.updateCamera(0, 0, 1, new THREE.Vector2(.2, -.2), 'CHANGSHA');
    assert(!position.equals(v.camera.position) || !scale.equals(v.camera.scale), 'intentional zoom still works');
    v.updateCamera(null, 0, 0, null, 'CHANGSHA');
    assert.equal(v.camera.rotation.x, 0);
    assert.equal(v.playArea().bottom, 992, 'genuine spectator transition releases the previous seat footprint');
    chrome.clear();
  });
}

test('compact status retraction preserves its top band; an explicit user hide releases it', () => {
  chrome.clear();
  bodyClasses.add('mobile-table-status-visible');
  const v = view(true);
  Object.assign(v, { width: 390, height: 844, main: { getBoundingClientRect: () => ({ left: 0, top: 0 }) } });
  chrome.set('lobby-toggle', { offsetTop: 62, offsetHeight: 44, offsetParent: null, getClientRects: () => [1] });
  chrome.set('bot-banner', {
    getClientRects: () => [1], getBoundingClientRect: () => ({ top: 112, bottom: 144 }),
  });
  v.updateCamera(0, 0, 0, null, 'CHANGSHA');
  const projection = v.camera.projectionMatrix.clone();
  assert.equal(v.playArea().top, 152);
  chrome.delete('bot-banner');
  v.updateCamera(0, 0, 0, null, 'CHANGSHA');
  assert.equal(v.playArea().top, 152);
  assert.deepEqual(v.camera.projectionMatrix.elements, projection.elements);
  bodyClasses.delete('mobile-table-status-visible');
  v.updateCamera(0, 0, 0, null, 'CHANGSHA');
  assert.equal(v.playArea().top, 114);
  assert.notDeepEqual(v.camera.projectionMatrix.elements, projection.elements);
  chrome.clear();
});

test('a Changsha world still uses the measured free viewport, independent of HUD class', () => {
  const v = view(true);
  let calls = 0;
  const area = { left: 8, right: 940, top: 110, bottom: 470 };
  v.playArea = () => { calls++; return area; };
  v.updateCamera(0, 0, 0, null, 'CHANGSHA');
  assert.equal(calls, 1);
  for (const corner of v.fitCorners) {
    const p = corner.clone().project(v.camera);
    const x = (p.x + 1) * 480, y = (1 - p.y) * 270;
    assert(x >= area.left - 1e-6 && x <= area.right + 1e-6);
    assert(y >= area.top - 1e-6 && y <= area.bottom + 1e-6);
  }
});

for (const [width, height] of [[1280, 900], [1920, 1080]]) {
  for (const perspective of [true, false]) {
    test(`Changsha ${width}x${height} ${perspective ? 'perspective' : 'flat'} fit survives input projection refresh`, () => {
      const v = view(perspective);
      Object.assign(v, { width, height });
      v.playArea = () => ({ left: 8, right: width - 320, top: 68, bottom: height - 134 });
      const anchors = () => v.fitCorners.map(point => {
        const p = point.clone().project(v.camera);
        return [(p.x + 1) * width / 2, (1 - p.y) * height / 2];
      });
      const deviation = (expected) => Math.max(...anchors().map(([x, y], i) =>
        Math.hypot(x - expected[i][0], y - expected[i][1])));
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      const reference = anchors();
      v.camera.updateProjectionMatrix();
      assert(deviation(reference) < 0.01, `input refreshed the fitted projection by ${deviation(reference)} CSS px`);
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert(deviation(reference) < 0.01, 'successive frames must not accumulate the fit');
      v.updateCamera(0, 1, 0, null, 'CHANGSHA');
      assert(deviation(reference) > 10, 'intentional look-down remains active');
      v.updateCamera(0, 0, 1, new THREE.Vector2(.2, -.2), 'CHANGSHA');
      assert(deviation(reference) > 10, 'intentional zoom and pan remain active');
      v.updateCamera(0, 0, 0, null, 'CHANGSHA');
      assert(deviation(reference) < 0.01, 'releasing view controls restores the fitted baseline');
    });
  }
}

for (const perspective of [true, false]) {
  test(`switching a fitted Changsha ${perspective ? 'perspective' : 'flat'} camera to relay restores the original frustum`, () => {
    const v = view(perspective);
    v.playArea = () => ({ left: 8, right: 640, top: 110, bottom: 470 });
    v.updateCamera(0, 0, 0, null, 'CHANGSHA');
    v.playArea = () => { throw new Error('Relay must not inspect Changsha chrome'); };
    v.updateCamera(0, 0, 0, null, 'FOUR_PLAYER');
    const reference = view(perspective);
    reference.updateCamera(0, 0, 0, null, 'FOUR_PLAYER');
    assert.deepEqual(v.camera.projectionMatrix.elements, reference.camera.projectionMatrix.elements);
  });
}
