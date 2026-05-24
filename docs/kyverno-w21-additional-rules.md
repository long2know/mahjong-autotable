# Kyverno W21 — additional audit-mode rules

> Phase K Wave 21 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W21 Kyverno
> rule pair (resource-limits + hostPath-deny).
> Companion to [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
> (the W19/W20 audit→enforce pair).

W21 adds **two more audit-mode rules** to the cluster's
Kyverno coverage, mirroring the W19 audit-mode launch +
W20 enforce-flip cadence:

| Rule | File | Subject | Status at W21 | Enforce-flip wave |
| --- | --- | --- | --- | --- |
| `require-resource-limits` | `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` | Pod | Audit | W22 (planned) |
| `disallow-host-paths` | `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml` | Pod | Audit | W22 (planned) |

## 1. What each rule asserts

### 1.1 `require-resource-limits` — `require-cpu-limit` sub-rule

Every container in a `mahjong-prod` Pod MUST declare
`resources.limits.cpu`. Missing limits.cpu lets the
container compete unbounded for CFS shares and burns the
cluster-wide P99 latency budget.

Pattern:

```yaml
spec:
  containers:
    - name: "*"
      resources:
        limits:
          cpu: "?*"
```

`"?*"` is the Kyverno pattern wildcard for "any
non-empty value". A container omitting `resources.limits.cpu`
fails validation.

### 1.2 `require-resource-limits` — `require-memory-limit` sub-rule

Same shape as §1.1 but for `resources.limits.memory`.
Missing limits.memory lets the container OOM the node — the
QoS-class evictor will kill OTHER pods on the same node
before terminating the unbounded offender.

### 1.3 `disallow-host-paths` — `deny-host-path-volumes` sub-rule

No Pod in `mahjong-prod` may declare a `hostPath` volume.
`hostPath` is a classical container-escape + lateral-
movement primitive flagged by CIS-1.6 §5.7.4 + SC-3 +
SOC-2.

Pattern (uses the `X(...)` negation operator):

```yaml
spec:
  =(volumes):
    - X(hostPath): null
```

Semantically: "for all volumes, `.hostPath` MUST NOT be
set". The `=(...)` (conditional) on `volumes` skips the
rule for Pods that declare no volumes at all.

## 2. Why audit-mode first (5-day grace)

Mirrors the W19 → W20 audit→enforce pattern:

1. **W21 land**: rules drop in Audit mode; `failurePolicy:
   Ignore` so webhook outages don't surface spurious
   PolicyReports.
2. **W21 + 5 days grace**: Hudson's `kyverno-deny-events`
   panel collects PolicyReport rows. Existing prod
   workloads that legitimately need an exception surface
   AS reports rather than admission denials.
3. **W22 cutover**: flip `validationFailureAction: Audit →
   Enforce` + `failurePolicy: Ignore → Fail` in a single
   commit. (Same shape as the W20 commit on
   `disallow-lateral-movement.yaml`.)

Pre-W21 verification snapshots (captured by Apone on the
W21 land day):

```bash
$ kubectl -n mahjong-prod get pods -o json \
    | jq '[.items[] | {name: .metadata.name, missing_cpu_limits:
                       [.spec.containers[] |
                          select(.resources.limits.cpu == null) |
                          .name]}]'
# Expected: every entry has missing_cpu_limits = []
# Verified clean as of W21 commit.

$ kubectl -n mahjong-prod get pods -o json \
    | jq '.items[] | .spec.volumes[]? | select(.hostPath)'
# Expected: no output (zero hostPath volumes in prod).
# Verified clean as of W21 commit.
```

Both invariants are clean — the W22 enforce flip will land
without breaking any existing workload.

## 3. Apply order

Out-of-band (NOT in `base/kustomization.yaml` — same shape
as the W19 rules). The operator applies each manifest
directly:

```bash
kubectl apply -f infra/k8s/base/kyverno-policies/require-resource-limits.yaml
kubectl apply -f infra/k8s/base/kyverno-policies/disallow-host-paths.yaml
```

Verification post-apply:

```bash
$ kubectl get clusterpolicy require-resource-limits -o jsonpath='{.spec.validationFailureAction}'
Audit

$ kubectl get clusterpolicy disallow-host-paths -o jsonpath='{.spec.validationFailureAction}'
Audit

$ kubectl get policyreport -A | grep -E 'require-resource-limits|disallow-host-paths'
# Background scan completes within ~30s; expect Pass
# rows for every prod pod.
```

## 4. W22 enforce-flip plan

Cutover-day procedure (same shape as W20):

1. T-24h — final grace-window snapshot:

   ```bash
   kubectl get policyreport -A | grep -E 'require-resource-limits|disallow-host-paths'
   # Capture into .work/apone-w22-evidence/kyverno-pre-enforce.txt
   ```

2. T-0 — flip the two policies via a SINGLE commit:

   ```yaml
   # require-resource-limits.yaml + disallow-host-paths.yaml
   spec:
     validationFailureAction: Enforce
     failurePolicy: Fail
   ```

3. T+30m — verify the W22 enforce posture:

   ```bash
   kubectl get clusterpolicy require-resource-limits -o yaml | grep -E 'validationFailureAction|failurePolicy'
   # validationFailureAction: Enforce
   # failurePolicy: Fail
   ```

4. T+24h — confirm no spurious denials:

   ```bash
   kubectl get events --field-selector reason=PolicyViolation -A
   ```

Rollback path: single `git revert <enforce-flip-commit>`.
The Audit-mode posture is the safe fallback.

## 5. Why TWO new rules (not one combined)

Same rationale as the W19 separation: each rule lives in
its own ClusterPolicy file. Rolling back ONE rule's
enforce-flip becomes a single-file revert. Adding a third
rule in W23+ becomes a clean `git add` of a new file
rather than an in-place edit to a multi-rule policy.

## 6. Cross-references

* [`infra/k8s/base/kyverno-policies/require-resource-limits.yaml`](../infra/k8s/base/kyverno-policies/require-resource-limits.yaml)
  — W21 audit-mode rule (resource-limits required).
* [`infra/k8s/base/kyverno-policies/disallow-host-paths.yaml`](../infra/k8s/base/kyverno-policies/disallow-host-paths.yaml)
  — W21 audit-mode rule (hostPath volumes denied).
* [`infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml`](../infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml)
  — W19/W20 lateral-movement rule (Enforce post-W20).
* [`infra/k8s/base/kyverno-policies/require-network-policy.yaml`](../infra/k8s/base/kyverno-policies/require-network-policy.yaml)
  — W19/W20 NetworkPolicy-presence rule (Enforce post-W20).
* [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
  — W19/W20 runbook (parallel shape).

## 7. W21 → W22 hand-off

* W22 ships the enforce-flip commit (single edit per file).
* W22 candidate: a third W21+W22 audit-mode rule —
  `require-readonly-rootfs.yaml` (deny `securityContext.
  readOnlyRootFilesystem != true`). Currently every prod
  pod already sets this (the W14 USER 1000 + non-root
  Dockerfile pattern); audit-mode confirmation lands
  W22+.
* Consider whether the W22 enforce-flip + the W21 audit
  rules share the same `kyverno-w19-additional-rules.md`
  file or split into a `kyverno-w21-additional-rules.md`
  (THIS file) + `kyverno-w22-enforce.md` pair. Decision
  deferred — Stephen's call.
