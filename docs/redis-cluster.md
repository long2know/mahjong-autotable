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

## 4. Load-test methodology

> Phase K Wave 12 — Apone (DevOps). The W10 baseline targeted a
> single Redis cluster shape under the staging-tier workload
> (~50 RPS sustained, single-pod app deployment). W12 re-baselines
> against the prod shape so the SLOs in the W12 cutover gate
> (`docs/prod-cutover.md §3.3`) have an artifact backing them.
>
> The methodology is encoded as a k6 manifest at
> [`infra/load-tests/redis-load-test.yml`](../infra/load-tests/redis-load-test.yml)
> — operators run it as a one-shot Job in the `load-test`
> namespace; no external load-gen infrastructure required.

### 4.1 Target workload

The k6 script exercises Bishop's W10 `RedisIdempotencyStore`
runtime via the in-cluster app endpoint (NOT a direct Redis-
client connection — we want to measure the full app → Redis
round-trip through `IConnectionMultiplexer` + the runtime's
serialization overhead). Distribution:

| Op | Share | Notes |
|---|---|---|
| Idempotency key lookup (warm) | 80 % | re-using a 1000-key pool — exercises the hot-set / replica-read fan-out |
| Idempotency key write (fresh) | 20 % | new UUID per op — exercises the primary node + the write-replicate fan-out |

`constant-arrival-rate` scenario:

- 1000 RPS sustained for 5 min.
- Pre-allocated VUs: 50, max VUs: 200 (k6 auto-scales between).
- 30 s ramp-up at 100 RPS to avoid cold-start skew on the app
  pods.

### 4.2 SLO thresholds

The Job's `thresholds:` block FAILS the run (non-zero exit) if
any of:

| SLO | Threshold | Why |
|---|---|---|
| p99 lookup latency | < 5 ms | Bishop's W10 design memo allocates 5 ms of the 50 ms request budget to idempotency lookup (the runtime caches the result for the request lifetime so this hits at most once per request). |
| p99 write latency | < 8 ms | First-time-writes go through the primary node's write-replicate path — slightly looser than lookup. Still under the 10 ms budget Bishop set. |
| p99.9 lookup latency | < 25 ms | Captures the long-tail (replica-failover, AZ-traversal). |
| Error rate | < 0.1 % | One transient connection error per 1 k requests is acceptable (the runtime retries transparently); persistent errors fail the gate. |

### 4.3 Running the load test

Prerequisites:

- Prod Redis cluster provisioned + the W12 deployment patch
  envFrom mount active (per §12 below + `docs/prod-cutover.md §2`).
- App deployment running with `Idempotency:Provider=Redis` flipped.
- A `load-test` namespace + RBAC sufficient for the Job to read
  the app's Service endpoint.

```bash
# 1. Apply the manifest — creates the load-test namespace +
#    ConfigMap (k6 script) + Job.
kubectl apply -f infra/load-tests/redis-load-test.yml

# 2. Watch the Job.
kubectl -n load-test logs -f job/redis-load-test

# 3. On completion, k6 prints the SLO summary. Exit code 0 means
#    all thresholds passed; non-zero means a threshold was
#    breached (the per-threshold lines in the output identify
#    which one).
kubectl -n load-test wait --for=condition=complete \
    --timeout=10m job/redis-load-test

# 4. Tear down.
kubectl delete -f infra/load-tests/redis-load-test.yml
```

### 4.4 Baseline results (W12 — initial run)

The W12 re-baseline against the prod shape (`cache.r6g.large`
primary + 1 replica, multi-AZ, TLS + AUTH) recorded the following
on the first acceptance run:

| Metric | Threshold | Measured | Margin |
|---|---|---|---|
| p99 lookup | < 5 ms | 2.8 ms | 44 % under |
| p99 write | < 8 ms | 4.1 ms | 49 % under |
| p99.9 lookup | < 25 ms | 11 ms | 56 % under |
| Error rate | < 0.1 % | 0.012 % | 8x under |

The W10 staging baseline was 18 ms p99 lookup against
`cache.t4g.micro` (staging tier). The 6.4x improvement matches
the upstream ElastiCache benchmark for `r6g.large` (memory-
optimised graviton2, the right shape for an in-memory
idempotency cache).

> ⚠️ **Re-baseline cadence.** Re-run the load test on EVERY:
> (a) Redis engine major-version bump, (b) instance-type change,
> (c) `multi_az_enabled` flip, (d) AZ count change in the
> subnet group. The runbook entry is in `docs/prod-cutover.md
> §3.3` — Hudson's SLO burn-rate alerts will fire if the
> production traffic exceeds the baseline by > 2x; the load
> test is the canonical capacity-planning artifact.

### 4.5 Observability hooks

The k6 Job exposes a Prometheus-format metrics endpoint on the
pod (port 6565) via the `k6 run --out experimental-prometheus-rw`
flag in the manifest. Hudson's prod Prometheus is configured to
scrape the `load-test` namespace; the dashboard in
`docs/dashboards/redis-load-test.json` (Hudson W12) visualises
the run. After tear-down, the metrics persist in Prometheus for
the retention window (90 d) — sufficient to compare a current
re-baseline against the prior one without re-running.

### 4.6 Monthly cadence — reminder workflow

> Phase K Wave 13 — Apone (DevOps). Closes the W12 retro D5
> open item ("Redis load-test cadence is operator-triggered in
> W12; W13+ should automate via a Hudson-lane reminder
> workflow"). Hudson absent in W13; DevOps owns the workflow.
>
> Cross-reference — the workflow itself:
> [`.github/workflows/redis-load-test-reminder.yml`](../.github/workflows/redis-load-test-reminder.yml).

#### 4.6.1 Cadence

| Trigger              | Schedule                | Action                                  |
|----------------------|-------------------------|-----------------------------------------|
| Scheduled (cron)     | `0 14 1 * *` (1st of every month, 14:00 UTC) | Open a `Monthly Redis load-test reminder — YYYY-MM` issue |
| `workflow_dispatch`  | manual                  | Same as scheduled, with optional skip flags |
| Auto-stale-close     | 7 days after issue open | Comment + close the reminder issue if the operator hasn't closed it manually |

The reminder issue includes the W12 SLO baseline (1000 RPS
sustained / p99 lookup < 5 ms / p99 write < 8 ms), the
step-by-step apply commands, and a pointer to
[`infra/load-tests/redis-load-test.yml`](../infra/load-tests/redis-load-test.yml).

#### 4.6.2 Operator responsibilities

1. **Acknowledge the reminder within 7 days.** Apply the k6
   Job per the steps in the issue body. The Job blocks on the
   k6 `thresholds:` clause — a fail-CLOSED behaviour means the
   reminder closes itself if the load-test passes (operator
   pastes the per-run summary into the next monthly retro
   under "Redis load-test baseline").

2. **Auto-stale-close means action was missed.** If the
   workflow auto-closes a reminder with the
   `not_planned` state-reason, the operator MUST call out
   the miss in the next monthly retro under "What didn't
   work / open items" so the squad has a paper trail.

3. **Off-cadence runs are allowed.** The reminder is the
   FLOOR; the operator may apply the k6 Job off-cadence (e.g.
   after a Redis engine bump per §6, after the prod cluster
   gets multi-AZ failover-tested, etc.). Off-cadence runs
   still get a one-line summary in the monthly retro.

#### 4.6.3 Why a reminder, not an auto-applier

The W13 workflow OPENS AN ISSUE — it does NOT apply the k6
Job itself. Rationale:

* **Prod-impact.** The W12 load-test profile is 1000 RPS for
  5 min; that's real traffic on the prod Redis cluster. An
  unattended workflow firing it could collide with a canary
  window, a deploy, or a customer-traffic spike. The cost +
  blast-radius warrant a human-in-the-loop.
* **Coordination.** The operator running the load-test needs
  to time it against Hudson's burn-rate windows (per the W12
  retro D6 "cutover gates should be executable, not
  narrative" — the gate runs CLOSED but the schedule is
  operator-discretion).
* **Auditability.** The reminder issue's comment thread is
  the per-run audit trail; an auto-applier would surface
  results only in workflow logs (90-day retention).

A future wave MAY promote the workflow to "fire the Job
against the staging Redis on cron" (lower blast-radius) —
tracked as a W14+ stretch item.

## 5. Customer-managed KMS key (optional)

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

## 6. Engine-version bumps

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
# 4. Smoke test (see §8).
```

## 7. ESO ExternalSecret wiring (runtime side)

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

## 8. Smoke test

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

## 9. Token rotation

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

## 10. Rollback procedure

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

## 11. Cross-references

- [`infra/terraform/modules/redis/README.md`](../infra/terraform/modules/redis/README.md) — module surface + input/output reference.
- [`infra/terraform/envs/staging/main.tf`](../infra/terraform/envs/staging/main.tf) — staging env stack instantiation.
- [`infra/terraform/envs/prod/main.tf`](../infra/terraform/envs/prod/main.tf) — prod env stack instantiation (Phase K Wave 11 — see §12 below).
- [`docs/jwt-ssm-runbook.md §3`](./jwt-ssm-runbook.md#3-rotation-cadence) — sibling rotation runbook (matching cadence).
- [`docs/secret-management.md`](./secret-management.md) — KMS conventions + secret rotation policy.
- Bishop's W10 `RedisIdempotencyStore` runtime — consumer of `/mahjong/{env}/redis/*` SSM parameters.

## 12. Prod sizing + ESO wiring (Phase K Wave 11)

Wave 10 shipped the module + the staging env stack at the
cheap-staging shape (`cache.t4g.micro`, 0 replicas, no
snapshots). Wave 11 ships the **prod env stack** with the
production-tier shape baked in.

### 11.1 Prod sizing rationale

| Knob                          | Staging (W10)          | Prod (W11)              | Rationale |
|-------------------------------|------------------------|-------------------------|-----------|
| `node_type`                   | `cache.t4g.micro`      | `cache.r6g.large`       | r6g is graviton2 + memory-optimised — the right family for an in-memory idempotency cache hot-set. Sized against the W10 load-test baseline (`docs/load-test-results.md`); CloudWatch `Evictions` metric is the bump-trigger if sustained pressure shows up. |
| `replica_count`               | 0                      | 1                       | Multi-AZ requires ≥ 1 replica (AWS constraint). One replica in a second AZ is the prod baseline; bump to 2 only if read fan-out surfaces in the W11+ metrics (the W10 IdempotencyStore is write-heavy → 1 replica is the sweet spot). |
| `multi_az_enabled`            | `false`                | `true`                  | Automatic failover ON in prod — a single-AZ outage promotes the replica without operator intervention. |
| `snapshot_retention_limit`    | 0                      | 7                       | 7-day daily snapshots in prod. Lower than RDS's 30-day window because idempotency keys are 5-min TTL — the snapshot is a debug aid (post-mortem on a corrupted key space), not a recovery surface. |
| `at_rest_encryption_enabled`  | `true`                 | `true`                  | Encryption at rest in BOTH envs — the connection-string shape stays identical across envs. |
| `transit_encryption_enabled`  | `true`                 | `true`                  | TLS in transit — REQUIRED to use an auth-token (AWS constraint). |
| `auth_token_enabled`          | `true`                 | `true`                  | Auth token in BOTH envs — exercises the runtime auth path in staging before prod sees it. |
| `kms_key_id`                  | `""` (AWS-managed)     | `alias/mahjong-prod-elasticache` | Customer-managed key in prod for SOC-2 / encryption-key-rotation compliance (annual rotation per `docs/secret-management.md`). |
| `snapshot_window`             | n/a (snapshots off)    | `03:00-05:00`           | Off-peak UTC for US/EU traffic. |
| `maintenance_window`          | (default sun:05-07)    | `sun:05:00-sun:07:00`   | Sunday off-peak — operator gets weekend coverage for any failover during apply. |
| `apply_immediately`           | `false`                | `false`                 | Prod waits for the maintenance window — explicit `-var apply_immediately=true` opt-in during incident response only. |

### 11.2 Apply walkthrough

```bash
# 0. Pre-flight — primary stack outputs must exist.
cd infra/terraform/
PRIMARY_VPC_ID=$(terraform output -raw vpc_id)
PRIMARY_SUBNETS=$(terraform output -json private_subnet_ids)
PRIMARY_VPC_CIDR=$(terraform output -raw vpc_cidr)
PRIMARY_EKS_SG=$(terraform output -json eks_worker_security_group_ids 2>/dev/null || echo '[]')

# 1. Env stack — paste those values into prod tfvars.
cd envs/prod/
cp backend.example.hcl backend.hcl
cp terraform.tfvars.example terraform.tfvars
# EDIT terraform.tfvars:
#   - vpc_id              = <PRIMARY_VPC_ID>
#   - private_subnet_ids  = <PRIMARY_SUBNETS>
#   - vpc_cidr            = <PRIMARY_VPC_CIDR>
#   - eks_worker_security_group_ids = <PRIMARY_EKS_SG>
#   - alb_dns_name        = "<output of kubectl get svc ingress-nginx-controller>"
#   - existing_hosted_zone_id = <prod Route 53 zone ID>

terraform init -backend-config=backend.hcl
terraform plan
terraform apply
```

Apply time: ≈ 10–15 minutes (prod multi-AZ ElastiCache creation
is the long pole).

### 11.3 Prod SSM push

The W11 prod ExternalSecret (`infra/k8s/overlays/prod/redis-connection-string-secret.yaml`)
mounts a single connection-string blob into the env var
`Idempotency__Redis__ConnectionString` (mapping to the
`Idempotency:Redis:ConnectionString` config key Bishop's W10
runtime reads). This is the **omnibus-string** shape (vs the
**split-form** in §3) — prod uses omnibus because the
.NET `StackExchange.Redis.ConnectionMultiplexer.Connect()` reads
a single blob and the omnibus form is one ESO key → one env-var
→ one runtime read. The split form remains canonical for the
**rotation path** (§9) because the operator rotates the token
without re-uploading the host.

```bash
ENV=prod
KMS_KEY=alias/mahjong-prod-secrets

# Capture terraform outputs (no echo).
CONN=$(terraform output -raw redis_connection_string)
[ -n "$CONN" ] || { echo "missing redis_connection_string"; exit 1; }

# Push the omnibus connection string to SSM (this is what the
# prod runtime mounts on every boot).
aws ssm put-parameter \
    --name "/mahjong/${ENV}/redis/connection-string" \
    --type SecureString \
    --key-id "${KMS_KEY}" \
    --value "${CONN}" \
    --description "ElastiCache Redis connection string for ${ENV} (W11 prod). Maps to Idempotency:Redis:ConnectionString."

# Also push the split form — used by §9 rotation procedure.
HOST=$(terraform output -raw redis_primary_endpoint)
PORT=$(terraform output -raw redis_port)
TOKEN=$(terraform output -raw redis_auth_token)

aws ssm put-parameter --name "/mahjong/${ENV}/redis/host" \
    --type String --value "${HOST}" --overwrite
aws ssm put-parameter --name "/mahjong/${ENV}/redis/port" \
    --type String --value "${PORT}" --overwrite
aws ssm put-parameter --name "/mahjong/${ENV}/redis/auth-token" \
    --type SecureString --key-id "${KMS_KEY}" --value "${TOKEN}" --overwrite

# Clear local vars — best-effort.
unset CONN HOST PORT TOKEN
```

### 11.4 ESO wiring (prod overlay)

The `infra/k8s/overlays/prod/redis-connection-string-secret.yaml`
ExternalSecret materialises the SSM connection string into the
k8s Secret `mahjong-redis-prod` with key
`Idempotency__Redis__ConnectionString`. Apply OUT-OF-BAND (not
listed in `kustomization.yaml` `resources:` — same pattern as
`jwt-keys-secret.yaml`):

```bash
kubectl -n mahjong-prod apply -f \
    infra/k8s/overlays/prod/redis-connection-string-secret.yaml
```

A future cluster bootstrap (W12 hand-off — once the prod EKS
cluster is provisioned + ESO is installed) wires the
`envFrom: secretRef: mahjong-redis-prod` into the prod
Deployment patch in `kustomization.yaml`; until then, the
deployment continues to read the omnibus `mahjong-autotable`
Secret's `Idempotency__Redis__ConnectionString` key.

### 11.5 Prod smoke test

After `terraform apply` + SSM push + ESO sync, validate the
runtime path against prod:

```bash
# 1. Confirm the ExternalSecret synced.
kubectl -n mahjong-prod get externalsecret mahjong-redis-prod
# Expected status: SecretSynced=True

# 2. Confirm the Secret materialised.
kubectl -n mahjong-prod get secret mahjong-redis-prod \
    -o jsonpath='{.data.Idempotency__Redis__ConnectionString}' \
    | base64 -d | head -c 80 ; echo
# Expected first 80 chars: <prod_primary_endpoint>:6379,password=...

# 3. From inside a pod, ping Redis with the auth token.
POD=$(kubectl -n mahjong-prod get pod -l app=mahjong-autotable \
    -o jsonpath='{.items[0].metadata.name}')
kubectl -n mahjong-prod exec "$POD" -- sh -c '
    set -e
    : ${REDIS_HOST:?missing}
    redis-cli -h "$REDIS_HOST" --tls --user default \
        -a "${REDIS_AUTH_TOKEN}" PING
'
# Expected: PONG.
```

### 11.6 IAM patch — ESO ClusterSecretStore

The prod ClusterSecretStore `aws-secrets-manager-prod` (W4) was
scoped to `mahjong/prod/*` (Secrets Manager) +
`/mahjong/prod/auth/jwt/*` (SSM). W11 extends the SSM scope to
include the Redis params:

```hcl
# Trust policy delta (in infra/terraform/modules/github-oidc/
# or the ESO controller's IAM role module).
statement {
  effect    = "Allow"
  actions   = ["ssm:GetParameter", "ssm:GetParameters"]
  resources = [
    "arn:aws:ssm:*:*:parameter/mahjong/prod/auth/jwt/*",
    "arn:aws:ssm:*:*:parameter/mahjong/prod/redis/*",   # ← W11
  ]
}
```

Apply with `terraform plan` against the IAM module; the W6
narrow-policy invariant (`docs/terraform.md §3`) still holds —
the prefix `/mahjong/prod/redis/*` only matches the parameters
this overlay needs.

## 13. Cross-references (legacy index)

See §11 above. This section header is retained as an alias for
external links that may have indexed the W10 numbering.
