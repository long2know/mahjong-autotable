# `modules/redis` — ElastiCache Redis (W10)

> Phase K Wave 10 — Apone (DevOps).

This module provisions a single-shard AWS ElastiCache Redis
replication group + the supporting subnet group, parameter group,
security group, and optional auth token. It is purpose-built for
the Wave-10 `RedisIdempotencyStore` runtime — Bishop's real
implementation of the W8 `IIdempotencyStore` interface.

## Inputs (summary)

| Variable                     | Default                          | Notes                                                                 |
| ---------------------------- | -------------------------------- | --------------------------------------------------------------------- |
| `environment`                | (required)                       | lower-case ident, 2-31 chars                                          |
| `vpc_id`                     | (required)                       | typically `module.primary.vpc_id`                                     |
| `private_subnet_ids`         | (required)                       | ≥ 2 subnets in distinct AZs when `multi_az_enabled = true`            |
| `vpc_cidr`                   | (required)                       | the security-group ingress CIDR                                       |
| `allowed_security_group_ids` | `[]`                             | least-privilege ingress: prefer over the VPC-CIDR rule                |
| `node_type`                  | `cache.t4g.small`                | bump for prod-throughput                                              |
| `replica_count`              | `1`                              | 0 in staging (no failover), 1 in prod (one replica in second AZ)      |
| `multi_az_enabled`           | `true`                           | forces `automatic_failover_enabled = true`; needs `replica_count ≥ 1` |
| `engine_version`             | `7.1`                            | matches `parameter_group_family = redis7`                             |
| `parameter_group_family`     | `redis7`                         | must match `engine_version` major                                     |
| `parameter_overrides`        | `maxmemory-policy = allkeys-lru` | overrides applied on the custom parameter group                       |
| `snapshot_retention_limit`   | `7`                              | days; set 0 in staging to disable snapshots                           |
| `snapshot_window`            | `03:00-05:00` UTC                | ≥ 60-min window                                                       |
| `maintenance_window`         | `sun:05:00-sun:07:00` UTC        | ≥ 60-min window                                                       |
| `at_rest_encryption_enabled` | `true`                           | free on ElastiCache                                                   |
| `transit_encryption_enabled` | `true`                           | required for `auth_token_enabled = true`                              |
| `auth_token_enabled`         | `true`                           | 32-char random AUTH token surfaced as sensitive output                |
| `kms_key_id`                 | `""` (AWS-managed)               | set to a CMK ARN for customer-managed                                 |
| `apply_immediately`          | `false`                          | flip to true ONLY when the operator accepts a potential failover      |

## Outputs (summary)

| Output                     | Sensitive | Notes                                                                |
| -------------------------- | :-------: | -------------------------------------------------------------------- |
| `primary_endpoint_address` |           | the writes endpoint                                                  |
| `reader_endpoint_address`  |           | the reads endpoint (empty when `replica_count = 0`)                  |
| `port`                     |           | always 6379                                                          |
| `replication_group_id`     |           | useful for CloudWatch wiring                                         |
| `security_group_id`        |           | append your own ingress rules against this SG                        |
| `subnet_group_name`        |           | console-side convenience                                             |
| `parameter_group_name`     |           | for in-place parameter tuning out-of-band                            |
| `auth_token`               |    🔒     | empty string when `auth_token_enabled = false`                       |
| `redis_connection_string`  |    🔒     | full StackExchange.Redis-style connection string with token embedded |

## Apply example (env stack)

```hcl
module "redis" {
  source = "../../modules/redis"

  environment        = "staging"
  vpc_id             = module.primary.vpc_id
  private_subnet_ids = module.primary.private_subnet_ids
  vpc_cidr           = module.primary.vpc_cidr

  node_type        = "cache.t4g.micro"
  replica_count    = 0
  multi_az_enabled = false

  snapshot_retention_limit = 0
}

output "redis_primary_endpoint" {
  value = module.redis.primary_endpoint_address
}

output "redis_connection_string" {
  value     = module.redis.redis_connection_string
  sensitive = true
}
```

## What this module does NOT do

- **Redis Cluster (sharded) mode.** The W10 `IdempotencyStore`
  is keyed by a small hot-set with 5-min TTL — a single shard
  suffices. Sharding lands as a Phase-L follow-up variant if
  needed.
- **EC2-self-managed Redis.** Managed ElastiCache is the prod
  baseline. The squad does not operate a Redis fleet.
- **SSM Parameter Store push of the auth token.** The runbook in
  [`docs/redis-cluster.md`](../../../../docs/redis-cluster.md) §6
  documents the manual `aws ssm put-parameter` step. We do NOT
  let terraform own the runtime-secret rotation surface; the
  seed lives in tfstate, ESO does the runtime delivery.
- **Pod ↔ Redis k8s NetworkPolicy.** That belongs in the k8s
  overlay, not in this module.

## Cross-references

- [`docs/redis-cluster.md`](../../../../docs/redis-cluster.md) — operator runbook for provisioning + rotation.
- [`infra/terraform/envs/staging/`](../../envs/staging/) — staging env stack instantiating this module (Apply order: primary stack → THIS module → ESO secret on the k8s side).
- W10 Bishop `RedisIdempotencyStore` — the runtime consumer of the connection string.
