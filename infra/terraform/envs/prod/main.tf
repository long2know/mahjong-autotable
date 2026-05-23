# Phase K Wave 11 — Apone (DevOps).
#
# Production-env stack — wires the W7 `modules/edge` module +
# the W10 `modules/redis` module against the production EKS
# cluster. Mirror of `envs/staging/main.tf` with the prod-tier
# shape baked in:
#
#   * Edge module: managed rule-groups in BLOCK mode (the W8 →
#     W10 staging-soak commitment — staging proved the rule sets
#     don't false-positive on real traffic for one quarter, prod
#     now flips count-only → block).
#   * Redis module: multi-AZ (`replica_count = 1` minimum),
#     `cache.r6g.large` (production tier — see §3 in
#     `docs/redis-cluster.md` for the sizing rationale +
#     load-test baseline), 7-day snapshot retention,
#     encryption-at-rest + TLS-in-transit + AUTH token (the
#     three security defaults Bishop's W10 `RedisIdempotencyStore`
#     hard-requires).
#   * Logs: 90-day retention (vs staging's 30) — matches the
#     audit/SOC-2 retention baseline in `docs/audits/`.
#
# Layout choice (mirrors staging): the production EDGE + Redis
# live in this env stack rather than collapsing into
# `infra/terraform/main.tf`. The primary stack is the SoT for
# VPC + EKS + RDS + ECR + the GitHub-OIDC role; the edge surface
# + Redis cluster depend on those outputs (notably the ingress
# controller's ALB DNS) so this stack MUST apply AFTER the
# primary cluster bootstrap completes.
#
# Apply order:
#   1. `cd infra/terraform/` → primary stack apply with
#      `-var-file=prod.tfvars` (provisions VPC + EKS + RDS).
#   2. Install nginx-ingress via helm against the prod cluster
#      → ALB DNS published.
#   3. `cd infra/terraform/envs/prod/` → THIS stack:
#        cp backend.example.hcl backend.hcl
#        cp terraform.tfvars.example terraform.tfvars
#        # EDIT terraform.tfvars: domain_name, alb_dns_name,
#        # alb_zone_id (from kubectl), vpc_id +
#        # private_subnet_ids + vpc_cidr (from primary outputs).
#        terraform init -backend-config=backend.hcl
#        terraform plan
#        terraform apply
#
# Smoke test (see `docs/production-deployment-runbook.md` §6):
#   curl https://mahjong.example.com/healthz   → 200 ok
#   curl https://mahjong.example.com/.well-known/jwks.json | jq '.keys | length'
#                                                → ≥ 3
#
# State backend: pre-create `mahjong-tfstate-prod` bucket +
# `mahjong-tflock-prod` DynamoDB table in us-east-1. Distinct
# from the staging state-bucket so an `apply` against THIS env
# can't accidentally mutate the staging stack.

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }

  backend "s3" {}
}

# ── Providers ────────────────────────────────────────────────────
#
# `aws` (default, var.region — us-east-1 in prod).
# `aws.us_east_1` REQUIRED for the CloudFront ACM cert (AWS
# constraint — CloudFront ACM certs MUST live in us-east-1).
# When `var.region` IS already us-east-1 the alias resolves to
# the same region; the alias declaration is unconditional because
# the edge module declares `configuration_aliases = [aws.us_east_1]`
# and Terraform errors out without it.

provider "aws" {
  region = var.region

  default_tags {
    tags = local.common_tags
  }
}

provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"

  default_tags {
    tags = local.common_tags
  }
}

# ── Locals ───────────────────────────────────────────────────────

locals {
  environment = "prod"

  common_tags = {
    "Project"     = "mahjong-autotable"
    "Environment" = local.environment
    "ManagedBy"   = "terraform"
    "Module"      = "envs/prod"
    "Wave"        = "phase-k-wave-11"
  }

  # WAF rule set for prod:
  #   * `count_only = false` on every managed group — prod
  #     blocks. Staging soaked these rule sets in COUNT mode
  #     from W8 through W10 (≈ 1 quarter) with no false-positive
  #     events on live traffic; W11 flips prod to BLOCK per the
  #     W8 cutover commitment (`docs/terraform.md` §5.6).
  prod_waf_managed_rule_groups = [
    {
      name       = "AWSManagedRulesCommonRuleSet"
      priority   = 10
      count_only = false
    },
    {
      name       = "AWSManagedRulesKnownBadInputsRuleSet"
      priority   = 20
      count_only = false
    },
    {
      name       = "AWSManagedRulesAmazonIpReputationList"
      priority   = 30
      count_only = false
    },
  ]
}

# ── Edge module ──────────────────────────────────────────────────

module "edge" {
  source = "../../modules/edge"

  providers = {
    aws           = aws
    aws.us_east_1 = aws.us_east_1
  }

  environment = local.environment
  region      = var.region
  common_tags = local.common_tags

  # DNS / ACM.
  domain_name                  = var.domain_name
  create_hosted_zone           = var.create_hosted_zone
  existing_hosted_zone_id      = var.existing_hosted_zone_id
  additional_subject_alt_names = var.additional_subject_alt_names

  # ALB binding (skip when fronting via CloudFront — prod
  # defaults to ALB-direct; CloudFront is opt-in via the
  # `cloudfront` block below).
  alb_dns_name = var.alb_dns_name
  alb_zone_id  = var.alb_zone_id

  # WAF — BLOCK mode for prod (W11 cutover from W8's count-only
  # staging-soak commitment).
  waf_managed_rule_groups = local.prod_waf_managed_rule_groups
  waf_rate_limit_per_5min = var.waf_rate_limit_per_5min
  logs_retention_days     = var.logs_retention_days

  # CloudFront fronting — disabled by default. Flip to enabled
  # via tfvars when the squad is ready to take on the CDN cost
  # + the cache-invalidation runbook.
  cloudfront = {
    enabled                  = var.cloudfront_enabled
    origin_domain_name       = var.cloudfront_origin_domain_name
    price_class              = var.cloudfront_price_class
    minimum_protocol_version = "TLSv1.2_2021"
  }

  # Multi-region endpoints (Phase K Wave 12). Empty by default;
  # operator populates via tfvars once the regional EKS clusters
  # are stood up. Drives the latency-based RR set + per-region
  # health checks in `modules/edge/r53-regional-records.tf`.
  regional_endpoints = var.regional_endpoints
}

# ── Redis module (W10 — IdempotencyStore backing; W11 prod) ──────
#
# Bishop's W10 `RedisIdempotencyStore` runtime needs a Redis
# cluster reachable from the EKS workers. This module is wired
# against the PRIMARY stack's VPC + private subnets via tfvars
# (operator forwards the IDs).
#
# Prod shape (vs the cheap-staging shape in envs/staging):
#
#   * `cache.r6g.large`           — production tier. Sized for
#                                   the W10 load test baseline
#                                   (Hudson's Q3 2026 report);
#                                   r6g family is graviton2 +
#                                   memory-optimised, the right
#                                   shape for an in-memory
#                                   idempotency cache hot-set.
#   * `replica_count = 1`         — one replica in a second AZ.
#                                   Required for `multi_az_enabled
#                                   = true` (AWS constraint).
#   * `multi_az_enabled = true`   — automatic failover ON.
#   * `snapshot_retention_limit
#       = 7`                      — 7-day daily snapshots. Lower
#                                   than RDS's 30-day window
#                                   because idempotency keys
#                                   are 5-min TTL — the
#                                   snapshot is a debug aid, not
#                                   a recovery surface.
#   * `at_rest_encryption_enabled
#       = true`                   — encryption at rest.
#   * `transit_encryption_enabled
#       = true`                   — TLS in transit (REQUIRED for
#                                   auth-token).
#   * `auth_token_enabled = true` — Redis AUTH token. The token
#                                   is a 32-char random string,
#                                   generated by the module +
#                                   surfaced as a sensitive
#                                   output. Operator pushes it
#                                   to SSM (split-parameter
#                                   shape — see
#                                   `docs/redis-cluster.md` §3
#                                   + the new W11 §11 walk-
#                                   through for the prod-
#                                   specific procedure).

module "redis" {
  source = "../../modules/redis"

  environment = local.environment
  common_tags = local.common_tags

  vpc_id                     = var.vpc_id
  private_subnet_ids         = var.private_subnet_ids
  vpc_cidr                   = var.vpc_cidr
  allowed_security_group_ids = var.eks_worker_security_group_ids

  # Prod shape.
  node_type                = var.redis_node_type
  replica_count            = var.redis_replica_count
  multi_az_enabled         = true
  snapshot_retention_limit = 7
  snapshot_window          = "03:00-05:00"
  maintenance_window       = "sun:05:00-sun:07:00"

  # Security defaults — at-rest + transit + AUTH all ON in prod.
  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  auth_token_enabled         = true

  # Customer-managed KMS key for at-rest encryption — pass the
  # alias (or ARN) of the prod ElastiCache KMS key. Empty string
  # is the AWS-managed default; the operator runbook
  # (`docs/redis-cluster.md` §4) walks the CMK rotation flow.
  kms_key_id = var.redis_kms_key_id

  # Apply-immediately is OFF in prod (changes wait for the
  # Sunday maintenance window unless the operator explicitly
  # flips this via a one-off `-var` override during an
  # incident).
  apply_immediately = false
}
