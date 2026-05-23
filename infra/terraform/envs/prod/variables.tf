# Phase K Wave 11 — Apone (DevOps).
#
# Variable surface for the production edge + Redis env stack.
# Mirrors `envs/staging/variables.tf` with prod-tier defaults
# (BLOCK-mode WAF, multi-AZ Redis, 90-day log retention).

variable "region" {
  description = "Primary AWS region — prod's regional WAFv2 ACL + Route 53 records + S3 logs bucket + ElastiCache cluster live here. Default us-east-1 matches the primary stack."
  type        = string
  default     = "us-east-1"
}

# ── DNS / ACM ────────────────────────────────────────────────────

variable "domain_name" {
  description = "Apex domain for production (e.g. `mahjong.example.com`). Operator MUST own it + be able to prove via DNS validation."
  type        = string
  default     = "mahjong.example.com"

  validation {
    condition     = can(regex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", var.domain_name))
    error_message = "domain_name must be a lowercase FQDN without trailing dot."
  }
}

variable "create_hosted_zone" {
  description = "If true, this stack creates the Route 53 hosted zone for `domain_name`. Default false for prod — the apex zone typically pre-exists (registrar-delegated) and the operator records `mahjong.*` directly in it. Flip to true ONLY for a fresh apex bring-up."
  type        = bool
  default     = false
}

variable "existing_hosted_zone_id" {
  description = "Existing Route 53 hosted zone ID — required when `create_hosted_zone = false`. Ignored otherwise."
  type        = string
  default     = ""
}

variable "additional_subject_alt_names" {
  description = "Additional SANs on the ACM cert (e.g. `[\"api.mahjong.example.com\", \"www.mahjong.example.com\"]`). The apex `domain_name` is always included by the edge module."
  type        = list(string)
  default     = []
}

# ── ALB binding ──────────────────────────────────────────────────

variable "alb_dns_name" {
  description = "DNS name of the prod EKS ingress ALB (output of `kubectl -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'`). The edge module creates an ALIAS A record from `domain_name` to this ALB. Set to empty string ONLY if fronting the apex via CloudFront instead — prod baseline is ALB-direct."
  type        = string
  default     = ""
}

variable "alb_zone_id" {
  description = "Route 53 hosted-zone ID of the ALB (canonical-hosted-zone id from `aws elbv2 describe-load-balancers`). For us-east-1 application load balancers this is `Z35SXDOTRQ7X7K`. Required when `alb_dns_name` is set."
  type        = string
  default     = ""
}

# ── WAF tuning ───────────────────────────────────────────────────

variable "waf_rate_limit_per_5min" {
  description = "Per-IP rate limit on the WAF rate-based rule (requests per 5-minute window). 1000 is the W11 prod baseline — matches the staging soak period so the rule's behaviour is comparable."
  type        = number
  default     = 1000

  validation {
    condition     = var.waf_rate_limit_per_5min >= 100 && var.waf_rate_limit_per_5min <= 20000000
    error_message = "waf_rate_limit_per_5min must be between 100 and 20,000,000 (AWS WAFv2 constraint)."
  }
}

# ── CloudFront fronting (opt-in) ─────────────────────────────────

variable "cloudfront_enabled" {
  description = "Toggle CloudFront fronting. Default false — prod ships ALB-direct at W11. Flip to true ONLY when the squad has signed off on the cache-invalidation runbook + CDN cost."
  type        = bool
  default     = false
}

variable "cloudfront_origin_domain_name" {
  description = "Origin DNS for CloudFront (typically the ALB DNS). Required when `cloudfront_enabled = true`."
  type        = string
  default     = ""
}

variable "cloudfront_price_class" {
  description = "CloudFront price class (`PriceClass_100` = US/EU only, `PriceClass_200` = + Asia, `PriceClass_All` = global). Default `PriceClass_100`."
  type        = string
  default     = "PriceClass_100"

  validation {
    condition     = contains(["PriceClass_100", "PriceClass_200", "PriceClass_All"], var.cloudfront_price_class)
    error_message = "cloudfront_price_class must be one of PriceClass_100, PriceClass_200, PriceClass_All."
  }
}

# ── Logging ──────────────────────────────────────────────────────

variable "logs_retention_days" {
  description = "Lifecycle expiry for WAF logs on the prod S3 bucket. 90 days matches the SOC-2 / audit retention baseline in `docs/audits/`."
  type        = number
  default     = 90

  validation {
    condition     = var.logs_retention_days >= 7 && var.logs_retention_days <= 3653
    error_message = "logs_retention_days must be between 7 and 3653 (S3 lifecycle range)."
  }
}

# ── Redis VPC wiring (W11) ───────────────────────────────────────
#
# The Redis module needs VPC + private subnet IDs + the VPC CIDR
# from the PRIMARY stack. Plumbed through tfvars (operator copies
# the values from `terraform output` against the primary stack)
# rather than via terraform_remote_state — keeps the env stack
# dependency-free at `terraform init` time and lets the operator
# run the Redis apply without granting cross-state read access.

variable "vpc_id" {
  description = "VPC ID from the primary stack (`terraform output -raw vpc_id` against `infra/terraform/`). Required for the Redis security group."
  type        = string

  validation {
    condition     = can(regex("^vpc-[a-f0-9]{8,17}$", var.vpc_id))
    error_message = "vpc_id must look like `vpc-…` (8-17 hex chars after the prefix)."
  }
}

variable "private_subnet_ids" {
  description = "Private subnet IDs from the primary stack (`terraform output -json private_subnet_ids`). REQUIRED to have ≥ 2 subnets in distinct AZs for prod multi-AZ Redis."
  type        = list(string)

  validation {
    condition     = length(var.private_subnet_ids) >= 2
    error_message = "private_subnet_ids must contain at least 2 subnet IDs in distinct AZs (ElastiCache multi-AZ constraint)."
  }
}

variable "vpc_cidr" {
  description = "VPC CIDR from the primary stack (`terraform output -raw vpc_cidr`). Used as the broad ingress CIDR on the Redis security group (fallback when `eks_worker_security_group_ids` is empty)."
  type        = string
  default     = "10.0.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "vpc_cidr must be a valid CIDR block (e.g. `10.0.0.0/16`)."
  }
}

variable "eks_worker_security_group_ids" {
  description = "Security group IDs of the EKS worker nodes (`terraform output -json eks_worker_security_group_ids` against the primary stack). Preferred over the broad VPC-CIDR ingress for least-privilege — the Redis SG admits only EKS workers on 6379. Default empty list (fall back to VPC-CIDR ingress)."
  type        = list(string)
  default     = []
}

# ── Redis cluster shape (W11 prod tier) ──────────────────────────

variable "redis_node_type" {
  description = "ElastiCache node type for prod. Default `cache.r6g.large` — production-tier shape from the W10 load-test baseline (`docs/load-test-results.md`). Bump to `cache.r6g.xlarge` if the W11+ load test surfaces sustained eviction pressure (the `Evictions` CloudWatch metric is the trigger)."
  type        = string
  default     = "cache.r6g.large"

  validation {
    condition     = can(regex("^cache\\.[a-z0-9]+\\.[a-z0-9]+$", var.redis_node_type))
    error_message = "redis_node_type must look like `cache.<family>.<size>` (e.g. `cache.r6g.large`)."
  }
}

variable "redis_replica_count" {
  description = "Number of Redis replica nodes (in addition to the primary). Default 1 — one replica in a second AZ for automatic failover. Bump to 2 if read fan-out becomes a concern (the W10 IdempotencyStore is write-heavy → 1 replica is the sweet spot at W11)."
  type        = number
  default     = 1

  validation {
    condition     = var.redis_replica_count >= 1 && var.redis_replica_count <= 5
    error_message = "redis_replica_count must be between 1 and 5 in prod (multi-AZ requires ≥ 1 replica)."
  }
}

variable "redis_kms_key_id" {
  description = "KMS CMK alias / ARN for the at-rest encryption on the prod Redis cluster. Empty string = AWS-managed `alias/aws/elasticache` (default). For SOC-2 / customer-managed-key compliance, set to the prod ElastiCache CMK (e.g. `alias/mahjong-prod-elasticache`)."
  type        = string
  default     = ""
}

# ── Multi-region endpoints (Phase K Wave 12) ─────────────────────
#
# Wires the W12 per-region R53 records + health checks in the edge
# module. Empty list (default) preserves the W11 single-ALB apex
# behaviour — operator opts in once the regional EKS clusters are
# stood up.
#
# Operator-supplied per-region ALB DNS + zone IDs are typically
# captured from each regional cluster's `kubectl -n ingress-nginx
# get svc ingress-nginx-controller` output (DNS) + `aws elbv2
# describe-load-balancers` (zone ID).
#
# See `docs/edge-region-probes.md §3` for the W12 hand-off
# walkthrough.

variable "regional_endpoints" {
  description = "Per-region endpoint config for the W12 multi-region latency-based apex RR set + per-region health checks. Empty list (default) skips the multi-region wiring — apex stays on the single-ALB ALIAS from W11."
  type = list(object({
    region       = string
    hostname     = string
    alb_dns_name = string
    alb_zone_id  = string
  }))
  default = []
}
