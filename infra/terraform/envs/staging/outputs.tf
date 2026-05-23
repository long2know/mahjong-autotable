# Phase K Wave 8 — Apone (DevOps).

output "hosted_zone_id" {
  description = "Route 53 hosted zone ID — feeds the staging cutover smoke-test runbook (`docs/staging-cutover.md`)."
  value       = module.edge.hosted_zone_id
}

output "hosted_zone_name_servers" {
  description = "NS records the registrar must point at when `create_hosted_zone = true`. Empty list otherwise."
  value       = module.edge.hosted_zone_name_servers
}

output "regional_acm_certificate_arn" {
  description = "ARN of the validated regional ACM cert — bind on the ALB HTTPS listener (operator step; the edge module doesn't manage the ALB)."
  value       = module.edge.regional_acm_certificate_arn
}

output "regional_web_acl_arn" {
  description = "ARN of the staging WAFv2 ACL. Bind to the ALB via `aws_wafv2_web_acl_association` (out-of-band; the edge module deliberately doesn't manage that resource so the ALB lifecycle stays with the cluster bootstrap)."
  value       = module.edge.regional_web_acl_arn
}

output "waf_logs_bucket_name" {
  description = "S3 bucket holding staging WAF logs (`aws-waf-logs-staging-*`)."
  value       = module.edge.waf_logs_bucket_name
}

output "athena_workgroup_name" {
  description = "Athena workgroup name for staging WAF log queries."
  value       = module.edge.athena_workgroup_name
}

output "apex_fqdn" {
  description = "FQDN of the apex A record (`staging.mahjong.example.com` when defaults)."
  value       = module.edge.apex_fqdn
}
