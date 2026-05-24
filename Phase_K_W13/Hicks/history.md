# Hicks — Phase K Wave 13 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 13 — Frontend bring-up

Branch: `stlong/phase-k-wave-13-bringup`
Bringup-on commit (W12 close): the W12 PR merged into the
bringup branch immediately before W13 launch.

### Deliverables (five)

1. **PMREMGenerator deeper strip
   (tonemapping_* + PBR-extras + map-feature chains).**
   `src/frontend/autotable-src/vite.config.ts` —
   `SHADER_CHUNKS_TO_EMPTY` (W11+W12) extended from 11
   entries to 53 (+42 new). The new entries empty:

   - `tonemapping_pars_fragment`, `tonemapping_fragment`
     (autotable uses default `NoToneMapping`).
   - `lights_phong_fragment`, `lights_phong_pars_fragment`,
     `lights_toon_fragment`, `lights_toon_pars_fragment`,
     `lights_physical_fragment`,
     `lights_physical_pars_fragment` (W9-stubbed materials —
     the lighting chains for those materials are deadweight).
   - `transmission_fragment`, `transmission_pars_fragment`,
     `iridescence_fragment`, `iridescence_pars_fragment`,
     `clearcoat_pars_fragment`,
     `clearcoat_normal_fragment_begin`,
     `clearcoat_normal_fragment_maps` (PBR extras — no scene
     material sets the corresponding properties).
   - 15 map-feature `_fragment` / `_pars_fragment` chunks:
     `alphamap_*` (2), `alphahash_*` (2), `alphatest_*` (2),
     `aomap_*` (2), `lightmap_*` (2), `emissivemap_*` (2),
     `bumpmap_pars_fragment`, `normal_*` partial (kept where
     `lights_lambert_fragment` reads `geometryNormal`),
     `specularmap_pars_fragment` only (the `_fragment` body
     is KEPT because its `#else` branch sets
     `specularStrength = 1.0` that `lights_lambert_fragment`
     reads),
     `metalnessmap_*` (2), `roughnessmap_*` (2),
     `displacementmap_*` (2).
   - `fog_fragment`, `fog_pars_fragment`, `fog_vertex`,
     `fog_pars_vertex` (no scene fog).
   - `dithering_fragment`, `dithering_pars_fragment`,
     `premultiplied_alpha_fragment` (autotable uses
     premultipliedAlpha=false, no dithering).

   All targets verified guarded by `#ifdef USE_<MACRO>` (or
   feature-flag include) via inline three.module.js audit
   before stripping.

2. **`UniformsLib` deeper strip.**
   `vite.config.ts` — `UNIFORMS_LIB_KEYS_TO_EMPTY` (W12)
   extended from 5 to 14 entries (+9). The new entries:
   `specularmap`, `envmap`, `aomap`, `lightmap`, `bumpmap`,
   `normalmap`, `displacementmap`, `emissivemap`, `fog`.
   Each holds the JS-side uniform values consumed by the
   `USE_<MACRO>`-guarded shader bodies stripped under
   deliverable #1.

   **W13 chunk-strip + UniformsLib-strip combined result:
   `three-renderer-big = 448,648 B → 406,635 B` (−42,013 B
   / −9.4 %).** ~34 kB margin under the < 440 kB stretch
   target; ~39 kB margin under the < 445 kB acceptable
   target. Strip log: shaderchunk-strip 85 strings →
   95,757 chars saved (W12 was 43 strings → 52,437);
   uniformslib-strip 14 entries → 2,260 chars saved (W12
   was 5 entries → 945).

   Cumulative drop from W6 baseline: 739.72 kB → 406.64 kB
   (−45.0 %) over seven waves.

3. **LH13 workflow threshold hard-pin — DEFERRED TO W14.**
   `gh run list -w pwa-audit.yml -L 30` against the bringup
   branch failed with a credentials error (the W13 CLI
   runtime does not have a working `GH_TOKEN`). Per the W12
   hand-off explicit fallback path:

   > If <3 successful cron runs are available, defer to W14,
   > document calibration progress, and notify Vasquez via
   > memo.

   No modification was made to
   `.github/workflows/pwa-audit.yml` this wave. The current
   threshold gates remain the W11 calibration values from
   `docs/frontend-pwa-audit.md §7`. The W14 contract
   (verify cron-data point count with a working GH_TOKEN,
   compute p95, hard-pin in a co-bump PR with Vasquez) is
   captured in `docs/frontend-pwa-audit.md §10` and in the
   W13 memo `.squad/decisions/inbox/hicks-phase-k-wave-13.md`.

4. **Visual-regression baselines.**
   Captured the three `manifest-screenshots-visual.spec.ts`
   baselines at the Jest-style location:

   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/main-game.png`
     (1280x720, ~40 kB)
   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/spectator-commentary.png`
     (1280x720, ~28 kB)
   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/tournament-dashboard.png`
     (1280x720, ~40 kB)

   Captured against the K13 build via a side-channel script
   `src/frontend/autotable-src/scripts/capture-visual-baselines.js`
   that uses the Playwright runtime API directly. This
   workaround exists because Vasquez W12 spec has a latent
   bug: it calls `page.setContent()` without a prior
   `page.goto(BASE_URL)`, so the `<img src="/screenshots/...">`
   in the HTML resolves against `about:blank` and Chromium
   404s the asset; the spec then catches the
   `locator.waitFor()` timeout and exits via its
   forward-staged annotation path WITHOUT writing a
   baseline, even when invoked with `--update-snapshots=all`.

   The spec fix (add `page.goto(BASE_URL)` before
   `setContent()`, add `snapshotPathTemplate` to
   `playwright.config.ts` so the spec reads from the
   Jest-style location) belongs in Vasquez W14 lane and is
   handed off in
   `docs/frontend-pwa-audit.md §11.5` + the W13 memo.

5. **`?action=spectate&gameId=<id>` deep-link routing.**
   `src/frontend/autotable-src/src/action-router.ts` —
   the W11 `?action=spectate` keyword now branches on the
   presence of a `gameId` co-param:

   - **No `gameId`** (W11 behaviour, unchanged) — activate
     the public-games tab.
   - **With `gameId`** (new W13) —
     POST `/api/spectator/handoff` (Bishop W12) with
     `{ gameId }` body, credentials-included. On 200
     navigate to `/spectate/<id>?token=<jwt>#/spectate/<id>`
     via `history.replaceState` AND directly call
     `openSpectatorLivestream({ tableId: gameId })`
     (because `replaceState` with a combined path+hash
     does not emit `hashchange`). On 401 redirect to `/` so
     `installAuthUi()` mounts the sign-in modal at boot.
     On 404 / 5xx / network show a transient "Game not
     found" toast and rewrite the URL to `/spectate`
     (lobby landing).

   New surface: `dispatchSpectateWithGameId()`,
   `fetchHandoffAndOpenSpectator()`,
   `redirectToLobbyForSignIn()`, `showGameNotFoundToast()`.
   `dispatchSpectate()` now branches on the `gameId`
   co-param. `parseActionFromUrl()` already extracted the
   `gameId` in W12; W13 only added the consumer side.

6. **bundle-health.yml CI workflow.**
   `.github/workflows/bundle-health.yml` (new) —
   per-PR auto-report. Triggers on `pull_request` open/sync
   for any change touching the frontend; builds the bundle
   with `WAVE_NAME=PR-<n>` (so the PR row in
   `dist-size.json` is segregated from the canonical
   `K<N>` history rows); parses the last history row;
   computes the verdict:

   - **pass** when current ≤ W12 baseline × 1.02 AND ≤ 445 kB
   - **warn** when current > W12 baseline × 1.02 OR > 445 kB
   - **fail** when current > 500 kB (hard-fail, blocks merge)

   Posts a sticky PR comment via
   `peter-evans/create-or-update-comment@v4` with marker
   `<!-- bundle-health-report -->`; uploads the report JSON
   as an artifact. The workflow's Node verdict logic was
   smoke-tested locally against the W13 build — verdict =
   `pass` (current 406.64 kB < W12 × 1.02 = 446.96 kB AND
   < 445 kB).

### Targets met

| Target | Goal | Actual | Status |
|--------|------|--------|--------|
| `three-renderer-big` (stretch) | < 440 kB | 406.64 kB | ✅ (~34 kB margin) |
| `three-renderer-big` (acceptable) | < 445 kB | 406.64 kB | ✅ (~38 kB margin) |
| LH13 hard-pin | If ≥3 cron runs available | <3 verifiable in env | ⏸ deferred to W14 |
| Visual-regression baselines | 3 baselines committed | 3 PNGs committed | ✅ |
| `?action=spectate&gameId` route | Round-trips Bishop handoff endpoint | implemented | ✅ |
| bundle-health.yml | Sticky PR comment + pass/warn/fail verdict | implemented + smoke-tested | ✅ |

### Hand-off to W14

1. **LH13 hard-pin** — re-attempt with a working
   `GH_TOKEN`; pull the cron data; compute p95; hard-pin in
   a co-bump PR with Vasquez. Source-of-truth section:
   `docs/frontend-pwa-audit.md §10`.
2. **Visual-regression spec fix** (Vasquez lane) — add
   `await page.goto(BASE_URL)` before `setContent()` in
   `manifest-screenshots-visual.spec.ts`; add
   `snapshotPathTemplate: 'tests/e2e/__screenshots__/{testFilePath}/{arg}{ext}'`
   to `playwright.config.ts` so the spec reads the W13
   baselines from the Jest-style location. Source-of-truth:
   `docs/frontend-pwa-audit.md §11.5`.
3. **Further strip candidates (Phase L candidate)** —
   `logdepthbuf_*`, `clipping_planes_*`, plus partial
   `normal_*` trimming would each shave ~400-600 B. Combined
   they could push the chunk under <400 kB; aggressive Phase
   L hand-roll spike (W6 estimate −200 to −300 kB) remains
   the larger play.
4. **Action-router co-parameter schema layer** (carried fwd
   from W12 hand-off) — when a fifth keyword lands with its
   own co-param, generalise the W12 `replayId` + W13
   `gameId` parse-strip-refetch pattern via a per-action
   `parseCoParams<T>()` helper.
5. **Visual-regression real captures** — once the W14 spec
   fix lands, replace the K13-build placeholder captures
   with the real, fully-rendered table / spectator /
   tournament screens (the current baselines are the
   manifest-declared screenshot assets themselves, not
   live-rendered surfaces). Owner: Vasquez or Hicks (TBD in
   W14 charter).

### Cross-references

- `docs/frontend-three-budget.md §9` — W13 PMREMGenerator
  deeper strip write-up + risk/back-out matrix + trend
  ledger.
- `docs/frontend-three-budget.md §10` — bundle-health CI
  workflow recipe.
- `docs/frontend-routing.md §3` + new §3.1 — W13
  `?action=spectate&gameId=<id>` co-parameter contract.
- `docs/frontend-pwa-audit.md §10` — LH13 hard-pin deferral
  + W14 dispatch contract.
- `docs/frontend-pwa-audit.md §11` — visual-regression
  baselines + the spec setContent bug + Vasquez W14
  follow-ups.
- `src/frontend/autotable-src/tests/selectors.md` — W13
  Hicks footer (`?action=spectate&gameId` contract, K13
  dist-size pin, W13 strip extension note, visual baselines
  pointer, bundle-health workflow declaration).
- `Phase_K_W13/Hicks/charter.md` + `history.md` (this file)
  — wave-scoped hand-off artefacts.
- `.squad/decisions/inbox/hicks-phase-k-wave-13.md` — memo
  with LH13 deferral notice to Vasquez and the W14
  follow-up dispatch.

### Model

Stephen's standing directive `claude-opus-4.7-xhigh` honoured
throughout the wave. Not downgraded.
