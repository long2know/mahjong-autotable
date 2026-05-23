# Phase J Wave 10 — Apone (DevOps) — Final-pass memo

**Date:** 2026-05-24
**Branch:** `stlong/phase-j-wave-10-completion`
**Author:** Apone <apone@squad.mahjong>
**Scope:** Final-pass DevOps polish — flake fix, CSP Round 2, prod runbook, load test, multi-arch image, docs review.
**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build` → **820 / 0 / 0**.
**Backend gate target:** ≥760 → **exceeded** (+60).

---

## 1. `LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates` flake — fixed + 50× regression test

**Root cause:** `AutotableConnectionManager.GetStoredEntryCount(gameId)` returns `AutotableGameState.Snapshot().Count` — the **aggregate** count across all collection kinds (`match`, `seats`, `things`, `discards`). On JOIN, the translator emits a `match` entry plus per-seat `seat:N` entries before Alice's `UPDATE things` ever lands. The test's `count >= 3` predicate therefore tripped on translator chatter, NOT on Alice's actual UPDATEs. Compounded by `WaitForAsync` silently returning on deadline expiry, so timeouts surfaced as misleading downstream asserts.

**Fix:**

| Change | File | Purpose |
| --- | --- | --- |
| New `CountFor(string kind)` method | `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableGameState.cs` | Returns the per-kind count from the indexed store (O(1) lookup against the existing per-kind dictionary). |
| New `GetStoredEntryCount(string gameId, string kind)` overload | `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` | Delegates to `CountFor`; aggregate overload retained. |
| `WaitForAsync` now throws on timeout | `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsRelayTests.cs` (helper at line 303) | Throws `Xunit.Sdk.XunitException` with a descriptive `reason:` argument. Silent timeout-as-false is the anti-pattern that masked the flake for two waves. |
| Original flake test rewired | same file (line ~128) | Predicate now polls `GetStoredEntryCount(gameId, "things") >= 3` with a 5s timeout — deterministic against translator chatter. |
| New 50× stability gate | same file (line ~157) | `LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates_Stability50x` runs the inner scenario 50× in a single test method. Passes 50/50 on every local run. |

**Pattern locked:** `AutotableGameState.CountFor(string kind)` is now the canonical "how many entries of kind X" probe. Aggregate `Snapshot().Count` remains but MUST NOT be used as a predicate threshold from tests.

---

## 2. CSP Round 2 — `style-src 'unsafe-inline'` canary knob

**Added** `SecurityHeadersMiddleware.CspStrictStylesConfigKey = "Security:CspStrictStyles"` (default OFF). When set, a `DropStyleUnsafeInline(string csp)` helper strips `'unsafe-inline'` from the `style-src` directive ONLY — adjacent directives are byte-for-byte preserved.

| Surface | Behaviour |
| --- | --- |
| `Security:CspStrictStyles` unset / false | `style-src 'self' 'unsafe-inline'` — current bundle ships with HTML `style="..."` attributes; flipping would brick. |
| `Security:CspStrictStyles=true` | `style-src 'self'` — operator-controlled tightening once Hicks's inline-style-free bundle is verified in canary. |
| `Security:ContentSecurityPolicy` override | Override path unchanged — knob does NOT apply to operator-supplied policies (operator can drop it themselves). |

**Constants intentionally remain permissive.** Pinned by **Vasquez's** `CspStyleSrcNoUnsafeInlineTests` contract suite (lives in `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/`). Three Wave-10 tests added to `CspHeaderTests.cs`:

- `DropStyleUnsafeInline_RemovesTokenFromDefaultCsp` — unit test of the helper.
- `DropStyleUnsafeInline_NoOpWhenAbsent` — idempotency.
- `LiveCsp_CspStrictStylesFlag_DropsUnsafeInlineFromStyleSrc` — integration test via `WebApplicationFactory` with the flag set.

**Pattern locked:** Same shape as the four existing knobs (`CspStrict`, `CspReportOnly`, `CspReportUri`, `UseScriptNonces`) — default OFF, flipped per-deploy via `Security:*`. Constants stay PERMISSIVE; the knob is the strip path.

**Conflict to resolve:** A working-tree edit to `src/backend/tests/Mahjong.Autotable.Api.Tests/Observability/SecurityHeadersMiddlewareTests.cs` adds Wave-10 tests that assert the CONSTANTS themselves drop `'unsafe-inline'`. This directly conflicts with Vasquez's contract that the constants STAY permissive until canary. The edit is NOT mine — flagging for cleanup by whoever authored it. The canary-knob design wins per Vasquez's pinning suite.

---

## 3. Production deployment runbook

**Created** `docs/production-deployment-runbook.md` (~26 KB). End-to-end production runbook covering:

- Pre-flight checklist (image, DB, secrets, monitoring readiness)
- Image build + publish (single-arch and multi-arch — cross-references `docker.md`)
- First-deploy DB init (EF migrations via the k8s pre-rollout Job)
- Rolling update procedure (k8s `kubectl rollout` + readiness gates)
- Rollback procedure (image pin + DB compatibility window)
- Monitoring/alerting (Prometheus metrics, Sentry events, JSON-log queries)
- Incident response playbooks: DB outage, rate-limit storm, OAuth provider down, magic-link queue stall, CSP regression
- Cross-references to `docker.md`, `k8s.md`, `observability.md`, `sbom.md`, `load-test-results.md`

Linked from `docs/README.md` (the new docs index — see §6).

---

## 4. End-to-end load test

**Created** `tests/load/lobby-flood.js` (Node + `ws@^8`, NO k6 dependency — keeps CI runner footprint minimal). Three workloads:

| Workload | Concurrency | Endpoint | Result | p99 |
| --- | ---: | --- | --- | ---: |
| Lobby polling | 100 | `GET /api/lobby` (HTTP) | 12,466 req / 0 errors | **525 ms** |
| WS join | 25 | `WS /autotable/ws` (canonical path per `AutotableWsEndpoint.Path`) | 771 connects / 0 errors | **555 ms** |
| Bot tournament | 5 simultaneous (4 bots each) | `WS /autotable/ws` | 35 games / 0 errors | **2,520 ms** |

**0% error rate across all three workloads** on Debug build against `WebApplicationFactory` at `http://localhost:5114` (the test port set by `launchSettings.json`). Well inside SLO targets. Documented in `docs/load-test-results.md`.

**Pattern locked:** `LOAD_TEST_BASE_URL` env var (defaults `http://localhost:5114`). Harness is dependency-light — `tests/load/package.json` pins only `ws@^8.18.0`. Future CI cron workflow can wire it against a Release build with p99 budgets.

---

## 5. Multi-arch Docker image (`linux/amd64` + `linux/arm64`)

**Closed the Wave 4 carryover.** `.github/workflows/docker-build.yml` now:

- Adds `docker/setup-qemu-action@v3` step.
- Adds `PLATFORMS: linux/amd64,linux/arm64` env.
- Passes `platforms: ${{ env.PLATFORMS }}` to `docker/build-push-action@v6`.
- Includes the manifest digest in the workflow run summary.

**Local verification:** `tonistiigi/binfmt --install arm64` + a `docker-container` buildx driver builder (`w10-multiarch`). OCI tarball exported to `.work/oci-out/mahjong-autotable-wave10.tar`.

| Layer | Digest |
| --- | --- |
| Manifest list | `sha256:dd3618cf1a9eed8e38ad90b464336b8bf427c856185fb555946bc28e19278e8d` |
| amd64 image | `sha256:117ab896ec608a53d73cbc741e8c7790ce654a5df99636e1d66dfea212ee31a3` |
| arm64 image | `sha256:dd0cca6f020f0c66f0e895506a568cdb1c1bd37016a569001b7806314a16a9b9` |

**Pattern locked:** QEMU via `tonistiigi/binfmt` MUST be installed BEFORE the buildx container-driver builder is created. The default Docker driver doesn't support multi-platform output. Documented in the Wave-10 multi-arch section of `docs/docker.md`.

---

## 6. Final docs review

**Created** `docs/README.md` — the docs index. Landing page that maps each operator/dev/QA need to the right doc (e.g. "I need to deploy to prod" → `production-deployment-runbook.md`; "I need to know which arch was published" → `docker.md`; "I need to verify SLO budgets" → `load-test-results.md`).

**Updated:**

- `docs/docker.md` — Wave-10 multi-arch section with QEMU + buildx prerequisites and a worked example.
- `docs/sbom.md` — Wave-10 multi-arch note + cross-reference to the new production runbook.

**Verified** zero dead links across `docs/**` via Python scan.

---

## Open items / handoff

1. **Bishop or original author of the working-tree `SecurityHeadersMiddlewareTests.cs` Wave-10 edits.** Delete or rephrase the conflicting `DefaultCsp_DropsUnsafeInlineFromStyleSrcAfterWave10Migration` + `StrictCsp_DropsUnsafeInlineFromStyleSrcAfterWave10Migration` tests. They assert against the CONSTANTS and contradict Vasquez's canary-knob contract pinning suite.
2. **Hicks.** When the inline-style-free bundle lands in main, flip `Security:CspStrictStyles=true` in the prod overlay (canary first via `Security:CspReportOnly=true` for 24 h, then enforce).
3. **CI follow-up.** Wire `tests/load/lobby-flood.js` into a nightly cron workflow that boots a Release build and asserts p99 < SLO budgets. Out of scope for Wave 10.
4. **Cosign keyless image signing.** Still deferred. The multi-arch manifest digest (`sha256:dd3618…78e8d`) is now ready for `cosign sign --yes ghcr.io/...@sha256:dd3618…78e8d` once GHCR OIDC is whitelisted.

---

## File summary

**Modified (Wave-10 scope only):**

- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableGameState.cs` — `CountFor` method.
- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` — `GetStoredEntryCount(gameId, kind)` overload.
- `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs` — `CspStrictStylesConfigKey` + knob + helper.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsRelayTests.cs` — `WaitForAsync` hard-fail + flake-test fix + 50× stability test.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/CspHeaderTests.cs` — 3 Wave-10 tests.
- `.github/workflows/docker-build.yml` — multi-arch QEMU + buildx.
- `docs/docker.md` — Wave-10 multi-arch section.
- `docs/sbom.md` — Wave-10 multi-arch note.

**Created:**

- `docs/README.md` — docs index.
- `docs/production-deployment-runbook.md` — prod runbook.
- `docs/load-test-results.md` — Wave-10 load test results.
- `tests/load/lobby-flood.js` — Node load harness.
- `tests/load/package.json` — `ws@^8.18.0` pin.

**NOT mine — left untouched in working tree (per author hygiene):** Bishop's `Changsha/`, `Tournament/`, EF migration snapshots; Vasquez's `LateJoinSnapshotStabilityTests.cs`, `MultiArchDockerSanityTests.cs`, `ReplayV2NormaliserTests.cs`, `CspStyleSrcNoUnsafeInlineTests.cs`, `Tournaments/`, `DatabaseHealthDetailTests.cs`, `BotDecisionReasoningTests.cs`; Hicks's `src/frontend/autotable-src/**` + Playwright e2e specs; prior Apone waves' `.github/workflows/squad-*.yml`, `.copilot/skills/error-recovery/`, `.tool-actionlint/`.
