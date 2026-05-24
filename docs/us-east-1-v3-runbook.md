# us-east-1 V3 runbook — post-rollback drift reconciliation

> Phase K Wave 23 — Apone (DevOps). **V3 extends V2** (W20)
> by adding a structured post-rollback drift reconciliation
> path. V2 documented HOW to roll back; V3 documents WHAT
> TO DO with the cluster state that remains AFTER the
> rollback completes — the silent drift that accumulates
> when terraform destroys part of a stack but cluster-side
> objects (CRDs, ExternalSecrets, Kyverno PolicyReports,
> Argo Rollouts state) persist in etcd.
>
> Audience: SRE / on-call operator AFTER executing a V2
> rollback (§6) on the
> [`docs/us-east-1-apply-runbook.md`](./us-east-1-apply-runbook.md)
> or after the W21 auto-rollback `null_resource` fires.
> Companion to:
>
> * [`docs/us-east-1-apply-runbook.md`](./us-east-1-apply-runbook.md)
>   (W19 V1, W20 V2 — apply + rollback).
> * [`docs/us-east-1-auto-rollback-runbook.md`](./us-east-1-auto-rollback-runbook.md)
>   (W22 — auto-rollback workflow + opt-in).

## 1. Background

W19 / W20 / W21 / W22 layered the us-east-1 safety stack:

* **W19** — pre-apply checklist + apply runbook.
* **W20** — V2 8-invariant post-apply smoke + rollback
  failure-shape catalogue (5 shapes).
* **W21** — opt-in `null_resource` auto-rollback
  provisioner (`var.enable_auto_rollback=true`).
* **W22** — CI workflow + dry-run gate around the W21
  opt-in path.

What V2 and the W22 workflow do NOT cover: the
**post-rollback drift surface**. When terraform destroys
the regional stack (or the auto-rollback provisioner
fires `terraform destroy -auto-approve`), the AWS-side
resources go away — but the cluster-side state may
persist. V3 codifies the reconciliation path.

## 2. Drift surfaces that survive a V2 rollback

| Surface | Persistence shape | Reconciliation owner |
| ------- | ----------------- | -------------------- |
| EKS aws-auth ConfigMap | Cluster destroyed → ConfigMap gone with it. | None — V2 destroy is clean. |
| R53 latency policy | TF-managed; destroyed cleanly. | None. |
| ExternalSecrets `mahjong-jwt-rsa-keys` | ESO is namespace-scoped; if the destroy left the SSM params alone, a re-apply re-binds. | Apone — re-apply step §3.2. |
| Kyverno ClusterPolicies (W3 / W4 / W15 / W19 / W21 / W23) | Cluster destroyed → policies gone. | None for full destroy; §3.3 for partial. |
| Kyverno PolicyReport / ClusterPolicyReport | Background-scan output; rebuilds on cluster re-create. | None. |
| Argo Rollouts AnalysisRun state | Rollouts CRD persists per cluster lifetime. | Hicks-coord — §3.4. |
| coturn DH params + TURN session keys | Kubernetes Secret; rebuilds on re-apply. | Apone — §3.5. |
| Hudson metric backfill | Prometheus TSDB on the obs cluster — DIFFERENT cluster, survives the apply-target rollback. | Hudson — §3.6 (out-of-band coord). |
| Sigstore / Fulcio entries | Off-cluster; persistent ledger. | None — by design. |

## 3. Reconciliation steps (in execution order)

### 3.1 Verify the rollback completed cleanly

Run the W20 §6.1 failure-shape recognition checklist
FIRST. If any of the 5 shapes is unresolved, return to
the W20 V2 runbook before continuing.

```bash
# Confirm the regional EKS control-plane is destroyed.
aws eks describe-cluster \
   --name mahjong-prod-use1 \
   --region us-east-1 \
   2>&1 | grep -q "ResourceNotFoundException" \
   && echo "✓ EKS cluster destroyed" \
   || echo "✗ EKS cluster STILL present — return to W20 V2 §6"

# Confirm the R53 latency policy is destroyed.
aws route53 list-resource-record-sets \
   --hosted-zone-id Z<your-zone-id> \
   --query "ResourceRecordSets[?Name=='use1.mahjong.example.com.']" \
   2>&1 | jq -e 'length == 0' \
   && echo "✓ R53 latency record destroyed" \
   || echo "✗ R53 record STILL present"
```

### 3.2 Reconcile ESO-managed secrets

If the rollback destroyed the EKS cluster but LEFT the
SSM parameters in place (common — the W17 SSM module is
in a different terraform state), re-applying the cluster
will re-bind ESO without operator action. If the
rollback ALSO destroyed the SSM params (rare, only when
the operator ran `terraform destroy -target=module.ssm`),
re-provision per
[`docs/jwt-ssm-runbook.md §3`](./jwt-ssm-runbook.md).

```bash
# Audit SSM param presence post-rollback.
aws ssm get-parameters-by-path \
   --path "/mahjong/jwt-rsa-keys/" \
   --region us-east-1 \
   --query "Parameters[*].Name"

# Expected: 3 params (current/next/previous). If empty,
# re-provision before re-applying the cluster.
```

### 3.3 Reconcile Kyverno policies — partial-destroy case

If the rollback was a PARTIAL destroy (e.g.
`-target=module.eks_addons`) that took out the Kyverno
controller while leaving the EKS control-plane, the
ClusterPolicy CRD instances may persist in etcd as
ghost objects. Reconcile:

```bash
# Audit Kyverno policy CRD presence.
kubectl get clusterpolicy -o name | sort

# Expected (post-W23 baseline — 9 policies):
#   clusterpolicy/disallow-host-paths        (W22 Enforce)
#   clusterpolicy/disallow-lateral-movement  (W20 Enforce)
#   clusterpolicy/enforce-prod-default       (W15 Enforce)
#   clusterpolicy/enforce-prod-mahjong-images (W4 Enforce)
#   clusterpolicy/require-network-policy     (W20 Enforce)
#   clusterpolicy/require-readonly-rootfs    (W23 Audit)
#   clusterpolicy/require-resource-limits    (W22 Enforce)
#   clusterpolicy/require-runas-non-root     (W23 Audit)
#   clusterpolicy/verify-mahjong-images      (W3 Audit + override)

# If any are missing, re-apply from the per-file path.
for P in \
   infra/k8s/base/kyverno-policies/disallow-host-paths.yaml \
   infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml \
   infra/k8s/base/kyverno-policies/require-network-policy.yaml \
   infra/k8s/base/kyverno-policies/require-readonly-rootfs.yaml \
   infra/k8s/base/kyverno-policies/require-resource-limits.yaml \
   infra/k8s/base/kyverno-policies/require-runas-non-root.yaml; do
  kubectl apply -f "$P"
done
```

### 3.4 Reconcile Argo Rollouts state

Argo Rollouts state is held in CRDs — Rollout,
AnalysisTemplate, AnalysisRun. A full cluster destroy
takes them all with it; a partial destroy may leave
stale AnalysisRun rows.

```bash
# Audit pending AnalysisRuns post-rollback.
kubectl get analysisrun -A \
   -o jsonpath='{range .items[*]}{.metadata.namespace}/{.metadata.name}\t{.status.phase}\n{end}'

# If any are stuck in `Running`, they're from before
# the rollback — delete them so the next rollout starts
# clean.
kubectl get analysisrun -A \
   -o jsonpath='{range .items[?(@.status.phase=="Running")]}{.metadata.namespace}/{.metadata.name} {end}' \
   | xargs -n1 kubectl delete analysisrun -A --
```

Coordinate with **Hicks** before deleting — an
in-flight rollout is a SIGNAL that the prior cluster
state was carrying real traffic.

### 3.5 Reconcile coturn state

The coturn TURN server's DH params live in a Secret;
the static-auth-secret HMAC is in another Secret.
Both are namespace-scoped and rebuild on re-apply via
the W6 manifest set.

```bash
# Audit coturn Secret presence post-rollback.
kubectl -n mahjong-prod get secret \
   coturn-dh-params \
   coturn-static-auth-secret \
   -o jsonpath='{range .items[*]}{.metadata.name}\t{.type}\n{end}' \
   2>&1

# If MISSING, re-apply via:
kubectl apply -k infra/k8s/overlays/prod/

# The W6 ExternalSecret in the prod overlay re-binds the
# static-auth-secret from SSM (`/mahjong/turn/secret`).
```

### 3.6 Hudson metric coordination (out-of-band)

The observability cluster lives in a DIFFERENT
terraform state from the regional apply target. The
W20 rollback does NOT touch Hudson's Prometheus TSDB —
metric data PERSISTS. Two consequences:

1. **The W22 SignalR sticky-session affinity histogram
   continues recording** even with the cluster gone,
   so the post-rollback time window will surface as a
   gap (no data) rather than a noisy scrape failure.
2. **Hudson's `kyverno-deny-events` panel** will lose
   its data source until the regional cluster comes
   back. Coordinate with Hudson on a panel-level
   "expected outage" annotation if the rollback
   window exceeds 1 hour.

```bash
# Note the rollback window in the Hudson PR audit log
# so the on-call dashboard reviewer doesn't flag the
# panel-gone state as an obs-cluster regression.
gh issue comment <hudson-tracking-issue> \
   --body "us-east-1 rollback executed at $(date -u +%FT%TZ); \
   kyverno-deny-events + signalr-sticky-affinity panels \
   will show gap until next \`terraform apply\`."
```

## 4. Re-apply readiness gate

After reconciliation, the cluster is ready to re-apply
ONLY when the V2 §1 pre-flight checklist passes again
(8 rows green). Do not skip the §1 walk-through — the
post-rollback state is functionally equivalent to a
fresh apply.

### 4.1 V3-specific additions to the §1 checklist

Two new rows that the V2 checklist does NOT cover:

| # | Row id              | Severity | Owner   | Summary                                          |
| - | ------------------- | -------- | ------- | ------------------------------------------------ |
| 9 | `drift-reconciled`  | blocking | Apone   | §3.1 → §3.5 reconciliation steps all executed.   |
| 10| `hudson-annotated`  | warning  | Apone   | §3.6 panel-gap annotation posted (>1hr outages). |

Append these rows to the apply PR description before
re-opening the window.

## 5. Failure shapes specific to V3

The V2 §6.1 catalogue lists 5 failure shapes during
rollback. V3 adds 2 NEW shapes specific to the
reconciliation phase:

| Shape | Recognition | Remediation |
| ----- | ----------- | ----------- |
| **6: SSM params destroyed** | `aws ssm get-parameters-by-path` returns empty for `/mahjong/jwt-rsa-keys/`. | Re-provision per [`docs/jwt-ssm-runbook.md §3`](./jwt-ssm-runbook.md). |
| **7: Kyverno CRD ghost** | `kubectl get clusterpolicy` returns 0 entries on a cluster that survives the rollback. | Re-apply via §3.3 loop. |

## 6. Rollback of the rollback (escalation)

If reconciliation reveals the rollback was UNNECESSARY
(e.g. the smoke-test invariant that triggered it was a
false positive), the path back is a fresh
`terraform apply`. Do not attempt to "undo" the rollback
in-place — terraform's state is the source of truth,
and an in-place state-edit is a higher-risk operation
than a clean re-apply against a reconciled cluster.

Escalation contact: **Stephen** (final operator) +
**Apone** (rollback authoring). Surface in
`#mahjong-sre` with the §3.1 audit output attached.

## 7. V3 → V4 hand-off candidates

* **V4** — IF Stephen lands real production rollback
  evidence, V4 will replace the §5 "shapes 6 + 7"
  hypotheticals with capture-from-actual-rollback data.
  V4 also folds the §3 reconciliation steps into the
  W21 `null_resource` provisioner as post-destroy
  steps (so reconciliation runs automatically rather
  than via operator runbook walkthrough).
* **Auto-reconciliation gate** — a candidate V4
  addition is a `terraform apply` precondition that
  refuses to re-apply when §3.1 → §3.5 reconciliation
  hasn't been logged.

## 8. Cross-references

* [`docs/us-east-1-apply-runbook.md`](./us-east-1-apply-runbook.md) —
  W19 V1 + W20 V2 (apply + smoke + rollback).
* [`docs/us-east-1-auto-rollback-runbook.md`](./us-east-1-auto-rollback-runbook.md) —
  W22 auto-rollback workflow.
* [`infra/terraform/regional-eks/us-east-1/auto-rollback.tf`](../infra/terraform/regional-eks/us-east-1/auto-rollback.tf) —
  W21 `null_resource` provisioner.
* [`infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`](../infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh) —
  W20 8-invariant smoke script.
* [`docs/jwt-ssm-runbook.md`](./jwt-ssm-runbook.md) —
  SSM JWT param provisioning (referenced from §3.2).
* [`docs/kyverno-w23-additional-rules.md`](./kyverno-w23-additional-rules.md) —
  W23 audit-mode rule pair (referenced from §3.3).
