# Smoke tests

End-to-end verification scripts that exercise deployment-shaped surfaces
the unit-test suite intentionally cannot reach (multi-stage container
builds, real network sockets, container-lifecycle behaviour). They are
deliberately **outside** the `dotnet test` gate so the inner loop stays
fast — these scripts are slow (multi-stage Docker builds dominate).

| Script | Purpose | Approx runtime |
| --- | --- | --- |
| `docker-build-smoke.sh` | Builds Apone's multi-stage Dockerfile, runs the image, asserts `/health` returns the expected 4-field JSON shape, then tears everything down. | ~2–5 min (first build can be longer with cold base images) |

## docker-build-smoke.sh

End-to-end verification of the Phase J Wave 3 single-image deployment.

### What it does

1. Builds the image. Apone's `.dockerignore` declares the canonical
   Dockerfile lives at the **repo root**, so the script invokes a plain
   `docker build .` when `./Dockerfile` is present. It falls back to
   `infra/docker/Dockerfile` (with `--target runtime-autotable`) only
   when the root file is absent, so the script survives both the legacy
   `infra/docker/` layout and the current repo-root layout.
2. Starts the container detached, binding host port `18080` → container
   `8080`.
3. Polls `http://localhost:18080/health` for up to 30 s.
4. Asserts the JSON body contains all four expected fields:
   `status`, `buildSha`, `uptime`, `version`.
5. Always tears down the container, image, and per-run log directory
   on exit (success **or** failure) via a `trap`.

The container name and image tag both include `$$` (the shell PID) so
parallel runs do not collide. A per-run directory under
`tests/smoke/.run-<pid>/` holds the build log and the captured health
JSON; it is purged on cleanup.

### Prerequisites

- Docker daemon installed and runnable by the current user
  (`docker info` should succeed without `sudo`).
- Host port `18080` available. Edit the `PORT=` line in the script if
  that conflicts with anything else local.
- `curl` available on the host.

### How to run

```bash
tests/smoke/docker-build-smoke.sh
```

Exits non-zero on any failure step (build, container start, health
probe timeout, shape assertion). The trap-driven teardown means a
half-built image or a stuck container will not leak between runs.

### Expected output (success)

```
==> [1/4] Building mahjong-autotable:smoke-12345 ...
✅ build succeeded
==> [2/4] Starting container...
==> [3/4] Waiting for /health...
✅ /health responding after 3s
{"status":"ok","buildSha":"dev","uptime":"00:00:03.1","version":"..."}
==> [4/4] Assert response shape...
  ✅ status field present
  ✅ buildSha field present
  ✅ uptime field present
  ✅ version field present

🎯 Docker smoke test PASSED
```

### CI integration

**Not yet wired.** This is currently a manual / nightly check. A future
wave can add a GitHub Actions job that runs this script on the
`ubuntu-latest` runner (which ships a Docker daemon) — likely gated to
`push` on `main` and `workflow_dispatch` only, given the multi-minute
cost is poorly suited to per-PR runs.

### Troubleshooting

- **Build fails with "no Dockerfile found"** — Apone's canonical
  Dockerfile lives at the **repo root**. Confirm the file is present
  there (or as a fallback at `infra/docker/Dockerfile`) before running
  the script.
- **`/health` never responds** — inspect `docker logs <container>`; the
  script tails the last 50 lines on timeout. The most common cause is
  a startup exception in `Program.cs` (DB bootstrap, persistence
  hydration); the container fails fast and `docker ps` will not list
  it.
- **Port 18080 already in use** — change `PORT=` near the top of the
  script.
- **Shape assertion fails** — the `/health` endpoint is contract-pinned
  in `HealthEndpointTests.cs` in the unit-test suite; if that file is
  green but the smoke script fails on shape, the production endpoint
  is regressing on serialisation casing or field selection.
