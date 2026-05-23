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
