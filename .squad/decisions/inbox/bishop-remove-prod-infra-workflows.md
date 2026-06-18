# Bishop decision memo — removed 5 prod-infra CI workflows + their contract tests

**Date:** 2026-06-18
**Author:** Bishop (Backend)
**Status:** Shipped
**Scope:** `.github/workflows/`, `src/backend/tests/Mahjong.Autotable.Api.Tests/`, `docs/`
**Companion PR:** #107 squash-merged at `8f6c974` on main
**Verification:** db-providers run `27762022896` on `8f6c974` — GREEN, both Sqlite + Postgres cells

## Why

Stephen runs a single hobby container. He explicitly approved deleting five CI
workflows that target production infrastructure he will never have. Each one
also had backend "contract tests" that `File.Exists`-assert the workflow (and,
in one case, a runbook doc) is present — so deleting the workflows alone would
have turned the `db-providers` gate red on the missing files. Removed both.

## Workflows deleted (5)

- `prod-health-check.yml` — probes `api.mahjong-autotable.com` (no prod host).
- `hsts-readiness-check.yml` — checks `mahjong.example.com` HSTS preload.
- `load-test-nightly.yml` — k6/artillery nightly load stack.
- `redis-load-test-reminder.yml` — Redis/k8s load-test reminder.
- `us-east-1-auto-rollback.yml` — AWS us-east-1 auto-rollback.

`docker-smoke.yml` was **kept** (Stephen is keeping that one).

## Orphaned doc deleted (1)

- `docs/us-east-1-auto-rollback-runbook.md` — documented the now-deleted
  `us-east-1-auto-rollback.yml`; the W22 contract test asserted it existed.

## Test files deleted whole (wholly about a deleted workflow)

- `Deploy/LoadTestCronYamlTests.cs` — 6 tests, all contract-testing
  `load-test-nightly.yml`.
- `Phase_K_W22/Vasquez/AponeW22AutoRollbackContractTests.cs` — 2 tests, both
  about `us-east-1-auto-rollback.yml` + its runbook.

## Test files surgically edited (kept all sibling/unrelated tests)

- `Phase_K_W10/Vasquez/AponeW10InfraContractTests.cs` — removed only the
  `ProdHealthCheckWorkflow_Present_OrForwardStaged` method (section 6) + its
  stale `<summary>` item. Kept Redis-TF, Argo, ESO, container-scan, CHANGELOG,
  and the W9 regression pins.
- `Phase_K_W11/Vasquez/AponeW11InfraContractTests.cs` — removed the two
  prod-health methods (`ProdHealthCheckWorkflow_Present_W10Pin`,
  `ProdHealthCheck_DeclaresRegionMatrix_OrForwardStaged`); **kept**
  `EdgeRegionProbesDoc_Present_OrForwardStaged` (it tests a doc that still
  exists). Retitled the section comment + `<summary>` item to "Edge region
  probes" so they no longer reference the deleted workflow.
- `Phase_K_W10/W10SurfaceSmokeFactsTests.cs` — removed only
  `Smoke_W10_ProdHealthCheckWorkflow_OrForwardStaged`. Kept the rest of the
  broad smoke-facts file.
- `Regression/Wave1ThroughKW22RegressionTests.cs` — removed only the
  `PhaseK10_ProdHealthCheck_FileOrForwardStaged` block in this 3,288-line
  regression file. All other regression checks intact.

## Anti-pattern note (please avoid going forward)

**Contract tests that `File.Exists`-assert a workflow or doc file make that
file undeletable without a coupled test edit.** Five workflows + one runbook
could not be removed without simultaneously hunting down and editing six test
files across four phase-wave directories. If you must pin "this workflow
exists," prefer a single, clearly-named meta-test that enumerates the workflow
directory (so removals are one-line edits in one place), or make the pin
soft/forward-staged and out of the blocking `db-providers` gate. Don't scatter
hard `File.Exists` filesystem assertions for infra artifacts across per-wave
contract suites.

## Verification footnote (worktree gotcha)

Local `dotnet test` in this worktree shows ~81 pre-existing failures from
contract tests that locate the repo root via a `.git` **directory** — in a git
worktree `.git` is a **file**, so those locators return null. Provider-
independent, identical on base, unrelated to this change. CI runs on a normal
clone, so the authoritative signal is the green `db-providers` run above.
