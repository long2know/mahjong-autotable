# `dr-replication/` — cross-region DR module

> Phase K Wave 6 — Apone (DevOps).

Stitches the three cross-region resources needed to flip
`mahjong-autotable` from a single-region (us-east-1) deployment to a
warm-standby DR pair (primary us-east-1, secondary us-west-2):

1. **RDS Postgres cross-region read replica.** Replicates the primary
   DB into the secondary region with its own customer-managed KMS
   key. Encrypted at rest; deletion-protected for DR-prod.
2. **ECR image replication.** Account-level rule that ships every
   push to `mahjong-autotable` in the primary registry to the
   secondary registry asynchronously (typical lag 1–5 min).
3. **Route 53 failover record.** Primary + secondary CNAMEs sharing
   one name, gated by an HTTPS health check against the primary
   target's `/health` endpoint. TTL < 60s (see the variable
   validator) so clients pick up the failover within a minute.

The module is instantiated from `envs/dr-us-west-2/main.tf` and
takes two AWS provider aliases (`aws.primary`, `aws.secondary`) so
every resource is explicitly placed in the correct region; the
module does NOT use a default provider.

See `docs/terraform.md` §"DR rehearsal" for the quarterly drill
runbook.

## Inputs

See `variables.tf` for the authoritative variable list. The most
common inputs supplied by the secondary env are:

| Input | Why |
|-------|-----|
| `primary_db_arn` | ARN of the primary RDS instance. Read from the primary stack's `terraform_remote_state`. |
| `replica_kms_key_arn` | Secondary-region CMK provisioned in the secondary env. AWS forbids re-using the primary's KMS key cross-region. |
| `replica_subnet_group_name` | DB subnet group in the secondary VPC. |
| `replica_vpc_security_group_ids` | Secondary-region SG list (Postgres ingress from secondary-region EKS workers). |
| `hosted_zone_id` | Pre-existing Route 53 hosted zone. |
| `failover_record_name` | FQDN clients hit (e.g. `mahjong.example.com`). |
| `primary_target_dns` / `secondary_target_dns` | ALB DNS names. |

## Outputs

| Output | Used by |
|--------|---------|
| `replica_db_arn` / `replica_db_identifier` | `aws rds promote-read-replica` during rehearsal. |
| `primary_health_check_id` | `aws route53 update-health-check --inverted` to force failover without taking the primary down. |
| `failover_record_fqdn` | Smoke-test target after failover. |

## What this module does NOT do

* It does NOT provision the secondary VPC / EKS / RDS subnet group /
  KMS key — those belong in the secondary env's `main.tf`.
* It does NOT promote the replica. Promotion is an operator command
  on rehearsal day so a human is in the loop.
* It does NOT manage the inverted health check. The rehearsal
  runbook flips that with the CLI; the module's
  `aws_route53_health_check.primary` resource owns the
  steady-state configuration.
