// Phase K Wave 15 — WebGL2 renderer hello-world mount (Hicks).
//
// Phase L W1 spike entry.  Loaded by `src/index.ts` ONLY when the
// URL contains `?renderer=webgl2-hello` (or the W16
// `?renderer=webgl2-tile-mesh` smoke); never runs on the lobby
// cold path.  Its sole job in W15 / W16 is to:
//
//   1. Prove the chunk split works — `vite.config.ts:manualChunks`
//      routes everything under `src/renderer-webgl2/` into a
//      `renderer-webgl2.<hash>.js` chunk.  `append-dist-size.js`
//      records the chunk size in `dist-size.json`.
//   2. Render the W15 textured-quad hello-world (default mode) OR
//      the W16 instanced tile-mesh smoke (`?renderer=webgl2-tile-
//      mesh`) against a synthesized atlas.  Both validate the
//      bundle math against a real `npm run build:vite`.
//   3. NOT depend on three.js — the renderer-webgl2 module is
//      free-standing.
//
// Future Phase L waves (W3+) expand this entry into the full
// renderer: animation graph, lighting model, picking / raycaster,
// post-fx.  The W15+W16 baselines are the "hello world + tile
// graph" costs we measure every subsequent wave against.

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
import {
  attachMouseControls,
  attachTouchControls,
  createOrbitCamera,
  viewProjMatrix,
} from './camera';
import { compileProgram, createWebgl2Context } from './index';
import { identity4 } from './math';

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

function isTileMeshMode(): boolean {
  return /[?&]renderer=webgl2-tile-mesh/.test(window.location.search);
}

/**
 * Public entry point invoked by `src/index.ts` behind the
 * `?renderer=webgl2-hello` / `?renderer=webgl2-tile-mesh` URL guard.
 */
export async function mount(): Promise<void> {
  if (isTileMeshMode()) {
    return mountTileMesh();
  }
  return mountHelloWorld();
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
    `WebGL2 tile-mesh rendered (${mesh.instanceCount} instances, ${atlasLabel}). `
    + 'Drag = orbit; right-drag = pan; wheel = zoom.';

  attachMouseControls(canvas, cam, draw);
  attachTouchControls(canvas, cam, draw);
  window.addEventListener('resize', draw, { passive: true });
}
