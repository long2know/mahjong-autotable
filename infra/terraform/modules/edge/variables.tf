# Phase K Wave 7 — Apone (DevOps).
#
# Variable surface for the `edge/` module — Route 53 + ACM + WAFv2 +
# Athena/S3 logging, with optional CloudFront wiring.
#
# This module is INSTANTIATED FROM the primary env (e.g. add a
# `module "edge"` block in `infra/terraform/main.tf` once
# `mahjong.example.com` is registered). Module is single-region —
# WAFv2 web ACLs scoped to `REGIONAL` MUST live in the same region
# as the resource they protect (the ALB / API Gateway). ACM certs
# for CloudFront MUST live in us-east-1 (AWS constraint) — the
# `cloudfront_certificate_region_alias` provider is plumbed through
# for that case.
#
# Two AWS provider aliases:
#
#   * default      — the regional resources (WAFv2 ACL, Athena
#                    workgroup, S3 bucket, Route 53 records).
#   * us_east_1    — REQUIRED for the CloudFront-attached ACM cert
#                    (AWS constraint: CloudFront ACM certs MUST live
#                    in us-east-1 regardless of the CloudFront
#                    distribution's edge geography).

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # configuration_aliases REQUIRED so callers know to pass both
      # provider blocks (default + us_east_1). us_east_1 only used
      # for the CloudFront cert when cloudfront.enabled = true.
      configuration_aliases = [aws.us_east_1]
      version               = "~> 5.50"
    }
  }
}

variable "environment" {
  description = "Environment tag (e.g. `prod`, `staging`). Drives resource tags + name suffixes."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,30}$", var.environment))
    error_message = "environment must be lower-case alphanumeric + hyphen, 2-31 chars."
  }
}

variable "region" {
  description = "Primary AWS region — the regional WAFv2 ACL + Route 53 records + S3 logs bucket live here. Defaults to us-east-1."
  type        = string
  default     = "us-east-1"
}

variable "common_tags" {
  description = "Tags merged onto every resource. The module appends `Module=edge` + `Wave=phase-k-wave-7`."
  type        = map(string)
  default     = {}
}

# ── DNS ──────────────────────────────────────────────────────────

variable "domain_name" {
  description = "Apex domain name (e.g. `mahjong.example.com`). MUST be a domain you own + can prove via DNS validation."
  type        = string

  validation {
    # Reject trailing dots + ensure the value looks like an FQDN.
    condition     = can(regex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", var.domain_name))
    error_message = "domain_name must be a lowercase FQDN without trailing dot (e.g. `mahjong.example.com`)."
  }
}

variable "create_hosted_zone" {
  description = "If true, this module creates the Route 53 hosted zone for `domain_name`. Set false when the operator pre-creates the zone (registrars often pre-create on domain registration). When false, the operator MUST supply `existing_hosted_zone_id`."
  type        = bool
  default     = true
}

variable "existing_hosted_zone_id" {
  description = "Existing Route 53 hosted zone ID — used when `create_hosted_zone = false`. Required in that case; ignored when create_hosted_zone = true."
  type        = string
  default     = ""
}

variable "alb_dns_name" {
  description = "DNS name of the ALB/NLB fronting the cluster (e.g. `mahjong-1234567.us-east-1.elb.amazonaws.com`). The module creates an ALIAS A record pointing the apex at this load balancer. Set to empty string to skip the alias record (useful when CloudFront fronts the apex instead)."
  type        = string
  default     = ""
}

variable "alb_zone_id" {
  description = "Route 53 hosted-zone ID OF THE ALB (NOT the apex zone). For an ALB this is the canonical-hosted-zone id returned by `aws elbv2 describe-load-balancers`. Required when `alb_dns_name` is set."
  type        = string
  default     = ""
}

# ── ACM ──────────────────────────────────────────────────────────

variable "additional_subject_alt_names" {
  description = "Additional Subject Alternative Names on the ACM cert (e.g. `[\"www.mahjong.example.com\", \"api.mahjong.example.com\"]`). The apex is always included."
  type        = list(string)
  default     = []
}

# ── WAFv2 ────────────────────────────────────────────────────────

variable "waf_managed_rule_groups" {
  description = <<-EOT
    AWS managed rule groups to enable on the WAFv2 web ACL. Each entry
    is `{ name = "<vendor name>", priority = <int>, count_only = <bool> }`.
    `count_only` puts the rule in count mode (no block) — useful for
    canarying a new rule before enforcing.
    Defaults to AWS Common, Known Bad Inputs, and Amazon IP reputation
    (the W7 baseline).
  EOT
  type = list(object({
    name       = string
    priority   = number
    count_only = bool
  }))
  default = [
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

variable "waf_rate_limit_per_5min" {
  description = "Per-IP rate limit on the WAF — requests per 5-minute window. W7 baseline = 1000. Bump higher for prod once Hudson's load profile lands."
  type        = number
  default     = 1000

  validation {
    # WAFv2 rate-based rule constraints (AWS docs): min 100, max 20,000,000.
    condition     = var.waf_rate_limit_per_5min >= 100 && var.waf_rate_limit_per_5min <= 20000000
    error_message = "waf_rate_limit_per_5min must be between 100 and 20,000,000 (AWS WAFv2 constraint)."
  }
}

# ── Logging (S3 + Athena) ────────────────────────────────────────

variable "logs_bucket_name" {
  description = "S3 bucket name receiving WAF logs. Defaults to `aws-waf-logs-<environment>-<domain-segment>` — the `aws-waf-logs-` prefix is REQUIRED by AWS (WAF will reject any bucket without it)."
  type        = string
  default     = ""
}

variable "logs_retention_days" {
  description = "Lifecycle expiry for WAF logs in days. Default 90 — long enough for incident-response, short enough to keep storage cost bounded."
  type        = number
  default     = 90

  validation {
    condition     = var.logs_retention_days >= 7 && var.logs_retention_days <= 3653
    error_message = "logs_retention_days must be between 7 (a forensic minimum) and 3653 (~10y, S3 max)."
  }
}

variable "athena_workgroup_name" {
  description = "Athena workgroup name — analysts query WAF logs here. Defaults to `mahjong-edge-<environment>`."
  type        = string
  default     = ""
}

# ── Optional CloudFront wiring ────────────────────────────────────

variable "cloudfront" {
  description = <<-EOT
    Optional CloudFront distribution wiring. When `enabled = true`, the
    module:

      * Provisions an ACM cert in us-east-1 (AWS constraint).
      * Creates a CloudFront distribution with origin = `origin_domain_name`.
      * Attaches the WAFv2 ACL (CloudFront-scope) to the distribution.
      * Points the Route 53 record at the CloudFront distribution INSTEAD
        of the ALB (`alb_dns_name` is ignored when cloudfront.enabled).

    When `enabled = false` (default), CloudFront is skipped — the
    apex points at the ALB directly. This is the W7 baseline shape.
  EOT
  type = object({
    enabled                  = bool
    origin_domain_name       = string
    price_class              = string
    minimum_protocol_version = string
  })
  default = {
    enabled                  = false
    origin_domain_name       = ""
    price_class              = "PriceClass_100"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  validation {
    condition = !var.cloudfront.enabled || (
      contains(["PriceClass_100", "PriceClass_200", "PriceClass_All"], var.cloudfront.price_class)
    )
    error_message = "cloudfront.price_class must be one of PriceClass_100, PriceClass_200, PriceClass_All."
  }
}
