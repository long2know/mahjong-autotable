import { Scene, Camera, WebGLRenderer, Vector2, Vector3, Group, AmbientLight, DirectionalLight, PerspectiveCamera, OrthographicCamera, Mesh, Object3D, PlaneGeometry } from 'three';

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
