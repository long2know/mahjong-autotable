# Three.js bundle budget — Phase K Wave 8

The `three-renderer.<hash>.js` chunk (split out of `scene-shell` in
Wave 5) is the heaviest single asset in the autotable bundle. This
document tracks which three.js subpackages we currently pull and
what was removed wave-by-wave.

## Budget targets

| Chunk                         | W5 size      | W6 actual | W7 actual | W8 target | W8 actual | Notes                                  |
|-------------------------------|--------------|-----------|-----------|-----------|-----------|----------------------------------------|
| `three-renderer.<hash>.js`*   | 869.6 kB     | 838.8 kB  | 648.07 kB | <600 kB   | **602.86 kB** ⚠️ over by 2.86 kB (see §4) | Sum of both sub-chunks            |
| `three-renderer.<hash>.js` (big) | 724.7 kB  | 739.7 kB  | 578.72 kB | <540 kB   | **531.86 kB** ✅ | Single largest chunk              |
| `gltf-loader.<hash>.js`       | (inside big) | (inside big) | (inside big) | <50 kB | **44.22 kB** ✅ | NEW W8: peeled out of the big chunk |
| `scene-shell.<hash>.js`       | 2.3 kB       | 2.33 kB   | 2.34 kB   | <5 kB     | 2.34 kB   | Thin coordinator, three.js-free        |
| `scene-effects.<hash>.js`     | 60 kB        | ~60 kB    | 59.04 kB  | <80 kB    | 58.67 kB  | GameUi modal graph + MoveLog           |

*Vite emits the renderer as two sub-chunks (one big content-heavy
chunk + a small entry chunk). The budget compares the sum.

## In-use three.js surface (verified W7)

Anything below is statically imported and reachable by the renderer
boot path. Removing them would break the WebGL canvas.

| Module                                                  | Used by              | Reason                                                                                            |
|---------------------------------------------------------|----------------------|---------------------------------------------------------------------------------------------------|
| `three` (core: Scene / Camera / WebGLRenderer / etc.)   | `main-view.ts`       | Top-down + perspective renderer, camera, lights.                                                  |
| `three/examples/jsm/loaders/GLTFLoader.js`              | `asset-loader.ts`    | Loads `models.auto.glb` (table, tile geometry).                                                   |
| `three/examples/jsm/utils/BufferGeometryUtils.js`       | `object-view.ts`     | `mergeGeometries` consolidates the tile-tray geometry into a single draw call.                    |

## Removed in Wave 7

| Module                                                     | Removed from   | W6 cost (min)  | Replacement / rationale                                                                                                                                                                |
|------------------------------------------------------------|----------------|----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `three/examples/jsm/postprocessing/EffectComposer.js`      | `main-view.ts` | ~12 kB         | **Replaced.** Direct `renderer.render(scene, camera)` is the post-Outline path; no composer needed.                                                                                    |
| `three/examples/jsm/postprocessing/RenderPass.js`          | `main-view.ts` | ~2 kB          | **Replaced.** Subsumed into direct render.                                                                                                                                              |
| `three/examples/jsm/postprocessing/OutlinePass.js`         | `main-view.ts` | ~85 kB         | **Replaced** by `src/render/custom-outline.ts` (~3 kB minified). See §3 below.                                                                                                          |

## Removed in Wave 6 (for completeness)

| Module                                                     | Removed from   | W5 cost (min)  | Replacement / rationale                                                                                                                                                                |
|------------------------------------------------------------|----------------|----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `three/examples/jsm/libs/stats.module.js` (FPS overlay)    | `main-view.ts` | ~3 kB          | **Lazy + opt-in.** The dev FPS overlay is now dynamic-imported only when the URL carries `?stats=1`.                                                                                   |

## How to re-audit

```bash
cd src/frontend/autotable-src
npm run build:vite
du -k ../autotable/three-renderer.*.js
cat dist-size.json | jq '.history[-1].chunks'
```

A future sweep should consider:

- Replacing GLTFLoader with a pre-converted binary tile mesh
  packed alongside the bundle (eliminates the GLTF parser
  + extension code paths — saves ~50 kB).
- Switching three.js imports from `three` → `three/src/*` direct
  paths (would let rollup tree-shake the internal classes too,
  saves an estimated 30-80 kB but high refactor cost).

These are Phase L scope.

## §3. OutlinePass replacement spike (W7)

### Motivation

W6's `frontend-three-budget.md` listed
"`three/examples/jsm/postprocessing/OutlinePass.js` — manual
outline pass via stencil — too risky pre-Phase-L, current outline
is core UX." W7 revisited the call and shipped the replacement.

The selection outline is the only post-processing pass we use.
Pulling `EffectComposer` + `RenderPass` + `OutlinePass` together
costs ~99 kB minified (uncompressed). `OutlinePass` alone is
~85 kB — and most of that is features we never invoke:
selective bloom, blur kernel sizing, pattern textures, the
"hidden edge" blending mode.

### Design: inverted-hull outline shader

`src/render/custom-outline.ts` (~3 kB minified) is a drop-in
subset of `OutlinePass`'s API:

```ts
import { CustomOutline } from './render/custom-outline';

const outline = new CustomOutline(scene, camera, renderer);
outline.setSelected([mesh]);
outline.setEdgeColor(0xffd75e);
outline.precompile(scene, () => renderer.render(scene, camera));
// In the render loop:
renderer.render(scene, camera);  // base scene
outline.render();                // overlay outline
```

Under the hood it uses the classic **inverted-hull** technique:

1. For each selected mesh, build a sibling `Mesh` sharing the
   same geometry.
2. The sibling renders with a `ShaderMaterial`:
   - `side: BackSide` — only the back-faces of the expanded
     mesh are drawn.
   - Vertex shader expands each vertex along its normal by a
     small constant (in NDC-space, so outline thickness is
     view-independent).
   - Fragment shader writes a flat color.
3. Depth test set to `LessEqual` with depth-write disabled, so
   the outline shows through occluding geometry only at edges.

The combined effect is a clean, anti-aliasing-friendly outline
that looks visually equivalent to OutlinePass's edge-glow
output for our solid-color tile UX.

### Visual parity

Side-by-side `expect-visible` snapshots taken against the
selection-discard flow:

| Aspect | OutlinePass (W6) | CustomOutline (W7) |
|--------|------------------|--------------------|
| Color  | `#ffd75e` yellow | `#ffd75e` yellow (matches) |
| Thickness | ~3 screen-px | ~3 screen-px (NDC-tuned) |
| Anti-aliasing | Post-pass blur kernel | Hardware MSAA on the renderer |
| Visible through occluders | Yes (edge-glow) | Yes (depth-test trick) |
| Frame cost (RTX 3060) | 0.32 ms | 0.18 ms |
| Frame cost (Chromebook iGPU) | 1.4 ms | 0.7 ms |

The thinner cost on iGPUs is because OutlinePass requires three
separate full-screen passes (mask → blur → composite); the
inverted hull is one draw call per selected mesh.

### API parity

| OutlinePass method | CustomOutline | Notes |
|--------------------|---------------|-------|
| `setSelected(objects)` | ✅ | Accepts `Mesh[]` or `Object3D[]` |
| `setEdgeColor(hex)` | ✅ | RGB only — no edgeGlow / edgeStrength |
| `precompile(scene, renderFn)` | ✅ | Warms shader cache once |
| `dispose()` | ✅ | Frees geometries + materials |
| `selectedObjects =` | ❌ | Use `setSelected(...)` instead |
| `edgeStrength` / `edgeGlow` / `edgeThickness` | ❌ | Constant baked into the shader (3px) |
| `pulsePeriod` | ❌ | Not used in our UI |
| `visibleEdgeColor` / `hiddenEdgeColor` | ❌ | Single color only |

We didn't use any of the dropped features in W1-W6, so this is a
zero-functional-loss replacement.

### Renderer integration

`src/main-view.ts` setup was rewired:

```diff
- import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
- import { RenderPass }     from 'three/examples/jsm/postprocessing/RenderPass.js';
- import { OutlinePass }    from 'three/examples/jsm/postprocessing/OutlinePass.js';
+ import { CustomOutline }  from './render/custom-outline';

- this.composer = new EffectComposer(this.renderer);
- this.composer.addPass(new RenderPass(scene, camera));
- this.outlinePass = new OutlinePass(new Vector2(w, h), scene, camera);
- this.composer.addPass(this.outlinePass);
+ this.outline = new CustomOutline(scene, camera, this.renderer);

  // render loop:
- this.composer.render();
+ this.renderer.render(scene, camera);
+ this.outline.render();
```

All call sites that previously poked at `outlinePass.selectedObjects`
now use `outline.setSelected(...)`.

### Size impact

| Asset | Before (W6) | After (W7) | Delta |
|-------|-------------|------------|-------|
| OutlinePass.js (raw) | ~85 kB | 0 kB | -85 kB |
| EffectComposer.js + RenderPass.js | ~14 kB | 0 kB | -14 kB |
| `src/render/custom-outline.ts` (minified) | — | ~3 kB | +3 kB |
| **Net** | — | — | **-96 kB** |

This is roughly **two-thirds of the W7 renderer-chunk reduction**.
The remaining third comes from the rollup tree-shake (W7 bundler
swap; see `docs/frontend-build-tooling.md`).

### Risk + mitigation

The inverted-hull technique fails on meshes with sharp creases
where adjacent face-normals diverge >90° (the expansion gaps
look split). Tile meshes are simple boxes — no risk.

If we ever introduce a curved tile geometry, the fallback is to
add `geometry.computeVertexNormals()` and use a per-vertex
averaged normal rather than the face normal — already a one-line
change inside `custom-outline.ts`.

## SW pre-cache implications

`scripts/generate-sw-manifest.js` carries `THREE_RENDERER_RE` so
the service worker pre-warms both renderer sub-chunks at install
time. Any future chunk-name change must update the regex (see
`src/frontend/autotable-src/scripts/generate-sw-manifest.js`).

## §4 — Wave 8: GLTFLoader chunk peel + deep-imports experiment

### What changed

The big chunk dropped from 578.72 kB (W7) to **531.86 kB (W8)** — a
46.86 kB savings, putting it under the 540 kB W8 ceiling with
~8 kB of headroom.

Two changes in `vite.config.ts`:

1. **GLTFLoader chunk peel (W8 winner — −44.22 kB):** The
   `asset-loader.ts` dynamic-import already lazy-loaded
   `three/examples/jsm/loaders/GLTFLoader.js`, but Vite/Rollup
   collapsed it back into `three-renderer` because both paths
   matched the catchall `node_modules/three/` rule in
   `manualChunks`. Adding an explicit pre-check that returns the
   `gltf-loader` chunk name for the loader path splits the import
   into its own 44.22 kB chunk that the asset-loader's existing
   `await import(...)` now fetches in parallel with the textures.
   Net cold-load cost is unchanged because `AssetLoader.loadAll()`
   was already awaiting both in parallel — the change just lets
   Rollup honour the boundary.

2. **`mergeGeometries` replaced with `mergeSimpleGeometries`
   (−3.83 kB):** `object-view.ts` only used `mergeGeometries` for
   the static tile-tray geometry consolidation (24 trays sharing
   identical attribute layout, no indexed geometries). The full
   `BufferGeometryUtils.js` (1435 lines) was being pulled in for
   that single one-shot call. Replaced with a 36-line hand-rolled
   helper that handles the simple non-indexed case (see the comment
   block above `mergeSimpleGeometries` in `object-view.ts` for the
   contract — all inputs must share attribute names + itemSize,
   all must be non-indexed). If we ever need to merge indexed
   geometries we revert to `mergeGeometries` for those callers.

3. **Aggressive Rollup tree-shake levers (no measurable impact):**
   Added `propertyReadSideEffects: false`,
   `tryCatchDeoptimization: false`,
   `unknownGlobalSideEffects: false` to `build.rollupOptions.treeshake`.
   These had no observable effect on the W8 chunk sizes but are
   kept as future-proofing — they enable a slightly more aggressive
   tree-shake in case a future three.js point release introduces a
   getter side-effect pattern that the default config would
   conservatively preserve.

### Counter-intuitive negative finding — deep imports made the bundle BIGGER

The W8 directive hinted that switching `import { Vector3 } from 'three'`
to per-class deep imports (`import { Vector3 } from 'three/src/math/Vector3.js'`)
should help tree-shaking. **We tried it; it made the bundle ~150 kB
LARGER.** Two experiments confirmed this:

| Approach                                                        | Big chunk size | Δ from W7 baseline |
|-----------------------------------------------------------------|----------------|--------------------|
| W7 baseline (`from 'three'`, all paths via `build/three.module.js`) | 578.72 kB | — |
| Bulk swap to `from 'three/src/Three.js'`                        | 729.4 kB       | +150.7 kB ❌ |
| Per-class deep imports (`three/src/math/Vector3.js`, etc.)      | 725.5 kB       | +146.8 kB ❌ |

Why? The bundled `build/three.module.js` (single ESM file emitted
by three's own Rollup build) is **more** tree-shake-friendly than
the `src/` tree because:

- Rollup with `moduleSideEffects: false` on a single bundled file
  can dead-strip unused exports AND their private-helper
  transitive dependencies inside the same module.
- Per-file `src/` imports force Rollup to include entire
  transitive class dependency chains (e.g. `WebGLBackground.js`
  imports `BoxGeometry` even when the caller doesn't use
  `BoxGeometry` — the import is kept as a side-dep because
  Rollup conservatively assumes the import is required for
  `WebGLBackground`'s eval).
- Three's build process likely strips dev-mode debug paths that
  the raw `src/` tree preserves verbatim.

**Conclusion: don't retry deep imports.** The scripts
`scripts/three-deep-imports.js` and `scripts/three-collapse-imports.js`
are kept in-tree as reference / safety in case a future major
three.js release flips this calculus, but should NOT be run on
the source by default.

### Remaining dead-weight inside the renderer chunk

Even after the W8 peel, the big chunk still carries ~80 kB of
material classes pulled by `WebGLRenderer`'s internal
`material.type` string switches: `MeshStandardMaterial` (×13
references inside three's renderer code paths),
`MeshPhongMaterial`, `MeshPhysicalMaterial`, `MeshToonMaterial`,
`MorphTarget`, `Skeleton`, `SkinnedMesh`, `VideoTexture`,
`CompressedTexture`, `Sprite`, `Points`, `LOD`,
`GLBufferAttribute`. **These cannot be tree-shaken without
forking three's build** — they are referenced by string in the
renderer's draw-call dispatcher, which Rollup conservatively
treats as a live reference.

A future wave that wants more savings should consider either:

- **Patch three.js's `WebGLRenderer.js`** to remove the
  string-keyed material dispatches we don't use (we only render
  `MeshLambertMaterial` + `MeshBasicMaterial`), and ship the
  patch as a `pnpm` patch dependency or a custom `three`
  resolution in `package.json`.
- **Replace three.js with a leaner renderer** (e.g. ogl,
  regl, twgl) — large scope, deferred indefinitely.

### Trend table

| Wave | Big chunk | Total renderer (big + small) | gltf-loader | Delta vs prev |
|------|-----------|------------------------------|-------------|---------------|
| W5   | 724.7 kB  | 869.6 kB                     | (in big)    | —             |
| W6   | 739.7 kB  | 838.8 kB                     | (in big)    | +15 kB        |
| W7   | 578.72 kB | 648.07 kB                    | (in big)    | **−161 kB**   |
| W8   | 531.86 kB | 602.86 kB                    | 44.22 kB    | **−46.86 kB** |
| W9   | 507.47 kB | 582.85 kB                    | 44.22 kB    | **−24.39 kB** |
| W10  | 497.44 kB | 572.82 kB                    | 44.22 kB    | **−10.03 kB** |

## §5 — Wave 9: three.js feature strip via Vite transform plugin

### Motivation

W8 closed at 531.86 kB on the big chunk. W9 ceiling: **<510 kB**.
Deep imports (W8 §4) were the rabbit hole that made the bundle
GROW; W9 takes a different angle — instead of trying to make
Rollup tree-shake harder, we surgically *delete* unused source
in three.js's bundled files before they reach Rollup.

### Approach: source transforms at the Vite `transform()` hook

Two Vite plugins in `vite.config.ts` run with `enforce: 'pre'`
ahead of Rollup's tree-shake pass:

1. **`stripUnusedThreeMaterials`** — targets
   `node_modules/three/build/three.core.js`. Replaces each
   unused material class body with a 6-line stub:

   ```ts
   class MeshPhongMaterial extends Material {
     constructor(parameters) {
       super();
       this.isMeshPhongMaterial = true;
       this.type = 'MeshPhongMaterial';
       if (parameters !== undefined) this.setValues(parameters);
     }
     copy(source) { super.copy(source); return this; }
   }
   ```

   Materials stubbed: `MeshPhongMaterial`, `MeshStandardMaterial`,
   `MeshPhysicalMaterial`, `MeshToonMaterial`, `MeshNormalMaterial`,
   `MeshDepthMaterial`, `MeshDistanceMaterial`, `MeshMatcapMaterial`,
   `PointsMaterial`, `SpriteMaterial`, `ShadowMaterial`,
   `LineDashedMaterial`, `RawShaderMaterial` (13 classes).

   Constraints honoured:
   - `MeshDepthMaterial` / `MeshDistanceMaterial` are
     instantiated unconditionally by three's `WebGLShadowMap`
     constructor (`three.module.js:8413-8414`). The stub
     preserves the `depthPacking` slot so
     `new MeshDepthMaterial({depthPacking: RGBADepthPacking})`
     survives — `setValues` reads `currentValue = this[key]` and
     warns/skips when the property doesn't pre-exist on the
     instance, so the slot MUST be initialised in the stub
     constructor before `setValues(parameters)` runs.
   - Each stub preserves the `isXxxMaterial` flag — the
     WebGLRenderer's program cache reads these flags off the
     material instance to pick the shader; absent flags would
     break legitimate uses of `MeshBasicMaterial` /
     `MeshLambertMaterial` whose code paths gate on `else if`
     siblings.

2. **`stripModuleFeatures`** — targets
   `node_modules/three/build/three.module.js`. Replaces three
   feature-module function/class bodies with no-op stubs:

   | Feature                | Original | Stub | Why it's safe                            |
   |------------------------|----------|------|------------------------------------------|
   | `WebGLShadowMap`       | ~11 kB   | ~150 B | Autotable scene never sets `renderer.shadowMap.enabled = true` and no mesh has `castShadow = true`. The stub exposes `{enabled:false, autoUpdate:true, needsUpdate:false, type:1, render(){}}` — the surface the renderer reads. |
   | `WebXRManager`         | ~27 kB   | ~250 B | No VR/AR mode. Stub `extends EventDispatcher` so `xr.addEventListener('sessionstart', …)` (called inside the renderer constructor) succeeds. Exposes `enabled=false`, `isPresenting=false`, `cameraAutoUpdate=true`, `setAnimationLoop()`, `hasDepthSensing()`, `getEnvironmentBlendMode()`, `getDepthSensingMesh()`, `dispose()`. |
   | `WebXRDepthSensing`    | ~2 kB    | ~100 B | Sub-component of WebXR, never reached without an active AR session. |

### Implementation notes

Both transforms walk the source with a brace-depth counter that
respects single-line, block, and string-literal contexts so a
brace inside a JSDoc example or a regex literal doesn't break
the matcher.

The transforms are idempotent — re-running on already-stubbed
code is a no-op because the matchers anchor on the original
class header signature (`class Name extends Parent {`). When
three.js upgrades, the matchers fail loudly via a
`console.warn` and the original code passes through unchanged
— a missed strip is a build-size regression, not a correctness
regression.

### Recovery measurement

| Step                              | three-renderer-big | Delta |
|-----------------------------------|--------------------|-------|
| W8 baseline (no W9 strips)        | 531.86 kB          | —     |
| + material strip                  | 526.60 kB          | −5.26 kB |
| + module-feature strip            | **507.47 kB**      | **−19.13 kB** |

Total W9 saving on the big chunk: **−24.39 kB** (4.6 %). The
material strip alone underperformed because Rollup was already
eliminating most of the class internals — most of the W9 saving
comes from removing the three feature-modules' top-level
function bodies, which Rollup keeps as long as they're imported
unconditionally by `WebGLRenderer`.

### Runtime smoke test

A headless Playwright smoke run (`chromium.launch()` →
`page.goto(/autotable/)` → wait for canvas) reports zero JS
errors after the strip. Combined with the 7 Vasquez W8 specs
passing (all 7/7, including the trend gate
`three-renderer-540-hard`), this confirms the runtime surface
the stubs preserve is sufficient.

### Risk + future work

- **Three.js upgrades:** when bumping the `three` dep, run a
  smoke test before committing. The matchers will warn-and-pass
  on unknown class headers, so the build will succeed but with
  a regressed bundle size — keep an eye on `dist-size.json`.
- **Future strip candidates (W10+):**
  - `PMREMGenerator` (~14 kB) — only instantiated lazily; if we
    can prove no code path triggers it we could stub.
  - `WebXRController` (lives in three.core.js, ~3 kB).
  - `Lighting probe` family (small but unused).
- **DO NOT** retry deep-imports (W8 §4). The autopsy showed
  they grew the bundle by ~150 kB on the same scene.

## §6 — Wave 10: PMREMGenerator strip (partial win) + the cubeUV blocker

### Motivation

W9 closed at 507.47 kB on the big chunk. The W10 directive set
**<480 kB** as the stretch ceiling (≈ 28 kB more savings via the
PMREMGenerator candidate flagged in §5 "Future strip candidates").

### What landed

PMREMGenerator is the texture pipeline three.js uses to pre-process
cube + equirectangular environment maps into a cubeUV layout that PBR
materials sample from. It's instantiated lazily inside
`WebGLCubeUVMaps#get()` behind an
`if (isEquirectMap || isCubeMap)` branch — the autotable scene
uses ONLY plain 2D textures (`tiles.auto.png`, `sticks.auto.png`,
`center.auto.png`, `winds.auto.png`, the GLTF-bundled table), so
the branch never evaluates true at runtime. Rollup nonetheless
keeps the class body because the statically-reachable `new
PMREMGenerator(renderer)` call lives inside `WebGLCubeUVMaps`.

W10 extends the `stripModuleFeatures` Vite plugin
(`vite.config.ts`) to stub:

1. **`PMREMGenerator` class body** — constructor pre-initialises
   the ten private slots three's renderer reads off the instance
   (`_renderer`, `_pingPongRenderTarget`, `_lodMax`, `_cubeSize`,
   `_lodPlanes`, `_sizeLods`, `_sigmas`, `_blurMaterial`,
   `_cubemapMaterial`, `_equirectMaterial`). Public methods
   (`fromScene`, `fromEquirectangular`, `fromCubemap`,
   `compileCubemapShader`, `compileEquirectangularShader`,
   `dispose`) become no-ops returning null / void.

2. **Seven private helpers** — `_getBlurShader`,
   `_getEquirectMaterial`, `_getCubemapMaterial`,
   `_getCommonVertexShader`, `_createPlanes`, `_createRenderTarget`,
   `_setViewport` — replaced with stub returns. Each helper embeds
   ~3-4 kB of GLSL shader code; the strip kicks them out of the
   transform stream before Rollup sees them, so the shader strings
   never reach the renderer chunk.

### Recovery measurement

| Step                              | three-renderer-big | Delta |
|-----------------------------------|--------------------|-------|
| W9 baseline (no W10 strips)       | 507.47 kB          | —     |
| + PMREMGenerator class strip      | 497.44 kB          | −10.03 kB |
| + PMREM helper-function strip     | 497.44 kB          | 0 kB  |

The helper-function strip yielded **zero additional bytes on the
emitted chunk** — Rollup's tree-shaker was already dropping the
unreferenced helpers (their declarations weren't even reaching the
chunk after the class body was gutted). The explicit strip is
retained for defence-in-depth (a future three.js upgrade might
reintroduce a path that pulls them).

### The <480 kB blocker — cubeUV shader chunk

The remaining ~17 kB gap to the W10 target is held by the
`cube_uv_reflection_fragment` shader chunk (three.module.js:344)
+ the background fragment shader (`fragment$g` at line 516) +
the PBR fragment shader (`fragment$5` at line 560). These are
exported as named entries on `ShaderChunk` /
`ShaderLib` — Rollup cannot strip individual properties of a
named-export object literal without breaking the broader barrel.

Three live references in the renderer prevent a clean strip:

| Reference                               | Source                              | Why it can't be stripped                                              |
|-----------------------------------------|-------------------------------------|------------------------------------------------------------------------|
| `ShaderChunk.cube_uv_reflection_fragment` | `meshlambert_frag.glsl`           | Lambert's `#include <cube_uv_reflection_fragment>` makes the chunk live even though our scene never sets `material.envMap`. |
| `fragment$g` (background)                | `WebGLBackground#render`            | The renderer's `background.render(scene, ...)` call runs every frame; the shader compilation is gated on `scene.background !== null` but the constant is statically reachable. |
| `fragment$5` (Mesh{Standard,Physical}Material) | `WebGLPrograms.acquireProgram` | Although W9 stripped the material *classes*, the program registry still string-keys against `'MeshStandardMaterial'`. |

Closing the gap to <480 kB would require either:

- **Patching `meshlambert_frag.glsl`** to drop the
  `#include <cube_uv_reflection_fragment>` directive (a custom
  Vite plugin similar to the W9 material-class strip, but
  operating on shader strings). Risk: medium — if a future scene
  introduces an `envMap` it would silently render black.
- **Stripping `WebGLBackground`** — the renderer's
  `background.render(scene, ...)` is unconditional, but the
  inner shader path is gated; we could stub the inner branches.
  Risk: low if our scene never sets `scene.background`.
- **Patching `WebGLPrograms.acquireProgram`** to short-circuit
  the unused material dispatches. Risk: high — touches the hot
  per-frame render path; smoke-test surface is non-trivial.

W10 ships the conservative PMREMGenerator strip only. The three
candidates above are queued for W11 +consideration; expected
combined yield ~20-25 kB if all three land cleanly.

### Risk + back-out

The PMREMGenerator stub keeps the class declaration so any
runtime branch that does evaluate `isEquirectMap || isCubeMap`
gets a no-op `pmremGenerator.fromCubemap(...)` that returns null
— `WebGLCubeUVMaps#get` then falls into its "renderTarget !==
undefined" branch and returns the original texture, which is
exactly the path a non-env scene takes.

If a future change introduces:
- `scene.environment = new CubeTexture(...)` — silently no-ops,
  scene renders without env reflection (acceptable degradation).
- `material.envMap = cubeTex` — the env sample returns black; a
  future PBR-bringup wave should remove the strip first.

A scene-graph audit at W10 close confirms zero envMap / cube
texture usage in `src/frontend/autotable-src/src/`.

### Trend ledger update

| Wave | Big chunk | Target  | Result |
|------|-----------|---------|--------|
| W7   | 578.72 kB | <550 kB | ✅      |
| W8   | 531.86 kB | <540 kB | ✅      |
| W9   | 507.47 kB | <510 kB | ✅      |
| W10  | 497.44 kB | <480 kB | ⚠️ partial — see blockers above |

Monotonic-decrease invariant holds for a 5th consecutive wave
(Vasquez's W7 trend gate).

## §7 — Wave 11: ShaderChunk barrel surgery

### Motivation

W10 closed at 497.44 kB on the big chunk, missing the <480 kB
stretch ceiling by ~17 kB. The autopsy in §6 traced the remaining
bulk to three named entries on the `ShaderChunk` / `ShaderLib`
barrel:

- `cube_uv_reflection_fragment` (line 344 of three.module.js,
  ~2.3 kB unminified) — the cubeUV environment-sampling chunk,
  `#include`d by `meshlambert_frag` and `meshphysical_frag`.
- `fragment$g` (line 516, ~0.6 kB) — `backgroundCube_frag`, used
  only when `scene.background` is set to a `CubeTexture`.
- `fragment$5` (line 560, ~2.7 kB) — `meshphysical_frag`, the
  PBR shader (PointsMaterial / StandardMaterial / etc., all of
  which W9 already stubbed at the class level).

Rollup keeps the entire barrel as a single unit (it can't strip
individual properties from a named-export object literal), so
all three shader strings ride along into the renderer chunk
even though the autotable scene never touches the code paths
that compile them.

### Approach: source transform on the shader-string consts

W11 adds the `stripUnusedShaderChunks()` Vite plugin to
`vite.config.ts`. The plugin runs `enforce: 'pre'` on
`three.module.js` and:

1. Identifies the shader-string consts keyed off the ShaderLib
   registry (the `ShaderLib` object literal at line ~660 of
   three.module.js maps `meshbasic_frag` → `fragment$a`,
   `meshlambert_frag` → `fragment$9`, etc.).
2. KEEPS `fragment$a` / `vertex$a` (meshbasic — used by
   `MeshBasicMaterial` AND `LineBasicMaterial`, which shares the
   "basic" ShaderLib entry).
3. KEEPS `fragment$9` / `vertex$9` (meshlambert — used by
   `MeshLambertMaterial`).
4. EMPTIES every other `fragment$X` / `vertex$X` pair (the
   barrel still re-exports the name; the body becomes `""`).
5. EMPTIES `cube_uv_reflection_fragment`. The `#include
   <cube_uv_reflection_fragment>` directive inside
   `meshlambert_frag` resolves to an empty string at GLSL
   compile time, and the cubeUV block was already guarded by
   `#ifdef ENVMAP_TYPE_CUBE_UV` — a macro never defined in
   our scene (no envMap usage).
6. EMPTIES the standalone `vertex` / `fragment` VSM blur shader
   pair (the W9-stubbed `WebGLShadowMap` was the only caller).

### Stripped shader entries (with their ShaderLib aliases)

| Const | ShaderLib alias | Why safe to strip |
|-------|------------------|---------------------|
| `vertex$h` / `fragment$h` | `background_*` | No `scene.background = Texture(...)`. |
| `vertex$g` / `fragment$g` | `backgroundCube_*` | No cube-texture background. |
| `vertex$f` / `fragment$f` | `cube_*` | No CubeShader / Sky usage. |
| `vertex$e` / `fragment$e` | `depth_*` | Shadow path stubbed in W9. |
| `vertex$d` / `fragment$d` | `distanceRGBA_*` | Shadow path stubbed in W9. |
| `vertex$c` / `fragment$c` | `equirect_*` | No equirect env mapping. |
| `vertex$b` / `fragment$b` | `linedashed_*` | No `LineDashedMaterial`. |
| `vertex$8` / `fragment$8` | `meshmatcap_*` | `MeshMatcapMaterial` stubbed W9. |
| `vertex$7` / `fragment$7` | `meshnormal_*` | `MeshNormalMaterial` stubbed W9. |
| `vertex$6` / `fragment$6` | `meshphong_*` | `MeshPhongMaterial` stubbed W9. |
| `vertex$5` / `fragment$5` | `meshphysical_*` | PBR stack stubbed W9. **Largest single contributor.** |
| `vertex$4` / `fragment$4` | `meshtoon_*` | `MeshToonMaterial` stubbed W9. |
| `vertex$3` / `fragment$3` | `points_*` | `PointsMaterial` stubbed W9. |
| `vertex$2` / `fragment$2` | `shadow_*` | `ShadowMaterial` stubbed W9. |
| `vertex$1` / `fragment$1` | `sprite_*` | `SpriteMaterial` stubbed W9. |
| `cube_uv_reflection_fragment` | (chunk) | No envMap usage; `#include` resolves to empty. |
| `vertex` / `fragment` (standalone) | VSM blur | `WebGLShadowMap` stubbed W9. |

### Scene-graph audit (W11 re-confirmation)

`src/frontend/autotable-src/src/` instantiates only these
material classes from `three`:

| Class | Used in |
|-------|---------|
| `MeshBasicMaterial` | `object-view.ts` (raycast outline mesh) |
| `MeshLambertMaterial` | `asset-loader.ts`, `center.ts`, `thing-group.ts` (table + tiles + sticks + winds) |
| `LineBasicMaterial` | `selection-box.ts`, `mouse-ui.ts` (drag rectangle + hover ring) |
| `ShaderMaterial` | `scene-effects.ts` (W7 CustomOutline inverted-hull shader) |

`MeshBasicMaterial` + `LineBasicMaterial` share the
`ShaderLib.basic` entry → `meshbasic_*` shaders. KEPT.
`MeshLambertMaterial` uses `ShaderLib.lambert` →
`meshlambert_*`. KEPT.
`ShaderMaterial` provides its own GLSL strings — none of the
ShaderLib chunks above are referenced.

### Recovery measurement

| Step | three-renderer-big | Delta |
|------|--------------------|-------|
| W10 baseline (no W11 strip)        | 497,440 B (497.44 kB) | —          |
| + ShaderChunk barrel surgery (W11) | 466,395 B (466.40 kB) | **−31.04 kB** |

The W11 strip delivers a **−6.24% reduction on the big chunk
in a single wave**, **comfortably under the <475 kB stretch
ceiling** with a ~9 kB margin. Combined with the W2 → W11 trend,
the heavy renderer chunk has dropped **−36.95% (−273 kB)** since
the Wave 6 Parcel peak of 739.72 kB.

### Runtime smoke test

A Playwright preview server + chromium smoke run (`vite preview`
on port 4174 → `chromium.launch()` → `page.goto('/')` →
`page.waitForSelector('body[lang]')`) confirms:

- The lobby paints with no console errors.
- The PWA shortcuts route handler fires for `?action=*` URLs.
- Three screenshots captured at the expected viewports
  (1024×768 wide + 768×1024 narrow) with the table / spectator
  / tournament views rendering without WebGL compile errors.

The Vasquez `three-renderer-480-hard.spec.ts` (W10 forward-
staged) passes against the K11 `dist-size.json` row.

### Risk + back-out

The shader-strip plugin is named after `stripUnusedShaderChunks`
in `vite.config.ts` and registered in the `plugins:` array
between `stripWebGLShadowMap` and `copyStaticAssets`. To roll
back:

1. Comment out the entry in the `plugins:` array.
2. Re-run `npm run build:vite`.
3. Confirm `three-renderer-big` returns to ~497 kB (the W10
   baseline lives in `dist-size.json:history[wave=K10]`).

Failure modes to watch for after future three.js upgrades:

- **A new scene introduces `scene.background = new
  CubeTexture(...)`** → `backgroundCube_frag` compile fails
  (empty string is not valid GLSL). Symptom: black canvas,
  console "ERROR: 0:1: '': syntax error". Re-add the affected
  shader to the keep-list in `vite.config.ts`.
- **A new material with `envMap` set** → cubeUV sample path
  silently returns black. Less catastrophic — UI still paints,
  but reflections are gone. Detect by adding a Playwright
  visual-regression spec for env-mapped surfaces.
- **A three.js upgrade renames the `fragment$X` const suffixes**
  (rollup-internal — the bundler renames anonymous suffixes
  per build). The plugin warn-and-skips unknown names; the
  build will succeed but the chunk size will regress. Watch
  `dist-size.json` after dep bumps.

### Trend ledger update

| Wave | Big chunk | Target | Result |
|------|-----------|--------|--------|
| W7   | 578.72 kB | <550 kB | ✅      |
| W8   | 531.86 kB | <540 kB | ✅      |
| W9   | 507.47 kB | <510 kB | ✅      |
| W10  | 497.44 kB | <500 kB ✅ / <480 kB ⚠️ | partial |
| W11  | **466.40 kB** | **<475 kB** | ✅ (stretch met with ~9 kB margin) |

Monotonic-decrease invariant holds for a 6th consecutive wave
(Vasquez's W7 trend gate). The W10 <480 kB stretch ceiling is
also retroactively satisfied.

### Hand-off to W12

The big chunk now sits at 466.40 kB. Remaining strip candidates
(low single-digit kB each):

- **`opaque_fragment` + `colorspace_fragment` + `tonemapping_*`
  ShaderChunks** — referenced via `#include` from
  `meshlambert_frag` / `meshbasic_frag`. The autotable scene
  uses `LinearSRGBColorSpace` everywhere and no tone-mapping
  (default = `NoToneMapping`), so the include bodies resolve
  to pass-through code at compile time. Stripping the JS-side
  string drops ~3-5 kB but requires keeping enough of the chunk
  to satisfy the `#include` resolver — needs care.
- **`UniformsLib` entries for unused features** (clearcoat,
  iridescence, sheen, transmission, anisotropy, dispersion,
  reflectivity-extras). Each entry is ~50-200 B. Aggregate
  ~2-3 kB if all stripped.
- **`shadowmap_*` chunks** — referenced via `#include` from
  `meshlambert_frag` but guarded by `#ifdef USE_SHADOWMAP`.
  Could empty the chunk bodies similarly to cubeUV.

Combined the W12 candidates above could shave another 5-8 kB.
The next round-number ceiling is <460 kB → realistic with one
more wave of surgery.

A more aggressive Phase L candidate: hand-roll `three/src/*`
imports + a custom `WebGLRenderer` wrapper. The W6 estimate
(–200 to –300 kB total) still stands but the engineering cost
remains very high — defer until the Phase K series closes.

## §8 — Wave 12: PMREMGenerator-adjacent + shadow + envmap strip + UniformsLib

### Motivation

W11 closed at 466.40 kB on the big chunk, satisfying the <475
kB W11 ceiling with ~9 kB margin. The §7 hand-off identified
three remaining surgical candidates:

1. **`shadowmap_*` ShaderChunks** (parts + vertex + mask
   fragment) — body wrapped in `#ifdef USE_SHADOWMAP`.
2. **PMREMGenerator-adjacent ShaderChunks** — the cubeUV chunk
   was stripped in W11, but the wider envmap chunk family
   (`envmap_*`) was still riding along into the renderer
   even though the autotable scene uses only 2D textures
   (no `scene.environment`, no `material.envMap`).
3. **`UniformsLib` entries for stubbed materials** —
   `roughnessmap`, `metalnessmap`, `gradientmap`, `points`,
   `sprite` are only ever referenced by ShaderLib material
   definitions that W9 stubbed at the class level.

W12 ships all three in one Vite-plugin pass.

### Approach: same `stripUnusedShaderChunks` + new `stripUnusedUniformsLib`

The W11 ShaderChunk strip plugin already operates on
`three.module.js` via the `enforce: 'pre'` transform hook. W12
extends its `SHADER_CHUNKS_TO_EMPTY` list with ten new entries:

```
shadowmap_pars_fragment
shadowmap_pars_vertex
shadowmap_vertex
shadowmask_pars_fragment
envmap_fragment
envmap_common_pars_fragment
envmap_pars_fragment
envmap_pars_vertex
envmap_physical_pars_fragment
envmap_vertex
```

Each chunk's GLSL body is wrapped in either `#ifdef
USE_SHADOWMAP` or `#ifdef USE_ENVMAP`. Neither macro is ever
defined by the autotable's renderer (no `renderer.shadowMap.
enabled`, no `material.envMap`), so the bodies are stripped
at the GLSL preprocessor stage anyway. Emptying the JS-side
strings drops the runtime cost of carrying ~10 kB of glsl
verbatim. `shadowmask_pars_fragment` is the one chunk that
defines a function (`getShadowMask()`) referenced outside an
`#ifdef`; the only call site is the W9-stripped `shadow_frag`
shader, so the empty chunk's runtime use is zero.

The new `stripUnusedUniformsLib()` plugin walks the
`UniformsLib = { ... }` registry at the same `enforce: 'pre'`
phase and rewrites the five W9-stubbed-material entries to
empty object literals. ShaderLib's `mergeUniforms([
UniformsLib.roughnessmap, ... ])` calls still resolve (they
read `{}` instead of the original ~6-line descriptors), and
the materials that would have consumed those uniforms are
stubbed anyway.

### Measured savings (K12 build)

| Source pass | Before → After | Δ |
|-------------|----------------|----|
| `[module-strip]` (W9 + W10 carried fwd) | 603,380 → 544,127 | −59,253 |
| `[shaderchunk-strip]` (W11 + W12) | 544,127 → 491,690 | **−52,437 (W12 +50,176 vs. W11)** |
| `[uniformslib-strip]` (W12 NEW) | 491,690 → 490,745 | **−945** |
| `[material-strip]` (W9 carried fwd) | 1,401,287 → 1,342,346 | −58,941 |
| Renderer chunk emitted | — | **448,648 B (≈ 438.13 kiB / 448.65 kB)** |

`dist-size.json:history[wave=K12]` records the new chunk
sizes. Vasquez's `three-renderer-475-soft.spec.ts` (W11) +
`three-renderer-480-hard.spec.ts` (W10) both pass with
healthy margins.

### Risk + back-out

Identical structure to W11: each chunk's empty body is safe
**only** because the autotable scene never sets the
corresponding `USE_*` macro. If a future scene introduces
shadow casting or an envMap-bearing material:

| Trigger | Symptom | Roll-back |
|---------|---------|-----------|
| `renderer.shadowMap.enabled = true` | Console error: `'getShadow': undefined function` at WebGL compile time. Black canvas. | Remove `shadowmap_*` + `shadowmask_*` from `SHADER_CHUNKS_TO_EMPTY` in `vite.config.ts`. |
| `material.envMap = new CubeTexture(...)` or `scene.environment = ...` | Reflections render as flat black. UI keeps rendering — no console error. | Remove `envmap_*` from `SHADER_CHUNKS_TO_EMPTY`. |
| `new MeshStandardMaterial({ roughnessMap, metalnessMap })` un-stubbed | Console warns from three's uniform-validation pass. Material renders without the affected texture's input. | Remove the offending key from `UNIFORMS_LIB_KEYS_TO_EMPTY` AND un-stub the material class in W9's `STUB_MATERIALS` map. |

The unstripped baseline lives at W11's 466.40 kB; disable the
W12 entries first (cheap) and only un-stub the W9 material
classes if the W12 strip alone doesn't fix the regression.

### Trend ledger update

| Wave | Big chunk | Target | Result |
|------|-----------|--------|--------|
| W7   | 578.72 kB | <550 kB | ✅      |
| W8   | 531.86 kB | <540 kB | ✅      |
| W9   | 507.47 kB | <510 kB | ✅      |
| W10  | 497.44 kB | <500 kB ✅ / <480 kB ⚠️ | partial |
| W11  | 466.40 kB | <475 kB | ✅ (stretch met with ~9 kB margin) |
| W12  | **448.65 kB** | **<450 kB stretch / <460 kB acceptable** | ✅ (stretch met with ~1.4 kB margin) |

Monotonic-decrease invariant holds for a 7th consecutive wave
(Vasquez's W7 trend gate). Cumulative drop from W6 baseline:
**739.72 kB → 448.65 kB (−39.4 %)** over six waves.

### Hand-off to W13

The big chunk now sits at 448.65 kB. Remaining strip
candidates (low single-digit kB each):

- **`opaque_fragment` + `colorspace_fragment` + `tonemapping_*`
  ShaderChunks** (carried fwd from W11 hand-off) — referenced
  via `#include` from `meshlambert_frag` / `meshbasic_frag`.
  The autotable scene uses `LinearSRGBColorSpace` and no
  tone-mapping (default `NoToneMapping`), so the include
  bodies resolve to pass-through code at GLSL compile time.
  Stripping the JS-side string drops ~3-5 kB but requires
  keeping enough of the chunk to satisfy the `#include`
  resolver — needs care. The W12 envmap + shadow strip is
  the precedent for this surgery.
- **Remaining `UniformsLib` features** — `clearcoat`,
  `iridescence`, `sheen`, `transmission`, `anisotropy`,
  `dispersion`, `reflectivity-extras` (all PBR-feature
  specific, all routed through `ShaderLib.physical` which
  itself is stubbed via the W11 `meshphysical_*` strip).
  Aggregate ~1-2 kB if all five additional features stripped.
- **`lights_*` chunks** — the autotable uses `AmbientLight` +
  `DirectionalLight` only. `lights_phong_*` / `lights_toon_*`
  / `lights_physical_*` chunks are deadweight (the
  corresponding materials were W9-stubbed). Each chunk is
  ~0.5-2 kB.

Combined the W13 candidates above could shave another 4-7 kB.
The next round-number ceiling is **<445 kB** — feasible with
one more wave of careful surgery, **<440 kB** would need a
Phase L step.

A more aggressive Phase L candidate (deferred): hand-roll
`three/src/*` imports + a custom `WebGLRenderer` wrapper. The
W6 estimate (–200 to –300 kB total) still stands but the
engineering cost remains very high — defer until the Phase K
series closes.

## §9 — Wave 13: PMREMGenerator deeper strip (tonemapping + PBR-extras + map-feature chains)

The W12 hand-off identified three remaining surgery targets:
`opaque_fragment` / `colorspace_fragment` / `tonemapping_*`,
remaining `UniformsLib` PBR features, and the `lights_*`
chunks for unused materials. W13 closes the first and third
buckets, plus an additional sweep across every map-feature
`_pars_fragment` (alphamap / alphatest / alphahash / aomap /
lightmap / emissivemap / bumpmap / normalmap / specularmap /
metalnessmap / roughnessmap / displacementmap / fog /
dithering / premultiplied-alpha / clearcoat / iridescence /
transmission) — all guarded by `#ifdef USE_<MACRO>` that the
autotable scene never `#define`s.

The W12 hand-off note about `opaque_fragment` proved
unsafe: that chunk's body contains the **unconditional**
`gl_FragColor = vec4( outgoingLight, diffuseColor.a );`
assignment that produces the final render output. Stripping
it would yield a working compile but a black canvas.
`colorspace_fragment` was also held back — it's an
unguarded one-liner that reassigns `gl_FragColor` through
`linearToOutputTexel()`; safe in the `LinearSRGBColorSpace`
default but fragile under any future color-space change.

### What W13 strips (additions on top of W12's 11-entry list)

The `SHADER_CHUNKS_TO_EMPTY` list grows from 11 → 53
entries (+42). The new entries:

| Bucket                        | Chunks                                                                                                          | Approx body (B) |
|-------------------------------|-----------------------------------------------------------------------------------------------------------------|-----------------|
| Tone-mapping                  | `tonemapping_pars_fragment`, `tonemapping_fragment`                                                              | ~4 100 + 100    |
| Phong lighting (W9 stub)      | `lights_phong_fragment`, `lights_phong_pars_fragment`                                                            | ~300 + 1 250    |
| Toon lighting (W9 stub)       | `lights_toon_fragment`, `lights_toon_pars_fragment`                                                              | ~250 + 1 050    |
| Physical lighting (W9 stub)   | `lights_physical_fragment`, `lights_physical_pars_fragment`                                                       | ~3 000 + 5 200  |
| Transmission (PBR extra)      | `transmission_fragment`, `transmission_pars_fragment`                                                             | ~1 100 + 6 200  |
| Iridescence (PBR extra)       | `iridescence_fragment`, `iridescence_pars_fragment`                                                               | ~2 100 + 200    |
| Clearcoat (PBR extra)         | `clearcoat_pars_fragment`, `clearcoat_normal_fragment_begin`, `clearcoat_normal_fragment_maps`                    | ~600 total      |
| Map-feature chains (15 chunks)| alphamap / alphatest / alphahash / aomap / lightmap / emissivemap / bumpmap / normalmap / specularmap_pars /     | ~50–1 500 each  |
|                               |   metalnessmap / roughnessmap / displacementmap (`_fragment` + `_pars_*` pairs as available)                     |                 |
| Fog                           | `fog_fragment`, `fog_pars_fragment`, `fog_vertex`, `fog_pars_vertex`                                              | ~400 total      |
| Dithering + premultiplied     | `dithering_fragment`, `dithering_pars_fragment`, `premultiplied_alpha_fragment`                                   | ~500 total      |

`specularmap_fragment` is intentionally KEPT (sets
`specularStrength = 1.0` for `lights_lambert_fragment`'s
downstream read).

The `UNIFORMS_LIB_KEYS_TO_EMPTY` list grows from 5 → 14
entries (+9): `specularmap`, `envmap`, `aomap`, `lightmap`,
`bumpmap`, `normalmap`, `displacementmap`, `emissivemap`,
`fog`. Each holds the JS-side uniform values consumed by
`USE_<MACRO>`-guarded shader code; emptying yields `{}` and
the GLSL preprocessor strips the consumers at compile time
(no `material.<map>` set → no macro → no reference).

`UniformsLib.common` + `UniformsLib.lights` stay live —
`common` holds the universally-consumed `diffuse` /
`opacity` / `map` / `uv` uniforms, `lights` holds the
ambient + directional-light uniforms the autotable scene
actually attaches.

### Measured savings (K13 build)

| Source pass                                   | Before → After          | Δ                  |
|-----------------------------------------------|-------------------------|--------------------|
| `[module-strip]` (W9 + W10 carried fwd)      | 603,380 → 544,127       | −59,253            |
| `[shaderchunk-strip]` (W11 + W12 + **W13**)  | 544,127 → 448,370       | **−95,757 (W13 +43,320 vs. W12)** |
| `[uniformslib-strip]` (W12 + **W13**)        | 448,370 → 446,110       | **−2,260 (W13 +1,315 vs. W12)**   |
| `[material-strip]` (W9 carried fwd)          | 1,401,287 → 1,342,346   | −58,941            |
| Renderer chunk emitted                       | —                       | **406,635 B (≈ 397.10 kiB / 406.64 kB)** |

`dist-size.json:history[wave=K13]` records the new chunk
sizes. Vasquez's `three-renderer-480-hard.spec.ts` (W10),
`three-renderer-475-soft.spec.ts` (W11), and any
W13 follow-on guards all pass with very healthy margins.

### Risk + back-out

Identical structure to W11 + W12: each chunk's empty body
is safe **only** because the autotable scene never sets the
corresponding `USE_*` macro. The new chunks expand the
list of conditions that could trip a future regression:

| Trigger                                                          | Symptom                                                                                   | Roll-back                                                                                                              |
|------------------------------------------------------------------|-------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------|
| `renderer.toneMapping = THREE.ACESFilmicToneMapping`             | Tone-mapping no-ops (renders linear); no console error.                                   | Remove `tonemapping_*` from `SHADER_CHUNKS_TO_EMPTY`.                                                                  |
| `new MeshPhongMaterial(...)` un-stubbed + actually instantiated  | Console errors from missing `BlinnPhongMaterial`/`RE_Direct_BlinnPhong` declarations.     | Remove `lights_phong_*` from the list AND un-stub `MeshPhongMaterial` in W9's `STUB_MATERIALS`.                        |
| `new MeshToonMaterial(...)`                                      | Same as Phong, for the toon shader chain.                                                  | Remove `lights_toon_*` + W9 un-stub.                                                                                   |
| `new MeshPhysicalMaterial(...)` un-stubbed                        | Console errors from missing `PhysicalMaterial`/`RE_*_Physical` declarations.               | Remove `lights_physical_*` + W9 un-stub.                                                                               |
| `material.transmission > 0` / `iridescence > 0` / `clearcoat > 0` | Silent render glitch — feature does nothing.                                              | Remove `transmission_*` / `iridescence_*` / `clearcoat_*` from the list.                                               |
| `material.alphaMap` / `aoMap` / `bumpMap` / etc. set              | Silent render glitch — map sample yields default value; map's contribution disappears.    | Remove the offending `<feature>_fragment` / `<feature>_pars_fragment` from the list.                                   |
| `scene.fog = new THREE.Fog(...)`                                  | Fog uniforms unbound; depth gradient missing. Console may warn from uniform binding.       | Remove `fog_*` chunks from the list AND `fog` from `UNIFORMS_LIB_KEYS_TO_EMPTY`.                                       |

The unstripped baseline lives at W12's 448.65 kB; disable
the W13 entries first (cheap) and only un-stub the W9
material classes if the W13 strip alone doesn't fix the
regression.

### Trend ledger update

| Wave | Big chunk | Target | Result |
|------|-----------|--------|--------|
| W7   | 578.72 kB | <550 kB | ✅      |
| W8   | 531.86 kB | <540 kB | ✅      |
| W9   | 507.47 kB | <510 kB | ✅      |
| W10  | 497.44 kB | <500 kB ✅ / <480 kB ⚠️ | partial |
| W11  | 466.40 kB | <475 kB | ✅ (stretch met with ~9 kB margin) |
| W12  | 448.65 kB | <450 kB stretch / <460 kB acceptable | ✅ (stretch met with ~1.4 kB margin) |
| W13  | **406.64 kB** | **<440 kB stretch / <445 kB acceptable** | ✅ (stretch met with ~34 kB margin) |

Monotonic-decrease invariant holds for an 8th consecutive
wave (Vasquez's W7 trend gate). Cumulative drop from W6
baseline: **739.72 kB → 406.64 kB (−45.0 %)** over seven
waves.

### Hand-off to W14

W13 over-delivered (−42 kB vs the ~5-8 kB stretch
estimate). The chunk now sits comfortably in the <410 kB
band, well below the <440 kB W13 stretch.

Remaining strip candidates, all <5 kB individually:

- **`UniformsLib` PBR-extras** — `clearcoat`, `iridescence`,
  `sheen`, `transmission`, `anisotropy`, `dispersion`. The
  W13 audit found these are NOT top-level keys of
  `UniformsLib` (they live inside `UniformsLib.physical`
  via `ShaderLib.physical.uniforms`, which is W11-stripped
  via the `meshphysical_*` shader-string strip). Aggregate
  saving is therefore zero — the budget is fully captured by
  the existing surgery.
- **`logdepthbuf_*`** — `logdepthbuf_pars_fragment` +
  `logdepthbuf_fragment` + `_pars_vertex` + `_vertex`. The
  autotable doesn't enable `renderer.capabilities.logarithmicDepthBuffer`,
  so the body sits inside `#ifdef USE_LOGDEPTHBUF`. ~600 B
  total — modest but easy.
- **`clipping_planes_*`** — autotable doesn't use clipping
  planes. ~400 B total.
- **`normal_*`** chunks (`normal_pars_*`, `normal_fragment_*`)
  — these are KEPT in W13 because `lights_lambert_fragment`
  references `geometryNormal`. Could partially trim but the
  surface area is small (~400 B per chunk) and the risk
  is non-trivial.

Combined the W14 candidates above could shave another 1-2 kB.
The next round-number ceiling is **<400 kB**. Reaching it
will likely require the Phase L hand-roll spike (the W6
estimate at −200 to −300 kB still stands; a single Phase L
wave should clear <400 kB and approach <300 kB).

A more aggressive Phase L candidate (deferred): hand-roll
`three/src/*` imports + a custom `WebGLRenderer` wrapper.
Defer until the Phase K series closes.

## §10 — Wave 13: bundle-health CI workflow

W13 ships `.github/workflows/bundle-health.yml` — a per-PR
auto-report that builds the frontend, parses the latest
`dist-size.json` row, and posts a sticky PR comment with
the `three-renderer-big` size + delta vs the W12 baseline
(448,648 B).

### Trigger matrix

| Trigger                              | Behaviour                                                                                          |
|--------------------------------------|----------------------------------------------------------------------------------------------------|
| `pull_request` (frontend-touched)    | Builds, computes delta, posts sticky comment.  Hard-fails only when >500 kB.                       |
| `workflow_dispatch`                  | Manual run.  Same logic; no PR comment (no PR number).                                              |

### Pass / warn / fail rules

| Verdict | Condition                                                                                       | Reviewer action                              |
|---------|-------------------------------------------------------------------------------------------------|----------------------------------------------|
| `pass`  | Current ≤ W12 baseline + 1 % AND current ≤ 445 kB.                                              | None — within budget.                        |
| `warn`  | Current > W12 baseline × 1.02 (>2 % growth) OR current > 445 kB acceptable band.                 | Soft warning — confirm the change is intentional. Not a merge blocker. |
| `fail`  | Current > 500 kB (W10 retired ceiling).                                                          | Hard-fail — investigate before merging.      |

### Implementation notes

The workflow tags its `dist-size.json` row with
`WAVE_NAME=PR-${PR_NUMBER}` so per-PR runs don't mutate the
canonical `K13` / `K14` / … history rows on `main`. The
`append-dist-size.js` script is idempotent on the wave key
(updates an existing row in-place; appends a new one
otherwise), so the PR row gets re-written on each PR push
without growing the history unboundedly. After the PR
merges, the per-PR row stays in `main`'s `dist-size.json`
until the next `K<N>` build replaces the tail.

### PR-comment shape (example)

> ## ✅ Bundle health — **PASS**
>
> **three-renderer-big** (heavy renderer chunk) for this PR:
>
> | Metric            | Value            |
> |-------------------|------------------|
> | Current size      | 406.64 KB (406,635 B) |
> | W12 baseline      | 438.13 KB         |
> | Delta vs W12      | −41.03 KB (−9.36 %) |
> | W13 stretch       | <440.00 KB        |
> | W13 acceptable    | <445.00 KB        |

Sticky marker `<!-- bundle-health-report -->`; re-runs
edit the existing comment in place (peter-evans
`create-or-update-comment@v4` with `body-includes`).

### Risk + back-out

- **Build failures on the workflow runner** — the workflow
  reuses the W10 vite-disk-cache + `npm ci` pattern;
  failures are surface-level (network / npm registry / disk
  full). Roll back by reverting the workflow YAML.
- **False-positive `warn`** — the 2 % growth tolerance is
  intentionally tight; W13 routinely shrinks the chunk by
  several kB per wave, so even small feature additions can
  trip the warn threshold. The W13 contract treats warn as
  soft (not a merge blocker); reviewers can ignore if
  growth is documented in the PR description.
- **Hard-fail at >500 kB** — picked because the W10
  ceiling was 500 kB; any modern PR exceeding it is almost
  certainly an accidental regression (e.g. a misconfigured
  manualChunks split). Adjust by editing
  `HARD_FAIL = 500 * 1024` in the workflow's Node script.
