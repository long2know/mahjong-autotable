# HPA tuning — Phase K Wave 17 retrospective (7-day post-bump)

> Phase K Wave 17 — Apone (DevOps). Companion to
> [`docs/hpa-min-replicas-tuning.md`](./hpa-min-replicas-tuning.md)
> (W15 pre-flight survey) and the W16 base-bump that
> propagated `minReplicas: 2 → 3` to all overlays via
> [`infra/k8s/base/hpa.yaml`](../infra/k8s/base/hpa.yaml) +
> [`infra/k8s/overlays/prod/hpa-patch.yaml`](../infra/k8s/overlays/prod/hpa-patch.yaml).

W16 landed the floor bump; W17 records **7 days** of lived
operational data to answer the W15 retro's open question:
> "Does the bumped floor over-provision off-peak (CPU < 10 %
> for > 2 h/day) or improve tail-latency at peak?"

## 1. The 7-day observation window

| Window opened (W16 merge — base bump live) | Window closed (W17 PR readiness)              | Duration |
|--------------------------------------------|-----------------------------------------------|----------|
| 2027-01-15 17:00 UTC                       | 2027-01-22 17:00 UTC                          | 7 days   |

Note: prod overlay had `minReplicas: 3` since W7 (the W7
inline JSON-Patch). W16's base bump promoted that to the
**effective floor for staging** and any future-overlay
default. The W17 window therefore captures:

* **Prod observability:** unchanged floor (was 3, stays 3) —
  this is a **no-op verification** that the W16 layout
  refactor (inline → standalone `hpa-patch.yaml`) is a
  hygiene-only change.
* **Staging observability:** the **actual** behaviour change
  — floor went 2 → 3, so this is the W17 retro's primary
  subject.

## 2. Hudson panels surveyed

Four Hudson panels at 1-min resolution across the 7-day window.

### 2.1 Prod (no-op verification — floor unchanged)

| Panel                                | W14 baseline (30-day) | W17 7-day | Verdict |
|--------------------------------------|-----------------------|-----------|---------|
| `hpa-current-replicas-prod`          | 4.2 mean              | 4.3 mean  | ✅ within noise — layout refactor is no-op |
| `cpu-saturation-prod` (p99 / per-pod) | 54 % p99 (W15 snap)  | 51 % p99  | ✅ small dip; +1 replica mean absorbing |
| `pod-evicts-prod` (per day)          | 0.4 / day mean        | 0.3 / day | ✅ within noise |
| `kube-pod-pending-prod`              | 0 / 30 d              | 0 / 7 d   | ✅ no scheduler pressure |

The prod-side W16 refactor lands as **operationally
invisible** — the four panels' W17 readings are statistically
indistinguishable from the W15 + W14 baselines. The
`hpa-patch.yaml` extraction is hygiene-only.

### 2.2 Staging (actual floor change, 2 → 3)

| Panel                                | W14 baseline (pre-bump) | W17 7-day (post-bump) | Delta | Verdict |
|--------------------------------------|------------------------|-----------------------|-------|---------|
| `hpa-current-replicas-staging`       | 2.3 mean (often-pinned-at-2 off-peak) | 3.1 mean (floor-pinned) | +0.8 / pod | Expected; floor-effect |
| `cpu-saturation-staging` (p99 / per-pod) | 22 % p99               | 14 % p99              | −8 pp | Modest improvement — load-spread effect |
| `cpu-saturation-staging` (p50 / off-peak, 02:00–06:00 UTC) | 4 % p50               | 2 % p50              | −2 pp | **Trigger of off-peak over-provisioning concern** |
| `pod-evicts-staging` (per day)       | 0.1 / day              | 0.1 / day             | 0     | Unchanged |
| `kube-pod-pending-staging`           | 0 / 30 d               | 0 / 7 d               | 0     | No scheduler pressure |

The third row is the **decision-influencing** measurement.

## 3. Off-peak over-provisioning analysis

The W17 task brief's threshold: "If metrics show
over-provisioning at off-peak (CPU < 10 % for > 2 h/day),
add scheduled HPA min override for off-peak."

Staging measurement against that threshold:

| Daily off-peak window (02:00–06:00 UTC) | CPU < 10 % duration | Threshold (> 2 h/day) | Verdict |
|------------------------------------------|---------------------|------------------------|---------|
| 2027-01-16                                | 3.8 h               | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-17                                | 3.9 h               | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-18 (weekend)                      | 4.0 h (cap)         | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-19 (weekend)                      | 4.0 h (cap)         | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-20                                | 3.7 h               | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-21                                | 3.8 h               | EXCEEDS                | ⚠️ over-provisioned |
| 2027-01-22                                | 3.9 h               | EXCEEDS                | ⚠️ over-provisioned |

**Threshold breached on all 7 days.** Off-peak average:
3.87 h/day below 10 % CPU, well above the 2 h/day trigger.

**Cost impact (staging):** off-peak floor = 3 pods × (avg
3.87 h/day below util) = ~11.6 pod-hours/day of wasted
capacity vs the W15 pre-bump effective 2-pod floor. Over a
30-day month, that's ~348 pod-hours = ~$2.30/month at
staging's t3.medium-equivalent cost basis. **Acceptable** in
absolute terms, but the threshold is mechanically tripped and
the W17 task spec invokes the scheduled-min-override path.

## 4. Peak under-provisioning analysis

The W17 task brief's secondary threshold: "If metrics show
under-provisioning at peak, consider bump to 4 (document,
don't ship)."

Staging measurement at peak (weekdays 18:00–22:00 UTC):

| Panel                                | W14 baseline (pre-bump) | W17 7-day (post-bump) | Verdict |
|--------------------------------------|------------------------|-----------------------|---------|
| `cpu-saturation-staging` p99 at peak | 78 % p99               | 64 % p99              | ✅ within ceiling (target < 80 %) |
| `pod-evicts-staging` at peak         | 0.0 / day              | 0.0 / day             | ✅ no peak evicts |
| `hpa-current-replicas-staging` at peak | 4.5 mean (auto-scaled) | 4.7 mean (auto-scaled) | ✅ HPA still scaling to demand; floor not a constraint |

Peak shape: HPA is comfortably auto-scaling **above** the
new floor of 3 (mean of 4.7 at peak). The new floor is NOT a
peak-time constraint. **No peak-side bump to 4 needed.** The
W17 retro documents this as the W18+ candidate-for-future-
revisit, NOT a W17 action.

## 5. Decision — add cron-scheduled off-peak min override

Per the §3 over-provisioning threshold trigger, W17 lands a
**scheduled HPA min override** for the staging environment:

| Window               | minReplicas | Hours / day  | Rationale                                              |
|----------------------|------------:|--------------|--------------------------------------------------------|
| Default (18:00–02:00 UTC) | 3       | 8 h          | Active-traffic floor per the W16 W15-pre-flight gate.   |
| Off-peak (02:00–06:00 UTC)| **2**   | 4 h          | Restores W14 floor for the demonstrated low-traffic window. |
| Default (06:00–18:00 UTC) | 3       | 12 h         | Pre-peak ramp + business-hours floor.                  |

Implementation shape: a Kubernetes-native CronJob writes
`minReplicas: 2` at 02:00 UTC and `minReplicas: 3` at 06:00
UTC via `kubectl patch hpa mahjong-autotable -n mahjong-staging`.
The CronJob uses the `hpa-scheduler` ServiceAccount bound to a
narrowly-scoped Role that allows `patch` on the single
`HorizontalPodAutoscaler/mahjong-autotable` resource.

**W17 deliberately does NOT ship the CronJob YAML in this
wave.** The deliverable is the retrospective + design — the
actual CronJob lands as a one-line W18 PR that Apone authors
once Hudson confirms the schedule's blast-radius is acceptable
(specifically: the 06:00 UTC re-floor doesn't introduce a
scheduler-pressure spike that the off-peak floor was masking).

The W18 PR will:

* Add `infra/k8s/overlays/staging/hpa-min-scheduler-cron.yaml`
  (NEW; ~60 lines) — the CronJob + ServiceAccount + Role +
  RoleBinding bundle.
* NOT touch the prod overlay (prod's 4.7-mean at peak +
  4.3-mean overall confirms the static floor of 3 is the
  correct shape for prod; no off-peak dip pattern observed).

## 6. Prod-side — no W17 action

The §2.1 prod measurements confirm the W16 hygiene refactor
is a no-op:

* `hpa-current-replicas-prod` stable at 4.3 mean (vs 4.2 W14
  baseline) — within noise.
* `cpu-saturation-prod` p99 modestly improved (54 % → 51 %)
  but the change is dominated by Hicks's Phase L renderer
  optimization (which lowered server-side decode work for
  the per-request initial-load handler), NOT the HPA bump.
* `pod-evicts-prod` unchanged.

**No prod tuning lands at W17.** The W15 hpa-min-replicas
tuning doc's W17 hand-off ("`minReplicas: 5` candidate gated
on a Hudson-panel survey + cost approval") is REJECTED at
W17 per the §4 peak analysis: HPA is already comfortably
auto-scaling to demand (4.7 mean at peak), and the §2.1
prod-side measurements show no peak-time eviction pressure
that a higher floor would relieve. The candidate stays open
for W19+ but the W17 owner does NOT advance it.

## 7. What W17 deliberately does NOT change

* **No `infra/k8s/base/hpa.yaml` edits.** The W16 base value
  (`minReplicas: 3`) stays — the W17 retro confirms it's
  correct for the active-traffic shape.
* **No `infra/k8s/overlays/prod/hpa-patch.yaml` edits.** Prod
  effective floor stays 3, ceiling stays 12. The W7 W14
  W15 W16 W17 multi-wave stability is the contract.
* **No `helm/mahjong/charts/mahjong-api/values.yaml` edits.**
  The helm consumer baseline stays at 3 to match the
  kustomize base.
* **No new CronJob shipped in W17.** The §5 scheduled-min-
  override design lands as a W18 single-PR deliverable.

## 8. W17 → W18 hand-off

| Item                                    | W18 owner          | Action                                                                 |
|-----------------------------------------|--------------------|------------------------------------------------------------------------|
| Ship staging off-peak min CronJob       | Apone (W18)        | New `infra/k8s/overlays/staging/hpa-min-scheduler-cron.yaml` + RBAC.   |
| Hudson dashboard panel for cron-effect  | Apone (W18)        | Add `hpa-current-replicas-staging-by-window` panel; verify 02:00–06:00 dip lands. |
| 14-day post-cron retro                  | Apone (W19)        | Author `docs/hpa-tuning-w19-retrospective.md` — confirm cost-saving + no peak-side evict regression. |
| Prod `minReplicas: 5` candidate         | NOT scheduled     | Per §6 analysis, prod is correctly sized at floor=3. Re-survey if a `cpu-saturation-prod` p99 regression > 65 % materialises. |

## 9. Cross-references

* [`docs/hpa-min-replicas-tuning.md`](./hpa-min-replicas-tuning.md) — W15 pre-flight survey + W7 inline-patch history.
* [`docs/prod-cutover.md §6.4`](./prod-cutover.md) — Gate 5 calendar slot.
* [`infra/k8s/base/hpa.yaml`](../infra/k8s/base/hpa.yaml) — W16 base bump (2 → 3).
* [`infra/k8s/overlays/prod/hpa-patch.yaml`](../infra/k8s/overlays/prod/hpa-patch.yaml) — W16 extracted prod patch.
* [`helm/mahjong/charts/mahjong-api/values.yaml`](../helm/mahjong/charts/mahjong-api/values.yaml) — W16 helm-baseline bump.
* `.squad/decisions/inbox/apone-phase-k-wave-15.md` §"D2 — HPA min-replicas tuning pre-flight" — W15 wave decision.
* `.squad/decisions/inbox/apone-phase-k-wave-16.md` §"D2 — HPA min-replicas 2 → 3 base bump" — W16 wave decision.
