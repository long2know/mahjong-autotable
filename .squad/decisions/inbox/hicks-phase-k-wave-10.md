# Hicks — Phase K Wave 10 decisions memo

Branch: `stlong/phase-k-wave-10-bringup`
Author: Hicks (Frontend)
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

6 deliverables per the W10 directive:

1. **Commentary panel — TileReference adoption + `source` on
   dispatch.** `commentary-panel.ts` now consumes Bishop's
   canonical `TileReference = { tileId, suit, rank }` shape
   (was bare strings in W9). Chip clicks dispatch
   `mahjong:highlight-tile` on `document` with
   `{ tileId, source: 'commentary-panel' }`. The renderer
   threads `ref.suit` + `ref.rank` into `data-tile-suit` /
   `data-tile-rank` attributes alongside `data-tile-id`. A
   W9-string fallback is retained for one wave via
   `parseTileIdShape()` (planned removal: W12, after Bishop's
   backend ships two consecutive deploys on the object shape).
   Canonical contract pinned in
   `docs/contracts/commentary-tile-ref.md` (NEW).

2. **PWA Builder CI workflow.** `.github/workflows/pwa-audit.yml`
   (NEW) runs on push to `stlong/**` + `main`, every PR
   against `main`, and a nightly cron at 03:30 UTC.
   - `build` → `manifest-lint` → `lighthouse` → `pr-comment`.
   - `scripts/manifest-lint.js` (NEW) replays LH11 PWA
     installability preconditions; geometric-mean across four
     sub-scores. Gate `pwaScore ≥ 0.90`. **W10 local baseline:
     1.000.**
   - `scripts/render-pwa-comment.js` (NEW) renders a Markdown
     PR comment with sticky marker; updates in place via
     `peter-evans/create-or-update-comment@v4`.
   - Vite cache restored via `actions/cache@v4`.
   - actionlint v1.7.7 + python3 YAML parser both pass clean.
   - LH13 thresholds (perf 0.85 / a11y 0.95 / bp 0.95 /
     seo 0.95 / agentic-browsing 0.50) carried over from W9.

3. **`partitionDoubleElim` removal + Parcel teardown.**
   - `bracket-renderer.ts` shrinks from 646 → 600 lines:
     `partitionDoubleElim` + `PartitionedMatches` deleted,
     replaced with a W10 comment explaining the W6→W9 history.
   - `package.json`: `build:parcel` script + 4 Parcel devDeps
     (`parcel`, `@parcel/packager-raw-url`,
     `@parcel/transformer-image`,
     `@parcel/transformer-webmanifest`) removed.
   - `package-lock.json` regenerated: **−636 packages**
     (most of the saved time on cold CI). Tree is now Vite +
     Lighthouse only.

4. **PWA manifest gap-fills.** `manifest.webmanifest`:
   - Added `id: "/?source=pwa"`, `lang: "en"`, `dir: "ltr"`,
     `description: "Mahjong Autotable — Changsha + Chinese
     variants"`.
   - Added `screenshots[]` (3 entries: 1024×768 lobby + table
     wide; 768×1024 mobile narrow) — placeholder PNGs
     generated via ImageMagick; pixel-quality replacement is
     queued for W11 once cinematic-camera work lands.
   - Added `shortcuts[]` (New game / Spectate / Tournament
     dashboard).
   - `copyStaticAssets()` in `vite.config.ts` extended to
     copy the three screenshots into the dist root.

5. **PMREMGenerator strip — partial win, blocker documented.**
   `three-renderer-big`: 507.47 → **497.44 kB** (−10.03 kB,
   −1.97%). **Stretch ceiling MISSED:** spec wanted <480 kB
   (−28 kB needed).
   - **What worked:** class-body strip of `PMREMGenerator`
     (constructor pre-initialises ten private slots three's
     renderer reads off the instance; public methods become
     no-ops). Yielded the full 10 kB win.
   - **What didn't:** 7 helper-function stubs
     (`_getBlurShader`, etc.) yielded **zero additional
     bytes** — Rollup was already tree-shaking them once the
     class body was gutted. Retained as defence-in-depth for
     future three.js bumps.
   - **The blocker:** remaining bloat lives in three named
     ShaderChunk barrel exports (`cube_uv_reflection_fragment`
     ~3-4 kB; `fragment$g` background shader; `fragment$5`
     PBR shader). Rollup cannot strip individual properties
     of a named-export object literal without breaking the
     barrel. Three live references (Lambert
     `#include <cube_uv_reflection_fragment>`; `WebGLBackground
     .render` unconditional call; `WebGLPrograms.acquireProgram`
     string-keyed dispatch) keep them resident.
   - **Back-out rationale (per directive's explicit
     allowance):** the partial win is monotonic-decrease-
     compatible with Vasquez's W7 invariant and the W9 <510 kB
     gate; the remaining ~17 kB requires either GLSL shader
     surgery, a `WebGLBackground` stub, or a hot-path
     `acquireProgram` patch — none safe for a one-wave bring-up
     without a Playwright smoke pass. Queued to W11.
   - Full autopsy + trend table in
     `docs/frontend-three-budget.md §6`.

6. **Vite build cache.** `cacheDir = resolve(__dirname,
   '.vite')` in `vite.config.ts` puts the dep pre-bundle and
   transform cache at `src/frontend/autotable-src/.vite/`
   (next to source — wipeable without nuking
   `node_modules`). `.gitignore` excludes it. CI cache key is
   `hashFiles('package-lock.json', 'vite.config.ts')` —
   either changing busts the cache; source-only PRs hit warm
   (~3× speedup measured locally and projected on
   ubuntu-latest).

## Trend ledger

`three-renderer-big`: 740 → 579 → 531.86 → 507.47 →
**497.44 kB** — Vasquez's monotonic-decrease invariant holds
for a **5th consecutive wave**.

| Wave | Big chunk | Target  | Result |
|------|-----------|---------|--------|
| W7   | 578.72 kB | <550 kB | ✅      |
| W8   | 531.86 kB | <540 kB | ✅      |
| W9   | 507.47 kB | <510 kB | ✅      |
| W10  | 497.44 kB | <500 kB | ✅      |
| W10  | 497.44 kB | <480 kB | ⚠️ partial (ShaderChunk barrier — see §6) |

PWA score (manifest-lint): **1.000** local; CI gate 0.90.
LH13 categories: W9 measured baseline carried over; first
CI nightly will produce the new baseline.

## Files modified

Frontend source: 7 files. Build config: 1
(`vite.config.ts` — PMREMGenerator strip + screenshot copy
+ cacheDir). Manifest + gitignore + dist-size: 3. CI: 3
files (workflow + 2 scripts). Docs: 4 (`frontend-three-budget.md`
+ `frontend-build-tooling.md` + `frontend-pwa-audit.md` updates;
`docs/contracts/commentary-tile-ref.md` NEW). Tests:
`selectors.md` W10 footer. Generated: `dist-size.json` +
`src/frontend/autotable/` Vite output. Placeholder PNGs: 3.

## Identity discipline

- All commits use per-command git env
  (`git -c user.name="Hicks (Frontend)" -c user.email=
  "hicks@squad.mahjong"`).
- NEVER `git config user.name`.
- Flock-wrapped commit at `.work/squad-git-lock` (-w 120)
  (relocated from `/tmp/squad-git-lock` by Apone W9).
- Stash-before / restore-after (no half-baked work left).
- Only lane-allowed paths staged: `src/frontend/`,
  `Phase_K_W10/Hicks/`, `docs/frontend-*`, `docs/contracts/`,
  `.squad/agents/hicks/`, `.squad/decisions/inbox/hicks-*`,
  `src/frontend/autotable-src/tests/selectors.md`,
  `.github/workflows/pwa-audit.yml`.
- `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github
  .com>` trailer included.

## Open hand-offs to W11

1. **ShaderChunk barrel surgery** — close the remaining
   ~17 kB to <480 kB. Cheapest: patch
   `meshlambert_frag.glsl` to drop the
   `#include <cube_uv_reflection_fragment>` directive.
   Combined three-strip yield: ~20-25 kB.
2. **PWA Builder CLI integration** — once a public preview
   URL exists, drop `npx @pwabuilder/cli@latest report` after
   the LH13 step. Gate on Manifest ≥ 95% + SW = 100%.
   `pwa-audit.yml` carries a `TODO(W11)` hook.
3. **LH13 category baselining** — after ≥ 3 nightly cron runs,
   walk thresholds to observed-minus-2-points.
4. **Vite cache hit-rate metric** — surface `actions/cache@v4`'s
   hit/miss output, write 7-day rolling rate to `.work/`.
5. **Screenshot quality** — replace W10 placeholder PNGs once
   W11 cinematic-camera work lands.
6. **`shortcuts[]` deep-linking** — wire `?action=*` dispatch
   in `lobby-app.ts` before Store listings.
7. **W12 cleanup** — drop `parseTileIdShape` + the string
   fallback branch in `pickTileReferences` once Bishop ships
   two consecutive backend deploys on the object shape.
