# Off-peak HPA `minReplicas` override (cron-scheduled)

> Phase K Wave 18 — Apone (DevOps). Companion to:
> [`infra/k8s/base/hpa-cron-override.yaml`](../infra/k8s/base/hpa-cron-override.yaml)
> (the manifest set this runbook covers),
> [`docs/hpa-tuning-w17-retrospective.md`](./hpa-tuning-w17-retrospective.md)
> (the W17 retro that designed this override),
> [`docs/hpa-min-replicas-tuning.md`](./hpa-min-replicas-tuning.md)
> (the W15 30-day Hudson-panel survey that justified the
> static `minReplicas: 3` floor),
> [`infra/k8s/overlays/prod/hpa-patch.yaml`](../infra/k8s/overlays/prod/hpa-patch.yaml)
> (the W16 prod-overlay HPA patch the override temporarily
> relaxes).
>
> Audience: SRE / on-call who runs `kubectl apply -k
> infra/k8s/overlays/prod/` and consumes the Hudson HPA
> panels. The operator does NOT need to manually fire the
> CronJobs — they run on a daily UTC schedule.

## 1. Why this doc exists

Wave 15 + 16's HPA tuning landed a static `minReplicas: 3`
floor across all overlays (see `docs/hpa-min-replicas-tuning.md`
for the 30-day survey). The W17 7-day retrospective
(`docs/hpa-tuning-w17-retrospective.md`) flagged the off-peak
over-provisioning trigger:

> "During the UTC 23:00-07:00 off-peak window, the
>  `cpu-utilization-prod` Hudson panel reads sub-5 % across
>  all three replicas — the `minReplicas: 3` floor over-
>  provisions by ~67 % for 8 hours/day with no in-flight game
>  resilience benefit. A cron-scheduled `minReplicas` override
>  is the W18 fix."

Wave 18 implements that design.

## 2. What the override does

Two daily Kubernetes CronJobs patch the HPA's `minReplicas`
field on the off-peak / on-peak window edges:

| CronJob                          | Schedule (UTC) | minReplicas after fire |
|----------------------------------|----------------|------------------------|
| `hpa-min-replicas-off-peak`      | `0 23 * * *`   | 1                      |
| `hpa-min-replicas-on-peak`       | `0 7 * * *`    | 3                      |

The HPA's `maxReplicas: 12` is UNCHANGED at all times — the
override only relaxes the floor; the ceiling is unchanged so a
sudden off-peak traffic spike (e.g., viral tournament cross-
posted at 02:00 UTC) still gets the same maximum scale-out
headroom that on-peak traffic gets.

Between the 23:00 and 07:00 fires, the HPA's auto-scale loop
holds at `minReplicas: 1`; if CPU/memory dictate, it scales up
to anywhere in `[1, 12]`. Between 07:00 and 23:00, the floor
is `minReplicas: 3`; the loop scales in `[3, 12]`.

## 3. Why CronJob + `kubectl patch hpa` (not KEDA)

The W17 retro enumerated three candidates:

* **Option A — KEDA ScaledObject with `cron` scaler.** Rejected
  for W18 scope: requires installing the KEDA operator
  cluster-wide (new CRDs + RBAC + controller deployment). The
  prod cluster does NOT run KEDA today; the install + RBAC +
  CRD set is a multi-resource surface that exceeds W18 scope.
  KEDA stays a **W19+ candidate** if the off-peak window's
  complexity expands (e.g., per-region overrides, holiday-
  calendar exceptions, multi-window staggering — all out of
  W18 scope).
* **Option B — Kubernetes CronJob + `kubectl patch hpa`.**
  **Picked for W18.** Zero new cluster-scoped dependencies; the
  prod cluster already supports `batch/v1` CronJob. The patch
  shape is a single JSON-Patch `op: replace /spec/minReplicas`.
  Requires a dedicated ServiceAccount + Role + RoleBinding for
  the `patch hpa` verb scoped to the prod namespace.
* **Option C — `kubectl scale --replicas` on the Deployment.**
  Rejected: bypasses the HPA entirely; the HPA's auto-scale
  loop would immediately fight the manual scale, creating an
  oscillation hazard.

## 4. RBAC scope

The `hpa-cron-patcher` ServiceAccount carries a Role with the
narrowest possible verbs:

```yaml
rules:
  - apiGroups: [autoscaling]
    resources: [horizontalpodautoscalers]
    resourceNames: [mahjong-autotable]
    verbs: [get, patch]
```

The `resourceNames` pin closes the blast radius — even if the
SA token leaks, the holder can ONLY patch the single named
HPA, NOT any other HPA in the namespace. No `create`,
`update`, or `delete` verbs are granted.

## 5. Container hardening

Each CronJob runs `bitnami/kubectl:1.31` with:

* `runAsNonRoot: true` + `runAsUser: 1001`
* `allowPrivilegeEscalation: false`
* `readOnlyRootFilesystem: true`
* `capabilities: drop: [ALL]`

The `securityContext` matches the Kyverno enforce-mode floor
landed at W16 (`docs/kyverno-enforce-rollout.md §3` non-root
invariant).

## 6. Failure modes + safety

### 6.1 CronJob failure (kubectl error)

`backoffLimit: 2` retries the patch twice. If both retries
fail, the Job ends in a failed state and the existing Hudson
`cronjob-failures-prod` panel pages the on-call. Meanwhile the
HPA stays at whatever `minReplicas` was last successfully
patched; auto-scale continues to work normally.

### 6.2 Schedule miss (cluster unavailable at fire time)

`startingDeadlineSeconds: 300` — if the CronJob couldn't fire
within 5 minutes of the scheduled time, the missed fire is
skipped (next-window reconciliation). Worst case:

* If the 23:00 UTC off-peak fire is missed, the HPA stays at
  `minReplicas: 3` through the off-peak window —
  **over-provisioned but safe** (this is the W16 + W17 static
  posture).
* If the 07:00 UTC on-peak fire is missed, the HPA stays at
  `minReplicas: 1` through the on-peak window —
  **under-provisioned and dangerous**. The Hudson
  `cpu-utilization-prod` + `replica-count-prod` panels page on
  sustained under-provision so the on-call notices within
  one panel-evaluation cycle (~60s).

### 6.3 Concurrent CronJob runs

`concurrencyPolicy: Forbid` — if a previous run hasn't
completed when the next schedule fires, the new run is
skipped. The patch is a single API call; concurrent execution
is unlikely but the policy closes the race.

### 6.4 Rollback

`git revert <merge-commit>` removes BOTH CronJobs + the RBAC
triplet. The HPA then reverts to its W16 + W17 static
`minReplicas: 3` floor (the value last patched by the CronJob
stays in cluster state until the next manual `kubectl apply`
or HPA reconciliation; the `infra/k8s/overlays/prod/hpa-patch.
yaml` strategic-merge will re-assert `minReplicas: 3` on next
`kubectl apply -k`).

There is NO data migration and NO state drift. The CronJob is
purely a periodic write to the HPA's `spec.minReplicas` —
removing the CronJob removes the writer; the value stays put
until the next `kubectl apply -k` overrides it.

## 7. Verify

### 7.1 Build-time

```bash
kustomize build infra/k8s/overlays/prod/ \
  | yq 'select(.kind == "CronJob") | .metadata.name'
```

Expected output (post-W18):

```
prod-hpa-min-replicas-off-peak
prod-hpa-min-replicas-on-peak
```

(The `prod-` prefix comes from `namePrefix: prod-` in the prod
overlay's `kustomization.yaml`.)

### 7.2 Runtime — observe HPA `minReplicas` flip

Wait until the next 23:00 UTC window:

```bash
kubectl -n mahjong-prod get hpa mahjong-autotable -o yaml \
  | yq .spec.minReplicas
```

Should read `3` before 23:00 UTC; `1` after the 23:00 fire
completes (~60s); `3` again after the 07:00 UTC fire
completes.

### 7.3 Runtime — observe CronJob run history

```bash
kubectl -n mahjong-prod get jobs.batch \
  -l app.kubernetes.io/component=hpa-override \
  --sort-by=.metadata.creationTimestamp \
  | tail -10
```

`successfulJobsHistoryLimit: 3` keeps the last 3 successful
runs visible; `failedJobsHistoryLimit: 3` keeps the last 3
failed runs.

### 7.4 Hudson panel — `hpa-min-replicas-prod`

The W15 Hudson panel set already includes
`hpa-min-replicas-prod`. Post-W18, the panel should show a
sawtooth pattern: 3 (07:00 UTC) → 1 (23:00 UTC) → 3 (07:00
UTC) → ... daily.

A FLAT 3 across an entire 24-hour cycle indicates a CronJob
fire miss (see §6.2). A FLAT 1 across a 24-hour cycle
indicates the on-peak fire is failing (see §6.1; check the
`cronjob-failures-prod` panel).

## 8. Operator runbook — emergency manual override

If the on-call needs to force `minReplicas: 3` immediately
(e.g., unexpected mid-off-peak traffic surge, or the on-peak
CronJob is failing):

```bash
kubectl -n mahjong-prod patch hpa mahjong-autotable \
  --type=json \
  --patch='[{"op":"replace","path":"/spec/minReplicas","value":3}]'
```

The next `kubectl apply -k` will RESET `minReplicas` back to
whatever the prod overlay's HPA patch declares (currently 3 via
`hpa-patch.yaml`), but the W18 CronJob will then re-patch it
to 1 at the next 23:00 UTC fire. To DISABLE the CronJob
override for an extended emergency window:

```bash
kubectl -n mahjong-prod patch cronjob prod-hpa-min-replicas-off-peak \
  --patch='{"spec":{"suspend":true}}'
```

Re-enable after the incident:

```bash
kubectl -n mahjong-prod patch cronjob prod-hpa-min-replicas-off-peak \
  --patch='{"spec":{"suspend":false}}'
```

Document the suspend window in `docs/incidents/` per the
existing incident-runbook convention.

## 9. W18 → W19+ hand-off

* **W19+ candidate — KEDA cutover.** If the cron-override's
  daily-edge complexity grows (per-region overrides, holiday
  calendar, multi-window staggering), revisit Option A. The
  CronJob approach scales linearly in resource count with
  window edges — a 4-window-per-day schedule needs 4 CronJobs
  + 4 ConfigMaps; the KEDA approach folds all 4 windows into a
  single ScaledObject.
* **W19+ candidate — per-region tuning.** If `us-east-1`,
  `us-west-2`, `eu-west-1`, `ap-southeast-1` need different
  off-peak windows (their off-peak windows DON'T align — Asian
  off-peak is European on-peak, etc.), the W18 single-cluster
  shape needs a per-region rework. Defer to W19+ Hicks regional
  cluster lifecycle hand-off (`docs/regional-eks-bringup.md`).
* **W20+ candidate — holiday calendar.** If the user base
  concentrates around Lunar New Year (Hudson's Changsha
  cohort) or other regional holidays, an exception-day
  override may be warranted. The CronJob shape doesn't natively
  support exception days; a `kubectl scale`-equivalent OR a
  switch to KEDA's `cron` scaler is needed. Defer to W20+.

## 10. What W18 does NOT change

* The HPA's `maxReplicas: 12` is unchanged.
* The HPA's CPU/memory utilization targets (70 % / 80 %)
  are unchanged.
* The HPA's scale-up + scale-down stabilization windows
  (60s / 300s) are unchanged.
* The Deployment's `replicas: 3` baseline in the prod overlay
  kustomization patch is unchanged — `replicas:` and
  `minReplicas:` are independent fields; the HPA owns the
  effective replica count once the Deployment has rolled out.
* The base-layer `hpa.yaml` `minReplicas: 3` floor is
  unchanged — the CronJob writes to the LIVE HPA's
  `spec.minReplicas`; the source-of-truth declared floor stays
  at 3.
* The staging overlay is unchanged in effective behaviour;
  staging inherits the CronJobs from base/ but staging's
  traffic profile makes the off-peak relax benign (off-peak
  traffic in staging is already near-zero so the floor relax
  changes nothing observable).
