# Phase K Wave 11 — Apone (DevOps).
#
# Outputs from the prod edge + Redis env stack. Mirror of
# `envs/staging/outputs.tf` minus the sensitive Redis outputs
# being printed without the `terraform output -raw` opt-in.

output "hosted_zone_id" {
  description = "Route 53 hosted zone ID — feeds the prod deployment runbook (`docs/production-deployment-runbook.md`)."
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
  description = "ARN of the prod WAFv2 ACL. Bind to the ALB via `aws_wafv2_web_acl_association` (out-of-band; the edge module deliberately doesn't manage that resource so the ALB lifecycle stays with the cluster bootstrap)."
  value       = module.edge.regional_web_acl_arn
}

output "waf_logs_bucket_name" {
  description = "S3 bucket holding prod WAF logs (`aws-waf-logs-prod-*`). 90-day retention per `docs/audits/`."
  value       = module.edge.waf_logs_bucket_name
}

output "athena_workgroup_name" {
  description = "Athena workgroup name for prod WAF log queries."
  value       = module.edge.athena_workgroup_name
}

output "apex_fqdn" {
  description = "FQDN of the apex A record (`mahjong.example.com` when defaults)."
  value       = module.edge.apex_fqdn
}

# ── Redis outputs (W11 prod) ─────────────────────────────────────

output "redis_primary_endpoint" {
  description = "DNS name of the prod Redis primary endpoint. Operator copies into the ESO ExternalSecret + the SSM `/mahjong/prod/redis/host` parameter (split-shape) or `/mahjong/prod/redis/connection-string` (omnibus shape — see `docs/redis-cluster.md` §11 for the W11 prod walkthrough)."
  value       = module.redis.primary_endpoint_address
}

output "redis_reader_endpoint" {
  description = "DNS name of the prod Redis reader endpoint (round-robins across replicas). Available because `replica_count >= 1` in prod."
  value       = module.redis.reader_endpoint_address
}

output "redis_port" {
  description = "Port the prod Redis cluster listens on (always 6379)."
  value       = module.redis.port
}

output "redis_security_group_id" {
  description = "Security group ID of the prod Redis cluster. Caller-side `allowed_security_group_ids` are passed AT MODULE TIME; if the EKS worker SG ID isn't known at apply time, add the rule out-of-band against this SG."
  value       = module.redis.security_group_id
}

output "redis_replication_group_id" {
  description = "Prod Redis replication group ID — useful when scoping CloudWatch alarms or running `aws elasticache describe-replication-groups`."
  value       = module.redis.replication_group_id
}

output "redis_connection_string" {
  description = "Full Redis connection string (StackExchange.Redis format). Sensitive — embeds the auth token. Retrieve via `terraform output -raw redis_connection_string`, then `aws ssm put-parameter --type SecureString` per `docs/redis-cluster.md` §11 (W11 prod walkthrough — pushes to `/mahjong/prod/redis/connection-string`)."
  value       = module.redis.redis_connection_string
  sensitive   = true
}

output "redis_auth_token" {
  description = "Prod Redis AUTH token alone (sensitive). Useful when the operator pushes host + token as SEPARATE SSM parameters per `docs/redis-cluster.md` §3 — preferred shape because the token can be rotated without re-uploading the host."
  value       = module.redis.auth_token
  sensitive   = true
}
