# Hicks — Phase K Wave 11 decisions memo

Branch: `stlong/phase-k-wave-11-bringup`
Author: Hicks (Frontend)
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

6 deliverables per the W11 directive:

1. **ShaderChunk barrel surgery for `three-renderer-big` <
   475 kB.** `src/frontend/autotable-src/vite.config.ts` —
   new Vite plugin `stripUnusedShaderChunks()` registered in
   the `plugins:` array between `stripWebGLShadowMap` (W9) and
   `copyStaticAssets`. The plugin targets `three.module.js`
   with `enforce: 'pre'` and empties the GLSL string bodies
   of 32 ShaderLib `vertex$X` / `fragment$X` constants
   (**excluding** `$a` meshbasic — shared by `MeshBasicMaterial`
   AND `LineBasicMaterial` — and `$9` meshlambert), the
   `cube_uv_reflection_fragment` ShaderChunk, and the
   standalone VSM-blur `vertex` / `fragment` pair. The barrel
   re-export tables (`ShaderChunk.*`, `ShaderLib.*`) stay
   intact — only the GLSL body is emptied. Safe because
   three.js compiles shaders lazily and our scene-graph audit
   confirms only `MeshBasicMaterial` + `MeshLambertMaterial` +
   `LineBasicMaterial` + the W7 `CustomOutline` ShaderMaterial
   are constructed in `autotable-src`.
   **Result: 497,440 B → 466,395 B (−31,045 B / −6.2%)**, 9 kB
   margin under the < 475 kB stretch target.

2. **PWA Builder CLI CI workflow.**
   `.github/workflows/pwa-builder.yml` (NEW) — companion to
   W10's `pwa-audit.yml`. Triggers: pull_request (paths-
   filtered), nightly cron 03:30 UTC, workflow_dispatch with
   `preview_url` input. Steps: resolve preview URL (input
   first, then `secrets.PWA_PREVIEW_URL`, fall through to
   skip-with-warning if absent), `npm install -g
   @pwabuilder/cli@latest`, `pwabuilder analyze --json`, parse
   per-platform readiness scores (Edge / Chrome / Safari w/
   multi-alias tolerance for CLI minor-version drift), gate
   ≥ 75 per platform on PR, sticky PR comment with marker
   `<!-- pwa-builder-report -->`, upload artefacts.

3. **LH13 baseline calibration.** `scripts/lh-baseline.js`
   (NEW). 5-run methodology against Vite preview on
   127.0.0.1:4175, computes p50 / p95 / mean / min / max per
   category. **Observed local baseline (K11 build, deterministic
   across 5 runs):** perf=100 / a11y=83 / bp=96 / seo=82.
   W10's `pwa-audit.yml` thresholds for a11y / seo (both 0.95)
   are above the measured local ceiling — they would silently
   hard-fail every PR if the gate were exercised. Calibrated
   thresholds documented in `docs/frontend-pwa-audit.md §7`.
   The actual workflow edit is intentionally **deferred to
   W12** so ≥ 3 nightly cron data points from real CI can
   confirm the local-vs-CI variance offset before we walk the
   gate down.

4. **Vite cache effectiveness metric.**
   `scripts/build-with-cache-metric.js` (NEW). Runs
   `npm run build:vite`, parses chunk names
   (`<name>.<hash:8>.js`) from `../autotable/`, compares each
   `hash:8` segment against a prior baseline at
   `.vite-cache-metric.json`. Output:
   `cacheHitRate = stable_hashes / total_chunks`. Gate via
   `THRESHOLD` env var (default 0). Pivoted away from the
   W10 hand-off's suggested `.vite/deps/` mtime walk because
   that directory only populates during dev-server
   `optimizeDeps` pre-bundle — `vite build` doesn't write to
   it, so an mtime scan would always report 0%. Chunk-hash
   stability is the honest signal: cold = 0% (no baseline),
   warm = 100% (22/22 chunks on unchanged source). See
   `docs/frontend-build-tooling.md §6`.

5. **Real Playwright-captured manifest screenshots.**
   `scripts/capture-screenshots.js` (NEW). Spawns
   `vite preview` on 127.0.0.1:4174, launches headless
   chromium via Playwright, captures three real PNGs:
   `main-game.png` (1024×768, wide), `spectator-commentary.png`
   (768×1024, narrow), `tournament-dashboard.png` (1024×768,
   wide). Saved to `static/screenshots/`; copy chain extended
   in `vite.config.ts:copyStaticAssets()` to land them at
   `dist/screenshots/*.png`. Manifest schema updated:
   `screenshots[].src` → `screenshots/{name}.png` (was W10
   placeholder `img/screenshot-*.auto.png`), each entry now
   carries explicit `form_factor` + `label` per spec.
   `shortcuts[]` `?action=tournaments` (W10 plural) →
   `?action=tournament` (W11 canonical) — action-router
   accepts both for installed-PWA compatibility. W10
   placeholder copy block in `copyStaticAssets()` retained as
   a safety net for two more waves.

6. **`?action=*` PWA shortcut deep-link routing.**
   `src/frontend/autotable-src/src/action-router.ts` (NEW).
   Sole owner of `?action=*` interpretation. Public surface:
   `parseActionFromUrl()`, `clearActionParam()`,
   `handlePwaActionFromUrl()`. Supported keywords:
   - `new-game` → clicks `[data-action="new-game"]` on the
     `#new-game` button (annotated W11), URL becomes `/`.
   - `spectate` → activates `#lobby-public-games-tab`, URL
     rewritten to `/spectate`.
   - `tournament` (+ `tournaments` plural alias) → activates
     `#lobby-tournaments-tab`, URL rewritten to
     `/tournament/list`.
   Wired in `src/index.ts` BEFORE the W2 game-bootstrap guard
   so the heavy renderer chunk isn't imported when a shortcut
   URL is opened. Full contract in `docs/frontend-routing.md`
   (NEW W11).

## Trend ledger (three-renderer-big)

| Wave | Bytes  | kB     | Δ vs prev | Gate |
|------|--------|--------|-----------|------|
| W7   | 592,609 | 578.72 | -161 kB   | <550 kB ✅ |
| W8   | 544,627 | 531.86 | -46.86 kB | <540 kB ✅ |
| W9   | 519,650 | 507.47 | -24.39 kB | <510 kB ✅ |
| W10  | 497,440 | 497.44 | -10.03 kB | <500 kB ✅ / <480 kB ⚠️ |
| W11  | 466,395 | 466.40 | -31.04 kB | <475 kB ✅ stretch |

## Identity discipline (as practised)

- Per-command git env:
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`.
- NEVER `git config user.name` (would leak into other
  in-flight branches via the shared workdir).
- Flock-wrapped at `.work/squad-git-lock` (-w 120).
- No stash needed — opened on a clean working tree at
  `0c95748` (W10 PR #56 merged).
- Only lane-allowed paths staged (`src/frontend/**`,
  `Phase_K_W11/Hicks/**`, `docs/frontend-*.md`,
  `.github/workflows/pwa-*.yml`, `.squad/agents/hicks/**`,
  `.squad/decisions/inbox/hicks-*.md`, the
  `src/frontend/autotable-src/tests/selectors.md` shared
  file per W8 policy).
- `Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>` trailer
  included in every W11 commit.

## Open hand-offs to W12

1. **PMREMGenerator-adjacent ShaderChunk strip candidates** —
   `opaque_fragment`, `colorspace_fragment`, `tonemapping_*`,
   and the standalone ShaderChunk entries reached only via
   `#include` from stripped ShaderLib bodies. Yield ~8-12 kB.
2. **`UniformsLib` unused-entry strip** — exports ~12 named
   uniform tables; only `common` + `lights` reachable. Yield
   ~3-5 kB.
3. **`shadowmap_*` chunk body strip** — W9 stubbed the parent
   class but the chunks still ship. Yield ~6 kB.
4. **LH13 threshold workflow edit** — walk
   `accessibility` / `seo` thresholds in `pwa-audit.yml` down
   to the §7 calibrated values once ≥ 3 real-CI cron data
   points land.
5. **`secrets.PWA_PREVIEW_URL` provisioning** for the new
   `pwa-builder.yml` workflow — Apone (infra) owns the
   Cloudflare-Pages or cloudflared-tunnel hookup. Until that
   lands, the workflow falls back to "skip with warning" so
   it doesn't gate forks.
6. **W10 placeholder screenshot copy block removal** — once
   two waves have shipped with the W11 `screenshots/` paths,
   remove the legacy `img/screenshot-*.auto.png` copy in
   `vite.config.ts:copyStaticAssets`.
7. **Visual-regression spec** for the W11 captures (Vasquez's
   spec lane).
8. **`?action=replay`** in the action-router once Drake's
   replay-by-id endpoint lands. Reserved in
   `docs/frontend-routing.md §7`.

## Model

Stephen's standing directive `claude-opus-4.7-xhigh` honoured
throughout the wave. Not downgraded.

## For Scribe

This memo is the W11 wave summary suitable for promotion into
`.squad/decisions/` proper. Cross-references for the merge:

- `docs/frontend-three-budget.md §7` — ShaderChunk surgery
  write-up + scene-graph audit + trend ledger.
- `docs/frontend-pwa-audit.md §6` — PWA Builder CI workflow
  contract.
- `docs/frontend-pwa-audit.md §7` — LH13 baseline calibration.
- `docs/frontend-pwa-audit.md §8` — Real screenshot capture
  recipe.
- `docs/frontend-build-tooling.md §6` — cache effectiveness
  metric.
- `docs/frontend-routing.md` — action-router contract (NEW).
- `Phase_K_W11/Hicks/charter.md` + `history.md` — wave-scoped
  artefacts.
- `src/frontend/autotable-src/tests/selectors.md` — W11
  footer mirroring W7/W8/W9/W10 pattern.
