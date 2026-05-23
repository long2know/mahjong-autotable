# Production Deployment Runbook — Mahjong Autotable

> **Owner:** Apone (DevOps). **Wave:** Phase J Wave 10 — final pass for
> Phase J. **Audience:** the operator who runs `kubectl apply` / `docker
> run` against the live cluster.
>
> This document is the single source of truth for every step that
> separates a freshly-merged `main` SHA from a healthy, observable,
> rollback-safe production deploy. If anything in the system disagrees
> with this runbook, the runbook wins until amended via a PR + Stephen's
> sign-off in [`.squad/decisions.md`](../.squad/decisions.md).

---

## Table of contents

1. [Pre-flight checks](#1-pre-flight-checks)
2. [Image build + publish](#2-image-build--publish-to-ghcrio)
3. [First-deploy initialization](#3-first-deploy-initialization)
4. [Rolling update procedure](#4-rolling-update-procedure)
5. [Rollback procedure](#5-rollback-procedure)
6. [Monitoring + alerting](#6-monitoring--alerting-setup)
7. [Incident response runbook](#7-incident-response-runbook)
8. [Continuous health probes (W10)](#8-continuous-health-probes-w10)
9. [Companion docs](#9-companion-docs)

---

## 1. Pre-flight checks

Run every box on this list before pushing the `main` SHA into prod.
Each line is **GO/NO-GO** — a single NO blocks the deploy.

### 1.1 Tooling versions

| Tool          | Required                          | Verify                         |
| ------------- | --------------------------------- | ------------------------------ |
| Docker Engine | ≥ 20.10 (multi-stage + buildx)    | `docker --version`             |
| Buildx        | ≥ 0.10 (multi-arch manifest list) | `docker buildx version`        |
| kubectl       | ≥ 1.27                            | `kubectl version --client`     |
| Kustomize     | ≥ 5.0 (or via `kubectl -k`)       | `kustomize version`            |
| Argo CD CLI   | ≥ 2.8 (optional — GitOps lane)    | `argocd version --client`      |
| .NET SDK      | 10.0.x (only on devbox, not host) | `dotnet --version`             |
| Node          | 20.x (only on devbox, not host)   | `node --version`               |

### 1.2 Environment variables required by the runtime

The image reads these at startup. Missing values that have no default
will cause the pod to crash-loop. See
[`docs/secret-management.md`](./secret-management.md) for the canonical
list and which knobs are operator-tunable vs secret-rotated.

| Variable                                | Purpose                                                   | Sensitive |
| --------------------------------------- | --------------------------------------------------------- | :-------: |
| `ConnectionStrings__Sqlite`             | SQLite path **OR**                                        |           |
| `ConnectionStrings__Postgres`           | …Postgres conn-string **OR**                              |    🔒    |
| `ConnectionStrings__SqlServer`          | …SQL Server conn-string                                   |    🔒    |
| `Database__Provider`                    | `Sqlite` / `Postgres` / `SqlServer`                       |           |
| `Auth__Google__ClientId/Secret`         | OAuth provider (Wave 8)                                   |    🔒    |
| `Auth__Github__ClientId/Secret`         | OAuth provider (Wave 8)                                   |    🔒    |
| `Auth__MagicLink__SmtpHost/Port/Creds`  | Magic-link email transport                                |    🔒    |
| `Security__CspStrict`                   | `true` ⇒ drop `'wasm-unsafe-eval'` from CSP               |           |
| `Security__CspStrictStyles`             | Wave 10 — `true` ⇒ drop `'unsafe-inline'` from style-src  |           |
| `Security__CspReportOnly`               | `true` ⇒ canary mode (no enforcement, reports only)       |           |
| `Security__CspReportUri`                | Override report sink (default `/api/csp-report`)          |           |
| `Security__EnableHsts`                  | Opt-in after HTTPS is confirmed stable                    |           |
| `Sentry__Dsn`                           | Sentry SaaS or self-hosted DSN                            |    🔒    |
| `Sentry__Environment`                   | `production` / `staging`                                  |           |
| `RateLimit__General__*`                 | See [`docs/rules`](./rules/) for sane defaults            |           |
| `BUILD_SHA`                             | Baked at image build; **do not override**                 |           |

> All sensitive values must come from the cluster secret store (k8s
> `Secret` resources, mounted as env). Never bake secrets into images
> or commit them to git. See
> [`docs/secret-management.md`](./secret-management.md) for the kustomize
> overlay + sealed-secrets workflow we use.

### 1.3 DNS, TLS, edge

- `mahjong.<domain>` resolves to the cluster ingress IP.
- A valid TLS certificate (Let's Encrypt via cert-manager, or
  Cloudflare-managed) is provisioned. See
  [`docs/cloudflare.md`](./cloudflare.md) +
  [`docs/reverse-proxy.md`](./reverse-proxy.md).
- Edge cache rules:
  - Hashed bundles (`*.????????.js|css|png|wav|glb`) →
    `public, max-age=31536000, immutable` (already set by
    `SecurityHeadersMiddleware`).
  - `index.html` and `/api/*` → bypass cache.
- WebSocket upgrade enabled on the path prefix used by
  `/api/autotable/relay`.

### 1.4 Database

- Backups freshly taken — see
  [`docs/backup-restore.md`](./backup-restore.md). For Postgres,
  `pg_dump --format=custom` snapshot held off-cluster.
- DB version compatible with the EF Core migration set in
  `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/`. See
  [`docs/database-providers.md`](./database-providers.md).
- Connection limit headroom: each pod opens up to
  `Database__MaxPoolSize` (default 100) connections; verify
  `pods × pool_size ≤ db_max_connections × 0.8`.

### 1.5 Observability stack ready

- Prometheus scraping the `/metrics` endpoint on each pod (see
  [`docs/observability.md`](./observability.md)).
- Sentry project receiving events (see
  [`docs/sentry.md`](./sentry.md)).
- Log aggregator (Loki / Datadog / your-favourite) ingesting
  stdout JSON logs — see [`docs/log-rotation.md`](./log-rotation.md).
- CSP-report sink (`/api/csp-report` — Wave 9) reachable from
  browsers (no auth wall on this path).

### 1.6 Build provenance

- The `main` SHA you intend to deploy has a green build in
  [`docker-build.yml`](../.github/workflows/docker-build.yml) AND
  [`sbom.yml`](../.github/workflows/sbom.yml). The SBOM workflow's
  Trivy gate (CRITICAL / HIGH, fixable only) must be green — no
  open vulnerabilities. See [`docs/sbom.md`](./sbom.md).
- The `release.yml` workflow has produced a versioned image tag if
  this is a tagged release.

---

## 2. Image build + publish (to ghcr.io)

### 2.1 CI path (recommended)

Every push to `main` and every `v*.*.*` tag triggers
[`docker-build.yml`](../.github/workflows/docker-build.yml). The
workflow:

1. Sets up Buildx + QEMU (`docker/setup-qemu-action`).
2. Logs into `ghcr.io` using the workflow's `GITHUB_TOKEN`.
3. Builds the single multi-stage `Dockerfile` for **both**
   `linux/amd64` and `linux/arm64` (Wave 10 multi-arch — Stephen's
   Linux server can be either architecture).
4. Publishes a single manifest list with these tags:
   - `latest` (only on `main` pushes)
   - `sha-<commit>` (immutable rollback target — every build)
   - `<git-tag>` (only on `refs/tags/v*.*.*` pushes)
5. Bakes `BUILD_SHA=${GITHUB_SHA}` into the image so
   `/health` returns the actual commit SHA.
6. Emits the manifest digest to the run summary — copy this for
   the deployment record.

The companion [`release.yml`](../.github/workflows/release.yml)
workflow promotes a tag image to the production registry alias.

### 2.2 Local path (for verification / air-gapped builds)

```bash
# One-time: install QEMU emulator for arm64 + create a multi-arch builder.
docker run --privileged --rm tonistiigi/binfmt --install arm64
docker buildx create --name mahjong-multiarch --use --bootstrap

# Build (no push — local manifest)
docker buildx build \
  --builder mahjong-multiarch \
  --platform linux/amd64,linux/arm64 \
  -t mahjong-autotable:wave10 \
  --build-arg BUILD_SHA="$(git rev-parse HEAD)" \
  .

# Build + push to ghcr.io
echo "$GH_TOKEN" | docker login ghcr.io -u <username> --password-stdin
docker buildx build \
  --builder mahjong-multiarch \
  --platform linux/amd64,linux/arm64 \
  -t ghcr.io/long2know/mahjong-autotable:sha-$(git rev-parse HEAD) \
  -t ghcr.io/long2know/mahjong-autotable:latest \
  --build-arg BUILD_SHA="$(git rev-parse HEAD)" \
  --push \
  .
```

The Dockerfile is arch-agnostic — every `FROM` clause targets an
official multi-arch base image (`node:20-alpine`,
`mcr.microsoft.com/dotnet/sdk:10.0`,
`mcr.microsoft.com/dotnet/aspnet:10.0`). No conditional logic needed.

See also: [`docs/docker.md`](./docker.md) for the single-arch
development quick-start.

---

## 3. First-deploy initialization

Only required for a brand-new environment (new namespace, new DB).
Skip to §4 for a rolling update on an existing environment.

### 3.1 Cluster prep

```bash
kubectl create namespace mahjong
kubectl label namespace mahjong istio-injection=enabled  # optional service-mesh
kubectl apply -k infra/k8s/overlays/production
```

`infra/k8s/overlays/production` is the canonical overlay — it
references `infra/k8s/base` (Deployment, Service, Ingress,
ConfigMap, the Wave-9 migration Job) plus production-specific
patches (replicas, resource limits, env overrides, sealed
secrets). See [`docs/kubernetes.md`](./kubernetes.md) for the full
manifest tree and overlay strategy.

### 3.2 Secret rotation (first install)

```bash
# Generate fresh sealed secrets from a local plaintext file (NEVER commit plaintext).
kubectl create secret generic mahjong-secrets \
  --from-env-file=./prod.env \
  --namespace=mahjong \
  --dry-run=client -o yaml \
  | kubeseal --controller-namespace=kube-system -o yaml \
  > infra/k8s/overlays/production/mahjong-sealed-secret.yaml
```

Commit the **sealed** file (encrypted) but never the plaintext
`prod.env`. The sealing happens against the cluster's controller
public key — see [`docs/secret-management.md`](./secret-management.md)
for key rotation cadence (90 days) and the procedure for an
emergency re-seal.

### 3.3 Pre-rollout DB migration (Wave 9)

The k8s manifest `infra/k8s/base/job-migrate.yaml` runs the image
in `--migrate` mode. With Argo CD, the Job carries
`sync-wave: -1` + `hook: PreSync` so it always completes before
the Deployment rolls.

For plain kubectl:

```bash
# Apply the migration Job and wait for it to complete.
kubectl apply -f infra/k8s/base/job-migrate.yaml
kubectl wait --for=condition=complete --timeout=300s \
  job/mahjong-migrate -n mahjong
kubectl logs -n mahjong job/mahjong-migrate
```

The `--migrate` entrypoint resolves the configured provider:

- **Postgres / SQL Server:** runs `dbContext.Database.MigrateAsync()`
  (EF Core idempotent migration history).
- **SQLite:** runs `DatabaseBootstrapper.InitializeAsync(db)`
  (idempotent schema-ensure).

It exits 0 on success, non-zero on failure. The Job has
`backoffLimit: 3` and `ttlSecondsAfterFinished: 600` so it cleans
itself up.

### 3.4 First smoke

```bash
kubectl rollout status deploy/mahjong -n mahjong --timeout=180s
INGRESS=$(kubectl get ingress mahjong -n mahjong -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
curl -fsS "https://${INGRESS}/health" | jq .
# expect: { "status": "ok", "sha": "<commit-sha>", "providers": {…} }
```

If `/health` returns the expected JSON with the correct commit
SHA — the first deploy is GA.

---

## 4. Rolling update procedure

For an existing environment receiving a fresh `main` SHA.

### 4.1 Confirm the build artifact

```bash
SHA=<commit-sha>
docker pull ghcr.io/long2know/mahjong-autotable:sha-${SHA}
docker manifest inspect ghcr.io/long2know/mahjong-autotable:sha-${SHA} \
  | jq '.manifests[].platform'
# expect: linux/amd64 AND linux/arm64
```

### 4.2 Schema migration (if any)

If `Persistence/Migrations/*` changed in this SHA, re-apply the
migration Job first — exactly as in §3.3. The Job is idempotent;
EF Core's `__EFMigrationsHistory` (Postgres / SQL Server) or the
bootstrapper's per-table `EnsureXAsync` checks (SQLite) skip
already-applied migrations.

For breaking schema changes (column drops, type narrowing), follow
the expand-then-contract pattern documented in
[`docs/database-providers.md`](./database-providers.md): ship the
expand migration in deploy N, then the contract migration in
deploy N+1 after the old code is gone.

### 4.3 Update the image tag

```bash
# Edit overlay to point at the new sha tag.
sed -i "s/sha-[a-f0-9]\{40\}/sha-${SHA}/g" \
  infra/k8s/overlays/production/kustomization.yaml
git commit -am "deploy: roll mahjong to sha-${SHA}"
git push origin main
```

GitOps (Argo CD) picks up the change automatically. Without
GitOps:

```bash
kubectl set image deploy/mahjong -n mahjong \
  app=ghcr.io/long2know/mahjong-autotable:sha-${SHA}
kubectl rollout status deploy/mahjong -n mahjong --timeout=300s
```

The Deployment's `strategy: RollingUpdate` with `maxSurge: 1` +
`maxUnavailable: 0` means the cluster spins up one new pod, waits
for its readiness probe (`/health?simple=1`) to go green, then
terminates one old pod — no downtime.

### 4.4 Post-deploy verification

```bash
# 1. Every pod reports the new SHA.
kubectl get pods -n mahjong -l app=mahjong \
  -o jsonpath='{range .items[*]}{.metadata.name}{"\t"}{.spec.containers[0].image}{"\n"}{end}'

# 2. /health on each pod returns the expected SHA.
for pod in $(kubectl get pods -n mahjong -l app=mahjong -o name); do
  kubectl exec -n mahjong $pod -- curl -fsS localhost:8080/health \
    | jq -r '.sha'
done

# 3. WebSocket relay still serving (lobby smoke).
curl -fsS https://mahjong.<domain>/api/lobby/games | jq '. | length'
```

If any pod reports the **old** SHA, the rollout did not complete —
investigate before clearing the deploy bell.

---

## 5. Rollback procedure

### 5.1 Identify the last-known-good SHA

Every successful prod deploy is committed to git as the
`sha-<commit>` tag in the overlay's kustomization. Walk back the
log:

```bash
git log --oneline --grep='^deploy: roll mahjong to sha-' | head -10
```

Pick the highest-numbered green deploy.

### 5.2 Roll back the image (fast path)

```bash
LKG=<last-known-good-sha>
kubectl set image deploy/mahjong -n mahjong \
  app=ghcr.io/long2know/mahjong-autotable:sha-${LKG}
kubectl rollout status deploy/mahjong -n mahjong --timeout=300s
```

This is **always** safe within the same major schema generation —
EF Core migrations are idempotent and the SQLite bootstrapper is
read-only when schema matches.

### 5.3 Roll back a schema change

If the rollback target predates a migration applied in the failed
deploy, you need the contract step. Choose:

- **A. Forward-fix in place.** Ship a hotfix that's compatible
  with the new schema. Lowest risk.
- **B. Restore from backup.** See
  [`docs/backup-restore.md`](./backup-restore.md). This loses
  every commit applied since the backup snapshot, so confirm
  with Stephen first.
- **C. Author a reverse migration.** Write
  `Persistence/Migrations/N_RevertX.cs` that undoes the change,
  ship via the migration Job, then roll back the image.

The Wave-9 migration-Job pattern lets you re-run §3.3 against
the LKG image — but only if you've author a reverse migration
first (B / C above).

### 5.4 Git revert

For a permanent rollback (not a temporary one), revert the deploy
commit on `main`:

```bash
git revert <bad-deploy-commit>
git push origin main
```

GitOps re-syncs to the reverted overlay automatically.

---

## 6. Monitoring + alerting setup

### 6.1 Prometheus

The runtime exposes `/metrics` on the same Kestrel port (8080).
Scrape config (see [`docs/observability.md`](./observability.md)):

```yaml
scrape_configs:
  - job_name: mahjong
    kubernetes_sd_configs: [{ role: pod, namespaces: { names: [mahjong] } }]
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_label_app]
        regex: mahjong
        action: keep
```

Key series:

| Series                          | Type     | Purpose                                  |
| ------------------------------- | -------- | ---------------------------------------- |
| `mahjong_connected_clients`     | gauge    | WS connections per pod                   |
| `mahjong_active_games`          | gauge    | Live game count per pod                  |
| `mahjong_hand_completions_total`| counter  | Hands won (per pod)                      |
| `mahjong_ratelimit_rejects_total`| counter | 429s emitted (per route)                 |
| `mahjong_csp_violations_total`  | counter  | CSP violations posted to `/api/csp-report`|
| `aspnetcore_*`                  | std      | Stock ASP.NET Core meters                |

Alert rules — copy into Prometheus AlertManager:

```yaml
groups:
  - name: mahjong.production
    rules:
      - alert: MahjongHighErrorRate
        expr: rate(aspnetcore_request_duration_seconds_count{status=~"5.."}[5m]) > 0.1
        for: 5m
        labels: { severity: page }
        annotations: { summary: "Mahjong 5xx > 0.1/s for 5m" }
      - alert: MahjongPodCrashLoop
        expr: rate(kube_pod_container_status_restarts_total{namespace="mahjong"}[10m]) > 0.1
        for: 5m
        labels: { severity: page }
      - alert: MahjongWSReconnectStorm
        expr: rate(mahjong_ws_reconnect_total[5m]) > 5
        for: 10m
        labels: { severity: ticket }
      - alert: MahjongCspViolationSpike
        expr: rate(mahjong_csp_violations_total[15m]) > 0.5
        for: 15m
        labels: { severity: ticket }
        annotations: { summary: "CSP violation spike — possible XSS attempt or bundle regression" }
```

### 6.2 Sentry

DSN configured via `Sentry__Dsn`. Source maps uploaded by the
release workflow. See [`docs/sentry.md`](./sentry.md) for:

- Project setup
- Source-map upload (frontend bundle Wave 8)
- Performance sampling rate (production: 0.1)
- Release tag scheme (`mahjong@${BUILD_SHA}`)

### 6.3 JSON structured logs

`Program.cs` configures Serilog with JSON output on stdout. Each
log line carries:

- `@timestamp` (ISO-8601)
- `level` (`Information` / `Warning` / `Error`)
- `messageTemplate`, `properties.*`
- `traceId` + `spanId` (W3C TraceContext)
- `gameId` (when in a game request scope)
- `playerId` (anonymized — see audit log policy)

Ship these to your log aggregator (Loki / Datadog / CloudWatch
Logs). Rotation policy on the host is handled by Docker's
`json-file` driver with `max-size=100m max-file=5` — see
[`docs/log-rotation.md`](./log-rotation.md).

---

## 7. Incident response runbook

Every incident: open a ticket, copy the relevant section's actions
into the ticket as a checklist, work the checklist top-to-bottom.

### 7.1 Database connection issues

**Symptom:** Pods crash-looping with `Npgsql.PostgresException:
SCRAM-SHA-256 authentication failed` or
`SqliteException: database is locked`.

**Triage:**

1. `kubectl logs -n mahjong -l app=mahjong --tail=200 | grep -iE 'connection|auth'`
2. `kubectl exec -n mahjong deploy/mahjong -- env | grep ConnectionStrings__`
   — confirm the env var is set; if the value is wrong, the secret
   has rotated out of sync (see [`docs/secret-management.md`](./secret-management.md)).
3. `kubectl get pods -n <db-namespace>` — confirm DB itself is up.
4. From a pod, try a direct connection:
   `kubectl exec -n mahjong deploy/mahjong -- /app/Mahjong.Autotable.Api --migrate`
   — this exits 0 if DB is reachable.

**Resolution:**

- Wrong credentials → re-seal the sealed secret (see §3.2);
  `kubectl rollout restart deploy/mahjong -n mahjong`.
- DB itself down → see the DB's own runbook;
  `mahjong-autotable` will resume auto-reconnect once DB is up
  (Npgsql + EF Core have built-in retry policies).
- SQLite "database is locked" → indicates writer contention.
  Switch to Postgres for production; SQLite is intended for
  development + the single-pod compose-up path (see
  [`docs/database-providers.md`](./database-providers.md)).

### 7.2 Rate-limit storm

**Symptom:** `mahjong_ratelimit_rejects_total` spikes; users
report "Too many requests" or 429s in browser devtools.

**Triage:**

1. `kubectl logs -n mahjong -l app=mahjong --tail=500 | grep -i 'ratelimit'`
2. `kubectl exec -n mahjong deploy/mahjong -- curl -fsS localhost:8080/metrics | grep ratelimit`
3. Identify the offending IP / player ID from the logs:
   ```
   {"level":"Warning","message":"Rate limited","ip":"1.2.3.4","route":"/api/lobby/games",…}
   ```

**Resolution:**

- **Legitimate spike (popular event):** Temporarily relax the
  limit by patching the ConfigMap.
  ```bash
  kubectl patch configmap mahjong-config -n mahjong --type=merge \
    -p '{"data":{"RateLimit__General__PermitLimit":"200"}}'
  kubectl rollout restart deploy/mahjong -n mahjong
  ```
  Revert via the same command once the spike subsides.
- **Malicious / abusive client:** Block at the edge.
  Cloudflare → Firewall Rules → block the offending IP /
  ASN; see [`docs/cloudflare.md`](./cloudflare.md).
- **Bot scraping the lobby API:** Add the route to the
  per-route ConfigMap entry with a stricter ceiling.

### 7.3 OAuth provider down (Google / GitHub)

**Symptom:** New sign-ins time out; logs show
`AuthenticationFailureException: oauth callback failed`.

**Triage:**

1. Check the provider status page
   (`https://www.google.com/appsstatus/dashboard/`,
   `https://www.githubstatus.com`).
2. Existing sessions / reconnect tokens (Wave 9) keep working
   regardless — only **new** sign-ins are blocked.

**Resolution:**

- If the outage is < 10 minutes: do nothing. The frontend's
  `auth.ts` retry path handles the transient failure.
- If sustained: switch users to magic-link sign-in. The link
  on the login form is always available — no infrastructure
  change required. Magic-link is fully self-hosted (SMTP via
  `Auth__MagicLink__SmtpHost`); see [`docs/secrets.md`](./secrets.md).
- Disable the broken provider button in the bundle (forces a
  fresh deploy — only worth it for a sustained outage).

### 7.4 Magic-link queue backlog

**Symptom:** Users report magic-link emails never arrive;
`mahjong_magiclink_pending` gauge climbs.

**Triage:**

1. `kubectl logs -n mahjong -l app=mahjong --tail=500 | grep -i magiclink`
2. Confirm SMTP host is reachable:
   ```bash
   kubectl exec -n mahjong deploy/mahjong -- \
     timeout 5 nc -vz $(echo $Auth__MagicLink__SmtpHost) 587
   ```
3. Check SMTP creds with the provider's dashboard — many
   providers throttle / suspend on abuse signals (high bounce
   rate, blacklist hit).

**Resolution:**

- SMTP creds wrong / expired → rotate via sealed secret (§3.2);
  re-deploy.
- Provider rate-limiting → switch to backup transport. The
  `Auth__MagicLink__SmtpHost` is operator-tunable; pointing at
  a different SMTP service is a ConfigMap patch + restart.
- Persistent backlog → flush the pending queue manually:
  ```bash
  kubectl exec -n mahjong deploy/mahjong -- \
    /app/Mahjong.Autotable.Api --magiclink-flush
  ```
  (Future CLI flag — track in known-limitations until landed.)

### 7.5 CSP violation flood

**Symptom:** `mahjong_csp_violations_total` rate jumps after a
deploy.

**Triage:**

1. `kubectl exec -n mahjong deploy/mahjong-postgres -- \
   psql -d mahjong -c "SELECT violated_directive, COUNT(*) FROM csp_violations \
   WHERE created_at > now() - interval '1 hour' GROUP BY 1 ORDER BY 2 DESC LIMIT 20;"`
2. Identify which directive (`script-src` / `style-src` /
   `connect-src`) and which sources are violating.

**Resolution:**

- **Genuine regression** (bundle started using inline
  `<style>` or eval) → revert the deploy (§5) and investigate
  the bundle build.
- **Browser-extension noise** (common — ad-blockers and
  password managers inject inline styles) → leave it. The
  policy still protects against actual XSS.
- **Intentional new feature** (e.g. embedded analytics) →
  widen the policy via overlay env: set
  `Security__CspStrict=false` and / or add an explicit
  override via `Security__ContentSecurityPolicy=…`.

---

## 8. Continuous health probes (W10)

> Phase K Wave 10 — Apone (DevOps).

The W6 Sentry + W7 Prometheus stack is **reactive** — it tells
us that a request failed after the user noticed it. W10 adds a
**synthetic** probe that runs every 5 minutes from GitHub-hosted
runners, hits the live edge, and opens an incident issue when
the probe fails repeatedly.

### 8.1 What it checks

The probe (`.github/workflows/prod-health-check.yml`) calls
three endpoints in sequence:

| Path                             | Asserted on                              |
| -------------------------------- | ---------------------------------------- |
| `GET /healthz`                   | HTTP 200; JSON body has `"status":"ok"`. |
| `GET /readyz`                    | HTTP 200; latency < 1500 ms.             |
| `GET /metrics`                   | HTTP 200; body size > 1024 bytes.        |
| `GET /.well-known/jwks.json`     | HTTP 200; JSON body has ≥ 3 `keys`.      |

`/healthz` is the liveness probe (W8 — checks process is up),
`/readyz` is readiness (W8 — checks DB + Redis reachability),
`/metrics` is the Prometheus surface (W7), and `/.well-known/
jwks.json` is the JWT key publication surface (W7 + W10 §3).

### 8.2 Failure behaviour

The workflow uses a **3-strike cooldown**:

1. A single 5-minute probe failure marks the run as failed,
   logs the failure to the step summary, and writes a
   workflow-state file as an artefact.
2. After three consecutive failed runs (i.e. 15 minutes of
   sustained failure), the workflow opens a GitHub issue with
   labels `incident`, `automated`, `production` and posts a
   Slack webhook notification (if `SLACK_WEBHOOK_URL` secret
   is configured).
3. While an incident issue is open, subsequent failures
   **update** the issue body with a running probe-status log
   rather than opening duplicates. The cooldown prevents
   spam: at most one issue per outage window.
4. When the probe is green again for two consecutive runs,
   the workflow closes the incident issue with a comment
   summarising the outage window.

### 8.3 Operator integration

* The probe is **not** a substitute for the on-call pager
  — it's a backstop. The Sentry alert rules in §6 still fire
  faster (typically within 30-60 s of the first 500).
* The probe runs from `ubuntu-latest` runners; if GitHub
  Actions is itself degraded, the probe will appear to "fail"
  even when the app is healthy. The incident issue body
  includes the runner region + a check on `https://www.
  githubstatus.com/` so triage can distinguish the two cases.
* Disabling the probe (e.g. during a planned maintenance
  window): set the `pause_until` workflow input via
  `workflow_dispatch`. The cron job aborts early when the
  current UTC time is before the pause expiry.

### 8.4 Configuration

The probe target URL is the `PROD_BASE_URL` repository
variable (NOT secret — the URL itself is public). Default:
`https://api.mahjong-autotable.com`. Override per-run via
`workflow_dispatch.inputs.target_url`.

The Slack notification uses the `SLACK_WEBHOOK_URL`
repository secret. Absent secret → the workflow still
opens the GitHub issue (Slack is best-effort).

### 8.5 Disabling / troubleshooting

* **Probe is flaking but app is healthy:** check the
  GitHub Actions runner region (logged in the step summary).
  Cross-region latency from `us-east` runner → `eu-west`
  edge can exceed the 1500 ms `/readyz` budget; relax via
  `workflow_dispatch.inputs.readyz_latency_budget_ms`.
* **Probe is silent (no incident issue) but app is down:**
  check the workflow's last run — if `workflow_run` UI shows
  no recent execution, GitHub-hosted runners may be backlogged.
  The probe is best-effort; do not depend on it for
  hard-SLA paging.
* **Need to suppress paging without disabling the probe:**
  remove the `incident` label from the open issue — the next
  failed run will not reopen it.

### 8.6 Cross-references

* [`docs/observability.md`](./observability.md) — Prometheus +
  Grafana dashboards (the metrics the probe hits).
* §6 above — alert rules; the W10 probe complements the
  Sentry / Prometheus alerts (synthetic vs reactive).
* §7 above — incident response runbook; the W10 probe's
  generated issue includes a link to §7's first-response steps.

---

## 9. Companion docs

| Topic                                | Doc                                                                |
| ------------------------------------ | ------------------------------------------------------------------ |
| Architecture overview                | [`architecture.md`](./architecture.md)                             |
| Single-container Docker quickstart   | [`docker.md`](./docker.md)                                         |
| General deployment notes             | [`deployment.md`](./deployment.md)                                 |
| Kubernetes manifests + overlays      | [`kubernetes.md`](./kubernetes.md)                                 |
| Database providers + migrations      | [`database-providers.md`](./database-providers.md)                 |
| Reverse-proxy (nginx, Traefik)       | [`reverse-proxy.md`](./reverse-proxy.md)                           |
| Cloudflare edge config               | [`cloudflare.md`](./cloudflare.md)                                 |
| Sentry observability                 | [`sentry.md`](./sentry.md)                                         |
| Prometheus + structured logs         | [`observability.md`](./observability.md)                           |
| Secret management + rotation         | [`secret-management.md`](./secret-management.md)                   |
| Secret inventory (env vars)          | [`secrets.md`](./secrets.md)                                       |
| Backup + restore                     | [`backup-restore.md`](./backup-restore.md)                         |
| SBOM + Trivy supply-chain gate       | [`sbom.md`](./sbom.md)                                             |
| systemd unit (non-k8s deploy)        | [`systemd.md`](./systemd.md)                                       |
| Log rotation                         | [`log-rotation.md`](./log-rotation.md)                             |
| CI pipeline + gates                  | [`ci.md`](./ci.md)                                                 |
| Known limitations                    | [`known-limitations.md`](./known-limitations.md)                   |
| Argo Rollouts cluster install (W10)  | [`argo-rollouts-setup.md`](./argo-rollouts-setup.md)               |
| Redis ElastiCache provisioning (W10) | [`redis-cluster.md`](./redis-cluster.md)                           |

---

## Appendix — Wave-10 changes vs Wave-9

- Multi-arch image (`linux/amd64` + `linux/arm64`) — see
  [`.github/workflows/docker-build.yml`](../.github/workflows/docker-build.yml).
- `Security__CspStrictStyles` knob — drop `'unsafe-inline'` from
  `style-src` once Hicks's inline-style-free bundle ships
  (canary via `Security__CspReportOnly=true` first).
- This runbook + [`docs/load-test-results.md`](./load-test-results.md).
