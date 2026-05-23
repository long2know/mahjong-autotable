# Phase J Wave 3 — Apone (DevOps): single-image Docker deployment

**By:** Apone (DevOps / Platform Engineer), 2026-05-22

**Branch:** `stlong/phase-j-wave-3-completion`
**Primary commit:** `ea2c991` — `chore(devops): Phase J Wave 3 — single-image Docker deployment`

---

## Task 1 — Multi-stage Dockerfile ✅

Ships at the repo root as `/Dockerfile`. Three stages:

1. **`frontend-build` (`node:20-alpine`)** — `npm ci --no-audit --no-fund` →
   `npx parcel build index.html --dist-dir /out/autotable --public-url . --no-source-maps --no-cache`.
   Honours the Phase G Hicks build invariant (decisions.md L1864) re. `--public-url .`.
2. **`backend-build` (`mcr.microsoft.com/dotnet/sdk:10.0`)** — restores and
   publishes `src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj`
   directly (not the slnx — tests are not needed in the image) into `/out/api`.
   Passes `/p:UseAppHost=false` to skip the platform-specific apphost binary.
3. **`runtime` (`mcr.microsoft.com/dotnet/aspnet:10.0`)** —
   - `apt-get install curl tini` (curl for HEALTHCHECK; tini as PID 1 for clean signal handling).
   - Copies the API to `/app` and the bundle to **`/frontend/autotable/`** (not `wwwroot/autotable`).
   - `mkdir /data && chmod 777 /data` + `VOLUME ["/data"]`.
   - `ENV ConnectionStrings__Sqlite="Data Source=/data/mahjong-autotable.db"` + `Persistence__Provider=Sqlite`.
   - `ENV ASPNETCORE_URLS=http://+:8080` + `EXPOSE 8080`.
   - `HEALTHCHECK` curls `/health` first, falls back to `/api/health`.
   - `ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "Mahjong.Autotable.Api.dll"]`.

### Why the bundle lives at `/frontend/autotable/` (not `wwwroot/autotable/`)

Program.cs L65 computes the autotable static-files path as:

```csharp
Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../frontend/autotable"))
```

With `WORKDIR=/app` the path collapses to `/frontend/autotable`. Putting the
bundle exactly there means:

- **No backend code change required** for Docker (the task forbade touching
  `src/backend/**` anyway).
- The custom `.glb` / `.gltf` MIME-type registrations in Program.cs L70–71 are
  exercised on every request — verified via
  `curl -I /autotable/models.auto.72ee60ea.glb` → `Content-Type: model/gltf-binary`.
- The check on L66 (`if (Directory.Exists(autotablePath))`) finds the
  bundle, so the seat-aware `RequestPath="/autotable"` binding is wired.

### Connection-string env-var name

The task example used `ConnectionStrings__DefaultConnection`, but the actual key
this codebase reads is `Sqlite` (Persistence/ServiceCollectionExtensions.cs L30:
`configuration.GetConnectionString("Sqlite")`). The Dockerfile sets
`ConnectionStrings__Sqlite="Data Source=/data/mahjong-autotable.db"` accordingly.

### Healthcheck — defence-in-depth

Bishop's `/health` (Phase J Wave 3 Task 3, commit `9235859`) landed before I
finalised the Dockerfile, so the HEALTHCHECK got wired straight to it. I kept
the fallback to `/api/health` as belt-and-braces (handles rollbacks to images
predating Bishop's commit, and matches the dual-probe pattern teams expect):

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health \
      || curl -fsS http://127.0.0.1:8080/api/health \
      || exit 1
```

---

## Task 2 — docker-compose.yml ✅

`/docker-compose.yml` at the repo root. Builds the image as
`mahjong-autotable:local`, exposes host port 8080, mounts SQLite on the
`mahjong-data` named volume, passes through `BUILD_SHA` from the host env
(`${BUILD_SHA:-local}`). `restart: unless-stopped` so a host reboot brings the
table back up.

The pre-existing root `docker-compose.yml` (which referenced the deprecated
`infra/docker/Dockerfile` and bind-mounted `./data:/app/data`) was replaced
with the new canonical compose file.

---

## Task 3 — .dockerignore ✅

Aggressively trims context. Highlights:

- `**/node_modules` (the autotable-src tree alone is 2.2 GB).
- `**/.parcel-cache`, `**/dist`, `**/bin`, `**/obj`.
- `src/frontend/autotable/` — the committed pre-build is excluded because
  Stage 1 produces a fresh one.
- `src/backend/tests/` — runtime image doesn't need tests.
- `.git/`, `.vscode/`, `.idea/`, `.squad/`, `.copilot/`.
- Local DB files: `*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `*.sqlite3`.
- `data/` — never bake host DBs in.
- `docker-compose.override.yml`, `.env`, `.env.*` (with `.env.example` allow-listed).
- Docs (`docs/`, `**/README.md`) — not needed at runtime.

Context size dropped from ~2.5 GB (with node_modules) to a few MB.

---

## Task 4 — Documentation ✅

### `docs/deployment.md` — full runbook (11 KB, 11 sections)

Prerequisites · Image layout (stage table + filesystem map) · Build (with SHA
stamping) · Run (`docker run` and `compose`) · Env-var reference table (with
PostgreSQL / SQL Server failover example) · Persistence (backup/restore via
sidecar `alpine` containers + wipe) · Healthcheck (probe URLs, `docker
inspect` snippet) · Day-2 ops (logs, exec, sqlite3 sidecar, restart) ·
Updating (compose / bare docker; mid-hand rehydration safety) ·
Troubleshooting matrix (port conflicts, volume permissions, 404s,
manifest-unknown for `dotnet/sdk:10.0` tag rotation) · Quick-command
reference.

### `docs/docker.md` — 5-minute quickstart

`docker compose up -d --build` → `http://localhost:8080/autotable/`. Links
into `deployment.md` for production.

### `README.md` — replaced stale Docker section

The pre-existing `## Docker (single image)` section still referenced the
deleted `modern/` Vite frontend (with a non-existent `runtime-modern`
target) and the deprecated `infra/docker/Dockerfile`. I replaced it with a
new `## Deploy via Docker` section pointing at `docs/docker.md` +
`docs/deployment.md`. (Stale documentation directly adjacent to my new
content; cleanup is in-scope per the DevOps lane.)

---

## Container-build verification

Docker **is** available on the runner (`Docker version 29.5.2`, `compose v5.1.4`).

```bash
docker build -t mahjong-autotable:test .
docker run -d --name mahjong-test -p 18080:8080 -e BUILD_SHA=apone-verify mahjong-autotable:test
```

Observed:

| Metric            | Value                                              |
| ----------------- | -------------------------------------------------- |
| Cold build time   | **47.3 s**                                         |
| Warm rebuild time | **16.2 s**                                         |
| Final image size  | **299 MB**                                         |
| `/health`         | HTTP 200 `{"status":"healthy","buildSha":"apone-verify","uptime":"…","version":"1.0.0.0"}` |
| `/api/health`     | HTTP 200 `{"status":"ok","service":"mahjong-autotable-api"}` |
| `/autotable/`     | HTTP 200 (index.html, 28.66 kB)                    |
| `/autotable/models.auto.72ee60ea.glb` | HTTP 200, `Content-Type: model/gltf-binary` (custom MIME mapping confirmed) |
| `HEALTHCHECK` status | `healthy` immediately at startup AND after the first 30 s probe cycle |
| Boot logs         | EF Core schema init OK, `Hydrated 0 Changsha game(s) from persistence.`, `Now listening on: http://[::]:8080`. No errors or warnings other than the expected `Overriding HTTP_PORTS '8080' …` notice. |

Bundle contents inside the image (22 files):

```
index.html, about.html, autotable-src.afd1b718.js (1.07 MB),
autotable-src.df85b4c4.css + autotable-src.f3ef64cd.css,
models.auto.72ee60ea.glb, table.60230825.jpg, game.332493fc.mp4,
discard.c3151c81.wav, stick.207ef49b.wav,
Segment7Standard.f1d05002.otf, hand.03a3a7cc.svg,
icon-{16,32,96}.auto.*.png, dealer/dice/pay/round/unseat/winds/tiles-labels.*.png
```

---

## Build + run commands for Vasquez's smoke test

**Build:**

```bash
docker build -t mahjong-autotable:smoke .
```

**Run (mapped to host port 18080 to avoid colliding with anything Vasquez
might already have on 8080):**

```bash
docker run -d \
    --name mahjong-smoke \
    -p 18080:8080 \
    -e BUILD_SHA=smoke \
    mahjong-autotable:smoke

# Give Kestrel a beat to bind + EF Core to bootstrap the schema.
sleep 10
```

**Assertions (every one verified by me locally):**

```bash
# 1. /health returns 200 with the expected JSON shape
curl -fsS http://localhost:18080/health \
    | jq -e '.status == "healthy" and .buildSha == "smoke" and .version != ""'

# 2. /api/health returns 200 (legacy probe)
curl -fsS http://localhost:18080/api/health \
    | jq -e '.status == "ok" and .service == "mahjong-autotable-api"'

# 3. /autotable/ returns 200 (lobby HTML)
test "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/autotable/)" = "200"

# 4. .glb MIME type is registered (would default to application/octet-stream otherwise)
curl -fsS -I http://localhost:18080/autotable/models.auto.72ee60ea.glb \
    | grep -i '^content-type: model/gltf-binary'

# 5. Docker HEALTHCHECK status converges to "healthy" within one probe cycle (35 s)
sleep 35
test "$(docker inspect --format='{{.State.Health.Status}}' mahjong-smoke)" = "healthy"
```

**Teardown:**

```bash
docker stop mahjong-smoke && docker rm mahjong-smoke
docker rmi mahjong-autotable:smoke
```

A bind-mounted SQLite test (POST to `/api/tables` or open a Changsha hub
connection, then restart the container, then assert the game survives) is the
natural Wave 4 extension but is out of scope for the build-time smoke gate.

---

## Cross-lane coordination

- **Bishop's `/health` endpoint** — landed in commit `9235859` before I
  finalised the HEALTHCHECK; no blocker. Wire shape is
  `{ status, buildSha, uptime, version }` — Dockerfile sets `BUILD_SHA`
  via env so deploys are identifiable. **No outstanding blocker on
  Bishop's side.**
- **Hicks's frontend bundle** — Stage 1's
  `COPY src/frontend/autotable-src/ ./` picks up everything in the source
  tree, so the new `sounds/` directory + `src/sound.ts` / `src/replay.ts`
  modules will be bundled automatically once Hicks commits them. Verified
  Stage 1 emits 22 bundle artifacts including `discard.c3151c81.wav` and
  `stick.207ef49b.wav` (the existing sound assets referenced from
  `index.html`).
- **Vasquez** — exact build/run commands and assertions delivered above.
  Suggest she adds the script to `tests/smoke/` and wires it into CI on
  PRs that touch `Dockerfile`, `.dockerignore`, `docker-compose.yml`, or
  `src/frontend/autotable-src/package*.json`. Smoke is build-then-probe;
  unit/integration coverage stays in `src/backend/tests/`.

---

## Risks / follow-ups

1. **Deprecated `infra/docker/Dockerfile`** still exists. It references a
   non-existent `runtime-modern` target and the deleted `modern/` Vite
   frontend. I deliberately did not delete it (out of strict scope) but
   recommend a Wave 4 housekeeping commit to remove `infra/docker/`
   entirely now that the canonical Dockerfile lives at the repo root.
2. **`Program.cs` Line 16 still calls** `Directory.CreateDirectory(Path.Combine(ContentRootPath, "data"))`.
   In the container that creates an empty `/app/data` directory next to
   the SQLite file at `/data/mahjong-autotable.db`. Harmless but odd —
   either Bishop deletes the line (now that connection string is
   absolute) or it stays for the dev-mode `dotnet run` path. Flagged for
   Bishop, no action on my side.
3. **`docker-compose.override.yml`** is gitignored so contributors can
   stash host-specific tweaks (e.g. bind-mount a host folder instead of
   the named volume for debugging). Recommended pattern documented in
   `deployment.md` §4.
4. **`.NET` daily build tag stability** — `mcr.microsoft.com/dotnet/sdk:10.0`
   is a floating tag during the .NET 10 preview window. If GA changes the
   tag layout, a one-line pin update (e.g. `10.0-bookworm-slim`) is
   needed.
5. **Image size 299 MB** — could be trimmed to ~150 MB with a chiselled
   base (`mcr.microsoft.com/dotnet/nightly/aspnet:10.0-noble-chiseled`)
   but those bases lack apt + curl, so the HEALTHCHECK has to be rewired
   to `dotnet-healthcheck` style. Defer until Stephen asks for it.

---

## Reproduction one-liner (for the Squad chat / Stephen's notes)

```bash
git clone <repo> && cd mahjong-autotable && docker compose up -d --build \
  && curl -fsS http://localhost:8080/health && open http://localhost:8080/autotable/
```
