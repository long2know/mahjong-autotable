import { AmbientLight, Camera, DirectionalLight, Group, Mesh, Object3D, OrthographicCamera, PerspectiveCamera, PlaneGeometry, Scene, Vector2, Vector3, WebGLRenderer } from 'three';
import { World } from './world';
import { CustomOutline } from './render/custom-outline';

// Phase K Wave 7 — OutlinePass + EffectComposer + RenderPass are
// gone.  The renderer is now a single-pass `renderer.render(scene,
// camera)` call; the yellow tile-selection halo is provided by the
// custom inverted-hull `CustomOutline` helper (≈3 kB) instead of
// three's post-processing pipeline (≈90 kB combined).  See
// docs/frontend-three-budget.md §3 for the spike rationale + visual
// parity audit.

const RATIO = 1.5;

// Phase K Wave 6 — Tree-shake the dev-only FPS overlay out of the
// production build.  Wave 5's `three-renderer.<hash>.js` carried
// `three/examples/jsm/libs/stats.module.js` even though the FPS panel
// has no production owner — every user paid for a debug widget they
// could not see (the DOM node was appended to `#full` but pinned
// off-screen by `style.right = '0'`).  Wave 6 swaps the static import
// for a lazy `?stats=1` query-string opt-in: the dev tool still works
// (`?stats=1` triggers a dynamic import of `stats.module.js`), the
// production user no longer fetches it.  See
// docs/frontend-three-budget.md for the full add-on audit.
interface StatsLike {
  readonly dom: HTMLElement;
  update(): void;
}

const isStatsEnabled = (): boolean => {
  if (typeof window === 'undefined') return false;
  try { return /[?&]stats=1\b/.test(window.location.search); }
  catch { return false; }
};

export class MainView {
  private main: HTMLElement;
  private stats: StatsLike | null = null;
  private perspective = false;

  private scene: Scene;
  private mainGroup: Group;
  private viewGroup: Group;
  private renderer: WebGLRenderer;

  camera: Camera = null!;
  private outline: CustomOutline = new CustomOutline();

  private width = 0;
  private height = 0;

  private dummyObject: Object3D;

  // Phase K Wave 8 — Commentary tile-ref highlight overlay.  When a
  // `mahjong:highlight-tile` CustomEvent fires (dispatched by
  // `commentary-panel.ts` on tile-ref chip click), the overlay flashes
  // a yellow halo on top of the canvas for `HIGHLIGHT_PULSE_MS`.  The
  // halo lives as a DOM sibling of the WebGL canvas (not as a 3D
  // overlay) so it doesn't fight the per-frame `outline.setSelected`
  // rebuild driven by `objectView.selectedObjects`, and so Playwright
  // can assert via `[data-highlight-tile-id]` selector without
  // touching the renderer pipeline.  Latency is one event-loop turn —
  // well under the 500 ms `commentary-tile-ref-flash` budget.  The
  // actual tile-id → 3D mesh mapping is deferred to Phase L (the
  // wire format "S2-Z7" / "M1" is opaque without the runtime tile
  // dictionary that lives server-side in commentary generation).
  private highlightOverlay: HTMLDivElement | null = null;
  private highlightTimer: number | null = null;
  private static readonly HIGHLIGHT_PULSE_MS = 2000;

  constructor(mainGroup: Group) {
    this.mainGroup = mainGroup;
    this.main = document.getElementById('main')!;

    this.scene = new Scene();
    this.scene.matrixWorldAutoUpdate = false;
    this.viewGroup = new Group();
    this.viewGroup.position.set(World.WIDTH/2, World.WIDTH/2, 0);
    this.scene.add(this.mainGroup);
    this.scene.add(this.viewGroup);

    this.dummyObject = new Mesh(new PlaneGeometry(0, 0, 0));

    this.renderer = new WebGLRenderer({
      antialias: false,
      // Phase K Wave 7 — Kept for visual continuity with W6 (the
      // logarithmic depth buffer also helps the inverted-hull
      // outline stay tight against the silhouette at small zoom
      // levels).  The historical reason — "OutlinePass causes
      // glitching on some browsers" — no longer applies.
      logarithmicDepthBuffer: true,
    });
    this.main.appendChild(this.renderer.domElement);

    this.setupLights();
    this.setupRendering();
    this.setupHighlightOverlay();

    // Phase K Wave 6 — Dev-only FPS overlay (lazy).  Only loaded when
    // the URL carries `?stats=1`; the production cold-load no longer
    // ships ~3 kB of stats.module.js minified.
    if (isStatsEnabled()) {
      void import('three/examples/jsm/libs/stats.module.js').then(mod => {
        const StatsCtor = (mod as { default: new () => StatsLike }).default;
        const stats = new StatsCtor();
        stats.dom.style.left = 'auto';
        stats.dom.style.right = '0';
        document.getElementById('full')?.appendChild(stats.dom);
        this.stats = stats;
      }).catch(() => { /* dev-only — ignore failures */ });
    }
  }

  /**
   * Phase K Wave 8 — Build the commentary highlight overlay + wire
   * the `mahjong:highlight-tile` window listener.  The overlay is a
   * pointer-events-none div positioned over the canvas; on event,
   * we set its `--highlight-color` custom property and toggle
   * `data-highlight-active` for 2 s, then clear.  The host
   * `#main` element receives `data-highlight-tile-id` so Playwright
   * can assert immediately on the event delivery (visible-within
   * latency contract is < 500 ms because we set the data attr
   * synchronously inside the click handler chain).
   */
  private setupHighlightOverlay(): void {
    const overlay = document.createElement('div');
    overlay.className = 'tile-highlight-overlay';
    overlay.setAttribute('data-testid', 'tile-highlight-overlay');
    overlay.setAttribute('aria-hidden', 'true');
    this.main.appendChild(overlay);
    this.highlightOverlay = overlay;

    window.addEventListener('mahjong:highlight-tile', (event: Event) => {
      const detail = (event as CustomEvent<{ tileId: string }>).detail;
      if (detail === undefined || detail === null) return;
      const tileId = detail.tileId;
      if (typeof tileId !== 'string' || tileId.length === 0) return;
      this.pulseHighlight(tileId);
    });
  }

  /**
   * Phase K Wave 8 — Pulse the commentary highlight overlay for the
   * given tile id.  Re-entrant: a second pulse before the first one
   * expires resets the timer (the most-recently-clicked chip wins).
   * No leak: the timer is cleared on every entry and the overlay is
   * unconditionally deactivated when the timer fires.
   *
   * Vasquez's `commentary-tile-ref-latency.spec.ts` reads
   * `window.__lastHighlightedTile` (string) and
   * `window.__highlightTimestampMs` (DOMHighResTimeStamp from
   * `performance.now()`) — both written synchronously inside this
   * call so the chip-click → observability handoff stays well
   * under the 500 ms latency budget.
   */
  pulseHighlight(tileId: string): void {
    if (this.highlightOverlay === null) return;
    if (this.highlightTimer !== null) {
      window.clearTimeout(this.highlightTimer);
      this.highlightTimer = null;
    }
    const tsMs = (typeof performance !== 'undefined' && typeof performance.now === 'function')
      ? performance.now()
      : Date.now();
    this.main.setAttribute('data-highlight-tile-id', tileId);
    this.highlightOverlay.setAttribute('data-highlight-active', 'true');
    this.highlightOverlay.setAttribute('data-highlight-tile-id', tileId);
    (window as unknown as { __lastHighlightedTile?: string }).__lastHighlightedTile = tileId;
    (window as unknown as { __highlightTimestampMs?: number }).__highlightTimestampMs = tsMs;
    // Phase K Wave 8 — Re-dispatch as `tile-highlight` (without the
    // `mahjong:` prefix) for the Vasquez latency-sniffer spec, which
    // listens for this event name as the canonical "highlight has
    // landed on the board" signal.  Different from the originating
    // `mahjong:highlight-tile` event (which is the chip→view
    // request); this is the view→assertion-harness completion
    // confirmation.
    window.dispatchEvent(
      new CustomEvent<{ tileId: string; timestamp: number }>('tile-highlight', {
        detail: { tileId, timestamp: tsMs },
      }),
    );
    this.highlightTimer = window.setTimeout(() => {
      if (this.highlightOverlay !== null) {
        this.highlightOverlay.removeAttribute('data-highlight-active');
        this.highlightOverlay.removeAttribute('data-highlight-tile-id');
      }
      this.main.removeAttribute('data-highlight-tile-id');
      this.highlightTimer = null;
    }, MainView.HIGHLIGHT_PULSE_MS);
  }

  private setupLights(): void {
    const white = 0xffffff;
    this.viewGroup.add(new AmbientLight(white, 1.45));
    const topLight = new DirectionalLight(white, 1.45);
    topLight.position.set(0, 0, 10000);
    this.viewGroup.add(topLight);

    const frontLight = new DirectionalLight(white, 0.45);
    frontLight.position.set(0, -10000, 0);
    this.viewGroup.add(frontLight);

    const sideLight = new DirectionalLight(white, 0.45);
    sideLight.position.set(-10000, -10000, 0);
    this.viewGroup.add(sideLight);
  }

  private setupRendering(): void {
    // Wave 7 — w/h previously sized OutlinePass's offscreen FBO.
    // The hull-expansion outline has no FBO so no resize is
    // needed; the values are kept for future spike work that
    // might want a screen-space stroke.
    // const w = this.renderer.domElement.clientWidth;
    // const h = this.renderer.domElement.clientHeight;

    if (this.camera !== null) {
      this.scene.remove(this.camera);
    }

    this.camera = this.makeCamera(this.perspective);
    this.viewGroup.add(this.camera);
    this.outline.setEdgeColor(0xffff99);

    // Phase K Wave 7 — Pre-warm the outline shader so the first
    // tile selection does not stutter on shader compile.  The W6
    // code did this by pushing a dummy object onto OutlinePass +
    // running one composer.render(); the hull helper exposes the
    // same one-shot warm via `precompile`.
    this.outline.precompile(this.dummyObject as Mesh, () => {
      this.renderer.render(this.scene, this.camera);
    });
  }

  private makeCamera(perspective: boolean): Camera {
    if (perspective) {
      const camera = new PerspectiveCamera(30, RATIO, 0.1, 1000);
      return camera;
    } else {
      const w = World.WIDTH * 1.2;
      const h = w / RATIO;
      const camera = new OrthographicCamera(
        -w / 2, w / 2,
        h / 2, -h / 2,
        0.1, 1000);
      return camera;
    }
  }

  updateCamera(seat: number | null, lookDown: number, zoom: number, mouse2: Vector2 | null): void {
    if (this.perspective) {
      this.updatePespectiveCamera(seat === null, lookDown, zoom, mouse2);
    } else {
      this.updateOrthographicCamera(seat === null, lookDown, zoom, mouse2);
    }

    const angle = (seat ?? 0) * Math.PI * 0.5;
    this.viewGroup.rotation.set(0, 0, angle);
    this.viewGroup.updateMatrixWorld();
  }

  private updatePespectiveCamera(
    fromTop: boolean,
    lookDown: number,
    zoom: number,
    mouse2: Vector2 | null): void
  {
    if (fromTop) {
      this.camera.position.set(0, 0, 400);
      this.camera.rotation.set(0, 0, 0);
    } else {
      this.camera.position.set(0, -World.WIDTH*1.44, World.WIDTH * 1.05);
      this.camera.rotation.set(Math.PI * 0.3 - lookDown * 0.2, 0, 0);
      if (zoom !== 0) {
        const dist = new Vector3(0, 1.37, -1).multiplyScalar(zoom * 55);
        this.camera.position.add(dist);
      }
      if (zoom > 0 && mouse2) {
        // NOTE: with multiplier larger than 0.5 it's possible to look at left
        // or right player's tiles!
        this.camera.position.x += mouse2.x * zoom * World.WIDTH * 0.5;
        this.camera.position.y += mouse2.y * zoom * World.WIDTH * 0.5;
      }
    }
  }

  private updateOrthographicCamera(
    fromTop: boolean,
    lookDown: number,
    zoom: number,
    mouse2: Vector2 | null): void
  {
    if (fromTop) {
      this.camera.position.set(0, 0, 100);
      this.camera.rotation.set(0, 0, 0);
      this.camera.scale.setScalar(1.55);
    } else {
      this.camera.position.set(
        0,
        -53 * lookDown - World.WIDTH,
        174);
      this.camera.rotation.set(Math.PI * 0.25, 0, 0);
      this.camera.scale.setScalar(1 - 0.45 * zoom);

      if (zoom > 0 && mouse2) {
        this.camera.position.x += mouse2.x * zoom * World.WIDTH * 0.6;
        this.camera.position.y += mouse2.y * zoom * World.WIDTH * 0.6;
      }
    }
  }

  updateOutline(selectedObjects: Array<Mesh>): void {
    this.outline.setSelected(selectedObjects);
  }

  setPerspective(perspective: boolean): void {
    this.perspective = perspective;
    this.setupRendering();
  }

  render(): void {
    // Phase K Wave 7 — Direct single-pass render.  EffectComposer +
    // OutlinePass + RenderPass are gone — see ./render/custom-outline.
    this.renderer.render(this.scene, this.camera);
    this.stats?.update();
  }

  updateViewport(): void {
    if (this.main.parentElement!.clientWidth !== this.width ||
      this.main.parentElement!.clientHeight !== this.height) {

      this.width = this.main.parentElement!.clientWidth;
      this.height = this.main.parentElement!.clientHeight;

      let renderWidth: number, renderHeight: number;

      if (this.width / this.height > RATIO) {
        renderWidth = Math.floor(this.height * RATIO);
        renderHeight = Math.floor(this.height);
      } else {
        renderWidth = Math.floor(this.width);
        renderHeight = Math.floor(this.width / RATIO);
      }
      renderWidth -= renderWidth % 2;
      renderHeight -= renderHeight % 2;

      const pixelRatio = Math.min(window.devicePixelRatio, 3);

      this.main.style.width = `${renderWidth}px`;
      this.main.style.height = `${renderHeight}px`;
      this.renderer.setSize(renderWidth, renderHeight);
      this.renderer.setPixelRatio(pixelRatio);
      // Phase K Wave 7 — The composer (and its FBO) is gone; only
      // the WebGLRenderer needs resizing.
    }
  }
}
