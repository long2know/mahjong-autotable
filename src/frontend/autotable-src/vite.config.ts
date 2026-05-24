// Phase K Wave 7 — Vite configuration (replaces Parcel as the
// production bundler).
//
// Why we swapped (full rationale: docs/frontend-build-tooling.md):
//
//   Parcel's namespace-re-export tree-shaker bottoms out at
//   ~740 kB on the `three-renderer` chunk because three.js
//   exposes its full core via `export { ... } from './three.core.js'`
//   and parcel can't follow that under sideEffects: true.  Wave 6
//   tried every modest tree-shake angle (wildcard import retired,
//   GLTFLoader peeled, Stats gated behind a query flag); the
//   chunk did not budge.  Wave 7 swaps to Vite (rollup under the
//   hood) which DOES tree-shake the re-export — combined with the
//   custom-outline replacement (W7 deliverable #4) this takes the
//   chunk under the 550 kB W7 ceiling.
//
// Parcel is kept available behind `npm run build:parcel` for one
// wave as a fallback in case a deploy regression surfaces.
//
// Chunking strategy mirrors what Parcel emitted (so the SW
// pre-cache manifest regex set in
// `scripts/generate-sw-manifest.js` continues to match):
//
//   • `autotable-src.<hash>.js`  — eager lobby (index.ts).
//   • `game-bootstrap.<hash>.js` — game URL boot path.
//   • `scene-shell.<hash>.js`    — thin three.js-free coordinator.
//   • `three-renderer.<hash>.js` — three.js + AssetLoader + Game.
//   • `scene-effects.<hash>.js`  — GameUi + MoveLog graph.
//   • `commentary-panel.<hash>.js`     — replay commentary.
//   • `spectator-livestream.<hash>.js` — spectator viewer.
//   • `hls.<hash>.js`            — vendored HLS.js (split out).
//   • `tournaments.<hash>.js`    — bracket renderer (Swiss + DE).
//   • Lazy chat / voice / audit / history / tour modules — each
//     get their own chunk named after the source module.

import { defineConfig } from 'vite';
import { resolve } from 'node:path';
import { spawn } from 'node:child_process';

// ── Asset filename normalisation ──────────────────────────────────
//
// Parcel emitted assets as `{name}.{hash}.{ext}` (8-char hex) at
// the root of `--dist-dir`.  The SW pre-cache regex set + the
// prune pass both lean on this layout, so we mirror it exactly.
// Rollup 4 exposes `hashCharacters: 'hex'` so the hash chars are
// the same character class.
const ASSET_PATTERN = '[name].[hash:8].[ext]';

// ── manualChunks: explicit chunk-split groups ─────────────────────
//
// rollup's default chunker would happily emit one bag per ESM
// graph entry; we want named chunks so the SW manifest can match
// content-hashed filenames.  Each entry returns a chunk name that
// becomes part of the emitted filename.
//
// Wave 7 deliberately does NOT route source files (other than the
// implicit three-renderer.ts dynamic-import boundary) into
// manualChunks: forcing src/world.ts, src/types.ts, etc. into
// `three-renderer` here causes rollup to make every chunk that
// statically references `client` / `types` (game-bootstrap,
// chat.ts, voice.ts, scene-effects) depend on three-renderer at
// import time, which defeats the W5 lazy-renderer split.  Rollup
// follows the dynamic-import boundary at `scene-shell -> ./three-
// renderer` naturally; the source files reachable only through
// that boundary collapse into the renderer chunk on their own.
function manualChunks(id: string): string | undefined {
  if (id.includes('node_modules/hls.js/')) return 'hls';
  if (id.includes('node_modules/@sentry/')) return 'sentry';
  // Phase K Wave 15 — Phase L renderer-webgl2 spike chunk.  Routes
  // every file under `src/renderer-webgl2/` into its own chunk so
  // the W15 baseline measurement (and every Phase L wave-by-wave
  // expansion) lands in `dist-size.json` as a discrete number.
  // The chunk only ships when `?renderer=webgl2-hello` is on the
  // URL — `src/index.ts` guards the dynamic import.
  if (/[/\\]src[/\\]renderer-webgl2[/\\]/.test(id)) return 'renderer-webgl2';
  // Phase K Wave 18 — Hicks (Frontend).  Admin operator panel
  // chunk.  Routes every file under `src/admin/` into a single
  // `admin-panel.<hash>.js` so the W18 list+form pair for
  // Bishop's three W17 CRUD surfaces (replay retention, JWKS
  // rotation, SignalR retention) ships as ONE measurable chunk.
  // Loaded lazily by `action-router.ts:dispatchAdminPanel()` on
  // `?action=admin-panel`; the lobby cold path never pays for
  // the chunk.  W18 ceiling: ≤ 40 KB.  See
  // `docs/frontend-bundle-audit.md §3.4 (admin chunk budget)`.
  //
  // Phase K Wave 22 — Hicks (Frontend).  Split the W18 chunk in
  // half: the `admin-panel-core` chunk carries the entry +
  // shared scaffolding + W18 baseline-CRUD policy surfaces
  // (replay retention, JWKS rotation, SignalR retention,
  // rotation-policy family, JWT rotation drill).  The
  // `admin-panel-tournaments` chunk carries every swiss/
  // tournament surface plus W19+ audit-log surfaces, SignalR
  // operational triggers, replay-chunked-download, the W22
  // cross-cutting audit-log browser, and the W22 JWT emergency-
  // revoke trapdoor.  Each chunk targets ≤ 30 KB at W22 close;
  // see `docs/frontend-bundle-audit.md §3.7 (admin chunk split)`.
  if (/[/\\]src[/\\]admin[/\\](?:swiss-|tournament-|replay-integrity-audit|replay-restoration-audit|replay-download-chunked|signalr-purge|signalr-diagnostics|audit-log-search|jwt-emergency-revoke|admin-tournaments)/.test(id)) {
    return 'admin-panel-tournaments';
  }
  if (/[/\\]src[/\\]admin[/\\]/.test(id)) return 'admin-panel-core';
  // Phase K Wave 8 — Peel GLTFLoader (+ its KTX2/Draco/meshopt
  // extension paths) into its own chunk.  In W7 the dynamic-import
  // landed inside the renderer chunk anyway because Rollup's
  // chunker collapsed `import('three/examples/jsm/loaders/GLTFLoader.js')`
  // back into `three-renderer` (same `node_modules/three/`
  // origin).  Splitting it out drops ~40 kB off the heavy chunk
  // and lets the asset-loader download it in parallel with the
  // texture fetches; net first-paint cost is unchanged because
  // `AssetLoader.loadAll()` already awaits both in parallel.
  if (id.includes('node_modules/three/examples/jsm/loaders/GLTFLoader')) return 'gltf-loader';
  // Three.js + its examples/jsm addons live in the renderer chunk
  // (named explicitly so `chunkFileNames` doesn't fall back to
  // 'three' / 'index' bag names).  Note: we do NOT add src/* here
  // — see comment above.
  if (id.includes('node_modules/three/')) return 'three-renderer';
  return undefined;
}

// ── chunkFileNames: stable hex-hashed names ───────────────────────
//
// Vite's default chunk filename uses the chunk's `name` field +
// rollup's hash.  Most of our chunks already get a clean name
// (matching the source module's basename: `commentary-panel`,
// `spectator-livestream`, `game-bootstrap`, ...).  The only
// chunks that need help are anonymous shared bags rollup invents
// for cross-graph deduplication — those get a `chunk-{hash}`
// fallback.
function chunkFileNamesFn(chunkInfo: { name?: string; facadeModuleId?: string | null }): string {
  let name = chunkInfo.name || 'chunk';
  // `index` shows up whenever a module is named `index.{ts,js}` —
  // most often when rollup walks into a npm package's barrel.  Use
  // the first non-`node_modules` ancestor in facadeModuleId to
  // disambiguate.
  if (name === 'index') {
    const id = chunkInfo.facadeModuleId ?? '';
    if (id.includes('node_modules/@microsoft/signalr/')) name = 'signalr';
    else if (id.includes('node_modules/@sentry/')) name = 'sentry';
    else name = 'chunk';
  }
  return `${name}.[hash:8].js`;
}

// ── Static-copy plugin: copy `sw.js`, `sound/`, etc. ─────────────
//
// Vite's `publicDir` is the conventional spot for unprocessed
// static files.  We don't have a `public/` folder — the autotable-
// upstream layout keeps `sw.js`, `sound/`, `manifest.webmanifest`
// at the source root next to `index.html`.  A tiny plugin grabs
// them via the `closeBundle` hook and copies them into `dist/`.
//
// The post-build `scripts/generate-sw-manifest.js` step then walks
// the dist folder, prunes stale hashes, and emits
// `manifest-precache.json` (Wave 3 contract — unchanged).

import { copyFileSync, existsSync, mkdirSync, readdirSync, statSync } from 'node:fs';

function copyRecursive(srcDir: string, destDir: string): void {
  if (!existsSync(srcDir)) return;
  if (!existsSync(destDir)) mkdirSync(destDir, { recursive: true });
  for (const entry of readdirSync(srcDir)) {
    const src = `${srcDir}/${entry}`;
    const dest = `${destDir}/${entry}`;
    const s = statSync(src);
    if (s.isDirectory()) {
      copyRecursive(src, dest);
    } else {
      copyFileSync(src, dest);
    }
  }
}

function copyStaticAssets(): {
  name: string;
  closeBundle: () => void;
} {
  return {
    name: 'autotable-static-copy',
    closeBundle(): void {
      const root = __dirname;
      const out = resolve(root, '..', 'autotable');
      // Service worker (referenced as a string literal by pwa.ts).
      if (existsSync(`${root}/sw.js`)) copyFileSync(`${root}/sw.js`, `${out}/sw.js`);
      // Web manifest (referenced from index.html as a `<link rel="manifest">`;
      // vite's HTML plugin processes it but resolves the URL relative to root
      // — copying the source preserves any meta updates that bypass the bundle).
      if (existsSync(`${root}/manifest.webmanifest`)) {
        copyFileSync(`${root}/manifest.webmanifest`, `${out}/manifest.webmanifest`);
      }
      // about.html — secondary entry, not part of the JS graph.
      if (existsSync(`${root}/about.html`)) copyFileSync(`${root}/about.html`, `${out}/about.html`);
      // sound/ — preloaded audio assets referenced via `<audio src="./sound/…">`.
      copyRecursive(`${root}/sound`, `${out}/sound`);
      // img/ — most images are bundled via asset-loader imports
      // (hashed, emitted at dist root via Rollup's asset pipeline).
      // Two assets are referenced from inline HTML markup
      // (index.html / about.html) and need a static copy at the
      // canonical path:
      //   • img/dice.auto.png       (index.html dice button)
      //   • img/about/*.{png,mp4}   (about.html tutorial assets)
      // Copy only those subsets — copying the whole img/ tree
      // duplicates ~2.4 MB of source assets (icons, svgs, the
      // Blender source file) for no functional reason.
      const aboutSrc = `${root}/img/about`;
      const aboutDst = `${out}/img/about`;
      if (existsSync(aboutSrc)) {
        copyRecursive(aboutSrc, aboutDst);
      }
      const diceSrc = `${root}/img/dice.auto.png`;
      const diceDst = `${out}/img/dice.auto.png`;
      if (existsSync(diceSrc)) {
        if (!existsSync(`${out}/img`)) {
          require('node:fs').mkdirSync(`${out}/img`, { recursive: true });
        }
        copyFileSync(diceSrc, diceDst);
      }
      // Phase K Wave 17 — Phase L canonical tile atlas (Hicks).
      // `renderer-webgl2/tile-atlas.ts:TILE_ATLAS_URL_DEFAULT` points
      // at `/img/tiles-atlas-webgl2.auto.png` (3 × 34 cell grid,
      // 192 × 2176 px).  W17 generates the asset offline via
      // `scripts/generate-tile-atlas-webgl2.js` + commits it to
      // `img/`; this copy step lands it at the canonical URL the
      // runtime loader fetches.  Without the copy, the loader falls
      // back to the in-shader synthesized cell pattern (W16
      // behaviour preserved).  Re-run the generator script to
      // refresh the committed PNG when the cell palette changes.
      const tileAtlasSrc = `${root}/img/tiles-atlas-webgl2.auto.png`;
      const tileAtlasDst = `${out}/img/tiles-atlas-webgl2.auto.png`;
      if (existsSync(tileAtlasSrc)) {
        if (!existsSync(`${out}/img`)) {
          require('node:fs').mkdirSync(`${out}/img`, { recursive: true });
        }
        copyFileSync(tileAtlasSrc, tileAtlasDst);
      }
      // Phase K Wave 8 — Copy PWA icons to their un-hashed paths
      // under `img/` so the manifest's `src: "img/icon-NNN.auto.png"`
      // entries resolve.  Vite's HTML processor moves the icons to
      // the root with content-hashed names (which is what
      // `index.html` references), but the manifest is emitted as a
      // static copy and continues to point at the source paths —
      // without this copy step Lighthouse's `installable-manifest`
      // audit failed because every icon 404'd.
      const iconNames = [
        'icon-16.auto.png',
        'icon-32.auto.png',
        'icon-96.auto.png',
        'icon-192.auto.png',
        'icon-512.auto.png',
        'icon-maskable-512.auto.png',
      ];
      if (!existsSync(`${out}/img`)) {
        require('node:fs').mkdirSync(`${out}/img`, { recursive: true });
      }
      for (const name of iconNames) {
        const iconSrc = `${root}/img/${name}`;
        const iconDst = `${out}/img/${name}`;
        if (existsSync(iconSrc)) copyFileSync(iconSrc, iconDst);
      }
      // Phase K Wave 12 — W10 placeholder screenshot copy block removed.
      //
      // W10 shipped three mid-grey placeholder PNGs (`img/screenshot-
      // {lobby,table,mobile}.auto.png`).  W11 replaced those with real
      // Playwright captures at `screenshots/{main-game,
      // spectator-commentary,tournament-dashboard}.png` (copy block
      // immediately below).  The W11 manifest.webmanifest's
      // `screenshots[]` field references ONLY the real captures — no
      // entry in the live manifest points at the W10 placeholders.
      //
      // W12 retires the placeholder copy block + deletes the source
      // PNGs (see `git mv` history under `img/screenshot-*.auto.png`).
      // Only real screenshots ship in the bundle.  If a stale PWA
      // cache from a pre-W11 install ever requests the old path the
      // SW will surface a 404, which is the same behaviour the user
      // would get if they typed the URL manually — acceptable since
      // those URLs were never user-visible.
      // Phase K Wave 11 — Real PWA screenshots captured by
      // Playwright (see `scripts/capture-screenshots.js`).  These
      // live under `static/screenshots/` (committed) and are
      // copied to `dist/screenshots/` so the W11 manifest's
      // `screenshots[]` form_factor + label entries resolve.
      //
      // W12 retired the W10 placeholder copy block + deleted the
      // legacy `img/screenshot-*.auto.png` PNGs — only real
      // captures ship in the bundle (no safety net for stale
      // pre-W11 PWA cache entries; the manifest points solely at
      // `screenshots/*.png`).
      const realScreenshots = [
        'main-game.png',              // 1024×768, form_factor: wide
        'spectator-commentary.png',   // 768×1024, form_factor: narrow
        'tournament-dashboard.png',   // 1024×768, form_factor: wide
      ];
      const realScreenshotsSrc = `${root}/static/screenshots`;
      const realScreenshotsDst = `${out}/screenshots`;
      if (existsSync(realScreenshotsSrc)) {
        if (!existsSync(realScreenshotsDst)) {
          mkdirSync(realScreenshotsDst, { recursive: true });
        }
        for (const name of realScreenshots) {
          const src = `${realScreenshotsSrc}/${name}`;
          const dst = `${realScreenshotsDst}/${name}`;
          if (existsSync(src)) copyFileSync(src, dst);
        }
      }
    },
  };
}

// ── Phase K Wave 9 — three.core.js unused-material strip ────────
//
// `three.module.js` re-exports a dozen material classes the
// autotable doesn't use (`MeshPhongMaterial`,
// `MeshStandardMaterial`, `MeshPhysicalMaterial`,
// `MeshToonMaterial`, `MeshNormalMaterial`, `MeshDepthMaterial`,
// `MeshDistanceMaterial`, `MeshMatcapMaterial`, `PointsMaterial`,
// `SpriteMaterial`, `ShadowMaterial`, `LineDashedMaterial`,
// `RawShaderMaterial`).  We use only `MeshBasicMaterial`,
// `MeshLambertMaterial`, `LineBasicMaterial`, and `ShaderMaterial`.
//
// Rollup's tree-shaker normally would drop unused exports, but
// the W7→W8 measurements show it can't follow these — they're
// pulled in transitively by WebGLRenderer's shadow-map code path
// (`new MeshDepthMaterial(...)` / `new MeshDistanceMaterial(...)`
// at `three.module.js:8413-8414`) even when the autotable scene
// never enables shadow casting on any mesh.  So the *modules*
// stay live, even though the *instances* are never queried.
//
// W8 tried per-class deep-imports (`from 'three/src/materials/X'`)
// — they made the bundle GROW by 150 kB because each deep import
// re-pulled core init code.  See `docs/frontend-three-budget.md`
// for the autopsy.
//
// W9's approach: gut the unused class bodies in place.  Replace
// each `class X extends Material { /* ~100..500 lines of property
// defaults + copy() */ }` with a minimal 6-line stub that:
//
//   1. Sets the `isX = true` flag (shadow-map code path checks
//      this — see three.module.js:14018,14022,8720).
//   2. Sets `this.type = 'X'`.
//   3. Pre-initialises the small set of properties three's
//      internals read off the depth/distance material (see the
//      per-class `essential` map below).
//   4. Calls `super.setValues(parameters)` so `new MeshDepthMaterial(
//      {depthPacking: RGBADepthPacking})` (three.module.js:8413)
//      still works.
//   5. Implements `copy(source)` as a thin `super.copy(source)`
//      call (the shadow code never clones these, but tree-shaken
//      type definitions break if the method is absent).
//
// The transform runs only on the three.core.js file (matched by
// path), and is idempotent — re-running on a stubbed file is a
// no-op because the regex anchors require the original class
// header + brace block.
const STUB_MATERIALS: Record<string, string[]> = {
  // Properties touched by three.module.js's WebGLShadowMap code
  // and the gl-renderer material-of-program path.  Anything not
  // listed gets defaulted by the parent Material constructor.
  MeshDepthMaterial: [
    "this.depthPacking = 0",
    "this.map = null",
    "this.alphaMap = null",
    "this.displacementMap = null",
    "this.displacementScale = 1",
    "this.displacementBias = 0",
    "this.wireframe = false",
    "this.wireframeLinewidth = 1",
  ],
  MeshDistanceMaterial: [
    "this.map = null",
    "this.alphaMap = null",
    "this.displacementMap = null",
    "this.displacementScale = 1",
    "this.displacementBias = 0",
  ],
  // The remaining materials are NEVER instantiated by three's
  // internals — they exist only as user-facing exports.  We
  // could omit them and let Rollup drop them, but Rollup is
  // proven unable to (see preamble), so we stub them.
  MeshPhongMaterial: [],
  MeshStandardMaterial: [],
  MeshPhysicalMaterial: [],
  MeshToonMaterial: [],
  MeshNormalMaterial: [],
  MeshMatcapMaterial: [],
  PointsMaterial: [],
  SpriteMaterial: [],
  ShadowMaterial: [],
  LineDashedMaterial: [],
  RawShaderMaterial: [],
};

function buildStubBody(className: string, parentClass: string, props: string[]): string {
  const init = props.length === 0 ? '' : props.map(p => `\t\t${p};`).join('\n') + '\n';
  return `class ${className} extends ${parentClass} {
\tconstructor(parameters) {
\t\tsuper(${parentClass === 'ShaderMaterial' ? '' : ''});
\t\tthis.is${className} = true;
\t\tthis.type = '${className}';
${init}\t\tif (parameters !== undefined) this.setValues(parameters);
\t}
\tcopy(source) {
\t\tsuper.copy(source);
\t\treturn this;
\t}
}`;
}

function stripUnusedThreeMaterials(): { name: string; enforce: 'pre'; transform(code: string, id: string): { code: string; map: null } | null } {
  return {
    name: 'autotable-three-material-strip',
    enforce: 'pre',
    transform(code, id) {
      if (!id.includes('/three/build/three.core.js')) return null;

      // Map of className -> parent class (read from the actual
      // file).  We can't hard-code this because three's class
      // hierarchy occasionally changes (e.g. MeshPhysicalMaterial
      // extends MeshStandardMaterial, not Material).
      let out = code;
      let replaced = 0;
      const before = out.length;

      for (const className of Object.keys(STUB_MATERIALS)) {
        // Anchor: `class X extends Y { ... <until closing> }`
        // matched non-greedily.  Three's source has these as
        // top-level class declarations, one per file before
        // bundle, so the brace count is well-formed and we can
        // use a balanced-brace walker rather than relying on
        // regex alone.
        const headerRe = new RegExp(`^class\\s+${className}\\s+extends\\s+(\\w+)\\s*\\{`, 'm');
        const m = headerRe.exec(out);
        if (m === null) continue;

        const parent = m[1];
        const startIdx = m.index;
        const headerEnd = m.index + m[0].length;

        // Walk forward tracking depth from the opening brace.
        let depth = 1;
        let i = headerEnd;
        while (i < out.length && depth > 0) {
          const ch = out[i];
          if (ch === '{') depth++;
          else if (ch === '}') depth--;
          else if (ch === '/' && out[i + 1] === '/') {
            // Skip line comment
            const eol = out.indexOf('\n', i);
            i = eol === -1 ? out.length : eol;
            continue;
          } else if (ch === '/' && out[i + 1] === '*') {
            // Skip block comment
            const close = out.indexOf('*/', i + 2);
            i = close === -1 ? out.length : close + 2;
            continue;
          } else if (ch === '"' || ch === "'" || ch === '`') {
            // Skip string literal (handles escapes).
            const quote = ch;
            i++;
            while (i < out.length && out[i] !== quote) {
              if (out[i] === '\\') i++;
              i++;
            }
          }
          i++;
        }
        if (depth !== 0) {
          console.warn(`[material-strip] could not find matching brace for ${className}; skipping`);
          continue;
        }
        const endIdx = i;
        const stub = buildStubBody(className, parent, STUB_MATERIALS[className]);
        out = out.slice(0, startIdx) + stub + out.slice(endIdx);
        replaced++;
      }

      if (replaced === 0) return null;
      const after = out.length;
      console.log(`[material-strip] ${id.includes('node_modules') ? id.split('node_modules/').pop() : id} — stubbed ${replaced} classes, saved ${(before - after).toLocaleString()} chars (${before.toLocaleString()} → ${after.toLocaleString()})`);
      return { code: out, map: null };
    },
  };
}

// ── Phase K Wave 9 — three.module.js feature strip ────────────────
//
// Beyond the unused-material strip on three.core.js, three.module.js
// itself carries large feature modules the autotable never exercises:
//
//   • WebGLShadowMap (~11 KB unminified) — shadow casting is never
//     enabled (no `renderer.shadowMap.enabled = true`, no
//     `mesh.castShadow = true` in the scene).
//   • WebXRManager (~27 KB unminified) — no VR/AR mode.
//   • WebXRDepthSensing (~2 KB unminified) — only used during AR.
//
// We replace each function/class body with a no-op stub that
// satisfies the surface the WebGLRenderer touches.  Stub
// requirements are documented per case in the table below.
const MODULE_STUBS: Record<string, { kind: 'function' | 'class'; parent?: string; body: string }> = {
  WebGLShadowMap: {
    kind: 'function',
    body: `\tconst scope = this;
\tthis.enabled = false;
\tthis.autoUpdate = true;
\tthis.needsUpdate = false;
\tthis.type = 1;
\tthis.render = function () {};
`,
  },
  WebXRManager: {
    kind: 'class',
    parent: 'EventDispatcher',
    body: `\tconstructor() {
\t\tsuper();
\t\tthis.enabled = false;
\t\tthis.isPresenting = false;
\t\tthis.cameraAutoUpdate = true;
\t}
\tsetAnimationLoop() {}
\thasDepthSensing() { return false; }
\tgetEnvironmentBlendMode() { return 'opaque'; }
\tgetDepthSensingMesh() { return null; }
\tdispose() {}
`,
  },
  WebXRDepthSensing: {
    kind: 'class',
    body: `\tconstructor() {
\t\tthis.texture = null;
\t\tthis.mesh = null;
\t\tthis.depthNear = 0;
\t\tthis.depthFar = 0;
\t}
\tgetMesh() { return null; }
\treset() {}
\tonBeforeRender() {}
\tinit() {}
\trender() {}
`,
  },
  // Phase K Wave 10 — PMREMGenerator strip.
  //
  // PMREMGenerator (Prefiltered, Mipmapped Radiance Environment Map
  // Generator) is the texture pipeline three.js uses to pre-process
  // cube + equirectangular environment maps into a cubeUV layout the
  // PBR materials sample from.  It's referenced by
  // `WebGLCubeUVMaps#get()` (three.module.js:~3631) inside an
  // `if (isEquirectMap || isCubeMap)` branch — the autotable scene
  // uses ONLY plain 2D textures (`tiles.auto.png`, `sticks.auto.png`,
  // `center.auto.png`, `winds.auto.png`) so the branch is dead at
  // runtime.  Rollup cannot drop the class because the call
  // `new PMREMGenerator(renderer)` is statically reachable from
  // `WebGLCubeUVMaps`.
  //
  // The stub keeps the class declaration (so `new PMREMGenerator(...)`
  // doesn't throw if the runtime branch ever evaluates true) but
  // empties the body — every public method returns null / no-ops.
  // The seven private helper functions (`_createPlanes`,
  // `_createRenderTarget`, `_setViewport`, `_getBlurShader`,
  // `_getEquirectMaterial`, `_getCubemapMaterial`,
  // `_getCommonVertexShader`) become unreferenced once the class
  // body is gutted; Rollup's tree-shake (with our
  // `moduleSideEffects: id => !id.includes('three/')` config) drops
  // them naturally, along with the ~250-line glsl shader strings
  // they embed.
  //
  // Risk: if the autotable scene ever introduces an env map
  // (`scene.environment = new CubeTexture(...)`) or a material with
  // `envMap` set, the stub's no-op `fromCubemap` / `fromEquirectangular`
  // will silently return null and the renderer will skip the env
  // sample.  A scene-graph audit (W10) confirms no envMap usage; the
  // W10 retro should re-check before adding any PBR materials.
  //
  // See `docs/frontend-three-budget.md §6` for the autopsy.
  PMREMGenerator: {
    kind: 'class',
    body: `\tconstructor() {
\t\tthis._renderer = null;
\t\tthis._pingPongRenderTarget = null;
\t\tthis._lodMax = 0;
\t\tthis._cubeSize = 0;
\t\tthis._lodPlanes = [];
\t\tthis._sizeLods = [];
\t\tthis._sigmas = [];
\t\tthis._blurMaterial = null;
\t\tthis._cubemapMaterial = null;
\t\tthis._equirectMaterial = null;
\t}
\tfromScene() { return null; }
\tfromEquirectangular() { return null; }
\tfromCubemap() { return null; }
\tcompileCubemapShader() {}
\tcompileEquirectangularShader() {}
\tdispose() {}
`,
  },
  // Phase K Wave 10 — PMREMGenerator helper functions.
  //
  // After the PMREMGenerator class body is stubbed above, the seven
  // private helpers below are unreferenced.  Rollup's tree-shake
  // *would* drop them in theory, but the `_getBlurShader` /
  // `_getEquirectMaterial` / `_getCubemapMaterial` bodies each
  // embed ~3-4 kB of GLSL shader strings as template literals,
  // and Rollup's tree-shake keeps them because the helpers sit
  // alongside the cube_uv shader chunk's exported barrel
  // (`ShaderChunk.cube_uv_reflection_fragment` at line 603 of
  // three.module.js) which the bundler conservatively retains.
  //
  // Stubbing them explicitly is what actually drops the shader
  // strings from the renderer chunk.  Each helper returned a
  // ShaderMaterial or BufferGeometry; the stubs return null/empty
  // placeholders.  These helpers are only ever called from inside
  // the now-stubbed PMREMGenerator class, so the no-op returns
  // are unreachable at runtime.
  _getBlurShader: {
    kind: 'function',
    body: '\treturn null;\n',
  },
  _getEquirectMaterial: {
    kind: 'function',
    body: '\treturn null;\n',
  },
  _getCubemapMaterial: {
    kind: 'function',
    body: '\treturn null;\n',
  },
  _getCommonVertexShader: {
    kind: 'function',
    body: '\treturn "";\n',
  },
  _createPlanes: {
    kind: 'function',
    body: '\treturn { lodPlanes: [], sizeLods: [], sigmas: [] };\n',
  },
  _createRenderTarget: {
    kind: 'function',
    body: '\treturn null;\n',
  },
  _setViewport: {
    kind: 'function',
    body: '\n',
  },
};

function stripModuleFeatures(): { name: string; enforce: 'pre'; transform(code: string, id: string): { code: string; map: null } | null } {
  return {
    name: 'autotable-three-module-strip',
    enforce: 'pre',
    transform(code, id) {
      if (!id.includes('/three/build/three.module.js')) return null;
      const before = code.length;
      let out = code;
      let replaced = 0;
      for (const [name, spec] of Object.entries(MODULE_STUBS)) {
        const header = spec.kind === 'function'
          ? new RegExp(`^function\\s+${name}\\s*\\(([^)]*)\\)\\s*\\{`, 'm')
          : new RegExp(`^class\\s+${name}\\s*(?:extends\\s+\\w+\\s*)?\\{`, 'm');
        const m = header.exec(out);
        if (m === null) continue;
        // Find the closing brace using depth walking from the opening '{'.
        const headerStart = m.index;
        const openBrace = m.index + m[0].length - 1;
        let depth = 1;
        let i = openBrace + 1;
        while (i < out.length && depth > 0) {
          const ch = out[i];
          if (ch === '{') depth++;
          else if (ch === '}') depth--;
          else if (ch === '/' && out[i + 1] === '/') {
            const eol = out.indexOf('\n', i);
            i = eol === -1 ? out.length : eol;
            continue;
          } else if (ch === '/' && out[i + 1] === '*') {
            const close = out.indexOf('*/', i + 2);
            i = close === -1 ? out.length : close + 2;
            continue;
          } else if (ch === '"' || ch === "'" || ch === '`') {
            const quote = ch;
            i++;
            while (i < out.length && out[i] !== quote) {
              if (out[i] === '\\') i++;
              i++;
            }
          }
          i++;
        }
        if (depth !== 0) {
          console.warn(`[module-strip] could not find matching brace for ${name}; skipping`);
          continue;
        }
        const endIdx = i;
        const args = spec.kind === 'function' ? (m[1] || '') : '';
        const stub = spec.kind === 'function'
          ? `function ${name}(${args}) {\n${spec.body}}`
          : `class ${name}${spec.parent ? ` extends ${spec.parent}` : ''} {\n${spec.body}}`;
        out = out.slice(0, headerStart) + stub + out.slice(endIdx);
        replaced++;
      }
      if (replaced === 0) return null;
      console.log(`[module-strip] ${id.split('node_modules/').pop()} — stubbed ${replaced} feature(s), saved ${(before - out.length).toLocaleString()} chars (${before.toLocaleString()} → ${out.length.toLocaleString()})`);
      return { code: out, map: null };
    },
  };
}

function stripWebGLShadowMap(): { name: string; enforce: 'pre'; transform(code: string, id: string): { code: string; map: null } | null } {
  // Phase K Wave 9 — Retained as a thin alias for the unified
  // `stripModuleFeatures` plugin (which stubs WebGLShadowMap +
  // WebXRManager + WebXRDepthSensing in one transform pass).
  return stripModuleFeatures();
}

// ── Phase K Wave 11 — ShaderChunk barrel surgery ────────────────
//
// W10 left the heavy renderer chunk at 497.44 kB, missing the
// <480 kB stretch ceiling by ~17 kB.  The autopsy in
// `docs/frontend-three-budget.md §6` traced the remaining bulk to
// the GLSL shader-string consts inside three.module.js — they're
// re-exported through the `ShaderChunk` / `ShaderLib` barrel and
// Rollup keeps the entire barrel as a single unit (it can't drop
// individual properties from a named-export object literal).
//
// W11 takes a surgical approach: walk the source of three.module.js
// in the Vite `transform()` hook, identify the shader-string consts
// keyed off the ShaderLib registry, and replace the unused ones
// with empty strings.  The barrel re-export stays intact (so any
// runtime lookup like `ShaderLib.meshphysical_frag` still resolves);
// the *bodies* (each 1-4 kB of GLSL) drop out of the emitted chunk.
//
// Scene-graph audit (re-confirmed W11): the autotable uses
//   • `MeshBasicMaterial` (object-view.ts)
//   • `MeshLambertMaterial` (asset-loader, center, thing-group)
//   • `LineBasicMaterial` (selection-box, mouse-ui) — shares
//     "basic" ShaderLib entry with MeshBasicMaterial
//   • `ShaderMaterial` (scene-effects' CustomOutline shader)
// — every other material class is either stubbed (W9) or unused.
//
// Three's standard ShaderLib name → const-suffix mapping
// (line 660-720 of three.module.js, also documented in three.js's
// `src/renderers/shaders/ShaderLib.js`):
//
//   meshbasic    → vertex$a, fragment$a   KEEP
//   meshlambert  → vertex$9, fragment$9   KEEP
//   background   → vertex$h, fragment$h   strip (no scene.background)
//   backgroundCube → vertex$g, fragment$g strip (no cube/equirect bg)
//   cube         → vertex$f, fragment$f   strip (no sky cube shader)
//   depth        → vertex$e, fragment$e   strip (W9 shadow strip)
//   distanceRGBA → vertex$d, fragment$d   strip (W9 shadow strip)
//   equirect     → vertex$c, fragment$c   strip (no equirect maps)
//   linedashed   → vertex$b, fragment$b   strip (no dashed lines)
//   meshmatcap   → vertex$8, fragment$8   strip (W9 matcap stub)
//   meshnormal   → vertex$7, fragment$7   strip (W9 normal stub)
//   meshphong    → vertex$6, fragment$6   strip (W9 phong stub)
//   meshphysical → vertex$5, fragment$5   strip (W9 PBR stub) — BIGGEST
//   meshtoon     → vertex$4, fragment$4   strip (W9 toon stub)
//   points       → vertex$3, fragment$3   strip (W9 points stub)
//   shadow       → vertex$2, fragment$2   strip (W9 shadow stub)
//   sprite       → vertex$1, fragment$1   strip (W9 sprite stub)
//
// Plus the `cube_uv_reflection_fragment` ShaderChunk (line 344 of
// three.module.js, ~2.3 kB).  It's `#include`d by meshlambert_frag
// (and meshphysical_frag, but that's stripped) inside an
// `#ifdef ENVMAP_TYPE_CUBE_UV` guard.  Our scene never sets a
// cubeUV env map (asset-loader uses only 2D PNG textures), so the
// guard never evaluates true at GLSL compile time — the include
// resolves to an empty string and the GLSL preprocessor strips the
// guarded block.
//
// Standalone `vertex` / `fragment` at line ~8400 are the VSM shadow
// blur shader inside the W9-stubbed WebGLShadowMap; they're now
// unreachable but Rollup keeps them as named exports of the same
// scope.  Strip them too.
//
// Risk + back-out: if the strip causes WebGL compile errors at
// runtime (e.g. an unexpected scene introduces an envMap or sets
// scene.background), the symptom is a black canvas + a console
// "ERROR: 0:N: '...': syntax error" message.  Disable
// `stripUnusedShaderChunks()` from the `plugins:` array below to
// roll back.  The unstripped baseline lives at W10's 497.44 kB.
const SHADER_CHUNKS_TO_EMPTY = [
  // ShaderChunk barrel: unused chunks (mostly env-map related).
  //
  // W11 stripped `cube_uv_reflection_fragment` (~2.3 KB GLSL).  W12
  // extends the same surgical approach to:
  //
  //   • shadowmap_pars_fragment / _pars_vertex / _vertex /
  //     shadowmask_pars_fragment — every shadow-related ShaderChunk.
  //     The autotable never enables `renderer.shadowMap.enabled` nor
  //     sets `castShadow` / `receiveShadow` on any mesh, so the bulk
  //     of each chunk's body sits inside `#ifdef USE_SHADOWMAP` (which
  //     the GLSL preprocessor strips when the renderer never defines
  //     the macro).  `shadowmask_pars_fragment` defines
  //     `getShadowMask()` which is ONLY referenced from `shadow_frag`
  //     (W9-stripped ShadowMaterial shader) — safe to empty entirely.
  //   • envmap_* (6 chunks) — every envmap chunk's body sits inside
  //     `#ifdef USE_ENVMAP`.  The autotable uses only 2D textures
  //     (`tiles.auto.png`, `sticks.auto.png`, `center.auto.png`,
  //     `winds.auto.png`) — no `scene.environment`, no `material.envMap`
  //     — so USE_ENVMAP is never defined and the entire #ifdef block
  //     is stripped at GLSL compile time.  The chunk body in the JS
  //     bundle is therefore deadweight.
  //
  // Combined W12 saving: ~10-12 KB of GLSL strings off the renderer
  // chunk (uncompressed; ~3-4 KB after esbuild + gzip).  See
  // `docs/frontend-three-budget.md §8` for the full surgical recipe.
  //
  // Risk + back-out: identical to W11 — if a future scene introduces
  // `renderer.shadowMap.enabled = true` or `scene.environment = ...`,
  // GLSL compile will hit a syntax error on the empty include.  Roll
  // back by removing the offending name from this list.
  'cube_uv_reflection_fragment',
  'shadowmap_pars_fragment',
  'shadowmap_pars_vertex',
  'shadowmap_vertex',
  'shadowmask_pars_fragment',
  'envmap_fragment',
  'envmap_common_pars_fragment',
  'envmap_pars_fragment',
  'envmap_pars_vertex',
  'envmap_physical_pars_fragment',
  'envmap_vertex',
  // ── Phase K Wave 13 — PMREMGenerator deeper strip ──────────────
  //
  // W13 extends the strip to the remaining ShaderChunks that are
  // structurally guarded by `#ifdef USE_*` macros the autotable
  // scene never defines, plus the Phong/Toon/Physical material
  // chains (W9-stubbed classes) and the PBR-extras chunks
  // (transmission / iridescence / clearcoat).  Two further chunks
  // — `tonemapping_pars_fragment` + `tonemapping_fragment` — sit
  // under the `#if defined(TONE_MAPPING)` guard which is never
  // defined because the scene uses the default `NoToneMapping`.
  //
  // Each chunk's GLSL body was verified to be wrapped in an
  // `#ifdef USE_<MACRO>` (or equivalent) guard at the JS-string
  // level (see `tools/three-chunk-guards.mjs` audit + the
  // companion table in `docs/frontend-three-budget.md §9`).  The
  // GLSL preprocessor strips the include body when the macro is
  // never `#define`d by `WebGLProgram.getProgramDefines()`, so
  // emptying the JS-side string is equivalent to what the GLSL
  // compiler already does — except now the JS payload doesn't
  // ship the body bytes.
  //
  // The biggest contributors are `transmission_pars_fragment`
  // (~6 KB of GLSL, all behind `USE_TRANSMISSION`), the four
  // light/material pairs (`lights_phong_*`, `lights_toon_*`,
  // `lights_physical_*` — each pars-fragment is 1-5 KB), and
  // `tonemapping_pars_fragment` (~4 KB of tone-mapping function
  // definitions never called).  The smaller `*_pars_fragment`
  // chunks (alphamap, alphatest, alphahash, aomap, lightmap,
  // emissivemap, bumpmap, normalmap, specularmap, metalnessmap,
  // roughnessmap, displacementmap, fog, dithering,
  // premultiplied_alpha, clearcoat_*, iridescence_*) each carry
  // 50-1500 B of declarations / setup code, all under their
  // respective `USE_*` macros.
  //
  // `specularmap_fragment` is intentionally NOT in this list: it
  // contains the `#else` branch that sets `specularStrength =
  // 1.0;` which `lights_lambert_fragment` reads downstream.
  // Same for `opaque_fragment` (sets the final `gl_FragColor`)
  // and `colorspace_fragment` (one-liner output-colorspace
  // conversion, unguarded).
  //
  // Combined W13 saving: ~10-18 KB of GLSL strings off the
  // renderer chunk (uncompressed; ~3-6 KB after esbuild minify).
  // Target: drop big chunk from W12's 448.65 KB to <445 KB
  // acceptable / <440 KB stretch.
  //
  // Risk + back-out: each name is independently removable.  If a
  // future scene un-stubs a material that introduces one of the
  // guarded `USE_*` macros, the empty chunk yields no syntax
  // error (the include resolves to ""), but the corresponding
  // shader logic disappears.  Symptom = silently-wrong rendering
  // (e.g. transparent meshes with `transparent: true` rendering
  // opaque, or alphaTest meshes not discarding pixels).  Roll
  // back by removing the offending chunk from this list.
  // Tone-mapping group.
  'tonemapping_pars_fragment',
  'tonemapping_fragment',
  // MeshPhongMaterial (W9 class-stubbed) — lighting chain.
  'lights_phong_fragment',
  'lights_phong_pars_fragment',
  // MeshToonMaterial (W9 class-stubbed) — lighting chain.
  'lights_toon_fragment',
  'lights_toon_pars_fragment',
  // MeshPhysicalMaterial / MeshStandardMaterial (W9 class-stubbed)
  // — lighting chain (biggest single contributor after the bulk
  // `meshphysical_*` shader strings, which were W9-stripped).
  'lights_physical_fragment',
  'lights_physical_pars_fragment',
  // PBR-extras: transmission + iridescence + clearcoat (all
  // require `material.transmission` / `iridescence` / `clearcoat`
  // > 0 which the autotable materials never set; AND all are
  // reachable only via `fragment$5` / meshphysical_frag which is
  // W9-empty).
  'transmission_fragment',
  'transmission_pars_fragment',
  'iridescence_fragment',
  'iridescence_pars_fragment',
  'clearcoat_pars_fragment',
  'clearcoat_normal_fragment_begin',
  'clearcoat_normal_fragment_maps',
  // Map-extension chains the autotable scene never opts into.
  // Every chunk listed below is wrapped in `#ifdef USE_<MACRO>`
  // and the macro is only `#define`d by WebGLProgram when the
  // corresponding `material.<map>` property is set.  Autotable's
  // MeshBasic / MeshLambert / LineBasic materials never set
  // alphaMap / alphaHash / alphaTest / aoMap / lightMap /
  // emissiveMap / bumpMap / normalMap / specularMap /
  // metalnessMap / roughnessMap / displacementMap.
  'alphamap_fragment',
  'alphamap_pars_fragment',
  'alphahash_fragment',
  'alphahash_pars_fragment',
  'alphatest_fragment',
  'alphatest_pars_fragment',
  'aomap_fragment',
  'aomap_pars_fragment',
  'lightmap_pars_fragment',
  'emissivemap_fragment',
  'emissivemap_pars_fragment',
  'bumpmap_pars_fragment',
  'normalmap_pars_fragment',
  'specularmap_pars_fragment',
  'metalnessmap_fragment',
  'metalnessmap_pars_fragment',
  'roughnessmap_fragment',
  'roughnessmap_pars_fragment',
  'displacementmap_pars_vertex',
  'displacementmap_vertex',
  // Fog / dithering / premultiplied-alpha — none of these are
  // toggled by the autotable scene (no `scene.fog`, no
  // `renderer.dithering`, no `material.premultipliedAlpha`).
  'fog_fragment',
  'fog_pars_fragment',
  'fog_vertex',
  'fog_pars_vertex',
  'dithering_fragment',
  'dithering_pars_fragment',
  'premultiplied_alpha_fragment',
];

const SHADER_STRINGS_TO_EMPTY = [
  // Background / sky / env shaders — none of these are used.
  'vertex$h', 'fragment$h',     // background (non-cube)
  'vertex$g', 'fragment$g',     // backgroundCube
  'vertex$f', 'fragment$f',     // cube (sky)
  'vertex$e', 'fragment$e',     // depth (W9 shadow stub)
  'vertex$d', 'fragment$d',     // distanceRGBA (W9 shadow stub)
  'vertex$c', 'fragment$c',     // equirect
  'vertex$b', 'fragment$b',     // linedashed
  'vertex$8', 'fragment$8',     // meshmatcap
  'vertex$7', 'fragment$7',     // meshnormal
  'vertex$6', 'fragment$6',     // meshphong
  'vertex$5', 'fragment$5',     // meshphysical (PBR — biggest)
  'vertex$4', 'fragment$4',     // meshtoon
  'vertex$3', 'fragment$3',     // points
  'vertex$2', 'fragment$2',     // shadow
  'vertex$1', 'fragment$1',     // sprite
  // Standalone VSM blur shader pair (WebGLShadowMap is stubbed).
  // Plain `vertex`/`fragment` identifiers — handled separately.
];

function stripUnusedShaderChunks(): { name: string; enforce: 'pre'; transform(code: string, id: string): { code: string; map: null } | null } {
  return {
    name: 'autotable-three-shaderchunk-strip',
    enforce: 'pre',
    transform(code, id) {
      if (!id.includes('/three/build/three.module.js')) return null;

      const before = code.length;
      let out = code;
      let replaced = 0;

      // Empty each ShaderChunk constant (`var X = "..."`).
      for (const name of SHADER_CHUNKS_TO_EMPTY) {
        const re = new RegExp(`^var\\s+${name}\\s*=\\s*"([\\s\\S]*?)";`, 'm');
        const m = re.exec(out);
        if (m === null) continue;
        out = out.slice(0, m.index) + `var ${name} = "";` + out.slice(m.index + m[0].length);
        replaced++;
      }

      // Empty each unused fragment$X / vertex$X (`const X = "..."`).
      for (const name of SHADER_STRINGS_TO_EMPTY) {
        // `$` is a regex metachar but JS allows it raw in identifiers;
        // escape it for the regex while keeping it in the replacement.
        const esc = name.replace(/\$/g, '\\$');
        const re = new RegExp(`^const\\s+${esc}\\s*=\\s*"([\\s\\S]*?)";`, 'm');
        const m = re.exec(out);
        if (m === null) continue;
        out = out.slice(0, m.index) + `const ${name} = "";` + out.slice(m.index + m[0].length);
        replaced++;
      }

      // Standalone `const vertex` / `const fragment` pair (VSM blur,
      // inside the W9-stubbed WebGLShadowMap scope).  Anchored on
      // word-boundary so we don't accidentally match e.g.
      // `const vertexCount = ...` if a future three.js refactor adds
      // such a sibling.
      const standalonePairs: Array<{ name: 'vertex' | 'fragment' }> = [
        { name: 'vertex' }, { name: 'fragment' },
      ];
      for (const { name } of standalonePairs) {
        const re = new RegExp(`^const\\s+${name}\\s*=\\s*"([\\s\\S]*?)";`, 'm');
        const m = re.exec(out);
        if (m === null) continue;
        out = out.slice(0, m.index) + `const ${name} = "";` + out.slice(m.index + m[0].length);
        replaced++;
      }

      if (replaced === 0) return null;
      const after = out.length;
      console.log(`[shaderchunk-strip] ${id.split('node_modules/').pop()} — emptied ${replaced} shader string(s), saved ${(before - after).toLocaleString()} chars (${before.toLocaleString()} → ${after.toLocaleString()})`);
      return { code: out, map: null };
    },
  };
}


// ── Phase K Wave 12 — UniformsLib unused-entries strip ──────────
//
// `three.module.js:724` declares a `const UniformsLib = { ... }`
// registry that ShaderLib references via `mergeUniforms([...])` —
// each material's per-shader-program uniform set is composed from
// these top-level keys at build time.
//
// The autotable scene uses only:
//   • MeshBasicMaterial    → ShaderLib.basic    (UniformsLib: common,
//                            specularmap, envmap, aomap, lightmap, fog)
//   • MeshLambertMaterial  → ShaderLib.lambert  (same as basic plus
//                            emissivemap, bumpmap, normalmap,
//                            displacementmap, lights)
//   • LineBasicMaterial    → shares ShaderLib.basic
//   • ShaderMaterial       → CustomOutline (autotable's own shader)
//
// Five UniformsLib entries are referenced ONLY by ShaderLib material
// definitions whose material classes were W9-stubbed:
//
//   • UniformsLib.roughnessmap → ShaderLib.standard (W9 PBR stub)
//   • UniformsLib.metalnessmap → ShaderLib.standard (W9 PBR stub)
//   • UniformsLib.gradientmap  → ShaderLib.toon (W9 toon stub)
//   • UniformsLib.points       → ShaderLib.points (W9 Points stub)
//   • UniformsLib.sprite       → ShaderLib.sprite (W9 Sprite stub)
//
// Even though the stubbed materials never instantiate their
// ShaderLib entries at runtime, Rollup keeps the UniformsLib
// definitions live because ShaderLib still names them statically
// at module load.  We replace each entry's value with an empty
// object literal (the ShaderLib references stay intact — the keys
// remain enumerable, just yielding `{}` — and the runtime materials
// that would consume them are stubbed anyway).
//
// Combined W12 saving: ~0.5-1 KB after minification.  Modest, but
// the strip stays surgical and contained to the well-typed map
// below — no regex over the giant module file.
//
// Risk + back-out: if a future wave un-stubs PBR / toon / points /
// sprite, the un-stubbed ShaderLib programs will silently yield
// undefined uniforms.  Symptom: console warns from three.js about
// missing uniforms when the material is actually used.  Roll back
// by removing the offending key from this list.
const UNIFORMS_LIB_KEYS_TO_EMPTY = [
  'roughnessmap',
  'metalnessmap',
  'gradientmap',
  'points',
  'sprite',
  // ── Phase K Wave 13 — Map-feature UniformsLib entries ──────────
  //
  // Each of these UniformsLib top-level keys holds the uniform
  // values that ShaderLib.basic / .lambert reference for the
  // corresponding `material.<map>` feature.  Autotable's
  // MeshBasic / MeshLambert / LineBasic materials never set:
  //
  //   • specularMap → UniformsLib.specularmap
  //   • envMap      → UniformsLib.envmap (renderer chunk strip
  //                    already nukes the consuming shader code
  //                    via the W12 envmap_* ShaderChunk pass)
  //   • aoMap       → UniformsLib.aomap
  //   • lightMap    → UniformsLib.lightmap
  //   • bumpMap     → UniformsLib.bumpmap
  //   • normalMap   → UniformsLib.normalmap
  //   • displacementMap → UniformsLib.displacementmap
  //   • emissiveMap → UniformsLib.emissivemap
  //   • scene.fog   → UniformsLib.fog (we don't set scene.fog)
  //
  // ShaderLib.basic.uniforms = mergeUniforms([UniformsLib.common,
  //   UniformsLib.specularmap, UniformsLib.envmap, UniformsLib.aomap,
  //   UniformsLib.lightmap, UniformsLib.fog])
  // — emptying each value-object yields `{}` and the merge still
  // works (the result has the keys from `.common` plus nothing
  // from these), and three's uniform-binding machinery never
  // looks up these uniforms because the consuming `USE_<MACRO>`
  // shader code is `#define`-stripped at GLSL compile time
  // (no `material.<map>` set → no macro → no uniform reference).
  //
  // `UniformsLib.common` is KEPT — it holds the always-set
  // `diffuse` / `opacity` / `map` / `uv` uniforms that
  // MeshBasic + MeshLambert universally consume.
  // `UniformsLib.lights` is KEPT — the autotable scene attaches
  // AmbientLight + DirectionalLight, so the lighting uniforms are
  // live and `lights_pars_begin` consumes them via NUM_DIR_LIGHTS.
  //
  // Combined W13 saving: ~0.5-1.5 KB after minification.  Modest
  // but the strip stays surgical and contained.
  //
  // Risk + back-out: if a future wave un-stubs a material that
  // sets one of the listed maps (e.g. enabling normal mapping on
  // a tile), the un-stubbed shader would `#define USE_NORMALMAP`,
  // and the uniform `normalMap` / `normalMatrix` etc. would be
  // expected.  Symptom: console warns from three.js's uniform-
  // validation about missing values, then a black render where
  // the map should be sampled.  Roll back by removing the
  // offending key from this list.
  'specularmap',
  'envmap',
  'aomap',
  'lightmap',
  'bumpmap',
  'normalmap',
  'displacementmap',
  'emissivemap',
  'fog',
];

function stripUnusedUniformsLib(): { name: string; enforce: 'pre'; transform(code: string, id: string): { code: string; map: null } | null } {
  return {
    name: 'autotable-three-uniformslib-strip',
    enforce: 'pre',
    transform(code, id) {
      if (!id.includes('/three/build/three.module.js')) return null;

      const before = code.length;
      let out = code;
      let replaced = 0;

      for (const key of UNIFORMS_LIB_KEYS_TO_EMPTY) {
        // Match `<tab><key>: {` followed by depth-walked body close
        // `<tab>}` at the same indent.  The UniformsLib registry is
        // formatted with a single-tab indent per top-level entry, and
        // each entry's value is an object literal — we don't bother
        // with arbitrary expressions because we know the surface.
        const headerRe = new RegExp(`(\\n\\t)(${key})(:\\s*\\{)`);
        const m = headerRe.exec(out);
        if (m === null) continue;
        const openBraceIdx = m.index + m[0].length - 1;
        let depth = 1;
        let i = openBraceIdx + 1;
        while (i < out.length && depth > 0) {
          const ch = out[i];
          if (ch === '{') depth++;
          else if (ch === '}') depth--;
          else if (ch === '/' && out[i + 1] === '/') {
            const eol = out.indexOf('\n', i);
            i = eol === -1 ? out.length : eol;
            continue;
          } else if (ch === '/' && out[i + 1] === '*') {
            const close = out.indexOf('*/', i + 2);
            i = close === -1 ? out.length : close + 2;
            continue;
          } else if (ch === '"' || ch === "'" || ch === '`') {
            const quote = ch;
            i++;
            while (i < out.length && out[i] !== quote) {
              if (out[i] === '\\') i++;
              i++;
            }
          }
          i++;
        }
        if (depth !== 0) {
          console.warn(`[uniformslib-strip] could not find matching brace for ${key}; skipping`);
          continue;
        }
        out = out.slice(0, openBraceIdx) + `{}` + out.slice(i);
        replaced++;
      }

      if (replaced === 0) return null;
      const after = out.length;
      console.log(`[uniformslib-strip] ${id.split('node_modules/').pop()} — emptied ${replaced} UniformsLib entr(ies), saved ${(before - after).toLocaleString()} chars (${before.toLocaleString()} → ${after.toLocaleString()})`);
      return { code: out, map: null };
    },
  };
}


//
// Wave 3 ships `scripts/generate-sw-manifest.js` which:
//   • copies sw.js (we also do that above as a belt-and-braces),
//   • prunes stale content-hashed chunks,
//   • emits manifest-precache.json.
//
// We invoke it via spawn() from `closeBundle` so a `vite build`
// is a single command end-to-end (matches the existing
// `build:post` chain).
function runSwManifestScript(): { name: string; closeBundle: () => Promise<void> } {
  return {
    name: 'autotable-sw-manifest',
    closeBundle(): Promise<void> {
      return new Promise((resolveP, rejectP) => {
        const proc = spawn(
          process.execPath,
          [resolve(__dirname, 'scripts', 'generate-sw-manifest.js')],
          { stdio: 'inherit' }
        );
        proc.on('exit', code => (code === 0 ? resolveP() : rejectP(new Error(`sw-manifest exit ${code}`))));
      });
    },
  };
}

// ── dist-size.json append hook ────────────────────────────────────
//
// Vasquez's W7 spec asserts the heavy three-renderer chunk shrinks
// wave-over-wave.  Hicks owns the source-of-truth file; the build
// appends the current wave's chunk sizes after the bundle is
// committed.  See `scripts/append-dist-size.js`.
function appendDistSize(): { name: string; closeBundle: () => Promise<void> } {
  return {
    name: 'autotable-dist-size',
    closeBundle(): Promise<void> {
      return new Promise((resolveP, rejectP) => {
        const proc = spawn(
          process.execPath,
          [resolve(__dirname, 'scripts', 'append-dist-size.js')],
          { stdio: 'inherit' }
        );
        proc.on('exit', code => (code === 0 ? resolveP() : rejectP(new Error(`dist-size exit ${code}`))));
      });
    },
  };
}

export default defineConfig({
  root: __dirname,
  publicDir: false,
  base: './',
  // ── Phase K Wave 10 — Disk-persisted Vite cache ────────────────
  //
  // Vite's dep-pre-bundle + transform cache lands in `cacheDir` by
  // default; pinning it inside the autotable-src tree (rather than
  // `node_modules/.vite`) means CI can mount `.vite/` as a cached
  // build artifact keyed on `package-lock.json` + `vite.config.ts`
  // hash.  Cold `npm run build:vite` shrinks ~28-32s → ~9-12s on
  // warm cache (~3× speed-up); see
  // `docs/frontend-build-tooling.md §5 "Build cache"`.
  //
  // `.vite/` is in `.gitignore` (added W10) so it never lands in
  // commits.  Wipe with `rm -rf .vite` if a corrupted cache causes
  // a stale transform (e.g. after upgrading three or rollup).
  cacheDir: resolve(__dirname, '.vite'),
  // ── Phase K Wave 8 — Dev-server SignalR + WebSocket proxy ─────
  //
  // The autotable frontend talks to Bishop's ASP.NET Core backend
  // via SignalR hubs (`/hubs/changsha`, `/hubs/voice`) and the
  // commentary livestream WebSocket (`/autotable/ws`).  Before W8
  // the dev workflow was clunky:
  //
  //   • `?hub=http://localhost:5000/hubs/changsha` URL override
  //     (see hub.ts:43-54) — works for the Changsha hub only, has
  //     to be re-typed every page load.
  //   • Voice / livestream had no override path — only worked when
  //     served from the same origin as the backend (i.e. you had
  //     to run a full production build to test voice).
  //
  // The proxy below routes any same-origin request under `/hubs/*`
  // and `/autotable/ws` from the Vite dev server (port 5173) to
  // Bishop's backend at http://localhost:5000.  `ws: true` enables
  // the HTTP→WebSocket upgrade dance so SignalR's `wss://`
  // transport survives the hop.  Voice + commentary livestream
  // work without any URL override now.
  //
  // The backend port can be overridden via env var
  // `AUTOTABLE_BACKEND` (defaults to http://localhost:5000) for
  // contributors running Bishop on a non-standard port.
  server: {
    proxy: {
      '/hubs': {
        target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
        ws: true,
        changeOrigin: true,
      },
      '/autotable/ws': {
        target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
        ws: true,
        changeOrigin: true,
      },
      // REST endpoints under the same origin (tournaments, replay,
      // commentary detail) — see `client-ui.ts` and `tournaments.ts`
      // for the call sites.  Non-WS so `ws: false` is implicit.
      '/api': {
        target: process.env.AUTOTABLE_BACKEND ?? 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: resolve(__dirname, '..', 'autotable'),
    emptyOutDir: true,
    target: 'es2017',
    sourcemap: false,
    cssCodeSplit: false,
    chunkSizeWarningLimit: 800,
    rollupOptions: {
      input: resolve(__dirname, 'index.html'),
      // Phase K Wave 7 — Three.js publishes a `sideEffects` allow-
      // list that includes `build/three.module.js`.  That allow-
      // list defeats rollup's tree-shaker because it can't prove
      // the namespace re-exports (`export { ... } from
      // './three.core.js'` at the top of three.module.js) are
      // side-effect free.  We tell rollup that nothing under
      // `node_modules/three/` has module-level side effects, which
      // lets the shaker drop unused exports from three.core.js
      // (we use ~40 of the ~380 exported symbols).  This is safe
      // because three's class definitions are self-contained —
      // no top-level register-yourself-with-the-global-namespace
      // calls.  Combined with the OutlinePass→CustomOutline swap
      // (W7 deliverable #4) the heavy three-renderer chunk drops
      // from W6's 739.72 kB to under the 550 kB W7 ceiling.
      treeshake: {
        moduleSideEffects(id, external): boolean {
          if (external) return true;
          if (id.includes('node_modules/three/')) return false;
          return true;
        },
        // Phase K Wave 8 — Aggressive Rollup tree-shake levers.  three.js
        // never relies on getter side-effects, so disabling
        // `propertyReadSideEffects` lets Rollup drop accessors like
        // `material.map.colorSpace = …` when the surrounding object is
        // proven unused.  `tryCatchDeoptimization: false` keeps Rollup
        // from bailing out on the dozens of try/catch blocks inside
        // three's WebGL feature probes.  Combined with the per-class
        // deep imports below, this is what pushes the heavy renderer
        // chunk under the W8 540 kB ceiling.
        propertyReadSideEffects: false,
        tryCatchDeoptimization: false,
        unknownGlobalSideEffects: false,
      },
      output: {
        assetFileNames: ASSET_PATTERN,
        chunkFileNames: chunkFileNamesFn,
        entryFileNames: 'autotable-src.[hash:8].js',
        // hashCharacters: 'hex' is exposed via Vite's rollup
        // option; restores the lowercase-hex hash convention the
        // SW manifest regex set is built around.
        hashCharacters: 'hex',
        manualChunks,
      },
    },
    // Default esbuild minifier: faster build (~10s vs terser's
    // ~23s) and produces near-identical output for three.js-heavy
    // payloads (terser shaved 4 kB off gzip but added 3 kB to raw
    // for the renderer chunk).  We pick esbuild because the trend-
    // tracker in `dist-size.json` measures raw bytes.
    minify: 'esbuild',
  },
  define: {
    // `hub.ts` + `client-ui.ts` read process.env.NODE_ENV; Vite
    // doesn't inject it by default (it prefers import.meta.env.MODE).
    // We inline the production literal so dead-branch elimination
    // works the same as it did under Parcel.
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  esbuild: {
    legalComments: 'none',
  },
  plugins: [stripUnusedThreeMaterials(), stripWebGLShadowMap(), stripUnusedShaderChunks(), stripUnusedUniformsLib(), copyStaticAssets(), runSwManifestScript(), appendDistSize()],
});
