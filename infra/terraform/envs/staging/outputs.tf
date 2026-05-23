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

# ── Redis outputs (W10) ──────────────────────────────────────────

output "redis_primary_endpoint" {
  description = "DNS name of the staging Redis primary endpoint. Operator copies into the ESO ExternalSecret + the SSM `/mahjong/staging/redis/host` parameter."
  value       = module.redis.primary_endpoint_address
}

output "redis_reader_endpoint" {
  description = "DNS name of the staging Redis reader endpoint. Empty when `replica_count = 0` (which is the staging default)."
  value       = module.redis.reader_endpoint_address
}

output "redis_port" {
  description = "Port the staging Redis cluster listens on (always 6379)."
  value       = module.redis.port
}

output "redis_security_group_id" {
  description = "Security group ID of the staging Redis cluster. Caller-side allowed_security_group_ids are passed AT MODULE TIME; if the EKS worker SG ID isn't known at apply time, add the rule out-of-band against this SG."
  value       = module.redis.security_group_id
}

output "redis_connection_string" {
  description = "Full Redis connection string (StackExchange.Redis format). Sensitive — embeds the auth token. Operator does NOT push terraform-output secrets into runtime; use `terraform output -raw redis_connection_string` to retrieve, then `aws ssm put-parameter --type SecureString` per `docs/redis-cluster.md` §6."
  value       = module.redis.redis_connection_string
  sensitive   = true
}

output "redis_auth_token" {
  description = "Redis AUTH token alone (sensitive). Useful when the operator pushes host + token as SEPARATE SSM parameters per `docs/redis-cluster.md` §6 — preferred shape because the token can be rotated without re-uploading the host."
  value       = module.redis.auth_token
  sensitive   = true
}
