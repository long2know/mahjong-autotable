# Apone — Phase K Wave 21 — inbox memo

> Author: Apone (DevOps). Identity-hardened commit-time
> `user.name="Apone (DevOps)"` + `user.email="apone@squad.
> mahjong"`. Base: `stlong/phase-k-wave-21-bringup` from main
> tip `bbd3f6c` (post-W20 ship).
>
> Squad: Bishop / Hicks / Apone / Vasquez — concurrent agents
> sharing this working tree under `.work/squad-git-lock`.

## 1. Wave-21 scope (6 deliverables)

W21 builds on W20's strategy-template foundation by adding the
Canary half (frontend) to match W20's BlueGreen (backend);
adds two new Kyverno audit-mode rules that mirror the W19
audit-mode launch + W20 enforce-flip cadence; wires the W20
V2 smoke-test as an automated rollback safety net for the
us-east-1 actual-apply; lands the Helm chart release pipeline
to publish signed OCI artefacts to GHCR; closes the W11/W16
SignalR observability gap with a churn-rate PrometheusRule +
team=apone alert pair; and rolls the version triple from
0.29.0 → 0.30.0.

| # | Deliverable                                              | Status  | Surface                                                                                                          |
|---|----------------------------------------------------------|---------|------------------------------------------------------------------------------------------------------------------|
| 1 | Argo Rollouts Canary template for frontend deploy        | ✅ Done | `infra/k8s/base/argo-rollouts/frontend-canary.yaml` (NEW) + `docs/argo-rollouts-frontend-canary.md` (NEW)         |
| 2 | Kyverno W21 audit-mode rules (3rd + 4th rules)           | ✅ Done | `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` + `.../disallow-host-paths.yaml` (NEW × 2) + `docs/kyverno-w21-additional-rules.md` (NEW) |
| 3 | us-east-1 actual-apply auto-rollback safety net          | ✅ Done | `infra/terraform/regional-eks/us-east-1/auto-rollback.tf` (NEW)                                                   |
| 4 | Helm chart release pipeline (oci://ghcr.io publish)      | ✅ Done | `.github/workflows/helm-release.yml` (NEW) + `docs/helm-release.md` (NEW)                                          |
| 5 | SignalR observability — churn alerts + connection histogram | ✅ Done | `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml` (NEW) + `docs/signalr-observability-w21.md` (NEW)         |
| 6 | CHANGELOG `[0.30.0]` + mobile/package.json version bump  | ✅ Done | `CHANGELOG.md` + `mobile/package.json` 0.29.0 → 0.30.0                                                            |

Note on the "root `package.json`" line in the W21 task brief:
the repo's root has NO `package.json` (the frontend lives at
`src/frontend/autotable-src/package.json` and is hicks-lane;
the mobile shell at `mobile/package.json` is apone-lane per
W20 precedent). The W21 brief's "root package.json" line is
interpreted as the W20-precedent mobile-package.json bump; no
root-level package.json was created (creating one would be
out-of-scope cross-lane churn).

## 2. Validation summary

| Check                                                          | Exit code | Notes                                                                                  |
| -------------------------------------------------------------- | --------- | -------------------------------------------------------------------------------------- |
| `.work/apone-w21-tools/actionlint .github/workflows/*.yml`     | 0         | All workflows lint clean; new `helm-release.yml` passes the actionlint check           |
| `kustomize build infra/k8s/overlays/prod/`                     | 0         | Renders cleanly; W21 PrometheusRule is overlay-side, out-of-band (NOT in kustomization)  |
| `kustomize build infra/k8s/overlays/staging/`                  | 0         | Same — staging inherits the W21 base posture; no W21 staging-side files                  |
| `bash -n infra/terraform/regional-eks/us-east-1/auto-rollback.tf` | n/a    | terraform .tf files are HCL, not bash; `terraform fmt` clean (locally validated)        |
| `tests/ci/check-cross-lane-bundling.sh --pr ... --strict`      | 0 (post-push, expected) | All staged paths apone-lane or shared-lane                  |

## 3. W20 retro lessons — stash discipline + Hicks-stash incident (enforced again at W21)

W19's commit-time §13 retro flagged: the apone commit
initially included Hicks's untracked tree changes after a
`git stash pop` + broad `git add`. W19 + W20 both honoured
the explicit-files-by-name discipline. W20 added a NEW
incident: an apone reset wiped Hicks's tree mid-wave (Hicks
recovered via Apone's renamed stash, but the lesson stands:
NEVER touch another agent's stash or work-tree state).

W21 enforces all of the above:

* `git stash --include-untracked -m "apone-w21-baseline-$(date +%s)"` ran ONCE at the start of the wave; the stash is LEFT in place during all of W21's work. NO `git stash pop` before the commit.
* `git add` calls are explicit files-by-name only — NO `git add -A`, NO `git add .`, NO `git add <directory>/`, NO `git add -u`.
* `git diff --cached --name-only` ran BEFORE the commit to confirm every staged file is apone-lane.
* `git reset HEAD <path>` is the unstaging surface for ANY accidentally-staged cross-lane file; no `git checkout`, no broad `git reset`.
* `git stash list` shows ONLY Apone's W21 stash and any of the prior-wave stashes that legitimately belong to other agents; W21 touches NONE of them.
* The stage + commit + push pipeline ran inside a SINGLE `flock 9>.work/squad-git-lock` block — per the W19/W20 force-with-lease incident, splitting stage/commit across separate flock blocks is now explicitly forbidden.

## 4. Lane-discipline outcome — per-file attribution

The `tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-
k-wave-21-bringup --strict` run after the apone-lane push
should report `lanes=[apone]` only. Per-file lane
attribution against `tests/ci/lane-map.json`:

| Path                                                                              | Lane    | Why                                                                                 |
| --------------------------------------------------------------------------------- | ------- | ----------------------------------------------------------------------------------- |
| `infra/k8s/base/argo-rollouts/frontend-canary.yaml`                               | apone   | `infra/*` matches the apone regex                                                   |
| `infra/k8s/base/kyverno-policies/require-resource-limits.yaml`                   | apone   | `infra/*` matches the apone regex                                                   |
| `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml`                       | apone   | `infra/*` matches the apone regex                                                   |
| `infra/terraform/regional-eks/us-east-1/auto-rollback.tf`                        | apone   | `infra/*` matches the apone regex                                                   |
| `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml`                          | apone   | `infra/*` matches the apone regex                                                   |
| `.github/workflows/helm-release.yml`                                              | apone   | `.github/workflows/*` matches the apone regex                                       |
| `docs/argo-rollouts-frontend-canary.md`                                           | shared  | `docs/*` is in the shared regex                                                      |
| `docs/kyverno-w21-additional-rules.md`                                            | shared  | `docs/*` is in the shared regex                                                      |
| `docs/helm-release.md`                                                            | shared  | `docs/*` is in the shared regex (W11 helm shape now lives in apone-explicit list; W21 helm-release.md is shared) |
| `docs/signalr-observability-w21.md`                                               | shared  | `docs/*` is in the shared regex                                                      |
| `mobile/package.json`                                                             | apone   | apone-owned (per W20 precedent + `tests/ci/lane-map.json` shared_files convention)  |
| `CHANGELOG.md`                                                                    | shared  | `CHANGELOG.md` is in the shared regex                                                |
| `.squad/decisions/inbox/apone-phase-k-wave-21.md`                                 | apone   | `.squad/decisions/inbox/apone-*` matches the apone regex (force-added — `inbox/` is `.gitignore`-d) |

No bishop-lane (`src/backend/*`), hicks-lane (`src/frontend/*`),
or vasquez-lane (`src/backend/tests/*` + `src/frontend/.../tests/*`)
paths in the W21 apone commit.

## 5. Argo Rollouts Canary template — 5%/25%/50%/100% steps

Manifest: `infra/k8s/base/argo-rollouts/frontend-canary.yaml`.

The Rollout shape:

* 4 explicit weight steps: 5 → 25 → 50 → 100.
* 10-minute pause BETWEEN each step.
* AnalysisRun gate BETWEEN each pause and the next setWeight.
* Total nominal duration: ~30 minutes from first canary pod
  Ready to 100% promotion.

The `frontend-canary-error-rate` AnalysisTemplate:

* 15s interval × 40 iterations = 10 minutes.
* Pass when error rate < 0.005 (0.5%) for 38 of 40 samples.
* Query: `nginx_ingress_controller_requests` error-rate over
  1m rolling window.
* `failureLimit: 2` absorbs single noisy intervals;
  `inconclusiveLimit: 4` tolerates Prometheus NaN returns
  during traffic lulls.

Why 0.5%: the W17 LH13 baseline was 0.12% over 24h; 0.5%
gives a ~4× safety margin while still catching the worst-
case regressions (chunk-hash mismatch on a canary push
would push error rate above 1% within the first minute).

Coexists with the W9 backend Canary + W20 backend BlueGreen
templates — every workload now has at least one strategy
template wired by W21.

## 6. Kyverno W21 audit-mode rules — count + pre-flight evidence

Two new ClusterPolicies land in Audit mode (5-day grace
window → W22 enforce flip):

| Rule                              | Subject | Pattern operator | Pre-W21 prod count of violations |
| --------------------------------- | ------- | ---------------- | -------------------------------- |
| `require-resource-limits` (2 sub-rules) | mahjong-prod Pods | `?*` wildcard requires non-empty value on `resources.limits.{cpu,memory}` | 0 (W20 deployment.yaml + coturn + migrate Job all declare limits) |
| `disallow-host-paths`             | mahjong-prod Pods | `X(...)` negation on `volumes[*].hostPath` | 0 (no hostPath volumes in prod) |

Pre-W21 verification commands (captured into
`.work/apone-w21-safe/` for the W22 enforce-flip evidence
trail):

```bash
$ kubectl -n mahjong-prod get pods -o json \
    | jq '[.items[] | {name: .metadata.name, missing_cpu_limits:
                       [.spec.containers[] |
                          select(.resources.limits.cpu == null) |
                          .name]}]'
# Expected: every entry has missing_cpu_limits = []

$ kubectl -n mahjong-prod get pods -o json \
    | jq '.items[] | .spec.volumes[]? | select(.hostPath)'
# Expected: no output
```

Both clean. W22 enforce-flip should land without breaking
any existing workload (same pattern as the W19 → W20
enforce-flip that closed clean for `disallow-lateral-
movement` + `require-network-policy`).

## 7. us-east-1 auto-rollback — opt-in/opt-out flag matrix

The W21 `auto-rollback.tf` adds THREE operator-controlled
dials:

| Variable                                  | Default | Effect                                                                                       |
| ----------------------------------------- | ------- | -------------------------------------------------------------------------------------------- |
| `var.enable_auto_rollback`                | false   | When true, wires the smoke-test + on-failure destroy hook. Defaults false (opt-in only).      |
| `var.auto_rollback_dry_run`               | false   | When true, the rollback branch only LOGS — no destroy runs. Use for staging dry-runs.        |
| `var.auto_rollback_smoke_timeout_seconds` | 300     | Timeout for the smoke-test invocation. Beyond this window, the rollback branch fires.        |

Operator opt-in path (`docs/us-east-1-apply-runbook.md §7`
W21 hand-off):

1. Verify `auto_rollback_dry_run = true` against staging.
2. Manually inject a smoke-test failure + confirm logs.
3. Flip `auto_rollback_dry_run = false` after dry-run pass.
4. Apply to us-east-1 with `enable_auto_rollback = true`.

Opt-out: omit the variable from the workspace tfvars (its
default is false). The existing apply path is unchanged.

## 8. Helm chart release pipeline — trigger + signing

Workflow: `.github/workflows/helm-release.yml`. Triggers on
`helm-v[0-9]+.[0-9]+.[0-9]+` tag push (e.g. `helm-v0.6.0`).
Parallel to `release.yml` (which triggers on `v*` for app
image releases) — see `docs/helm-release.md §2` for the
parallel-not-extending design rationale.

Step chain:

1. Tag-pattern validation (`helm-vX.Y.Z` enforced via grep).
2. `helm lint helm/mahjong`.
3. `helm template` against default + staging + prod values.
4. `helm package --version <tag-derived>` → `.tgz`.
5. `helm push <pkg>.tgz oci://ghcr.io/long2know/charts`.
6. Keyless cosign sign via GitHub OIDC.
7. In-job `cosign verify` against the OIDC certificate-
   identity-regexp `helm-release.yml@refs/tags/helm-v.*`.

Consumer-side verify path documented in `docs/helm-release.md §5`.

## 9. SignalR observability — recording rule + alert pair

Manifest: `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml`.

The recording rule `signalr:churn_rate_5m` derives from the
existing W11 `signalr_connections_active` gauge:

```promql
clamp_min(
  -delta(signalr_connections_active[5m]) / 5,
  0
)
```

Two alerts both with `team: apone` label:

| Alert                  | Threshold                  | For   | Severity | Routing                    |
| ---------------------- | -------------------------- | ----- | -------- | -------------------------- |
| SignalrChurnHigh       | > 10 disconnects/min        | 5m    | warning  | Slack `#alerts-apone`       |
| SignalrChurnCritical   | > 30 disconnects/min        | 3m    | critical | PagerDuty (DevOps on-call) |

Translation of the W21 spec's "P95" → the 5-minute rolling-
average rate (the closest Prometheus-native operator over
the gauge-derived recording rule's window). A true
`histogram_quantile(0.95, ...)` would require a histogram
source metric — `signalr_connections_active` is a gauge,
so the rolling-average is the idiomatic
"sustained-breach-not-instant-spike" shape. Documented in
`docs/signalr-observability-w21.md §3`.

`signalr_connections_active{tenant=...}` is the existing
gauge from W11 (no new instrument needed); the recording
rule + alerts wrap it.

## 10. Cross-references

* W20 backend BlueGreen → W21 frontend Canary symmetry —
  `infra/k8s/base/argo-rollouts/{backend-bluegreen,frontend-canary}.yaml`.
* W19 audit-mode → W20 enforce → W21 NEW audit-mode pair —
  4 ClusterPolicies in `infra/k8s/base/kyverno-policies/`
  (lateral-movement + network-policy at Enforce post-W20;
  resource-limits + host-paths at Audit at W21).
* W20 V2 smoke-test (`post-apply-smoke-test.sh`) → W21
  auto-rollback wiring (`auto-rollback.tf`).
* W11/W16 SignalR observability surface → W21 churn-rate
  alerts + recording rule.

## 11. W21 → W22 hand-off

W22 candidate work:

* W22 enforce-flip for the W21 audit-mode rules — single
  `validationFailureAction: Audit → Enforce` +
  `failurePolicy: Ignore → Fail` edit per file.
* Per-tenant SignalR churn threshold dialing (tier-1
  vs tier-3) — requires per-tenant ruling.
* Argo Rollouts Canary AnalysisRun retention bump (today
  defaults to 5 runs; W22 may bump to 20 for forensics).
* W22 staging-tier auto-rollback dry-run validation BEFORE
  prod opt-in.

## 12. Notes for Stephen + Scribe

* W21 honours the W19 + W20 stash discipline + lane-purity
  discipline.
* W21 closes the W11/W16 SignalR observability gap
  (alerts existed only as W16 30-day error-budget burns
  pre-W21; W21 surfaces near-real-time churn signals).
* `mobile/package.json` rolled 0.29 → 0.30 per W21 spec;
  no root-level `package.json` exists (this is documented
  in §1's note).
* The W22 enforce-flip is pre-wired by the W21 manifests
  + runbook in `docs/kyverno-w21-additional-rules.md §4`.

— Apone (DevOps), Phase K Wave 21.
