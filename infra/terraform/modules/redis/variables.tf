# Phase K Wave 10 — Apone (DevOps).
#
# Variable surface for the `redis/` module — AWS ElastiCache Redis
# cluster (multi-AZ replication group), purpose-built for the
# Wave-10 Bishop `RedisIdempotencyStore` runtime backing store.
#
# Module style mirrors `modules/edge/` + `modules/dr-replication/`:
#   * Single `aws` provider (no us-east-1 alias — ElastiCache is
#     fully regional, no cross-region wiring at the module level).
#   * `environment` + `common_tags` carried through every resource.
#   * Module appends `Module = "redis"` to common_tags so a
#     resource-graph query can scope by module without grepping
#     the resource name.
#
# Variables are split into four groups (matching the resource
# sections in main.tf):
#
#   1. Identity + tagging.
#   2. Networking (VPC + subnet wiring + allowed ingress CIDRs).
#   3. Cluster shape (node type, replica count, multi-AZ,
#      automatic-failover, parameter group, maintenance windows).
#   4. Security (encryption at rest + in transit, auth token).

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

# ── 1. Identity + tagging ────────────────────────────────────────

variable "environment" {
  description = "Environment tag (e.g. `prod`, `staging`). Drives resource tags + name suffixes. Lower-case alphanumeric + hyphen, 2-31 chars."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,30}$", var.environment))
    error_message = "environment must be lower-case alphanumeric + hyphen, 2-31 chars."
  }
}

variable "common_tags" {
  description = "Tags merged onto every resource. The module appends `Module = redis` + `Wave = phase-k-wave-10`."
  type        = map(string)
  default     = {}
}

# ── 2. Networking ────────────────────────────────────────────────

variable "vpc_id" {
  description = "VPC the ElastiCache cluster lives in (typically `module.primary.vpc_id` when this module is wired from an env stack). Used to scope the security group."
  type        = string

  validation {
    condition     = can(regex("^vpc-[a-f0-9]{8,17}$", var.vpc_id))
    error_message = "vpc_id must look like `vpc-…` (8-17 hex chars after the prefix)."
  }
}

variable "private_subnet_ids" {
  description = "Private subnet IDs (typically `module.primary.private_subnet_ids`) the cache subnet group spans. MUST be ≥ 2 subnets in distinct AZs when `multi_az_enabled = true` (AWS constraint)."
  type        = list(string)

  validation {
    condition     = length(var.private_subnet_ids) >= 2
    error_message = "private_subnet_ids must contain at least 2 subnet IDs in distinct AZs (ElastiCache multi-AZ constraint)."
  }
}

variable "vpc_cidr" {
  description = "CIDR block of the VPC. The module security group admits Redis port (6379) ingress from this CIDR only — keeps the cache reachable from EKS workers + RDS without exposing it to the public NAT egress range."
  type        = string

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "vpc_cidr must be a valid CIDR block (e.g. `10.0.0.0/16`)."
  }
}

variable "allowed_security_group_ids" {
  description = "Additional security group IDs whose members may connect to Redis on 6379. Pass the EKS worker-node SG ID here to lock ingress to cluster-workload sources only (preferred over the VPC-CIDR ingress rule for least privilege). Defaults to empty — the VPC-CIDR rule is the broad fallback."
  type        = list(string)
  default     = []
}

# ── 3. Cluster shape ─────────────────────────────────────────────

variable "node_type" {
  description = "ElastiCache node type (e.g. `cache.t4g.micro`, `cache.t4g.small`, `cache.r7g.large`). Default `cache.t4g.small` fits the W10 baseline (single-AZ staging + multi-AZ prod with 1 primary + 1 replica). For high-throughput prod, bump to `cache.r7g.large` or larger."
  type        = string
  default     = "cache.t4g.small"

  validation {
    condition     = can(regex("^cache\\.[a-z0-9]+\\.[a-z0-9]+$", var.node_type))
    error_message = "node_type must look like `cache.<family>.<size>` (e.g. `cache.t4g.small`)."
  }
}

variable "replica_count" {
  description = "Number of replica nodes per shard (in addition to the primary). 1 is the W10 baseline for prod — one replica in a second AZ for automatic failover. Set to 0 in staging to halve the cost. Range: 0-5 (ElastiCache constraint)."
  type        = number
  default     = 1

  validation {
    condition     = var.replica_count >= 0 && var.replica_count <= 5
    error_message = "replica_count must be between 0 and 5 (ElastiCache constraint)."
  }
}

variable "multi_az_enabled" {
  description = "Multi-AZ failover support. When true, replicas land in a DIFFERENT AZ from the primary and automatic_failover_enabled is forced true. Requires `replica_count ≥ 1` (AWS constraint). Default true for prod-grade defaults — flip to false in staging to halve the cost."
  type        = bool
  default     = true
}

variable "engine_version" {
  description = "Redis engine version. Pin to the LTS-equivalent; 7.x is the W10 baseline. Bumping requires a maintenance-window reboot — schedule via the operator runbook (`docs/redis-cluster.md` §5)."
  type        = string
  default     = "7.1"

  validation {
    condition     = can(regex("^[0-9]+\\.[0-9]+$", var.engine_version))
    error_message = "engine_version must look like `<major>.<minor>` (e.g. `7.1`)."
  }
}

variable "parameter_group_family" {
  description = "ElastiCache parameter-group family. MUST match `engine_version` major (AWS constraint). Default `redis7` matches `engine_version = 7.x`."
  type        = string
  default     = "redis7"
}

variable "parameter_overrides" {
  description = "Map of Redis CONFIG keys to override on the parameter group. Defaults: `maxmemory-policy = allkeys-lru` (the Wave-10 IdempotencyStore eviction semantics — least-recently-used keys evicted under pressure rather than OOM). Extend with any prod-tuning needed (`tcp-keepalive`, `timeout`, etc.)."
  type        = map(string)
  default = {
    "maxmemory-policy" = "allkeys-lru"
  }
}

variable "snapshot_retention_limit" {
  description = "Number of daily snapshots retained. 7 = 1 week (W10 prod baseline). Set to 0 to disable snapshots entirely (staging baseline — idempotency keys are 5-min TTL, snapshots are not security-relevant)."
  type        = number
  default     = 7

  validation {
    condition     = var.snapshot_retention_limit >= 0 && var.snapshot_retention_limit <= 35
    error_message = "snapshot_retention_limit must be between 0 (disabled) and 35 (ElastiCache constraint)."
  }
}

variable "snapshot_window" {
  description = "Daily UTC window during which ElastiCache takes snapshots, in `hh:mm-hh:mm` (must be ≥ 60 min). Default `03:00-05:00` — off-peak for US/EU traffic."
  type        = string
  default     = "03:00-05:00"

  validation {
    condition     = can(regex("^[0-2][0-9]:[0-5][0-9]-[0-2][0-9]:[0-5][0-9]$", var.snapshot_window))
    error_message = "snapshot_window must look like `hh:mm-hh:mm` (24h UTC)."
  }
}

variable "maintenance_window" {
  description = "Weekly UTC window during which ElastiCache applies upgrades, in `ddd:hh:mm-ddd:hh:mm` (must be ≥ 60 min). Default `sun:05:00-sun:07:00` — Sunday off-peak."
  type        = string
  default     = "sun:05:00-sun:07:00"

  validation {
    condition     = can(regex("^[a-z]{3}:[0-2][0-9]:[0-5][0-9]-[a-z]{3}:[0-2][0-9]:[0-5][0-9]$", var.maintenance_window))
    error_message = "maintenance_window must look like `ddd:hh:mm-ddd:hh:mm` (24h UTC; ddd is `sun`, `mon`, …)."
  }
}

# ── 4. Security ──────────────────────────────────────────────────

variable "at_rest_encryption_enabled" {
  description = "Encrypt the snapshot + replication-group data at rest. Default true. Disabling is operator-explicit — at-rest encryption is free on ElastiCache."
  type        = bool
  default     = true
}

variable "transit_encryption_enabled" {
  description = "TLS in transit between the client and the cluster (port 6379 → TLS-wrapped). REQUIRED to use an auth token. Default true. Client (`StackExchange.Redis` etc.) MUST set `ssl=True` to connect."
  type        = bool
  default     = true
}

variable "auth_token_enabled" {
  description = "If true, the module generates a 32-char random auth token (Redis AUTH) and surfaces it as a sensitive output. Requires `transit_encryption_enabled = true` (AWS constraint). Default true. ESO consumes the output value into a k8s Secret on the runtime side (see `docs/redis-cluster.md` §6)."
  type        = bool
  default     = true
}

variable "kms_key_id" {
  description = "KMS CMK ID/ARN used for at-rest encryption. Empty string = use the AWS-managed `alias/aws/elasticache` key (default). Set to a CMK ARN for customer-managed key (operator runbook in `docs/redis-cluster.md` §4)."
  type        = string
  default     = ""
}

variable "apply_immediately" {
  description = "Apply parameter / size changes IMMEDIATELY rather than at the next maintenance window. Set true ONLY when the operator is comfortable with a potential failover during apply. Default false (safer for prod)."
  type        = bool
  default     = false
}
