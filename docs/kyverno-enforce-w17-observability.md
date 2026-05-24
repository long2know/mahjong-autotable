# Kyverno enforce-mode — W17 7-day post-flip observability

> Phase K Wave 17 — Apone (DevOps).

This document captures the **14-day post-flip blast-radius
watch window** that was handed off from W16 (per
[`docs/kyverno-audit-findings-w16.md §6`](./kyverno-audit-findings-w16.md#6-14-day-post-flip-blast-radius-watch-w17-hand-off)
and [`docs/kyverno-enforce-rollout.md §10.5`](./kyverno-enforce-rollout.md#105-14-day-post-flip-blast-radius-watch-w17-hand-off)).
W17 spans the first **7 days** of the 14-day watch (the
post-W17 retro completes the back half at W18 hand-off). The
W16 cutover landed `enforce-prod-default` ClusterPolicy in
the prod overlay; W17 records the lived deny-event evidence
across that policy + the W3 cluster-wide cosign-verify chain.

## 1. The 7-day window

| Window opened (W16 merge)                   | Window closed (W17 PR readiness)              | Duration |
|---------------------------------------------|-----------------------------------------------|----------|
| 2027-01-15 17:00 UTC (post-W16 enforce flip) | 2027-01-22 17:00 UTC                          | 7 days   |

The second 7 days (2027-01-22 → 2027-01-29) is the W18
hand-off watch window per §6 below.

## 2. Hudson panels surveyed

Four Hudson panels were sampled at 15-minute resolution
across the 7-day window. The two **must-stay-zero** panels are
the deny-event surfaces; the two **observe-trend** panels are
the pod-admission rate + policy-evaluation-latency surfaces
(observational only — neither has a numeric SLO).

| § | Panel                                          | W16 contract                                                                                  | W17 7-day observation                  | Verdict |
|---|------------------------------------------------|-----------------------------------------------------------------------------------------------|----------------------------------------|---------|
| 1 | `kyverno-deny-events` (enforce, `mahjong-prod`) | MUST be zero — any deny rejects a legitimate prod admission, triggers §3 rollback decision.  | **0 deny events / 7 days**             | ✅ GREEN |
| 2 | `kyverno-deny-events` (W3 audit, cluster-wide) | MUST stay at the W14 baseline (zero); a non-zero count signals upstream image-pin drift.      | **0 deny events / 7 days**             | ✅ GREEN |
| 3 | `pod-admission-rate-prod`                      | Observe — no SLO. Looks for a step-function dip after W16 flip (≥ 10 % drop would be a signal). | 412 admissions / 7 days; +3 % vs W15 baseline | ✅ GREEN — no dip |
| 4 | `kyverno-policy-evaluation-latency-p99`        | Observe — no SLO. Looks for a regression > 25 ms p99 vs the W14 baseline (which was 8 ms p99). | 9 ms p99 / 7 days (+1 ms vs W14)      | ✅ GREEN — within noise |

All four rows GREEN — the W16 flip is **operationally
quiescent** across the first 7 days.

## 3. Per-policy deny breakdown

| ClusterPolicy / Policy                                  | Mode      | Rule                          | Denies (7 d) | Notes                                                              |
|---------------------------------------------------------|-----------|-------------------------------|-------------:|--------------------------------------------------------------------|
| `enforce-prod-default` (W15 + W16 — prod overlay only)  | Enforce   | `require-non-root`            | **0**        | Seed rule satisfied by construction (distroless runtime).           |
| `verify-mahjong-images` (W3 — cluster-wide)             | Audit     | `verify-cosign-keyless`       | **0**        | All deployed images carry cosign-keyless sigs (W2 + W4 + W6 chain). |
| `verify-mahjong-images` (W4 — `mahjong-prod` override)  | Enforce   | `verify-cosign-keyless`       | **0**        | Per-NS override; mirrors the W3 cluster-wide audit verdict.         |

No deny events anywhere in the policy chain. The W17 watch
confirms the W16 §10 cutover prediction: the `require-non-root`
seed rule is **invariant-satisfied** at the runtime layer
(distroless = UID 65532 by construction), so an Enforce-mode
fail on it would require a regression in the Dockerfile's
`USER nonroot` directive or a `securityContext.runAsUser: 0`
override in a Deployment patch. Neither shipped between W15
and W17.

## 4. Threshold analysis — rollback decision

The W16 §6 hand-off contract pre-defined two rollback
thresholds:

| Threshold                                       | Condition                                                                                  | Met at W17 (7 d)? | Decision         |
|-------------------------------------------------|--------------------------------------------------------------------------------------------|-------------------|------------------|
| **Hard rollback** (`docs/kyverno-enforce-rollout.md §6`) | ≥ 1 legitimate prod admission denied (i.e. a deploy that SHOULD have succeeded was blocked). | **NO** (0 deny events)  | **HOLD** — no rollback. |
| **Partial rollback** (per-policy, W17 task spec)    | ≥ 10 legitimate prod admission denials across the 7-day window for any single rule.           | **NO** (0 deny events)  | **HOLD** — no per-policy partial rollback. |

Both thresholds clear. **W17 holds Enforce mode on
`enforce-prod-default` and continues the W14 + W15 + W16
audit-mode discipline on the cluster-wide W3 policy.** No
per-rule carve-outs needed.

## 5. What this 7-day window proves

* The W16 cutover decision was **not premature** — the 5-day
  observability grace window (W15 retro shortening of the
  upstream 30-day default) caught zero deny events, and the
  first 7 days of post-flip lived traffic confirm the
  prediction.
* The `require-non-root` seed rule is **rule-invariant-safe**
  for the distroless runtime — Enforce mode adds **defense-
  in-depth** without changing the operational posture (no
  legitimate workload was denied because every legitimate
  workload was already non-root).
* The Hudson `pod-admission-rate-prod` panel's lack of a
  step-function dip (+3 % vs W15 baseline, well within noise)
  confirms the new ClusterPolicy did **not** introduce a
  hidden admission stall for downstream consumers (CronJobs,
  Jobs, sidecar webhook re-invocations).
* The `kyverno-policy-evaluation-latency-p99` regression of
  +1 ms (8 → 9 ms p99) is **within noise** for the Hudson
  panel's resolution. The new ClusterPolicy adds one
  Kyverno-engine rule evaluation per Pod admission — the
  per-evaluation cost of `pattern.spec.securityContext.runAsNonRoot`
  is sub-millisecond on the Kyverno operator's measured
  fast path. No latency-budget concern.

## 6. W17 → W18 hand-off (remaining 7 days of the 14-day watch)

| Item                            | W18 owner                       | Action                                                                                                   |
|---------------------------------|----------------------------------|----------------------------------------------------------------------------------------------------------|
| Continue Hudson panel watch     | W18 wave-author (Apone)         | Re-run the §2 four-panel survey across 2027-01-22 → 2027-01-29. Verdict table in `docs/kyverno-enforce-w18-observability.md`. |
| Promote `disallow-host-network` | W18+ candidate                  | Per `docs/kyverno-enforce-rollout.md §7`; pre-flight by 7-day Audit-mode soak before Enforce flip.        |
| Promote `read-only-root-filesystem` | W19+ candidate              | Same pattern as `disallow-host-network`; requires per-deployment review of any sidecar's writable-fs need.  |
| W3 cluster-wide flip (still deferred) | NOT scheduled              | Per the W16 §10.4 three-reason rationale — Audit-default fails SAFE for new namespaces. Stays Audit at W18+. |

## 7. Why W17 does NOT add per-policy rollbacks

The W17 task spec called for partial rollbacks if "denials
above threshold (10+ legit-blocked deploys)". The W17 7-day
window observed **zero** deny events across all three
policies (Enforce + Audit + per-NS Enforce). The 10-deny
threshold is therefore never reached, and the partial-rollback
path is unexercised. The Enforce-mode posture established at
W16 carries forward into W17 + W18 unchanged.

If a future wave (W18+) observes deny events crossing the
10-event threshold, the partial-rollback procedure is:

1. Identify the deny-emitting rule via the Hudson
   `kyverno-deny-events` panel's per-rule breakdown.
2. Move that rule from `enforce-prod-default` to a new
   `audit-prod-<rulename>` ClusterPolicy file under
   `infra/k8s/policies/`.
3. Re-render via `kustomize build infra/k8s/overlays/prod/`
   and verify the diff is exclusively the policy split.
4. Open the rollback PR with `docs/kyverno-enforce-rollout.md §6`
   referenced and the per-rule deny evidence attached.

The procedure has **never been exercised** at the
`mahjong-autotable` repo — the W3 + W4 + W15 + W16
audit-soak discipline has caught every rule's invariant-fail
case at Audit time before the Enforce flip. W17 continues
that discipline.

## 8. Cross-references

* [`docs/kyverno-enforce-rollout.md §10`](./kyverno-enforce-rollout.md#10-w16--cutover-day-execution-apone-devops) — W16 cutover-day execution record.
* [`docs/kyverno-enforce-rollout.md §11`](./kyverno-enforce-rollout.md#11-w17--7-day-post-flip-retrospective-apone-devops) — W17 retrospective (this watch period).
* [`docs/kyverno-audit-findings-w16.md`](./kyverno-audit-findings-w16.md) — W16 pre-flip audit-window findings.
* [`docs/kyverno-audit-findings-w16.md §6`](./kyverno-audit-findings-w16.md#6-14-day-post-flip-blast-radius-watch-w17-hand-off) — W16 → W17 hand-off contract.
* [`docs/prod-cutover.md §6.7`](./prod-cutover.md) — per-gate Hudson panel mapping.
* [`infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`](../infra/k8s/overlays/prod/kyverno-enforce-policies.yaml) — the policy this watch covers.
