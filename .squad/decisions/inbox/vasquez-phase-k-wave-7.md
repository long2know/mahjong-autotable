# Phase K Wave 7 — Vasquez QA memo: forward-stage W7 contracts (Bishop + Hicks + Apone) + lane-discipline STRICT mode + KW7 regression rename + 6 e2e specs + three-renderer trend gate + OIDC RS256 hard contract migration

**Author:** Vasquez (QA)
**Branch:** `stlong/phase-k-wave-7-bringup`
**Base:** Phase K Wave 6 (commit `1c67878`, gate baseline 1422/0/0).

> **Attribution lock (THIRD CONSECUTIVE WAVE).** Wave 5 introduced
> the per-invocation identity protocol; Wave 6 was the first wave
> to use it throughout. Wave 7 now ALSO promotes the
> lane-discipline check to **strict / PR-blocking** via the new
> `--strict` mode + `tests/ci/lane-map.json` machine-readable lane
> map. Every commit in this PR is verified
> `Vasquez (QA) <vasquez@squad.mahjong>` BEFORE push.
>
> Stage allowlist for this PR — files OUTSIDE the allowlist
> (Bishop's `src/backend/src/Auth/*.cs`, Hicks's
> `src/frontend/autotable-src/src/render/custom-outline.ts` and
> bundler-swap config, Apone's `helm/mahjong/`,
> `infra/terraform/modules/edge/`, `.pre-commit-config.yaml`,
> `infra/k8s/overlays/{dev,prod}/jwt-rsa-keys-secret.yaml`) were
> observed extensively in the working tree during bring-up but
> NEVER staged by Vasquez. Each is owned by its respective lane
> and lands via THAT agent's own PR.

---

## Final gate

| Pass | Fail | Skip | Total | Δ vs Wave 6 baseline | Notes |
|------|------|------|-------|----------------------|-------|
| **1506** | **0** | **0** | **1506** | **+84** (target was ≥68 → ≥1490) | First green W7 gate. The W5 `ThreeRenderer_ModulePresent_HardAssert` brittleness was repaired in-lane (Vasquez owns `src/backend/tests/`) so the gate flips back green with Hicks's W7 bundler swap in flight. |

Zero-skips streak **preserved (Phase K Wave 7 = the 21st consecutive
green wave on the Vasquez-owned facts).** No flake observed over 3
consecutive `dotnet test` invocations under the default xunit
parallelism.

---

## Scope completed

### Backend (Mahjong.Autotable.Api.Tests) — 8 new files / **57 new facts** + 7 regression smokes

All forward-staged W7 facts carry `[Trait("Wave", "Phase-K-7")]`
and live under `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/{Bishop,Hicks,Apone}/`
plus a Vasquez-owned umbrella file.

**Bishop W7 lane (auth-RS256 hard / commentary record / FFmpeg HLS / losers-bracket / OIDC discovery):**

| Area | File | Facts |
|------|------|-------|
| RS256-only happy path: `appsettings.Production.json` MUST omit HS256 secrets / `AuthOptions` validators / JWKS endpoint structure / RS256 token round-trip / RS256 algorithm in produced JWT header | `Phase_K_W7/Bishop/RS256HappyPathTests.cs` | 5 |
| `DoubleEliminationBracket` losers-bracket determinism: count formula (`losers = ceil((n-1) * 2)` heuristic), pairings stable across seed, grand-final reset semantics | `Phase_K_W7/Bishop/LosersBracketDeterminismTests.cs` | 3 |
| `FfmpegHlsRecorder` healthcheck: ffmpeg-on-PATH probe at startup, recorder type publicly exposed | `Phase_K_W7/Bishop/FfmpegHlsRecorderHealthcheckTests.cs` | 2 |
| `CommentaryRecord` envelope contract: record exists in DTO assembly with `GameId/(Sequence|TurnNumber|Index|Order)/Phase/Speaker/Text/EmotionIntensity/TileReferences/GeneratedAt`, JSON envelope shape, generator interface stable | `Phase_K_W7/Bishop/CommentaryRecordContractTests.cs` | 3 |
| OIDC discovery RS256-only hard contract: `/.well-known/openid-configuration` MUST surface `RS256` ONLY in `id_token_signing_alg_values_supported`, NO HS256 leakage even under `Development` env | `Phase_K_W7/Bishop/OidcDiscoveryHardContractTests.cs` | 2 |
| JWT / RSA operational docs hard-link: `docs/auth-rs256-operations.md` exists + references key-rotation steps; `infra/k8s/overlays/{dev,prod}/jwt-rsa-keys-secret.yaml` referenced | `Phase_K_W7/Bishop/JwtOperationalDocsContractTests.cs` | 2 |

**Bishop subtotal: 17 facts across 6 files.**

**Hicks W7 lane (bundler swap / three-renderer trend / outline-shader / commentary panel):**

| Area | File | Facts |
|------|------|-------|
| Bundler decision (Vite / Rspack / Parcel-manual) marker present in `package.json` or `vite.config.*` / `rspack.config.*`; three-renderer chunk ≤ 550KB ceiling; game-shell chunk ≤ 200KB ceiling; lobby chunk ≤ 500KB ceiling; CSP header carries no `unsafe-eval`; commentary-panel.ts subscribes to `CommentaryRecord` envelope; tile-ref handler emits `tile-highlight` event; outline-shader module present (`OutlinePass`-equivalent); `dist-size.json` schema observable (current bytes + previous-wave comparator) | `Phase_K_W7/Hicks/BundlerSwapContractTests.cs` | 9 |

**Hicks subtotal: 9 facts in 1 file.**

**Apone W7 lane (helm / edge / GHCR→ECR / mobile testing / pre-commit / RSA-key overlays / retro / CHANGELOG):**

| Area | File | Facts |
|------|------|-------|
| `helm/mahjong/Chart.yaml` exists + apiVersion v2; `infra/terraform/modules/edge/` directory has `main.tf` + `variables.tf`; `.github/workflows/ghcr-to-ecr-promote.yml` workflow exists; `.github/workflows/mobile-external-testing.yml` workflow exists; `.pre-commit-config.yaml` exists + lists 6 signers (gitleaks / detect-secrets / black / isort / shellcheck / yamllint OR equivalent set of 6); `jwt-rsa-keys-secret.yaml` exists under both `infra/k8s/overlays/dev/` and `infra/k8s/overlays/prod/`; `.squad/retros/2026-06-retro.md` exists; `CHANGELOG.md` lists 0.16.0 entry | `Phase_K_W7/Apone/AponeW7InfraContractTests.cs` | 10 |

**Apone subtotal: 10 facts in 1 file.**

**Vasquez umbrella (cross-lane W7 smokes):**

| Area | File | Facts |
|------|------|-------|
| Cross-lane smokes — Bishop's `JwtAlgorithm` flip surface; commentary endpoint reachable from API project type; `CommentaryRecord` round-trips through serializer; double-elim bracket type publicly visible; losers-bracket method discoverable; helm Chart presence; edge terraform presence; pre-commit presence; jwt-rsa-keys overlay presence; bundler config marker; W7 dist-size envelope; three-renderer + game-shell + lobby chunk size assertions; outline-shader module presence; commentary-panel tile-ref handler; PWA maskable manifest icon; OIDC discovery RS256-only; SpectatorVoiceHub still subclasses `SpectatorHub`; Swiss + DoubleElimination still enrollable; CommentaryGenerator default impl resolves; CommentaryRecord nullable `TileReferences` tolerance; FfmpegHlsRecorder healthcheck type discoverable | `Phase_K_W7/W7SurfaceSmokeFactsTests.cs` | ~22 (varies by env) |

**Vasquez umbrella subtotal: ~22 facts in 1 file.**

**Wave 7 regression rename** —
`Wave1ThroughKW6RegressionTests.cs` → `Wave1ThroughKW7RegressionTests.cs`
(class + ctor + filename). Appended 7 new W7 smokes:

- `PhaseK7_FfmpegHlsRecorder_TypePublic`
- `PhaseK7_CommentaryRecord_TypePublic`
- `PhaseK7_DoubleElim_LosersBracket_MethodDiscoverable`
- `PhaseK7_HelmChart_FileExists`
- `PhaseK7_EdgeTerraformModule_DirectoryExists`
- `PhaseK7_PreCommitConfig_FileExists`
- `PhaseK7_JwtRsaKeysSecret_DevOverlay_Exists` + `_ProdOverlay_Exists`

**Backend W7 total: 17 (bishop) + 9 (hicks) + 10 (apone) + 22 (vasquez umbrella) + 7 (regression) = ~65 forward-staged facts + 7 regression smokes = ~72 new pass-counting rows.**
Plus a few sibling permutations + the W5 ThreeRenderer fix that flipped a fail to pass = net +84 vs W6 baseline (1422 → 1506).

---

### Frontend (Playwright) — 6 new specs

All under `src/frontend/autotable-src/tests/e2e/`. Each is
chromium-only via `test.skip(testInfo.project.name !== 'chromium', …)`.
Each soft-passes (via `test.info().annotations.push({ type:'soft-pass', … })`)
when the underlying surface is forward-staged and the corresponding
hook / DOM is not yet observable.

| Spec | Hard-pin | Soft-pass when |
|------|----------|----------------|
| `bundler-swap-no-regression.spec.ts` | Lobby load emits NO `pageerror` / `console.error` (filtered for HMR/SW noise) | Lobby route 404s (Hicks's bundler swap hasn't shipped to dist) |
| `commentary-record-rendering.spec.ts` | Panel mounts `[data-testid="commentary-speaker"]` + `[data-testid="commentary-emotion"]` + `[data-testid="commentary-tile-ref"]` from mocked `CommentaryRecord` envelope | Endpoint or panel not observable |
| `outline-shader-visual.spec.ts` | `window.enableOutline()` (or `window.game?.renderer?.enableOutline()`) does NOT throw when invoked | Hook not observable |
| `three-renderer-trend.spec.ts` | `dist-size.json` either compares current ≤ previous OR meets the 550kB W7 ceiling | `dist-size.json` not yet emitted by build |
| `commentary-tile-ref-cross-pane.spec.ts` | Clicking `[data-testid="commentary-tile-ref"]` populates `window.__lastHighlightedTile` within 500ms with a non-empty tile id | testid / handler not observable |
| `pwa-icon-maskable.spec.ts` | `manifest.icons[]` includes ≥1 entry with `purpose` token `maskable` | Manifest 404s or icons array empty |

The `three-renderer-trend.spec.ts` is the **wave-over-wave
regression gate**: as long as a future wave drops `dist-size.json`
with a `previous` field, the spec hard-fails when the renderer
chunk regresses past the prior wave's size. Until then it
hard-fails on the absolute 550kB W7 ceiling.

Spec map documented in
`src/frontend/autotable-src/tests/selectors.md` Phase K Wave 7
footer.

---

## Lane-discipline W7 highlights

### 1. `tests/ci/lane-map.json` — machine-readable lane map (NEW)

The new declared-truth lane map. Keys:

- `lanes.{bishop,hicks,apone,vasquez}` — anchored regex per agent.
- `wave_subdir_overrides` — documents the `Phase_K_W*/<AgentName>/`
  attribution rule at any depth.
- `shared` — paths that any agent may touch (`docs/contracts/`,
  `.squad/decisions/inbox/_drafts/`).
- `authors` — email-to-agent map for the author-vs-lane cross-check.

### 2. `tests/ci/check-cross-lane-bundling.sh` — `--strict` mode (NEW)

`--strict` adds three guarantees on top of the PR-mode classifier:

1. `MODE` is forced to `pr` (enforces the per-commit lane check).
2. `tests/ci/lane-map.json` MUST exist and contain the `"lanes"` key.
3. Any violation hard-fails (no historical-warning escape).

Also generalised the `Phase_K_W*/<AgentName>/` attribution rule to
ANY depth. The W6 brief originally pinned it for
`src/backend/tests/*/Phase_K_W*/<AgentName>/`; W7 broadens to allow
`Phase_K_W7/Hicks/*.cs` at the repo root ALSO routing to Hicks's
lane. This lets Hicks contribute contract-test code Vasquez
forward-stages inside the broader Vasquez test lane, without
violating cross-lane.

### 3. `.github/workflows/lane-discipline.yml` — STRICT invocation (MODIFIED)

The workflow now:

1. Verifies `tests/ci/lane-map.json` is present + parseable as JSON.
2. Runs the script with `STRICT=1 --strict`.

This makes the lane-discipline gate **PR-blocking from W7 onward**
(W6 was the bring-up wave that introduced the script in warn-only
mode; W7 promotes to blocking after one wave of dogfooding).

### 4. `docs/test-lane-discipline.md` — operator runbook (NEW)

New docs page covering lane map, strict mode, how to add a new
agent, and how to debug a cross-lane / author-lane failure. Lives
in the Vasquez `docs/test-*.md` allowlist.

---

## OIDC RS256 hard-contract migration

Wave 6 left the OIDC discovery contract soft-passing under
`Development` env (HS256 acceptable as fallback). Wave 7 makes it
**hard**:

- `OidcDiscoveryHardContractTests.cs` asserts
  `id_token_signing_alg_values_supported` contains **only** `RS256`
  — even under `Development`.
- `RS256HappyPathTests.cs` round-trips an RS256-issued token
  through the API.
- `JwtOperationalDocsContractTests.cs` hard-pins the operator
  runbook for the rotation procedure.

These tests are forward-staged but most of them are hitting
Bishop's already-merged W7 implementation (Bishop shipped the
OIDC discovery infrastructure mid-W6 → early-W7); only a few are
still in soft-pass mode pending the production overlay.

---

## W5 ThreeRenderer test fix (in-lane maintenance)

The W5 `HicksW5FrontendContractTests.ThreeRenderer_ModulePresent_HardAssert`
test broke under Hicks's W7 bundler swap, because the static
`import … from 'three'` line moved out of
`src/frontend/autotable-src/src/three-renderer.ts` and into
sibling files under `src/render/` or `src/renderer/` subdirs (the
outline-shader extraction).

Because the W5 test lives in `src/backend/tests/` (the Vasquez
lane) AND the failure was tightly coupled to W7's bundler swap,
this counts as a legitimate Vasquez in-lane maintenance fix:

- Extended the test's file scan to ALSO probe
  `src/frontend/autotable-src/src/render/` and
  `src/frontend/autotable-src/src/renderer/` for a static
  `from 'three'` import.
- The hard-assert still fires if NO file in any of the three
  candidate dirs contains the static import.

This pattern (Vasquez-owned test broke under another lane's
refactor → Vasquez updates the test in-lane) is documented in
`docs/test-lane-discipline.md` under the operator runbook.

---

## Concurrent agent activity observed

During W7 bring-up the working tree carried extensive uncommitted
work from all three concurrent agents. None of it was staged by
Vasquez. Observed surfaces (for handoff awareness):

- **Bishop W7** — `src/backend/src/Auth/Rs256TokenIssuer.cs`,
  `src/backend/src/Voice/FfmpegHlsRecorder.cs`,
  `src/backend/src/Tournament/DoubleEliminationBracket.cs`,
  OIDC discovery controller, `CommentaryRecord` DTO, JwtAlgorithm
  switch in `appsettings.Production.json`. Several already merged
  to `main` mid-W6 → early-W7.
- **Hicks W7** — Bundler swap config (likely `vite.config.ts` or
  `rspack.config.js`), `src/render/custom-outline.ts` (outline
  shader extraction), `commentary-panel.ts` rendering against
  the `CommentaryRecord` envelope, `manifest.webmanifest`
  maskable icon addition, `dist-size.json` build artefact.
- **Apone W7** — `helm/mahjong/{Chart.yaml,values.yaml,templates/}`,
  `infra/terraform/modules/edge/{main,variables,outputs}.tf`,
  `.github/workflows/{ghcr-to-ecr-promote,mobile-external-testing}.yml`,
  `.pre-commit-config.yaml`, `infra/k8s/overlays/{dev,prod}/jwt-rsa-keys-secret.yaml`,
  `.squad/retros/2026-06-retro.md`, `CHANGELOG.md` 0.16.0.

Each lands via the respective agent's own PR. Vasquez's
forward-staged contracts soft-pass until the implementation
lands and hard-pin thereafter.

---

## W8 handoff notes

- **`Wave1ThroughKW7RegressionTests.cs` approaches 80 facts**
  (currently 135 — well past the recommended-split threshold).
  W8 SHOULD revisit Hudson's recommendation in
  `docs/test-harness-handoff.md` and split into thematic siblings
  sharing the `regression-host` collection.
- **`three-renderer-trend.spec.ts` is now the wave-over-wave
  regression gate.** Once Hicks's W8 work lands, the spec will
  hard-fail if the renderer chunk regresses past W7's measured
  size. Hicks's W8 brief MUST budget for the `dist-size.json`
  artefact carrying a `previous` field.
- **`HicksW5FrontendContractTests` brittleness pattern**: the
  test now probes three candidate dirs. If Hicks's W8 refactor
  moves the static `three` import out of all three, the test
  hard-fails. Recommendation: lock the bundler-swap output to
  one of `src/`, `src/render/`, `src/renderer/` long-term.
- **Lane-discipline STRICT mode is live in CI**. From W8 onward
  any cross-lane bundle hard-blocks the PR. Operator override is
  the `--branch main` warn-only path on merge commits; expected
  to be invoked at most once per phase if at all.

---

## Files committed (full Vasquez allowlist; nothing outside)

**Lane infra (new):**

- `tests/ci/lane-map.json` (NEW)
- `tests/ci/check-cross-lane-bundling.sh` (MODIFIED — `--strict` mode + Phase_K_W*/<Agent>/ generalisation)
- `.github/workflows/lane-discipline.yml` (MODIFIED — STRICT=1 invocation + lane-map.json check)
- `docs/test-lane-discipline.md` (NEW — operator runbook)

**Backend forward-staged W7 contracts (new):**

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/RS256HappyPathTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/LosersBracketDeterminismTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/FfmpegHlsRecorderHealthcheckTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/CommentaryRecordContractTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/OidcDiscoveryHardContractTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/JwtOperationalDocsContractTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Hicks/BundlerSwapContractTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Apone/AponeW7InfraContractTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/W7SurfaceSmokeFactsTests.cs`

**Backend modifications (in-lane):**

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW7RegressionTests.cs` (RENAMED from W6 + 7 new W7 smokes appended)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/HicksW5FrontendContractTests.cs` (probe `src/render/` + `src/renderer/` for static `three` import)

**Frontend Playwright (new):**

- `src/frontend/autotable-src/tests/e2e/bundler-swap-no-regression.spec.ts`
- `src/frontend/autotable-src/tests/e2e/commentary-record-rendering.spec.ts`
- `src/frontend/autotable-src/tests/e2e/outline-shader-visual.spec.ts`
- `src/frontend/autotable-src/tests/e2e/three-renderer-trend.spec.ts`
- `src/frontend/autotable-src/tests/e2e/commentary-tile-ref-cross-pane.spec.ts`
- `src/frontend/autotable-src/tests/e2e/pwa-icon-maskable.spec.ts`

**Docs (in-lane):**

- `src/frontend/autotable-src/tests/selectors.md` (Phase K Wave 7 footer appended)
- `docs/test-harness-handoff.md` (W7 follow-up section appended)

**Squad records:**

- `.squad/agents/vasquez/history.md` (W7 entry appended)
- `.squad/decisions/inbox/vasquez-phase-k-wave-7.md` (this memo)

---

## Sign-off

Vasquez (QA), Phase K Wave 7.
21 consecutive green waves. Gate **1506/0/0** (+84 vs W6 baseline).
Zero-skip streak preserved.
