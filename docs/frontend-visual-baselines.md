# Committed WebGL visual baselines — flat + perspective (#119 / WP-D)

This is the deterministic, **blocking** visual-regression gate for the two Changsha
view modes. It replaces the statistics-only approach rejected in the P1-10 review
with committed `toHaveScreenshot` baselines, while keeping the statistical
framebuffer checks as complementary guards.

- Spec: `src/frontend/autotable-src/tests/e2e/view-mode-visual-baseline.spec.ts`
- Baselines: `src/frontend/autotable-src/tests/e2e/__screenshots__/view-mode-visual-baseline.spec.ts/{four-player-perspective,four-player-flat}.png`
- Project: `visual` in `tests/e2e/playwright.config.ts` (gated behind `E2E_VISUAL_GATE=1`)
- Blocking CI job: `.github/workflows/view-visual-gate.yml`
- Complementary statistical gate (kept): `tests/e2e/view-mode-toggle.spec.ts`

## Why this is deterministic (and can therefore block)

WebGL rasterises differently across GPUs / software renderers, which is why the
legacy `playwright-visual-regression` job is non-blocking. This gate removes every
source of variance:

1. **Software WebGL** — ANGLE → SwiftShader (`--use-gl=angle --use-angle=swiftshader
   --enable-unsafe-swiftshader`). No GPU, no driver variance — pure CPU raster.
2. **Pinned browser + fonts + OS** — everything runs inside the pinned
   `mcr.microsoft.com/playwright:v1.60.0-jammy` container, both to **generate** the
   committed baselines and in the **blocking CI job**. Same chromium build, same
   font packages, same libc.
3. **Pinned surface** — fixed `960×540 @ deviceScaleFactor 1` viewport,
   `--force-color-profile=srgb`, subpixel text off, `animations: 'disabled'`.
4. **Deterministic scene** — the scene is the upstream *local-deal* renderer
   (`World.deal('HANDS')`, **not** a WS backdoor). `Math.random` is replaced in-page
   with a seeded mulberry32 PRNG **before** the deal, so `utils.shuffle` (and the
   dice roll) produce the same tiles every run; the centre HUD dice are additionally
   pinned to a fixed `[3,4]` face. All DOM chrome (lobby panel, Move Log with its
   wall-clock timestamps, settings, HUD) is hidden so only the pure WebGL canvas is
   captured — no timestamp / no shell drift.

## The 2% tolerance — justified by evidence

Repeat generations and compares in the pinned container are **byte-identical**:

- The baseline PNGs generated locally (host SwiftShader) and re-generated inside the
  pinned container are byte-for-byte identical (same size + sha256).
- 6 independent compare runs (3 host + 3 container) passed.
- A **0-pixel** strict compare passed in the container:
  `VISUAL_STRICT=1 … --project=visual` → `maxDiffPixels: 0` → **passed**.

So the *observed* run-to-run difference is **0 px**. The gate nonetheless uses
`maxDiffPixelRatio: 0.02` as a conservative safety margin to absorb sub-pixel AA
drift should the pinned base image pick up a minor chromium/mesa patch within the
`v1.60.0-jammy` tag. Any real render regression (black canvas, untextured tiles,
NaN-poisoned geometry, a no-op camera swap) changes far more than 2% of pixels and
fails the gate; the complementary statistical asserts in the same spec also catch a
blank/blown-out frame before the pixel compare.

## Scenes (meaningful, not shells)

Both captures are a FOUR_PLAYER deal — four tile walls, the dice-bearing centre HUD,
and a full face-up hand — in the two projections:

- `four-player-perspective.png` — default `PerspectiveCamera`.
- `four-player-flat.png` — top-down `OrthographicCamera` (the "flat" view).

The spec also asserts the two projections raster to measurably different frames, so
a broken/no-op toggle fails even though each individual baseline might still pass.

## Regenerating baselines (intentional render changes only)

Regenerate in the pinned container so the bytes match CI. From a checkout with the
frontend bundle built and the backend serving it at `http://127.0.0.1:5114/autotable/`:

```bash
cd src/frontend/autotable-src
docker run --rm --network host --user "$(id -u):$(id -g)" \
  -e HOME=/work -e E2E_VISUAL_GATE=1 \
  -e E2E_BASE_URL=http://127.0.0.1:5114/autotable/ \
  -v "$PWD":/work -w /work \
  mcr.microsoft.com/playwright:v1.60.0-jammy \
  npx playwright test --config tests/e2e/playwright.config.ts \
    --project=visual view-mode-visual-baseline.spec.ts --update-snapshots
```

Commit the regenerated PNGs **in the same commit** as the render change. Keep each
baseline under the 512 KB `check-added-large-files` pre-commit cap (the 960×540
viewport keeps them ~290–440 KB).
