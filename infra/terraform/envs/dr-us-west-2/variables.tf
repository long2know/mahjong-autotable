# Phase K Wave 6 — Apone (DevOps).
#
# DR env variable surface. Defaults reflect the canonical
# us-east-1 → us-west-2 pair; override per-account.

variable "primary_region" {
  description = "Primary AWS region. The single-region prod stack lives here."
  type        = string
  default     = "us-east-1"
}

variable "secondary_region" {
  description = "Secondary (DR) AWS region. This env's resources are provisioned here."
  type        = string
  default     = "us-west-2"
}

variable "vpc_cidr" {
  description = "Secondary VPC CIDR. MUST NOT overlap with the primary VPC's 10.0.0.0/16 — Wave-6 spec pins 10.1.0.0/16 so future VPC peering / TGW works without renumbering."
  type        = string
  default     = "10.1.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0)) && substr(var.vpc_cidr, length(var.vpc_cidr) - 3, 3) == "/16"
    error_message = "vpc_cidr must be a valid /16 CIDR."
  }

  validation {
    condition     = var.vpc_cidr != "10.0.0.0/16"
    error_message = "DR VPC CIDR must NOT overlap with the primary VPC (10.0.0.0/16). Spec: 10.1.0.0/16."
  }
}

variable "primary_state_bucket" {
  description = "S3 bucket holding the PRIMARY-region terraform state. Read by `terraform_remote_state` so DR knows the primary DB ARN + KMS without manual plumbing."
  type        = string
  default     = "mahjong-tfstate-prod"
}

variable "primary_state_key" {
  description = "S3 key (within `primary_state_bucket`) holding the primary stack's state file."
  type        = string
  default     = "infra/terraform/prod.tfstate"
}

# ── Replica sizing ─────────────────────────────────────────────────

variable "replica_instance_class" {
  description = "DR replica instance class. Smaller than prod by default (DR-warm) — operator scales up on promotion."
  type        = string
  default     = "db.t4g.small"
}

variable "replica_backup_retention_days" {
  description = "Backup retention on the replica. 7 days matches the prod floor."
  type        = number
  default     = 7
}

variable "replica_deletion_protection" {
  description = "Deletion protection on the replica. true for DR-prod (we don't want a midnight `terraform destroy` to wipe the replica)."
  type        = bool
  default     = true
}

# ── ECR replication ────────────────────────────────────────────────

variable "ecr_repository_filter" {
  description = "Prefix filter for ECR cross-region replication."
  type        = string
  default     = "mahjong-autotable"
}

# ── Route 53 failover ──────────────────────────────────────────────

variable "hosted_zone_id" {
  description = "Route 53 hosted zone ID — operator-provided. Cannot default safely (account-specific)."
  type        = string
}

variable "failover_record_name" {
  description = "FQDN of the failover record (e.g. `mahjong.example.com`)."
  type        = string
}

variable "failover_record_ttl" {
  description = "TTL on the failover record. <60s per spec; 30s default."
  type        = number
  default     = 30
}

variable "primary_target_dns" {
  description = "DNS name of the primary-region target (ALB / Cloudflare)."
  type        = string
}

variable "secondary_target_dns" {
  description = "DNS name of the secondary-region target (DR ALB; provisioned by `prod-dr` overlay once a promotion fires — until then, this can point at a placeholder)."
  type        = string
}
