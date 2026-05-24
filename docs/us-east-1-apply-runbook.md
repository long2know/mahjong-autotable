# us-east-1 ACTUAL APPLY runbook (W19 → W20 V2)

> Phase K Wave 19 — Apone (DevOps). **V2 hardening landed at
> Phase K Wave 20** (post-Stephen feedback): the §4 row table
> is now backed by the executable
> [`infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`](../infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh)
> (W20 NEW) and the §6 rollback section now covers the full
> failure-shape catalogue (5 shapes, was 4 at W19).
> Audience: Stephen (operator) and on-call SRE assisting the
> W19 `terraform apply` of the regional EKS stack in
> `us-east-1`. Companion to
> [`docs/regional-eks-bringup.md`](./regional-eks-bringup.md)
> §3.12 (the in-line cross-reference to this runbook) and to
> [`infra/terraform/regional-eks/us-east-1/preflight.yaml`](../infra/terraform/regional-eks/us-east-1/preflight.yaml)
> (the structured pre-flight checklist this runbook walks
> through one row at a time).

W11 → W18 ran terraform DRY-RUN captures for the regional
EKS bring-up in `us-east-1`. The §3.9 gate flipped from W17
PARTIAL-GREEN to W18 FULL-GREEN once Hicks's W17 close-out
landed the eager-bundle reduction; the apply itself is now
Stephen's call. W19 packages everything Stephen needs to
actually run `terraform apply` from his workstation — this
runbook + the structured checklist + a rollback procedure.

This runbook does NOT run `terraform apply` itself. That
remains Stephen's call. **W19 lands the runbook + the pre-
flight artefact; W19 does NOT touch the cluster.**

## 1. Pre-apply verification — pre-flight checklist

The structured pre-flight checklist lives at
[`infra/terraform/regional-eks/us-east-1/preflight.yaml`](../infra/terraform/regional-eks/us-east-1/preflight.yaml).
Walk through each `preconditions[*]` row in declaration order;
mark each row complete in the apply PR description before
opening the apply window. Severity = `blocking` rows are
HARD STOPS; severity = `warning` rows can ship with operator
sign-off.

| # | Row id                  | Severity  | Owner   | Summary                                            |
| - | ----------------------- | --------- | ------- | -------------------------------------------------- |
| 1 | `source-drift`          | blocking  | Apone   | Zero TF source drift vs. W18 capture.              |
| 2 | `aws-creds`             | blocking  | Stephen | Operator AWS creds valid + scoped.                 |
| 3 | `tf-state-bucket`       | blocking  | Stephen | State bucket + lock table READY.                   |
| 4 | `operator-tfvars`       | blocking  | Stephen | Operator tfvars present + validates.               |
| 5 | `plan-replay`           | blocking  | Stephen | Fresh plan matches W18 capture (modulo tfvars).    |
| 6 | `cutover-ready-checklist` | blocking | Stephen | §4.1 per-region checklist 100% green.              |
| 7 | `apply-window`          | warning   | Stephen | Window opens at a low-traffic UTC slot.            |
| 8 | `rollback-pr`           | blocking  | Apone   | Rollback PR drafted (closed-but-ready).            |

The preflight.yaml `preconditions[*].verify` and `expect`
fields carry the exact shell command + expected output for
each row.

## 2. The apply window

Recommended slot: **09:00–11:00 UTC** (a Tuesday or
Wednesday). The apply touches EKS aws-auth ConfigMap + R53
latency policy + IAM bindings; a 5–10 min apply duration is
typical, but a 30 min slot accommodates a re-plan if Row 5
deviates.

### 2.1 Pre-apply: open the apply window PR

Branch: `stlong/phase-k-wave-19-prod-us-east-1-apply`.

PR description template:

```markdown
# W19 — us-east-1 ACTUAL APPLY

## Pre-flight (infra/terraform/regional-eks/us-east-1/preflight.yaml)

- [ ] 1. `source-drift` ............................ GREEN at <timestamp>
- [ ] 2. `aws-creds`   ............................. GREEN at <timestamp>
- [ ] 3. `tf-state-bucket` ......................... GREEN at <timestamp>
- [ ] 4. `operator-tfvars` ......................... GREEN at <timestamp>
- [ ] 5. `plan-replay` ............................. GREEN at <timestamp>
- [ ] 6. `cutover-ready-checklist` ................. GREEN at <timestamp>
- [ ] 7. `apply-window` ............................ GREEN at <timestamp>
- [ ] 8. `rollback-pr` (drafted, NOT merged) ....... GREEN at <timestamp>

## Apply window: <UTC start time> → <UTC end time>

Plan capture attached at `docs/us-east-1-w19-plan-output.txt`.
Rollback PR drafted at #<rollback-pr-num>.

cc: @stephen (operator) / @apone-bot (Apone CI)
```

### 2.2 The apply

From the operator workstation:

```bash
cd infra/terraform/envs/prod
terraform init -reconfigure
terraform plan -out=w19-apply.tfplan \
    | tee ../../../docs/us-east-1-w19-plan-output.txt

# Verify the plan shape matches the W18 capture's expectation.
diff <(grep -E '^(Plan:|  # |  [+~-])' docs/us-east-1-w18-plan-output.txt) \
     <(grep -E '^(Plan:|  # |  [+~-])' docs/us-east-1-w19-plan-output.txt)

# IF the diff is empty (modulo regional_endpoints adds): proceed.
# IF the diff shows unexpected changes: STOP. Open a Wave-20 plan-
# only PR to investigate. DO NOT apply.

# Apply.
terraform apply w19-apply.tfplan
```

The apply prints per-resource progress; expect:

```
aws_eks_cluster.regional["us-east-1"]: Creating...
aws_route53_record.regional["us-east-1"]: Creating...
aws_acm_certificate.regional["us-east-1"]: Creating...
...
Apply complete! Resources: <N> added, 0 changed, 0 destroyed.
```

ANY non-zero `destroyed` count is a HARD STOP — abort + run
the §6 rollback.

## 3. Post-apply — verify the apex resolves us-east-1

The plan-then-apply flow only validates terraform-side
state. The OPERATOR-SIDE verification is the §4 smoke tests
(against the live apex).

### 3.1 R53 propagation wait

After `terraform apply` exits 0:

```bash
# R53 TTL on the apex latency set is 60s (per
# infra/terraform/modules/edge/r53-regional-records.tf).
# Wait ≥ 60s before running the smoke tests.
sleep 90
```

### 3.2 Capture the live apex DNS shape

```bash
# Capture the apex resolution from three resolvers to confirm
# propagation.
for resolver in 8.8.8.8 1.1.1.1 9.9.9.9; do
    echo "=== $resolver ==="
    dig +short mahjong.example.com @$resolver
done
```

Expect: all three resolvers return a `*.elb.us-east-1.amazonaws.com`
CNAME (or the ALB's A-record set for the latency-routed `us-
east-1` endpoint, depending on the W12 EDGE module shape at
W19 HEAD).

## 4. Post-apply smoke tests

The preflight.yaml `smokeTests[*]` list carries the exact
shell commands. **W20 V2 hardening:** these are now wrapped
by the executable
[`infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`](../infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh)
script — one invocation runs all 8 invariants (the original
W19 V1 four + four W20-added cluster-side invariants) and
exits non-zero if any fail.

### 4.0 Run the W20 smoke-test script

```bash
# Single-shot. Exits 0 = all 8 invariants passed; 1 = at
# least one failed (per-row OK/FAIL printed); 2 = bad
# invocation. Re-runnable; idempotent; read-only (no
# kubectl apply / terraform / aws modify calls).
bash infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh

# Tunables (env vars, optional):
#   APEX=mahjong.example.com               # the R53 apex
#   REGIONAL_APEX=us-east-1.mahjong.example.com  # regional CNAME
#   EXPECTED_REGION=us-east-1
#   HEALTH_CHECK_REF=mahjong-prod-us-east-1
#   EKS_CLUSTER_NAME=mahjong-prod-us-east-1
APEX=mahjong.example.com bash infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh
```

### 4.1 The 8 invariants (W20 V2)

| # | Smoke test                | Verify                                                                | Pass criterion             |
| - | ------------------------- | --------------------------------------------------------------------- | -------------------------- |
| 1 | `r53-latency-resolves`    | `dig +short mahjong.example.com @8.8.8.8`                             | `*.elb.us-east-1...` CNAME |
| 2 | `alb-200`                 | `curl -sS -o /dev/null -w "%{http_code}" https://us-east-1.mahjong.example.com/healthz` | `200`                     |
| 3 | `r53-health-check`        | `aws route53 get-health-check-status ...`                             | `Success` × N              |
| 4 | `signalr-handshake`       | `curl -sS -X POST .../hubs/changsha/negotiate \| jq .availableTransports[0].transport` | `"WebSockets"` |
| 5 | `eks-cluster-active`      | `aws eks describe-cluster --region us-east-1 ... --query cluster.status` | `ACTIVE`                |
| 6 | `deployment-ready`        | `kubectl -n mahjong-prod get deployment prod-mahjong-autotable -o jsonpath='{.status.readyReplicas}'` | `>= 3` AND `= .spec.replicas` |
| 7 | `kyverno-enforce`         | `kubectl get clusterpolicy disallow-lateral-movement -o jsonpath='{.spec.validationFailureAction}'` | `Enforce` (W20 flip) |
| 8 | `coturn-reachable`        | `kubectl -n mahjong-prod get svc prod-mahjong-autotable-coturn -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'` | non-empty           |

Rows 1–4 are the W19 V1 catalogue; rows 5–8 are the W20 V2
additions Stephen requested post-W19 (the W19 runbook only
checked the front-of-cluster shape; the V2 set covers the
control-plane + workload-readiness + admission-policy
posture too).

### 4.2 Optional follow-up (within 5 minutes of apply)

* `docs/edge-region-probes.md §4` runbook — full per-region
  probe sweep. The W14 §7 cross-reference outlines the resolver
  rotation pattern.
* Hudson's `regional-latency-apex` panel — confirm the panel
  surfaces a non-zero `us-east-1` traffic sample within the
  first 60s.

## 5. Post-apply checklist (W19 retro hand-off)

| #  | Action                                                                                       | Owner       |
| -- | -------------------------------------------------------------------------------------------- | ----------- |
| 1  | Capture the full `terraform apply` log in `docs/us-east-1-w19-apply-log.txt`.                | Stephen     |
| 2  | Archive the W19 plan capture in `docs/us-east-1-w19-plan-output.txt`.                        | Stephen     |
| 3  | File a brief retro note under the W19 retro: per-region apply timing + any anomalies.        | Apone       |
| 4  | Flip `docs/regional-eks-bringup.md §3.9` from "FULL-GREEN apply-readiness" to "us-east-1 LIVE". | Apone (W20) |
| 5  | Run the §4 smoke tests and attach output to the apply PR.                                    | Stephen     |
| 6  | Close the rollback PR (no longer needed once apply is GREEN).                                | Stephen     |
| 7  | Schedule Hudson's `regional-latency-apex` 7-day soak review.                                 | Apone (W20) |

## 6. Rollback procedure (W20 V2)

If the apply fails partway, OR if the §4 smoke-test script
exits non-zero, recover via the drafted rollback PR
(preflight.yaml row 8).

### 6.1 Recognise the failure shapes

| Symptom                                              | Likely cause                              | Recovery |
| ---------------------------------------------------- | ----------------------------------------- | -------- |
| `terraform apply` exits non-zero before completion   | API rate-limit / IAM permission issue     | Re-run with `-parallelism=1`; if still failing, follow §6.2. |
| Apply completes but apex doesn't resolve us-east-1   | R53 propagation delay (rare with 60s TTL) | Wait 5 min then re-test. If still missing, §6.2. |
| Apply completes but ALB returns 5xx                  | Cluster not yet healthy / ESO not ready   | §6.2 if persists > 5 min. |
| Apply completes but SignalR handshake fails          | Ingress-class or cookie affinity broken   | §6.2. |
| Apply tries to DESTROY a resource (Row 4 plan-replay flagged) | State drift between W18 capture and W19 apply | HARD STOP — abort before the apply. |
| Smoke-test script invariant 5 (`eks-cluster-active`) reports non-ACTIVE | EKS control-plane stuck on bootstrap; ENI-attach race | §6.2 if persists > 10 min (eks-cluster bring-up is typically 8–10 min). |
| Smoke-test script invariant 6 (`deployment-ready`) reports ready < 3 | Image-pull failure / ESO secret not yet materialised / HPA mis-target | Triage with `kubectl -n mahjong-prod describe deployment` before §6.2. If ESO is the cause, wait 5 min for the `mahjong-prod-omnibus` ExternalSecret refresh. |
| Smoke-test script invariant 7 (`kyverno-enforce`) reports a non-Enforce ClusterPolicy | W20 enforce-flip didn't propagate, OR the cluster pre-dates W20 | Re-apply `infra/k8s/base/kyverno-policies/*.yaml`. If still wrong, the cluster needs a Kyverno controller restart. |

### 6.2 Rollback steps

```bash
# 1. Merge the rollback PR (regional_endpoints drops us-east-1).
gh pr merge --merge \
    stlong/phase-k-wave-19-prod-us-east-1-rollback

# 2. Pull + apply the rollback branch.
git checkout main
git pull origin main
cd infra/terraform/envs/prod
terraform apply -auto-approve

# 3. Verify the apex routes AWAY from us-east-1.
dig +short mahjong.example.com @8.8.8.8
# Expect: empty (if us-east-1 was the only entry) OR a
# *.elb.<other-region>.amazonaws.com CNAME.

# 4. Verify R53 health-check status reflects the rollback.
aws route53 list-health-checks \
    --query 'HealthChecks[?CallerReference==`mahjong-prod-us-east-1`]'
# Expect: empty (the rollback PR drops the health-check).

# 5. Re-run the smoke-test script with EXPECTED_REGION
#    pointing at the surviving region — confirm the cluster
#    is healthy AFTER the rollback.
EXPECTED_REGION=us-west-2 REGIONAL_APEX=us-west-2.mahjong.example.com \
    bash infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh

# 6. File a W19 retro note documenting the failure shape +
#    the deferral to Wave-20+.
```

### 6.3 Rollback failsafe (W20 V2 addition)

If the §6.2 sequence itself fails (terraform refuses to
destroy because of a cluster-side controller still holding
a finalizer, or the ALB lingers in `Deleting` state past the
60s TTL), the operator can issue a MANUAL CNAME flip via
the R53 console to drop the `us-east-1` latency record
before terraform finishes cleaning up the AWS-side state.
The CNAME flip is observable within 60s (TTL); the terraform-
side cleanup can then proceed at the controller's pace
without blocking user traffic.

The R53 console-side flip is documented at
`docs/regional-eks-bringup.md §6.2` (operator-only —
requires R53 console write access scoped to the
`mahjong.example.com` hosted-zone).

## 7. Cross-references

- [`docs/regional-eks-bringup.md`](./regional-eks-bringup.md)
  §3.9–§3.12 — apply-readiness gate + W19 runbook reference.
- [`docs/regional-eks-bringup.md §4.1`](./regional-eks-bringup.md)
  — operator-driven per-region Cutover-Ready checklist.
- [`docs/us-east-1-w18-plan-output.txt`](./us-east-1-w18-plan-output.txt)
  — W18 plan capture (the dry-run shape the apply will match
  modulo tfvars).
- [`infra/terraform/regional-eks/us-east-1/preflight.yaml`](../infra/terraform/regional-eks/us-east-1/preflight.yaml)
  — structured pre-flight checklist (the YAML this runbook
  walks through one row at a time).
- [`infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`](../infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh)
  — **W20 V2** post-apply 8-invariant verifier (§4 automation).
- [`docs/edge-region-probes.md`](./edge-region-probes.md) — per-
  region probe runbook for the post-apply smoke sweep.
- [`docs/terraform.md`](./terraform.md) §5 — W7 EDGE module
  reference.
