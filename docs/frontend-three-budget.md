# Three.js bundle budget — Phase K Wave 6

The `three-renderer.<hash>.js` chunk (split out of `scene-shell` in
Wave 5) is the heaviest single asset in the autotable bundle. This
document tracks which three.js subpackages we currently pull and what
was removed in the Wave-6 tree-shake sweep.

## Budget targets

| Chunk                         | W5 size      | W6 target | W6 actual | Notes                                  |
|-------------------------------|--------------|-----------|-----------|----------------------------------------|
| `three-renderer.<hash>.js`*   | 724 kB       | <700 kB   | see CI    | Sum of both sub-chunks parcel emits    |
| `scene-shell.<hash>.js`       | 2.3 kB       | <5 kB     | 2.3 kB    | Thin coordinator, three.js-free        |
| `scene-effects.<hash>.js`     | 60 kB        | <80 kB    | ~60 kB    | GameUi modal graph + MoveLog           |

*Parcel emits the renderer as two sub-chunks (~145 kB + ~579 kB). The
budget compares the sum.

## In-use three.js surface (verified)

Anything below is statically imported and reachable by the renderer
boot path.  Removing them would break the WebGL canvas.

| Module                                                  | Used by              | Reason                                                                                            |
|---------------------------------------------------------|----------------------|---------------------------------------------------------------------------------------------------|
| `three` (core: Scene / Camera / WebGLRenderer / etc.)   | `main-view.ts`       | Top-down + perspective renderer, camera, lights.                                                  |
| `three/examples/jsm/postprocessing/EffectComposer.js`   | `main-view.ts`       | Post-processing pipeline owner (mandatory for `OutlinePass`).                                     |
| `three/examples/jsm/postprocessing/RenderPass.js`       | `main-view.ts`       | First pass in the composer — bare scene render.                                                   |
| `three/examples/jsm/postprocessing/OutlinePass.js`      | `main-view.ts`       | Yellow tile-selection outline. Removing breaks the discard-prompt UX.                             |
| `three/examples/jsm/loaders/GLTFLoader.js`              | `asset-loader.ts`    | Loads `models.auto.glb` (table, tile geometry).                                                   |
| `three/examples/jsm/utils/BufferGeometryUtils.js`       | `object-view.ts`     | `mergeGeometries` consolidates the tile-tray geometry into a single draw call.                    |

## Removed in Wave 6

| Module                                                     | Removed from   | W5 cost (min)  | Replacement / rationale                                                                                                                                                                |
|------------------------------------------------------------|----------------|----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `three/examples/jsm/libs/stats.module.js` (FPS overlay)    | `main-view.ts` | ~3 kB          | **Lazy + opt-in.** The dev FPS overlay is now dynamic-imported only when the URL carries `?stats=1`. Production users no longer ship the panel they could not see.                     |

The stats overlay is the only practical W6 cut. Other Wave-5
candidates surveyed and **rejected**:

- `OutlinePass` — explored "manual outline pass via stencil" — too
  risky pre-Phase-L, current outline is core UX.
- `EffectComposer` / `RenderPass` — only used in combination with
  `OutlinePass`; cannot be removed independently.
- `GLTFLoader` — model load is the asset pipeline foundation.
- `BufferGeometryUtils.mergeGeometries` — switching to per-mesh
  rendering would inflate draw-call count and regress FPS on
  low-end Chromebooks (Vasquez's E2E machine class).

## How to re-audit

```bash
cd src/frontend/autotable-src
npm run build
# Inspect emitted chunks
du -k ../autotable/three-renderer.*.js
# Visual map (only if @parcel/reporter-bundle-analyzer installed)
npx parcel build index.html --reporter @parcel/reporter-bundle-analyzer --dist-dir ../autotable
```

A future sweep should also consider:

- Pulling `OutlinePass` shader source into our own minimal post-pass
  (saves the parts of OutlinePass.js we don't use — selectiveBloom,
  the blur kernel, the patternTexture path).
- Replacing the GLTFLoader with a pre-converted binary tile mesh
  packed alongside the bundle (eliminates the GLTF parser).

These are Phase-L scope and intentionally not in W6.

## SW pre-cache implications

`scripts/generate-sw-manifest.js` carries `THREE_RENDERER_RE` so the
service worker pre-warms both sub-chunks at install time.  Any future
chunk-name change must update the regex (see
`src/frontend/autotable-src/scripts/generate-sw-manifest.js`).
