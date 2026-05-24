# Phase L renderer — implementation baseline (W15 hello-world)

> **Wave:** Phase K Wave 15 (foundation for Phase L W1)
> **Author:** Hicks (Frontend lead)
> **Status:** Live — W15 hello-world chunk emitted + measured.
> **Predecessor:** `docs/phase-l-renderer-spike.md` (W14 spike doc, Go on WebGL2 hand-roll).
> **Target:** Phase L W1 — full WebGL2 renderer replacement.
> **Bundle envelope:** 180–220 KB renderer chunk (vs. W14 hold-line 406.64 KB three.js renderer).

## §1 — What W15 delivered

Phase K Wave 14 closed with a **Go** recommendation on the hand-rolled
WebGL2 renderer.  W15 stands up the foundation:

| Asset | Path | W15 size |
|-------|------|----------|
| WebGL2 scaffold module | `src/frontend/autotable-src/src/renderer-webgl2/index.ts` | source 11.2 KB |
| Hello-world entry | `src/frontend/autotable-src/src/renderer-webgl2/hello.ts` | source 3.8 KB |
| Emitted chunk | `../autotable/renderer-webgl2.<hash>.js` | **6,237 B** (W15 baseline) |
| URL gate | `?renderer=webgl2-hello` (dev/spike only) | n/a |

The chunk is recorded under the `renderer-webgl2` key in
`src/frontend/autotable-src/dist-size.json`.  Vite's `manualChunks`
routes every file under `src/renderer-webgl2/` into this discrete
chunk via the W15 rule in `vite.config.ts`:

```ts
if (/[/\\]src[/\\]renderer-webgl2[/\\]/.test(id)) return 'renderer-webgl2';
```

## §2 — What's inside the W15 module

* **`createWebgl2Context(canvas)`** — guarded WebGL2 acquisition with
  the autotable's canonical pixel-format flags
  (`alpha=true`, `antialias=true`, `depth=true`,
  `premultipliedAlpha=false`, `powerPreference='high-performance'`).
* **`compileProgram(gl, vs, fs)`** — vertex + fragment shader compile /
  link with inline GLSL log so dev-tools console points at the
  offending shader.
* **`createTexturedQuadBuffers(gl, program)`** — single unit-square
  quad with position (xyz) + UV (st) attribute layout, indexed
  triangle list, VAO-backed for stateless redraws.
* **`createTexture(gl, source)`** — `HTMLImageElement` (or canvas) →
  WebGL2 texture with `LINEAR_MIPMAP_LINEAR` / `NEAREST` filtering +
  `CLAMP_TO_EDGE` wrapping (mahjong-tile-shaped defaults).
* **`identity4()` / `perspective4(fov, aspect, near, far)`** —
  4×4 column-major matrix helpers in the same convention as
  `three.PerspectiveCamera.updateProjectionMatrix()` so the Phase
  L migration can re-use the existing camera state without a
  coordinate-space flip.
* **`helloWorld(canvas, textureSource)`** — full end-to-end:
  context → program → quad → texture → draw call.  Returns a
  `{ redraw, dispose }` handle.

The hello-world entry (`hello.ts`) wires the above against a real
`/img/tiles-labels.auto.png` texture asset (the same mahjong-tile
label sheet the production renderer ships).  It mounts a 512×512
canvas under `#webgl2-hello-container` with a status line + resize
listener.

## §3 — How to run the hello-world

```bash
cd src/frontend/autotable-src
npm run build:vite

# Serve the built bundle.
nohup npx vite preview --host 127.0.0.1 --port 4173 \
  --strictPort --outDir ../autotable > vite-preview.log 2>&1 &
PREVIEW_PID=$!

# Open the dev URL.
xdg-open "http://127.0.0.1:4173/?renderer=webgl2-hello"
# … or curl-check that the chunk loads on demand:
curl -sI "http://127.0.0.1:4173/renderer-webgl2.$(jq -r \
  '.history[-1].chunks["renderer-webgl2"]' dist-size.json \
  >/dev/null 2>&1 && ls ../autotable/renderer-webgl2.*.js | \
  head -1 | sed 's|.*renderer-webgl2\.||;s|\.js$||').js" | head -5

kill "$PREVIEW_PID"
```

The `?renderer=webgl2-hello` guard in `src/index.ts` is:

```ts
if (window.location.search.includes('renderer=webgl2-hello')) {
  void import('./renderer-webgl2/hello').then((mod) => mod.mount());
}
```

— so the chunk **never loads on the lobby cold path** and adds
ZERO bytes to `autotable-src-eager` (the W15 +1.1 KB delta on
`autotable-src` came from the new `cost-forecast` action-router
plumbing, not the renderer-webgl2 scaffold).

## §4 — Bundle math vs. the Phase L envelope

| Wave | three-renderer-big | renderer-webgl2 (parallel) | Delta to Phase L target |
|------|--------------------|----------------------------|-------------------------|
| K14  | 406,635 B          | —                          | -180 to -210 KB still to cut |
| K15  | 406,635 B (hold)   | **6,237 B (hello world)**  | 200 KB headroom remaining in the 180–220 KB envelope |

The W15 hello-world is **3 % of the Phase L envelope** (6.2 KB of
~200 KB target).  This is comfortably below the inflection point
where the matrix-math + draw-loop scaffold dominates the chunk.
Phase L W1 should anchor against this baseline:

* Adding the tile mesh graph (geometry + per-tile draw call wiring)
  is the next single-largest insertion (~10–15 KB estimated).
* Adding lighting (one ambient + one directional, Lambert model in
  the fragment shader) is ~2 KB of GLSL + ~1 KB JS.
* Adding the camera controller (orbit + pan + zoom) is ~3 KB.
* Adding raycaster + picking against the tile mesh is ~4–5 KB.
* Asset graph (re-using the W14 `gltf-loader.<hash>.js` chunk for
  `models.auto.glb`) is **zero new bytes** in this chunk — already
  split.

Cumulative W1 envelope estimate: **6 + 15 + 3 + 3 + 5 = 32 KB** —
still well below the 180 KB lower bound.  The remaining envelope
is spent on shader graphs (the second-most-expensive class in the
estimate; three.js's `ShaderChunk` graph is one of the larger
fractions of the heavy chunk we're replacing).

## §5 — Three.js boundary (what stays vs. what goes)

The Phase L migration plan keeps three.js in flight for the
remaining Phase L waves so the swap is incremental, not
big-bang:

* **Stays during Phase L W1–W3:** the production renderer chunk
  (`three-renderer.<hash>.js`, 406 KB) continues to ship.  All
  game rendering on production URLs goes through it.
* **Stays permanently (no plan to replace):**
  `gltf-loader.<hash>.js` (44 KB).  The Phase L renderer
  consumes the same `models.auto.glb` asset; rolling our own
  GLTF parser is out of scope.
* **Goes during Phase L W4 (planned):** the production
  `?game=<id>` boot path swaps from `three-renderer` to
  `renderer-webgl2`.  three.js is removed from `node_modules`
  + `package.json`; the `three-renderer.*` chunk disappears
  from `dist-size.json`.

The W15 chunk is the foundation; the W1–W3 expansions land on
top of it.  After W3, the renderer-webgl2 chunk should be at
roughly 100–130 KB (full feature parity with three-renderer
sans GLTF).  W4 ships the swap once parity is locked.

## §6 — Risk register (W15-known)

1. **Browser support floor.** WebGL2 is universally available on
   evergreen browsers (Chrome 56+, Firefox 51+, Safari 15+).  Of
   the autotable's analytics-tracked sessions in the W14 audit,
   100 % had WebGL2.  No fallback needed.
2. **MSAA / anti-aliasing parity.** `antialias: true` on the
   context request gives the browser-default MSAA level.  The
   W14 three.js chunk explicitly disabled MSAA in favour of
   FXAA-style post-processing (smaller chunk).  Phase L W3 will
   need to decide whether to match the FXAA path (smaller +
   familiar to existing screenshots) or go with browser MSAA
   (cleaner, but baseline visual-regression PNGs may drift).
3. **Texture-atlas re-encoding.** Three.js performs implicit
   colour-space conversion when loading tile face textures.  The
   W15 hello-world matches the three.js convention via
   `gl.UNPACK_FLIP_Y_WEBGL=true` + `gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL=false`,
   so tile faces should render upright with no shader-side flip.
   This needs a Phase L W2 visual-regression sweep against the
   W14 baseline captures.
4. **Picking / raycaster math.** Three.js's `Raycaster` does
   ray-triangle intersection in object-space against the mesh's
   buffer geometry.  Phase L W2 will need to port the same math
   (it's ~50 lines per primitive type) — Möller-Trumbore for
   tiles, sphere-ray for dice (or just bounding-box if dice
   picking is rarely user-facing).
5. **Animation scheduler.** Three.js doesn't ship an animation
   system that we use (we drive frame-by-frame transforms
   ourselves via `requestAnimationFrame` in `world.ts`).  No
   scheduler dependency to port.

## §7 — Phase L W1 hand-off

W1 owners (Hicks):

1. Land the tile-mesh graph (~15 KB) as the second discrete
   addition to `renderer-webgl2/`.  Suggested layout:
   * `renderer-webgl2/tile-mesh.ts` — geometry builder.
   * `renderer-webgl2/tile-material.ts` — lit-tile shader pair
     (Lambert in the fragment shader).
   * `renderer-webgl2/scene.ts` — scene graph (replaces
     `THREE.Scene` + `THREE.Group`).
2. Wire the W1 entry behind the existing
   `?renderer=webgl2-hello` URL guard (or a new
   `?renderer=webgl2-tile-grid` if W1 wants a dedicated route).
3. Append a W1 entry to **this doc**'s §4 table with the new
   chunk size.  The trajectory we're plotting is the analogue
   of the §1 table in `phase-l-renderer-spike.md` for the
   replacement renderer.
4. **Hold the W14 three-renderer-big at 406 KB** for the W1
   build — no production-renderer changes during the W1 wave.

## §8 — References

* `docs/phase-l-renderer-spike.md` — W14 Go-decision spike doc.
* `src/frontend/autotable-src/dist-size.json` — chunk-size ledger.
* `src/frontend/autotable-src/src/renderer-webgl2/` — W15 source.
* `src/frontend/autotable-src/vite.config.ts:manualChunks` — W15
  chunk-split rule.
