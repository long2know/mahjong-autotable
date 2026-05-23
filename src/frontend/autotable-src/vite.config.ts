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
    },
  };
}

// ── Post-build SW manifest hook ───────────────────────────────────
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
  plugins: [copyStaticAssets(), runSwManifestScript(), appendDistSize()],
});
