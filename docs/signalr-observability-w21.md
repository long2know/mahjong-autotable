# SignalR observability (W21) — connection histogram + churn alerts

> Phase K Wave 21 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W21
> SignalR observability surface.
> Companions:
> [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
> (the W16 sequence-replay SLO contract),
> [`docs/signalr-affinity-hardening-w19.md`](./signalr-affinity-hardening-w19.md)
> (W19 sticky-session cookie + IP-hash fallback).

## 1. What landed at W21

A new prod-overlay PrometheusRule manifest + a runbook
(this file):

| File | Purpose |
| --- | --- |
| `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml` | PrometheusRule with 1 recording rule + 2 alerts |
| `docs/signalr-observability-w21.md` (this file) | Operator runbook |

The PrometheusRule:

1. **Recording rule** — `signalr:churn_rate_5m` derives
   the per-tenant 5-minute connection-loss rate from the
   existing `signalr_connections_active` gauge.
2. **Alert: SignalrChurnHigh** — fires at >10 net
   disconnects/min sustained for 5 minutes.
3. **Alert: SignalrChurnCritical** — fires at >30 net
   disconnects/min sustained for 3 minutes.

Both alerts carry `team: apone` and route to the on-call
DevOps engineer via the W16 Alertmanager team-routing
tree.

## 2. The metric surface

### 2.1 `signalr_connections_active` (gauge)

Exported by the backend (W11 §3.4). Labels:

| Label | Value | Source |
| --- | --- | --- |
| `tenant` | tenant slug (e.g. `tier1-alice`, `tier3-public`) | per-Hub middleware that resolves tenant from the JWT `tnt` claim |
| `pod` | backend pod name | OpenTelemetry automatic resource attribute |
| `hub` | SignalR hub class name (e.g. `ChangshaHub`) | per-hub metric registration |

Reading current values:

```bash
kubectl -n mahjong-prod port-forward svc/prometheus-server 9090:9090 &
curl -s 'http://localhost:9090/api/v1/query?query=signalr_connections_active' | jq
```

The W11 baseline cluster-wide observation was ~1500
connections + ~1.5 net disconnects per minute (background
tab-close + JWT-refresh reconnects).

### 2.2 `signalr:churn_rate_5m` (recording rule, NEW W21)

Derived from `signalr_connections_active`:

```promql
clamp_min(
  -delta(signalr_connections_active[5m]) / 5,
  0
)
```

* `delta(...)` over a 5-minute window captures
  NET-DOWN movement.
* The leading `-` and `clamp_min(...,0)` keep ONLY
  net-loss observations (positive churn).
* Divide by 5 to convert "loss in 5 minutes" to
  "loss per minute".

The recording rule emits a per-tenant series:

```promql
signalr:churn_rate_5m{tenant="tier1-alice"}  → 2.4
signalr:churn_rate_5m{tenant="tier3-public"} → 0.1
```

## 3. Alert design — `>10/min P95` (warning) + `>30/min P95` (critical)

The deliverable spec asks for "P95 churn over a 5-minute
window". The implementation translates this to the
5-minute rolling-average rate (the closest Prometheus-
native operator over the recording rule's window). A true
`histogram_quantile(0.95, ...)` would require a histogram
source metric; `signalr_connections_active` is a gauge,
so the 5-minute rolling-average is the idiomatic
"sustained-breach-not-instant-spike" shape.

The thresholds map to:

| Threshold | Multiplier vs W11 baseline | Severity | Routing |
| --- | --- | --- | --- |
| > 10 disconnects/min (5m sustained) | ~7× | warning | Slack `#alerts-apone` |
| > 30 disconnects/min (3m sustained) | ~20× | critical | PagerDuty (DevOps on-call) + Slack |

## 4. Wiring

Out-of-band — NOT in `base/kustomization.yaml`. The
prod-only overlay applies this via `kubectl apply -f` in
the operator runbook (§3 above). Mirrors the W11
`prometheus-rules-coturn.yaml` wiring pattern.

```bash
kubectl apply -f infra/k8s/overlays/prod/prometheus-rules-signalr.yaml
```

The prometheus-operator picks up the new PrometheusRule
on its next reconcile (typically within 60s). Verify:

```bash
kubectl -n mahjong-prod get prometheusrule signalr-observability
kubectl -n mahjong-prod port-forward svc/prometheus-server 9090:9090 &
curl -s 'http://localhost:9090/api/v1/rules' | jq '.data.groups[] | select(.name | startswith("signalr"))'
```

## 5. Investigation runbook

### 5.1 SignalrChurnHigh response

When PagerDuty Slacks the on-call engineer:

```
[WARN] SignalrChurnHigh — tenant=tier1-alice losing >10 conn/min (5m P95)
```

Investigation steps:

1. Check the tenant's recent activity:

   ```bash
   kubectl -n mahjong-prod logs -l app=mahjong-autotable --tail=200 \
     | grep -i 'tier1-alice'
   ```

2. Check the ingress for affinity-cookie loss (W19 surface):

   ```bash
   kubectl -n ingress-nginx logs -l app.kubernetes.io/name=ingress-nginx --tail=500 \
     | grep -i 'mahjong_aff'
   ```

3. Check the backend pod restart history:

   ```bash
   kubectl -n mahjong-prod get pods -l app=mahjong-autotable -o json \
     | jq '.items[] | {name: .metadata.name, restarts:
                       [.status.containerStatuses[]?.restartCount]}'
   ```

4. If the churn is coming from ONE backend pod restarting,
   the W11 pod-restart-buffer is doing its job. If the
   churn is cluster-wide, escalate to §5.2.

### 5.2 Critical response playbook

When PagerDuty pages the on-call engineer:

```
[CRIT] SignalrChurnCritical — tenant=tier3-public losing >30 conn/min (3m P95)
```

Immediate actions (T+0 to T+5min):

1. **Read the metric** — confirm the value is real:

   ```bash
   curl -s 'http://localhost:9090/api/v1/query?query=signalr:churn_rate_5m' | jq
   ```

2. **Check the SLO error budget burn** — a 30/min churn
   over 5 minutes burns ~3 minutes of the 30-day W16
   budget. The W16 monthly budget is 21.6 minutes; treat
   this as a high-priority incident.

3. **Verify ingress health** — the W19 affinity-cookie
   bypass should kick in on cookie loss; a TRUE outage
   here means BOTH the cookie + the IP-hash fallback have
   failed.

4. **Check backend pod set** — a cluster-wide pod restart
   is the most common cause of cluster-wide churn. The
   W2 HPA + W18 cron-override may be scaling DOWN at an
   inopportune moment.

5. **If all else fails — rollback** — use the W20
   BlueGreen abort path for the backend, OR `kubectl argo
   rollouts abort` for the W21 frontend if a recent
   release is the suspect:

   ```bash
   kubectl argo rollouts abort mahjong-autotable          # backend
   kubectl argo rollouts abort mahjong-autotable-frontend # frontend
   ```

Communications:

* Post in `#alerts-apone` with the incident #.
* Pull Stephen into the channel if the budget burn
  exceeds 5 minutes (i.e. budget-event escalation).

## 6. Cross-references

* [`infra/k8s/overlays/prod/prometheus-rules-signalr.yaml`](../infra/k8s/overlays/prod/prometheus-rules-signalr.yaml)
  — the PrometheusRule manifest documented here.
* [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
  — W16 99.95% sequence-replay SLO contract.
* [`docs/signalr-affinity-hardening-w19.md`](./signalr-affinity-hardening-w19.md)
  — W19 sticky-session cookie + IP-hash fallback.
* [`docs/realtime-resilience.md`](./realtime-resilience.md)
  — W11/W12 realtime backpressure + replay-buffer surface.

## 7. W21 → W22 hand-off

W22 candidate work:

* **Per-tenant absolute threshold dialing** — tier-1
  tenants might page at 5/min while tier-3 tolerates
  20/min. Today the threshold is cluster-wide. Requires
  per-tenant ruling shapes — moderate effort.
* **Histogram source metric** — replace the gauge-derived
  rate with a true histogram of disconnect-events for a
  proper P95. Requires backend instrumentation work
  (Bishop lane).
* **W22 Grafana dashboard** — a Grafana row tile rendering
  `signalr:churn_rate_5m` per tenant alongside the W11
  `signalr_envelope_age_seconds` histogram. Out of scope
  for W21 (the PrometheusRule + alert pair is the W21
  surface; the dashboard work is a follow-up).
