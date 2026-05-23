# Phase K Wave 5 — Apone (DevOps).
#
# Per-environment defaults: staging.
# Apply with: terraform apply -var-file=staging.tfvars

environment = "staging"
region      = "us-east-1"
vpc_cidr    = "10.10.0.0/16"

# Cheap-and-cheerful staging shape:
#   * 2 × t3.medium nodes (one per AZ for HA; can scale to 5 under load).
#   * db.t4g.small, single-AZ, no deletion protection — we want to be
#     able to `terraform destroy` staging cleanly.
node_desired_size = 2
node_min_size     = 2
node_max_size     = 5

db_instance_class        = "db.t4g.small"
db_allocated_storage_gb  = 20
db_multi_az              = false
db_deletion_protection   = false
db_backup_retention_days = 1

github_oidc_subjects = [
  "repo:long2know/mahjong-autotable:ref:refs/heads/main",
  "repo:long2know/mahjong-autotable:ref:refs/heads/stlong/*",
  "repo:long2know/mahjong-autotable:ref:refs/tags/v*",
  # Allow the dispatchable deploy workflow to target staging from
  # any PR's environment context.
  "repo:long2know/mahjong-autotable:environment:staging",
]
