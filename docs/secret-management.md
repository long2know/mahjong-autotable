# Secret management

Phase J Wave 8 (Apone, DevOps). End-to-end guidance for keeping
credentials out of the repo, the image, and the logs — from a
laptop dev loop through Kubernetes.

`docs/secrets.md` (Wave 5) audits what is and isn't a secret in
the codebase; **this** doc is the operational handbook for how to
inject and rotate the things that *are*.

## What we treat as a secret

| Key | Used by | Sensitivity |
|---|---|---|
| `ConnectionStrings:Postgres` (or `SqlServer`) | Backend, EF Core | Database password — high |
| `Sentry:Dsn` | Backend init | Sentry project DSN — medium (it's a public-ish key, but leak it and your quota suffers) |
| `Auth:Google:ClientSecret` | Bishop's auth surface | OAuth — high |
| `Auth:GitHub:ClientSecret` | Bishop's auth surface | OAuth — high |
| `Auth:JwtSigningKey` | Bishop's auth surface | Token signing — critical |
| `Auth:CookieEncryptionKey` | DataProtection ring | Cookie encryption — critical |
| Cloudflare API token | CI / kubectl scripts | Account-level — critical |
| ghcr.io image push token | Release workflow | Account-level — high |

What is NOT a secret:

- `BUILD_SHA` (just an image identifier)
- `ASPNETCORE_ENVIRONMENT` (operational flag)
- `Sentry:Environment`, `Sentry:SampleRate` (tuning knobs)
- `ConnectionStrings:Sqlite` when it's a file path against `/data`

## Three environments, three patterns

### 1. Local development

Use environment variables in a `.env` file that is `.gitignore`'d.
Two helpers:

- `appsettings.Development.example.json` — checked-in template with
    every secret key set to a placeholder. Copy to
    `appsettings.Development.json` and fill in real values *which
    Git is configured to ignore*.
- `scripts/generate-dev-secrets.sh` — generates a fresh `.env.dev`
    with strong random values for the DB password, JWT signing key,
    and cookie encryption key. Idempotent: it never overwrites an
    existing file.

```bash
./scripts/generate-dev-secrets.sh           # creates .env.dev
docker compose --env-file .env.dev up
```

`.env.dev` is in `.gitignore`. **Never commit it.**

The .NET configuration system reads env vars with double-underscore
syntax: `Auth__JwtSigningKey=…` maps to `Auth:JwtSigningKey`.

### 2. Staging (Kubernetes)

Plain `kind: Secret` objects, referenced by the `mahjong-autotable`
Deployment. The k8s overlay
(`infra/k8s/overlays/staging/secret-template.yaml`) ships an
`ExternalSecret` CRD wired to the External Secrets Operator (ESO),
but staging *also* works with a plain Secret if ESO isn't installed:

```bash
kubectl -n mahjong create secret generic mahjong-app-secrets \
    --from-literal=connectionstrings__postgres="Host=…;Username=…;Password=…" \
    --from-literal=auth__jwtsigningkey="$(openssl rand -base64 64)" \
    --from-literal=auth__cookieencryptionkey="$(openssl rand -base64 64)" \
    --from-literal=sentry__dsn=""
```

The Deployment's `envFrom: secretRef` lifts every key into the
container env. The `__`-separated key names are already in the
correct shape for the .NET config binder.

### 3. Production (External Secrets Operator + AWS Secrets Manager)

The canonical production pattern: a single AWS Secrets Manager
secret per environment, fetched by ESO into a k8s `Secret`, and
mounted via `envFrom`.

```
AWS Secrets Manager
    └── mahjong/prod/app
                ├── connectionstrings__postgres
                ├── auth__jwtsigningkey
                ├── auth__cookieencryptionkey
                ├── auth__google__clientsecret
                ├── auth__github__clientsecret
                └── sentry__dsn
                            │
                            ▼ (every 1h)
ExternalSecret  (infra/k8s/overlays/prod/secret-template.yaml)
                            │
                            ▼
Secret  mahjong-app-secrets
                            │
                            ▼
Deployment  envFrom.secretRef
```

Why ESO?

- Rotation: change the value in Secrets Manager; ESO refreshes the
    k8s Secret on its next sync (default 1h). The pod sees the new
    value on its next restart.
- Audit: AWS CloudTrail logs every `GetSecretValue` call. We don't
    have to roll our own.
- Separation of concerns: cluster operators can read the k8s
    `Secret` (RBAC-gated) but never the upstream AWS secret —
    rotation happens out-of-cluster.

Alternative backends ESO supports:

- HashiCorp Vault
- GCP Secret Manager
- Azure Key Vault
- 1Password Connect

The CRD shape is the same; the `SecretStore` configuration differs.

## Rotation runbook

| Secret | Rotation cadence | Procedure |
|---|---|---|
| Database password | 90 days | 1. Create new role/password in Postgres. 2. Update Secrets Manager. 3. Wait for ESO sync (or `kubectl rollout restart`). 4. Drop old role. |
| `Auth:JwtSigningKey` | 180 days | Tokens are JWTs with 1h lifetime — rotate, restart pods, accept ≤1h of 401s. To avoid downtime, ship a fallback-key list (Wave 9 work). |
| `Auth:CookieEncryptionKey` | 365 days | DataProtection ring; rotating it invalidates all auth cookies. Communicate the forced sign-out in advance. |
| OAuth client secrets | per provider policy | Generate new secret in Google/GitHub console, update Secrets Manager, restart pods. Old secret remains valid until you revoke it in the provider console — do that AFTER the new one is verified working. |
| Cloudflare API token | 365 days | Generate new token (Account → API Tokens), update CI / kubectl context, delete old. |
| ghcr.io PAT | per the operator's PAT expiry | Rotate in repo Settings → Secrets → Actions. |
| `Sentry:Dsn` | only on key compromise | Regenerate in Sentry → Settings → Client Keys, update Secrets Manager. |

## What never goes in the repo

- `.env*` files (gitignored)
- `appsettings.Production.json`, `appsettings.Staging.json`
    (gitignored)
- `*.pfx`, `*.pem` cert files (gitignored)
- `id_rsa`, `id_ed25519` SSH keys (gitignored — global config but
    repeating for clarity)
- `kubeconfig` (gitignored)
- Any file named `secrets.*` other than `docs/secrets.md` and
    `docs/secret-management.md`

`.gitignore` enforces these patterns; `pre-commit` runs `gitleaks`
on every commit (Wave 7 work). If you see a `gitleaks` failure on
a commit, do not bypass it — instead, rotate the leaked secret and
amend the commit to remove it.

## Logging discipline

The JSON console logger (Wave 5) deliberately does NOT log:

- request bodies (could carry auth cookies)
- the `Authorization` header
- the `Set-Cookie` response header
- query strings on `/api/auth/*` (could carry magic link tokens)

If you add a new endpoint that handles a credential, audit the
logging path. The `LogScrubber` middleware (Wave 7) catches the
common cases by header name, but custom-shaped payloads need
custom-shaped redaction.

## Sentry handling

Sentry's own SDK is configured to drop PII (`SendDefaultPii = false`)
and to redact known sensitive breadcrumb keys (`RedactBreadcrumb`
in `Observability/SentryConfiguration.cs`). The remaining risk is
**event metadata** — exception messages and stack traces. If your
code throws

```csharp
throw new ArgumentException($"Invalid token: {token}");
```

Sentry will receive the token. Never interpolate secrets into
exception messages. The Wave 8 `EnableLogs` flag is off by default
in part to limit how much app-controlled text reaches Sentry.

## See also

- `docs/secrets.md` — Wave 5 audit
- `docs/sentry.md` — Sentry-specific DSN handling
- `docs/cloudflare.md` — keeping the CF API token out of CI logs
- `infra/k8s/overlays/{staging,prod}/secret-template.yaml`
- `scripts/generate-dev-secrets.sh`
