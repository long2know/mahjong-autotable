# Phase K Wave 6 — Apone (DevOps).

output "role_arn" {
  description = "IAM role ARN GitHub Actions assumes via OIDC."
  value       = aws_iam_role.github_deploy.arn
}

output "role_name" {
  description = "IAM role name."
  value       = aws_iam_role.github_deploy.name
}

output "oidc_provider_arn" {
  description = "OIDC provider ARN — either the one we created, or the existing one the caller passed in."
  value       = local.oidc_provider_arn
}
