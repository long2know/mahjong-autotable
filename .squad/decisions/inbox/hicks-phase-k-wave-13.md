# Hicks — Phase K Wave 13 decisions memo

Branch: `stlong/phase-k-wave-13-bringup`
Author: Hicks (Frontend)
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

5 deliverables per the W13 directive (4 ship, 1 deferred with
documented rationale + memo notification to Vasquez):

1. **PMREMGenerator deeper strip
   (tonemapping_* + PBR-extras + map-feature chains).**
   `src/frontend/autotable-src/vite.config.ts` —
   `SHADER_CHUNKS_TO_EMPTY` (W11+W12) extended from 11 → 53
   entries (+42); `UNIFORMS_LIB_KEYS_TO_EMPTY` (W12)
   extended from 5 → 14 entries (+9). All new candidates
   verified guarded by `#ifdef USE_<MACRO>` via inline
   three.module.js audit. New strips: `tonemapping_*`,
   `lights_phong/toon/physical_*` (W9-stubbed material
   chains), `transmission_*`, `iridescence_*`,
   `clearcoat_*` partials, `dithering_*`,
   `premultiplied_alpha_fragment`, every map-feature
   `_fragment` / `_pars_fragment` chain (alphamap,
   alphahash, alphatest, aomap, lightmap, emissivemap,
   bumpmap, normalmap, specularmap_pars, metalnessmap,
   roughnessmap, displacementmap), `fog_*`. NOT stripped
   for safety (decisions documented in
   `docs/frontend-three-budget.md §9`):

   - `opaque_fragment` — unconditional `gl_FragColor` assign.
   - `colorspace_fragment` — unguarded one-liner; safe in
     `LinearSRGBColorSpace` default but fragile under
     future colour-space change.
   - `specularmap_fragment` — `#else` branch sets
     `specularStrength = 1.0` that `lights_lambert_fragment`
     reads downstream.

   **Result: `three-renderer-big = 448,648 B → 406,635 B`
   (−42,013 B / −9.4 %).** ~34 kB margin under the < 440 kB
   stretch target; ~38 kB margin under the < 445 kB
   acceptable target. Vasquez's W10 hard-cap
   (`three-renderer-480-hard.spec.ts`), W11 soft-pin
   (`three-renderer-475-soft.spec.ts`), and W12 stretch
   (`three-renderer-450-stretch.spec.ts`) all pass
   trivially.

2. **LH13 workflow threshold hard-pin — DEFERRED TO W14.**
   `gh run list -w pwa-audit.yml -L 30` failed with a
   credentials error: the W13 CLI runtime does not have a
   working `GH_TOKEN`. Per the W12 hand-off explicit
   fallback path:

   > If <3 successful cron runs are available, defer to
   > W14, document calibration progress, and notify Vasquez
   > via memo.

   No modification was made to
   `.github/workflows/pwa-audit.yml` this wave. The
   threshold gates remain the W11 calibration values from
   `docs/frontend-pwa-audit.md §7` (Performance ≥ 90,
   Accessibility ≥ 100, Best Practices ≥ 95, PWA ≥ 90).

   **Notice to Vasquez (threshold owner of record per W11
   §6.1):** the LH13 hard-pin is the W14 critical-path
   item. W14 contract:

   1. Re-attempt `gh run list -w pwa-audit.yml -L 30` with a
      working `GH_TOKEN`. Pull JSON report artifacts from
      each `success` row.
   2. Compute p95 + p99 across the score arrays for each
      category.
   3. Update the `pwa-audit.yml` `assertions:` block with
      p95 (or p95-rounded-down) for each category.
   4. Bump the comment block in `pwa-audit.yml` to cite the
      W14 calibration source data.
   5. Land in a co-bump PR with Hicks (Hicks owns the
      vite.config.ts + tests/selectors.md side; Vasquez
      owns the spec gate).

   Source-of-truth section:
   `docs/frontend-pwa-audit.md §10`.

3. **Visual-regression baselines (3 PNGs).**
   Captured the three
   `manifest-screenshots-visual.spec.ts` baselines for
   `main-game`, `spectator-commentary`,
   `tournament-dashboard` at the Jest-style location
   `src/frontend/autotable-src/tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/<slug>.png`.

   Captured via a new side-channel script
   `src/frontend/autotable-src/scripts/capture-visual-baselines.js`
   that uses the Playwright runtime API directly. This
   workaround is necessary because Vasquez's W12 spec has a
   latent bug: `page.setContent(html, ...)` runs against
   `about:blank` without a prior `page.goto(BASE_URL)`, so
   the relative `<img src="/screenshots/...">` 404s and the
   spec exits via its forward-staged annotation path
   WITHOUT writing baselines, even when invoked with
   `--update-snapshots=all`.

   **Notice to Vasquez (spec owner):** Two fixes belong in
   your W14 lane:

   1. Add `await page.goto(BASE_URL)` (or
      `goto(BASE_URL + '/manifest.webmanifest')`) **before**
      `setContent()` in
      `tests/e2e/manifest-screenshots-visual.spec.ts`. The
      origin must be the static server's origin for the
      relative `<img>` src to resolve.
   2. Add `snapshotPathTemplate:
      'tests/e2e/__screenshots__/{testFilePath}/{arg}{ext}'`
      to `playwright.config.ts` so the spec reads from the
      Jest-style location where W13 wrote the baselines.
      Without this, the spec will create a parallel
      baseline at Playwright's default
      `tests/e2e/<spec>-snapshots/<arg>-<projectName>-<platform>.png`
      and the W13 baselines become orphaned.

   Source-of-truth section:
   `docs/frontend-pwa-audit.md §11`.

4. **`?action=spectate&gameId=<id>` deep-link routing.**
   `src/frontend/autotable-src/src/action-router.ts` —
   the W11 `?action=spectate` keyword now branches on the
   presence of a `gameId` co-param. New helpers:
   `dispatchSpectateWithGameId(gameId)`,
   `fetchHandoffAndOpenSpectator(gameId)`,
   `redirectToLobbyForSignIn()`,
   `showGameNotFoundToast()`. `dispatchSpectate()` now
   detects the co-param and delegates.

   Wire sequence (Bishop W12 endpoint unchanged):

   - POST `/api/spectator/handoff` `{ gameId }`,
     credentials-included.
   - **200** → `{ token, expiresAt, scope:
     "spectator:<gameId>", ttlSeconds }`. Router rewrites
     URL to
     `/spectate/<id>?token=<jwt>#/spectate/<id>` via
     `history.replaceState` AND directly calls
     `openSpectatorLivestream({ tableId: gameId })`
     (replaceState with combined path+hash does NOT emit
     `hashchange`).
   - **401** → `redirectToLobbyForSignIn()` rewrites URL
     to `/` and reloads so `installAuthUi()` mounts the
     sign-in modal at boot. No post-login resume (JWT
     contract is short-lived).
   - **404 / 5xx / network** → `showGameNotFoundToast()`
     + rewrite URL to `/spectate` (lobby landing).

   The W11 bare `?action=spectate` keyword (lobby-tab
   activation) is unchanged.

   **Notice to Bishop:** the W13 client matches the W12
   wire shape exactly. Any future change to the JWT scope
   shape or `ttlSeconds` semantics needs a Bishop+Hicks
   co-bump.

   Source-of-truth section:
   `docs/frontend-routing.md §3.1`.

5. **bundle-health.yml CI workflow.**
   `.github/workflows/bundle-health.yml` (NEW) — per-PR
   bundle-size auto-report. Triggers on `pull_request`
   open/sync for any frontend touch. Builds with
   `WAVE_NAME=PR-<n>` so the row in `dist-size.json` is
   segregated from canonical `K<N>` rows. Computes verdict:

   - **pass** when current ≤ W12 baseline × 1.02 (≤ 446.97
     kB) AND ≤ 445 kB.
   - **warn** when current > W12 baseline × 1.02 OR > 445
     kB.
   - **fail** when current > 500 kB (hard-fail, blocks
     merge).

   Posts a sticky PR comment via
   `peter-evans/create-or-update-comment@v4` with marker
   `<!-- bundle-health-report -->`. Uploads the report JSON
   as an artifact for audit.

   **Lane policy note:** `.github/workflows/` is shared CI;
   the W10 `pwa-audit.yml` set the precedent that Hicks may
   stage new `.github/workflows/pwa-*.yml`. W13 extends
   that allowance to one new non-pwa-prefixed workflow
   (`bundle-health.yml`) on the strength of the W13
   directive listing it as a Hicks deliverable. Apone is
   notified via this memo of the new workflow file; future
   modifications to `bundle-health.yml` follow the same
   shared-CI co-bump pattern.

   Source-of-truth section:
   `docs/frontend-three-budget.md §10`.

## Targets met / missed

| Target | Goal | Actual | Status |
|--------|------|--------|--------|
| `three-renderer-big` (stretch) | < 440 kB | 406.64 kB | ✅ (~34 kB margin) |
| `three-renderer-big` (acceptable) | < 445 kB | 406.64 kB | ✅ |
| LH13 hard-pin | If ≥3 cron runs available | <3 verifiable | ⏸ deferred to W14 |
| Visual-regression baselines | 3 baselines | 3 PNGs committed | ✅ |
| `?action=spectate&gameId` | Round-trips handoff endpoint | implemented + 4-branch error surface | ✅ |
| bundle-health.yml | Sticky PR comment + verdict | implemented + smoke-tested | ✅ |

## Open hand-offs to W14

1. **LH13 hard-pin (Vasquez)** — needs working `GH_TOKEN`
   to verify cron data points. See deliverable #2 above.
2. **Visual-regression spec fix (Vasquez)** — add
   `page.goto(BASE_URL)` + `snapshotPathTemplate`. See
   deliverable #3 above.
3. **Phase L preview** — remaining strip candidates are
   sub-kB each (`logdepthbuf_*`, `clipping_planes_*`); the
   <400 kB ceiling needs the Phase L hand-roll spike (W6
   estimate −200 to −300 kB).
4. **Action-router co-parameter schema layer** — carried
   fwd from W12; with W13 adding `gameId` as a second
   co-param, the `parseCoParams<T>()` generalisation
   becomes higher value.
5. **Real visual-regression captures (Vasquez or Hicks
   TBD)** — once the W14 spec fix lands, replace the
   placeholder manifest-screenshot baselines with
   live-rendered table / spectator / tournament surfaces.

## Files touched

- `src/frontend/autotable-src/vite.config.ts`
- `src/frontend/autotable-src/src/action-router.ts`
- `src/frontend/autotable-src/scripts/capture-visual-baselines.js`
  (NEW)
- `src/frontend/autotable-src/tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/{main-game,spectator-commentary,tournament-dashboard}.png`
  (NEW — 3 binary baselines)
- `src/frontend/autotable-src/dist-size.json` (K13 row)
- `src/frontend/autotable/*` (rebuilt content-hashed
  chunks)
- `.github/workflows/bundle-health.yml` (NEW)
- `docs/frontend-three-budget.md` (§9 + §10)
- `docs/frontend-routing.md` (§3 + §3.1)
- `docs/frontend-pwa-audit.md` (§10 + §11)
- `src/frontend/autotable-src/tests/selectors.md` (W13
  Hicks footer)
- `Phase_K_W13/Hicks/{charter,history}.md` (NEW)
- `.squad/agents/hicks/history.md` (W13 entry)
- `.squad/decisions/inbox/hicks-phase-k-wave-13.md` (this
  memo)

## Identity discipline

- Per-command git env:
  `git -c user.name="Hicks (Frontend)" -c user.email="hicks@squad.mahjong"`.
- NEVER `git config user.name`.
- Flock-wrapped at `.work/squad-git-lock` (-w 120).
- Only lane-allowed paths staged.
- `Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>` trailer
  included.

## Model

Stephen's standing directive `claude-opus-4.7-xhigh`
honoured throughout the wave. Not downgraded.
