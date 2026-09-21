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
const module = { exports: {} };
runInNewContext(ts.transpileModule(fs.readFileSync(path.join(root, 'src/main-view.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, {
  module, exports: module.exports,
  require: id => ({
    three: THREE,
    './world': { World: { WIDTH: 174 } },
    './types': types,
    './table-fit': fit,
    './render/custom-outline': { CustomOutline: class {} },
  })[id],
  document: { body: { classList: { contains: () => false } } },
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
  Object.assign(v, { camera, perspective, viewGroup: group, width: 960, height: 540, fitCorners: corners });
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
