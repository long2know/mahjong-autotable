# Hicks — Phase K Wave 12 decisions memo

Branch: `stlong/phase-k-wave-12-bringup`
Author: Hicks (Frontend)
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

6 deliverables per the W12 directive (5 ship, 1 deferred with
documented rationale):

1. **PMREMGenerator-adjacent ShaderChunk strip (`envmap_*`).**
   `src/frontend/autotable-src/vite.config.ts` —
   `SHADER_CHUNKS_TO_EMPTY` (W11) extended with six new
   entries: `envmap_fragment`, `envmap_common_pars_fragment`,
   `envmap_pars_fragment`, `envmap_pars_vertex`,
   `envmap_physical_pars_fragment`, `envmap_vertex`. Each
   body is wrapped in `#ifdef USE_ENVMAP`. The autotable's
   material set (`MeshBasicMaterial`, `MeshLambertMaterial`,
   `LineBasicMaterial`, W7 `CustomOutline`) never sets the
   `envMap` property nor enables `scene.environment`, so the
   GLSL preprocessor strips the include bodies anyway —
   emptying the JS strings drops ~10 kB of carrying weight.

2. **`UniformsLib` unused-entry strip.**
   `vite.config.ts` — new `stripUnusedUniformsLib()` Vite
   plugin (mirrors the W9 brace-walker pattern). Operates on
   `three.module.js` with `enforce: 'pre'`. Targets the
   `UniformsLib = { ... }` registry header and rewrites five
   W9-stubbed-material keys to empty object literals
   (`roughnessmap`, `metalnessmap`, `gradientmap`, `points`,
   `sprite`). ShaderLib calls to
   `UniformsUtils.merge([UniformsLib.X, ...])` still resolve
   (read `{}` instead of the original 4-6 line descriptors).
   Plugin registered in the `plugins:` array between
   `stripUnusedShaderChunks` (W11) and `copyStaticAssets`.

3. **`shadowmap_*` + `shadowmask_*` chunk body strip.**
   Four more entries added to `SHADER_CHUNKS_TO_EMPTY`:
   `shadowmap_pars_fragment`, `shadowmap_pars_vertex`,
   `shadowmap_vertex`, `shadowmask_pars_fragment`. Bodies
   wrapped in `#ifdef USE_SHADOWMAP`; the autotable's
   `WebGLRenderer` never sets `shadowMap.enabled` and no
   light has `castShadow = true`. The `shadowmask_pars_*`
   chunk defines `getShadowMask()` whose only call site is
   the W9-stripped `shadow_frag` shader — safe to empty
   entirely.

   **Combined deliverables 1+2+3 result:**
   `three-renderer-big = 466,395 B → 448,648 B`
   **(−17,747 B / −3.8 %).** ~1.4 kB margin under the
   <450 kB stretch target; 11.4 kB margin under <460 kB
   acceptable.

4. **LH13 workflow threshold edit — DEFERRED TO W13.**
   `gh run list --workflow=pwa-audit.yml` returned 0 cron
   runs since the W11 §7 calibration landed. Per the W12
   directive's conditional clause, the edit requires ≥ 3
   cron data points (so the p95 estimate folds in CI-runner
   jitter). Deferral rationale + W13 procedure documented in
   new `docs/frontend-pwa-audit.md §9`. The workflow gate
   currently only enforces `pwaScore < 0.90` floor — no
   LH-category thresholds are wired in yet, so deferring
   does NOT regress any prior behaviour.

5. **W10 placeholder screenshot copy block removed.**
   `vite.config.ts:copyStaticAssets` — the W10 fallback loop
   that copied `img/screenshot-{lobby,table,mobile}.auto.png`
   into `dist/img/` is gone (replaced with a W12 retirement
   comment). The three source PNGs are `git rm`'d. The W11
   manifest pointed only at the real captures at
   `screenshots/{main-game,spectator-commentary,
   tournament-dashboard}.png` — the legacy paths were never
   surfaced in any live build, so removal is safe (no PWA
   cache stale concern).

6. **`?action=replay&replayId=<guid>` deep-link routing.**
   `src/frontend/autotable-src/src/action-router.ts`
   extended with the fourth SUPPORTED_ACTION (`'replay'`).
   New private helpers `dispatchReplay`,
   `fetchAndOpenReplay`, `showReplayNotFoundToast`. Switch
   case in `handlePwaActionFromUrl` reads the `replayId`
   co-parameter from `URLSearchParams`, strips BOTH `action`
   and `replayId` from the URL (refresh-safe — re-loading
   the rewritten URL does NOT re-trigger), fetches
   `GET /api/replays/{replayId}` against Bishop's W12
   endpoint, JSON-parses the body, and on success
   lazy-imports `./replay-launcher` to call the new
   `openReplayPayload(replayId, body, options?)` export
   while rewriting the URL to `/replay/{replayId}` via
   `history.replaceState()`. ANY failure path
   (404 / 5xx / network / JSON-parse / missing co-param) →
   `showToast('Replay not found', 'error')` from `./toast`,
   no URL rewrite. **No fallback** to the legacy
   `/api/games/{gameId}/replay` endpoint — would mask
   config drift.

## Trend ledger (three-renderer-big)

| Wave | Bytes   | kB     | Δ vs prev | Gate |
|------|---------|--------|-----------|------|
| W7   | 592,609 | 578.72 | -161 kB   | <550 kB ✅ |
| W8   | 544,627 | 531.86 | -46.86 kB | <540 kB ✅ |
| W9   | 519,650 | 507.47 | -24.39 kB | <510 kB ✅ |
| W10  | 497,440 | 497.44 | -10.03 kB | <500 kB ✅ / <480 kB ⚠️ |
| W11  | 466,395 | 466.40 | -31.04 kB | <475 kB ✅ stretch |
| W12  | 448,648 | 448.65 | -17.75 kB | **<450 kB ✅ stretch** |

7th consecutive monotonic decrease. Cumulative drop from W6
(739.72 kB) to W12 (448.65 kB) is **−39.4 %** over six waves.

## Identity discipline (as practised)

- Per-command git env:
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`.
- NEVER `git config user.name` (would leak into other
  in-flight branches via the shared workdir).
- Flock-wrapped at `.work/squad-git-lock` (-w 120).
- No stash needed for Hicks's lane — Apone's WIP terraform
  changes left stashed at `stash@{0}` (NOT POPPED — out of
  lane).
- Only lane-allowed paths staged.
- `Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>` trailer
  included in every W12 commit.

## Open hand-offs to W13

1. **`opaque_fragment` + `colorspace_fragment` +
   `tonemapping_*` ShaderChunk strip** (carried fwd from
   W11). Yield ~3-5 kB.
2. **Remaining `UniformsLib` features** — clearcoat,
   iridescence, sheen, transmission, anisotropy, dispersion,
   reflectivity-extras. All routed through
   `ShaderLib.physical` (W11-stubbed). Aggregate ~1-2 kB.
3. **`lights_phong_*` / `lights_toon_*` /
   `lights_physical_*` ShaderChunks** — autotable uses
   `AmbientLight` + `DirectionalLight` only. ~0.5-2 kB each.
4. **LH13 threshold edit** (carried fwd from W12 deferral) —
   walk a11y / seo / bp / perf thresholds in
   `pwa-audit.yml` down to the §7 calibrated values once
   ≥ 3 cron data points are available.
5. **Visual-regression spec** for W11 captures (Vasquez
   lane — still open).
6. **Bishop W12 `/api/replays/{replayId}` endpoint
   integration test** — Vasquez should add a Playwright
   spec `deep-link-action-replay.spec.ts` that mocks the
   endpoint (404 + 200 + malformed-JSON cases) and asserts
   the URL-rewrite + toast contract.
7. **Action-router co-parameter schema layer** (deferred
   from routing-doc §9 hand-off) — when a fifth keyword
   lands with its own co-param, generalise the W12
   `replayId` parse-strip-refetch pattern via a per-action
   `parseCoParams<T>()` helper.

## Model

Stephen's standing directive `claude-opus-4.7-xhigh` honoured
throughout the wave. Not downgraded.

## For Scribe

This memo is the W12 wave summary suitable for promotion into
`.squad/decisions/` proper. Cross-references for the merge:

- `docs/frontend-three-budget.md §8` — W12 ShaderChunk + envmap
  + UniformsLib strip write-up + risk/back-out matrix +
  trend ledger.
- `docs/frontend-routing.md §2` (W12 subsection) + §3 (table
  row) + §7 (reservation list — `replay` moved out, marked
  cashed in) + §9 (W13 hand-off refreshed).
- `docs/frontend-pwa-audit.md §9` — LH13 threshold-edit
  deferral.
- `src/frontend/autotable-src/tests/selectors.md` — W12
  footer.
- `Phase_K_W12/Hicks/{charter,history}.md` — wave-scoped
  hand-off artefacts.
