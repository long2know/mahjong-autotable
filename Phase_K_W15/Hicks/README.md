# Phase K Wave 15 — Hicks placeholder

> Forward-stage marker (Vasquez-authored) for the Hicks W15 hand-off
> directory. The actual W15 Hicks deliverables (LH13 third retry +
> visual-regression `snapshotPathTemplate` + Phase L renderer spike
> implementation kickoff + `?action=cost-forecast` route + bundle
> audit) land in Hicks's own PR.

## Vasquez-side W15 forward-stage contract tests

Hicks's W15 surfaces are mirrored by the following Vasquez-authored
contract test files under
`src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Vasquez/`:

- `HicksW15ThreeRendererHoldLineTests.cs` — three-renderer ≤ 406.64 KB
  hold-line is preserved (no regression).
- `HicksW15LH13ThirdRetryTests.cs` — LH13 hard-pin third-retry status
  (soft-pinned if cron still hasn't converged; hard-pinned if it has).
- `HicksW15PhaseLRendererBundleTests.cs` — Phase L renderer-webgl2
  hello-world bundle present + ≤ 30 KB budget (preliminary).
- `HicksW15CostForecastRouteTests.cs` — `?action=cost-forecast`
  admin-redirect contract.
- `HicksW15BundleAuditCandidatesTests.cs` — bundle inventory audit
  document lists ≥ 3 candidates for further trimming.
- `HicksW15SnapshotPathTemplateTests.cs` — Playwright config uses
  the `snapshotPathTemplate` convention (visual-regression baselines
  pin by relative path, not absolute).

## Cross-lane hygiene

Same precedent as `Phase_K_W15/Bishop/README.md`: this is the ONLY
Vasquez-authored file under `Phase_K_W15/Hicks/`. All contract probes
live under the Vasquez subdir.
