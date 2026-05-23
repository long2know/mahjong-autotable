# CI — Docker build + publish + nightly smoke

This document covers the two GitHub Actions workflows that live under
`.github/workflows/` and own the Docker image lifecycle for
`mahjong-autotable`. Both workflows were introduced in **Phase J Wave 4**
(Apone, DevOps).

## At a glance

| Workflow | File | Trigger | Job | Output |
| --- | --- | --- | --- | --- |
| `docker-build` | `.github/workflows/docker-build.yml` | push to `main`, push of `v*.*.*` tags, `workflow_dispatch` | Build + push multi-stage image | Tags pushed to `ghcr.io/long2know/mahjong-autotable` |
| `docker-smoke` | `.github/workflows/docker-smoke.yml` | nightly cron (`0 8 * * *` UTC), `workflow_dispatch` | Build + run + `/health` smoke | Build/run logs as failure artifact |

Both workflows run on `ubuntu-latest` and use the auto-provisioned
`GITHUB_TOKEN` for any GitHub-side authentication (no PAT, no
manually-managed secrets — see [Required secrets](#required-secrets)
below).

## `docker-build` — build + push on every main commit

### What it does

1. Checks out the repository.
2. Sets up Docker Buildx (multi-platform + cache backend).
3. Logs in to `ghcr.io` with the workflow's `GITHUB_TOKEN`.
4. Computes a tag set with `docker/metadata-action`.
5. Builds the multi-stage `Dockerfile` at the repo root with
   `BUILD_SHA=${{ github.sha }}` passed as a build-arg so the runtime
   `/health` endpoint returns the actual commit SHA in published images
   (not `"dev"`).
6. Pushes every tag in the computed set to ghcr.io.
7. Uses GitHub Actions cache (`type=gha`) for layer reuse across runs.

### Tag scheme

For a build of commit `abcdef0123` on branch `main`:

- `ghcr.io/long2know/mahjong-autotable:latest`
  — moves with every successful `main` build (and any tag push).
- `ghcr.io/long2know/mahjong-autotable:sha-abcdef0123…`
  — immutable, one-for-one with the commit. Use this when pinning a
  rollback target.
- `ghcr.io/long2know/mahjong-autotable:v1.2.3`
  — only emitted when the build was triggered by a `v*.*.*` git tag.

The `sha-…` tag is emitted on every triggering event (push to `main`,
tag push, or `workflow_dispatch` from any branch). The `latest` tag is
gated to `main` so an ad-hoc dispatch from a feature branch does not
clobber the production rolling tag.

### How to trigger manually

Repo → **Actions** → **docker-build** → **Run workflow** → pick the
branch → **Run workflow**.

The workflow will build whatever's on the chosen ref and push the
`sha-<commit>` tag. The `latest` tag only moves when the chosen ref is
`main`.

CLI equivalent (requires `gh` and a GHCR-scoped PAT or the standard
`repo` scope):

```bash
gh workflow run docker-build.yml --ref main
```

### Required secrets

None. `GITHUB_TOKEN` is provisioned by GitHub Actions on every run and
has the `packages: write` permission requested in the workflow's
`permissions:` block. This is the same-repo ghcr push path — no PAT,
no `GHCR_TOKEN`, no `DOCKERHUB_USERNAME`.

The workflow's `permissions:` block:

```yaml
permissions:
  contents: read
  packages: write
```

### Pulling published images

```bash
# Latest main build:
docker pull ghcr.io/long2know/mahjong-autotable:latest

# Specific commit (immutable):
docker pull ghcr.io/long2know/mahjong-autotable:sha-<full-40-char-sha>

# Specific release tag:
docker pull ghcr.io/long2know/mahjong-autotable:v1.2.3
```

To **find a specific SHA-tagged build**:

```bash
# Show every tag currently in the registry (newest first):
gh api -H "Accept: application/vnd.github+json" \
  /users/long2know/packages/container/mahjong-autotable/versions \
  --jq '.[] | {id, tags: .metadata.container.tags, updated_at}'

# Filter to just the SHA tags for a given commit prefix:
gh api -H "Accept: application/vnd.github+json" \
  /users/long2know/packages/container/mahjong-autotable/versions \
  --jq '.[] | .metadata.container.tags[]' | grep '^sha-abcdef'
```

The same view is available in the UI at
<https://github.com/long2know?tab=packages> →
**mahjong-autotable**.

### Verifying the baked-in SHA

Pull, run, and curl `/health` — the `buildSha` field is the commit
that produced the image:

```bash
docker pull ghcr.io/long2know/mahjong-autotable:latest
docker run -d --name mj -p 8080:8080 ghcr.io/long2know/mahjong-autotable:latest
curl -s http://localhost:8080/health
# → {"status":"healthy","buildSha":"abcdef0123…","uptime":"00:00:01.234","version":"…"}
docker stop mj && docker rm mj
```

If `buildSha` is `"dev"`, the image was built locally without
`--build-arg BUILD_SHA=…`, or `BUILD_SHA` was passed as an empty
string. Production images published by `docker-build` always carry the
actual commit SHA.

### Making the package public (one-time setup)

By default, packages pushed to ghcr.io from a user account inherit
**private** visibility, so the workflow run will succeed but
`docker pull` from outside the org will fail with `manifest unknown`.

To make the image world-pullable:

1. Go to <https://github.com/long2know?tab=packages>.
2. Click **mahjong-autotable**.
3. **Package settings** (right sidebar) → **Danger Zone** → **Change
   package visibility** → **Public** → type the package name to
   confirm.

This is a one-time step per package; subsequent pushes inherit the
existing visibility. Re-pushing `latest` does not flip the package
back to private.

(Alternative path through the org/user settings: **Settings → Packages
→ mahjong-autotable → Change visibility**.)

## `docker-smoke` — nightly end-to-end smoke

### What it does

1. Checks out the repository.
2. Sets up Docker Buildx.
3. Runs `tests/smoke/docker-build-smoke.sh` (Vasquez's Phase J Wave 3
   smoke script) — the script builds the image from scratch, starts a
   container, polls `/health` for up to 30 seconds, and asserts the
   four-field response shape (`status`, `buildSha`, `uptime`,
   `version`).
4. On failure, collects `smoke.log`, any `tests/smoke/.run-*` directory
   the script left behind, plus `docker ps -a` and `docker images`
   snapshots, and uploads them as a workflow artifact
   (`docker-smoke-failure-<run-id>`, 14-day retention).

### Schedule

`cron: '0 8 * * *'` — 08:00 UTC daily, which is roughly 03:00 CST
(UTC-5) / 04:00 CDT. Early enough that a broken nightly is visible on
the morning dashboard, late enough that same-day pushes have settled.

If a different timezone or cadence is preferred, edit the `schedule:`
block. Standard cron caveats apply — GitHub Actions schedules can
drift by ~10 minutes during peak load, and the workflow does not run
on forks.

### How to trigger manually

Repo → **Actions** → **docker-smoke** → **Run workflow**.

CLI:

```bash
gh workflow run docker-smoke.yml
```

A manual run is the recommended sanity check after any change to:

- `Dockerfile`
- `.dockerignore`
- `docker-compose.yml`
- `tests/smoke/docker-build-smoke.sh`
- `src/frontend/autotable-src/package*.json`
- `src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj`

### Failure surface

The workflow chose **artifact upload over auto-filed GitHub Issues**
intentionally:

- A flaky nightly should not spam the issue tracker.
- The red Actions run on `main` is already visible on the repo
  dashboard.
- The uploaded artifact (`smoke-logs/`) contains everything needed to
  triage: combined stdout/stderr from the script, the per-PID
  `.run-<pid>/docker-build.log` + `health.json` (when present), plus
  `docker ps -a` / `docker images` state.

To download the artifact from a failed run:

```bash
gh run download <run-id> -n docker-smoke-failure-<run-id>
```

Or via the UI: failed run → **Artifacts** at the bottom of the summary
page → **docker-smoke-failure-…** → download.

### Required secrets

None. The smoke is read-only against GitHub APIs and does not push
the image it builds — it only verifies that the image **can** be
built and is healthy at runtime.

## Local verification before opening a PR

The CI workflows are not the only smoke surface. Before merging any
Dockerfile / frontend / backend change that could affect the
container, run Vasquez's script locally:

```bash
tests/smoke/docker-build-smoke.sh
```

And the BUILD_SHA arg-to-env chain:

```bash
docker build --build-arg BUILD_SHA=test123 -t mj-test:local .
docker run -d --name mj-verify -p 18080:8080 mj-test:local
sleep 5
curl http://localhost:18080/health | grep '"buildSha":"test123"'
docker stop mj-verify && docker rm mj-verify && docker rmi mj-test:local
```

Both verifications are run by Apone as part of every wave that touches
the `Dockerfile` or the publish workflow.

## Pre-session `squad-*.yml` workflows

The repository contains several other workflow files under
`.github/workflows/squad-*.yml` (squad-ci, squad-docs, squad-release,
squad-promote, etc.). These are pre-session orchestration artifacts
managed outside the per-wave Squad coordination and are **not**
covered by this document. The two workflows above (`docker-build` and
`docker-smoke`) are the only ones owned by Apone's lane.
