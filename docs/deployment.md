# Deployment — source-built Docker image and Compose

The root Dockerfile builds the Vite frontend and .NET10 API from source,
then packages both into one Linux runtime image. One .NET process serves
HTTP, `/autotable/`, `/autotable/ws`, `/api/*`, and SignalR. See
[docker.md](docker.md) for the short Bash/PowerShell quickstart.

## Build and load locally

Requires Docker with Buildx; running Compose also requires Compose v2+.
No host Node/.NET toolchain is needed. Use a builder capable of the selected
Linux architecture and allow enough disk for SDK/dependency layers.

```bash
./build.sh
./build.sh --tag mahjong-autotable:my-build --platform linux/amd64
```

```powershell
./build.ps1
./build.ps1 -Tag mahjong-autotable:my-build -Platform linux/amd64
```

Both scripts locate the repository from their own path, preserve the
caller's working directory, propagate Docker failures, and load the image
into the local engine. They never deploy or push. Set `BUILD_SHA` for public
build metadata; this label is not a replacement for actual image/DLL/asset
identity.

`--archive FILE` / `-Archive FILE` optionally runs `docker image save` after
a successful build; an existing file is never overwritten. On the Linux
server, `docker load --input FILE` imports it. Keep the Compose image tag
and platform aligned with the imported image.

### Dockerfile stages

| Stage | Base | Output |
| --- | --- | --- |
| frontend-build | node:20-alpine | Fresh Vite bundle at `/src/frontend/autotable/` |
| backend-build | mcr.microsoft.com/dotnet/sdk:10.0 | Release API at `/out/api/` |
| runtime | mcr.microsoft.com/dotnet/aspnet:10.0 | API `/app/`, bundle `/frontend/autotable/` |

The context excludes host dist/bin/obj/node_modules, test execution output,
`session-files/`, `playtest-artifacts/`, agent scratch, and local env files.
It never substitutes a host-published DLL, host bundle, or cached application
image for the source stages.

BuildKit transient memory mounts keep apt verification directories and
.NET build/IPC working files out of persistent image layers. This is not a
signature or sandbox bypass: apt uses its normal keyring and unprivileged
verifier. Backend build servers are disabled for reproducible publication.
Normal dependency/build caches remain optional accelerators; use
`--no-cache` / `-NoCache` to execute uncached stages.

## Configure and start

```bash
./scripts/compose-bootstrap.sh
docker compose up -d --no-build
```

Alternatively, `docker compose up -d --build` builds from the Dockerfile.
The bootstrap creates/preserves `JWT_SIGNING_KEY` in `.env`. A different
file is supported with `--env-file PATH`; pass that same file explicitly
to Compose. Do not regenerate the key on every deployment.

| Setting | Default | Scope |
| --- | --- | --- |
| MAHJONG_IMAGE | mahjong-autotable:local | Image tag shared by scripts/Compose |
| MAHJONG_PLATFORM | linux/amd64 | One Linux platform |
| MAHJONG_HOST_PORT | 8950 | Host publication, never changes container8080 |
| MAHJONG_BIND_ADDRESS | 127.0.0.1 | Loopback; use0.0.0.0 only for intended direct exposure |
| JWT_SIGNING_KEY | required | Runtime only; stable base64 signing material |
| BUILD_SHA | local via scripts/Compose | Public health/build label |

Builder scripts read exported environment variables/options, not `.env`.
For a custom tag, either export `MAHJONG_IMAGE` for both commands or pass
the tag to the script and set the matching Compose `.env` value.

Inside the image: `ASPNETCORE_ENVIRONMENT=Production`,
`ASPNETCORE_URLS=http://+:8080`, UID/GID1000:1000,
`Persistence__Provider=Sqlite`, and
`ConnectionStrings__Sqlite=Data Source=/data/mahjong-autotable.db`.
`HOME` and runtime work files live under `/data`. Do not change the internal
port merely to avoid a host-port conflict.

Secrets must not enter source control, image build args, archives or logs.
Store `.env` on a private, permission-preserving filesystem. Avoid printing
resolved Compose/full container environment configuration. Runtime Docker
environment is visible to Docker administrators; use a trusted host.

## Isolate projects and remap ports

Compose-generated names and project-scoped named volumes avoid fixed
container-name collisions. Use distinct project/env/port combinations:

```bash
./scripts/compose-bootstrap.sh --env-file ./.env.smoke.local
MAHJONG_HOST_PORT=8951 docker compose --env-file ./.env.smoke.local \
    -p mahjong-smoke up -d --no-build
```

An external working directory can use absolute paths:

```bash
MAHJONG_HOST_PORT=8951 docker compose -f /srv/mahjong/docker-compose.yml \
    --env-file /private/mahjong-smoke.env -p mahjong-smoke up -d --no-build
```

Keep `-p` and `--env-file` stable across lifecycle commands. Never reuse
another user's primary server port or data volume for a smoke run.

## Nginx HTTP and WebSockets

The default host-side upstream is `http://127.0.0.1:8950`, not container8080.
[The HTTP example](../infra/nginx/mahjong-http.conf.example) preserves
request paths and forwards HTTP/1.1 Upgrade/Connection headers for both
raw WebSockets and SignalR. Its single `location /` also covers REST/static
endpoints. Adapt it into an existing TLS server without changing that
server's certificates. See [reverse-proxy.md](reverse-proxy.md).

The app currently lacks general ASP.NET forwarded-header middleware.
The IP rate limiter has its own X-Forwarded-For handling; other consumers of
`Request.Scheme`/`RemoteIpAddress` do not thereby gain trusted proxy support.
The sample edge proxy overwrites client-provided XFF. HTTP/WS routing
qualification does not claim OAuth HTTPS callback/cookie behavior is
fully qualified behind TLS; that application-level follow-up needs its
own review, not an unrestricted trust-all proxy setting.

## Persistence and lifecycle

The project-scoped `mahjong-data` volume holds SQLite data; signing
configuration is the persistent env file, not a key baked into the image.
Using the same project/env preserves data and signed identities:

```bash
docker compose restart mahjong
docker compose up -d --no-build --force-recreate mahjong
docker compose down
docker compose up -d --no-build
```

`down` retains named volumes. **`down -v` intentionally destroys data.**
Changing the project name creates a different data volume; changing the
key invalidates previously signed credentials. Coordinate production
restarts with active players.

### Known public-room reconnect limitation

The source-build hosting qualification reproduced an application-level
gap after restart: existing runtime database rows and the signing key
remain intact, but a same-identity JOIN of the same public room receives
no prior board. The normal seat-reclaim command then creates another
runtime row. A fixed-seed identical hand is not evidence of resumption.
The public-room/runtime association needs a separately reviewed backend
persistence/recovery correction. Build scripts and volume configuration
cannot repair it; do not delete data, rotate keys, or claim seamless
game continuation as a workaround.

For SQLite backups, use the existing online `.backup` script
[`scripts/backup-sqlite.sh`](../scripts/backup-sqlite.sh) with a supported
SQLite tool and approved data access, or stop only the intended instance
before taking a complete database copy. Do not copy just a live `.db`
while WAL writers are active. See [restore-sqlite.sh](../scripts/restore-sqlite.sh).

## Provider overlays

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d --build
```

Both inherit the required JWT and app port settings. They use project-local
service DNS/data volumes and wait for database health. Provider strings are
`Postgres` / `SqlServer`; the PostgreSQL connection key is exactly
`ConnectionStrings__PostgreSql`.

Database host ports bind loopback and can be remapped with
`POSTGRES_HOST_PORT` / `MSSQL_HOST_PORT`; internal5432/1433 stay unchanged.
Set private database credentials instead of the documented development
defaults. Separate Compose projects also need distinct published database
ports. Provider engine tests/migrations are separate from the SQLite
single-image hosting smoke.

## Health and troubleshooting

```bash
curl --fail http://127.0.0.1:8950/health
docker compose ps
docker compose logs --tail 100 mahjong
```

`/health` exposes database connectivity, version/build metadata and uptime;
`/api/health` is the legacy short-form fallback. Docker HEALTHCHECK uses
in-image curl; tini propagates shutdown to the single .NET host.

| Symptom | Action |
| --- | --- |
| Host port occupied | Set `MAHJONG_HOST_PORT`; leave container8080 unchanged. |
| Missing JWT at Compose interpolation | Run bootstrap/use the same private env file; do not disable Production validation. |
| `/data` permission error | Prefer a fresh project-scoped named volume. Check filesystem ownership support and UID1000; do not alter unrelated volumes. |
| Apt reports invalid signatures | Preserve verification. Inspect actual verifier errors and build-storage permissions; never use trusted/unauthenticated flags. |
| BuildKit extraction/permission failure | Retain exact command/log, Docker version/storage backend and context/source identities. Do not prune or change a shared daemon to conceal the failure. |
| HTTP health works but Docker health/exec/start reports a missing `/sys/kernel/security/apparmor/profiles` | This can fail before the application command runs. Preserve the responding service and collect host/profile-interface evidence for the authorized host owner. Do not disable AppArmor, change the healthcheck to hide the fault, or restart unrelated containers. A successful image build/export does not prove that a new container can currently start. |
| Image absent after a build | Use the entrypoints' `--load` path. A builder-cache result alone is not a runnable image. |
| `/autotable/` missing | Confirm image includes `/frontend/autotable/index.html`; never paper over it with a host source mount. |

On storage that cannot represent normal Unix permissions, Docker itself
may still have limitations despite transient build mounts. Report that
environment limitation if reproduced; an old cached application repack
is not proof of this source-build path.

Build/hosting proof and image identity do not assert gameplay qualification,
100+ completed matches, all rules, canonical signed release or deployment
approval. Those outcomes have separate owned evidence.

## CORS and rate limiting

For a deliberately cross-origin frontend/API deployment, configure exact
allowed origins with `Cors__AllowedOrigins__0` and subsequent indices; do
not combine unrestricted origins with credentials. The normal single-image,
same-origin proxy layout does not require a separate frontend origin.
See [secrets.md](secrets.md) for operator configuration.

### Rate limiting

Production configuration enables the API rate limiter. A rejected request
returns HTTP429 and `Retry-After`; health/metrics and persistent WebSocket
transports have separate probe/transport treatment. Do not disable the
limiter merely to make a hosting smoke pass. Current quotas and endpoint
assignments remain in `RateLimitingExtensions` and appsettings.

The limiter has its own X-Forwarded-For handling. Keep the backend private
and have the trusted edge overwrite untrusted incoming forwarding headers;
this is not general forwarded-proto middleware. See
[reverse-proxy.md](reverse-proxy.md).

## Existing operational references

- [systemd.md](systemd.md): optional service-manager deployment. Its explicit
  `docker run` port must be aligned with the chosen host publication.
- [log-rotation.md](log-rotation.md): bounded Docker/container logs.
- [observability.md](observability.md): metrics and proxy access controls.
- [production-deployment-runbook.md](production-deployment-runbook.md):
  separately authorized release, rollout and rollback procedures.

These guides do not authorize a local build script to change a host daemon,
restart another deployment, install certificates or publish an image.
