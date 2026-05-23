# Edge region probes — multi-region prod-health-check

**Status:** Phase K Wave 11 — live.
**Owner:** DevOps lane (Apone) for tooling, on-call SRE for
incident response.
**Pairs with:** [`.github/workflows/prod-health-check.yml`](../.github/workflows/prod-health-check.yml)

---

## 1. Purpose

Through W10 the prod-health-check workflow ran a single
synthetic probe against `https://mahjong.example.com` from
GitHub-hosted runners (effectively a single random
us-east region). That gave us strong signal for **origin
health** but very little signal for **edge/CDN regional
degradation** — a CloudFront point-of-presence outage in
ap-southeast-1 would never surface in the W10 probe.

Wave 11 ships a **4-region matrix**:

| Region              | Probe target var                                | Edge purpose                                         |
|---------------------|-------------------------------------------------|------------------------------------------------------|
| `us-east-1`         | `vars.PROD_BASE_URL_US_EAST_1`                  | Primary origin region; baseline RTT.                 |
| `us-west-2`         | `vars.PROD_BASE_URL_US_WEST_2`                  | DR replica region (W6 DR module).                    |
| `eu-west-1`         | `vars.PROD_BASE_URL_EU_WEST_1`                  | EU CDN POP coverage proxy.                           |
| `ap-southeast-1`    | `vars.PROD_BASE_URL_AP_SOUTHEAST_1`             | APAC CDN POP coverage proxy.                         |

Each matrix leg runs the same `verdict.json` probe shape as
W10; the aggregator job correlates verdicts across regions
and maintains per-region issue state.

## 2. Topology

```
                ┌──────────────────────────────────────────────┐
                │ prod-health-check.yml (15-min cron)          │
                │                                              │
                │   ┌─ matrix: us-east-1     ─┐                │
                │   ├─ matrix: us-west-2     ─┤  parallel x4   │
                │   ├─ matrix: eu-west-1     ─┤                │
                │   └─ matrix: ap-southeast-1 ┘                │
                │                                              │
                │   upload: verdict-<region>.json (artefact)   │
                │                                              │
                │   ┌──────────────────────────────────────┐   │
                │   │ aggregate job (needs: probe matrix)  │   │
                │   │   download all verdicts              │   │
                │   │   parse per-region                   │   │
                │   │   update per-region state-marker     │   │
                │   │   open/close prod-health-check       │   │
                │   │     issue based on strike/recovery   │   │
                │   └──────────────────────────────────────┘   │
                └──────────────────────────────────────────────┘
```

The CloudFront distribution (W7 edge module) routes regionally
via PoP — the same hostname returns different paths-of-edge
servers per region. The matrix doesn't probe four different
hostnames; it probes the **same logical service** from runners
configured to hit region-specific CloudFront endpoints (each
`vars.PROD_BASE_URL_<REGION>` resolves to a regional CloudFront
endpoint via DNS).

## 3. Per-region target resolution

The repo defines four `vars.PROD_BASE_URL_<REGION>` repository
variables. Each may be:

* The **same** root URL (`https://mahjong.example.com`) if the
  operator wants to rely on Anycast / global resolution and
  measure the regional path purely by runner topology.
* A **region-anchored** hostname
  (`https://us-east-1.mahjong.example.com`) if W11+ ships
  region-pinned R53 records (NOT in W11 scope).

The W11 default is **same root URL** — the matrix derives
its regional signal from the GitHub-hosted runners' egress
distribution. The runners themselves don't run in
ap-southeast-1; the probe captures the
US-runner → AP-edge path. That's deliberate: real-world traffic
ALSO crosses runner-to-edge distance, and the synthetic must
mirror it.

If a region's variable is unset, the matrix leg falls back to
a global default
(`https://mahjong.example.com`) and emits a warning step-summary
line. The leg still runs; the operator gets a yellow flag in
the workflow output reminding them to provision the
region-specific URL.

## 4. State-marker decoding

The aggregator job manages the prod-health-check issue body
using **per-region HTML state markers**:

```
<!-- prod-health-check:state region=us-east-1 strikes=0 recoveries=2 -->
<!-- prod-health-check:state region=us-west-2 strikes=0 recoveries=2 -->
<!-- prod-health-check:state region=eu-west-1 strikes=1 recoveries=0 -->
<!-- prod-health-check:state region=ap-southeast-1 strikes=0 recoveries=2 -->
```

Decoding:

* `strikes=N` → N consecutive failed probes (≥ threshold opens
  the issue or keeps it open).
* `recoveries=M` → M consecutive successful probes after a
  strike (≥ threshold closes the issue if ALL regions have
  recovered).

W11 thresholds (configurable in workflow env block):

* `STRIKE_THRESHOLD=3` — three consecutive failures in one
  region to escalate.
* `RECOVERY_THRESHOLD=2` — two consecutive successes in one
  region to clear that region's strike count.

Issue lifecycle:

* **Open** the issue when ANY single region hits
  `strikes >= STRIKE_THRESHOLD`.
* **Keep open** while ANY region still has `strikes > 0` (i.e.
  hasn't yet shown `recoveries >= RECOVERY_THRESHOLD`).
* **Close** when ALL four regions have
  `recoveries >= RECOVERY_THRESHOLD` (or all strikes are 0).

The body is rewritten by the aggregator on every run — the
state markers ARE the source of truth, not the prose
surrounding them.

## 5. Failure-mode playbook

When the prod-health-check issue opens, decode the per-region
markers and pattern-match:

### 5.1 Single region tripped

```
us-east-1:      strikes=3
us-west-2:      strikes=0
eu-west-1:      strikes=0
ap-southeast-1: strikes=0
```

**Likely cause:** Regional CDN POP outage OR
runner-to-edge networking issue specific to that region.
**Action:**
1. Check the AWS Health Dashboard for that region's
   CloudFront/Route 53 services.
2. From outside CI: `curl -sI https://${PROD_BASE_URL}/api/v1/health`
   from a non-GitHub-hosted host (e.g. dev laptop, alternate
   cloud). If it works, the issue is GitHub-runner → that-region
   path, not a customer-facing problem. Annotate the issue
   and let auto-recovery clear it.
3. If the cross-source probe ALSO fails, escalate to P2 and
   page the on-call SRE.

### 5.2 Two adjacent regions tripped

```
us-east-1:      strikes=3
us-west-2:      strikes=3
eu-west-1:      strikes=0
ap-southeast-1: strikes=0
```

**Likely cause:** The origin is degraded; both US regions are
hitting an unhealthy ELB target.
**Action:**
1. Check `mahjong-autotable` ALB target-group health in the
   primary region.
2. Check `RedisIdempotencyStore` connection failure metrics —
   a Redis outage cascades to 5xx on the idempotency-protected
   POST endpoints.
3. Page on-call SRE immediately (P1) — this is customer-
   facing.

### 5.3 All four regions tripped

```
us-east-1:      strikes=3
us-west-2:      strikes=3
eu-west-1:      strikes=3
ap-southeast-1: strikes=3
```

**Likely cause:** Global outage — origin down OR Route 53
problem OR shared dependency failed (DNS, certificate
expiry, the WAF flipped to BLOCK-all by mis-configuration).
**Action:**
1. Confirm with a manual `curl` from your laptop — if you
   ALSO can't reach the service, declare a P1 incident.
2. First-look checklist:
   * Is the ALB target group healthy?
   * Is the WAF rule list emitting BLOCK on legitimate
     traffic? (Check CloudWatch `WAF/BlockedRequests`.)
   * Is the ACM cert renewed? (90-day cycle — expiry day
     is the classic "all probes red, single root cause"
     pattern.)
3. Engage incident commander.

### 5.4 Flapping single region

```
us-east-1:      strikes=2 (and dropping to 0, then back to 2 on next run)
```

**Likely cause:** Threshold-edge latency — the probe times out
just above SLA on some runs and within SLA on others.
**Action:**
1. Don't fight it via the workflow — bump the timeout for
   that probe leg via a per-region env override (W12 hand-off
   candidate).
2. Check CloudWatch `Latency.p99` for the origin in that
   region. If it's drifting up over weeks, treat as a capacity
   problem (RDS / EKS scale-up needed).

## 6. CloudFront edge mapping

The W7 edge module's `aws_cloudfront_distribution.this`
resource includes:

* `price_class = "PriceClass_All"` — all PoPs enabled.
* Origin: the prod ALB.
* Cache behaviors: pass-through for `/api/v1/health` (probe
  endpoint — must NEVER be served from cache).

If a region's strikes are climbing in correlation with a
CloudFront cache-policy change, check the **invalidation log**
in the distribution's `History` tab — a recent cache-policy
update may have stale-served the `/health` endpoint.

Probe path discipline: the workflow probes `${BASE}/api/v1/health`
with `Cache-Control: no-cache` and a unique cache-buster query
param so a single stale-cached response can't lock the issue
into the "open" state.

## 7. Operator runbook — manual probe

To reproduce a matrix leg locally:

```bash
REGION=ap-southeast-1
BASE=$(gh variable get PROD_BASE_URL_AP_SOUTHEAST_1 || echo https://mahjong.example.com)

for i in 1 2 3; do
    code=$(curl -s -o /dev/null -w '%{http_code}' \
        -H 'Cache-Control: no-cache' \
        "${BASE}/api/v1/health?cb=$(date +%s)")
    echo "[$REGION attempt $i] http=$code"
    [ "$code" = "200" ] || sleep 5
done
```

Three consecutive 200s ≡ a recovery; three consecutive non-200s
≡ a strike (matching the workflow's per-region attempt loop).

## 8. Cross-references

* [`.github/workflows/prod-health-check.yml`](../.github/workflows/prod-health-check.yml)
  — the workflow itself.
* [`infra/terraform/modules/edge/README.md`](../infra/terraform/modules/edge/README.md)
  — the CloudFront distribution under probe.
* [`docs/production-deployment-runbook.md`](./production-deployment-runbook.md)
  §8 — Continuous health probes (W10 prose; W11 supersedes
  with the multi-region matrix).
* [`docs/staging-cutover.md`](./staging-cutover.md) — sibling
  staging-side cutover runbook.
* [`docs/retro-2026-08.md`](./retro-2026-08.md) §4 (action
  item 7) — origin commitment to multi-region probe coverage.
