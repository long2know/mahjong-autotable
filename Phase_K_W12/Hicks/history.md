# Hicks — Phase K Wave 12 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 12 — Frontend bring-up

Branch: `stlong/phase-k-wave-12-bringup`
Bringup-on commit (W11 close): `ee9dba0` (W11 PR #57 merged
into the bringup branch).

### Deliverables (six)

1. **PMREMGenerator-adjacent ShaderChunk strip (envmap_*).**
   `src/frontend/autotable-src/vite.config.ts` —
   `SHADER_CHUNKS_TO_EMPTY` (W11) extended with six new
   entries:

   - `envmap_fragment`
   - `envmap_common_pars_fragment`
   - `envmap_pars_fragment`
   - `envmap_pars_vertex`
   - `envmap_physical_pars_fragment`
   - `envmap_vertex`

   Each body is wrapped in `#ifdef USE_ENVMAP`. The autotable
   scene constructs `MeshBasicMaterial` / `MeshLambertMaterial`
   / `LineBasicMaterial` / the W7 `CustomOutline` ShaderMaterial
   only; none of those classes ever set the `envMap` property
   nor enable `scene.environment`, so the macro is never
   defined and the GLSL preprocessor strips the include
   bodies at runtime anyway. Emptying the JS strings drops
   ~10 kB of carrying weight from the renderer chunk.

2. **`UniformsLib` unused-entry strip.**
   `vite.config.ts` — new
   `stripUnusedUniformsLib()` Vite plugin (mirrors the W9
   `stripModuleFeatures` brace-walker pattern). Operates on
   `three.module.js` with `enforce: 'pre'`. Targets the
   `UniformsLib = { ... }` registry and rewrites five
   W9-stubbed-material keys to empty object literals (so
   `ShaderLib.*.uniforms = UniformsUtils.merge([ ... ])` calls
   still resolve):

   - `roughnessmap` (W9-stubbed `MeshStandardMaterial`)
   - `metalnessmap` (W9-stubbed `MeshStandardMaterial`)
   - `gradientmap` (W9-stubbed `MeshToonMaterial`)
   - `points` (W9-stubbed `PointsMaterial`)
   - `sprite` (W9-stubbed `SpriteMaterial`)

   Plugin registered between the W11 `stripUnusedShaderChunks`
   and `copyStaticAssets` in the `plugins:` array.

3. **`shadowmap_*` + `shadowmask_*` chunk body strip.**
   `SHADER_CHUNKS_TO_EMPTY` (W11) extended with four more
   entries:

   - `shadowmap_pars_fragment`
   - `shadowmap_pars_vertex`
   - `shadowmap_vertex`
   - `shadowmask_pars_fragment`

   Same `#ifdef USE_SHADOWMAP` guard pattern as envmap. The
   autotable's `WebGLRenderer` never sets
   `renderer.shadowMap.enabled` and no light has
   `castShadow = true`, so the macro is never defined.
   `shadowmask_pars_fragment` defines `getShadowMask()` —
   only called from `shadow_frag` (W9-stripped) — safe to
   empty entirely.

   **W12 chunk-strip + UniformsLib-strip combined result:
   `three-renderer-big = 466,395 B → 448,648 B`
   (−17,747 B / −3.8 %)**. ~1.4 kB margin under the < 450 kB
   stretch target; 11.4 kB margin under the < 460 kB
   acceptable target.

4. **LH13 workflow threshold edit — DEFERRED TO W13.**
   `gh run list --workflow=pwa-audit.yml` against the
   bringup branch returned **0 nightly cron runs** since the
   W11 §7 calibration landed. Per the W12 directive, the
   edit is conditional on ≥ 3 cron data points (per p95
   stability). Deferral documented in new
   `docs/frontend-pwa-audit.md §9` with the W13 follow-up
   procedure (re-read LH13 scores from artefact bundles,
   recompute p50/p95 against the CI-jitter-inclusive sample,
   then wire the calibrated thresholds into the workflow
   gate).

5. **W10 placeholder screenshot copy block removal.**
   `vite.config.ts:copyStaticAssets` — the W10 fallback loop
   that copied `img/screenshot-{lobby,table,mobile}.auto.png`
   into `dist/img/` has been deleted (replaced with a W12
   comment noting the retirement). The three source PNGs are
   `git rm`'d:

   - `src/frontend/autotable-src/img/screenshot-lobby.auto.png`
   - `src/frontend/autotable-src/img/screenshot-table.auto.png`
   - `src/frontend/autotable-src/img/screenshot-mobile.auto.png`

   The W11 manifest already pointed only at the real captures
   at `screenshots/{main-game,spectator-commentary,
   tournament-dashboard}.png`; the legacy paths were never
   surfaced in any live manifest.

6. **`?action=replay&replayId=<guid>` deep-link routing.**
   `src/action-router.ts` — `SUPPORTED_ACTIONS` now includes
   `'replay'`. New private helpers `dispatchReplay`,
   `fetchAndOpenReplay`, `showReplayNotFoundToast`. Switch
   case in `handlePwaActionFromUrl` reads the `replayId`
   co-parameter, strips BOTH `action` and `replayId` from the
   URL (refresh-safe), fetches `GET /api/replays/{replayId}`
   against Bishop's W12 endpoint, parses the JSON body, and
   on success lazy-imports `./replay-launcher` to call the
   new `openReplayPayload(replayId, body, options?)` export
   while rewriting the URL to `/replay/{replayId}` via
   `history.replaceState()`. ANY failure path
   (404 / 5xx / network / JSON parse / missing co-param) →
   `showToast('Replay not found', 'error')` from `./toast`,
   no URL rewrite. No fallback to the legacy
   `/api/games/{gameId}/replay` endpoint — would mask config
   drift.

### Commit identity + flock discipline

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

### Build verification

- `npm run build` succeeds with all four strip plugins
  active.
- `[shaderchunk-strip] emptied 43 strings (52,437 chars
  saved)` — W11 emptied 33 strings, W12 adds 10 more.
- `[uniformslib-strip] emptied 5 entries (945 chars saved)` —
  new in W12.
- `[module-strip] emptied 4 features (59,253 chars saved)` —
  W9/W10 carry-forward, unchanged.
- `[material-strip] stubbed 11 materials (58,941 chars
  saved)` — W9 carry-forward, unchanged.
- `dist/three-renderer.<hash>.js = 448,648 B`.
- TypeScript strict: no new errors (3 pre-existing errors in
  unrelated e2e specs left untouched, per scope).

### dist-size.json ledger update

`src/frontend/autotable-src/dist-size.json` —
`history[wave=K12]` appended with the new chunk sizes.
`current` field set to `"K12"`. The W11 row was preserved
verbatim. Trend-decrease invariant holds for the 7th
consecutive wave:

```
W6 740 → W7 532 → W8 532 → W9 507 → W10 497 → W11 466 → W12 449
```

(All values are the `three-renderer-big` kB-rounded number.)

### Open hand-offs to W13

1. **`opaque_fragment` + `colorspace_fragment` +
   `tonemapping_*` ShaderChunk strip** — carried fwd from
   W11; W12 didn't touch them. Yield ~3-5 kB.
2. **Remaining `UniformsLib` features** — clearcoat,
   iridescence, sheen, transmission, anisotropy, dispersion,
   reflectivity-extras. All routed through `ShaderLib.physical`
   (W11-stubbed). Aggregate ~1-2 kB.
3. **`lights_phong_*` / `lights_toon_*` / `lights_physical_*`
   ShaderChunks** — autotable uses `AmbientLight` +
   `DirectionalLight` only. ~0.5-2 kB per chunk.
4. **LH13 threshold edit** (carried fwd from W12 deferral) —
   walk a11y / seo / bp / perf thresholds in `pwa-audit.yml`
   down to the §7 calibrated values once ≥ 3 cron data
   points are available.
5. **Visual-regression spec** for the W11 captures (Vasquez
   lane — still open).
6. **Bishop W12 `/api/replays/{replayId}` endpoint
   integration test** — Vasquez should add a Playwright spec
   `deep-link-action-replay.spec.ts` that mocks the endpoint
   (404 + 200 + malformed-JSON cases) and asserts the
   URL-rewrite + toast contract.
7. **Action-router co-parameter schema layer** (deferred
   from routing-doc §9 hand-off) — when a fifth keyword
   lands with its own co-param, generalise the W12
   `replayId` parse-strip-refetch pattern via a per-action
   `parseCoParams<T>()` helper.

### Cross-references

- `docs/frontend-three-budget.md §8` — W12 ShaderChunk +
  envmap + UniformsLib strip write-up + risk/back-out matrix
  + trend ledger.
- `docs/frontend-routing.md §2` (W12 subsection) + §3 (table
  row) + §7 (reservation list — `replay` moved out, marked
  cashed in) + §9 (W13 hand-off refreshed).
- `docs/frontend-pwa-audit.md §9` — LH13 threshold-edit
  deferral.
- `src/frontend/autotable-src/tests/selectors.md` — W12
  footer (`?action=replay` contract, K12 dist-size pin,
  W10 placeholder retirement note, W12 strip extension
  note).
- `Phase_K_W12/Hicks/charter.md` + `history.md` (this file)
  — wave-scoped hand-off artefacts.

### Model

Stephen's standing directive `claude-opus-4.7-xhigh` honoured
throughout the wave. Not downgraded.
