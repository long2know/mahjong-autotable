# Phase K Wave 6 — Apone (DevOps).

output "replica_db_arn" {
  description = "ARN of the cross-region RDS replica."
  value       = module.dr.replica_db_arn
}

output "replica_db_endpoint" {
  description = "Read endpoint of the replica."
  value       = module.dr.replica_db_endpoint
}

output "primary_health_check_id" {
  description = "Route 53 health-check ID — inverted by the rehearsal runbook to force failover."
  value       = module.dr.primary_health_check_id
}

output "failover_record_fqdn" {
  description = "Failover record FQDN."
  value       = module.dr.failover_record_fqdn
}

output "vpc_id" {
  description = "Secondary VPC ID (10.1.0.0/16)."
  value       = aws_vpc.this.id
}

output "private_subnet_ids" {
  description = "Private subnet IDs in the secondary region."
  value       = aws_subnet.private[*].id
}
