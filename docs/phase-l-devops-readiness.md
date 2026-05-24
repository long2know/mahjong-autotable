# Phase L — DevOps readiness

> Phase K Wave 14 — Apone (DevOps). Phase K is wrapping up.
> This document is the DevOps-lane Phase L pre-plan: surfaces
> that need DevOps investment in Phase L, initial scope per
> surface, expected wave count, and the dependency graph
> between surfaces. The document is INTENDED to evolve through
> Phase K close-out and into Phase L bring-up; it is the
> persistent record of "what's coming" from a platform
> perspective so the rest of the squad can plan downstream
> deliverables.

## 1. Why this doc exists

Phase K's DevOps deliverable list focused on _getting the
runtime to production_ — multi-region readiness, ESO + KMS
secret hydration, image signing chain, JWT rotation cadence,
Redis idempotency cluster, post-cutover hardening playbook.
Phase L's DevOps focus is _expanding the surface_:

* WebRTC TURN cluster scales beyond a single region for the
  Phase L spectator-tier audio improvements.
* Mobile native app CI moves from internal-testing-only to
  TestFlight + Play Console production release rails.
* Multi-region clusters move from DR-ready (Phase K close)
  to active-active load distribution.
* Container scanning shifts from scheduled to PR-blocking
  (shift-left into the PR feedback loop).

Each Phase L surface has Phase K precedent. The scope below
describes the delta from the Phase K baseline, not a from-
scratch design.

## 2. Phase L DevOps surfaces

### 2.1 WebRTC TURN cluster scaling

**Phase K baseline.** A single-region TURN cluster runs in
`infra/k8s/overlays/turn/` (W6+ deliverable; W12 added the
janus-gateway integration per `docs/janus-deployment.md`).
The cluster is sized for the Phase K spectator load
(≤ 1000 concurrent spectators per game; ≤ 100 concurrent
games). The cluster runs in `us-east-1` only; spectators in
other regions connect via the apex ALB and pay the cross-
region RTT penalty (~80–200 ms additional latency for EU /
APAC).

**Phase L delta.** Phase L will introduce spectator-tier audio
+ improved video that pushes the TURN cluster sizing past the
Phase K headroom. Two scaling axes apply:

* **Vertical:** the TURN node sizing (`coturn` pod resource
  limits) needs to grow. Today: 4 vCPU + 8 GiB per node, 6
  nodes per region. Phase L target: 8 vCPU + 16 GiB per node,
  10–12 nodes per region. Backed by the Hudson load-test
  results captured in `docs/load-test-results.md` — the W13
  baseline tops out at ~600 concurrent spectators per node
  under the Phase K audio profile.

* **Horizontal (multi-region).** Spectators in `eu-west-1`
  and `ap-southeast-1` need a regional TURN cluster to avoid
  the cross-region RTT penalty. The regional cluster TF
  state buckets per `docs/regional-eks-bringup.md §2` are
  the prerequisite — once those regions are EKS-ACTIVE, the
  TURN cluster overlay is duplicated per-region with
  region-local STUN/TURN credentials.

**Expected wave count.** 3 waves.

| Wave (target) | Scope                                                                 |
|---------------|-----------------------------------------------------------------------|
| L1            | Vertical scale: 4/8 vCPU pin in `infra/k8s/overlays/turn/` + Hudson load-test re-baseline. |
| L2            | Horizontal scale: per-region TURN overlay (us-west-2 first), regional STUN/TURN credentials in AWS Secrets Manager. |
| L3            | EU + APAC TURN clusters (eu-west-1 + ap-southeast-1), per-region health checks, latency-based STUN/TURN URI resolution in the frontend. |

**Hand-off dependencies.** Hicks's frontend signaling layer
must consume a per-region STUN/TURN URI list (today: single
URI in the omnibus configmap). Bishop's voice-hub Realtime
service must accept a region-tagged TURN credential.

**Decision required:** whether to use a single ICE TURN
server pool with multiple endpoints (simpler, slower
failover) or per-region TURN servers with frontend-side
selection (faster, more config surface). Apone's preference
is per-region; Hicks's input + Hudson's RTT measurements are
the deciding factors.

**Cross-references:**
* `docs/voice-sfu-design.md` — W11 SFU design (Apone) — the
  TURN cluster is the SFU's first-hop.
* `docs/janus-deployment.md` — W12 Janus integration.
* `docs/load-test-results.md` — current TURN cluster sizing
  baseline.

### 2.2 Mobile native app CI — TestFlight + Play Console

**Phase K baseline.** The W11+W12 mobile lane shipped:

* `mobile/` Capacitor wrapper around the autotable frontend.
* `.github/workflows/mobile-build.yml` — iOS + Android
  build artefacts (IPA / AAB) on every PR.
* `.github/workflows/mobile-internal-testing.yml` — uploads
  to Firebase App Distribution (Android) +
  Apple App Store Connect Internal Testing (iOS).
* `.github/workflows/mobile-production-hotfix.yml` /
  `.github/workflows/mobile-production-release.yml` —
  release rails (NOT YET WIRED to the production stores;
  the workflows pass linting but the upload steps
  short-circuit on missing credentials).

**Phase L delta.** Promote the production rails to actually
upload artefacts to TestFlight + Play Console production.
The W11 design notes (per `docs/mobile-release.md §4`)
flagged this as a Phase L item because the production-store
credentials require manual Apple Developer + Google Play
Console enrolment that's gated on Phase K close-out review.

**Phase L scope:**

* Provision the Apple Developer team + App Store Connect API
  key (Apone-driven, but Stephen-approved at enrolment time).
* Provision the Google Play Console publisher key
  (service-account JSON, same approval gate).
* Wire `secrets.APP_STORE_CONNECT_API_KEY` +
  `secrets.PLAY_CONSOLE_JSON_KEY` into the production-rails
  workflows.
* Activate the upload-step short-circuit guards (today:
  `if: false # phase-l-only`); replace with proper
  fail-CLOSED checks on the credential presence.
* Add a `mobile-store-release-rehearsal` workflow modelled
  after `jwt-rotation-rehearsal-scheduled.yml` — quarterly
  upload-to-internal-track rehearsal that exercises the
  store APIs without flipping a public release.

**Expected wave count.** 2 waves.

| Wave (target) | Scope                                                                 |
|---------------|-----------------------------------------------------------------------|
| L1            | Apple + Google enrolment; credential provisioning + secret hydration; production-rails workflow wiring. |
| L2            | Rehearsal workflow + cadence; release tagging convention; mobile changelog automation. |

**Hand-off dependencies.** None on other lanes; the mobile
workflows are self-contained.

**Decision required:** which versioning convention to use
for mobile releases — match the backend SemVer (`0.23.0` →
mobile `0.23.0`) or carry a separate mobile-only counter
(`mobile-1.0.0`). Apone's preference is shared SemVer for
auditability; Hicks's preference is separate counter for
faster mobile-only releases. Resolve in L1 design memo.

**Cross-references:**
* `docs/mobile-release.md` — existing release runbook.
* `mobile/capacitor.config.json` — mobile bundle config.
* `.github/workflows/mobile-*.yml` — existing mobile
  workflows (eight files).

### 2.3 Multi-region active-active

**Phase K baseline.** `docs/regional-eks-bringup.md §2`
listed four regions (us-east-1 primary apex, us-west-2
secondary, eu-west-1 + ap-southeast-1 future). Phase K
closes with us-east-1 + us-west-2 EKS clusters provisioned
and DR-ready: us-west-2 is a warm standby that takes traffic
ONLY when the us-east-1 health check fails.

**Phase L delta.** Move from DR-ready (latency-based RR set
returns nearest healthy region) to active-active load
distribution (all regions receive proportional traffic; the
RDS + Redis tiers are multi-master or per-region with
session-affinity routing).

**Phase L scope (large — likely 4–5 waves):**

* **RDS active-active.** Phase K used a single-region RDS
  with a us-west-2 read replica for DR. Phase L needs:
  * Either an AWS Aurora Global Database (Aurora-only, ~3-5
    second cross-region replication lag) with per-region
    write forwarding, OR
  * A custom session-affinity routing layer that pins each
    game session to a single region (game sessions are
    independent — Bishop's W4 design lets game state live
    in a single region per session).

  Apone's recommendation: session-affinity routing. Aurora
  Global Database is technically active-passive (single
  writer per region per cluster) and the cross-region
  replication lag would surface as game-state drift if a
  session migrated mid-game.

* **Redis active-active.** Phase K's `mahjong-redis-prod`
  cluster is single-region. The W10 design intentionally
  scoped idempotency to per-session; active-active either
  requires per-region Redis clusters with session-affinity
  routing (mirrors the RDS approach) or AWS ElastiCache
  Global Datastore.

* **Frontend region selection.** Hicks's W7 frontend
  consumes a single `prod.mahjong.example.com` apex; Phase L
  needs the frontend to pick a region-stable endpoint
  per game session so the session-affinity routing surfaces
  at the L7 layer (not the L4 ALB layer).

* **Bishop's runtime region awareness.** The hub needs to
  emit a region tag on every event so Hudson's dashboards
  can attribute load per-region.

* **Cutover sequence.** Active-active cutover is a multi-
  step rollout — start with N% traffic per region, watch
  Hudson's region-affinity panels, ramp.

**Expected wave count.** 4–5 waves.

| Wave (target) | Scope                                                                 |
|---------------|-----------------------------------------------------------------------|
| L1            | RDS active-active design memo (Aurora vs session-affinity); decision record. |
| L2            | Session-affinity routing layer in the ALB ingress (or chosen design); us-west-2 acceptance.  |
| L3            | Per-region Redis cluster; idempotency-store region awareness in Bishop's runtime. |
| L4            | Frontend region-stable endpoint resolution; cutover from latency-apex to active-active. |
| L5 (optional) | Eu-west-1 + ap-southeast-1 activation following the Phase K W15+ regional cluster work. |

**Hand-off dependencies.** Heavy. Touches every other lane:
Bishop (runtime region awareness), Hicks (frontend region
endpoint selection), Hudson (per-region dashboards), Vasquez
(per-region E2E suite — today's tests assume a single
region).

**Decision required:** the Aurora-vs-session-affinity choice
is the gating decision. L1 closes it.

**Cross-references:**
* `docs/regional-eks-bringup.md` — Phase K W13 regional
  bring-up readiness.
* `docs/dr-rehearsal.md` — W6 DR rehearsal runbook
  (covers the Phase K warm-standby path that L
  active-active replaces).
* `infra/terraform/modules/dr-replication/` — W6 DR module
  (carry-over target; Phase L L1 design memo decides
  whether to retire this module).

### 2.4 Container scanning shift-left (Trivy in PR vs scheduled)

**Phase K baseline.**
`.github/workflows/container-scan.yml` runs Trivy
nightly + on `release` events. Scan results post to the
Security tab; no PR gate. The W13 retro flagged this as a
deferred shift-left item — the scan SHOULD run on every
PR that touches `Dockerfile` or `mobile/`, with a CVE
severity gate.

**Phase L delta.** Move Trivy into the PR feedback loop
with a configurable severity gate:

* **Block PR on CRITICAL CVEs.** Hard fail.
* **Warn PR on HIGH CVEs.** Comment + check-run warning,
  no merge block (the W14 PWA Builder skip-comment pattern
  is the convention to adopt).
* **Suppress CVEs flagged in a `trivy.ignore` file** with
  per-CVE justification (drift-tracked: Apone owns the
  ignore-file PR review).

**Phase L scope:**

* Refactor `container-scan.yml` to support a `pull_request`
  trigger in addition to the nightly cron.
* Add a `severity-gate` step that parses the Trivy JSON
  output and applies the CRITICAL/HIGH/MEDIUM policy.
* Add a `.trivy.ignore` file at repo root with the W14
  baseline allow-list (verified at Phase L L1 bring-up time).
* Document the operator workflow for adding a new ignore
  entry (PR-with-justification convention).

**Expected wave count.** 1 wave.

| Wave (target) | Scope                                                                 |
|---------------|-----------------------------------------------------------------------|
| L1            | Trivy PR trigger + severity gate + ignore-file convention + docs. |

**Hand-off dependencies.** None directly; cross-references
Bishop's `Dockerfile` (the scan target).

**Decision required:** none — pre-decided by W13 retro.

**Cross-references:**
* `.github/workflows/container-scan.yml` — current scheduled
  workflow.
* `.github/workflows/container-scan-remediation.yml` —
  W12 remediation tracker (re-uses the scheduled scan output).
* `docs/audits/` — quarterly CVE survey reports.

## 3. Cross-surface dependencies

The four surfaces above are NOT independent — there's a
partial-order between them:

```
Phase K close-out (us-east-1 + us-west-2 cluster ACTIVE)
   │
   ├─→ §2.1 TURN scaling L1 (single-region vertical)
   │      │
   │      └─→ §2.1 L2 (us-west-2 horizontal) ──┐
   │                                            │
   ├─→ §2.3 Multi-region active-active L1 (design memo) ─┐
   │                                                     │
   │                                                     ▼
   │                                  §2.3 L2 (session-affinity routing)
   │                                                     │
   │                                                     └─→ §2.1 L3 (eu/apse TURN)
   │                                                                  + §2.3 L4
   │
   ├─→ §2.2 Mobile L1 (store credentials) — INDEPENDENT
   │      │
   │      └─→ §2.2 L2 (rehearsal workflow)
   │
   └─→ §2.4 Container scan L1 — INDEPENDENT
```

Independent surfaces (§2.2 mobile, §2.4 container scan) can
land in parallel with the multi-region work in any order.
The TURN scaling §2.1 depends on the multi-region §2.3
session-affinity work for the L3 horizontal expansion;
§2.1 L1 + L2 can land independently.

## 4. Initial wave count + sequencing

Phase L DevOps preliminary wave estimate: **10–12 waves**
across the four surfaces. Sequencing recommendation:

| Phase L Wave | Surface | Scope |
|--------------|---------|-------|
| L1           | §2.3    | Active-active design memo (Aurora vs session-affinity decision). |
| L1           | §2.4    | Trivy shift-left (independent — can land same wave). |
| L1           | §2.2    | Mobile store enrolment (independent). |
| L2           | §2.1    | TURN vertical scale + load-test re-baseline. |
| L2           | §2.2    | Mobile store production-rails activation. |
| L3           | §2.3    | Session-affinity routing layer. |
| L3           | §2.1    | TURN us-west-2 horizontal. |
| L4           | §2.3    | Per-region Redis + Bishop runtime region awareness. |
| L4           | §2.2    | Mobile rehearsal workflow. |
| L5           | §2.3    | Frontend region-stable endpoint + active-active cutover. |
| L6           | §2.3    | EU + APAC activation (optional — Phase L extension). |
| L6           | §2.1    | EU + APAC TURN clusters (paired with §2.3 L6). |

Three surfaces close by L4–L5; one surface (§2.3 multi-region)
optionally extends to L6 with the regional cluster activation.

## 5. Phase K → Phase L hand-off artifacts

The Phase L bring-up depends on these Phase K artefacts being
in steady-state:

1. **Regional EKS state buckets per `docs/regional-eks-bringup.md`.**
   At Phase K close, us-east-1 + us-west-2 buckets are
   provisioned. EU + APAC buckets are W15+ Phase K work; if
   they slip to Phase L bring-up, §2.3 L1 design memo MUST
   note the dependency.

2. **ESO + KMS per-region trust scoping per W13 §3.1.**
   The Phase L work (§2.3) consumes the ESO sync to hydrate
   per-region Redis credentials.

3. **Bishop's runtime region tag emission.** Phase K W12+ shipped
   the `Hub.Region` tag on every hub event. Phase L
   §2.3 builds on this; if Bishop deprecates the tag in Phase L
   bring-up, §2.3 L3 MUST be the deprecation revert window.

4. **Hudson's per-region dashboard rendering.** W12 shipped
   the per-region dashboard panels (per `docs/observability.md
   §4`). Phase L §2.3 + §2.1 consume these panels — if Hudson is
   not back in scope by Phase L L1, the §2.3 design memo
   should treat dashboard rendering as an implicit dependency
   and schedule the panel work explicitly.

5. **Container signing chain per `docs/image-signing.md`.**
   Phase L §2.4 Trivy shift-left runs against signed
   images. The W12 Kyverno + cosign chain is the Phase L
   baseline; no signing-chain work expected in Phase L.

## 6. Cross-references

* `docs/regional-eks-bringup.md` — Phase K W13 regional
  bring-up readiness (Phase L §2.3 prerequisite).
* `docs/voice-sfu-design.md` — Phase K W11 voice SFU design
  (Phase L §2.1 prerequisite).
* `docs/janus-deployment.md` — Phase K W12 Janus integration.
* `docs/mobile-release.md` — Phase K W10–W12 mobile release
  runbook (Phase L §2.2 prerequisite).
* `docs/prod-cutover.md` — Phase K W12–W14 prod cutover
  (Phase K close-out before Phase L bring-up).
* `docs/load-test-results.md` — Phase K W11+ load-test
  baselines (Phase L §2.1 input).
* `docs/observability.md` — Hudson's dashboard set (Phase L
  §2.1 + §2.3 input).

## 7. Phase K-side hand-offs (W14 close)

These items DO NOT belong in Phase L scope — they are Phase K
close-out work that lands in W15 + W16:

* W15: Kyverno `audit → enforce` flip (per `docs/prod-cutover.md §6.3`).
* W15: HPA min-replicas 3 → 5 bump (per `docs/prod-cutover.md §6.4`).
* W15+: EU + APAC regional cluster provisioning (per
  `docs/regional-eks-bringup.md §5`).
* W16: CSP `report-only=false` flip (per `docs/prod-cutover.md §6.5`).
* W17: Terraform CLI Q1 2027 quarterly bump (1.11.4 → 1.12.x,
  per `docs/terraform.md §6.2`).
* W17+: First scheduled JWT rotation rehearsal fire on
  2027-01-01 02:00 UTC (per `docs/jwt-rotation-rehearsal.md §5.3`).

Phase L bring-up is conditional on Phase K close-out completing
through W17.
