# Phase K Wave 6 — Apone (DevOps).
#
# DR (secondary-region) env stack — us-west-2.
#
# Provisions the secondary-region resources that the
# `modules/dr-replication` module STITCHES INTO (subnets, KMS,
# security groups), then calls the module to wire up the cross-
# region links.
#
# Network plan:
#   * Secondary VPC CIDR: 10.1.0.0/16 (NON-overlapping with the
#     primary 10.0.0.0/16 so a future VPC-peering / Transit Gateway
#     attachment Just Works).
#   * 3 private subnets across the first 3 AZs of us-west-2 (no
#     public subnets — DR-warm doesn't need an ingress path until
#     promotion; ALB lands in `prod-dr` overlay if/when promoted).
#
# State backend: pre-create `mahjong-tfstate-dr-us-west-2` bucket +
# `mahjong-tflock-dr-us-west-2` DynamoDB table in us-west-2 BEFORE
# `terraform init`. See `../../README.md` §1.1 for the bootstrap
# procedure (substitute the dr env name + region).
#
# Apply:
#   terraform init -backend-config=backend.hcl
#   terraform plan
#   terraform apply

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
# `aws` (default, secondary region) — every secondary-region
# resource in this file binds to this provider by omission.
# `aws.primary` (us-east-1) — passed through to the DR module so
# the cross-region ECR replication rule + Route 53 health check
# can be created in the primary account.

provider "aws" {
  region = var.secondary_region

  default_tags {
    tags = local.common_tags
  }
}

provider "aws" {
  alias  = "primary"
  region = var.primary_region

  default_tags {
    tags = local.common_tags
  }
}

# ── Data lookups ────────────────────────────────────────────────

data "aws_availability_zones" "available" {
  state = "available"
}

# Pull the primary stack's outputs via remote-state so we don't
# have to hard-code the primary DB ARN / KMS ARN / etc.
data "terraform_remote_state" "primary" {
  backend = "s3"

  config = {
    bucket = var.primary_state_bucket
    key    = var.primary_state_key
    region = var.primary_region
  }
}

# ── Locals ──────────────────────────────────────────────────────

locals {
  environment = "dr-us-west-2"

  common_tags = {
    "Project"     = "mahjong-autotable"
    "Environment" = local.environment
    "ManagedBy"   = "terraform"
    "Module"      = "envs/dr-us-west-2"
    "Wave"        = "phase-k-wave-6"
  }

  azs = slice(data.aws_availability_zones.available.names, 0, 3)
  # /16 → 3 × /20 private subnets at offsets 8..10 (room left at 0..7
  # for future public subnets when promotion fires).
  private_subnets = [for i in range(3) : cidrsubnet(var.vpc_cidr, 4, i + 8)]
}

# ── Secondary VPC ────────────────────────────────────────────────

resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "mahjong-${local.environment}"
  }
}

resource "aws_subnet" "private" {
  count             = 3
  vpc_id            = aws_vpc.this.id
  cidr_block        = local.private_subnets[count.index]
  availability_zone = local.azs[count.index]

  tags = {
    Name = "mahjong-${local.environment}-private-${count.index}"
    Tier = "private"
  }
}

# ── Replica DB subnet group + security group ─────────────────────

resource "aws_db_subnet_group" "replica" {
  name        = "mahjong-${local.environment}-db"
  description = "Subnet group for the cross-region RDS read replica"
  subnet_ids  = aws_subnet.private[*].id

  tags = {
    Name = "mahjong-${local.environment}-db"
  }
}

resource "aws_security_group" "replica_db" {
  name        = "mahjong-${local.environment}-db"
  description = "Postgres ingress from secondary-region VPC only"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "mahjong-${local.environment}-db"
  }
}

resource "aws_security_group_rule" "replica_db_ingress_pg" {
  type              = "ingress"
  from_port         = 5432
  to_port           = 5432
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.replica_db.id
  description       = "Postgres ingress from inside the secondary VPC"
}

resource "aws_security_group_rule" "replica_db_egress_all" {
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.replica_db.id
  description       = "All egress (RDS reaches AWS service endpoints)"
}

# ── Secondary-region KMS key for the replica ─────────────────────
#
# AWS REQUIRES the replica's KMS key to be in the SECONDARY region;
# you cannot share a CMK across regions. The replica owns its own
# CMK independently of the primary's.

resource "aws_kms_key" "replica" {
  description             = "RDS storage-encryption key for mahjong-${local.environment} replica"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  tags = {
    Name = "mahjong-${local.environment}-rds-storage"
  }
}

resource "aws_kms_alias" "replica" {
  name          = "alias/mahjong-${local.environment}-rds-storage"
  target_key_id = aws_kms_key.replica.key_id
}

# ── DR module instantiation ─────────────────────────────────────

module "dr" {
  source = "../../modules/dr-replication"

  providers = {
    aws.primary   = aws.primary
    aws.secondary = aws
  }

  primary_region   = var.primary_region
  secondary_region = var.secondary_region
  environment      = local.environment

  # Pulled from the primary stack's remote state — keeps the DR env
  # in lock-step with the primary's actual DB ARN / KMS / etc.
  primary_db_arn         = data.terraform_remote_state.primary.outputs.db_instance_arn
  primary_db_kms_key_arn = data.terraform_remote_state.primary.outputs.db_kms_key_arn

  replica_kms_key_arn            = aws_kms_key.replica.arn
  replica_subnet_group_name      = aws_db_subnet_group.replica.name
  replica_vpc_security_group_ids = [aws_security_group.replica_db.id]
  replica_instance_class         = var.replica_instance_class
  replica_backup_retention_days  = var.replica_backup_retention_days
  replica_deletion_protection    = var.replica_deletion_protection

  ecr_repository_filter = var.ecr_repository_filter

  hosted_zone_id       = var.hosted_zone_id
  failover_record_name = var.failover_record_name
  failover_record_ttl  = var.failover_record_ttl
  primary_target_dns   = var.primary_target_dns
  secondary_target_dns = var.secondary_target_dns

  common_tags = local.common_tags
}
