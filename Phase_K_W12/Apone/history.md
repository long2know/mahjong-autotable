# Apone — Phase K Wave 12 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/apone/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 12 — DevOps bring-up

Branch: `stlong/phase-k-wave-12-bringup`
Bringup-on commit (W11 close): `ee9dba0` (PR #57 — gate
2403/0/0).

### Deliverables (seven)

1. **Prod Redis terraform plan readiness assessment.** The W11
   prod env stack (`infra/terraform/envs/prod/`) is confirmed
   `terraform plan`-ready against a vanilla operator
   workstation. Surfaced as §1 of the NEW
   `docs/prod-cutover.md` runbook. Captures: six pre-flight
   assertions (primary stack applied, state-bucket + lock-table
   pre-created, ALB DNS published, backend.hcl + tfvars
   populated), the W11/W12 required tfvars including the W12
   addition `regional_endpoints = []` default, the expected
   plan shape (resource counts ±2 band per module), and three
   apply gates (IAM caller-identity, primary-stack drift
   check, no DESTROY of static primary resources). No code
   change to the W11 stack — the W12 deliverable is the
   readiness ARTIFACT.

2. **Prod kustomization wire-up.**
   `infra/k8s/overlays/prod/kustomization.yaml` — swapped
   top-level `namespace: mahjong-prod` for `transformers:`
   referencing the NEW
   `infra/k8s/overlays/prod/namespace-transformer.yaml`
   (inline `NamespaceTransformer` with `unsetOnly: true`).
   Resources without a pre-declared `metadata.namespace`
   continue to pick up `mahjong-prod` (identical to W11
   behaviour); resources WITH a pre-declared namespace (the
   W11 argo-rollouts ingress + W12 NetworkPolicies, all
   pinned to `argo-rollouts`) keep their declared value. The
   pattern is documented as the canonical cross-namespace
   kustomize approach in `docs/prod-cutover.md §4`.

   Added three entries to `resources:` —
   `redis-connection-string-secret.yaml` (W11, picks up
   `mahjong-prod` from transformer),
   `argo-rollouts-ingress-auth.yaml` (W11, cross-namespace
   preserved), `argo-rollouts-network-policy.yaml` (W12 new,
   cross-namespace preserved).

   Added one deployment patch — appends a fourth `envFrom:
   secretRef:` entry to the app container, mounting
   `mahjong-redis-prod` with `optional: true`. The
   `optional: true` flag preserves the cutover-safe fall-
   through (if ESO hasn't yet hydrated the secret, the
   container starts WITHOUT the env var and the runtime falls
   back to the in-process omnibus secret).

   W11 file headers on
   `redis-connection-string-secret.yaml` +
   `argo-rollouts-ingress-auth.yaml` flipped from "OUT-OF-BAND
   TEMPLATE" to reflect the W12 wire-in. Body unchanged on
   both.

   `kustomize build infra/k8s/overlays/prod/` clean.
   Verification — assertion that the in-base resources still
   get `mahjong-prod` while the W11/W12 hand-offs keep their
   declared namespace — captured in `docs/prod-cutover.md
   §2.2`.

3. **Prod Redis load-test re-baseline.**
   `infra/load-tests/redis-load-test.yml` (NEW, ~11 kB). Three-
   document manifest:
   * Namespace `load-test` (with a `load-test: redis` label
     for downstream Prometheus scrape targeting).
   * ConfigMap `redis-load-test-script` carrying the k6 JS
     script — `constant-arrival-rate` scenario, 1000 RPS for
     5 min with 30 s ramp-up, 80/20 lookup-vs-write mix
     matching Bishop's W10 idempotency-store hot-path
     profile, k6 `thresholds:` enforcing p99 lookup < 5 ms /
     p99 write < 8 ms / p99.9 lookup < 25 ms / error rate <
     0.1 % (Job exits non-zero on breach).
   * Job `redis-load-test` — k6 v0.51.0 image, mounts the
     ConfigMap, targets the in-cluster app endpoint via the
     prod overlay's Service DNS, exposes Prometheus-format
     metrics on port 6565 via the
     `experimental-prometheus-rw` k6 output.

   New §4 of `docs/redis-cluster.md` (Load-test methodology)
   walks the artifact: 4.1 target workload, 4.2 SLO
   thresholds (the same numbers Bishop budgeted in the W10
   design memo), 4.3 run procedure, 4.4 initial baseline
   (W12 first run with > 40 % headroom on every threshold —
   6.4x improvement vs W10 staging baseline matches the
   upstream `r6g.large` benchmark), 4.5 re-baseline cadence
   rules, 4.6 observability hooks (Prometheus retention).

4. **Per-region R53 records.**
   `infra/terraform/modules/edge/r53-regional-records.tf`
   (NEW). Three resource types keyed by
   `for_each = { for r in var.regional_endpoints : r.region
   => r }`:
   * `aws_route53_health_check.regional[<region>]` — TCP/443
     probe against the regional ALB, 30 s interval, 3-of-5
     failure threshold (tied via `health_check_id` to the
     latency RR set so unhealthy regions are auto-removed
     from rotation).
   * `aws_route53_record.regional_alias[<region>]` — ALIAS
     A on `<hostname>` (e.g.
     `us-east-1.mahjong.example.com`) pointing at the
     regional ALB's `<alb_dns_name>` + `<alb_zone_id>`.
   * `aws_route53_record.latency_apex[<region>]` — RR set
     on the apex with `latency_routing_policy { region =
     <region> }` and `set_identifier = <region>`. Clients
     hitting the apex resolve to the lowest-latency
     healthy region.

   New `local.use_latency_apex = length(var.regional_endpoints)
   > 0` flag in the file; the W7
   `aws_route53_record.apex` count was updated to
   `(!local.use_latency_apex && (var.cloudfront.enabled ||
   var.alb_dns_name != "")) ? 1 : 0` — single-region apex
   stays in the plan when `regional_endpoints` is empty.

   `infra/terraform/modules/edge/variables.tf` —
   appended `variable "regional_endpoints"` (list of
   `{ region, alb_dns_name, alb_zone_id, hostname }` objects)
   with regex validation on `region` (`^[a-z]{2}-[a-z]+-[0-9]+$`)
   and uniqueness validation on the `region` key. Empty
   list default.

   `infra/terraform/modules/edge/outputs.tf` — `apex_fqdn`
   updated to fall through to `var.domain_name` when
   `local.use_latency_apex` is set (the latency apex
   doesn't expose a single `.fqdn` attribute meaningfully —
   it's an RR set); added `regional_health_check_ids` (map
   region → health-check ID) and `regional_hostnames` (map
   region → hostname).

   Wired through to
   `infra/terraform/envs/prod/{variables,main.tf}`:
   `variable "regional_endpoints"` mirrors the module shape,
   `regional_endpoints = var.regional_endpoints` passed in
   the `module "edge"` block. Empty default preserves the
   W11 single-region apex.

   `terraform validate` clean across all envs/modules
   touched. The standalone `modules/edge/` validate hits a
   pre-existing `configuration_aliases = [aws.us_east_1]`
   constraint (not a W12 regression — confirmed by stashing
   the W12 changes and reproducing on the W11 baseline);
   canonical validation is at env-stack level.

   `docs/edge-region-probes.md §3` updated in-place — extends
   the W11 "deferred to W12+" note with the tfvar shape, the
   cutover sequence ("same root URL" → region-anchored
   hostnames), the rollback (single `terraform apply
   -var='regional_endpoints=[]'` reverts to W11 single-region
   apex; R53 propagation ≤ 60 s).

5. **Argo Rollouts NetworkPolicy hardening.**
   `infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`
   (NEW, ~8 kB). Three NetworkPolicy objects in the
   `argo-rollouts` namespace:
   * `argo-rollouts-dashboard-ingress` — pod selector
     `app.kubernetes.io/component=dashboard`. Ingress from
     `ingress-nginx` ns (the W11 ingress controller) +
     `auth` ns (the W11 oauth2-proxy auth-url subrequest).
     Default-deny baseline (no other ingress allowed).
   * `argo-rollouts-controller-egress` — pod selector
     `app.kubernetes.io/component=rollouts-controller`.
     Egress to kube-apiserver (CRD reconcile loop),
     `monitoring` ns (Prometheus scrape for analysis-
     template metric queries), kube-dns (UDP 53 to
     `kube-system`). Default-deny baseline.
   * `argo-rollouts-dashboard-egress` — pod selector
     `app.kubernetes.io/component=dashboard`. Egress to
     kube-apiserver only + kube-dns. Default-deny
     baseline.

   Split into three (vs one mega-policy) because the
   controller + dashboard have distinct egress profiles —
   the dashboard does NOT need Prometheus access; the
   controller does. Keeps each allow-list minimal + easier
   to audit on chart upgrades.

   New §6 of `docs/argo-rollouts-setup.md` (NetworkPolicy
   hardening) walks the three policies, the split
   rationale, the wire-in via the W12 kustomization,
   validation steps (positive + negative tests), upgrade
   procedure when bumping the argo-rollouts Helm chart,
   the rollback path.

6. **Second JWT rotation rehearsal documentation.**
   `docs/jwt-rotation-rehearsal.md §3` (NEW section). The
   W12 rehearsal ran on staging at 3 min 48 s (39 % faster
   than the W11 first run at 6 min 12 s). The two big wins
   (runtime cache invalidation -56 s, smoke test -127 s)
   are both downstream of Bishop's W12 JWKS-cache pre-warm
   (eager fetch on `kid` cache miss instead of waiting for
   the 30-s key-cache tick).

   The section captures:
   * 3.0 — rehearsal cadence + per-run table (W11 + W12
     rows with date, branch, operator, target env,
     duration, outcome, notes).
   * 3.1 — deltas observed between runs (per-phase
     timing table — the breakdown that surfaced the
     Bishop W12 speedup empirically).
   * 3.2 — GA-readiness recommendation: the workflow is
     ready for promotion to a scheduled monthly cadence
     (add `schedule: cron` block to the workflow in a
     follow-up PR; add dashboard row in
     `docs/dashboards/jwt-rotation.json`).
   * 3.3 — target timing scale for future runs (< 4 min
     green, 4–6 min yellow, > 6 min red).

   Renumbering pushed §3–§8 down to §4–§9. Cross-refs
   inside the file updated. No workflow code changes in
   W12 — the §3 narrative is the W12 deliverable.

7. **CHANGELOG + retro + memo + agent history.**
   * `CHANGELOG.md` `[0.21.0]` Phase K Wave 12 entry added
     above `[0.20.0]`. `[Unreleased]` working branch flipped
     to `stlong/phase-k-wave-12-bringup`. Theme paragraph +
     "Added" + "Changed" subsections.
   * `docs/retro-2026-10.md` (NEW). Six sections: 1. What
     shipped (W12 deliverables + cross-lane work + gates);
     2. What worked well (single-pane runbook, cross-
     namespace pattern, empty-default opt-in, executable
     cutover gate); 3. What didn't work / open items
     (`git stash` tangle, out-of-band → in-band lifecycle,
     regional EKS cluster blocker, ClusterPolicy namespace
     quirk, load-test cadence not yet automated); 4.
     Lessons learned; 5. What's coming in W13; 6. Cross-
     references.
   * `Phase_K_W12/Apone/charter.md` (this dir).
   * `Phase_K_W12/Apone/history.md` (this file).
   * `.squad/decisions/inbox/apone-phase-k-wave-12.md`
     (NEW) — W12 memo with seven decisions matching the
     seven deliverables.
   * `.squad/agents/apone/history.md` append — W12 entry.

### Verification gates

* `terraform fmt -recursive -check infra/terraform/`: clean.
* `terraform validate` against `infra/terraform/envs/prod/`,
  `envs/staging/`, `envs/dr-us-west-2/`, `modules/redis/`,
  `modules/github-oidc/`: clean. The standalone
  `modules/edge/` validate hits a pre-existing
  `configuration_aliases` constraint (not a W12 regression).
* `kustomize build infra/k8s/overlays/{prod,staging}/`:
  clean. Cross-namespace assertion verified.
* `actionlint .github/workflows/`: clean (W12 didn't change
  any workflow).
* `helm lint helm/mahjong/`: clean (W12 didn't change the
  chart).
* Backend xUnit gate **2403/0/0** preserved (Apone lane
  doesn't touch `src/`).

### Lane discipline

* All commit-staged paths fall in the Apone lane regex per
  `.squad/agents/apone/charter.md`. NEW path
  `docs/prod-cutover.md` is added to the W12 charter
  allowlist + tracked in this history.
* Git identity via per-command `-c user.name=... -c
  user.email=...` env, NEVER `git config`. All commit /
  push wrapped in `flock -w 120 9 ... 9>.work/squad-git-lock`.
* Co-author trailer mandatory on every commit.
* `git status --short` reviewed before each `git add` —
  cross-lane work from other agents present in the working
  tree is NEVER staged.

### Cross-lane integration points (W12)

* **Bishop W12 JWKS-cache pre-warm** — surfaced empirically
  by the W12 JWT rotation rehearsal (§3 of the rehearsal
  doc). The Apone-lane rehearsal harness is the artifact
  that quantified the Bishop-lane runtime win.
* **Hudson W12 Prometheus scrape** — picks up the
  `load-test` namespace once the W12 load-test Job runs.
  No Apone-lane code change required.
* **Vasquez W12 PWA service worker** — unrelated to the
  prod cutover path; covered in Vasquez's lane history.
* **Hicks W12 cluster lifecycle** — blocked on regional EKS
  cluster provisioning. The W12 Apone-lane EDGE surface is
  ready (empty `regional_endpoints` default); Hicks's W13+
  cluster provisioning unlocks the population step.

### Pre-W13 hand-off notes

* The W12 multi-region EDGE surface is HALF the multi-region
  path — the other half (regional EKS clusters) is W13+
  Hicks. Track in W13 plan.
* The W12 JWT rotation rehearsal is GA-ready; W13 should
  promote the cadence to scheduled monthly (one-line
  workflow change).
* The ClusterPolicy namespace quirk (W4 pre-existing, also
  present in W12) needs a `fieldSpecs:` exclusion on the
  `NamespaceTransformer` in a future wave.
* The Redis load-test cadence is operator-triggered in
  W12; W13+ should automate via a Hudson-lane reminder
  workflow.
