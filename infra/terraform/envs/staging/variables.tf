# Phase K Wave 8 — Apone (DevOps).
#
# Variable surface for the staging edge stack.

variable "region" {
  description = "Primary AWS region — staging's regional WAFv2 ACL + Route 53 records + S3 logs bucket live here. Default us-east-1 matches the primary stack."
  type        = string
  default     = "us-east-1"
}

# ── DNS / ACM ────────────────────────────────────────────────────

variable "domain_name" {
  description = "Apex domain for staging (e.g. `staging.mahjong.example.com`). Operator MUST own it + be able to prove via DNS validation."
  type        = string
  default     = "staging.mahjong.example.com"

  validation {
    condition     = can(regex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", var.domain_name))
    error_message = "domain_name must be a lowercase FQDN without trailing dot."
  }
}

variable "create_hosted_zone" {
  description = "If true, this stack creates the Route 53 hosted zone for `domain_name`. Default true — staging tends to be a subdomain the operator pre-delegates from the apex registrar. Set false when the apex `mahjong.example.com` zone already exists and you want to record `staging.*` as a record in it rather than carving a child zone."
  type        = bool
  default     = true
}

variable "existing_hosted_zone_id" {
  description = "Existing Route 53 hosted zone ID — used when `create_hosted_zone = false`. Ignored otherwise."
  type        = string
  default     = ""
}

variable "additional_subject_alt_names" {
  description = "Additional SANs on the ACM cert (e.g. `[\"api.staging.mahjong.example.com\"]`). The apex `domain_name` is always included by the edge module."
  type        = list(string)
  default     = []
}

# ── ALB binding ──────────────────────────────────────────────────

variable "alb_dns_name" {
  description = "DNS name of the staging EKS ingress ALB (output of `kubectl -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'`). The edge module creates an ALIAS A record from `domain_name` to this ALB. Set to empty string ONLY if you fronting the apex via CloudFront instead — staging baseline is ALB-direct."
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
  description = "Per-IP rate limit on the WAF rate-based rule (requests per 5-minute window). 1000 is the W8 staging baseline — matches prod so the rule's behaviour is comparable."
  type        = number
  default     = 1000

  validation {
    condition     = var.waf_rate_limit_per_5min >= 100 && var.waf_rate_limit_per_5min <= 20000000
    error_message = "waf_rate_limit_per_5min must be between 100 and 20,000,000 (AWS WAFv2 constraint)."
  }
}

# ── Logging ──────────────────────────────────────────────────────

variable "logs_retention_days" {
  description = "Lifecycle expiry for WAF logs on the staging S3 bucket. Shorter than prod (90d) because staging traffic is synthetic + the soak window is short; the W8 baseline is 30 days."
  type        = number
  default     = 30

  validation {
    condition     = var.logs_retention_days >= 7 && var.logs_retention_days <= 3653
    error_message = "logs_retention_days must be between 7 and 3653 (S3 lifecycle range)."
  }
}
