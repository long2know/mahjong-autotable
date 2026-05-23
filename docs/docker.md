# Docker quickstart

5-minute TL;DR. For the full runbook (env vars, backups, troubleshooting,
production updates), see [`deployment.md`](deployment.md).

---

## Run it

```bash
docker compose up -d --build
```

Then open <http://localhost:8080/autotable/>.

That's it. The compose file builds the single image from `Dockerfile`,
exposes Kestrel on host port `8080`, and persists the SQLite database in a
named volume (`mahjong-data`) so games survive container restarts.

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

1. **`node:20-alpine`** built the Parcel bundle from
   `src/frontend/autotable-src/` into `/out/autotable/`.
2. **`mcr.microsoft.com/dotnet/sdk:10.0`** published the .NET 10 API from
   `src/backend/src/Mahjong.Autotable.Api/` into `/out/api/`.
3. **`mcr.microsoft.com/dotnet/aspnet:10.0`** combined them and exposed
   the bundle at `/autotable/` and the API at `/api/*`, `/health`,
   `/hubs/changsha`, and `/autotable/ws`.

The final image is ~300 MB. Initial build takes <1 minute on a warm cache.

## Bare `docker run` instead of compose

```bash
docker build -t mahjong-autotable:local .
docker run -d --name mahjong -p 8080:8080 -v mahjong-data:/data mahjong-autotable:local
```

## Production deploy

See [`deployment.md`](deployment.md) for env vars, image tagging,
backup/restore, healthcheck details, and troubleshooting.
