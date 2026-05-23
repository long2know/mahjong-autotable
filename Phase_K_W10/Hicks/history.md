# Hicks — Phase K Wave 10 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 10 — Frontend bring-up

Branch: `stlong/phase-k-wave-10-bringup`
Bringup-on commit (W9 close): `f518196`

### Deliverables (six)

1. **Commentary panel — TileReference adoption + `source` on
   dispatch.** `src/frontend/autotable-src/src/commentary-panel.ts`:

   - New interface `TileReference = { tileId: string; suit:
     string; rank: number }` exported.
   - `CommentaryRecord.tileReferences` typed
     `ReadonlyArray<TileReference>` (was `ReadonlyArray<string>`
     in W9).
   - `renderTileRef(ref)` reads `ref.suit` + `ref.rank` and
     emits `data-tile-suit` / `data-tile-rank` attributes on the
     chip alongside the existing `data-tile-id`.
   - Chip click handler dispatches
     `mahjong:highlight-tile` on `document` with
     `{ tileId, source: 'commentary-panel' }`.
   - `pickTileReferences()` accepts both the W10 object shape
     AND a W9 bare-string shape (parsed via
     `parseTileIdShape()`) — the W12 cleanup will drop the
     string branch after two backend deploys ship the object
     shape. See `docs/contracts/commentary-tile-ref.md §4` for
     the rolling-deploy discipline.

2. **PWA Builder CI workflow.** New file
   `.github/workflows/pwa-audit.yml` runs on push to
   `stlong/**` + `main`, every PR against `main`, and a
   nightly cron 03:30 UTC.

   - Jobs: `build` → `manifest-lint` → `lighthouse` →
     `pr-comment`.
   - `scripts/manifest-lint.js` (new) replays the LH11 PWA
     installability preconditions, computes a geometric-mean
     score across four sub-scores
     (manifest / icons / screenshots / shortcuts). Gate:
     `pwaScore ≥ 0.90`. W10 local baseline: **1.000**.
   - `scripts/render-pwa-comment.js` (new) emits a Markdown
     PR comment with a sticky `<!-- pwa-audit-comment -->`
     marker; uses `peter-evans/create-or-update-comment@v4`
     so re-runs update in place.
   - Vite cache restored via `actions/cache@v4`, key
     `vite-${{ runner.os }}-${{ hashFiles('package-lock.json',
     'vite.config.ts') }}`.
   - actionlint v1.7.7 passes cleanly.

3. **`partitionDoubleElim` removal + Parcel cleanup.**

   - `src/frontend/autotable-src/src/bracket-renderer.ts` —
     deleted `partitionDoubleElim` function + `PartitionedMatches`
     interface; replaced with a W10 comment explaining the
     W6→W9 history. File shrinks from 646 → 600 lines.
   - `src/frontend/autotable-src/package.json` — removed
     `build:parcel` script + 4 Parcel devDeps
     (`parcel`, `@parcel/packager-raw-url`,
     `@parcel/transformer-image`,
     `@parcel/transformer-webmanifest`).
   - `package-lock.json` regenerated — 636 transitive
     packages removed.

4. **PWA manifest gap-fills.** `manifest.webmanifest`:

   - Added top-level fields per the W3C 2024 recommendation:
     `id: "/?source=pwa"`, `lang: "en"`, `dir: "ltr"`,
     `description: "Mahjong Autotable — Changsha + Chinese
     variants"`.
   - Added `screenshots[]` (3 entries: 1024×768 lobby + table
     wide-form-factor, 768×1024 mobile narrow-form-factor).
     Generated placeholder PNGs via ImageMagick into
     `src/frontend/autotable-src/img/screenshot-
     {lobby,table,mobile}.auto.png` (~16–21 kB each).
   - Added `shortcuts[]` (3 entries: New game → `/?action=new`,
     Spectate → `/?action=spectate`, Tournament dashboard →
     `/tournament/`).
   - `copyStaticAssets()` in `vite.config.ts` extended to copy
     the three new screenshots into the dist root.

5. **PMREMGenerator strip — partial win.** Target was the W9
   §5 hand-off "~14 kB lazy-instantiated, strip if proven
   unreached". Audit confirmed the autotable scene never sets
   `material.envMap` or `scene.environment` — the
   `WebGLCubeUVMaps#get()` branch that instantiates
   `PMREMGenerator` is unreachable at runtime.

   - `vite.config.ts:stripModuleFeatures.MODULE_STUBS` extended
     with: `PMREMGenerator` (class body → no-op methods +
     pre-initialised private slots), plus 7 helper-function
     stubs (`_getBlurShader`, `_getEquirectMaterial`,
     `_getCubemapMaterial`, `_getCommonVertexShader`,
     `_createPlanes`, `_createRenderTarget`, `_setViewport`).
   - **Result:** `three-renderer-big = 497,440 B` (−10,034 B
     vs W9 = 507,474 B, −1.97%).
   - **Stretch ceiling MISSED:** spec asked for < 480 kB
     (−28 kB). PMREMGenerator class strip yielded the full
     10 kB win; helper-function stubs yielded **zero
     additional bytes** because Rollup was already tree-shaking
     the helpers once their only call sites (inside the class
     body) were gutted. Remaining bloat traced to three named
     ShaderChunk barrel exports: `cube_uv_reflection_fragment`,
     `fragment$g` (background), `fragment$5` (PBR). These
     can't be stripped without ShaderChunk-barrel surgery or a
     `WebGLBackground` stub — both deferred to W11 per the
     directive's explicit allowance ("If strip-out breaks
     anything … document the blockers and back out").
   - Full autopsy + trend table update in
     `docs/frontend-three-budget.md §6`.
   - **Vasquez invariant intact:** monotonic decrease holds
     for a 5th consecutive wave (740 → 579 → 531.86 → 507.47 →
     497.44 kB).

6. **Vite build cache.** `vite.config.ts` now sets
   `cacheDir: resolve(__dirname, '.vite')` — the cache lives
   at `src/frontend/autotable-src/.vite/` (not in
   `node_modules` — keeps it next to the source tree so it's
   discoverable and can be wiped without nuking deps).

   - `.gitignore` now excludes `.vite/`.
   - CI cache key in `pwa-audit.yml`: hash of
     `package-lock.json` + `vite.config.ts`.
   - Measured: cold ~28–32 s → warm ~8–12 s locally
     (M1 Pro); CI cold ~50–65 s → warm ~18–25 s.

### Files modified

| File                                                            | Change |
|-----------------------------------------------------------------|--------|
| `src/frontend/autotable-src/src/commentary-panel.ts`            | TileReference interface, object-shape coercion, `source: 'commentary-panel'` on dispatch. |
| `src/frontend/autotable-src/src/bracket-renderer.ts`            | Removed `partitionDoubleElim` + `PartitionedMatches`. |
| `src/frontend/autotable-src/vite.config.ts`                     | PMREMGenerator + 7 helper stubs; `cacheDir`; screenshot copy. |
| `src/frontend/autotable-src/manifest.webmanifest`               | Added id/lang/dir/description/screenshots/shortcuts. |
| `src/frontend/autotable-src/package.json`                       | Removed `build:parcel` + 4 Parcel devDeps. |
| `src/frontend/autotable-src/package-lock.json`                  | Regenerated (-636 packages). |
| `src/frontend/autotable-src/.gitignore`                         | Added `.vite/`. |
| `src/frontend/autotable-src/dist-size.json`                     | K10 row appended (three-renderer-big = 497,440 B). |
| `src/frontend/autotable-src/img/screenshot-{lobby,table,mobile}.auto.png` | NEW PWA screenshot placeholders. |
| `src/frontend/autotable/*`                                      | Vite rebuilt output. |
| `.github/workflows/pwa-audit.yml`                               | NEW — CI workflow. |
| `src/frontend/autotable-src/scripts/manifest-lint.js`           | NEW — PWA score replay. |
| `src/frontend/autotable-src/scripts/render-pwa-comment.js`      | NEW — PR comment renderer. |
| `docs/frontend-three-budget.md`                                 | §6 + W10 trend row. |
| `docs/frontend-build-tooling.md`                                | §4 (Parcel removed), §5 (Build cache), W10 trend row. |
| `docs/frontend-pwa-audit.md`                                    | §4 (CI workflow detail), §5 (hand-off refresh). |
| `docs/contracts/commentary-tile-ref.md`                         | NEW — canonical TileReference contract + W9→W10→W12 discipline. |
| `src/frontend/autotable-src/tests/selectors.md`                 | W10 footer (TileReference DOM hooks, `source` event field, trend gate, cache dir). |

### Trend ledger

| Wave | three-renderer-big | Δ vs prev | Vasquez gate |
|------|--------------------|-----------|--------------|
| W7   | 578.72 kB          | -161 kB   | <550 kB ✅   |
| W8   | 531.86 kB          | -46.86 kB | <540 kB ✅   |
| W9   | 507.47 kB          | -24.39 kB | <510 kB ✅   |
| W10  | 497.44 kB          | -10.03 kB | <500 kB ✅ / <480 kB ⚠️ partial |

### Open hand-offs to W11

1. **ShaderChunk barrel surgery.** The remaining ~17 kB to
   the <480 kB ceiling lives in `cube_uv_reflection_fragment`,
   `fragment$g` (WebGLBackground), `fragment$5` (PBR). Either
   patch `meshlambert_frag.glsl` to drop the `#include
   <cube_uv_reflection_fragment>` directive (cheapest), stub
   `WebGLBackground`'s shader path (medium), or patch
   `WebGLPrograms.acquireProgram` (touches per-frame hot path
   — high risk). Combined yield ~20-25 kB if all three land.
2. **PWA Builder CLI integration.** Once a public preview URL
   exists (Cloudflare Pages or `cloudflared tunnel`), drop
   `npx @pwabuilder/cli@latest report --url <preview-url>
   --output pwabuilder.json` into `pwa-audit.yml` after the
   LH13 step. Gate on Manifest ≥ 95% + Service Worker = 100%.
   The hook in `pwa-audit.yml` is marked `TODO(W11)`.
3. **LH13 category thresholds.** The W10 thresholds are
   conservative carry-overs from W9 manual runs. After ≥ 3
   nightly cron runs land, walk the thresholds to
   observed-minus-2-points.
4. **Vite cache hit-rate metric.** Add a step that prints
   `actions/cache@v4`'s "cache hit/miss" output and writes
   a rolling 7-day hit-rate to `.work/` for the squad ledger.
5. **Screenshot quality.** Replace W10 placeholder PNGs with
   real captures once the W11 cinematic-camera work lands.
6. **`shortcuts[]` deep-linking.** Wire query-param dispatch
   in `lobby-app.ts` to honour `?action=new` / `?action=spectate`
   before the Edge/Chromium Store listings go live.
7. **W12 string-fallback removal.** Once Bishop's backend ships
   two consecutive deploys with the object-shape
   `TileReference`, remove `parseTileIdShape` + the
   string-coercion branch in `pickTileReferences`.

### Identity discipline (as practised)

- Per-command git env:
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`.
- NEVER `git config user.name`.
- Flock-wrapped at `.work/squad-git-lock` (-w 120).
- Stash-before / restore-after.
- Only lane-allowed paths staged.
- `Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>` trailer included.
