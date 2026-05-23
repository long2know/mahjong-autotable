# Phase K Wave 5 — Apone (DevOps).
#
# Example backend config — copy to backend-staging.hcl OR
# backend-prod.hcl and edit per-environment.
#
# The state bucket + lock table MUST exist BEFORE
# `terraform init` (chicken-and-egg: `terraform apply` cannot
# create the bucket it stores its own state in). See
# README.md §1.1 for the bootstrap procedure.

bucket         = "mahjong-tfstate-EDITME"
key            = "infra/terraform/EDITME.tfstate"
region         = "us-east-1"
dynamodb_table = "mahjong-tflock-EDITME"
encrypt        = true
