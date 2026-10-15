import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runInNewContext } from 'node:vm';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const THREE = require('three');
const ts = require('typescript');
const compile = filename => ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
require.extensions['.ts'] = (module, filename) => module._compile(compile(filename), filename);
const World = { WIDTH: 174 };
const viewport = { innerWidth: 1900, innerHeight: 1063 };
const load = (name, stubs = {}) => {
  const module = { exports: {} };
  runInNewContext(compile(path.join(root, 'src', name + '.ts')), {
    module, exports: module.exports, console, window: viewport,
    require: id => stubs[id] ?? (id === 'three' ? THREE : require(path.join(root, 'src', id))),
  });
  return module.exports;
};
const { AssetLoader } = load('asset-loader', {
  './world': { World },
  '../img/table.jpg?url': { default: 'unused-table' },
  '../img/tiles-labels.auto.png?url': { default: 'unused-labels' },
  '../img/models.auto.glb?url': { default: 'unused-models' },
});
let variant = 'FOUR_PLAYER';
const { ObjectView } = load('object-view', {
  './world': { World },
  './client-ui': { readVariantFromUrl: () => variant },
  './center': { Center: class {
    mesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshLambertMaterial());
    setScores() {}
    draw() {}
  } },
});
const { MainView } = load('main-view', {
  './world': { World },
  './render/custom-outline': { CustomOutline: class {} },
});
const { makeSlots } = require(path.join(root, 'src/setup-slots.ts'));
const { slotLayoutFitCorners } = require(path.join(root, 'src/table-fit.ts'));
const { GLTFLoader } = await import(pathToFileURL(require.resolve('three/examples/jsm/loaders/GLTFLoader.js')));
const { mergeGeometries } = await import(pathToFileURL(require.resolve('three/examples/jsm/utils/BufferGeometryUtils.js')));
const loader = new GLTFLoader();
loader.register(() => ({
  name: 'CPU_GEOMETRY_NO_TEXTURE_IO',
  loadMaterial: () => Promise.resolve(new THREE.MeshLambertMaterial()),
}));
const bytes = fs.readFileSync(path.join(root, 'img/models.auto.glb'));
const model = await new Promise((resolve, reject) => loader.parse(
  bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '', resolve, reject));
const prototype = model.scene.getObjectByName('tray');
assert(prototype?.isMesh);
assert(prototype.geometry.index, 'the shipped tray really uses indexed triangles');

function assets(geometry = prototype.geometry) {
  const result = new AssetLoader();
  result.textures.table = new THREE.Texture();
  result.textures.tilesLabels = new THREE.Texture();
  for (const mesh of model.scene.children.filter(child => child.isMesh)) {
    result.meshes[mesh.name] = result.processMesh(mesh.clone());
  }
  result.meshes.tray.geometry = geometry;
  return result;
}

function reference(geometry) {
  const instances = [];
  for (let seat = 0; seat < 4; seat++) for (let column = 0; column < 6; column++) {
    const position = new THREE.Vector3(25 + 24 * column - World.WIDTH / 2, -33 - World.WIDTH / 2, 0)
      .applyAxisAngle(new THREE.Vector3(0, 0, 1), seat * Math.PI / 2)
      .add(new THREE.Vector3(World.WIDTH / 2, World.WIDTH / 2, 0));
    instances.push(geometry.clone().rotateZ(seat * Math.PI / 2).translate(position.x, position.y, 0));
  }
  const merged = mergeGeometries(instances, false);
  assert(merged, 'the upstream merger accepts the shipped tray attributes');
  return merged.index ? merged.toNonIndexed() : merged;
}

function fingerprint(geometry) {
  const hash = createHash('sha256');
  for (const attribute of [geometry.index, ...Object.values(geometry.attributes)].filter(Boolean)) {
    hash.update(Buffer.from(attribute.array.buffer, attribute.array.byteOffset, attribute.array.byteLength));
  }
  return hash.digest('hex');
}

function sameTriangles(actual, expected) {
  assert.equal(actual.index, null);
  assert.equal(actual.attributes.position.count, expected.attributes.position.count,
    'all 24 trays must retain every original triangle, not connect consecutive shared vertices');
  assert.deepEqual(Object.keys(actual.attributes).sort(), Object.keys(expected.attributes).sort());
  for (const name of Object.keys(expected.attributes)) {
    const a = actual.attributes[name], b = expected.attributes[name];
    assert.equal(a.itemSize, b.itemSize, name);
    assert.equal(a.normalized, b.normalized, name);
    assert.equal(a.array.length, b.array.length, name);
    let error = 0;
    for (let index = 0; index < b.array.length; index++) error = Math.max(error, Math.abs(a.array[index] - b.array[index]));
    assert(error < 1e-5, `${name} triangle stream changed by ${error}`);
  }
  actual.computeBoundingBox();
  actual.computeBoundingSphere();
  assert(Number.isFinite(actual.boundingSphere.radius));
  assert(actual.boundingBox.min.toArray().every(Number.isFinite));
  assert(actual.boundingBox.max.toArray().every(Number.isFinite));
}

for (const gameType of ['FOUR_PLAYER', 'THREE_PLAYER', 'BAMBOO', 'MINEFIELD']) {
  test(`actual ${gameType} tray triangles match the upstream merger and preserve the asset`, () => {
    variant = gameType;
    const before = fingerprint(prototype.geometry);
    const view = new ObjectView(new THREE.Group(), assets(), {});
    assert(view.trayMesh?.visible);
    sameTriangles(view.trayMesh.geometry, reference(prototype.geometry));
    assert.equal(fingerprint(prototype.geometry), before);
    const geometry = view.trayMesh.geometry;
    view.setVariant('CHANGSHA');
    assert.equal(view.trayMesh.visible, false);
    assert.equal(view.center.mesh.visible, false);
    view.setVariant(gameType);
    assert.equal(view.trayMesh.visible, true);
    assert.equal(view.center.mesh.visible, true);
    assert.equal(view.trayMesh.geometry, geometry);
  });
}

test('already non-indexed tray assets keep their complete triangle and UV streams', () => {
  variant = 'FOUR_PLAYER';
  const geometry = prototype.geometry.toNonIndexed();
  const before = fingerprint(geometry);
  const view = new ObjectView(new THREE.Group(), assets(geometry), {});
  sameTriangles(view.trayMesh.geometry, reference(geometry));
  assert.equal(fingerprint(geometry), before);
});

test('Changsha still omits the Riichi scoring trays', () => {
  variant = 'CHANGSHA';
  const view = new ObjectView(new THREE.Group(), assets(), {});
  assert.equal(view.trayMesh, null);
  assert.equal(view.center.mesh.visible, false);
});

for (const gameType of ['FOUR_PLAYER', 'THREE_PLAYER', 'BAMBOO', 'MINEFIELD']) {
  for (const perspective of [true, false]) {
    test(`${gameType} full-scene framing includes the real trays and preserves view controls, perspective=${perspective}`, () => {
      variant = gameType;
      const objects = new ObjectView(new THREE.Group(), assets(), {});
      const slots = makeSlots(gameType);
      const before = JSON.stringify(slots.map(slot => slot.places));
      const points = slotLayoutFitCorners(slots, objects.sceneryFitCorners);
      assert(points.length > 8, 'separated seat zones must not collapse into a mostly empty box');
      const envelope = new THREE.Box3().setFromPoints(points);
      const tray = objects.trayMesh;
      tray.geometry.computeBoundingBox();
      assert(envelope.containsBox(tray.geometry.boundingBox.clone().applyMatrix4(tray.matrixWorld)));
      const parked = {
        offTable: true,
        places: [{ position: new THREE.Vector3(0, 0, -100000), size: new THREE.Vector3(6, 9, 4) }],
      };
      assert.deepEqual(slotLayoutFitCorners([...slots, parked], objects.sceneryFitCorners), points);
      assert.equal(JSON.stringify(slots.map(slot => slot.places)), before);
      for (const [width, height] of [[1900, 1063], [1280, 900]]) for (const seat of [0, 1, 2, 3, null]) {
        Object.assign(viewport, { innerWidth: width, innerHeight: height });
        const camera = perspective ? new THREE.PerspectiveCamera(30, width / height, .1, 1000)
          : new THREE.OrthographicCamera(-1, 1, 1, -1, .1, 1000);
        const rig = new THREE.Group();
        rig.position.set(87, 87, 0);
        rig.add(camera);
        const area = { left: 228, top: 64, right: width - 328, bottom: height - 64 };
        const view = Object.create(MainView.prototype);
        Object.assign(view, {
          camera, perspective, viewGroup: rig, width, height,
          mobileLayoutMode: null, resolvedMobileLayout: null, playArea: () => area,
        });
        view.updateCamera(seat, 0, 0, null, gameType, points);
        for (const point of points) {
          const projected = point.clone().project(camera);
          const x = (projected.x + 1) * width / 2, y = (1 - projected.y) * height / 2;
          assert(x >= area.left - 1e-5 && x <= area.right + 1e-5, `clipped tray x=${x}`);
          assert(y >= area.top - 1e-5 && y <= area.bottom + 1e-5, `clipped tray y=${y}`);
          assert(projected.z >= -1 && projected.z <= 1);
        }
        const projection = camera.projectionMatrix.toArray();
        camera.updateProjectionMatrix();
        assert.deepEqual(camera.projectionMatrix.toArray(), projection, 'raycasting rebuild keeps the full scene fit');
        for (let frame = 0; frame < 3; frame++) {
          view.updateCamera(seat, 0, 0, null, gameType, points);
          assert.deepEqual(camera.projectionMatrix.toArray(), projection);
        }
        if (seat !== null) {
          const position = camera.position.clone(), scale = camera.scale.x;
          view.updateCamera(seat, 1, .5, new THREE.Vector2(.2, -.1), gameType, points);
          assert(camera.position.distanceTo(position) > 1 || camera.scale.x !== scale);
          assert.deepEqual(camera.projectionMatrix.toArray(), projection, 'intentional controls are not fitted away');
        }
      }
    });
  }
}
