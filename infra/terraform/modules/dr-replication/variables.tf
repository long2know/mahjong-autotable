# Phase K Wave 6 — Apone (DevOps).
#
# Variable surface for the DR replication module. The module is
# instantiated by the secondary-region env (`envs/dr-us-west-2/`)
# and reads/writes resources in BOTH the primary and secondary
# regions via two aliased AWS providers (configuration_aliases
# below).
#
# The module is intentionally NARROW — it does NOT re-provision the
# secondary-region VPC / EKS / RDS / ECR (those are the secondary
# env's `main.tf` problem); it only stitches CROSS-region resources
# (read replica, ECR replication rule, Route 53 health check +
# failover record).

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # Two providers under aliases so the module can address both
      # regions without the caller having to plumb provider blocks
      # through every resource. `aws.primary` is us-east-1 by
      # convention; `aws.secondary` is the DR region.
      configuration_aliases = [aws.primary, aws.secondary]
      version               = "~> 5.50"
    }
  }
}

variable "primary_region" {
  description = "Primary (active) AWS region — the source for RDS read-replication, ECR push, and the PRIMARY Route 53 record."
  type        = string
  default     = "us-east-1"
}

variable "secondary_region" {
  description = "Secondary (DR) AWS region — the read-replica target, the ECR pull-through destination, and the SECONDARY Route 53 record."
  type        = string
  default     = "us-west-2"
}

variable "environment" {
  description = "Environment tag applied to every cross-region resource (matches the env name of the secondary stack, e.g. `dr-us-west-2`)."
  type        = string
}

# ── RDS read replica ──────────────────────────────────────────────

variable "primary_db_arn" {
  description = "ARN of the PRIMARY-region RDS Postgres instance to replicate from. The primary must have `backup_retention_period > 0` for cross-region replication to be eligible."
  type        = string
}

variable "primary_db_kms_key_arn" {
  description = "KMS key ARN that encrypts the primary DB. RDS cross-region replicas need their OWN KMS key in the secondary region (you cannot re-use the primary's key); this value is logged for audit but the replica uses `kms_key_id` (secondary KMS) below."
  type        = string
}

variable "replica_kms_key_arn" {
  description = "Secondary-region KMS key ARN to encrypt the replica storage at rest. Provisioned in the secondary env's `main.tf` and passed in here."
  type        = string
}

variable "replica_instance_class" {
  description = "RDS instance class for the read replica. DR is warm-standby in W6 (smaller than prod is fine); promotion-time we scale up via `aws rds modify-db-instance --apply-immediately`."
  type        = string
  default     = "db.t4g.small"
}

variable "replica_subnet_group_name" {
  description = "DB subnet group name in the secondary region. Pre-provisioned by the secondary env stack."
  type        = string
}

variable "replica_vpc_security_group_ids" {
  description = "VPC security group IDs (secondary region) the replica binds to. Provisioned by the secondary env's `main.tf`."
  type        = list(string)
}

variable "replica_backup_retention_days" {
  description = "Backup retention for the replica. 7 days mirrors the prod floor so a promoted replica is immediately backup-protected."
  type        = number
  default     = 7
}

variable "replica_deletion_protection" {
  description = "Whether the replica has deletion protection. true for DR-prod, false for DR-staging."
  type        = bool
  default     = true
}

# ── ECR cross-region replication ──────────────────────────────────

variable "ecr_repository_filter" {
  description = "ECR repository name pattern to replicate (prefix match per AWS replication-rule semantics). Defaults to `mahjong-autotable` so only the app image replicates, not every repo in the account."
  type        = string
  default     = "mahjong-autotable"
}

# ── Route 53 failover ─────────────────────────────────────────────

variable "hosted_zone_id" {
  description = "Route 53 hosted zone ID for the application domain. Pre-existing — Route 53 is operator-provisioned per `docs/cloudflare.md` / DNS runbook."
  type        = string
}

variable "failover_record_name" {
  description = "DNS record name for the failover pair (e.g. `mahjong.example.com`). The PRIMARY + SECONDARY records share this name and differ only by `set_identifier` + `failover_routing_policy`."
  type        = string
}

variable "failover_record_ttl" {
  description = "TTL on the failover record. Spec requires <60s so a Route 53 failover propagates to clients within a minute; we default to 30s (Route 53 health-check default eval is 30s × 3 = 90s anyway, so a tighter TTL just front-loads the recursive-resolver cache flush)."
  type        = number
  default     = 30

  validation {
    condition     = var.failover_record_ttl < 60
    error_message = "failover_record_ttl must be < 60s per the W6 DR rehearsal spec (otherwise clients see >1 min outage on failover)."
  }
}

variable "primary_target_dns" {
  description = "DNS name of the PRIMARY-region load balancer / endpoint that the failover record points at when healthy."
  type        = string
}

variable "secondary_target_dns" {
  description = "DNS name of the SECONDARY-region load balancer / endpoint that takes over on failover."
  type        = string
}

variable "primary_health_check_path" {
  description = "HTTPS path Route 53 health-checks against on the PRIMARY target. Bishop's `/health` endpoint (200 = ok) is the canonical signal."
  type        = string
  default     = "/health"
}

variable "primary_health_check_port" {
  description = "Port Route 53 health-checks against. 443 = the public TLS terminator (ALB / Cloudflare); we deliberately do NOT health-check the cluster API directly (private)."
  type        = number
  default     = 443
}

variable "common_tags" {
  description = "Common resource tags propagated to every resource the module creates (in addition to the per-resource `Name` tag)."
  type        = map(string)
  default     = {}
}
