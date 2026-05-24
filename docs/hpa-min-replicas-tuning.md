# HPA min-replicas tuning — Phase K Wave 15 pre-flight

> Phase K Wave 15 — Apone (DevOps).

This doc captures the W15 pre-flight survey for the
`HorizontalPodAutoscaler.spec.minReplicas` bump on the prod
overlay. It is the W15 deliverable for the
`docs/prod-cutover.md §6.4` Gate-5 hardening calendar slot.

## 1. What's pinned today (W14 baseline)

`infra/k8s/overlays/prod/kustomization.yaml` patches the base
HPA at the prod overlay:

```yaml
- target:
    kind: HorizontalPodAutoscaler
    name: mahjong-autotable
  patch: |-
    - op: replace
      path: /spec/minReplicas
      value: 3
    - op: replace
      path: /spec/maxReplicas
      value: 12
```

`minReplicas: 3` is the **sticky-session floor** Hudson
recommended at W7 for single-pod-evict resilience: a 3 → 2
evict leaves ~66 % capacity, which absorbs the burst before
the HPA's 30-second cool-down adds a replacement pod. The W14
charter §6.4 flagged the **3 → 5 bump** as the next post-
cutover hardening gate.

## 2. Why bump to 5 (vs 4 or 6)

The Gate-5 justification (per `docs/prod-cutover.md §6.4`):
the burst-resilience window during a single pod evict
improves materially:

| `minReplicas` | Single-pod evict surviving capacity | Two-pod evict surviving capacity |
|---------------|--------------------------------------|----------------------------------|
| 3 (W14)       | 66 %  (3 → 2)                        | 33 %  (3 → 1)  — risky           |
| 4             | 75 %  (4 → 3)                        | 50 %  (4 → 2)                    |
| **5 (W15+ target)** | **80 %  (5 → 4)**              | **60 %  (5 → 3)**                |
| 6             | 83 %  (6 → 5)                        | 67 %  (6 → 4)                    |

The 4-replica midpoint doesn't clear the **two-pod evict**
threshold cleanly (50 % capacity is just barely above the
sticky-session breakage point for a Phase K-sized session
load); 5 clears 60 %, which Hudson's `cpu-saturation-prod`
panel correlates with stable p99 latency. Going to 6 buys
marginal additional headroom (+3 pp) at the cost of an extra
pod's worth of resource quota — not worth it at Phase K
traffic levels.

## 3. Prometheus / Hudson metrics survey (W15 snapshot)

The W15 owner pulled four Hudson panels covering the 30-day
window ending W15 bring-up:

| Panel                                   | 30-day p50 / p99            | Verdict |
|-----------------------------------------|------------------------------|---------|
| `kube-pod-pending` (replicas in Pending state)            | 0 % p50 / 0 % p99 — no scheduler pressure | **PASS** — no quota stall risk on +2 replicas. |
| `cpu-saturation-prod` (per-replica CPU at p99)            | 38 % p50 / 54 % p99 — well under 60 % p99 ceiling | **PASS** — `cpu-saturation-prod < 60 % p99` pre-condition holds. |
| `pod-evicts-prod` (single-pod evict count / day)          | 0.4 / day mean (preemptions during cluster autoscaler scale-down events) | **AT-RISK** — proves the burst-resilience claim is live, not hypothetical. |
| `hpa-current-replicas` (auto-scaled fleet size, average)  | 4.2 / day mean — already above minReplicas=3 most of the day | **DECISION-INFLUENCING** — the average runtime fleet is already at 4–5, so the bump from 3 → 5 increases the floor but doesn't dramatically increase the absolute pod-hours billed (the HPA is already running at 4–5 most of the time). |

The fourth row matters: the bump's **incremental resource
cost** is small because the HPA's own scaling logic already
keeps the fleet at 4–5 replicas during normal traffic.
`minReplicas: 5` mostly affects **scale-DOWN floor** during
low-traffic windows (overnight, weekends) — the floor goes
from 3 to 5, ~+2 replica-hours per night-side window.

**Resource quota impact:** at 250 m CPU request + 384 MiB
memory request per pod (per `kustomization.yaml`), the +2
replicas at the night-side floor adds 500 m CPU + 768 MiB to
the cluster reservation. The `mahjong-prod` namespace's
ResourceQuota allows 4 CPU + 8 GiB; current reservation at the
W14 floor (3 replicas × 250 m + 384 MiB = 750 m + 1152 MiB) is
~19 % CPU + ~14 % memory of the quota. Post-bump (5 replicas)
reservation is ~31 % CPU + ~24 % memory — well clear of the
50 % quota-pressure trigger.

## 4. Pre-flip readiness gate (Gate 5 contract)

The W15 §3 survey confirms the pre-conditions per
`docs/prod-cutover.md §6.4`:

| #  | Pre-condition                                                                  | Status (W15 snapshot) |
|----|--------------------------------------------------------------------------------|------------------------|
| 1  | `kube-pod-pending` 100 % zero for ≥ 30 days                                    | **GREEN**              |
| 2  | `cpu-saturation-prod` < 60 % p99 for ≥ 30 days                                 | **GREEN**              |
| 3  | `pod-evicts-prod` non-zero (proves the burst-resilience claim is live)         | **GREEN** (0.4/day)    |
| 4  | ResourceQuota headroom for +2 replicas (CPU < 50 %, memory < 50 % post-bump)   | **GREEN**              |
| 5  | Hudson + Apone sign-off on the readiness PR                                    | **Pending** — squad review on the W16 cutover-day PR. |

Rows 1-4 are GREEN at W15 bring-up. Row 5 lands when the W16
PR opens.

## 5. The PR-ready change (W15+ landing)

The cutover-day diff against `infra/k8s/overlays/prod/
kustomization.yaml`:

```diff
   - target:
       kind: HorizontalPodAutoscaler
       name: mahjong-autotable
     patch: |-
       - op: replace
         path: /spec/minReplicas
-        value: 3
+        value: 5
       - op: replace
         path: /spec/maxReplicas
         value: 12
```

**One line.** A literal value swap from 3 → 5. The
`maxReplicas: 12` ceiling is unchanged — the W12 capacity
planning analysis already cleared 12 as the cluster-quota
ceiling.

**W15 does NOT land this change.** The W15 deliverable is
the survey + readiness narrative (this document). The
cutover-day PR lands at W16 or later per the Gate-5 hardening
calendar slot, with Hudson + Apone sign-off attached.

## 6. Why this is NOT a pre-wire candidate

The W14 pre-wire-then-toggle pattern (envFrom optional →
required; Kyverno audit → enforce) applies to **flip-a-field-
value** changes where the cutover-day diff is multi-line and
benefits from being split into "land the patch file" + "wire
it up commented-out" + "uncomment".

The HPA min-replicas bump is a **literal value swap** on a
single line. Splitting it into a pre-wire wave gives ZERO
risk reduction (the diff is already trivial). Pre-wiring a
parametrised HPA (e.g. configMap-fed `minReplicas`) would add
complexity without buying anything operational.

**The HPA bump lands as a single-PR cutover-day change at
W16+**, gated on the §4 readiness PR sign-off. This is the
canonical "number-bump" cutover shape; pre-wire is for
"behaviour-flip" cutover shape.

The W14 retro (§4.1 "Pre-wire-then-toggle pattern") explicitly
called out HPA min-replicas as a counter-example for the
pattern.

## 7. Rollback (post-bump)

Single `git revert <merge-commit>` + `kubectl apply -k`. The
HPA controller reconciles to `minReplicas: 3` within one
scaling cycle (~30 s). Running pods at 4–5 replicas continue
to serve traffic; the controller does NOT immediately scale
DOWN unless `cpu-saturation-prod` drops below the scale-down
trigger (50 % p50 sustained for 5 minutes per HPA defaults).

The pod-hour spike from the failed bump is bounded at ~30 s ×
2 extra pods = ~60 pod-seconds of revert cost. Trivial.

## 8. Cross-references

* `docs/prod-cutover.md §6.4` — Gate 5 hardening calendar slot.
* `docs/prod-cutover.md §6.7` — per-gate observability panel mapping.
* `infra/k8s/overlays/prod/kustomization.yaml` — the prod HPA patch.
* `docs/retro-2026-12.md §4.1` — W14 retro's pattern-counter-example call-out.
* `Phase_K_W15/Apone/history.md` — W15 wave history with the survey output.
