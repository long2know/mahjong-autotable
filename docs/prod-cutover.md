# Production cutover runbook

> Phase K Wave 12 — Apone (DevOps). This doc captures the prod-
> environment readiness assessment + cutover sequencing as the W11
> hand-offs (Redis ElastiCache stack, prod EDGE module, ArgoCD
> ingress) reach W12 wire-in maturity. It supersedes the scattered
> "TODO: prod cutover" notes in `docs/redis-cluster.md §12` and
> `docs/argo-rollouts-setup.md §5` with a single chronological
> checklist.
>
> Scope: this doc is operator-facing. It assumes the reader is
> already onboarded against the primary terraform stack and has
> `kubectl` access to the prod EKS cluster.

## Table of contents

1. [Prod Redis terraform plan readiness](#1-prod-redis-terraform-plan-readiness)
2. [Prod kustomization wire-up](#2-prod-kustomization-wire-up)
3. [Cutover-Ready checklist](#3-cutover-ready-checklist)
4. [Argo Rollouts dashboard cross-namespace pattern](#4-argo-rollouts-dashboard-cross-namespace-pattern)
5. [Rollback playbook](#5-rollback-playbook)
6. [Post-cutover hardening](#6-post-cutover-hardening)

---

## 1. Prod Redis terraform plan readiness

The W10 `modules/redis` ElastiCache surface has been wired into the
W11 prod-env stack (`infra/terraform/envs/prod/main.tf`); W12
confirms the stack is `terraform plan`-ready against a vanilla
operator workstation (no hidden manual prereqs beyond the primary
stack apply documented in `docs/production-deployment-runbook.md`).

### 1.1 Pre-flight assertions

The operator MUST verify each of the following BEFORE running
`terraform plan` against the prod env:

| # | Assertion | How to verify | Source-of-truth |
|---|-----------|---------------|-----------------|
| 1 | Primary stack applied → VPC + EKS + RDS up | `terraform -chdir=infra/terraform output -raw vpc_id` returns a non-empty ID | `infra/terraform/main.tf` |
| 2 | `mahjong-tfstate-prod` S3 bucket exists in `us-east-1` | `aws s3 ls s3://mahjong-tfstate-prod/` returns 200 | manual one-off |
| 3 | `mahjong-tflock-prod` DynamoDB table exists (`LockID` HASH key) | `aws dynamodb describe-table --table-name mahjong-tflock-prod` returns ACTIVE | manual one-off |
| 4 | nginx-ingress controller installed; ALB DNS published | `kubectl -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'` returns the `*.elb.amazonaws.com` DNS | helm step #2 in `production-deployment-runbook.md` §3 |
| 5 | `infra/terraform/envs/prod/backend.hcl` populated from `backend.example.hcl` | `grep bucket backend.hcl` shows `mahjong-tfstate-prod` | operator |
| 6 | `infra/terraform/envs/prod/terraform.tfvars` populated from the `.example` | `terraform validate` passes after `init` | operator |

### 1.2 Required tfvars (W11 + W12 surface)

The `terraform.tfvars.example` lists the canonical inputs. Key
W11/W12 entries that operators commonly miss on first cutover:

```hcl
# W7/W11 — EDGE module inputs.
domain_name  = "mahjong.example.com"
alb_dns_name = "k8s-mahjong-...elb.amazonaws.com"        # from #4 above
alb_zone_id  = "Z35SXDOTRQ7X7K"                          # us-east-1 ALB hosted zone
existing_hosted_zone_id = "Z0123456789ABCDEFGHIJ"        # OR leave empty to provision

# W10/W11 — Redis module inputs.
vpc_id                         = "vpc-…"                 # primary stack output
private_subnet_ids             = ["subnet-…","subnet-…","subnet-…"]
vpc_cidr                       = "10.0.0.0/16"
eks_worker_security_group_ids  = ["sg-…"]                # EKS node SG
redis_node_type                = "cache.r6g.large"       # prod tier — `docs/redis-cluster.md §12`
redis_replica_count            = 1                       # one replica per AZ (multi_az = true)
redis_kms_key_id               = "alias/aws/elasticache" # OR a CMK alias for SOC-2

# W12 — multi-region EDGE inputs (empty until regional EKS lands).
regional_endpoints = []                                  # populate per §3 below once active
```

### 1.3 Expected plan shape (W12 baseline)

After a clean `terraform init -backend-config=backend.hcl` and
`terraform plan -out=prod.tfplan`, the operator should see
approximately these resource counts (drift outside the ±2 band is
a yellow flag — investigate before applying):

| Module | Resource kind | Count | Notes |
|---|---|---|---|
| `module.edge` | `aws_acm_certificate.this` | 1 | primary cert |
| `module.edge` | `aws_acm_certificate_validation.this` | 1 | DNS-01 |
| `module.edge` | `aws_route53_record.validation` | 1 — N | one per SAN |
| `module.edge` | `aws_route53_record.apex` | 0 or 1 | 0 IFF `regional_endpoints` non-empty (W12 latency apex takes over) |
| `module.edge` | `aws_route53_record.regional_alias` | 0 — N | one per `regional_endpoints` entry (W12) |
| `module.edge` | `aws_route53_record.latency_apex` | 0 — N | RR set on apex; one per region (W12) |
| `module.edge` | `aws_route53_health_check.regional` | 0 — N | one per region (W12) |
| `module.edge` | `aws_wafv2_web_acl.this` | 1 | BLOCK mode (W11 cutover) |
| `module.edge` | `aws_wafv2_web_acl_logging_configuration.this` | 1 | CloudWatch log group |
| `module.redis` | `aws_elasticache_replication_group.this` | 1 | multi-AZ |
| `module.redis` | `aws_elasticache_parameter_group.this` | 1 | TLS + AUTH params |
| `module.redis` | `aws_elasticache_subnet_group.this` | 1 | three AZs |
| `module.redis` | `aws_security_group.this` | 1 | 6379/TCP from EKS workers only |
| `module.redis` | `aws_ssm_parameter.connection_string` | 1 | SecureString, `/mahjong/prod/redis/connection-string` |
| `module.redis` | `aws_ssm_parameter.auth_token` | 1 | SecureString — rotated quarterly per `docs/redis-cluster.md §9` |

> ⚠️ **AUTH token first-apply quirk.** ElastiCache requires the
> AUTH token at creation time and re-creation is the ONLY way to
> change it (no in-place modify). The W10 module pre-generates the
> token via `random_password.auth_token` (Terraform-managed) and
> stores it in SSM. On a fresh apply you'll see the token in
> `terraform.tfstate` (write-only redaction is honoured by the
> S3 backend's KMS-encryption, but operators should NEVER cat the
> raw state file from a workstation).

### 1.4 Apply gates

Block the `terraform apply` if ANY of the following are true:

1. `aws sts get-caller-identity` resolves to a role WITHOUT
   `elasticache:CreateReplicationGroup` (the operator IAM role is
   the only role that should be applying prod terraform — verify
   via `aws iam get-role --role-name <role>` against the W11 IAM
   boundary documented in `docs/aws-iam-roles.md §3`).
2. `terraform plan` shows any DESTROY of the primary stack's
   `aws_vpc.this`, `aws_eks_cluster.this`, `aws_db_instance.this`
   — those resources are STATIC in this env. A destroy plan means
   the operator is pointed at the wrong backend or has stale
   tfvars (compare to the previous apply's state lineage).
3. The primary stack's `terraform plan` is NOT clean. The two
   stacks share VPC outputs; an unapplied drift on the primary
   side will manifest as Redis subnet-group churn here.

---

## 2. Prod kustomization wire-up

### 2.1 W11 hand-off summary

W11 delivered three operationally-relevant manifests in
`infra/k8s/overlays/prod/` that were INTENTIONALLY NOT in the
kustomization `resources:` list (each file header carried an
"OUT-OF-BAND" notice):

| File | Kind | Namespace | Why out-of-band at W11 |
|---|---|---|---|
| `redis-connection-string-secret.yaml` | ExternalSecret | `mahjong-prod` | Deployment patch envFrom mount deferred — the in-process omnibus secret already carried the same key as a fallback. |
| `argo-rollouts-ingress-auth.yaml` | Ingress | `argo-rollouts` | Cross-namespace from the overlay's `mahjong-prod` default; W11 didn't yet have a clean transformer pattern. |

### 2.2 W12 wire-in

W12 brings all three (plus the new `argo-rollouts-network-policy.yaml`)
INTO the prod overlay's `resources:` list. The headers on each
file have been updated from "OUT-OF-BAND" to "IN-BAND" with a
back-reference to this section.

The wire-in required ONE structural change to
`infra/k8s/overlays/prod/kustomization.yaml`: the top-level
`namespace: mahjong-prod` directive was replaced with a
`transformers:` entry pointing at the new
`namespace-transformer.yaml` (an inline `NamespaceTransformer`
with `unsetOnly: true`). The semantics are equivalent for ALL
in-base resources (none of them pre-declare a namespace, so they
pick up `mahjong-prod` from the transformer); the difference is
that resources WITH a pre-declared `metadata.namespace` (the
argo-rollouts ingress + the new NetworkPolicies) keep their
declared value rather than getting silently rewritten. See §4 of
this doc for the design rationale.

Verification:

```bash
$ kustomize build infra/k8s/overlays/prod/ | \
    awk '/^kind:/{k=$2} /^  name:/{n=$2} /^  namespace:/{print k,n,"→",$2}' | \
    sort -u
ClusterPolicy prod-enforce-prod-mahjong-images → mahjong-prod  # ← pre-W4 quirk; cluster-scoped resource picks up overlay ns (matches W11 behaviour)
ConfigMap prod-coturn-config → mahjong-prod
ConfigMap prod-mahjong-autotable → mahjong-prod
…
Ingress prod-argo-rollouts-dashboard → argo-rollouts          # ← W11 hand-off, cross-namespace preserved
Ingress prod-mahjong-autotable → mahjong-prod
NetworkPolicy prod-argo-rollouts-controller-egress → argo-rollouts  # ← W12 net policy, ns preserved
NetworkPolicy prod-argo-rollouts-dashboard-egress → argo-rollouts
NetworkPolicy prod-argo-rollouts-dashboard-ingress → argo-rollouts
NetworkPolicy prod-coturn-relay-ports → mahjong-prod
…
ExternalSecret prod-mahjong-redis-prod → mahjong-prod          # ← W11 hand-off, in-namespace
```

### 2.3 Runtime mount

The W12 wire-in also adds ONE deployment patch in the
kustomization:

```yaml
- target:
    kind: Deployment
    name: mahjong-autotable
  patch: |-
    - op: add
      path: /spec/template/spec/containers/0/envFrom/-
      value:
        secretRef:
          name: mahjong-redis-prod
          optional: true
```

`optional: true` preserves the cutover-safe fall-through. Before
ESO has materialised the secret (e.g. on a fresh cluster bring-up
where the AWS-Secrets-Manager IAM role hasn't propagated yet) the
container starts WITHOUT the env var — Bishop's runtime then
reads the connection string from the omnibus
`mahjong-autotable` Secret (the W10 fallback chain). Once ESO
hydrates the dedicated Secret the operator restarts the
deployment (`kubectl -n mahjong-prod rollout restart deploy/prod-mahjong-autotable`)
and the dedicated mount takes precedence.

### 2.4 Apply order

1. `cd infra/terraform/envs/prod && terraform apply prod.tfplan`
   provisions the SSM SecureString that the ExternalSecret
   targets.
2. `kustomize build infra/k8s/overlays/prod/ | kubectl apply -f -`
   creates the ExternalSecret, Ingress, NetworkPolicies, and
   deployment patches.
3. ESO reconciles → the `mahjong-redis-prod` Secret materialises
   in `mahjong-prod`.
4. `kubectl -n mahjong-prod rollout restart deploy/prod-mahjong-autotable`
   to pick up the new envFrom mount.
5. Flip `Idempotency:Provider=Redis` via the omnibus configmap +
   restart again (per `docs/redis-cluster.md §12` runbook).

---

## 3. Cutover-Ready checklist

This is the final gating checklist BEFORE the prod EKS cluster
takes its first non-test traffic. Each item names the owning
agent / wave / file so reviewers can audit completeness.

### 3.1 Infrastructure (Apone)

- [ ] **Primary stack applied** — `infra/terraform/` (W2–W7) →
      VPC, EKS, RDS, ECR, GitHub-OIDC role.
- [ ] **Prod env stack applied** — `infra/terraform/envs/prod/`
      (W11 + W12) → EDGE (ACM + Route53 + WAF BLOCK),
      ElastiCache Redis (multi-AZ), per-region health checks
      (W12; empty until regional EKS lands).
- [ ] **DR env stack applied** — `infra/terraform/envs/dr-us-west-2/`
      (W9 + W10) → cross-region Aurora replica, S3-CRR for
      tfstate + media.
- [ ] **Helm baseline** — nginx-ingress, cert-manager, ESO,
      argo-rollouts (per `docs/argo-rollouts-setup.md §2`),
      Kyverno (per `docs/audits/kyverno.md §2`).
- [ ] **Prod overlay built + applied** — `kustomize build
      infra/k8s/overlays/prod/ | kubectl apply -f -` returns
      clean.
- [ ] **NetworkPolicies enforced** — `kubectl -n argo-rollouts
      get netpol` shows the W12 trio. See `docs/argo-rollouts-setup.md §6`.
- [ ] **Image signatures** — Kyverno ClusterPolicy
      `prod-enforce-prod-mahjong-images` admits only cosign-signed
      images from the W3 signing workflow.

### 3.2 Application (Bishop)

- [ ] **W4 OAuth chain** active — dex + oauth2-proxy in
      `auth` namespace, OIDC IdP allow-list refreshed for the
      Q4 squad roster.
- [ ] **JWT keyset** — three active HS256 keys (W4) + three
      active RS256 keys (W7) bootstrapped per
      `docs/jwt-rotation.md §3`. Confirm
      `curl https://mahjong.example.com/.well-known/jwks.json | jq '.keys | length'`
      ≥ 3.
- [ ] **Idempotency provider** — `Idempotency:Provider=Redis`
      flipped in the prod configmap; `RedisIdempotencyStore`
      health-check 200 on `/healthz/idempotency` (per
      `docs/redis-cluster.md §12`).
- [ ] **Migration job** — `prod-mahjong-autotable-migrate`
      Job ran to completion against the prod RDS instance.

### 3.3 Observability (Hudson)

- [ ] **Grafana** prod dashboards loaded from
      `docs/dashboards/` JSON exports (per Hudson W11 runbook).
- [ ] **PagerDuty** routing keys wired against the
      `mahjong-prod-cutover` escalation policy.
- [ ] **SLO burn-rate alerts** active for the W10 Redis SLOs
      (p99 lookup < 5 ms / write < 8 ms) — see
      `docs/redis-cluster.md §4` for the W12 load-test baseline.

### 3.4 Frontend (Vasquez)

- [ ] **CDN cache invalidation** — when `cloudfront_enabled =
      true` flips, run the W11 invalidation runbook in
      `docs/cdn-cache-strategy.md §5`.
- [ ] **PWA service worker** — manifest deployed pointing at the
      prod origin per Vasquez W11 hand-off.

### 3.5 Per-region rollout (Apone + Hicks, W12 hand-off)

- [ ] **Regional EKS clusters** — stand up `us-east-1`,
      `us-west-2`, `eu-west-1` clusters (Hicks owns the cluster
      lifecycle; Apone owns the EDGE wire-in).
- [ ] **Per-region ALB DNS** — published; feed into
      `regional_endpoints` tfvar on the prod env stack.
- [ ] **Latency-based RR set** — `terraform apply` provisions
      the W12 R53 latency apex RR set + per-region health checks.
- [ ] **Probe sweep** — `docs/edge-region-probes.md §4` runbook
      walks the per-region smoke test.

---

## 4. Argo Rollouts dashboard cross-namespace pattern

### 4.1 The constraint

The prod overlay's W11 baseline pinned every resource into the
`mahjong-prod` namespace via the kustomization-top-level
`namespace:` directive. kustomize v5's
`namespace` field is a hard-overwrite: even resources that
pre-declare `metadata.namespace` in their source YAML get
rewritten. This was fine for the W11 set (every resource lived in
`mahjong-prod`) but BROKE the W12 wire-in target — the argo-
rollouts dashboard Ingress + the new W12 NetworkPolicies MUST sit
in the `argo-rollouts` namespace because:

- nginx-ingress requires the Ingress resource to be in the SAME
  namespace as the upstream Service (the dashboard Service lives
  in `argo-rollouts`, owned by the upstream Helm chart).
- The NetworkPolicies target the argo-rollouts controller +
  dashboard pods via pod-selector — pod-selectors are namespace-
  scoped, so the NetworkPolicy must sit in the same namespace as
  the pods it selects.

### 4.2 The pattern

W12 introduces a tested cross-namespace pattern for any future
overlay that needs to fan out across multiple namespaces from a
single kustomization root:

```yaml
# overlay/kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namePrefix: prod-                     # still applies cluster-wide

resources:
  - ../../base                        # in-namespace defaults
  - cross-namespace-resource.yaml     # pre-declares metadata.namespace

transformers:
  - namespace-transformer.yaml        # NamespaceTransformer + unsetOnly: true
```

```yaml
# overlay/namespace-transformer.yaml
apiVersion: builtin
kind: NamespaceTransformer
metadata:
  name: overlay-namespace-transformer
  namespace: mahjong-prod             # the default-fill target
unsetOnly: true                       # KEY: only fills empty ns
fieldSpecs:
  - path: metadata/namespace
    create: true
```

The semantics are:

- Resources WITHOUT `metadata.namespace` (the W11 in-base default)
  get filled with `mahjong-prod` — identical behaviour to the old
  top-level `namespace:` directive.
- Resources WITH a pre-declared `metadata.namespace` keep their
  declared value — the cross-namespace resources stay pinned.

### 4.3 Why not split into multiple kustomizations?

A clean alternative would be a top-level `argocd Application` (or
two parallel kustomize roots) where each root targets a single
namespace. This was REJECTED for W12 because:

1. The W11 overlay is already discoverable as a single
   `kustomize build infra/k8s/overlays/prod/` entrypoint — every
   incident runbook in `docs/` references that one command.
2. Splitting forces operators to remember TWO apply commands +
   gate them in the right order (the argo-rollouts ingress depends
   on the dashboard Service from the Helm chart but is independent
   of the `mahjong-prod` app deployment — operators could trivially
   apply them in the wrong order).
3. The W12 pattern is documented in the kustomize upstream docs
   (the `unsetOnly` flag is an explicit feature; this isn't a hack).

### 4.4 Future-proofing

The pattern extends cleanly to additional cross-namespace fan-out:

- W13+ Prometheus AlertManager `Alertmanager` CRDs in `monitoring`
  ns → just add to `resources:` with `namespace: monitoring`
  pre-declared.
- W13+ External-DNS CRDs in `external-dns` ns → same pattern.

No further kustomization-structure changes are needed.

---

## 5. Rollback playbook

If the cutover surfaces a regression after the
`Idempotency:Provider=Redis` flip:

### 5.1 Application layer (fast — < 5 min)

1. `kubectl -n mahjong-prod edit configmap prod-mahjong-autotable`
   → set `Idempotency:Provider=InMemory`.
2. `kubectl -n mahjong-prod rollout restart deploy/prod-mahjong-autotable`.
3. Verify `/healthz/idempotency` returns 200 + the provider value
   is now `InMemory` (Bishop W10 health-check exposes this).

In-memory provider is single-pod-scoped so idempotency guarantees
degrade to "best effort within a single pod's lifetime" — this is
acceptable as a temporary rollback per the W10 design memo (the
idempotency window is 24 h; a single-pod scope just means a
double-submit across pods could hit the database twice — Bishop's
SQL uniqueness constraints catch that case as the second-level
defence).

### 5.2 Infrastructure layer (slow — < 1 hr)

If the Redis cluster itself is the failure mode (e.g. AUTH token
mismatch, AZ failover stuck):

1. Roll back the SSM AUTH-token rotation: `aws ssm get-parameter
   --name /mahjong/prod/redis/auth-token --with-decryption
   --query 'Parameter.Version'` — restore the prior version via
   `aws ssm put-parameter --overwrite --name … --value <prior>`.
2. ESO will re-reconcile the Secret within its refresh interval
   (default 1 h — override with the `refreshInterval` field on the
   ExternalSecret for an emergency 5 m refresh).
3. If the cluster is unhealthy beyond an AUTH issue, scale the
   deployment to 0 → in-memory rollback per §5.1 → open an AWS
   support ticket against the prod ElastiCache replication group.

### 5.3 Edge layer (latency apex)

If the W12 latency-based RR set is mis-routing (e.g. one region
keeps health-checking unhealthy and traffic concentrates on the
others past capacity):

1. `terraform -chdir=infra/terraform/envs/prod plan
   -var='regional_endpoints=[]'` → diff should show ONLY the
   W12 R53 resources being destroyed, the apex A-record being
   re-created against `alb_dns_name` (single-region default).
2. `terraform apply` to revert to the W11 single-region apex
   shape.

R53 propagation is ≤ 60 s once the apex record is rewritten (TTL
60 in the W7 module). The CloudFront fronting (when enabled) adds
edge-cache invalidation latency — track via
`docs/cdn-cache-strategy.md §5`.

---

## 6. Post-cutover hardening

> Phase K Wave 13 — Apone (DevOps). This section captures the
> staged-tighten gates that fire AFTER the prod EKS cluster
> reaches steady-state. Each gate is intentionally NOT in the §3
> Cutover-Ready checklist — flipping them prematurely (during
> the cutover window itself) negates the cutover-safe defaults.

### 6.1 Tightening calendar

The hardening gates land in this order, with the 14-day waits
described per gate. Each gate is a wave-scoped PR; mark items
as they land.

| # | Gate                                                       | Pre-conditions                                                                                                                                  | Wave (target) |
|---|------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------|---------------|
| 1 | Flip Redis envFrom `optional: true → false`                | (a) Hudson `kube-pod-not-ready` 100 % for 14 d; (b) Hudson `eso-sync-failures-prod` 0 errors for 14 d; (c) `mahjong-redis-prod` ExternalSecret `SecretSynced=True` for 14 d; (d) staging rehearsal of the flip + revert. | W14 + 14 d |
| 2 | Flip JWT keys envFrom `optional: true → false` (W4)        | Same as gate 1, applied to the `mahjong-jwt-keys` Secret + ExternalSecret.                                                                       | W14 + 28 d   |
| 3 | Flip JWT RSA keys envFrom `optional: true → false` (W7)    | Same as gate 1, applied to the `mahjong-jwt-rsa-keys` Secret + ExternalSecret. Gate 2 + Gate 3 can land together.                                | W14 + 28 d   |
| 4 | Lock the Kyverno `verify-mahjong-images` policy to `enforce` (out of `audit`) | The W12 cosign-signed image admission has been operative for 30 d with 0 deny events outside test pushes; the W14 ECR mirror per region (per `docs/regional-eks-bringup.md §4`) is steady. | W15 |
| 5 | Promote `prod-mahjong-autotable` HPA min-replicas 3 → 5    | Hudson `kube-pod-pending` 100 % for 30 d (no scale-up stalls from quota); Hudson `cpu-saturation-prod` < 60 % p99 for 30 d.                       | W15 + 14 d   |
| 6 | Lock CSP to `report-only=false` (W4 surface)               | Hudson's `csp-violations-prod` per-day count is 0 for 30 d (no false positives from third-party scripts or new browser intrinsics).             | W16          |

### 6.2 Gate 1 — Redis envFrom `optional: false`

The W13 prep deliverable is the PR-ready patch file
[`infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`](../infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml).
See that file's header for the apply procedure, the pre-flight
gates, and the rollback path.

Why this gate is the FIRST: the Redis envFrom is the newest
ESO-backed mount (W12); flipping it first means the W14 SRE
gets to validate the fail-CLOSED behaviour on a Secret that
was JUST landed (recent operator memory > older muscle memory).
The W4 + W7 JWT secret flips (gates 2 + 3) follow because the
ESO infra is the same and we want to bias toward earlier wins.

### 6.3 Gate 4 — Kyverno enforce mode

The W12 Kyverno overlay (`kyverno-enforce-patch.yaml`) is in
`enforce` mode but the upstream `verify-mahjong-images`
ClusterPolicy is in `audit` mode. Gate 4 flips the upstream
to `enforce`, removing the W12 second-line defence as a
single-point-of-failure (the W12 ClusterPolicy continues to
enforce regardless of the upstream's mode — the gate is about
unifying behaviour, not relaxing it).

Pre-flight: a 30-day audit window with no deny events outside
test pushes confirms the cosign signing chain is reliable.

### 6.4 Gate 5 — HPA min-replicas bump

The W12 prod overlay pins HPA min-replicas at 3 (sticky-session
floor recommended by Hudson). Gate 5 bumps to 5, which improves
the burst-resilience window during a single pod evict (3 → 2
replicas leaves ≈ 50 % capacity; 5 → 4 leaves ≈ 80 %). Pre-flight
windows confirm there's no quota pressure that would cause the
+2 replicas to stall in Pending state.

### 6.5 Gate 6 — CSP enforce mode

The W4 Content-Security-Policy header on the prod Ingress is in
`report-only` mode (violations are logged to a Hudson dashboard
but NOT blocked). Gate 6 flips to enforce. The 30-day pre-flight
gives time for false-positive sources (browser intrinsics that
violate the policy spuriously, third-party scripts that move
their CDN domains) to be allow-listed.

### 6.6 Per-gate rollback

Each gate is a single-PR change. Rollback is `git revert <pr>`
+ `kubectl apply -k infra/k8s/overlays/prod/`. The cutover-
safe defaults (gate 1's `optional: true`, gate 6's
`report-only`, etc.) MUST remain in the git history as the
revert target — DO NOT squash-merge the hardening PRs; merge-
commit so the revert path is one click.

### 6.7 Per-gate observability

Each gate has a Hudson dashboard panel that tracks the post-
flip blast radius for 14 d. If the panel goes red, the gate is
reverted per §6.6. The panel mapping:

| Gate                        | Hudson panel                                  |
|-----------------------------|-----------------------------------------------|
| 1 — Redis envFrom required  | `kube-pod-not-ready` + `eso-sync-failures`    |
| 2 — JWT HS256 envFrom req.  | `kube-pod-not-ready` + `auth-failure-rate`    |
| 3 — JWT RS256 envFrom req.  | `kube-pod-not-ready` + `auth-failure-rate`    |
| 4 — Kyverno enforce         | `kyverno-deny-events` + `pod-admission-rate`  |
| 5 — HPA min-replicas 5      | `kube-pod-pending` + `cpu-saturation-prod`    |
| 6 — CSP enforce             | `csp-violations-prod` + `js-error-rate-prod`  |

Hudson owns the panels; DevOps owns the apply PRs; the squad
gates each PR on a green panel screenshot in the PR description.

---

## Cross-references

- `docs/redis-cluster.md` — Redis cluster operator manual (sizing, rotation, load-test methodology in §4).
- `docs/argo-rollouts-setup.md` — Argo Rollouts install + NetworkPolicy (§6).
- `docs/edge-region-probes.md` — per-region probe runbook (W12 R53 records in §3).
- `docs/jwt-rotation.md` + `docs/jwt-rotation-rehearsal.md` — JWT key rotation runbook + rehearsal history.
- `docs/production-deployment-runbook.md` — original prod cutover runbook (this doc is the W12 successor for the Redis + multi-region paths).
- `infra/terraform/envs/prod/` — prod terraform stack (W11 + W12).
- `infra/k8s/overlays/prod/` — prod kustomize overlay (W11 + W12 in-band wire-in).
