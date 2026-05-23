# Phase K Wave 5 — Apone (DevOps).
#
# Per-environment defaults: prod.
# Apply with: terraform apply -var-file=prod.tfvars

environment = "prod"
region      = "us-east-1"
vpc_cidr    = "10.20.0.0/16"

# Production shape:
#   * 3 × t3.medium nodes baseline (one per AZ); HPA scales to 10.
#   * db.t4g.medium for steady-state burst headroom; multi-AZ on;
#     deletion protection on (no accidental `terraform destroy`).
#   * 7-day backup retention is the prod floor.
node_desired_size   = 3
node_min_size       = 3
node_max_size       = 12
node_instance_types = ["t3.medium", "t3a.medium", "t3.large"]

db_instance_class        = "db.t4g.medium"
db_allocated_storage_gb  = 50
db_multi_az              = true
db_deletion_protection   = true
db_backup_retention_days = 7

# Tighter OIDC subject pinning for prod: only main + tags.
github_oidc_subjects = [
  "repo:long2know/mahjong-autotable:ref:refs/heads/main",
  "repo:long2know/mahjong-autotable:ref:refs/tags/v*",
  "repo:long2know/mahjong-autotable:environment:prod",
]
