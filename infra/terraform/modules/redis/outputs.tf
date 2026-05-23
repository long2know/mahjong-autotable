# Phase K Wave 10 — Apone (DevOps).
#
# Module outputs. Names follow `<resource>_<attribute>` so a
# wrapper module / env stack can grep by resource.
#
# Sensitive outputs (the auth token, the connection string with
# the token embedded) are marked `sensitive = true` so
# `terraform plan` / `terraform apply` logs do not leak the
# value. Operators retrieve them via `terraform output -raw <name>`.

output "primary_endpoint_address" {
  description = "DNS name of the primary endpoint (writes). Apps SHOULD connect here for write workloads — ElastiCache redirects reads to replicas when configured."
  value       = aws_elasticache_replication_group.this.primary_endpoint_address
}

output "reader_endpoint_address" {
  description = "DNS name of the reader endpoint (round-robins across replicas). Empty when `replica_count = 0`. Apps using read/write split should connect here for read workloads."
  value       = aws_elasticache_replication_group.this.reader_endpoint_address
}

output "port" {
  description = "Port the Redis cluster listens on (always 6379 in this module — provided as an output for caller-side convenience)."
  value       = aws_elasticache_replication_group.this.port
}

output "replication_group_id" {
  description = "Replication group ID — useful for cross-module wiring (e.g. CloudWatch alarms scoped to the cluster)."
  value       = aws_elasticache_replication_group.this.id
}

output "security_group_id" {
  description = "Module-managed security group ID. The caller can add additional ingress rules against this SG for one-off access (e.g. bastion debugging) without modifying the module."
  value       = aws_security_group.this.id
}

output "subnet_group_name" {
  description = "ElastiCache subnet group name — useful when the operator inspects the cluster via the AWS console."
  value       = aws_elasticache_subnet_group.this.name
}

output "parameter_group_name" {
  description = "Custom parameter group name. Useful when adjusting per-env knobs via `aws elasticache modify-cache-parameter-group` without re-running terraform."
  value       = aws_elasticache_parameter_group.this.name
}

output "auth_token" {
  description = "Redis AUTH token (32 random alphanumeric chars). Empty string when `auth_token_enabled = false`. Operator pushes this into SSM Parameter Store + the ESO ExternalSecret consumes it on the runtime side (see `docs/redis-cluster.md` §6)."
  value       = length(random_password.auth_token) > 0 ? random_password.auth_token[0].result : ""
  sensitive   = true
}

output "redis_connection_string" {
  description = "Full Redis connection string in the StackExchange.Redis format expected by the .NET runtime: `<host>:<port>,password=<token>,ssl=True,abortConnect=False`. Sensitive (embeds the auth token). Operator pushes this to SSM (or splits the components into separate SSM parameters per `docs/redis-cluster.md` §6 — the split form is preferred so the token can be rotated without re-uploading the host)."
  value = format(
    "%s:%d,password=%s,ssl=%s,abortConnect=False",
    aws_elasticache_replication_group.this.primary_endpoint_address,
    aws_elasticache_replication_group.this.port,
    length(random_password.auth_token) > 0 ? random_password.auth_token[0].result : "",
    var.transit_encryption_enabled ? "True" : "False",
  )
  sensitive = true
}
