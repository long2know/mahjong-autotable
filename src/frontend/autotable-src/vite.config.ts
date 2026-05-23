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
  plugins: [stripUnusedThreeMaterials(), stripWebGLShadowMap(), copyStaticAssets(), runSwManifestScript(), appendDistSize()],
});
