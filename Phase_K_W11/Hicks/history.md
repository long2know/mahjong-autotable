# Hicks — Phase K Wave 11 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 11 — Frontend bring-up

Branch: `stlong/phase-k-wave-11-bringup`
Bringup-on commit (W10 close): `0c95748` (W10 PR #56 merged
to the bringup branch).

### Deliverables (six)

1. **ShaderChunk barrel surgery for `three-renderer-big` <
   475 kB.**
   `src/frontend/autotable-src/vite.config.ts` — new Vite
   plugin `stripUnusedShaderChunks()` registered between
   `stripWebGLShadowMap` (W9) and `copyStaticAssets` in the
   `plugins` array. `enforce: 'pre'` transform that targets
   `three.module.js` and empties the GLSL string bodies of:

   - 32 ShaderLib entries (`vertex$h` / `fragment$h` down
     through `$1`) **except** `$a` (meshbasic — shared by
     both `MeshBasicMaterial` and `LineBasicMaterial`) and
     `$9` (meshlambert, used by the table-base material).
   - The standalone `cube_uv_reflection_fragment` ShaderChunk
     (a no-op `#include` from `meshlambert_frag` guarded by
     `#ifdef ENVMAP_TYPE_CUBE_UV` — macro is never defined in
     our scene).
   - The standalone VSM-blur `vertex` / `fragment` pair (no
     shadow map present; W9 stripped the parent
     `WebGLShadowMap` class already).

   The barrel re-export tables stay intact (so
   `ShaderLib.meshphysical_frag` etc. still resolve at lookup
   time); only the GLSL body is emptied. Safe because three.js
   compiles shaders lazily — only for materials that are
   actually instantiated. Scene-graph audit confirmed only
   `MeshBasicMaterial` + `MeshLambertMaterial` +
   `LineBasicMaterial` + `ShaderMaterial` (W7
   `CustomOutline`) are constructed in `autotable-src`.

   Result: **497,440 B → 466,395 B (−31,045 B / −6.2%)**,
   comfortably under the < 475 kB stretch target with 9 kB
   margin. The W10 W11-handoff hand-off-#1 yield estimate
   was 20-25 kB — actual landed at 31 kB because the
   ShaderChunk barrel removal also un-lazied a handful of
   formerly-tree-shake-resistant program-compilation
   bookkeeping that Rollup could now drop.

2. **PWA Builder CLI CI workflow.**
   `.github/workflows/pwa-builder.yml` (NEW). Triggers:
   - `pull_request` (paths-filtered to manifest, sw.js,
     screenshots, the workflow itself, and the PWA audit doc).
   - `schedule` (cron `30 3 * * *`, 03:30 UTC nightly).
   - `workflow_dispatch` with `preview_url` input.

   Steps:
   - Resolve preview URL: `inputs.preview_url` first, then
     `secrets.PWA_PREVIEW_URL`; skip-with-warning if absent.
   - `npm install -g @pwabuilder/cli@latest` (intentional
     `@latest` — see `docs/frontend-pwa-audit.md §6`).
   - `pwabuilder analyze --url <URL> --json
     > pwabuilder-report.json`.
   - Parse per-platform readiness scores (Edge / Chrome /
     Safari). Parse tolerates multiple alias spellings
     (`microsoftEdge`, `microsoft_edge`, `edge`,
     `googleChrome`, `google_chrome`, `chrome`,
     `appleSafari`, `safari`).
   - Gate ≥ 75 per platform. Hard-fail on PR; soft-warn for
     cron / dispatch.
   - Sticky PR comment with marker
     `<!-- pwa-builder-report -->`.
   - Upload artefact `pwabuilder-report.json` + parsed
     scores JSON for 7-day retention.

   See `docs/frontend-pwa-audit.md §6` for the contract.

3. **LH13 baseline calibration.**
   `scripts/lh-baseline.js` (NEW W11). Spins up Vite preview
   on 127.0.0.1:4175, runs `npx lighthouse` 5 times against
   it with `--form-factor=desktop --screenEmulation.disabled=
   true`, parses the four non-PWA category scores from each
   run's `.report.json`, and computes p50 / p95 / mean / min /
   max per category. Output: `.lh-baseline.json` (artefact-
   only; not git-tracked).

   Observed W11 local baseline (5 runs, K11 build,
   identical scores across all runs):

   | Category | score |
   |----------|------|
   | performance | 100 |
   | accessibility | 83 |
   | best-practices | 96 |
   | seo | 82 |

   The W10 thresholds in `pwa-audit.yml` (`accessibility:
   0.95` and `seo: 0.95`) are above the measured local
   ceiling — they would silently hard-fail every PR if the
   gate were exercised. Calibrated thresholds documented in
   `docs/frontend-pwa-audit.md §7`. Workflow file edit
   intentionally deferred to W12 (needs ≥ 3 cron data points
   from real CI to confirm the offset).

4. **Vite cache effectiveness metric.**
   `scripts/build-with-cache-metric.js` (NEW W11). Runs
   `npm run build:vite`, parses emitted chunk names
   (`<name>.<hash:8>.js`) from `../autotable/`, compares
   the `hash:8` segment per chunk against a prior baseline
   at `.vite-cache-metric.json` (or writes the baseline on
   first run).

   Output: `cacheHitRate = stable_hashes / total_chunks`
   plus per-chunk detail. Gate: `THRESHOLD` env var (default
   0, no gate); CI sets `THRESHOLD=0.70`.

   Pivoted **away from** the W10 hand-off's suggested
   `.vite/deps/` mtime scan because that directory only
   populates during dev-server `optimizeDeps` pre-bundle —
   `vite build` doesn't write to it, so a mtime scan would
   always report 0% regardless of the actual cache state.
   Chunk-hash stability is the honest signal. Cold run = 0%
   (no baseline yet). Warm rebuild of unchanged source = 100%
   (22/22 chunks). See `docs/frontend-build-tooling.md §6`.

   `package.json` gained `build:metric` script:
   `WAVE_NAME=K11 node scripts/build-with-cache-metric.js`.

5. **Real Playwright-captured manifest screenshots.**
   `scripts/capture-screenshots.js` (NEW W11). Spawns
   `vite preview` on 127.0.0.1:4174 against `../autotable/`,
   launches headless chromium via Playwright, captures three
   PNGs:

   - `main-game.png` @ 1024×768, `form_factor: wide`
   - `spectator-commentary.png` @ 768×1024, `form_factor: narrow`
   - `tournament-dashboard.png` @ 1024×768, `form_factor: wide`

   Saved to `static/screenshots/`. `vite.config.ts:
   copyStaticAssets` extended to copy `static/screenshots/*.png`
   → `dist/screenshots/*.png` so manifest paths resolve at
   install time. W10 placeholder copy
   (`img/screenshot-{lobby,table,mobile}.auto.png`) kept as a
   safety net for previously-installed PWAs that still
   reference the old paths.

   Manifest schema updated:

   - `screenshots[].src` → `screenshots/{name}.png`
   - Each entry has explicit `form_factor` + `label`
   - `shortcuts[]` third entry's `url` changed from
     `?action=tournaments` (W10 plural) → `?action=tournament`
     (W11 singular canonical form).

   `package.json` gained `capture:screenshots` script.

6. **`?action=*` PWA shortcut deep-link routing.**
   `src/frontend/autotable-src/src/action-router.ts` (NEW W11).
   Sole owner of `?action=*` interpretation. Public surface:

   ```ts
   export function parseActionFromUrl(): string | null;
   export function clearActionParam(): void;
   export function handlePwaActionFromUrl(): boolean;
   ```

   Supported keywords (with aliases):

   - `new-game` → click `[data-action="new-game"]`. URL
     becomes `/` post-dispatch.
   - `spectate` → activate `#lobby-public-games-tab`. URL
     rewritten to `/spectate`.
   - `tournament` (with `tournaments` plural alias) →
     activate `#lobby-tournaments-tab`. URL rewritten to
     `/tournament/list`.

   Wired in `src/index.ts` BEFORE the W2 game-bootstrap
   guard so the heavy renderer chunk isn't imported when a
   shortcut URL is opened. Returns `true` to the boot
   sequence on successful dispatch; the guard then skips
   the dynamic import.

   `index.html` line 326: added `data-action="new-game"` to
   the existing `#new-game` button so the router has a
   stable click target.

   Full contract in `docs/frontend-routing.md` (NEW W11).

### Files touched

| Path                                                            | Change |
|-----------------------------------------------------------------|--------|
| `src/frontend/autotable-src/vite.config.ts`                     | `stripUnusedShaderChunks()` plugin (~140 lines), `copyStaticAssets()` extended for `static/screenshots/`. |
| `src/frontend/autotable-src/src/action-router.ts`               | NEW — `?action=*` deep-link dispatch. |
| `src/frontend/autotable-src/src/index.ts`                       | Action-router import + call before game-bootstrap guard. |
| `src/frontend/autotable-src/index.html`                         | `data-action="new-game"` on `#new-game` button. |
| `src/frontend/autotable-src/manifest.webmanifest`               | screenshots[] → `screenshots/*.png` + form_factor; shortcuts[] `tournaments` → `tournament`. |
| `src/frontend/autotable-src/static/screenshots/*.png`           | NEW — real Playwright captures (3 files). |
| `src/frontend/autotable-src/scripts/capture-screenshots.js`     | NEW — Playwright + Vite-preview capture pipeline. |
| `src/frontend/autotable-src/scripts/build-with-cache-metric.js` | NEW — chunk-hash stability metric. |
| `src/frontend/autotable-src/scripts/lh-baseline.js`             | NEW — 5-run LH13 calibration. |
| `src/frontend/autotable-src/package.json`                       | `build:metric` + `capture:screenshots` scripts. |
| `src/frontend/autotable-src/.gitignore`                         | `.vite-cache-metric.json`, `pwabuilder-report.json`. |
| `src/frontend/autotable-src/dist-size.json`                     | K11 row pinned. |
| `src/frontend/autotable/*`                                      | Rebuilt artefacts; `three-renderer.*.js` = 466.40 kB. |
| `.github/workflows/pwa-builder.yml`                             | NEW — PWA Builder CI workflow. |
| `docs/frontend-three-budget.md`                                 | §7 (W11) — ShaderChunk surgery write-up + trend ledger. |
| `docs/frontend-pwa-audit.md`                                    | §5 retired, §6 (PWA Builder CI), §7 (LH13 calibration), §8 (screenshots). |
| `docs/frontend-build-tooling.md`                                | §6 — cache effectiveness metric. |
| `docs/frontend-routing.md`                                      | NEW — action-router contract. |
| `src/frontend/autotable-src/tests/selectors.md`                 | W11 footer (data-action selector, action-router surface, screenshot paths, cache metric format, W11 trend gate). |

### Trend ledger

| Wave | three-renderer-big | Δ vs prev | Vasquez gate |
|------|--------------------|-----------|--------------|
| W7   | 578.72 kB          | -161 kB   | <550 kB ✅   |
| W8   | 531.86 kB          | -46.86 kB | <540 kB ✅   |
| W9   | 507.47 kB          | -24.39 kB | <510 kB ✅   |
| W10  | 497.44 kB          | -10.03 kB | <500 kB ✅ / <480 kB ⚠️ |
| W11  | 466.40 kB          | -31.04 kB | <475 kB ✅ stretch |

### Open hand-offs to W12

1. **PMREMGenerator-adjacent chunk strip candidates.** The
   W10 hand-off flagged `opaque_fragment`, `colorspace_fragment`,
   `tonemapping_*`. W11 stripped most ShaderLib entries; the
   remaining win is in the **standalone ShaderChunk** entries
   that are referenced via `#include <name>` only — sweep the
   `ShaderChunk` exports table for entries that aren't reached
   from the `meshbasic_*` / `meshlambert_*` pairs and empty
   their bodies. Estimated yield: 8-12 kB.
2. **UniformsLib unused entries.** `UniformsLib` exports
   ~12 named uniform tables (`common`, `lights`, `envmap`,
   `aomap`, etc.); only `common` + `lights` are reachable
   from the materials in use. Stripping the rest yields
   ~3-5 kB.
3. **`shadowmap_*` chunks.** W9 stubbed `WebGLShadowMap`;
   the chunks still ship. Empty their bodies. Yield ~6 kB.
4. **LH13 threshold workflow edit.** Once three nightly cron
   runs land on real CI, walk `accessibility` / `seo`
   thresholds down to the §7 table values in
   `pwa-audit.yml`. (W11 did the measurement; the W12 author
   does the workflow edit + signs off on the calibration.)
5. **`secrets.PWA_PREVIEW_URL` provisioning** for the new
   `pwa-builder.yml` workflow. Apone (infra) owns the
   Cloudflare-Pages or cloudflared-tunnel hookup. Until that
   lands, the workflow falls back to "skip with warning" so
   it doesn't gate forks.
6. **W10 placeholder screenshot copy block removal.** Once
   two waves have shipped with the W11 `screenshots/` paths,
   remove the legacy `img/screenshot-*.auto.png` copy in
   `vite.config.ts:copyStaticAssets` and the corresponding
   placeholder PNGs from `static/img/`.
7. **Visual-regression spec for the W11 screenshots.** A
   Playwright sweep that re-captures the three screenshots
   and structural-tree-hashes them against the committed
   copies (W12 candidate for Vasquez's spec lane).
8. **Action-router replay shortcut.** `?action=replay&table=
   <id>` once Drake's replay-by-id endpoint lands. Reserved
   in `docs/frontend-routing.md §7`.

### Identity discipline (as practised)

- Per-command git env:
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`.
- NEVER `git config user.name`.
- Flock-wrapped at `.work/squad-git-lock` (-w 120).
- Stash-before / restore-after (no stash needed — opened
  on a clean tree).
- Only lane-allowed paths staged.
- `Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>` trailer included.

### Model

Stephen's standing directive: `claude-opus-4.7-xhigh` for
the duration of this wave. Honoured throughout.
