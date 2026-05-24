# Regional EKS cluster bring-up — operator readiness

> Phase K Wave 13 — Apone (DevOps). Companion to:
> [`docs/prod-cutover.md`](./prod-cutover.md) (overall cutover
> sequencing), [`docs/edge-region-probes.md`](./edge-region-probes.md)
> (per-region probe runbook), [`infra/terraform/modules/edge/`](../infra/terraform/modules/edge/)
> (W7 + W12 EDGE module).
>
> Audience: SRE / on-call who will run `terraform apply` against
> the per-region EKS stacks ONCE the regional cluster TF state
> stores + IAM are stood up by Hicks's W13+ cluster lifecycle
> work.
>
> Scope: this doc lists the BRING-UP READINESS checklist per
> region (i.e. the prerequisites the operator must satisfy
> BEFORE the multi-region `regional_endpoints` tfvar is
> populated and the latency-based apex RR set is activated).
> Wave 13 does NOT `terraform apply` any of this — the wave's
> deliverable is the documented readiness checklist. The actual
> apply lands in W14+ once Hicks's regional cluster lifecycle
> reaches "cluster provisioned" status.

## 1. Why this doc exists

Wave 12 shipped the multi-region EDGE surface
(`infra/terraform/modules/edge/r53-regional-records.tf` + the
`regional_endpoints` tfvar). The W12 default is an empty list
— operators who haven't yet stood up regional EKS clusters see
ZERO terraform plan diff. The W12 retro (D5) flagged the
"regional EKS cluster blocker" as the next-wave dependency:

> "The W12 multi-region EDGE surface is HALF the multi-region
>  path — the other half (regional EKS clusters) is W13+
>  Hicks. Track in W13 plan."

Hicks's W13 frontend `regional_endpoints` config consumes the
regional cluster's ALB DNS once those clusters are provisioned.
The Apone-lane work is to document the readiness gates the SRE
must walk BEFORE populating `regional_endpoints` against any
given region — both per-region (this doc, §3 per-region
checklists) and global (§4 cross-region invariants).

## 2. Region inventory + apex topology

Phase K Wave 13 baseline:

| Region          | Latency tier   | Apex weight | Cluster state (W13) | TF state bucket              | ACM cert (W14+) |
|-----------------|----------------|-------------|---------------------|------------------------------|-----------------|
| `us-east-1`     | primary apex   | 100         | Hicks-W14 planned   | `mahjong-tfstate-prod-use1`  | per-region      |
| `us-west-2`     | secondary apex | 100         | Hicks-W14 planned   | `mahjong-tfstate-prod-usw2`  | per-region      |
| `eu-west-1`     | secondary apex | 100         | Hicks-W15+ planned  | `mahjong-tfstate-prod-euw1`  | per-region      |
| `ap-southeast-1`| tertiary apex  | 100         | Hicks-W15+ planned  | `mahjong-tfstate-prod-apse1` | per-region      |

The latency-based RR set on the apex (`mahjong.example.com`)
returns the closest healthy region's ALB. All four regions get
equal weights (100); R53's latency policy picks the one with
the lowest measured RTT to the resolver. The per-region health
check (W12 `aws_route53_health_check.regional`) is what fences
off an unhealthy region from the apex.

## 3. Per-region Cutover-Ready checklist

Each region runs the same readiness sequence. Mark items as
they land. A region is "Cutover-Ready" when every item below
shows ✅.

### 3.1 `us-east-1` — Cutover-Ready checklist

- [ ] **Cluster TF state bucket** — `mahjong-tfstate-prod-use1`
      exists in `us-east-1`; DynamoDB lock table
      `mahjong-tflock-prod-use1` is ACTIVE. Verify:
      `aws s3 ls s3://mahjong-tfstate-prod-use1/` returns 200;
      `aws dynamodb describe-table --table-name mahjong-tflock-prod-use1`
      returns `ACTIVE`.
- [ ] **EKS cluster provisioned** — Hicks's lane. The cluster
      name follows `mahjong-prod-use1`. Verify:
      `aws eks describe-cluster --name mahjong-prod-use1
      --region us-east-1 --query 'cluster.status'` returns
      `ACTIVE`.
- [ ] **ACM cert per region** — `mahjong.example.com` cert
      issued in `us-east-1` (R53 ALIAS records require the
      cert to live in the SAME region as the ALB). Verify:
      `aws acm list-certificates --region us-east-1` shows
      the cert in `ISSUED` state. The W7 EDGE module pinned
      a single `us-east-1` cert; the multi-region path needs
      one cert PER REGION (one wildcard cert per CloudFront +
      one ALB cert per region).
- [ ] **R53 health-check association** — the W12
      `aws_route53_health_check.regional[us-east-1]` health
      check is `Healthy`. Verify:
      `aws route53 get-health-check-status --health-check-id
      <id>` returns `Success`. The probe path is
      `/healthz` per `docs/edge-region-probes.md §2`.
- [ ] **ESO target per region** — a ClusterSecretStore named
      `aws-secrets-manager-prod-use1` exists in the
      `mahjong-prod` namespace of the regional cluster,
      scoped to `arn:aws:secretsmanager:us-east-1:*:secret:mahjong/prod/*`.
      Verify: `kubectl --context=mahjong-prod-use1
      -n external-secrets get clustersecretstore
      aws-secrets-manager-prod-use1` returns
      `READY=True`.
- [ ] **ALB DNS published** — the regional ALB DNS name is
      ready to feed into `regional_endpoints`. Verify:
      `kubectl --context=mahjong-prod-use1 -n ingress-nginx
      get svc ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'`
      returns a `*.elb.us-east-1.amazonaws.com` DNS.
- [ ] **Regional probe sweep clean** — the
      `docs/edge-region-probes.md §4` smoke test against
      `https://us-east-1.mahjong.example.com/healthz`
      returns 200, p99 < 100 ms over 3 min sample.

When all seven boxes are green, OPEN a wave-scoped PR that
adds the region to `infra/terraform/envs/prod/terraform.tfvars`:

```hcl
regional_endpoints = [
  {
    region      = "us-east-1"
    alb_dns     = "k8s-mahjong-...elb.us-east-1.amazonaws.com"
    alb_zone_id = "Z35SXDOTRQ7X7K"
  },
  # add other regions as they reach Cutover-Ready.
]
```

The W12 EDGE module picks up the populated list and provisions
the latency apex RR set + per-region ALIAS records on next
`terraform apply`.

### 3.2 `us-west-2` — Cutover-Ready checklist

- [ ] **Cluster TF state bucket** — `mahjong-tfstate-prod-usw2`
      exists in `us-west-2`; DynamoDB lock table
      `mahjong-tflock-prod-usw2` is ACTIVE.
- [ ] **EKS cluster provisioned** — `mahjong-prod-usw2`
      ACTIVE in `us-west-2`.
- [ ] **ACM cert per region** — `mahjong.example.com` cert
      issued in `us-west-2` (ISSUED state).
- [ ] **R53 health-check association** —
      `aws_route53_health_check.regional[us-west-2]`
      `Healthy`.
- [ ] **ESO target per region** —
      `aws-secrets-manager-prod-usw2` ClusterSecretStore
      `READY=True` in `mahjong-prod` namespace of the
      `mahjong-prod-usw2` cluster.
- [ ] **ALB DNS published** — ingress-nginx LB hostname
      `*.elb.us-west-2.amazonaws.com` ready.
- [ ] **Regional probe sweep clean** — smoke test against
      `https://us-west-2.mahjong.example.com/healthz`
      returns 200, p99 < 100 ms over 3 min sample.

### 3.3 `eu-west-1` — Cutover-Ready checklist

- [ ] **Cluster TF state bucket** — `mahjong-tfstate-prod-euw1`
      exists in `eu-west-1`; lock table
      `mahjong-tflock-prod-euw1` ACTIVE.
- [ ] **EKS cluster provisioned** — `mahjong-prod-euw1`
      ACTIVE in `eu-west-1`. **Data-residency note:** if any
      EU user data flows to the EU cluster, the W5 GDPR
      review (`docs/dr-rehearsal.md §6.2`) must be re-run
      before the region accepts non-test traffic. Capture in
      a separate compliance memo.
- [ ] **ACM cert per region** — `mahjong.example.com` cert
      issued in `eu-west-1` (ISSUED state).
- [ ] **R53 health-check association** —
      `aws_route53_health_check.regional[eu-west-1]`
      `Healthy`.
- [ ] **ESO target per region** —
      `aws-secrets-manager-prod-euw1` `READY=True` in
      `mahjong-prod` namespace of the `mahjong-prod-euw1`
      cluster. **Pre-flight:** confirm the eu-west-1 KMS key
      alias `alias/mahjong-prod-secrets` is provisioned in
      that region (per-region CMK; the W7 KMS module is
      single-region by default).
- [ ] **ALB DNS published** — ingress-nginx LB hostname
      `*.elb.eu-west-1.amazonaws.com` ready.
- [ ] **Regional probe sweep clean** — smoke test against
      `https://eu-west-1.mahjong.example.com/healthz`
      returns 200, p99 < 150 ms over 3 min sample (looser
      threshold for trans-atlantic probe origin).

### 3.4 `ap-southeast-1` — Cutover-Ready checklist

- [ ] **Cluster TF state bucket** —
      `mahjong-tfstate-prod-apse1` exists in
      `ap-southeast-1`; lock table
      `mahjong-tflock-prod-apse1` ACTIVE.
- [ ] **EKS cluster provisioned** — `mahjong-prod-apse1`
      ACTIVE in `ap-southeast-1`. **Latency-sensitive:** the
      ap-southeast-1 region is the highest-RTT region for
      North-American + European users; the latency-based
      apex SHOULD route them away from this region under
      normal conditions. The region exists for in-region
      SEA users + as a DR cold-failover target.
- [ ] **ACM cert per region** — `mahjong.example.com` cert
      issued in `ap-southeast-1` (ISSUED state).
- [ ] **R53 health-check association** —
      `aws_route53_health_check.regional[ap-southeast-1]`
      `Healthy`.
- [ ] **ESO target per region** —
      `aws-secrets-manager-prod-apse1` `READY=True` in the
      `mahjong-prod` namespace of the `mahjong-prod-apse1`
      cluster.
- [ ] **ALB DNS published** — ingress-nginx LB hostname
      `*.elb.ap-southeast-1.amazonaws.com` ready.
- [ ] **Regional probe sweep clean** — smoke test against
      `https://ap-southeast-1.mahjong.example.com/healthz`
      returns 200, p99 < 200 ms over 3 min sample (highest-
      latency floor; the SEA region won't beat trans-pacific
      probes).

## 4. Cross-region invariants

These invariants must hold for ALL active regions BEFORE the
latency apex serves real traffic:

1. **DR data-replication direction is canonical.** The
   primary region is `us-east-1` (per
   `infra/terraform/envs/dr-us-west-2/`). RDS Multi-AZ
   replication is intra-region; cross-region replicas are
   read-only and serve as DR-cold targets. Regional EKS
   clusters reach the PRIMARY Aurora endpoint (in
   `us-east-1`) for writes via VPC peering; this incurs
   cross-region latency for non-`us-east-1` writes. Track
   the post-cutover write-latency SLO via Hudson's
   `db-cross-region-write-p99` panel.

2. **Single Redis cluster (W13 baseline).** The W10/W11
   Redis module provisions a single ElastiCache replication
   group per env stack. ALL regional clusters reach the
   `us-east-1`-hosted Redis cluster via VPC peering. A
   future wave (W17+) may introduce per-region Redis with
   ESO `kid`-style routing — out of scope for W13 docs.

3. **JWKS endpoint is region-agnostic.** Each regional
   cluster serves `/.well-known/jwks.json` from its own
   replica of the JWT signing keys (synced via ESO from a
   single SSM source-of-truth). The keyset is identical
   across regions; the W12 JWT rotation rehearsal validates
   the convergence semantics. Regional probes (per
   `docs/edge-region-probes.md §3.2`) MUST assert the JWKS
   `kid` set matches the staging-known value.

4. **Container image is the same SHA across regions.** The
   W12 cosign image-signature check (`kyverno-enforce-patch.yaml`)
   gates admission on the cosign signature; image digests
   are pinned via `mirror-ghcr-to-ecr.yml` per-region. The
   per-region ECR mirror MUST mirror the same SHA — verify
   via `aws ecr describe-images --region <r> --repository-name
   mahjong-autotable --query 'imageDetails[0].imageDigest'`
   across all regions; the digest must match the source
   GHCR SHA.

5. **Per-region health-check probe-source IPs are
   ALLOW-listed at the ALB.** R53's health-check probe IPs
   are documented at
   <https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/route53-ip-addresses.json>.
   The W11 ingress-nginx prod overlay defaults to "allow
   any" on `/healthz` — verify the per-region overlay
   doesn't shrink that surface. A misconfigured ALB
   security group that drops health-check traffic will
   read-show as "Unhealthy" forever; the apex routes away
   from the region and the region is silently dropped.

## 5. Apply order — when each region goes live

The recommended apply sequence (one region per PR per wave):

1. **W14 — `us-east-1`** (primary apex). Add to
   `regional_endpoints`. Latency apex starts serving the
   region. Run the §3.1 probe sweep against the live apex.

2. **W14 — `us-west-2`** (secondary apex, intra-NA latency
   sibling of us-east-1). Add to `regional_endpoints`.
   Verify R53 latency policy routes West-Coast users
   here (use a per-region traceroute / dig).

3. **W15 — `eu-west-1`** (trans-atlantic). Add to
   `regional_endpoints`. EU-resident probe verifies the
   latency policy routes EU users here.

4. **W15+ — `ap-southeast-1`** (SEA / DR-cold). Add to
   `regional_endpoints` AFTER the W15 EU rollout is steady.
   Verify the apex routes SEA-resident users here.

Each step is a wave-scoped PR (`stlong/phase-k-wave-NN-region-<region>`).
A rollback is `terraform apply -var-file=…` with the
problematic region removed from `regional_endpoints` —
propagation is ≤ 60 s (R53 TTL 60 in the W7 module).

## 6. Failure scenarios + recovery

| Symptom                                          | Likely cause                                         | Recovery                                                                                                       |
|--------------------------------------------------|------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| Latency apex returns NXDOMAIN                    | `regional_endpoints` empty AND apex A removed       | `terraform apply` with the W11 single-region apex restored (revert the W12 latency-set wire-in).               |
| Apex routes traffic to UNHEALTHY region          | R53 health-check failing but DNS hasn't propagated  | Force a re-check: `aws route53 get-health-check-status`. If still failing, manually disassociate the region.  |
| Per-region cluster crashloops on rollout         | ESO ClusterSecretStore not yet `READY=True`         | Wait for ESO; check `kubectl -n external-secrets logs deploy/external-secrets`. Hold the apex-add PR.         |
| Cross-region p99 latency apex violates SLO       | A region's ALB is up but the cluster is overloaded  | Drop the region from `regional_endpoints`; investigate Hudson's per-region CPU/mem panel.                     |
| Cert validation fails on the apex                | Per-region cert NOT issued or NOT in the right region | Re-run the ACM DNS-01 challenge in the failing region; certs must live in the SAME region as the ALB.        |

## 7. W14+ hand-offs

When the first region (`us-east-1`) reaches Cutover-Ready, the
W14 owner should:

1. Open the wave-scoped PR adding `us-east-1` to
   `regional_endpoints`. Reviewers gate on the §3.1
   checklist being 100 % green.
2. After the PR merges + `terraform apply` lands, run the
   `docs/edge-region-probes.md §4` runbook to verify the
   latency apex behaviour from at least three resolver
   locations (use `dig` against
   `8.8.8.8` / `1.1.1.1` / a local resolver).
3. File a brief retro note in the W14 retro under
   "Multi-region rollout" capturing per-region apply
   timing + any anomalies.

## 8. Cross-references

- [`docs/prod-cutover.md`](./prod-cutover.md) §3.5 — the
  cutover-ready checklist's per-region rollout section.
- [`docs/edge-region-probes.md`](./edge-region-probes.md) — the
  per-region probe runbook (probe paths + smoke tests).
- [`infra/terraform/modules/edge/r53-regional-records.tf`](../infra/terraform/modules/edge/r53-regional-records.tf)
  — the W12 latency apex + per-region ALIAS records.
- [`infra/terraform/envs/prod/`](../infra/terraform/envs/prod/)
  — the prod env stack where `regional_endpoints` is wired.
- [`docs/dr-rehearsal.md`](./dr-rehearsal.md) — the W6 DR
  rehearsal procedure; §6.2 GDPR review checklist required
  for the EU region.
- [`docs/terraform.md`](./terraform.md) §5 — the W7 EDGE
  module reference.
- Hicks-lane cluster lifecycle docs (W13+) — pending
  publication; track via the W13 Hicks history.
