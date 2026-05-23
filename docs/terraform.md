# Terraform infrastructure

> Phase K Wave 5 — bootstrap module.
> Phase K Wave 6 — DR (multi-region) module + GitHub-OIDC narrowing.

This document is the cross-module reference for the Terraform
that provisions Mahjong-Autotable's AWS footprint. Per-module
detail lives in each module's `README.md`; this document covers
multi-module workflows + the DR rehearsal runbook.

## 1. Module layout

```
infra/terraform/
├── main.tf, vpc.tf, eks.tf, rds.tf, ecr.tf, iam-github-oidc.tf
│   — flat primary-env stack (us-east-1). Provisions VPC + EKS
│     + RDS + ECR + the GitHub-OIDC role (W6-narrowed).
│
├── modules/
│   ├── github-oidc/    — Reusable GitHub-Actions OIDC module
│   │                     (W6 least-privilege policy).
│   └── dr-replication/ — Cross-region replication module
│                         (RDS read replica + ECR replication +
│                         Route 53 failover record).
│
└── envs/
    └── dr-us-west-2/   — Secondary-region (DR) stack. CIDR
                          10.1.0.0/16. Instantiates
                          `modules/dr-replication`.
```

The primary stack (flat `infra/terraform/`) is the source of
truth for the active production deployment. The `envs/` directory
holds region-bound child stacks; `modules/` holds reusable
building blocks the child stacks instantiate.

## 2. Apply order

1. Primary stack (`infra/terraform/`). Pre-create the
   state-backend bucket + DynamoDB lock table per
   `infra/terraform/README.md` §1.1.

       cd infra/terraform/
       terraform init -backend-config=backend-prod.hcl
       terraform plan -var-file=prod.tfvars
       terraform apply -var-file=prod.tfvars

   After apply: seed the DB password into SSM, run the helm
   post-bootstrap sequence (ESO → AWS-LBC → cert-manager →
   Kyverno).

2. **DR env (`envs/dr-us-west-2/`) — Wave 6 NEW.** Pre-create
   the secondary backend bucket + DynamoDB table in us-west-2
   per `envs/dr-us-west-2/backend.example.hcl`.

       cd infra/terraform/envs/dr-us-west-2/
       cp backend.example.hcl backend.hcl       # edit if names differ
       cp terraform.tfvars.example terraform.tfvars
       # ── EDIT terraform.tfvars: set hosted_zone_id,
       #    primary_target_dns, secondary_target_dns,
       #    failover_record_name ──
       terraform init -backend-config=backend.hcl
       terraform plan
       terraform apply

   The DR stack reads the primary stack's outputs via
   `terraform_remote_state` — the primary apply must complete
   first.

## 3. GitHub-OIDC role (W6 least-privilege)

The flat primary stack provisions the role inline
(`iam-github-oidc.tf`). Future envs that need their own role
(e.g. a staging EKS) should instantiate
`modules/github-oidc/` instead of duplicating the inline
resource.

W6 narrowed the inline policy from the W5 bootstrap shape:

| W5 grant | W6 grant | Narrowing reason |
|----------|----------|------------------|
| `ecr:*` | 8 verbs scoped to repo ARN | Push-only; cannot list / describe other repos. |
| `ssm:Get*` | `ssm:GetParameter` only, `mahjong/<env>/*` ARN | Drops GetParameterHistory / DescribeParameters; tightens ARN to per-env. |
| (no PassRole) | Opt-in dynamic block with `iam:PassedToService` condition | Wave 5 had no PassRole; W6 adds a fenced opt-in so future grants can't escalate via PassRole. |

Per-action rationale: `modules/github-oidc/least-privilege.tf`.
Audit invariant: ANY widening MUST land in the same commit as
the rationale update (W6 lock-step rule).

## 4. DR rehearsal

> **Cadence**: quarterly. The DR pair is warm-standby; the
> rehearsal exercises the failover path end-to-end so it stays
> tested before the day we need it for real.

### 4.1 Pre-flight

```bash
# 1. Confirm the replica is in sync.
aws rds describe-db-instances \
    --region us-west-2 \
    --db-instance-identifier mahjong-dr-us-west-2-replica \
    --query 'DBInstances[0].StatusInfos'
# Expect: [{"Status": "replicating", "StatusType": "read replication", "Normal": true}]

# 2. Confirm the ECR replication delivered the latest image.
aws ecr describe-images --region us-west-2 \
    --repository-name mahjong-autotable \
    --query 'imageDetails[?imageTags!=null]|sort_by(@, &imagePushedAt)[-3:]'
# Expect: at least the last 3 tags from us-east-1 to be present.

# 3. Confirm the Route 53 health check is currently healthy.
aws route53 get-health-check-status \
    --health-check-id "$(terraform -chdir=infra/terraform/envs/dr-us-west-2 output -raw primary_health_check_id)"
# Expect: all checkers healthy.
```

### 4.2 Force failover

The Route 53 health check is the failover trigger. To force
failover WITHOUT taking the primary down (so the rehearsal is
safe), INVERT the health check:

```bash
HC_ID=$(terraform -chdir=infra/terraform/envs/dr-us-west-2 output -raw primary_health_check_id)
aws route53 update-health-check \
    --health-check-id "$HC_ID" \
    --inverted
```

Within ~90 s (30 s × 3 failure threshold), Route 53 begins
serving the SECONDARY record. The TTL on the failover record is
30 s, so end-clients pick up the change within ~2 min total.

Smoke-test from a non-cached resolver:

```bash
dig +short @1.1.1.1 mahjong.example.com
# Expect: secondary ALB DNS (us-west-2) after ~2 min.

curl -fsSL https://mahjong.example.com/health
# Expect: 200 ok (served from us-west-2).
```

### 4.3 Promote the replica (full rehearsal — destructive)

The non-destructive rehearsal (4.2 + 4.4) is the default
quarterly run. The DESTRUCTIVE rehearsal — actually promoting
the replica — is run annually to confirm the replica's data is
intact AND the promoted DB accepts writes:

```bash
aws rds promote-read-replica \
    --region us-west-2 \
    --db-instance-identifier mahjong-dr-us-west-2-replica
# Wait ~10 min for the promotion to complete.

aws rds describe-db-instances \
    --region us-west-2 \
    --db-instance-identifier mahjong-dr-us-west-2-replica \
    --query 'DBInstances[0].ReadReplicaSourceDBInstanceIdentifier'
# Expect: null (no longer a replica)
```

After the destructive rehearsal, the replica MUST be
re-provisioned via `terraform apply` — promotion is one-way.
Schedule a tear-down + re-create maintenance window.

### 4.4 Restore

```bash
# Un-invert the health check.
aws route53 update-health-check \
    --health-check-id "$HC_ID" \
    --no-inverted

# Within ~90 s the primary record begins serving again.
dig +short @1.1.1.1 mahjong.example.com
# Expect: primary ALB DNS (us-east-1).
```

### 4.5 Post-rehearsal report

Document in `docs/retro-<YYYY-MM>.md`:

* Time from health-check invert → first SECONDARY DNS response.
* Time from health-check invert → first successful `/health` 200
  served from us-west-2.
* Any anomalies (replication lag, ECR mirror gap, Route 53
  propagation outliers).

Quarterly target SLOs: 5 min total failover time including
client DNS cache flush. If the rehearsal exceeds the SLO,
investigate the largest contributor (typically resolver TTL
caching at upstream resolvers; mitigated by the TTL<60s rule
the W6 module enforces).

## 5. Cross-references

* `infra/terraform/README.md` — primary-stack bootstrap runbook.
* `infra/terraform/modules/dr-replication/README.md` — DR module.
* `infra/terraform/modules/github-oidc/README.md` — OIDC module.
* `docs/production-deployment-runbook.md` — operator runbook for
  the helm post-bootstrap sequence.
* `docs/retro-2026-05.md` — May 2026 monthly retro
  (documents the W6 DR rehearsal commitments).
