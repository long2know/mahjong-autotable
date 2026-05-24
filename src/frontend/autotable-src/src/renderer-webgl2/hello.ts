// Phase K Wave 15 — WebGL2 renderer hello-world mount (Hicks).
//
// Phase L W1 spike entry.  Loaded by `src/index.ts` ONLY when the
// URL contains `?renderer=webgl2-hello`; never runs on the lobby
// cold path.  Its sole job in W15 is to:
//
//   1. Prove the chunk split works — `vite.config.ts:manualChunks`
//      routes everything under `src/renderer-webgl2/` into a
//      `renderer-webgl2.<hash>.js` chunk.  `append-dist-size.js`
//      records the chunk size in `dist-size.json`.
//   2. Render a single textured quad against a real mahjong-tile
//      image (the same `tiles-labels.auto.png` the production
//      renderer ships).  This validates the W14 spike's bundle
//      math against a "hello world" plus one texture pass.
//   3. NOT depend on three.js — the renderer-webgl2 module is
//      free-standing.
//
// Future Phase L waves (W1+) expand this entry into the full
// renderer: tile mesh + dice mesh + stick mesh + camera control +
// raycaster + lighting model.  The hello-world is intentionally
// minimal so the W15 chunk number is the BASELINE we measure
// against ("hello world cost") — every Phase L wave appends to
// this baseline.

import { helloWorld } from './index';

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
    canvas.width = 512;
    canvas.height = 512;
    canvas.style.cssText = 'width:512px;height:512px;border:1px solid #2a3a4a;background:#000;';
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

/**
 * Public entry point invoked by `src/index.ts` behind the
 * `?renderer=webgl2-hello` URL guard.
 */
export async function mount(): Promise<void> {
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
