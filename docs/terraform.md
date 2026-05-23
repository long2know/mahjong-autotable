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

## 5. Edge module (Phase K Wave 7 — Apone)

`infra/terraform/modules/edge/` provisions the **public-facing
edge** for the API + frontend: Route53 hosted zone, ACM
certificates (regional + CloudFront us-east-1), WAFv2 ACL
(REGIONAL + CLOUDFRONT scopes), WAF log delivery to S3, an
Athena workgroup over those logs, and an optional CloudFront
distribution + apex DNS records.

It is a **separate Terraform module** — not collapsed into the
primary stack — because:

* **Independent blast radius.** A misconfigured WAF rule
  shouldn't risk a `terraform apply` against the cluster IAM or
  the SSM parameters. The edge module owns its own state slice;
  callers compose it via `module "edge" { source = "./modules/edge" ... }`.
* **CloudFront opt-in.** Half-fanout: staging runs Route53+ACM+
  WAFv2 against the ALB only (`cloudfront = null`); prod adds
  the CloudFront distribution. The same module renders both
  shapes — the `cloudfront` input is an object that, when
  `null`, suppresses the CloudFront resources entirely.
* **`us-east-1` provider alias requirement.** CloudFront ACM
  certificates MUST live in us-east-1 regardless of the primary
  AWS region (AWS constraint). The module declares
  `configuration_aliases = [aws.us_east_1]` so callers pass an
  aliased provider explicitly; this is the same pattern
  `dr-replication/` uses for its us-west-2 alias.

### 5.1 What it builds

| Resource | Purpose |
|---|---|
| `aws_route53_zone` | Hosted zone for the apex domain. |
| `aws_acm_certificate` (regional + us_east_1) | Two certificates — regional for the ALB / API Gateway, us-east-1 for CloudFront. DNS-validated. |
| `aws_acm_certificate_validation` | Blocks `apply` until both certs validate. |
| `aws_wafv2_web_acl` (REGIONAL) | Front of the ALB. Managed rule groups + per-IP rate limit. |
| `aws_wafv2_web_acl` (CLOUDFRONT) | Front of the CloudFront distribution (opt-in). |
| `aws_s3_bucket` | WAF logs landing bucket — name MUST start with `aws-waf-logs-` (AWS constraint for WAF log delivery destinations). |
| `aws_wafv2_web_acl_logging_configuration` | Per-ACL log delivery to the S3 bucket. |
| `aws_athena_workgroup` | Query workgroup for WAF-log analytics. |
| `aws_cloudfront_distribution` (opt-in) | Optional CDN in front of the ALB. |
| `aws_route53_record` (apex ALIAS + AAAA) | Points the apex at either the CloudFront distribution or the ALB. |

### 5.2 Validators + defaults

`variables.tf` carries argument-level validators so callers
fail-loud on shape mismatches at `plan` time, NOT at `apply`:

* `domain_name` — lowercase FQDN regex (Route53 zone names are
  case-sensitive; Terraform doesn't enforce normalisation
  natively).
* `waf_rate_limit_per_5min` — bounded `[100, 20_000_000]`
  (AWS hard limits). Wave 7 baseline = `1000` per IP / 5 min
  (matches the W4 ALB-side rate limit; once CloudFront is in
  front in prod, this is the LAST-MILE limit hit by direct-to-ALB
  traffic).
* `logs_retention_days` — bounded `[7, 3653]` (Athena needs a
  minimum for query usefulness; max is the S3-lifecycle hard cap).
* `cloudfront.price_class` — one of `PriceClass_100`,
  `PriceClass_200`, `PriceClass_All`.

### 5.3 Usage

Single-region usage from the primary stack — pass the aliased
provider explicitly:

```hcl
provider "aws" {
  region = "us-east-1"
}

provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}

module "edge_prod" {
  source = "./modules/edge"

  providers = {
    aws            = aws
    aws.us_east_1  = aws.us_east_1
  }

  environment             = "prod"
  domain_name             = "mahjong-autotable.com"
  alb_dns_name            = module.alb.dns_name
  alb_zone_id             = module.alb.zone_id
  waf_rate_limit_per_5min = 1000
  logs_retention_days     = 90

  cloudfront = {
    price_class = "PriceClass_100"
    origin_id   = "primary-alb"
    allowed_methods = ["GET", "HEAD", "OPTIONS"]
  }

  tags = local.common_tags
}

module "edge_staging" {
  source = "./modules/edge"

  providers = {
    aws            = aws
    aws.us_east_1  = aws.us_east_1
  }

  environment             = "staging"
  domain_name             = "staging.mahjong-autotable.com"
  alb_dns_name            = module.alb_staging.dns_name
  alb_zone_id             = module.alb_staging.zone_id
  waf_rate_limit_per_5min = 1000
  logs_retention_days     = 30

  # CloudFront opt-out for staging — saves the CDN fixed cost +
  # keeps the staging surface a one-hop test target.
  cloudfront = null

  tags = local.common_tags
}
```

### 5.4 Validation caveat

Modules that declare `configuration_aliases` cannot
`terraform validate` standalone — the validator expects every
declared provider to be supplied by a caller. The `dr-replication`
module has the same property. To validate this module
in isolation:

```bash
mkdir .work/tf-edge-validate
cat > .work/tf-edge-validate/main.tf <<'EOF'
terraform {
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 5.0" }
  }
}

provider "aws" {
  region                      = "us-east-1"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
}

provider "aws" {
  alias                       = "us_east_1"
  region                      = "us-east-1"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
}

module "edge" {
  source = "../../infra/terraform/modules/edge"

  providers = {
    aws            = aws
    aws.us_east_1  = aws.us_east_1
  }

  environment  = "validate"
  domain_name  = "example.com"
  alb_dns_name = "dummy-alb-12345.us-east-1.elb.amazonaws.com"
  alb_zone_id  = "Z35SXDOTRQ7X7K"
}
EOF
cd .work/tf-edge-validate && terraform init -backend=false && terraform validate
```

The primary stack's `terraform validate` covers the edge module
implicitly once it's wired via `module "edge" { ... }`.

### 5.5 Interplay with DR module (Wave 6)

The edge module and `dr-replication` are **complementary,
non-overlapping** slices:

| Concern | Module | Provider alias |
|---|---|---|
| Hosted zone + ACM + WAF + CloudFront | `edge` | `aws.us_east_1` (CloudFront ACM constraint) |
| ECR cross-region replication | `dr-replication` | `aws.us_west_2` (DR target region) |
| Route53 health-check + failover policy (W8) | TBD — likely `edge` extension | both aliases |

A DR failover (W6 procedure) does NOT re-apply the edge module —
the apex DNS record is **active-passive against the primary ALB
DNS name**, and the operator's failover step is to update the
`alb_dns_name` input + `terraform apply` against the edge module
alone. The cluster + ECR resources are unchanged.

## 6. Cross-references

* `infra/terraform/README.md` — primary-stack bootstrap runbook.
* `infra/terraform/modules/dr-replication/README.md` — DR module.
* `infra/terraform/modules/github-oidc/README.md` — OIDC module.
* `infra/terraform/modules/edge/README.md` — Wave-7 edge module reference.
* `docs/production-deployment-runbook.md` — operator runbook for
  the helm post-bootstrap sequence.
* `docs/retro-2026-05.md` — May 2026 monthly retro
  (documents the W6 DR rehearsal commitments).
* `docs/retro-2026-06.md` — June 2026 monthly retro
  (documents the W7 edge module + helm chart-of-charts roll-out).
