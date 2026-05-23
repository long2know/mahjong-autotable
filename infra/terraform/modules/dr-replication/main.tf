# Phase K Wave 6 — Apone (DevOps).
#
# DR replication module.
#
# Wires three cross-region resources onto the existing single-region
# stack (provisioned by `infra/terraform/` and the per-env `envs/`
# stack):
#
#   1. RDS Postgres cross-region read replica (primary → secondary).
#   2. ECR image replication rule (primary → secondary).
#   3. Route 53 PRIMARY + SECONDARY failover records bound to a
#      Route 53 health check pointed at the primary target.
#
# This module is INSTANTIATED FROM `envs/dr-us-west-2/main.tf`; the
# caller passes both `aws.primary` (us-east-1) and `aws.secondary`
# (us-west-2) provider aliases. Every resource here pins its provider
# explicitly — no fall-through to default — so a misconfigured caller
# fails fast on `terraform plan`.
#
# DR rehearsal cadence: quarterly per `docs/terraform.md` §DR rehearsal
# — `terraform apply` brings the warm pair up; failover is a manual
# `aws rds promote-read-replica` + Route 53 health-check force-fail
# (`aws route53 update-health-check --inverted`); recovery is the
# reverse. The module does NOT automate promotion; that's the
# operator's call.

locals {
  module_tags = merge(var.common_tags, {
    "Module"        = "dr-replication"
    "Wave"          = "phase-k-wave-6"
    "Environment"   = var.environment
    "PrimaryRegion" = var.primary_region
    "DRRegion"      = var.secondary_region
  })
}

# ── 1. RDS cross-region read replica ─────────────────────────────
#
# AWS limits: the source DB must have backup retention > 0 and the
# replica's KMS key MUST live in the secondary region (you cannot
# re-use the primary's CMK). The replica inherits engine + version
# + storage type from the source; we override instance class
# because DR-warm runs smaller than prod.
#
# `replicate_source_db` accepts an ARN (cross-region) OR a plain
# identifier (same-region). We always pass the full ARN so the
# provider auto-detects cross-region and routes via the secondary
# provider alias correctly.
resource "aws_db_instance" "replica" {
  provider = aws.secondary

  identifier             = "mahjong-${var.environment}-replica"
  replicate_source_db    = var.primary_db_arn
  instance_class         = var.replica_instance_class
  storage_encrypted      = true
  kms_key_id             = var.replica_kms_key_arn
  db_subnet_group_name   = var.replica_subnet_group_name
  vpc_security_group_ids = var.replica_vpc_security_group_ids
  publicly_accessible    = false
  deletion_protection    = var.replica_deletion_protection
  # Replicas don't accept `db_name` / `username` / `password` /
  # `engine` / `engine_version` — they inherit from the source.
  # Backup retention CAN be set independently so the replica can be
  # promoted into an autonomous primary without an outage window.
  backup_retention_period = var.replica_backup_retention_days
  backup_window           = "07:00-08:00"
  maintenance_window      = "Tue:08:00-Tue:09:00"
  skip_final_snapshot     = !var.replica_deletion_protection

  performance_insights_enabled = true
  monitoring_interval          = 60

  tags = merge(local.module_tags, {
    Name = "mahjong-${var.environment}-replica"
    Role = "dr-read-replica"
  })

  # Source DB's `password` is rotated out-of-band — ignore so plan
  # doesn't churn on every refresh.
  lifecycle {
    ignore_changes = [password]
  }
}

# ── 2. ECR cross-region replication ──────────────────────────────
#
# AWS ECR replication is account-level + region-level (not per-repo
# CRUD). The single `aws_ecr_replication_configuration` resource
# can hold up to 25 destination regions; we add ONE destination
# (the secondary region) and filter to the `mahjong-autotable`
# repository prefix so unrelated repos aren't shipped along.
#
# A no-op rule from the primary's perspective: pushing to the
# primary's ECR triggers an asynchronous copy into the secondary
# (typical lag 1-5 min). The secondary-region ECR repository is
# auto-created on the first replication event (we do NOT need to
# pre-create it in the secondary env).
resource "aws_ecr_replication_configuration" "this" {
  provider = aws.primary

  replication_configuration {
    rule {
      destination {
        region      = var.secondary_region
        registry_id = data.aws_caller_identity.primary.account_id
      }

      repository_filter {
        filter      = var.ecr_repository_filter
        filter_type = "PREFIX_MATCH"
      }
    }
  }
}

data "aws_caller_identity" "primary" {
  provider = aws.primary
}

# ── 3. Route 53 failover record + health check ───────────────────
#
# Route 53 failover routing requires:
#   * Two records with the same name + type, different `set_identifier`,
#     one tagged `failover_routing_policy.type = "PRIMARY"`, the other
#     `"SECONDARY"`.
#   * A health check associated with the PRIMARY record. When the
#     health check goes UNHEALTHY, Route 53 begins serving the
#     SECONDARY record. The SECONDARY can OPTIONALLY have its own
#     health check; we leave it un-health-checked so a DR region
#     with intermittent issues still serves traffic when the
#     primary is hard-down.
#   * TTL < 60s (we default to 30s — see the variable validator).
#
# The health check polls HTTPS / port 443 / `/health` by default;
# Bishop's `/health` endpoint returns 200 when the backend can talk
# to Postgres + Redis. Three failures (default interval × failure
# threshold = 30s × 3 = 90s) trip the failover.

resource "aws_route53_health_check" "primary" {
  provider = aws.primary

  fqdn              = var.primary_target_dns
  port              = var.primary_health_check_port
  type              = "HTTPS"
  resource_path     = var.primary_health_check_path
  failure_threshold = 3
  request_interval  = 30
  measure_latency   = true

  tags = merge(local.module_tags, {
    Name = "mahjong-${var.environment}-primary-health"
  })
}

resource "aws_route53_record" "primary" {
  provider = aws.primary

  zone_id        = var.hosted_zone_id
  name           = var.failover_record_name
  type           = "CNAME"
  ttl            = var.failover_record_ttl
  set_identifier = "primary-${var.primary_region}"
  records        = [var.primary_target_dns]

  failover_routing_policy {
    type = "PRIMARY"
  }

  health_check_id = aws_route53_health_check.primary.id
}

resource "aws_route53_record" "secondary" {
  provider = aws.primary

  zone_id        = var.hosted_zone_id
  name           = var.failover_record_name
  type           = "CNAME"
  ttl            = var.failover_record_ttl
  set_identifier = "secondary-${var.secondary_region}"
  records        = [var.secondary_target_dns]

  failover_routing_policy {
    type = "SECONDARY"
  }
}
