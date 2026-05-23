# Phase K Wave 8 — Apone (DevOps).
#
# Staging-env stack — wires the W7 `modules/edge` module against
# the staging EKS ingress so the public-facing edge surface
# (Route 53, ACM, WAFv2 + S3/Athena logging, optional CloudFront)
# is provisioned alongside the primary stack.
#
# Layout choice: the staging EDGE lives in its own env stack (this
# file) rather than collapsing into `infra/terraform/main.tf`. The
# primary stack is the SoT for VPC + EKS + RDS + ECR + the GitHub-
# OIDC role; the edge surface depends on those outputs (notably
# the ingress controller's ALB DNS) so it MUST apply AFTER the
# primary cluster bootstrap completes (helm: aws-load-balancer-
# controller installs nginx-ingress → ALB DNS is published →
# THIS stack reads it via remote-state).
#
# Apply order:
#   1. `cd infra/terraform/` → primary stack apply with
#      `-var-file=staging.tfvars` (provisions VPC + EKS + RDS).
#   2. Install nginx-ingress via helm against the staging cluster
#      → ALB DNS published.
#   3. `cd infra/terraform/envs/staging/` → THIS stack:
#        cp backend.example.hcl backend.hcl
#        cp terraform.tfvars.example terraform.tfvars
#        # EDIT terraform.tfvars: domain_name, alb_dns_name (from
#        # `kubectl -n ingress-nginx get svc ingress-nginx-controller`),
#        # alb_zone_id.
#        terraform init -backend-config=backend.hcl
#        terraform plan
#        terraform apply
#
# Smoke test (see `docs/staging-cutover.md`):
#   curl https://staging.mahjong.example.com/healthz → 200 ok
#
# State backend: pre-create `mahjong-tfstate-staging` bucket +
# `mahjong-tflock-staging` DynamoDB table in us-east-1. Distinct
# from the primary state-bucket so an `apply` against THIS env
# can't accidentally mutate VPC / EKS / RDS state.

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
# `aws` (default, var.region — typically us-east-1).
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
  environment = "staging"

  common_tags = {
    "Project"     = "mahjong-autotable"
    "Environment" = local.environment
    "ManagedBy"   = "terraform"
    "Module"      = "envs/staging"
    "Wave"        = "phase-k-wave-10"
  }

  # WAF rule set for staging:
  #   * `count_only = true` on every managed group — staging is
  #     the test surface for the rule sets; production stays
  #     blocking. Wave 8 commitment: staging proves the rule sets
  #     don't false-positive on real traffic for one quarter, then
  #     prod flips count_only -> none (blocking).
  staging_waf_managed_rule_groups = [
    {
      name       = "AWSManagedRulesCommonRuleSet"
      priority   = 10
      count_only = true
    },
    {
      name       = "AWSManagedRulesKnownBadInputsRuleSet"
      priority   = 20
      count_only = true
    },
    {
      name       = "AWSManagedRulesAmazonIpReputationList"
      priority   = 30
      count_only = true
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

  # ALB binding (skip when the operator runs CloudFront-fronted —
  # but staging defaults are ALB-direct).
  alb_dns_name = var.alb_dns_name
  alb_zone_id  = var.alb_zone_id

  # WAF — count-only mode for staging (W8 cutover commitment).
  waf_managed_rule_groups = local.staging_waf_managed_rule_groups
  waf_rate_limit_per_5min = var.waf_rate_limit_per_5min
  logs_retention_days     = var.logs_retention_days

  # No CloudFront in staging — staging is a one-hop test surface,
  # CDN fixed cost not justified.
  cloudfront = {
    enabled                  = false
    origin_domain_name       = ""
    price_class              = "PriceClass_100"
    minimum_protocol_version = "TLSv1.2_2021"
  }
}

# ── Redis module (W10 — IdempotencyStore backing) ────────────────
#
# Bishop's W10 `RedisIdempotencyStore` runtime needs a Redis
# cluster reachable from the EKS workers. This module is wired
# against the PRIMARY stack's VPC + private subnets via remote
# state (the env-stack tfvars surface forwards the IDs).
#
# Staging shape: cheap (`cache.t4g.micro`, single-AZ, no replicas,
# no snapshots). The W10 IdempotencyStore tolerates a cold start
# (every entry has 5-min TTL), so the durability-cost trade-off
# is firmly in favour of single-AZ for staging.
#
# Prod shape (NOT this file — lives in `envs/prod/` once that env
# is stood up): `cache.t4g.small`, multi-AZ, 1 replica, 7-day
# snapshot retention. Operator runbook: `docs/redis-cluster.md`.

module "redis" {
  source = "../../modules/redis"

  environment = local.environment
  common_tags = local.common_tags

  vpc_id             = var.vpc_id
  private_subnet_ids = var.private_subnet_ids
  vpc_cidr           = var.vpc_cidr

  # Staging shape — single-AZ, no replicas, no snapshots.
  node_type                = "cache.t4g.micro"
  replica_count            = 0
  multi_az_enabled         = false
  snapshot_retention_limit = 0

  # Security defaults — TLS + auth-token ON even in staging so the
  # connection string shape matches prod (the W10 IdempotencyStore
  # code path is the same across envs; we want the auth surface
  # exercised in staging).
  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  auth_token_enabled         = true
}
