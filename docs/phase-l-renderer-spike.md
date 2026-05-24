# Phase L renderer spike — sub-300 KB feasibility

> **Wave:** Phase K Wave 14 (hand-off to Phase L W1)
> **Author:** Hicks (Frontend lead)
> **Status:** Exploratory feasibility report.  Not a deliverable.
> **Recommendation:** **Go, with the WebGL2 minimal-abstractions
> approach** (estimated 200–230 KB renderer chunk = −180 to −210 KB
> below the W13 hold-line).

## §1 — Where we are

| Wave | three-renderer-big | Δ vs. previous | Cumulative Δ |
|------|--------------------|----------------|--------------|
| K6   | 738,496 B (~720 KB)| baseline       | —            |
| K7   | 549,648 B (~537 KB)| −188,848 B     | −25.6 %      |
| K8   | 522,041 B (~510 KB)| −27,607 B      | −29.3 %      |
| K9   | 510,205 B (~498 KB)| −11,836 B      | −30.9 %      |
| K10  | 466,395 B (~455 KB)| −43,810 B      | −36.8 %      |
| K11  | 466,395 B (~455 KB)| 0              | −36.8 %      |
| K12  | 448,648 B (~438 KB)| −17,747 B      | −39.2 %      |
| K13  | 406,635 B (~397 KB)| −42,013 B      | −44.9 %      |
| K14  | 406,635 B (~397 KB)| 0 (hold)       | −44.9 %      |

The W6→W13 trajectory drove the renderer chunk down by 332 KB
(−44.9 %) through a combination of tree-shaking wins, dynamic-
import splits (GLTFLoader peel, scene-effects peel), and the
W12/W13 ShaderChunk + UniformsLib emptying passes.  Phase K
exhausted the within-three.js levers; W14 holds the line while
this spike investigates the larger play.

The Phase L target — **<300 KB** — requires another ~106 KB cut.
That's beyond what any remaining within-three.js trick can
deliver:

  * Further ShaderChunk emptying yields diminishing returns (most
    remaining strips save < 400 B each; the chunks we left alone
    in W13 are the ones that are NOT guarded by `#ifdef` macros
    and would cause black-canvas regressions if emptied).
  * Further `UniformsLib` emptying is bounded by the same macro-
    guard story — the W13 strip already targeted every key
    measured to be unused on the autotable's lambert + mesh-basic
    materials.
  * Conditional UMD splitting (e.g. tree-shaking the renderer
    module by hand) cannot delete classes already used by Game
    (Mesh, MeshLambertMaterial, PerspectiveCamera, Scene, WebGL-
    Renderer, AmbientLight + DirectionalLight, Texture, Geometry).

We are now bounded by **what three.js ships in its core graph**,
not by what we configure.  To go lower we have to either:

  1. Replace three.js with a hand-rolled WebGL renderer
     (Option A — this spike's recommendation), or
  2. Vendor a fork of three.js with the renderer / loader graph
     re-roofed (Option B — too high a maintenance tax for the
     autotable team), or
  3. Switch to a different lightweight renderer (Option C — e.g.
     `regl`, `picogl`, `twgl` — non-trivial port + each ships its
     own ~30-60 KB graph; net win ~50 KB, not enough to clear
     <300 KB on its own).

## §2 — What can be cut by replacing three.js with raw WebGL2

The autotable renderer uses a narrow slice of three.js:

| three.js feature        | Used for                                              |
|-------------------------|-------------------------------------------------------|
| `WebGLRenderer`         | The single canvas + frame loop                        |
| `Scene` + `Group`       | Object hierarchy (table + tiles + dice + sticks)      |
| `Mesh`                  | Tile / dice / stick / wall surfaces                   |
| `MeshLambertMaterial`   | All surface materials                                 |
| `MeshBasicMaterial`     | The center pad + the variant badge UI                 |
| `AmbientLight` + `DirectionalLight` | Lit shading on all meshes                 |
| `PerspectiveCamera`     | Default camera                                         |
| `OrthographicCamera`    | The mini overhead view (move-log panel)                |
| `Texture` + `CanvasTexture` | Tile face textures, the labelled-faces texture    |
| `BufferGeometry`        | Per-tile / dice / stick / table mesh geometry          |
| `GLTFLoader`            | Single asset load (`models.auto.glb`) — already split  |
| `Object3D.lookAt`       | Tile flipping rotation                                  |
| `Raycaster`             | Mouse picking against tiles                            |
| `Vector3` + `Quaternion`+ `Matrix4` | Math throughout                            |

That's it.  No skeletal animation, no morph targets, no PBR /
clearcoat / iridescence / transmission, no environment maps, no
shadow maps, no post-processing pipeline, no XR, no `ShaderChunk`
chains beyond the ones a Lambert material needs internally, no
tone mapping (we render at gamma-1.0 LDR), no encoding/colour-space
machinery (we hardcoded `LinearSRGBColorSpace` in W11), no `Audio`
helpers, no `Pass` graph, no UI helpers, no examples/jsm/loaders
beyond `GLTFLoader`, no examples/jsm/controls (we own our own
mouse), no examples/jsm/effects, no `BufferGeometryUtils`, no
`AnimationMixer`.

A minimal WebGL2 implementation of the listed slice would need:

  * One `WebGL2RenderingContext` wrapper (~3 KB) — shader compile,
    program link, attribute binding, frame loop, sized framebuffer.
  * One Lambert-equivalent vertex+fragment shader pair (~2 KB
    glsl source, ~1.5 KB after minification) — uniform-vs-attribute
    layout for normals, ambient + 1 directional light, modulated
    by per-instance colour and an albedo texture.
  * One unlit (basic-equivalent) shader pair (~1 KB) — for the
    center pad + badge surfaces.
  * One scene-graph (`Node` with parent + children + local matrix
    + world matrix invalidation) — ~2 KB after minify.
  * One BufferGeometry-equivalent (`Geometry` interleaved
    attribute buffer + indices + draw-call recipe) — ~2 KB.
  * One CanvasTexture-equivalent (`Texture` wrapping a
    `WebGLTexture`, with the W11 cube-of-tile-faces packing logic
    inlined) — ~2 KB.
  * Math primitives — `mat4`, `vec3`, `quat`.  Cribbed from
    `gl-matrix` (~4-5 KB tree-shaken to just the operations we
    use).  Alternatively hand-roll the dozen specific ops we need
    (~1.5 KB).
  * `Raycaster`-equivalent — ray-triangle picker over our specific
    tile mesh shape.  We don't need general BVH traversal; the
    tile picker can iterate the ~150 active tiles in a frame
    (~1 KB).
  * `GLTFLoader`-equivalent — already split into a 44 KB chunk.
    Phase L can keep it as-is (it's a separate chunk loaded only
    on the asset-fetch path; the renderer chunk doesn't carry it).

**Estimated raw chunk size after replacement: 180-220 KB
(uncompressed), 60-75 KB gzipped.**  The W13 chunk gzip is
108.53 KB; the spike target gzip is 60-75 KB.

The estimate breaks down as:

  * The `Game` / `World` / `Client` / asset-loader logic — already
    weighted in the chunk independently of three.js — accounts for
    about 120-130 KB of the current 406 KB.  This is the
    autotable's own gameplay code and is fixed cost.
  * The WebGL2 minimal layer adds the new ~20 KB of code listed
    above.
  * The three.js core that disappears: 406 - 130 - 20 ≈ 256 KB
    saved on the upper estimate, 286 KB on the lower estimate.

**Bottom-line estimate: 180-220 KB renderer chunk after Phase L
replacement.**  At 200 KB midpoint, that's 206 KB below the W13
baseline (−51 %) and 100 KB below the <300 KB Phase L target.

## §3 — Risk assessment

### §3.1 — Regression surface

| Risk                                 | Severity | Mitigation                                         |
|--------------------------------------|----------|----------------------------------------------------|
| Tile texture sampling regression     | High     | Pixel-for-pixel screenshot diff against W14 baselines (the W14 "real captures" methodology lets us catch this immediately at a per-screen level — see `docs/frontend-pwa-audit.md §7.1`). |
| Raycaster picking false positives    | Medium   | We pick against ~150 tile meshes / frame; current three Raycaster does the same.  A hand-rolled ray-triangle test (no BVH) is sufficient at this object count.  Add a per-tile picking spec under `tests/e2e/`. |
| Lambert shading delta                | Medium   | The ambient+directional Lambert model is well-defined; matching three.js's `MeshLambertMaterial` requires matching their exact gamma-curve, light intensity scaling, and the `LinearSRGBColorSpace` choice we already pinned in W11.  Capture pixel-diff baselines for "lit" surfaces. |
| `Object3D.lookAt` parity             | Low      | Library code, easy to port faithfully via `Matrix4.lookAt`. |
| `Quaternion.slerp` parity            | Low      | Used in tile-flip animation.  Standard slerp formula matches three.js exact behaviour. |
| Asset loader (GLTF) breakage         | None     | `GLTFLoader` chunk is independent; Phase L doesn't touch it. |
| Browser support drop (WebGL2 only)   | Low      | WebGL2 is supported by >97 % of users tracked by `caniuse` as of 2025-Q4.  The W13 dashboard hits 99 %+ on the autotable's real user pool; the chunk-size win justifies dropping the fallback path. |
| Maintenance burden (long-term)       | High     | This is the real cost.  Hand-rolled WebGL means each new requirement (e.g. shadow maps if W shipped a "spotlight" mode) is on the autotable team to implement.  Today's slice is bounded by the Phase J game-design freeze; a future "PBR materials for premium tile sets" feature would be expensive. |

### §3.2 — Maintenance burden trade-off

Replacing three.js puts ~600 lines of bespoke WebGL2 + GLSL code
on the autotable team.  In exchange we delete a ~200 KB dependency
+ the security-patch read-tax that ships with it.  The autotable's
rendering needs have been stable since Phase G; the marginal
maintenance burden is bounded.

The honest answer: this trade is justified if and only if Stephen's
roadmap continues the "no PBR, no shadow maps, no environment
maps" stance.  If a Phase M / Phase N introduces a feature that
needs a feature three.js ships out of the box (e.g. instanced
rendering of tile stacks for performance — three has
`InstancedMesh`; we'd have to port `gl.drawElementsInstanced` to
our scene graph manually), the cost of catching up could exceed
the chunk-size win.

### §3.3 — Bundle-health workflow integration

The W13 `.github/workflows/bundle-health.yml` workflow is the
guardrail.  Phase L W1 should:

  1. Land the WebGL2 renderer behind a feature flag
     (`USE_WEBGL2_RENDERER=true`).
  2. Run the bundle-health workflow against the flagged build.
  3. Verify the renderer chunk lands below 300 KB.
  4. Verify all W14 visual-regression baselines pass with the new
     renderer engaged.
  5. Verify the W11 `manifest-screenshots-visual.spec.ts` no-ops
     (no manifest-side change is needed).
  6. Cut over the production build to the flagged code path in W2.
  7. Delete the three.js dependency + the W7-W13 shader-chunk
     emptying logic in vite.config.ts in W3.

## §4 — Go / no-go recommendation

**Go.**  The Phase L W1 spike should advance to a feature-flagged
implementation.  Specifically:

  * **Week 1:** Hand-rolled WebGL2 renderer + scene-graph layer.
    Stub Lambert + Basic shaders.  Render an empty table behind
    the flag.
  * **Week 2:** Tile / dice / stick mesh + texture pipeline.  Stand
    up the picking + flip + slerp paths.  Visual-diff against W14
    baselines.
  * **Week 3:** Production cut-over.  Delete three.js.  Confirm
    bundle-health verdict = pass.  Validate <300 KB chunk landing.

If the Week 2 visual-diff exceeds 2 % pixel drift on any baseline,
escalate to Stephen for a go/no-go on whether to extend the spike
to Week 4 or roll the flag back.

### §4.1 — Pre-conditions for Phase L W1 kickoff

  * W14 hold-line preserved (renderer chunk ≤ 406.64 KB) — **done**.
  * Visual-regression real-captures baselines committed and
    enforceable — **done** (W14 §7.1, `tests/e2e/__screenshots__/`).
  * Vasquez's setContent / `snapshotPathTemplate` spec fix landed
    so the visual-diff is observable in CI — **pending W14
    Vasquez lane** (handed off in `docs/frontend-pwa-audit.md
    §11.5`).
  * Bishop's W14 listing endpoints stabilised so the W14 lobby
    overlays (`?action=bracket`, `?action=replays`, `?action=
    admin-cost`) aren't churning while the renderer cut-over
    happens — **expected to land alongside W14 bring-up**.

## §5 — Alternatives we considered and rejected

1. **Pre-built three.js bundle (no examples/jsm)** — we already
   peel `examples/jsm/loaders/GLTFLoader` into its own chunk.
   There's no more low-hanging fruit on the examples/jsm path
   that lives in the renderer chunk.
2. **Custom three.js fork** — would need to maintain a vendored
   version of three.js + patches.  Net chunk-size win ~80 KB
   (insufficient to clear <300 KB on its own) at a maintenance
   tax higher than the WebGL2 hand-roll.
3. **Switch to `regl` / `picogl` / `twgl`** — would each ship a
   ~30-60 KB graph of its own + require porting Game / World /
   Client logic to the target API.  Net chunk-size win ~50-70 KB
   (insufficient to clear <300 KB).
4. **Server-side rendering** — out of scope; the autotable is an
   interactive 3D experience, not a content site.
5. **Wait for three.js to ship a tree-shake-friendly v0.180+** —
   the upstream three.js team's stance is that the namespace
   re-export pattern in `three.core.js` is intentional and won't
   be removed.  No upstream win is reasonably available.

## §6 — Cross-references

  * `docs/frontend-three-budget.md` §9 — W13 PMREMGenerator deeper
    strip write-up + trend ledger.
  * `docs/frontend-build-tooling.md` — Parcel→Vite swap rationale.
  * `docs/frontend-pwa-audit.md` §7.1 — W14 real-captures
    methodology (the diff-baseline-of-record for Phase L W1).
  * `.github/workflows/bundle-health.yml` — per-PR chunk-size
    guardrail.
  * `src/frontend/autotable-src/vite.config.ts` `SHADER_CHUNKS_TO_EMPTY`
    + `UNIFORMS_LIB_KEYS_TO_EMPTY` — W13 within-three.js levers,
    document of "what's left of three.js after W14".

---

*Phase K Wave 14 — Hicks (Frontend).  Feasibility report only;
implementation begins Phase L W1.  Recommendation: **Go**.*
