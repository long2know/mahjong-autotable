# Kyverno W22 — enforce-flip cutover (3rd batch)

> Phase K Wave 22 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W22 enforce-
> flip on the W21 audit-mode rule pair (resource-limits +
> hostPath-deny). Companion to
> [`docs/kyverno-w21-additional-rules.md`](./kyverno-w21-additional-rules.md)
> (the W21 audit-mode launch) and to
> [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
> (the W19 launch + W20 enforce-flip precedent).

W22 closes the **third batch** of audit→enforce flips in
the W19-lineage. Both W21 policies (`require-resource-
limits` + `disallow-host-paths`) flip to **Enforce** in a
single commit after a 5-day clean audit window.

| Rule | File | W21 → W22 flip | Background scan ack | Pre-W22 PolicyReport count |
| --- | --- | --- | --- | --- |
| `require-resource-limits` | [`infra/k8s/base/kyverno-policies/require-resource-limits.yaml`](../infra/k8s/base/kyverno-policies/require-resource-limits.yaml) | `Audit → Enforce` + `Ignore → Fail` | clean | 0 |
| `disallow-host-paths` | [`infra/k8s/base/kyverno-policies/disallow-host-paths.yaml`](../infra/k8s/base/kyverno-policies/disallow-host-paths.yaml) | `Audit → Enforce` + `Ignore → Fail` | clean | 0 |

## 1. What changes at W22

Two edits per policy file (parallel to the W20 enforce-
flip shape on `disallow-lateral-movement.yaml` +
`require-network-policy.yaml`):

```diff
   spec:
-    validationFailureAction: Audit
+    validationFailureAction: Enforce
     background: true
     webhookTimeoutSeconds: 30
-    failurePolicy: Ignore
+    failurePolicy: Fail
```

Title + description annotations updated from
`(W21, Audit)` → `(W22, Enforce)` so PolicyReport rows +
`kubectl describe clusterpolicy` output reflects the new
enforce posture.

### 1.1 Semantic effect

* `validationFailureAction: Enforce` — admission denial
  surfaces at `kubectl apply` time. Pods that omit
  `resources.limits.cpu`/`memory` or declare a `hostPath`
  volume are REJECTED rather than logged as PolicyReport
  rows.
* `failurePolicy: Fail` — paired with Enforce. A Kyverno
  webhook outage during the W22+ window now SURFACES as
  an admission denial (fail-closed). This is the canonical
  W19-lineage enforce-flip shape — see
  [`docs/kyverno-w19-additional-rules.md §3`](./kyverno-w19-additional-rules.md)
  for the rationale on why Audit pairs with Ignore but
  Enforce pairs with Fail.

## 2. Why the 5-day audit window is required first

Same rationale as W19→W20 and W21 lineage:

1. **Audit mode collects ground truth.** The W21 audit
   window's `kubectl get policyreport -A` snapshots
   surface any prod workload that violates the invariant
   AS A REPORT rather than an admission denial. Without
   the audit window, a missed misconfiguration would
   wedge the next prod deploy.
2. **5 days = one weekly release cadence.** The audit
   window's lower bound is set by the release.yml +
   helm-release.yml cadence — five days covers a Monday
   ship + a mid-week patch ship + the Friday backfill
   window. Any pod that lands during that window without
   resource limits OR with a hostPath volume surfaces
   BEFORE the enforce flip.
3. **Background scans persist post-flip.** `background:
   true` is unchanged at W22 — the existing PolicyReport
   stream continues, but now any new violation TRIGGERS
   admission denial alongside the report.

## 3. Cutover-day evidence (5-day clean audit window)

The W21→W22 audit window opened with the W21 ship +
closed with the W22 cutover commit. Five days of
`kubectl get policyreport -A` snapshots confirmed zero
violations against the two new rules.

### 3.1 Audit-window snapshot procedure (executed daily)

```bash
$ kubectl get policyreport -A \
    -o json \
  | jq -r '.items[]
            | select(.results[]?
                     .policy
                     | test("require-resource-limits|disallow-host-paths"))
            | "\(.metadata.namespace)/\(.metadata.name) → \(.results[]
                                                              | select(.policy | test("require-resource-limits|disallow-host-paths"))
                                                              | .result)"'
# Each daily run captured into .work/apone-w22-evidence/
# kyverno-day-<N>.txt; days 1-5 all PASS-only.
```

Captured daily snapshots (W21 land = day 0; W22 cutover =
day 5+):

| Snapshot day | Rule | Pass rows | Fail rows | Error rows |
| --- | --- | --- | --- | --- |
| Day 1 | `require-resource-limits/require-cpu-limit` | 27 | 0 | 0 |
| Day 1 | `require-resource-limits/require-memory-limit` | 27 | 0 | 0 |
| Day 1 | `disallow-host-paths/deny-host-path-volumes` | 27 | 0 | 0 |
| Day 2 | `require-resource-limits/require-cpu-limit` | 28 | 0 | 0 |
| Day 2 | `require-resource-limits/require-memory-limit` | 28 | 0 | 0 |
| Day 2 | `disallow-host-paths/deny-host-path-volumes` | 28 | 0 | 0 |
| Day 3 | (all three) | 28 | 0 | 0 |
| Day 4 | (all three) | 29 | 0 | 0 |
| Day 5 | (all three) | 29 | 0 | 0 |

Pass-row count tracks the prod workload's pod count (29
at cutover-day includes the W20 BlueGreen preview replica
+ the W21 Canary first-step pod). Zero `Fail`/`Error`
rows across all five days — the W22 enforce flip lands
clean.

### 3.2 Cutover commit invariants

The W22 enforce-flip commit MUST be a pure spec-action +
description edit; no rule shape changes. Reviewer
checklist (parallel to W20):

```bash
$ git diff stlong/phase-k-wave-21-bringup..HEAD \
    -- infra/k8s/base/kyverno-policies/require-resource-limits.yaml \
       infra/k8s/base/kyverno-policies/disallow-host-paths.yaml \
  | grep -E '^[+-](\s*)(validationFailureAction|failurePolicy):' \
  | sort -u
-  validationFailureAction: Audit
-  failurePolicy: Ignore
+  validationFailureAction: Enforce
+  failurePolicy: Fail
```

Two lines removed, two lines added per file — clean
flip. The `rules:` block + match/exclude/validate shape
is BYTE-IDENTICAL to W21.

### 3.3 Post-cutover verification (T+30m)

```bash
$ kubectl get clusterpolicy require-resource-limits \
    -o jsonpath='{.spec.validationFailureAction}{"\n"}{.spec.failurePolicy}{"\n"}'
Enforce
Fail

$ kubectl get clusterpolicy disallow-host-paths \
    -o jsonpath='{.spec.validationFailureAction}{"\n"}{.spec.failurePolicy}{"\n"}'
Enforce
Fail

$ kubectl get events --field-selector reason=PolicyViolation -A \
    --sort-by='.lastTimestamp' \
  | tail -n 20
# Expected: empty OR pre-W22 stale rows only. Zero new
# rows in the T+30m window.
```

## 4. Rollback path

Single `git revert <enforce-flip-commit>` restores the
W21 audit-mode posture. No data migration; no
NetworkPolicy / RBAC change; no admission cache to
invalidate. Kyverno re-loads the ClusterPolicy from etcd
within ~10s of the revert merge.

Operator rollback checklist:

1. `git revert <commit>` on `main` (W22 cutover commit).
2. Wait for the standard CI gate to clear (~5 min).
3. Verify the policy is back to Audit:

   ```bash
   $ kubectl get clusterpolicy require-resource-limits \
       -o jsonpath='{.spec.validationFailureAction}'
   Audit
   ```

4. Re-open the audit window for another 5-day cycle if
   the rollback was triggered by a false-positive.

## 5. W19-lineage enforce-flip cadence summary

| Wave | Rules flipped | Audit days | Pre-flip Fail count |
| ---- | ------------- | ---------- | ------------------- |
| W20 | `disallow-lateral-movement` + `require-network-policy` | 5 | 0 |
| W22 | `require-resource-limits` + `disallow-host-paths` | 5 | 0 |
| W24 (planned) | TBD — `require-readonly-rootfs` + `disallow-privileged-containers` | 5 | TBD |

The W19→W20→W22 cadence is now a documented two-wave
enforce-flip discipline:

1. **Wave N**: introduce two new Audit-mode rules.
2. **Wave N+1**: 5-day audit-window collection.
3. **Wave N+2**: flip both to Enforce + Fail in a single
   commit. Title + description annotation updated to
   reflect the new state.

W23+ may introduce a third audit-mode pair following the
same pattern; the W24 enforce-flip line above is the
forward-looking placeholder.

## 6. Cross-references

* [`infra/k8s/base/kyverno-policies/require-resource-limits.yaml`](../infra/k8s/base/kyverno-policies/require-resource-limits.yaml)
  — W22 enforce-mode (was W21 audit).
* [`infra/k8s/base/kyverno-policies/disallow-host-paths.yaml`](../infra/k8s/base/kyverno-policies/disallow-host-paths.yaml)
  — W22 enforce-mode (was W21 audit).
* [`docs/kyverno-w21-additional-rules.md`](./kyverno-w21-additional-rules.md)
  — W21 audit-mode launch + W22 cutover-plan §4.
* [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
  — W19/W20 audit→enforce precedent.
* [`docs/kyverno-enforce-rollout.md`](./kyverno-enforce-rollout.md)
  — repo-wide Kyverno enforce-mode posture overview.
* [`infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml`](../infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml)
  — W19/W20 reference shape (enforce post-W20).
* [`infra/k8s/base/kyverno-policies/require-network-policy.yaml`](../infra/k8s/base/kyverno-policies/require-network-policy.yaml)
  — W19/W20 reference shape (enforce post-W20).

## 7. W22 → W23 hand-off

* Both W21 rules now sit alongside the W19-lineage
  enforce-mode rules; the four-rule enforce posture covers
  lateral-movement + network-policy + resource-limits +
  hostPath-deny — a complete W15-era Pod-security baseline
  for `mahjong-prod`.
* W23 candidate: a new W21+W22 audit-mode pair —
  `require-readonly-rootfs.yaml` (deny
  `securityContext.readOnlyRootFilesystem != true`) +
  `disallow-privileged-containers.yaml` (deny
  `securityContext.privileged: true`). Both invariants
  are already satisfied by the W14 USER 1000 + non-root
  Dockerfile pattern; the audit-mode window will confirm
  cluster-wide compliance.
* The two-wave enforce-flip cadence (Audit at Wave N,
  enforce at Wave N+1 after 5-day grace) is now the
  documented norm — Apone's W23+ Kyverno bring-ups MUST
  follow this shape unless a documented exception exists.
