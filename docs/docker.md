# Docker quickstart

5-minute TL;DR. For the full runbook (env vars, backups, troubleshooting,
production updates), see [`deployment.md`](deployment.md).

---

## Run it

The image runs in `Production` posture and needs a stable JWT signing key.
The one-time bootstrap writes one into a gitignored `.env` (idempotent — safe
to re-run; the key stays stable across restarts):

```bash
./scripts/compose-bootstrap.sh
docker compose up -d --build
```

Then open <http://localhost:8080/autotable/>.

That's it. The bootstrap provisions `JWT_SIGNING_KEY` (never committed); the
compose file builds the single image from `Dockerfile`, exposes Kestrel on
host port `8080`, and persists the SQLite database in a named volume
(`mahjong-data`) so games survive container restarts. Skipping the bootstrap
makes `docker compose up` fail fast with a one-line fix instruction rather
than crash-looping.

## Stop it

```bash
docker compose down              # keeps the volume
docker compose down -v           # wipes the volume too
```

## Health

```bash
curl http://localhost:8080/health
# {"status":"healthy","buildSha":"local","uptime":"00:00:42.1234567","version":"1.0.0.0"}
```

## Logs

```bash
docker compose logs -f
```

## What just happened?

Three stages were built into one image:

1. **`node:20-alpine`** built the Vite bundle from
   `src/frontend/autotable-src/` into `/src/frontend/autotable/`.
2. **`mcr.microsoft.com/dotnet/sdk:10.0`** published the .NET 10 API from
   `src/backend/src/Mahjong.Autotable.Api/` into `/out/api/`.
3. **`mcr.microsoft.com/dotnet/aspnet:10.0`** combined them and exposed
   the bundle at `/autotable/` and the API at `/api/*`, `/health`,
   `/hubs/changsha`, and `/autotable/ws`.

The final image is ~300 MB. Initial build takes <1 minute on a warm cache.

## Bare `docker run` instead of compose

`docker run` does not read `.env`, so pass the required JWT key explicitly.
Generate a stable one and keep it (e.g. in your secrets manager) — reusing it
across restarts keeps previously issued JWTs valid:

```bash
docker build -t mahjong-autotable:local .
docker run -d --name mahjong -p 8080:8080 -v mahjong-data:/data \
    -e Authentication__JwtSigningKeys__0="$(openssl rand -base64 48)" \
    mahjong-autotable:local
```

## Production deploy

See [`deployment.md`](deployment.md) for env vars, image tagging,
backup/restore, healthcheck details, and troubleshooting. For the
end-to-end production runbook (pre-flight, rolling update, rollback,
incident response) see
[`production-deployment-runbook.md`](production-deployment-runbook.md).

## Multi-arch images (Wave 10)

The CI workflow [`docker-build.yml`](../.github/workflows/docker-build.yml)
publishes a manifest list covering **both** `linux/amd64` and
`linux/arm64`. `docker pull ghcr.io/long2know/mahjong-autotable:latest`
on an arm64 host (Raspberry Pi, AWS Graviton, Apple Silicon Linux VM)
gets the arm64 variant transparently; an amd64 host gets the amd64
variant. To reproduce the multi-arch build locally:

```bash
docker run --privileged --rm tonistiigi/binfmt --install arm64
docker buildx create --name multiarch --use --bootstrap
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t mahjong-autotable:wave10 .
```
