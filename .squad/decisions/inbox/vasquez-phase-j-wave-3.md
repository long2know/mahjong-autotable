# Phase J Wave 3 — Vasquez memo (health endpoint + WinResult bool surfaces + Docker smoke)

**Branch:** `stlong/phase-j-wave-3-completion`
**Baseline:** 418 / 0 / 0 (Wave J.2 main = `2edf2e2`)
**Vasquez commit:** `d7c5337` (`test(api,changsha,smoke): Phase J Wave 3 — health endpoint + WinResult bool surfaces + Docker smoke`)
**Final gate:** **424 passed / 0 failed / 0 skipped** (+6 over baseline; zero-skip streak preserved, 7 consecutive waves green: I.1 → I.2 → I.3 → I.4 → J.1 → J.2 → J.3.)

**Vasquez lane (test-only):**

- NEW `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/WinResultSurfaceTests.cs` — 4 facts
- NEW `src/backend/tests/Mahjong.Autotable.Api.Tests/Api/HealthEndpointTests.cs` — 2 facts
- NEW `tests/smoke/docker-build-smoke.sh` (bash script) — verified locally, see below
- NEW `tests/smoke/README.md`

Strict test-only lane. **Zero production-code edits** in this commit.

## Production owners (Bishop, Apone, Hicks)

- **Bishop (commits `9235859`, `75baecc`, `2e84179`):**
  1. `9235859` — `feat(api): add /health endpoint for Docker HEALTHCHECK + Linux deploy`. New `GET /health` minimal-API endpoint returning `{ status, buildSha, uptime, version }` JSON. `buildSha` reads `Environment.GetEnvironmentVariable("BUILD_SHA")` with `?? "dev"` fallback. `uptime` is captured at module load (before `WebApplication.CreateBuilder`) so it reflects the host-process start, not the time of first /health request. Distinct from `/api/health` (legacy short-form probe used by the frontend) so deployment infrastructure has its own stable wire contract.
  2. `75baecc` — `feat(changsha): surface IsSelfDraw + IsKongReplacement bools on WinResult`. Explicit top-level bool properties on `WinResult` resolving the blind spot Vasquez flagged in Wave 2. `IsSelfDraw` lifted from `Method == WinMethod.SelfDraw`; `IsKongReplacement` lifted from `AllPatterns.Contains(WinPattern.KongReplacementWin)`. Both set at the construction sites in `DeclareSelfDrawWin` (line 649-650) and `ResolveHuClaim` (lines 1067-1068) — `AllPatterns` is preserved unchanged for backward compat with Phase H/I consumers that scan the pattern list.
  3. `2e84179` — `feat(changsha): canonical WinPattern display ordering + ordering endpoint`. New static `ChangshaPatternOrdering` metadata class with `Dictionary<WinPattern,int>` table + `GetOrder()` helper. Hicks's result-modal needs a canonical display order with Big Wins surfaced first; Bishop opted for option B (static metadata) over reflection to keep the presentation concern out of the detector hot path. Adds a `GET /api/changsha/win-patterns/ordering` endpoint surfacing the ordering to the frontend.
- **Apone (commit `ea2c991`):** `chore(devops): Phase J Wave 3 — single-image Docker deployment`. Three-stage `Dockerfile` at the **repo root** (canonical location per the new `.dockerignore`). Stage 1 (`node:20-alpine`) Parcel-builds the autotable bundle from `src/frontend/autotable-src/` with `--public-url .`. Stage 2 (`dotnet/sdk:10.0`) publishes the API with `UseAppHost=false`. Stage 3 (`dotnet/aspnet:10.0`) combines them, installs `curl` + `tini`, sets up a writable `/data` volume for SQLite, exports `BUILD_SHA=""` (empty default), and wires `HEALTHCHECK` against `/health` with `/api/health` fallback. Also updated `docker-compose.yml`, `.dockerignore`, `.gitignore`, README, and added `docs/deployment.md` + `docs/docker.md`.
- **Hicks (not yet committed at memo time, working-tree state visible):** frontend touch-ups in `src/frontend/autotable-src/{index.html, src/game-ui.ts, src/move-log.ts, src/style.css}` plus a regeneration of the bundled `src/frontend/autotable/*` artefacts. Likely the result-modal display ordering consuming Bishop's new `ChangshaPatternOrdering` endpoint plus polish on the move-log. No backend changes; my tests are insensitive to Hicks's lane.

## What shipped per file

### `WinResultSurfaceTests.cs` — 4 facts (+316 LOC)

Complements Wave-2 `SelfDrawWinContextTests` by exercising the **canonical-axis** `WinResult.IsSelfDraw` / `WinResult.IsKongReplacement` bool surfaces directly (no reflection-defensive fallback). Wave 2 stays green when the bools are absent; Wave 3 enforces them.

- `SelfDrawHu_ChangshaHandResult_HasIsSelfDrawTrue` — End-to-end through `DeclareSelfDrawWin`. 4×chow + Tong-5 pair winning shape, dealer at seat 0, HeavenlyHand suppressed via injected pre-deal discard, wall non-empty so LastTileFromWall stays false. Asserts `win.IsSelfDraw == true` directly + sanity-pins `Method = SelfDraw`, `WinningSeatIndex = 0`, `SourceSeatIndex = 0`.
- `RonHu_ChangshaHandResult_HasIsSelfDrawFalse` — End-to-end through `ResolveClaim(..., Hu)` (which routes to the internal `ResolveHuClaim`). Dealer holds 14 tiles incl. the Wan-1 discard tile, seat 2 holds the 13-tile wait, EarthlyHand suppressed via benign prior discard, claim window opens, seat 2 declares Hu. Asserts `win.IsSelfDraw == false` directly + sanity-pins `Method = Discard`, `WinningSeatIndex = 2`, `SourceSeatIndex = 0`.
- `KongReplacementHu_ChangshaHandResult_BothBoolsTrue` (杠上开花) — Reuses the Wave-2 `BuildKongReplacementWinScenario` helper (4×Tiao-9 kong source + 10 fillers + Tong-5 replacement tile pre-staged at `state.WallBackIndex`). Declares concealed kong, declares self-draw Hu. Asserts BOTH `win.IsSelfDraw == true` AND `win.IsKongReplacement == true` directly; also pins `AllPatterns.Contains(WinPattern.KongReplacementWin)` as the backward-compat surface for Phase H/I consumers.
- `RegularDiscardHu_ChangshaHandResult_KongReplacementFalse` — Negative counterpart to the kong-replacement test. Same Ron-Hu fixture as fact 2 but with the IsKongReplacement axis under test. Pre-condition pin: `state.LastDrawWasKongReplacement == false` BEFORE the discard so the regression we're guarding against (stale flag bleed from a prior kong window into a regular discard-hu) is actually testable. Defence-in-depth: asserts `AllPatterns` does NOT contain `KongReplacementWin` either — the two surfaces are required to agree.

### `HealthEndpointTests.cs` — 2 facts (+158 LOC)

`WebApplicationFactory<Program>` over an in-memory test host, per-test temp SQLite DB under `bin/Debug/net10.0/test-data/mahjong-health-{guid}.db` to avoid collisions. Same ChangshaRuntimeOptions snapshot pattern as `SpectatorModeTests` and `ChangshaHubTestHarness`.

- `HealthEndpoint_ReturnsOk_WithExpectedShape` — GET `/health`, asserts 200, parses body as `JsonDocument`, pins all four fields (`status`, `buildSha`, `uptime`, `version`) as present. Also pins `status` is a non-empty `JsonValueKind.String` (the smoke script doesn't constrain the literal vocabulary; this is the in-process counterpart that holds the line on shape without over-constraining).
- `HealthEndpoint_BuildSha_DefaultsToDev_WhenUnset` — Snapshots `BUILD_SHA`, sets to `null`, GETs `/health`, asserts `buildSha == "dev"`, restores the env var in a `finally`. Pins the documented null-fallback contract `Environment.GetEnvironmentVariable("BUILD_SHA") ?? "dev"`.

### `tests/smoke/docker-build-smoke.sh` (+~110 LOC)

Bash script outside the unit-test gate. `set -euo pipefail`, per-PID image tag + container name, per-PID log directory under `tests/smoke/.run-$$/`. Auto-detects the Dockerfile location: prefers repo-root `./Dockerfile` (Apone's canonical location per the new `.dockerignore`), falls back to `infra/docker/Dockerfile` with `--target runtime-autotable` only when the root file is absent. Builds → starts container on port `18080` → polls `/health` up to 30s → grep-asserts the four expected fields. `trap cleanup EXIT` removes container + image + per-run logs on success or failure.

### `tests/smoke/README.md`

Purpose, prerequisites (Docker daemon, host port `18080`, `curl`), how to run, expected output, CI integration (not yet wired — manual/nightly), and a troubleshooting matrix covering "no Dockerfile found", `/health` timeout, port conflict, and shape-assertion drift.

## Methodology — what worked

- **Scaffold against the published contract, hand off uncommitted.** Bishop published his Wave-3 surfaces (`/health` endpoint, `WinResult.IsSelfDraw`, `WinResult.IsKongReplacement`, `ChangshaPatternOrdering`) in his working tree BEFORE committing. That let me write all six unit-test facts compile-clean against his uncommitted state and run the Phase-J-3 filter at 6/6 green BEFORE his commits landed. By the time I committed, Bishop had pushed three commits and Apone had pushed one — clean linear history with strict-disjoint lanes.
- **Direct-axis canonical pinning, not reflection.** Wave 2's `SelfDrawWinContextTests` used reflection-defensive helpers (`AssertIsSelfDrawAxis` probes `WinResult.IsSelfDraw` first, falls back to `Method == SelfDraw`) to stay green whether or not the bools shipped. Wave 3 is the opposite: it uses direct property access (`win.IsSelfDraw`, `win.IsKongReplacement`) so a regression that flips the bool independently of the `Method` enum (e.g. a bad merge that defaults `IsSelfDraw` to false while `Method` is still `SelfDraw`, or a wire-serialization mismatch) **fails** the test instead of silently being papered over by the Method-axis fallback. The two suites are intentionally complementary: Wave 2 = "the canonical contract holds via either surface", Wave 3 = "the new surface is the canonical contract".
- **Live-container smoke as defence-in-depth for `WebApplicationFactory`.** `HealthEndpointTests` exercise `/health` in-process via the test host (fast, runs in the unit-test gate, no daemon needed). `docker-build-smoke.sh` exercises the same endpoint on a real container behind a real port via real `curl`. The two surfaces are required to agree — if the unit test passes but the smoke fails on shape, the regression lives in serialization or middleware ordering, not in the endpoint handler.
- **Auto-detect Dockerfile layout.** Apone's `.dockerignore` declared the canonical Dockerfile lives at the repo root, and excludes the pre-built `src/frontend/autotable/` bundle so Stage 1 rebuilds it from source. My first smoke-script revision blindly preferred `infra/docker/Dockerfile` and the build broke at `COPY src/frontend/autotable ./wwwroot/autotable` (the pre-built bundle was now excluded). Flipped the priority: prefer `./Dockerfile`, fall back to `infra/docker/Dockerfile`. Survives both layouts.

## Surprises / blind spots

- **`BUILD_SHA=""` in Apone's Dockerfile is empty-string, not unset.** Apone's `ENV BUILD_SHA=""` (Dockerfile line 83) sets the variable to a literal empty string. Bishop's endpoint reads `Environment.GetEnvironmentVariable("BUILD_SHA") ?? "dev"` — the `??` operator only handles `null`, so when `BUILD_SHA=""` the response carries `buildSha = ""` rather than `"dev"`. The live smoke output confirmed: `{"status":"healthy","buildSha":"","uptime":"00:00:02.0374655","version":"1.0.0.0"}`. The in-process `HealthEndpoint_BuildSha_DefaultsToDev_WhenUnset` test uses `Environment.SetEnvironmentVariable("BUILD_SHA", null)` which **does** unset the variable in-process, so the unit test correctly pins the `?? "dev"` contract — that contract is just bypassed in production by Apone's choice. Recommended fix for a follow-on wave (Bishop OR Apone): either widen the fallback to `string.IsNullOrEmpty(...) ? "dev" : value`, or change `Dockerfile`'s default to `BUILD_SHA=dev`. The smoke script only checks for field presence so it stays green either way.
- **`ChangshaPatternOrdering` endpoint (Bishop `2e84179`) is not unit-test covered.** The new `GET /api/changsha/win-patterns/ordering` endpoint Bishop added surfaces the static metadata table to the frontend. Hicks consumes it from his (uncommitted at memo time) result-modal display work. I did not write tests for it — it was not in the brief's three tasks. A J-4 wave should cover (a) the endpoint returns 200 with the expected pattern list, (b) the order matches Bishop's documented sequence, (c) every `WinPattern` enum value has an ordering entry (no silent omissions when new patterns ship).
- **Apone's healthcheck timing is generous for cold starts.** `HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3` gives a 20-second start-period grace before Docker counts retries. The smoke script polls for 30s, which is comfortably below the start-period; on cold-cache builds (first-time pull of the .NET base images) the actual startup is ~3s post-publish. Both budgets are conservative and correct.
- **`Dockerfile` Stage 1 (Parcel) is the dominant build cost.** Cached `docker build` completes in ~17s; uncached can be 2–5 minutes (Parcel CSS compilation + minification dominates). Documented in the smoke README's runtime expectations.

## Docker smoke verification

**Yes — verified locally in the agent environment.** Docker `29.5.2` is installed and runnable. Full run (cached layers):

```
==> [1/4] Building mahjong-autotable:smoke-834646 (docker build -t mahjong-autotable:smoke-834646 .)...
✅ build succeeded
==> [2/4] Starting container...
==> [3/4] Waiting for /health...
✅ /health responding after 3s
{"status":"healthy","buildSha":"","uptime":"00:00:02.0374655","version":"1.0.0.0"}
==> [4/4] Assert response shape...
  ✅ status field present
  ✅ buildSha field present
  ✅ uptime field present
  ✅ version field present

🎯 Docker smoke test PASSED

real	0m17.168s
user	0m0.287s
sys	0m0.268s
```

Notes: the empty `buildSha` value is the `BUILD_SHA=""` blind spot called out above — the script only asserts presence, not value, so it stays green. The first run (cold cache, before the smoke-script was committed) failed with the legacy `infra/docker/Dockerfile` layout because Apone's `.dockerignore` excludes the pre-built bundle path that file's `COPY` referenced — surfaced and fixed by flipping the auto-detect priority to prefer repo-root.

## Stability

- **Phase J Wave 3 filter (`--filter "Wave=Phase-J-3"`):** 6 passed / 0 failed / 0 skipped — clean.
- **Full suite:** 424 passed / 0 failed / 0 skipped (15s on `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`). Zero-skip streak preserved (now 7 consecutive waves green: I.1 → I.2 → I.3 → I.4 → J.1 → J.2 → J.3).
- **Docker smoke:** PASSED on the live local container (Docker 29.5.2). Per-PID isolation + trap-driven cleanup confirmed working — no leaked images / containers / log dirs after the run.
- No production code changed (`src/backend/src/**` untouched on this commit).

## Cross-agent coordination

Linear timeline:
1. Bishop pushed `9235859` (`/health` endpoint) → `75baecc` (WinResult bools) → `2e84179` (PatternOrdering).
2. Apone pushed `ea2c991` (single-image Docker deployment).
3. Vasquez pushed `d7c5337` (test commit) — lands on top of all four with strict-disjoint scope.

Hicks's frontend touch-ups (`src/frontend/autotable-src/**` modifications + bundle regen) remained as uncommitted working-tree state at memo time, awaiting his own commit. My single test commit is the next-to-last in the wave (history journal commits to follow).

Memo: `.squad/decisions/inbox/vasquez-phase-j-wave-3.md`.
