# Kubernetes deployment

Phase J Wave 7 — Apone (DevOps).

The `infra/k8s/` tree ships a Kustomize-based base + overlays for
the mahjong-autotable runtime. Designed for a vanilla `nginx-ingress`
cluster with `cert-manager` issuing Let's Encrypt certs; the
substitutions for other ingress controllers (Traefik, ALB, etc.) are
small and called out below.

## Layout

```
infra/k8s/
  base/
    configmap.yaml          ← non-secret env (provider, CORS, rate limit)
    secret-template.yaml    ← *template only* — connection strings
    pvc.yaml                ← 2Gi RWO PVC for SQLite path
    deployment.yaml         ← 2-replica rolling-update Deployment
    service.yaml            ← ClusterIP, named `http` port
    ingress.yaml            ← TLS + sticky-cookie session affinity
    hpa.yaml                ← CPU + memory HPA (min 2 / max 8)
    kustomization.yaml
  overlays/
    staging/kustomization.yaml   ← 1 replica, staging.* host
    prod/kustomization.yaml      ← 3 replicas, prod.* host, Postgres
```

## Cluster assumptions

| Component       | Default                                  | Override via                            |
| --------------- | ---------------------------------------- | --------------------------------------- |
| Ingress         | `ingressClassName: nginx`                | edit `base/ingress.yaml` `spec.ingressClassName` |
| TLS issuer      | `cert-manager.io/cluster-issuer: letsencrypt-prod` | annotation patch in overlay     |
| Storage class   | cluster default                          | `pvc.yaml` `storageClassName` (commented out by default) |
| Image registry  | `ghcr.io/long2know/mahjong-autotable`    | overlay `images:` block                 |
| Image pull secret | `ghcr-pull` (Opaque, dockerconfigjson)  | `deployment.yaml` `imagePullSecrets`    |

### ghcr.io image-pull secret

Create once per namespace:

```bash
kubectl create secret docker-registry ghcr-pull \
  --docker-server=ghcr.io \
  --docker-username=<github-username> \
  --docker-password=<pat-with-read:packages> \
  --docker-email=<email> \
  -n mahjong-staging
```

The pull secret is referenced by name in `deployment.yaml`; both
overlays inherit it. PATs with only `read:packages` are sufficient.

### cert-manager

Assumes a cluster-scoped `letsencrypt-prod` ClusterIssuer with a
working HTTP-01 (or DNS-01) solver. If you use a different name —
e.g. `letsencrypt`, `selfsigned` — patch the annotation in
`base/ingress.yaml`:

```yaml
metadata:
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt
```

## Sticky sessions (WebSocket affinity)

The autotable's raw WS endpoint (`/autotable/ws`) and the SignalR hub
(`/hubs/changsha`) are long-lived bidirectional transports. Once a
client upgrades to WS, every subsequent frame must land on the same
pod or the SignalR / WS state machine resets.

`base/ingress.yaml` enables cookie-based affinity through the
nginx-ingress annotations:

```yaml
nginx.ingress.kubernetes.io/affinity: "cookie"
nginx.ingress.kubernetes.io/affinity-mode: "persistent"
nginx.ingress.kubernetes.io/session-cookie-name: "mahjong_aff"
nginx.ingress.kubernetes.io/session-cookie-max-age: "86400"
nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
```

For Traefik, the equivalent is the `sticky` setting on the IngressRoute
+ `traefik.ingress.kubernetes.io/affinity: "true"` (Traefik 2.x). For
the AWS ALB controller, use
`alb.ingress.kubernetes.io/target-group-attributes: "stickiness.enabled=true,stickiness.type=lb_cookie,stickiness.lb_cookie.duration_seconds=86400"`.

## Probes

| Probe       | Path     | Owner contract                          |
| ----------- | -------- | --------------------------------------- |
| Liveness    | `/health` | Phase J Wave 3 (Program.cs)             |
| Readiness   | `/health` | Same — Vasquez's `K8sManifestSanityTests` pins both |

`/health` is **rate-limit-disabled** (Phase J Wave 6) so the kubelet's
~10s readiness poll never trips the limiter. The container also exposes
the legacy `/api/health` short-form as a HEALTHCHECK fallback (see
Dockerfile).

## Resources + HPA

Base requests are conservative (`100m`/`192Mi`) for a single replica.
The `prod` overlay raises requests to `250m`/`384Mi` and limits to
`1000m`/`1Gi`. HPA targets 70% CPU + 80% memory with a 5-minute
scale-down stabilisation window so an in-flight game isn't evicted by
a transient lull.

## Security context

`base/deployment.yaml` requests the Pod Security Standard `restricted`
profile equivalent:

- `runAsNonRoot: true`, `runAsUser: 1000`, `runAsGroup: 1000` (matches
  the Dockerfile `USER 1000:1000` directive).
- `readOnlyRootFilesystem: true` (writes go to the `data` PVC + an
  `emptyDir` tmp mount).
- `allowPrivilegeEscalation: false`, `capabilities.drop: [ALL]`,
  `seccompProfile.type: RuntimeDefault`.

## Deploying

```bash
# Staging
kubectl apply -k infra/k8s/overlays/staging

# Production (NB: replace REPLACE_ME values in the Secret first!)
kubectl apply -k infra/k8s/overlays/prod
```

The overlays generate prefixed names (`staging-mahjong-autotable`,
`prod-mahjong-autotable`) so the same cluster can host both.

## Database provider on k8s

The base ConfigMap defaults to `Persistence__Provider=Sqlite`. The
prod overlay flips to Postgres. When using SQLite + multiple replicas
you'll get write-skew flakes (SQLite is single-writer); set
`spec.replicas: 1` in the deployment patch, or — strongly preferred
— move to Postgres before scaling out.

See [`database-providers.md`](database-providers.md) for the full
provider story.

## Upgrades

The Deployment uses `RollingUpdate` with `maxSurge: 1`,
`maxUnavailable: 0` — there's always at least the configured replica
count serving traffic. WS-affinity-pinned clients will get a clean WS
close on their pod's eviction and the frontend's auto-reconnect path
(Phase J Wave 4) will land them on a healthy replica.

For database-schema-only changes, the API container runs `MigrateAsync`
at startup (Postgres / SQL Server) so the new pod migrates the shared DB
before serving traffic. Run **one** replica during a destructive
migration to avoid double-apply races.

### Pre-rollout migration Job (Phase J Wave 9)

In addition to the per-pod `MigrateAsync` boot path above, Wave 9 ships
a dedicated **pre-rollout** Kubernetes Job at
[`infra/k8s/base/job-migrate.yaml`](../infra/k8s/base/job-migrate.yaml).
The Job runs `dotnet Mahjong.Autotable.Api.dll --migrate` — a stand-
alone entrypoint (added in `Program.cs`) that boots the DI container
just far enough to resolve `AppDbContext`, runs
`db.Database.MigrateAsync()` (or `EnsureCreatedAsync` for SQLite), and
exits 0. The HTTP listener port is **never bound**, so the Job pod
doesn't fight the Deployment's readiness probe.

#### Why bother, when the Deployment already migrates on boot?

Three reasons:

1. **No double-apply race on multi-replica rollouts.** When two new
   pods start in parallel and both call `MigrateAsync` against the
   same schema, EF Core's `__EFMigrationsHistory` table serialises the
   work — but on Postgres specifically this can deadlock the lock-table
   acquisition during long-running ALTERs. Running the Job once,
   pre-rollout, removes the contention window entirely.
2. **Clean rollback signal.** If the migration fails, the Job goes RED
   *before* the new image rolls. The existing Deployment keeps serving;
   the operator can investigate without an active outage.
3. **Argo CD / Flux compatibility.** The Job carries
   `argocd.argoproj.io/sync-wave: -1` + `argocd.argoproj.io/hook: PreSync`
   so GitOps tools sequence it ahead of the Deployment automatically.

#### Wiring

The Job is referenced from
[`infra/k8s/base/kustomization.yaml`](../infra/k8s/base/kustomization.yaml)
under `resources:`, so `kubectl apply -k infra/k8s/base/` (or the
overlay equivalent) creates / updates it on every sync. The Job uses
the same image tag as the Deployment — Kustomize's `images:` patch
covers both — so the migration set baked into the image always
matches the API code that will start serving.

#### Manual invocation

For a one-off migration outside of a normal rollout (e.g. after
restoring from a Postgres backup taken before the latest migration
landed):

```bash
# Delete the previous Job (Job names are immutable; success-completed
# Jobs hang around for ttlSecondsAfterFinished — 10 min by default).
kubectl delete job mahjong-autotable-migrate --ignore-not-found

# Apply just the Job manifest (rest of the base is untouched).
kubectl apply -f infra/k8s/base/job-migrate.yaml

# Wait for the Job to complete.
kubectl wait --for=condition=complete job/mahjong-autotable-migrate --timeout=600s

# Inspect the migration log.
kubectl logs job/mahjong-autotable-migrate
```

#### Failure handling

`restartPolicy: OnFailure` + `backoffLimit: 3` — k8s retries a
crashed pod 3 times before marking the Job failed. The completed Pod is
GC'd after `ttlSecondsAfterFinished: 600` (10 min) so `kubectl get jobs`
stays scannable across many rollouts.

## Observability

- Prometheus scrape: `GET /metrics` (Phase J Wave 5 — `MetricsEndpoint`).
- Structured JSON logs in `ASPNETCORE_ENVIRONMENT=Production` (Phase J Wave 5).
- The Deployment doesn't ship a ServiceMonitor — your platform team's
  scrape config probably already has a wildcard for any pod with a
  `prometheus.io/scrape: "true"` annotation. Patch via overlay if needed.
