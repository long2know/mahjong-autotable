# Secrets handling

Phase J Wave 5 (Apone, DevOps) audit of the `mahjong-autotable`
deployment surface for hardcoded credentials, plus guidance for
operating the application securely once the team moves beyond local
SQLite.

## Audit findings (Phase J Wave 5)

### `Dockerfile`

Scanned every layer; no secrets. The `ENV` block declares operational
defaults only:

```dockerfile
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__Sqlite="Data Source=/data/mahjong-autotable.db" \
    Persistence__Provider=Sqlite
```

`ConnectionStrings__Sqlite` is a file path against a writable named
volume — SQLite has no auth, so this is not a credential. **No action.**

`ARG BUILD_SHA` / `ENV BUILD_SHA` carry the commit SHA. Not a secret;
it's a build identifier (see "Non-secrets" below).

### `docker-compose.yml`

Scanned. No secrets. Same env contract as the Dockerfile, with
`BUILD_SHA=${BUILD_SHA:-local}` passing the build identifier through.

### `appsettings.json`

**Finding (placeholder credentials in the version-controlled file):**

```json
"ConnectionStrings": {
  "Sqlite":     "Data Source=data/mahjong-autotable.db",
  "PostgreSql": "Host=localhost;Port=5432;Database=mahjong_autotable;Username=mahjong;Password=mahjong",
  "SqlServer":  "Server=localhost,1433;Database=mahjong_autotable;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true"
}
```

The PostgreSQL `Username=mahjong;Password=mahjong` and SQL Server
`User Id=sa;Password=YourStrong!Passw0rd` strings are **template /
local-dev placeholders only**, not credentials in use against any
production database. The active provider is `Sqlite` per
`Persistence.Provider`.

- **Risk today:** **None** — SQLite is the only active provider and
  has no auth concept.
- **Risk on migration:** if the team flips `Persistence:Provider` to
  `PostgreSql` or `SqlServer` without overriding the connection
  string via env var, the runtime will attempt to connect with these
  placeholder credentials.
- **Action when migrating:** override `ConnectionStrings__PostgreSql`
  or `ConnectionStrings__SqlServer` via the secret-store mechanism
  appropriate for the deployment target (see § Recipes below).
  Consider replacing the placeholder values in `appsettings.json`
  with a clearly-tagged stub (e.g. `Password=__OVERRIDE_VIA_ENV__`)
  so a forgotten override surfaces as a connect failure, not a silent
  fallback to the committed value.

### `appsettings.Development.json`

Scanned. Logging configuration only — no secrets, no auth.

### Workflows (`.github/workflows/*.yml`)

Tracked workflows scanned (`docker-build.yml`, `docker-smoke.yml`,
`e2e-playwright.yml`, `squad-heartbeat.yml`, `squad-issue-assign.yml`,
`squad-triage.yml`, `sync-squad-labels.yml`):

- No `password:` / `apiKey:` / static-token literals.
- `ghcr.io` push in `docker-build.yml` uses the auto-provisioned
  `GITHUB_TOKEN` (no manually-managed secret required — confirmed in
  Phase J Wave 4 memo).
- `BUILD_SHA: ${{ github.sha }}` — build identifier, not a secret.

**Untracked `squad-*.yml` files** are out-of-scope per Apone's
standing scope rule. They are pre-session scaffolding and are not
inspected here.

### `Program.cs`

Scanned. Environment-driven config only (`BUILD_SHA`, `Persistence`
section, `ConnectionStrings` section). No hardcoded credentials.

## Required environment variables

The values below are the contract every deployment target must honor.
Variables marked **secret** must be supplied via a real secret store
(see § Recipes); the rest can be plain env entries.

| Variable | Purpose | Secret? | Default |
|---|---|---|---|
| `ConnectionStrings__Sqlite` | EF Core SQLite connection. File path against a writable volume. | no (file path) | `Data Source=/data/mahjong-autotable.db` (Dockerfile) |
| `ConnectionStrings__PostgreSql` | Active when `Persistence__Provider=PostgreSql`. **Includes username + password.** | **yes** (when active) | placeholder in `appsettings.json` |
| `ConnectionStrings__SqlServer` | Active when `Persistence__Provider=SqlServer`. **Includes username + password.** | **yes** (when active) | placeholder in `appsettings.json` |
| `Persistence__Provider` | One of `Sqlite`, `PostgreSql`, `SqlServer`. Picks the EF provider. | no | `Sqlite` |
| `ASPNETCORE_ENVIRONMENT` | `Production`, `Development`, `Staging`. Drives logger formatter (JSON in Production). | no | `Production` (Dockerfile) |
| `ASPNETCORE_URLS` | Kestrel bind address(es). | no | `http://+:8080` (Dockerfile) |
| `DOTNET_RUNNING_IN_CONTAINER` | Hints the runtime about the host. | no | `true` (Dockerfile) |
| `DOTNET_EnableDiagnostics` | Disables the .NET diagnostic IPC (closes a small attack surface). | no | `0` (Dockerfile) |
| `BUILD_SHA` | Surfaces via `/health.buildSha` and `mahjong_build_info{sha=...}`. | **no — not a secret** | `dev` (resolved at request time) |
| `ChangshaRuntime__BotDecisionTimeoutMs` | Bot move cap. Tuning, not auth. | no | from `appsettings.json` |

`ASPNETCORE_*` variables sometimes carry secret-adjacent values
(`ASPNETCORE_Kestrel__Certificates__Default__Password` for TLS) — if
the team eventually terminates TLS inside the container, treat any
such variable as secret. The default Docker deploy terminates TLS at
a reverse proxy and binds Kestrel to HTTP only, so this is currently
not an issue.

## Non-secrets

These look "secrety" but are NOT credentials:

- **`BUILD_SHA`** — public commit SHA. Surfaces on `/health` and
  `/metrics` and is published in the `ghcr.io` image tag set. Never
  treat as secret.
- **SQLite file path** — even if the path contains the word
  "mahjong-autotable", the file's contents are not protected by the
  string; access control is filesystem-level.
- **`Persistence__Provider`** — string-typed switch, no auth.

## Recipes

### Local development

Use `.env.local` (already in `.gitignore`) for any per-developer override:

```dotenv
# .env.local — NOT checked in
ConnectionStrings__Sqlite=Data Source=data/dev.db
BUILD_SHA=dev-local
```

Then:

```bash
set -a
. ./.env.local
set +a
dotnet run --project src/backend/src/Mahjong.Autotable.Api
```

`docker-compose.override.yml` (also `.gitignore`d) is the compose
equivalent for compose-driven dev.

### Docker secrets (Swarm)

For Docker Swarm or a single-host compose setup, prefer
`docker secret` over plaintext env entries:

```bash
echo "Server=…;User Id=sa;Password=$(openssl rand -base64 24)" \
    | docker secret create mahjong-sqlserver-conn -
```

```yaml
# compose stub
services:
  mahjong:
    image: ghcr.io/long2know/mahjong-autotable:latest
    environment:
      Persistence__Provider: SqlServer
    secrets:
      - source: mahjong-sqlserver-conn
        target: /run/secrets/sqlserver-conn
    entrypoint: /bin/sh -c 'export ConnectionStrings__SqlServer="$(cat /run/secrets/sqlserver-conn)" && exec /usr/bin/tini -- dotnet Mahjong.Autotable.Api.dll'

secrets:
  mahjong-sqlserver-conn:
    external: true
```

### GitHub Actions secrets

For CI workflows that need a secret (none currently — the existing
workflows ride on `GITHUB_TOKEN`):

1. Repository → Settings → Secrets and variables → Actions → New
   repository secret.
2. Reference in the workflow: `${{ secrets.MY_SECRET }}` — **never**
   echo a secret unmasked; GitHub auto-redacts known secret values
   in step logs but only by exact-match.

If/when a workflow needs a real DB connection string, file the secret
as `DB_CONNECTION_STRING_PRODUCTION` and surface it as an env var on
the specific step that needs it, not at the job/workflow level.

### Kubernetes secrets

For a k8s deploy:

```yaml
# secret.yaml (apply with `kubectl apply -f secret.yaml`)
apiVersion: v1
kind: Secret
metadata:
  name: mahjong-db
  namespace: mahjong
type: Opaque
stringData:
  ConnectionStrings__PostgreSql: "Host=postgres;Port=5432;Database=mahjong;Username=mahjong;Password=…"
```

```yaml
# deployment.yaml fragment
spec:
  template:
    spec:
      containers:
        - name: mahjong-autotable
          image: ghcr.io/long2know/mahjong-autotable:latest
          envFrom:
            - secretRef:
                name: mahjong-db
          env:
            - name: Persistence__Provider
              value: PostgreSql
            - name: BUILD_SHA
              value: <commit-sha>   # populated by CD pipeline
```

For higher-grade secret management consider SOPS-encrypted manifests
or an external secret operator (External Secrets / Sealed Secrets);
either lets the secret manifest live in version control safely.

### Cloud-native secret stores

- **AWS:** ECS / EKS task definition `secrets` block backed by AWS
  Secrets Manager; surface each as an env var on the container.
- **Azure:** Container Apps `secretRef` pulling from Key Vault.
- **GCP:** Cloud Run `--set-secrets ConnectionStrings__Sqlite=sa-conn:latest`.

In all three cases the contract is identical: the application reads
ordinary env vars (`ConnectionStrings__*`, `Persistence__Provider`);
the platform takes care of injecting the secret material.

## Hygiene rules

1. **Never commit a real connection string.** The placeholders in
   `appsettings.json` are local-dev templates and are clearly fake
   (`mahjong/mahjong`, `YourStrong!Passw0rd`). If you find anything
   that looks production-shaped, rotate the credential immediately
   and replace it with an env-var override.
2. **No secrets in workflow inputs.** The `workflow_dispatch` inputs
   on `docker-build.yml` and `docker-smoke.yml` are tag / branch
   strings only. Don't add an `input:` of type `secret` — GitHub
   surfaces those as plaintext in the run UI.
3. **No secrets in `BUILD_SHA`.** The build SHA gets baked into the
   image label and published on `/health` + `/metrics`. Anything
   secret-shaped MUST NOT flow through it.
4. **Log redaction.** The structured logger (Phase J Wave 5) emits
   `State` and `Scopes` verbatim. If a future feature passes a token
   or password into a log scope, that token will appear in the JSON
   line. Use the `Microsoft.Extensions.Logging` `[LogProperty]`
   redaction attributes or scrub at the log-aggregator layer before
   indexing.
5. **Rotation.** Whenever the team migrates off SQLite, treat the
   first deploy as a one-time secret-rotation event: generate the DB
   password, populate the secret store, deploy, then verify nothing
   in the logs / image / git history references the new value.

## Cross-references

- `docs/deployment.md` — environment variables for the Docker deploy
- `docs/observability.md` — what `/metrics` exposes (no secrets)
- `docs/ci.md` — `GITHUB_TOKEN` is the only "secret" required for CI today
- `.gitignore` — `.env.local` + `docker-compose.override.yml` patterns
- `src/backend/src/Mahjong.Autotable.Api/Persistence/ServiceCollectionExtensions.cs`
  — the provider switch that consumes the connection-string env vars
