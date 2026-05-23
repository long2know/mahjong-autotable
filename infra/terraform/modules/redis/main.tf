# Phase K Wave 10 — Apone (DevOps).
#
# Redis module — AWS ElastiCache replication group (Redis engine),
# purpose-built for Bishop's W10 `RedisIdempotencyStore` runtime
# backing store.
#
# Layout:
#
#   * Subnet group  — spans the caller-supplied private subnets.
#                     Multi-AZ replicas REQUIRE ≥ 2 subnets in
#                     distinct AZs (validated in variables.tf).
#   * Parameter group — custom (`mahjong-${env}-redis`) so we can
#                       override `maxmemory-policy` etc. without
#                       sharing state with the AWS-managed default.
#   * Security group — VPC-CIDR ingress on 6379 by default; the
#                      operator passes `allowed_security_group_ids`
#                      to scope tighter (preferred — least
#                      privilege at the SG layer; k8s NetworkPolicy
#                      handles the workload-layer ingress).
#   * Replication group — single shard (no Redis Cluster mode),
#                         configurable replica count, optional
#                         multi-AZ + automatic failover.
#   * Auth token + KMS — optional; when enabled, the token is
#                        generated as a 32-char random string and
#                        surfaced as a sensitive output for the
#                        operator to push into SSM / ESO.
#
# What this module deliberately does NOT do:
#
#   * Run the cluster in Redis Cluster (sharded) mode — Bishop's
#     IdempotencyStore is keyed by a small (≤ 5-min TTL) hot-set;
#     a single shard suffices. If sharding becomes necessary
#     (Phase L candidate), set `num_node_groups > 1` in a
#     follow-up module variant.
#   * Provision Redis on EC2 — managed ElastiCache is the prod
#     baseline; no operator-managed Redis fleet.
#   * Push the auth token to SSM Parameter Store — the runbook in
#     `docs/redis-cluster.md` §6 documents the manual `aws ssm
#     put-parameter` step. We don't pipe Terraform-generated
#     secrets directly into a runtime secrets surface; the seed
#     lives in tfstate, ESO does the runtime delivery.
#
# Apply:
#   module "redis" {
#     source             = "../../modules/redis"
#     environment        = "staging"
#     vpc_id             = module.primary.vpc_id
#     private_subnet_ids = module.primary.private_subnet_ids
#     vpc_cidr           = module.primary.vpc_cidr
#     node_type          = "cache.t4g.micro"
#     replica_count      = 0
#     multi_az_enabled   = false
#   }

locals {
  name_prefix = "mahjong-${var.environment}-redis"

  module_tags = merge(var.common_tags, {
    "Module" = "redis"
    "Wave"   = "phase-k-wave-10"
  })

  # Multi-AZ requires automatic failover (AWS constraint) which
  # requires at least one replica. We enforce here so the operator
  # doesn't have to remember the matrix.
  effective_multi_az = var.multi_az_enabled && var.replica_count >= 1
  automatic_failover = local.effective_multi_az

  # Auth token is mutually exclusive with no-TLS (AWS constraint).
  effective_auth_token = var.auth_token_enabled && var.transit_encryption_enabled

  # KMS key — empty string = AWS-managed (use `null` in the
  # resource arg so terraform omits it rather than passing empty
  # string).
  kms_key_id_or_null = var.kms_key_id == "" ? null : var.kms_key_id
}

# ── Subnet group ─────────────────────────────────────────────────

resource "aws_elasticache_subnet_group" "this" {
  name        = local.name_prefix
  description = "Redis subnet group for ${var.environment} (W10 IdempotencyStore backing)"
  subnet_ids  = var.private_subnet_ids

  tags = local.module_tags
}

# ── Parameter group ──────────────────────────────────────────────

resource "aws_elasticache_parameter_group" "this" {
  name        = local.name_prefix
  description = "Mahjong-Autotable ${var.environment} Redis params (W10)"
  family      = var.parameter_group_family

  dynamic "parameter" {
    for_each = var.parameter_overrides
    content {
      name  = parameter.key
      value = parameter.value
    }
  }

  tags = local.module_tags

  lifecycle {
    # Family changes require a new parameter group; force a
    # replacement rather than failing at apply.
    create_before_destroy = true
  }
}

# ── Security group ───────────────────────────────────────────────

resource "aws_security_group" "this" {
  name        = local.name_prefix
  description = "Redis 6379 ingress from VPC + allowed SGs only"
  vpc_id      = var.vpc_id

  tags = merge(local.module_tags, {
    "Name" = local.name_prefix
  })
}

resource "aws_security_group_rule" "ingress_vpc_cidr" {
  type              = "ingress"
  from_port         = 6379
  to_port           = 6379
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.this.id
  description       = "Redis 6379 from inside the VPC"
}

resource "aws_security_group_rule" "ingress_allowed_sgs" {
  for_each = toset(var.allowed_security_group_ids)

  type                     = "ingress"
  from_port                = 6379
  to_port                  = 6379
  protocol                 = "tcp"
  source_security_group_id = each.value
  security_group_id        = aws_security_group.this.id
  description              = "Redis 6379 from allowed SG ${each.value}"
}

resource "aws_security_group_rule" "egress_all" {
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.this.id
  description       = "All egress (ElastiCache reaches AWS service endpoints + Cluster Discovery)"
}

# ── Auth token (optional) ────────────────────────────────────────
#
# 32 random alphanumeric chars (no special chars — avoids
# escaping bugs across the CLI / k8s envFrom / SSM Parameter
# value surface). ElastiCache auth-token requirements:
# 16-128 chars, printable, no `@` / `"` / `/`.
resource "random_password" "auth_token" {
  count = local.effective_auth_token ? 1 : 0

  length  = 32
  special = false
  upper   = true
  lower   = true
  numeric = true
}

# ── Replication group ────────────────────────────────────────────
#
# Single shard (no cluster mode). Replicas count + multi-AZ +
# automatic-failover are all interrelated — see the local block
# above for the enforced matrix.

resource "aws_elasticache_replication_group" "this" {
  replication_group_id = local.name_prefix
  description          = "Mahjong-Autotable ${var.environment} Redis (W10 IdempotencyStore)"

  engine               = "redis"
  engine_version       = var.engine_version
  node_type            = var.node_type
  num_cache_clusters   = 1 + var.replica_count
  parameter_group_name = aws_elasticache_parameter_group.this.name
  port                 = 6379
  subnet_group_name    = aws_elasticache_subnet_group.this.name
  security_group_ids   = [aws_security_group.this.id]
  apply_immediately    = var.apply_immediately

  multi_az_enabled           = local.effective_multi_az
  automatic_failover_enabled = local.automatic_failover

  at_rest_encryption_enabled = var.at_rest_encryption_enabled
  transit_encryption_enabled = var.transit_encryption_enabled
  auth_token                 = local.effective_auth_token ? random_password.auth_token[0].result : null
  kms_key_id                 = local.kms_key_id_or_null

  snapshot_retention_limit = var.snapshot_retention_limit
  snapshot_window          = var.snapshot_window
  maintenance_window       = var.maintenance_window

  tags = merge(local.module_tags, {
    "Name" = local.name_prefix
  })

  lifecycle {
    # AWS may patch the engine_version's minor digit on
    # maintenance; don't let that thrash the plan.
    ignore_changes = [
      engine_version,
    ]
  }
}
