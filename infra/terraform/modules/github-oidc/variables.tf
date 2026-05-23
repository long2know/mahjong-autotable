# Phase K Wave 6 — Apone (DevOps).

variable "environment" {
  description = "Environment name (staging / prod / dr-us-west-2). Drives the role name + SSM resource-ARN scoping."
  type        = string
}

variable "region" {
  description = "AWS region for the resource ARN scoping (ECR repo, SSM parameters)."
  type        = string
  default     = "us-east-1"
}

variable "github_org" {
  description = "GitHub organisation or user."
  type        = string
  default     = "long2know"
}

variable "github_repo" {
  description = "GitHub repository name."
  type        = string
  default     = "mahjong-autotable"
}

variable "github_oidc_subjects" {
  description = "OIDC `sub` patterns allowed to assume the role."
  type        = list(string)
  default = [
    "repo:long2know/mahjong-autotable:ref:refs/heads/main",
    "repo:long2know/mahjong-autotable:ref:refs/tags/v*",
  ]
}

variable "ecr_repository_name" {
  description = "ECR repository name the role may push to."
  type        = string
  default     = "mahjong-autotable"
}

variable "create_oidc_provider" {
  description = "Whether to create the OIDC provider in this module. Set false when another module / env in the same account already provisions it (AWS forbids two providers with the same issuer URL)."
  type        = bool
  default     = true
}

variable "existing_oidc_provider_arn" {
  description = "ARN of an EXISTING OIDC provider — required when `create_oidc_provider = false`."
  type        = string
  default     = ""
}

variable "passrole_target_roles" {
  description = "IAM role ARNs the deploy role may PassRole. Empty = no grant."
  type        = list(string)
  default     = []
}

variable "passrole_target_services" {
  description = "AWS service principals restricting the PassRole grant."
  type        = list(string)
  default = [
    "eks.amazonaws.com",
    "ecs-tasks.amazonaws.com",
  ]
}

variable "tags" {
  description = "Common tags to apply to the role + OIDC provider."
  type        = map(string)
  default     = {}
}
