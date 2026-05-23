# Three.js bundle budget — Phase K Wave 7

The `three-renderer.<hash>.js` chunk (split out of `scene-shell` in
Wave 5) is the heaviest single asset in the autotable bundle. This
document tracks which three.js subpackages we currently pull and
what was removed wave-by-wave.

## Budget targets

| Chunk                         | W5 size      | W6 actual | W7 target | W7 actual | Notes                                  |
|-------------------------------|--------------|-----------|-----------|-----------|----------------------------------------|
| `three-renderer.<hash>.js`*   | 869.6 kB     | 838.8 kB  | <650 kB   | **648.07 kB** ✅ | Sum of both sub-chunks            |
| `three-renderer.<hash>.js` (big) | 724.7 kB  | 739.7 kB  | <600 kB   | **578.72 kB** ✅ | Single largest chunk              |
| `scene-shell.<hash>.js`       | 2.3 kB       | 2.33 kB   | <5 kB     | 2.34 kB   | Thin coordinator, three.js-free        |
| `scene-effects.<hash>.js`     | 60 kB        | ~60 kB    | <80 kB    | 59.04 kB  | GameUi modal graph + MoveLog           |

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
