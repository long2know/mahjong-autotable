// Phase K Wave 15 — WebGL2 renderer hello-world mount (Hicks).
//
// Phase L W1 spike entry.  Loaded by `src/index.ts` ONLY when the
// URL contains `?renderer=webgl2-hello` (or the W16
// `?renderer=webgl2-tile-mesh` smoke, or the W17
// `?renderer=webgl2-scene` smoke); never runs on the lobby cold
// path.  Its sole job through W15 / W16 / W17 is to:
//
//   1. Prove the chunk split works — `vite.config.ts:manualChunks`
//      routes everything under `src/renderer-webgl2/` into a
//      `renderer-webgl2.<hash>.js` chunk.  `append-dist-size.js`
//      records the chunk size in `dist-size.json`.
//   2. Render one of three smoke modes (textured-quad hello-world,
//      instanced tile-mesh, or full scene-runtime + picking) against
//      the canonical tile atlas (W17) or its synth fallback.
//   3. NOT depend on three.js — the renderer-webgl2 module is
//      free-standing.
//
// Future Phase L waves (W4+) expand this entry into the full
// renderer: animation graph, lighting model, post-fx.  The
// W15+W16+W17 baselines are the "hello world + tile graph + scene
// runtime + picking" costs we measure every subsequent wave
// against.

import { helloWorld } from './index';
import {
  createTileMesh,
  drawTileMesh,
  populateWallDemo,
  setTileInstance,
  TILE_INSTANCE_FS,
  TILE_INSTANCE_VS,
  uploadTileInstances,
} from './tile-mesh';
import { acquireTileAtlas } from './tile-atlas';
import { TILE_FACE_COUNT, tileFace } from './tile-faces';
import {
  applyCameraMode,
  attachMouseControls,
  attachTouchControls,
  createOrbitCamera,
  viewProjMatrix,
  type CameraMode,
} from './camera';
import { compileProgram, createWebgl2Context } from './index';
import { identity4, scaleMatrix4, translateMatrix4 } from './math';
import { createTileScene } from './scene';
import { buildPickRay, pickTile } from './picking';
import {
  populateWallWithDora,
  CANONICAL_WALL_TILE_COUNT,
} from './wall-geometry';

const CONTAINER_ID = 'webgl2-hello-container';
const TEXTURE_URL = '/img/tiles-labels.auto.png';

async function loadTexture(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = (): void => resolve(img);
    img.onerror = (): void => reject(new Error(`[webgl2-hello] failed to load ${url}`));
    img.src = url;
  });
}

function ensureContainer(): { canvas: HTMLCanvasElement; status: HTMLElement } {
  let container = document.getElementById(CONTAINER_ID);
  if (container === null) {
    container = document.createElement('div');
    container.id = CONTAINER_ID;
    container.setAttribute('data-testid', 'webgl2-hello-container');
    container.style.cssText =
      'position:fixed;inset:0;display:flex;flex-direction:column;'
      + 'align-items:center;justify-content:center;background:#0d161e;'
      + 'color:#eaeaea;font-family:system-ui,sans-serif;z-index:99999;';
    document.body.appendChild(container);
  }
  let canvas = container.querySelector<HTMLCanvasElement>('canvas');
  if (canvas === null) {
    canvas = document.createElement('canvas');
    canvas.width = 800;
    canvas.height = 512;
    canvas.style.cssText = 'width:800px;height:512px;border:1px solid #2a3a4a;background:#000;';
    canvas.setAttribute('data-testid', 'webgl2-hello-canvas');
    container.appendChild(canvas);
  }
  let status = container.querySelector<HTMLElement>('.webgl2-hello-status');
  if (status === null) {
    status = document.createElement('p');
    status.className = 'webgl2-hello-status';
    status.setAttribute('data-testid', 'webgl2-hello-status');
    status.style.cssText = 'margin:12px 0 0;font-size:14px;';
    container.appendChild(status);
  }
  return { canvas, status };
}

function mode(): 'hello' | 'tile-mesh' | 'scene' | 'wall' {
  const s = window.location.search;
  if (/[?&]renderer=webgl2-wall/.test(s)) return 'wall';
  if (/[?&]renderer=webgl2-scene/.test(s)) return 'scene';
  if (/[?&]renderer=webgl2-tile-mesh/.test(s)) return 'tile-mesh';
  return 'hello';
}

/**
 * Public entry point invoked by `src/index.ts` behind the
 * `?renderer=webgl2-hello` / `?renderer=webgl2-tile-mesh` /
 * `?renderer=webgl2-scene` / `?renderer=webgl2-wall` URL guards.
 */
export async function mount(): Promise<void> {
  switch (mode()) {
    case 'wall':      return mountWall();
    case 'scene':     return mountScene();
    case 'tile-mesh': return mountTileMesh();
    default:          return mountHelloWorld();
  }
}

async function mountHelloWorld(): Promise<void> {
  const { canvas, status } = ensureContainer();
  status.textContent = 'Loading tile texture…';
  let texture: HTMLImageElement;
  try {
    texture = await loadTexture(TEXTURE_URL);
  } catch (err) {
    status.textContent = `Texture load failed: ${(err as Error).message}`;
    return;
  }
  try {
    const handle = helloWorld(canvas, texture);
    status.textContent = `WebGL2 hello-world rendered (${texture.naturalWidth}×${texture.naturalHeight} texture).`;
    window.addEventListener('resize', () => handle.redraw(), { passive: true });
  } catch (err) {
    status.textContent = `WebGL2 init failed: ${(err as Error).message}`;
  }
}

async function mountTileMesh(): Promise<void> {
  const { canvas, status } = ensureContainer();
  status.textContent = 'Initialising tile mesh…';
  const gl = createWebgl2Context(canvas);
  if (gl === null) {
    status.textContent = 'WebGL2 unavailable in this browser.';
    return;
  }

  let program: WebGLProgram;
  try {
    program = compileProgram(gl, TILE_INSTANCE_VS, TILE_INSTANCE_FS);
  } catch (err) {
    status.textContent = `Shader compile failed: ${(err as Error).message}`;
    return;
  }

  const mesh = createTileMesh(gl, program);
  populateWallDemo(mesh);

  // Add 14 in-hand tiles in front of the player so the smoke render
  // exercises a mix of orientations.
  for (let i = 0; i < 14 && mesh.instanceCount < mesh.capacity; i++) {
    const m = identity4();
    m[12] = (i - 6.5) * 1.05;
    m[13] = 0.5;
    m[14] = 2.0;
    setTileInstance(mesh, mesh.instanceCount, m, i % 34);
  }

  uploadTileInstances(gl, mesh);

  status.textContent = 'Loading tile atlas…';
  const atlas = await acquireTileAtlas(gl);
  const atlasLabel = atlas.fallback
    ? `synthesized ${atlas.width}×${atlas.height} fallback atlas`
    : `loaded ${atlas.width}×${atlas.height} atlas`;

  const cam = createOrbitCamera();
  const uViewProj = gl.getUniformLocation(program, 'u_viewProj');
  const uAtlasGrid = gl.getUniformLocation(program, 'u_atlasGrid');
  const uAtlas = gl.getUniformLocation(program, 'u_atlas');
  const uLightDir = gl.getUniformLocation(program, 'u_lightDir');

  const draw = (): void => {
    const w = canvas.clientWidth | 0;
    const h = canvas.clientHeight | 0;
    if (canvas.width !== w) canvas.width = Math.max(1, w);
    if (canvas.height !== h) canvas.height = Math.max(1, h);
    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.clearColor(0.05, 0.08, 0.05, 1.0);
    gl.enable(gl.DEPTH_TEST);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    gl.useProgram(program);
    gl.uniformMatrix4fv(uViewProj, false, viewProjMatrix(cam, canvas));
    gl.uniform2f(uAtlasGrid, atlas.gridCols, atlas.gridRows);
    gl.uniform3f(uLightDir, 0.4, 1.0, 0.3);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, atlas.texture);
    gl.uniform1i(uAtlas, 0);

    drawTileMesh(gl, mesh);
  };

  draw();
  status.textContent =
    `WebGL2 tile-mesh rendered (${mesh.instanceCount} instances across `
    + `${TILE_FACE_COUNT} tile faces, ${atlasLabel}). `
    + 'Drag = orbit; right-drag = pan; wheel = zoom.';

  attachMouseControls(canvas, cam, draw);
  attachTouchControls(canvas, cam, draw);
  window.addEventListener('resize', draw, { passive: true });
}

// Phase K Wave 17 — full scene-runtime smoke.  Exercises the
// `createTileScene()` orchestrator + the `pickTile()` ray-cast in
// one go.  Click any tile to highlight it (the front column hue
// inverts for the picked instance for one frame so the hit is
// visible without a separate UI overlay).
async function mountScene(): Promise<void> {
  const { canvas, status } = ensureContainer();
  status.textContent = 'Booting tile-scene runtime…';

  let scene: Awaited<ReturnType<typeof createTileScene>>;
  try {
    scene = await createTileScene(canvas);
  } catch (err) {
    status.textContent = `WebGL2 scene init failed: ${(err as Error).message}`;
    return;
  }

  // Wall demo + in-hand row, but plumbed through the scene helper so
  // every change schedules a coalesced rAF redraw (no manual draw()).
  let i = 0;
  for (let row = 0; row < 4; row++) {
    for (let col = 0; col < 36; col++) {
      if (i >= scene.mesh.capacity) break;
      const xOffset = (col - 17.5) * 1.02;
      const zOffset = (row - 1.5) * 0.7 - 6.0;
      const m = identity4();
      scaleMatrix4(m, 1.0, 1.0, 1.0);
      translateMatrix4(m, xOffset, 0, zOffset);
      scene.setTileAt(i, m, i % 34);
      i++;
    }
  }
  for (let j = 0; j < 14 && i < scene.mesh.capacity; j++, i++) {
    const m = identity4();
    m[12] = (j - 6.5) * 1.05;
    m[13] = 0.5;
    m[14] = 2.0;
    scene.setTileAt(i, m, j % 34);
  }

  scene.drawNow();

  // Mouse-pick: build a world-space ray from the click, intersect
  // every instance, and bump the picked tile a quarter-tile up to
  // confirm the pick.  Right-click resets the bump.
  let pickedIndex: number | null = null;
  let pickedMatrix: Float32Array | null = null;
  canvas.addEventListener('click', (ev: MouseEvent) => {
    if (ev.button !== 0) return;
    if (ev.shiftKey) return; // shift-click = pan (camera handler)
    const vp = viewProjMatrix(scene.camera, canvas);
    const ray = buildPickRay(canvas, ev.clientX, ev.clientY, vp);
    const hit = pickTile(scene.mesh, ray);
    if (hit === null) {
      status.textContent = `No tile hit at (${ev.clientX}, ${ev.clientY}).`;
      return;
    }
    // Restore previous pick.
    if (pickedIndex !== null && pickedMatrix !== null) {
      scene.setTileAt(pickedIndex, pickedMatrix, scene.mesh.tileIdData[pickedIndex]);
    }
    // Stash the current matrix so we can undo the bump on the next pick.
    const base = hit.instanceIndex * 16;
    pickedMatrix = new Float32Array(scene.mesh.modelData.subarray(base, base + 16));
    pickedIndex = hit.instanceIndex;
    const bumped = new Float32Array(pickedMatrix);
    bumped[13] += 0.75; // lift along +y
    scene.setTileAt(pickedIndex, bumped, scene.mesh.tileIdData[pickedIndex]);
    const pickedTileId = scene.mesh.tileIdData[pickedIndex];
    const face = tileFace(pickedTileId);
    const faceLabel = face ? `${face.label} (${face.suit}-${face.value})` : `id ${pickedTileId}`;
    status.textContent =
      `Picked tile #${pickedIndex} [${faceLabel}] at world (${hit.point[0].toFixed(2)}, `
      + `${hit.point[1].toFixed(2)}, ${hit.point[2].toFixed(2)}).`;
  });

  const atlasLabel = scene.atlas.fallback
    ? `synthesized ${scene.atlas.width}×${scene.atlas.height} fallback atlas`
    : `loaded ${scene.atlas.width}×${scene.atlas.height} canonical atlas`;
  status.textContent =
    `WebGL2 scene runtime rendered (${scene.mesh.instanceCount} instances across `
    + `${TILE_FACE_COUNT} tile faces, ${atlasLabel}). `
    + 'Drag = orbit; right-drag = pan; wheel = zoom; click = pick tile.';
}

// Phase K Wave 19 — canonical Changsha wall smoke.  Lays the full
// 4 × 18 × 2 wall using `populateWallWithDora()` and exposes the
// three W19 camera modes (orbital / isometric-flat / perspective
// three-quarter) via a small button row above the canvas.  This is
// the visual-parity baseline the W19 e2e spec captures against.
async function mountWall(): Promise<void> {
  const { canvas, status } = ensureContainer();
  status.textContent = 'Booting canonical wall scene…';

  // We don't pre-bump the mesh capacity here — the W16 TileMesh ships
  // with MAX_INSTANCES = 200 which is comfortably above the 144 wall
  // tiles (+ 1 dora) we need for the W19 smoke.
  let scene: Awaited<ReturnType<typeof createTileScene>>;
  try {
    scene = await createTileScene(canvas);
  } catch (err) {
    status.textContent = `WebGL2 scene init failed: ${(err as Error).message}`;
    return;
  }

  populateWallWithDora(scene.mesh, /*seed=*/ 0x77313939);
  scene.drawNow();

  // Camera-mode picker — a 3-button strip above the status line.  We
  // host it in the existing container so the test selector
  // (`[data-testid="webgl2-wall-camera-<mode>"]`) is stable.
  installCameraModePicker(scene.camera, scene.canvas, scene.requestRedraw);

  // Click-on-tile shows the face label, matching the W17 scene smoke.
  canvas.addEventListener('click', (ev: MouseEvent) => {
    if (ev.button !== 0 || ev.shiftKey) return;
    const vp = viewProjMatrix(scene.camera, canvas);
    const ray = buildPickRay(canvas, ev.clientX, ev.clientY, vp);
    const hit = pickTile(scene.mesh, ray);
    if (hit === null) {
      status.textContent = `No wall tile at (${ev.clientX}, ${ev.clientY}).`;
      return;
    }
    const pickedTileId = scene.mesh.tileIdData[hit.instanceIndex];
    const face = tileFace(pickedTileId);
    const faceLabel = face ? `${face.label} (${face.suit}-${face.value})` : `id ${pickedTileId}`;
    status.textContent =
      `Wall tile #${hit.instanceIndex} [${faceLabel}] — `
      + `mode=${scene.camera.mode}.`;
  });

  const atlasLabel = scene.atlas.fallback
    ? `synthesized ${scene.atlas.width}×${scene.atlas.height} fallback atlas`
    : `loaded ${scene.atlas.width}×${scene.atlas.height} canonical atlas`;
  status.textContent =
    `Canonical wall rendered — ${CANONICAL_WALL_TILE_COUNT} tiles `
    + `(4 × 18 × 2 + dora), ${atlasLabel}.  `
    + 'Camera modes above; drag = orbit; wheel = zoom; click = pick.';
}

function installCameraModePicker(
  camera: Parameters<typeof applyCameraMode>[0],
  canvas: HTMLCanvasElement,
  requestRedraw: () => void,
): void {
  const container = canvas.parentElement;
  if (container === null) return;
  let existing = container.querySelector<HTMLElement>('.webgl2-wall-camera-picker');
  if (existing !== null) existing.remove();
  const strip = document.createElement('div');
  strip.className = 'webgl2-wall-camera-picker';
  strip.setAttribute('data-testid', 'webgl2-wall-camera-picker');
  strip.style.cssText =
    'display:flex;gap:8px;margin:8px 0;justify-content:center;';
  const modes: Array<{ id: CameraMode; label: string }> = [
    { id: 'orbital', label: 'Orbital' },
    { id: 'isometric-flat', label: 'Iso flat' },
    { id: 'perspective-three-quarter', label: '¾ persp' },
  ];
  for (const m of modes) {
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = m.label;
    b.setAttribute('data-testid', `webgl2-wall-camera-${m.id}`);
    b.style.cssText =
      'padding:6px 12px;background:#1a2733;color:#eaeaea;'
      + 'border:1px solid #2a3a4a;border-radius:4px;cursor:pointer;';
    b.addEventListener('click', () => {
      applyCameraMode(camera, m.id);
      requestRedraw();
    });
    strip.appendChild(b);
  }
  // Insert above the canvas so the picker sits between the
  // container's flex children naturally.
  container.insertBefore(strip, canvas);
}
