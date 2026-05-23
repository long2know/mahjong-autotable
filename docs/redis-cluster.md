# Redis cluster — operator runbook

> Phase K Wave 10 — Apone (DevOps).
> Audience: SRE / on-call running `terraform apply` against the
> `modules/redis` cluster, then wiring the runtime side via ESO.
> Companion: [`infra/terraform/modules/redis/README.md`](../infra/terraform/modules/redis/README.md).

The W10 Bishop `RedisIdempotencyStore` runtime needs a Redis
cluster reachable from the EKS workers. This runbook covers the
end-to-end operator path: terraform apply → SSM token push →
ESO ExternalSecret consumption → runtime smoke test → rotation
+ rollback.

## 1. Module topology

The cluster is a **single-shard** ElastiCache replication group
(no Redis Cluster sharded mode):

```
┌────────────────────────────────────────────────────────────┐
│ aws_elasticache_replication_group "this"                   │
│   replication_group_id = mahjong-${env}-redis              │
│                                                            │
│   ┌──────────┐    ┌──────────┐                             │
│   │  primary │ ─→ │ replica  │ (when replica_count ≥ 1)    │
│   │  (AZ-a)  │    │ (AZ-b)   │                             │
│   └──────────┘    └──────────┘                             │
│       ↑                                                    │
│       │  6379/TCP (TLS-wrapped, AUTH-token-gated)          │
│       │                                                    │
│   ┌──────────────────────────┐                             │
│   │ aws_security_group "this"│                             │
│   │ ingress: vpc_cidr + SGs  │                             │
│   └──────────────────────────┘                             │
└────────────────────────────────────────────────────────────┘
```

Per-env shape (default):

| Env     | Node type           | Replicas | Multi-AZ | Snapshots | At-rest crypt | TLS | Auth token |
| ------- | ------------------- | -------: | :------: | --------: | :-----------: | :-: | :--------: |
| staging | `cache.t4g.micro`   |        0 |   off    |  disabled |      ✅       | ✅  |     ✅     |
| prod    | `cache.t4g.small`+  |        1 |    on    |     7-day |      ✅       | ✅  |     ✅     |

The prod shape lives in `infra/terraform/envs/prod/` once that env
is stood up; the staging env stack
(`infra/terraform/envs/staging/main.tf`) is the W10 reference
instantiation.

## 2. First-time provisioning (staging)

```bash
# 1. Primary stack — VPC + EKS + RDS must already exist.
cd infra/terraform/
terraform output -raw  vpc_id              # → record
terraform output -json private_subnet_ids  # → record
terraform output -raw  vpc_cidr            # → record

# 2. Env stack — paste those values into terraform.tfvars (see
#    terraform.tfvars.example).
cd envs/staging/
cp terraform.tfvars.example terraform.tfvars
$EDITOR terraform.tfvars
#   - vpc_id              = "vpc-0abc1234…"
#   - private_subnet_ids  = ["subnet-…", "subnet-…", "subnet-…"]
#   - vpc_cidr            = "10.10.0.0/16"

cp backend.example.hcl backend.hcl
terraform init -backend-config=backend.hcl
terraform plan
terraform apply
```

Apply time: ≈ 5-10 minutes (ElastiCache creation is the long pole).

## 3. SSM Parameter Store push

Terraform surfaces the connection-string + auth token as
**sensitive outputs**. We deliberately do NOT let terraform own
the runtime-secret rotation surface; the operator pushes the
values into SSM, and the ESO ExternalSecret on the runtime side
syncs SSM → k8s Secret.

The PREFERRED shape is **split parameters** (host + token in
separate SSM parameters) so the token can be rotated without
re-uploading the host:

```bash
ENV=staging
KMS_KEY=alias/mahjong-${ENV}-secrets

# Capture terraform outputs into local vars (no echo).
HOST=$(terraform output -raw redis_primary_endpoint)
PORT=$(terraform output -raw redis_port)
TOKEN=$(terraform output -raw redis_auth_token)

# Sanity — none of the three may be empty.
[ -n "$HOST" ] && [ -n "$PORT" ] && [ -n "$TOKEN" ] || { echo "missing output"; exit 1; }

aws ssm put-parameter \
    --name "/mahjong/${ENV}/redis/host" \
    --type String \
    --value "${HOST}" \
    --description "ElastiCache Redis primary endpoint for ${ENV} (W10 IdempotencyStore)."

aws ssm put-parameter \
    --name "/mahjong/${ENV}/redis/port" \
    --type String \
    --value "${PORT}" \
    --description "ElastiCache Redis port for ${ENV} (W10 IdempotencyStore)."

aws ssm put-parameter \
    --name "/mahjong/${ENV}/redis/auth-token" \
    --type SecureString \
    --key-id "${KMS_KEY}" \
    --value "${TOKEN}" \
    --description "ElastiCache Redis AUTH token for ${ENV} (W10 IdempotencyStore)."

# Optionally — full connection string for a runtime that wants
# one-pull configuration. The split form above is preferred.
CONN=$(terraform output -raw redis_connection_string)
aws ssm put-parameter \
    --name "/mahjong/${ENV}/redis/connection-string" \
    --type SecureString \
    --key-id "${KMS_KEY}" \
    --value "${CONN}" \
    --description "ElastiCache Redis full connection string for ${ENV}."

# Clear local vars — best-effort.
unset HOST PORT TOKEN CONN
```

## 4. Customer-managed KMS key (optional)

The module defaults to the AWS-managed `alias/aws/elasticache`
key. To switch to a customer-managed CMK (required for some
compliance regimes — SOC 2, HIPAA):

1. Provision the CMK out-of-band (the squad's KMS bootstrap
   policy is documented in `docs/secret-management.md`).
2. Pass the ARN to the module via `kms_key_id`:

   ```hcl
   module "redis" {
     # …
     kms_key_id = "arn:aws:kms:us-east-1:123456789012:key/abcd1234-…"
   }
   ```

3. `terraform apply` — ElastiCache will recreate the cluster
   (CMK changes are NOT a no-op rotation). Schedule during the
   maintenance window unless `apply_immediately = true`.

## 5. Engine-version bumps

ElastiCache patches the engine_version's minor digit on its
maintenance window — the module deliberately ignores
`engine_version` in `lifecycle.ignore_changes` so terraform plan
doesn't thrash on the auto-patch.

**Major-version bumps** require operator action:

```bash
# 1. Decide the new major (e.g. 7.x → 8.x).
# 2. Update the module variable in the env stack:
$EDITOR envs/staging/main.tf
#   - engine_version         = "8.0"
#   - parameter_group_family = "redis8"    # MUST match major
# 3. Plan + apply during a maintenance window. ElastiCache
#    performs an in-place upgrade — primary is failover-promoted
#    to a replica first; clients see ≤ 30s of write unavailability.
terraform plan
terraform apply
# 4. Smoke test (see §7).
```

## 6. ESO ExternalSecret wiring (runtime side)

Bishop's W10 runtime expects a k8s Secret named
`mahjong-redis-staging` (resp. `…-prod`) with three keys:
`host`, `port`, `auth-token`. The ESO ExternalSecret lives in
the k8s overlay (Apone-lane in `infra/k8s/overlays/staging/`):

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: mahjong-redis-staging
  namespace: mahjong-autotable
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: aws-ssm-staging
    kind: ClusterSecretStore
  target:
    name: mahjong-redis-staging
  data:
    - secretKey: host
      remoteRef:
        key: /mahjong/staging/redis/host
    - secretKey: port
      remoteRef:
        key: /mahjong/staging/redis/port
    - secretKey: auth-token
      remoteRef:
        key: /mahjong/staging/redis/auth-token
```

The .NET runtime consumes the Secret via `envFrom: secretRef:
mahjong-redis-staging` (see the Helm chart's `helm/mahjong/values-staging.yaml`).

## 7. Smoke test

After `terraform apply` + SSM push + ESO sync, validate the
runtime path:

```bash
# 1. Confirm the ExternalSecret synced.
kubectl get externalsecret mahjong-redis-staging \
    -n mahjong-autotable -o jsonpath='{.status.syncedAt}'

# 2. Confirm the Secret materialised.
kubectl get secret mahjong-redis-staging \
    -n mahjong-autotable -o jsonpath='{.data.host}' | base64 -d
# Expected: the primary_endpoint_address printed by terraform.

# 3. From inside a pod, ping Redis with the auth token.
kubectl exec -it -n mahjong-autotable \
    deploy/mahjong-autotable -- /bin/sh -c '
    apk add --no-cache redis 2>/dev/null || true
    redis-cli -h "$Redis__Host" -p "$Redis__Port" --tls \
        --pass "$Redis__AuthToken" PING
'
# Expected: PONG.
```

## 8. Token rotation

The auth token rotates by `terraform taint`-ing the
`random_password.auth_token` resource:

```bash
cd infra/terraform/envs/staging/
terraform taint -allow-missing module.redis.random_password.auth_token[0]
terraform apply

# Then re-push the new token to SSM (§3) and force-sync ESO.
NEW_TOKEN=$(terraform output -raw redis_auth_token)
aws ssm put-parameter \
    --name "/mahjong/staging/redis/auth-token" \
    --type SecureString \
    --key-id "alias/mahjong-staging-secrets" \
    --value "${NEW_TOKEN}" \
    --overwrite

kubectl annotate externalsecret mahjong-redis-staging \
    -n mahjong-autotable \
    force-sync="$(date +%s)" --overwrite

kubectl rollout restart deployment/mahjong-autotable \
    -n mahjong-autotable
```

**Rotation cadence:** quarterly (matches the JWT cadence in
[`docs/jwt-ssm-runbook.md §3`](./jwt-ssm-runbook.md#3-rotation-cadence)).
A leaked token is contained at the SG layer (VPC-internal only)
but rotating closes the residual risk.

## 9. Rollback procedure

`terraform destroy -target module.redis` removes the cluster but
NOT the SSM parameters; clear those by hand:

```bash
terraform destroy -target module.redis
aws ssm delete-parameter --name /mahjong/staging/redis/host
aws ssm delete-parameter --name /mahjong/staging/redis/port
aws ssm delete-parameter --name /mahjong/staging/redis/auth-token
aws ssm delete-parameter --name /mahjong/staging/redis/connection-string
```

The W10 Bishop runtime is **fallback-tolerant**: when the Redis
connection fails, `RedisIdempotencyStore` degrades to an
in-memory store (5-min TTL window, no cross-pod replay). Brief
Redis outages do NOT crash the API; long outages re-emerge
duplicate-request behaviour on multi-pod fleets.

## 10. Cross-references

- [`infra/terraform/modules/redis/README.md`](../infra/terraform/modules/redis/README.md) — module surface + input/output reference.
- [`infra/terraform/envs/staging/main.tf`](../infra/terraform/envs/staging/main.tf) — staging env stack instantiation.
- [`docs/jwt-ssm-runbook.md §3`](./jwt-ssm-runbook.md#3-rotation-cadence) — sibling rotation runbook (matching cadence).
- [`docs/secret-management.md`](./secret-management.md) — KMS conventions + secret rotation policy.
- Bishop's W10 `RedisIdempotencyStore` runtime — consumer of `/mahjong/{env}/redis/*` SSM parameters.
