# Phase K Wave 5 — Apone (DevOps).
#
# Terraform bootstrap for `mahjong-autotable` AWS infrastructure.
#
# Purpose: stand up a SUFFICIENT (not maximal) AWS footprint to host
# the mahjong-autotable stack on EKS + RDS Postgres + ECR. This is
# the "fresh prod environment in under 30 minutes" acceptance
# criterion that unblocks Wave-6 disaster-recovery rehearsals and
# the multi-region rollout plan.
#
# What this module provisions:
#
#   * 1 × VPC (10.0.0.0/16) — 3 public + 3 private subnets across 3
#     AZs. Public subnets host the NAT gateways + the EKS public
#     API endpoint; private subnets host the worker nodes + RDS.
#   * 1 × EKS cluster (managed node group, t3.medium × 3 default).
#     Public+private API endpoint by default; IRSA enabled; OIDC
#     issuer exported so GitHub Actions can assume the deploy role.
#   * 1 × RDS Postgres instance (db.t4g.small default; storage
#     auto-scaling 20 → 100 GB; encrypted at rest).
#   * 1 × ECR repository (`mahjong-autotable`) with image-scan-on-push
#     enabled. Lifecycle policy: keep last 30 images, untagged
#     images expire after 14 days.
#   * 1 × IAM role for GitHub-Actions OIDC federation with the trust
#     policy scoped to THIS repo + main branch + v* tags.
#
# What this module deliberately does NOT provision:
#
#   * Cluster add-ons beyond the AWS-managed defaults (ALB controller,
#     cert-manager, ESO, Kyverno) — those land via `helm` in the
#     Wave-5 cluster-bootstrap runbook (see README.md §3).
#   * Route53 + ACM + WAF — domain-bound, ship via a SEPARATE
#     overlay module once `mahjong.example.com` is registered.
#   * Multi-region replication — out of scope for the "spin up clean
#     prod env in <30 min" target; ships as a Wave-6+ extension.
#   * S3 buckets for application state — the app is stateless;
#     replays + SBOMs ship to GHCR / GH Releases / Sigstore.
#
# Reusable for staging + prod + DR by varying `var.environment`.
# The single tfvars file controls the per-env shape (instance
# sizes, replica counts, multi-AZ).
#
# State backend: SEE README §1.1 — operator MUST pre-create the
# `mahjong-tfstate-${var.environment}` S3 bucket + a
# `mahjong-tflock-${var.environment}` DynamoDB table BEFORE
# `terraform init`. We do NOT auto-create the backend (chicken-
# and-egg: `terraform apply` cannot create the bucket it stores
# its own state in).
#
# Apply:   `terraform init -backend-config=backend-${env}.hcl`
#          `terraform plan -var-file=${env}.tfvars`
#          `terraform apply -var-file=${env}.tfvars`
#
# Per-env tfvars files live alongside this main.tf (gitignored if
# they contain secrets — see README §5).

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
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }

  # The backend stanza is intentionally EMPTY so per-env values
  # flow in via `terraform init -backend-config=backend-${env}.hcl`.
  # Example backend-prod.hcl:
  #
  #   bucket         = "mahjong-tfstate-prod"
  #   key            = "infra/terraform/prod.tfstate"
  #   region         = "us-east-1"
  #   dynamodb_table = "mahjong-tflock-prod"
  #   encrypt        = true
  #
  # See README §1.1 for the bucket + lock-table bootstrap procedure.
  backend "s3" {}
}

provider "aws" {
  region = var.region

  default_tags {
    tags = local.common_tags
  }
}

locals {
  common_tags = {
    "Project"     = "mahjong-autotable"
    "Environment" = var.environment
    "ManagedBy"   = "terraform"
    "Module"      = "infra/terraform"
    "Wave"        = "phase-k-wave-5"
  }

  # CIDR plan: 10.0.0.0/16 split into 3 public + 3 private /20 blocks.
  azs            = slice(data.aws_availability_zones.available.names, 0, 3)
  public_subnets = [for i in range(3) : cidrsubnet(var.vpc_cidr, 4, i)]
  # Private subnets start at offset 8 to leave room for future
  # extensions (e.g. dedicated DB subnets) without renumbering.
  private_subnets = [for i in range(3) : cidrsubnet(var.vpc_cidr, 4, i + 8)]
}

data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_caller_identity" "current" {}
