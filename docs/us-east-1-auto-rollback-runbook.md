# us-east-1 auto-rollback — W22 operator runbook

> Phase K Wave 22 — Apone (DevOps).
> Audience: SRE / on-call operator running the
> `us-east-1-auto-rollback` CI workflow + driving a real
> apply with `enable_auto_rollback=true`. Companion to
> [`docs/us-east-1-apply-runbook.md`](./us-east-1-apply-runbook.md)
> (the W19/W20/W21 apply runbook this extends).

## 1. Background

W19/W20/W21 layered the us-east-1 ACTUAL APPLY safety
stack:

* **W19** — pre-apply checklist (`preflight.yaml`) +
  apply runbook.
* **W20** — post-apply 8-invariant smoke-test script
  (`post-apply-smoke-test.sh`) + V2 hardening of the
  runbook.
* **W21** — opt-in terraform `null_resource` provisioner
  (`auto-rollback.tf`) that fires the smoke-test as a
  post-apply hook and triggers `terraform destroy
  -auto-approve` if any of the 8 invariants fails within
  a 5-minute window. Opt-in via
  `var.enable_auto_rollback=true`; dry-run via
  `var.auto_rollback_dry_run=true`.

The W21 piece was OPT-IN BY DEFAULT — an operator who
applied without setting the variable got the W19/W20
shape (manual smoke + manual destroy). W22 wires the
opt-in path into a CI workflow that:

1. Validates the terraform module + smoke-test script
   shape week-over-week (catches drift before the next
   manual apply).
2. Exercises the dry-run plan in CI so the PR reviewer
   sees the null_resource diff inline.
3. Gates the ACTUAL `auto_rollback_dry_run=false` flip
   behind an explicit `workflow_dispatch` input.

## 2. The W22 workflow

[`.github/workflows/us-east-1-auto-rollback.yml`](../.github/workflows/us-east-1-auto-rollback.yml)

### 2.1 Triggers

| Trigger | Mode | Use case |
| ------- | ---- | -------- |
| `pull_request` (paths-filtered) | DRY-RUN | Catch lint/shape regressions before merge |
| `schedule` (Sunday 02:00 UTC) | DRY-RUN | Week-over-week drift detection |
| `workflow_dispatch` (default inputs) | DRY-RUN | Operator-initiated dry-run validation |
| `workflow_dispatch` (`actually_rollback_on_failure=true`) | **ACTUAL** | Operator-initiated prod rollback enablement |

Three of four trigger modes are DRY-RUN. The fourth
requires explicit `workflow_dispatch` opt-in with the
`actually_rollback_on_failure` input flipped to `true`.

### 2.2 Job graph

```
validate ──→ dry-run-plan ──→ auto-rollback-trigger (workflow_dispatch only)
  │              │
  │              └── PR comment with terraform plan output (pull_request only)
  │
  └── terraform fmt + validate + init + smoke-shape bash -n
```

* **validate** — `terraform fmt -check` + `terraform
  init -backend=false` + `terraform validate` against
  the regional-eks/us-east-1 workspace + `bash -n` on
  the smoke-test script. Runs on every trigger.
* **dry-run-plan** — `terraform plan` with `enable_auto_
  rollback=true` and `auto_rollback_dry_run` set per
  trigger mode. Plan output is uploaded as an artefact
  (`us-east-1-auto-rollback-plan-${RUN_ID}`); on PR
  triggers, also posted as a PR comment.
* **auto-rollback-trigger** — runs only on
  `workflow_dispatch`. Validates the trigger contract +
  emits a notice with the effective opt-in flags. Does
  NOT execute the smoke test against a real cluster
  (the CI runner has no kubectl context).

### 2.3 Inputs (workflow_dispatch)

| Input | Default | Effect |
| ----- | ------- | ------ |
| `actually_rollback_on_failure` | `false` | When `true`, sets `auto_rollback_dry_run=false` in the plan — captures the LIVE rollback shape |
| `smoke_timeout_seconds` | `300` | The 5-minute window the null_resource enforces. Matches the W21 design point |

## 3. Operator runbook — prod opt-in path

The W21 + W22 auto-rollback safety net is OPT-IN. Stephen
follows this sequence to enable it on a real us-east-1
apply:

### 3.1 Week 1 — dry-run validation in CI

1. Trigger the W22 workflow via `workflow_dispatch` with
   `actually_rollback_on_failure=false` (default):

   ```bash
   gh workflow run us-east-1-auto-rollback.yml \
       --ref main
   ```

2. Verify the run completes green. Download the plan
   artefact:

   ```bash
   gh run download <run-id> --name us-east-1-auto-rollback-plan-<run-id>
   ```

3. Inspect `plan.txt` for the expected `null_resource.
   us_east_1_auto_rollback[0]` resource diff. The
   `triggers` map should show `auto_rollback_dry_run =
   "true"` (the LOG-only path).

### 3.2 Week 2 — staging-tier apply with dry-run

1. On a staging-tier us-east-1 workspace, set
   `enable_auto_rollback=true` +
   `auto_rollback_dry_run=true` in `staging.tfvars`.
2. Run a real `terraform apply` against the staging
   workspace.
3. MANUALLY INJECT a smoke-test failure (per W21
   `auto-rollback.tf` comment block):

   ```bash
   kubectl drain <only-node> --ignore-daemonsets --force
   ```

4. Observe the `null_resource` provisioner LOG the
   rollback action without executing it:

   ```
   [auto-rollback] smoke-test FAILED (exit=1) — rollback engaged
   [auto-rollback] DRY-RUN: would have run 'terraform destroy -auto-approve'
   ```

5. Restore the staging cluster (uncordoning the drained
   node) + capture the log into
   `.work/apone-w22-evidence/staging-dryrun-${date}.txt`.

### 3.3 Week 3 — prod apply with full opt-in

ONLY AFTER §3.1 + §3.2 validate clean:

1. Update `prod.tfvars` to flip `auto_rollback_dry_run`
   to `false`:

   ```hcl
   enable_auto_rollback  = true
   auto_rollback_dry_run = false
   auto_rollback_smoke_timeout_seconds = 300
   ```

2. Verify the W22 CI workflow shows green for the
   commit that updates `prod.tfvars`:

   ```bash
   gh workflow run us-east-1-auto-rollback.yml \
       --ref main \
       -f actually_rollback_on_failure=true
   ```

3. The workflow's `auto-rollback-trigger` job confirms
   the trigger contract surface; the actual destroy
   fires from the null_resource provisioner at
   `terraform apply` time.

4. Run the prod apply per
   [`docs/us-east-1-apply-runbook.md §3`](./us-east-1-apply-runbook.md)
   with the W21 + W22 safety net wired.

5. The post-apply smoke-test runs automatically; on
   failure within the 5-minute window, `terraform
   destroy -auto-approve` fires.

## 4. Failure semantics + escalation

| Failure | Operator action |
| ------- | --------------- |
| `validate` job red on PR | Fix terraform fmt / validate issues in PR before merge |
| `validate` job red on schedule | Open an apone-lane bug — terraform module has drifted |
| `dry-run-plan` job red | Cluster state has drifted; inspect plan artefact + reconcile |
| `auto-rollback-trigger` job red on `workflow_dispatch` | Re-check the input values + smoke-test script shape |
| Smoke test FAILS in prod apply with `auto_rollback_dry_run=false` | Cluster is destroyed; investigate the failure cause via the captured `LATEST_LOGDIR/rollback.log` |
| Smoke test PASSES in prod apply | No action — auto-rollback hook completes silently |

The scheduled weekly run is a CANARY — a red Sunday
notification surfaces drift in the terraform module /
smoke-test script BEFORE the next manual apply runs
against it. The triage SLA is 7 days (close out before
the next Sunday cron).

## 5. The 5-minute window contract

The W21 `auto-rollback.tf` enforces a 5-minute
(`timeout 300`) window on the smoke-test invocation.
The W22 workflow exposes this as the
`smoke_timeout_seconds` input. Why 5 minutes:

* **EKS healthy-cluster baseline** — a freshly-applied
  regional EKS cluster's 8 invariants
  (API server reachable, system pods Ready, default
  StorageClass present, CNI installed, kube-proxy
  healthy, DNS resolving, NetworkPolicy enforced, IAM
  OIDC provider provisioned) all resolve in under 5
  minutes on a healthy apply.
* **Beyond 5 minutes** the smoke is assumed to have
  hung (network partition, kubelet unavailable, control-
  plane DNS resolution stalled) — the safety net
  assumes the worst and rolls back.
* **Shrinking the window** below 5 minutes risks false
  positives (e.g. CNI plugin install takes ~90s; cert-
  manager bootstrap takes ~120s; both are legitimate).
* **Growing the window** above 10 minutes weakens the
  safety net's recovery time objective. Stephen has
  documented 10min as the upper bound in the
  `docs/us-east-1-apply-runbook.md §7` table.

## 6. CI plan-output review checklist

When inspecting the `dry-run-plan` artefact's `plan.txt`,
look for:

* **`null_resource.us_east_1_auto_rollback[0]` will be
  created** — confirms the null_resource will fire on
  the next real apply.
* **`triggers.auto_rollback_dry_run`** — expected
  `"true"` for DRY-RUN runs; `"false"` only on the
  `actually_rollback_on_failure=true` opt-in.
* **`triggers.auto_rollback_smoke_timeout_seconds`** —
  expected `"300"` unless the operator has explicitly
  changed the input.
* **`triggers.apply_timestamp`** — non-empty timestamp;
  forces re-run on every fresh apply.

If the plan output shows any DIFF other than the
expected null_resource creation, halt — the workspace
has drifted from the captured state.

## 7. Cross-references

* [`.github/workflows/us-east-1-auto-rollback.yml`](../.github/workflows/us-east-1-auto-rollback.yml)
  — the W22 workflow.
* [`infra/terraform/regional-eks/us-east-1/auto-rollback.tf`](../infra/terraform/regional-eks/us-east-1/auto-rollback.tf)
  — the W21 null_resource provisioner.
* [`infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`](../infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh)
  — the W20 V2 8-invariant smoke.
* [`docs/us-east-1-apply-runbook.md`](./us-east-1-apply-runbook.md)
  — the W19/W20/W21 apply runbook (§7 covers the W21
  null_resource hand-off).
* [`infra/terraform/regional-eks/us-east-1/preflight.yaml`](../infra/terraform/regional-eks/us-east-1/preflight.yaml)
  — W19 pre-apply checklist.

## 8. W22 → W23 hand-off

* Stephen executes §3.1 → §3.2 → §3.3 over weeks 1-3
  post-W22. The actual prod opt-in lands when §3.3 is
  documented clean.
* W23+ may extend the workflow with:
  * a **`post-apply` trigger** that reuses the dry-run
    plan against the live workspace state (currently the
    W22 workflow only plans against the module shape, not
    against a real backend);
  * a **`destroy-then-restore` rehearsal** — runs the
    full destroy on a staging cluster + re-applies, with
    timing captured. Validates the RTO claim of <30 min
    end-to-end.
  * a **cross-region failover** smoke that asserts the
    primary `infra/terraform` workspace is reachable
    after the us-east-1 destroy fires (parallel to the
    W19 DR-rehearsal harness).
