# Phase K Wave 6 — Apone (DevOps).

output "replica_db_arn" {
  description = "ARN of the RDS cross-region read replica. Operator uses this for `aws rds promote-read-replica --db-instance-identifier <id>` on rehearsal day."
  value       = aws_db_instance.replica.arn
}

output "replica_db_identifier" {
  description = "Identifier of the read replica (e.g. `mahjong-dr-us-west-2-replica`). Pass-through to the promotion runbook."
  value       = aws_db_instance.replica.identifier
}

output "replica_db_endpoint" {
  description = "Read endpoint of the replica. Application configuration in the secondary region SHOULD point here for read-only traffic in steady state."
  value       = aws_db_instance.replica.endpoint
}

output "primary_health_check_id" {
  description = "Route 53 health-check ID gating the PRIMARY failover record. Operator inverts this check (`aws route53 update-health-check --inverted`) to force failover during rehearsal without touching the actual primary."
  value       = aws_route53_health_check.primary.id
}

output "failover_record_fqdn" {
  description = "Fully-qualified domain name of the failover record (e.g. `mahjong.example.com`). Pre-warmed for the DR rehearsal runbook."
  value       = aws_route53_record.primary.fqdn
}

output "ecr_replication_destination_region" {
  description = "Destination region of the ECR replication rule — fed to the rehearsal runbook so the operator can `aws ecr describe-images --region <region>` to confirm the replicated image landed."
  value       = var.secondary_region
}
