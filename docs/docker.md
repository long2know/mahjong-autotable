# Docker quickstart

Build and run the backend plus Vite frontend as one Linux image. Docker
Engine/Desktop in Linux-container mode, the Buildx plugin, and Compose v2+
are required. No host Node or .NET SDK is needed.

## Build once

From Bash:

```bash
./build.sh
```

From PowerShell:

```powershell
./build.ps1
```

Both commands build the root Dockerfile from source and use `--load`, so the
result is available locally as `mahjong-autotable:local`, not just in a
builder cache. Neither command deploys, pushes, creates an env file, or
requires a JWT key. Absolute script paths also work from another directory,
including paths with spaces.

| Option | Bash | PowerShell |
| --- | --- | --- |
| Image tag | `--tag mahjong-autotable:my-build` | `-Tag mahjong-autotable:my-build` |
| Linux platform | `--platform linux/arm64` | `-Platform linux/arm64` |
| Transfer archive | `--archive "image archive.tar"` | `-Archive "image archive.tar"` |
| Uncached build | `--no-cache` | `-NoCache` |
| Help | `--help` | `-Help` |

Defaults are `MAHJONG_IMAGE=mahjong-autotable:local` and
`MAHJONG_PLATFORM=linux/amd64`. Export those variables for both the builder
and Compose, or pass matching script options and `.env` values. Build
scripts intentionally do not read secret-bearing `.env` files.
Each script prints a public build identity and passes it to both the frontend
bundle and the server. With `BUILD_SHA` unset, the format is
`<full-git-commit>[-dirty]-<UTC-yyyyMMddTHHmmssZ>`; tracked or untracked changes
add `-dirty`. Without Git or an available commit it is
`local-source-unavailable-<UTC-yyyyMMddTHHmmssZ>`. This identifies the build
attempt; it is **not** a source-tree hash or a reproducibility guarantee.
An explicit `BUILD_SHA` override is passed through unchanged. Do not put
secrets in this public value.

The lobby footer has a **Build / Version** label, including when you reopen
the lobby from a table. Its short UI ID comes from the JavaScript actually
loaded. Expand it to see the full loaded-UI ID, actual server ID and server
assembly version (from same-origin `/health?simple=1`, without DB/OAuth details).
A mismatch displays a warning and a manual **Reload page** button; nothing
automatically reloads or interrupts play. Offline/invalid/timed-out responses
show **Retry**, not a guessed server version. Development/unidentified builds
are explicitly not compared.

Direct `npm run build` or `docker build` without `BUILD_SHA` leaves the UI
identified as development/unidentified. Use the scripts for automatically
stamped local images, or supply the same public `BUILD_SHA` to your build and
runtime when building outside Docker.

The selected Linux platform must be supported by the Docker builder.
Multi-platform publication/signing is a separate release operation, not
performed by these local build scripts.

### `latest` is a tag, not a deployment

To label a new local image `latest` explicitly:

```bash
./build.sh --tag mahjong-autotable:latest
# PowerShell: ./build.ps1 -Tag mahjong-autotable:latest
```

Retagging, building or loading an image does not replace an already-running
container, and `docker compose restart` still uses that container's old image.
On the target host, after loading the new image, use the matching tag and
recreate the service with the **existing** project, data volume and signing key:

```bash
MAHJONG_IMAGE=mahjong-autotable:latest docker compose up -d --no-build --pull never --force-recreate mahjong
```

Check the lobby's full UI/server IDs against the identity printed by the build;
an unchanged assembly version alone cannot distinguish revisions. Reloading a
page cannot update an old server deployment. These build scripts never deploy
to a remote host.

## Published main builds and GitHub releases

Every successful `docker-build` image publication from upstream `main`
(push or manual run on `main`) creates a GitHub **prerelease record** tagged
`build-<workflow-run-id>-<build-job-attempt>`. It is not a product-version bump
and is never promoted to GitHub's latest stable release. Failed builds, PR
validation, non-main manual builds and version-tag builds do not create these
records. Build-prefixed tags do not match the image/version-release triggers.

Each record links the exact source/merge commit and originating workflow
attempt, and records the manifest-list digest, published image tags and
`linux/amd64` + `linux/arm64` platforms. Its `docker pull ...@sha256:...` command
identifies the actual image from that build; `latest` **and** `sha-<commit>`
tags can move when images are rebuilt. The UI and `/health` `BUILD_SHA` identify
the source commit, not a particular rebuilt digest.

PR notes are generated between the preceding published build record in the
commit's ancestry and this exact commit, covering batches of merged PRs.
Out-of-order/newer or divergent builds are not used as a baseline. With no
prior record, only merged main PRs associated with the source commit are
listed, explicitly not the entire historical PR inventory. A rebuild of the
same source reports no newly included PRs.

Rerunning only a failed release-record job preserves the original build
attempt and digest. An existing matching release is verified, not edited;
a conflicting tag, commit or image identity fails rather than being
overwritten. Rerunning the build itself creates a new attempt-tagged record,
even for the same source commit. API/permission failures fail the job visibly;
fix permissions/tag rules and rerun the failed job rather than deleting or
moving previous records. A release-record failure makes `docker-build`
unsuccessful, so its existing downstream signer waits for a successful rerun.
The helper defaults to a non-publishing API dry run;
its mocked contract tests run with `python3 -B tests/ci/test-build-release.py`.

Only the release-record job has `contents: write` (plus PR read access), using
the existing `GITHUB_TOKEN`; no new PAT is required. Repository tag/release
rules must permit these main-build records. This permission is not intended
to tag arbitrary non-main workflow revisions. GitHub may also reject an older
main run if its workflow files differ from the current default branch; do not
broaden the token or move old tags to bypass that restriction.

**A build record is not production qualification.** Signing runs separately
after `docker-build` succeeds; the record does not assert that signing, SBOM
verification, smoke tests or deployment have completed. Existing `v*.*.*`
releases still require their separate smoke, image-signature and signed-SBOM
gates in `release.yml`. No signing permissions or gates are changed here.

## Run on the Linux host

The Production server requires a stable JWT signing key. The existing
bootstrap creates/preserves one in a gitignored `.env`; never pass it into
the image build:

```bash
./scripts/compose-bootstrap.sh
docker compose up -d --no-build
curl --fail http://127.0.0.1:8950/health
```

Open `http://127.0.0.1:8950/autotable/`.
`docker compose up -d --build` is the alternative combined source-build/run
command. Bootstrap is idempotent: repeating it preserves an existing key.
Unquoted, single-quoted and double-quoted configured keys are preserved;
empty `JWT_SIGNING_KEY=`, `JWT_SIGNING_KEY=""` and `JWT_SIGNING_KEY=''`
receive a key once. The last definition wins, including `export` syntax.
Bootstrap reads the assignment as data, never by sourcing/evaluating the
env file.
Store the env file privately on a filesystem that preserves access controls.

## Host port and Nginx

Only the published host port changes; Kestrel stays on container port8080.

```bash
MAHJONG_HOST_PORT=8951 docker compose up -d --no-build
```

The default bind address is `127.0.0.1`. A host Nginx upstream should point
to `http://127.0.0.1:8950` (or the chosen host port), preserving
`/autotable/`, `/api/`, `/hubs/`, and `/autotable/ws`. See
[reverse-proxy.md](reverse-proxy.md) and the
[HTTP/WebSocket example](../infra/nginx/mahjong-http.conf.example).
Set `MAHJONG_BIND_ADDRESS=0.0.0.0` explicitly for direct network access;
that exposes the service beyond loopback.

## Isolated second instance

Choose an unused host port, a new Compose project, and a separate private
env file. No fixed container name or shared explicit volume name is used.
From any directory, supply absolute paths:

```bash
ROOT=/path/to/mahjong-autotable
ENV_FILE=/private/path/mahjong-smoke.env
"$ROOT/scripts/compose-bootstrap.sh" --env-file "$ENV_FILE"
MAHJONG_HOST_PORT=8951 docker compose -f "$ROOT/docker-compose.yml" \
    --env-file "$ENV_FILE" -p mahjong-smoke up -d --no-build
```

Keep the same project name/env file for subsequent restarts or recreation.
The named `mahjong-data` volume is scoped by project. Do not reuse another
instance's data volume or occupied port.

## Transfer without a registry

```bash
./build.sh --archive mahjong-autotable.tar
```

Transfer the archive and Compose/bootstrap files to the Linux server, then:

```bash
docker load --input mahjong-autotable.tar
./scripts/compose-bootstrap.sh
docker compose up -d --no-build
```

Use the same image tag/platform when building and running. Archive paths
are relative to the caller's working directory; existing files are not
overwritten. Runtime signing keys are not part of the archive.

`--archive` / `-Archive` uses `docker image save` and writes **raw tar**,
regardless of the filename. For an actual gzip-compressed transfer:

```bash
./build.sh --tag mahjong-autotable:latest --archive mahjong-autotable.tar
gzip -n mahjong-autotable.tar
# Transfer mahjong-autotable.tar.gz, then on the target host:
docker load --input mahjong-autotable.tar.gz
```

Do not name raw tar `.tar.gz`. PowerShell users can transfer the raw `.tar`,
or compress it with a gzip tool; `Compress-Archive` produces ZIP, not gzip.

## Lifecycle and logs

```bash
docker compose ps
docker compose logs --tail 100 mahjong
docker compose restart mahjong
docker compose up -d --no-build --force-recreate mahjong
docker compose down
```

Restart/recreate with the same env file/project preserves the key and
volume. `down` retains data; **`down -v` deletes the project's database**.
Do not print `docker compose config` or full `docker inspect` output when
it would expose runtime environment secrets.

**Game recovery:** when a Changsha runtime is created for a public room,
its public room-to-runtime binding and initial snapshot are persisted
together. Recovery uses that stored binding and validates the saved state
and replay history before publishing the room. Retain `/data`, the stable
signing-key configuration, and the player's original identity credentials.
Invalid or incompatible recovery data must not silently create a replacement
runtime.

Legacy rooms created without a durable public binding cannot be recovered
by guessing a runtime from the old room URL. Start a new game explicitly
instead of treating a failed join as permission to redeal that room.
Volume/key retention alone must not be presented as proof of public-room
game resumption or completed gameplay qualification.

The image runs as UID/GID1000:1000, with SQLite under `/data`, an in-image
healthcheck, and `tini` for shutdown signal handling. All application files
are baked into the image; no source/bundle bind mount is required.

For provider overlays, backups, limitations and troubleshooting, see
[deployment.md](deployment.md). Build/hosting proof is distinct from the
separately counted Mahjong gameplay/rules qualification and release gates.
