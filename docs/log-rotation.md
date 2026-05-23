# Log rotation

Phase J Wave 6 (Apone, DevOps). Without rotation, the container's
stdout/stderr (captured by Docker's default `json-file` log driver)
grows unboundedly until the host's disk fills up — a very common
production outage. This document is the two-page answer: configure the
Docker log driver to rotate, or fall back to host-side `logrotate(8)`
when you've already standardized on something else.

## Option 1 — Docker's built-in rotation (recommended)

Docker's `json-file` driver accepts two opts that turn on rotation
in-process without touching the host. Add them to the `docker run`
command (or to `docker-compose.yml`):

```bash
docker run -d \
    --name mahjong \
    --restart unless-stopped \
    -p 8080:8080 \
    -v mahjong-data:/data \
    --log-driver json-file \
    --log-opt max-size=10m \
    --log-opt max-file=5 \
    ghcr.io/long2know/mahjong-autotable:latest
```

| Opt              | Meaning                                                                 |
| ---------------- | ----------------------------------------------------------------------- |
| `max-size=10m`   | Rotate when the current file hits 10 MiB. Suffixes: `k`, `m`, `g`.      |
| `max-file=5`     | Keep at most 5 rotated files; the oldest is deleted when the cap fits. |

With these defaults the upper bound on disk consumption per container
is `max-size * max-file` = **50 MiB**, plenty for a self-hosted box and
fast enough that `docker logs --tail 200 mahjong` stays usable.

### Compose equivalent

```yaml
services:
  mahjong:
    image: ghcr.io/long2know/mahjong-autotable:latest
    # ...
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "5"
```

### Host-wide default (daemon level)

If you don't want to repeat the same `--log-opt` on every `docker run`,
set it once in `/etc/docker/daemon.json`:

```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "5"
  }
}
```

Then `systemctl restart docker`. New containers (including any started
by `systemctl start mahjong-autotable`) inherit the rotation policy.

## Option 2 — `logrotate(8)` on a bind-mounted log file

If you've bind-mounted the container's `/data` (or another writable
volume) and the application writes structured logs to a file inside
that volume — or you've started the app outside of Docker — wire
`logrotate(8)` instead.

> **The default `mahjong-autotable` image does NOT write logs to a
> file.** It emits structured JSON on stdout (Phase J Wave 5 logger
> config). `logrotate` is only relevant if you intentionally pipe
> stdout to a file in a host-mounted path (e.g.
> `docker run ... > /var/log/mahjong/app.log 2>&1`), or if you run the
> app under systemd outside of Docker.

Drop this in `/etc/logrotate.d/mahjong-autotable`:

```conf
/var/log/mahjong/*.log {
    daily
    rotate 14
    missingok
    notifempty
    compress
    delaycompress
    copytruncate
    create 0640 mahjong mahjong
    sharedscripts
    postrotate
        # No app reload needed for stdout redirection — the file
        # descriptor is held by docker-run's shell; copytruncate keeps
        # the descriptor valid across rotations.
    endscript
}
```

| Directive       | Why                                                                |
| --------------- | ------------------------------------------------------------------ |
| `daily`         | Rotate every 24 h. `weekly` / `monthly` also valid.               |
| `rotate 14`     | Keep 14 archives. Tune to your retention policy.                  |
| `compress`      | Gzip rotated files. `delaycompress` keeps the most-recent uncompressed for live grepping. |
| `copytruncate`  | Crucial — the app holds the fd open, so a rename + create would orphan its writer. `copytruncate` truncates in place. |
| `create`        | Re-create the file with the right perms after rotation.            |

### Sample bind-mount setup

```bash
mkdir -p /var/log/mahjong
chown 1654:1654 /var/log/mahjong     # uid in the .NET runtime base image
docker run -d \
    --name mahjong \
    -p 8080:8080 \
    -v mahjong-data:/data \
    -v /var/log/mahjong:/logs \
    --log-driver none \
    ghcr.io/long2know/mahjong-autotable:latest \
    /bin/sh -c 'dotnet Mahjong.Autotable.Api.dll > /logs/app.log 2>&1'
```

> This setup is for operators who specifically want filesystem logs;
> Option 1 is simpler and is the recommended default.

## Verifying rotation works

```bash
# Force a manual rotation under the Docker driver:
docker logs mahjong --tail 1 >/dev/null
ls -lh $(docker inspect --format='{{ .LogPath }}' mahjong)
# Expect at most max-file files of <= max-size bytes each.

# Force logrotate(8) to run now:
sudo logrotate -f /etc/logrotate.d/mahjong-autotable
ls -lh /var/log/mahjong/
```

## What gets rotated

Phase J Wave 5 wires `AddJsonConsole` in Production so every line on
stdout is a single JSON document with `Timestamp`, `LogLevel`,
`Category`, `Message`, `State`, and `Scopes`. Whichever rotation
mechanism you pick, the rotated archives stay JSON-Lines-shaped and
remain ingestible by Loki / Vector / Fluent Bit without
post-processing. See [`docs/observability.md`](observability.md) for
the structured-log catalog.

## Related docs

- [`docs/deployment.md`](deployment.md) — base run command (now updated to include rotation opts)
- [`docs/systemd.md`](systemd.md) — the sample unit file passes the rotation opts through
- [`docs/observability.md`](observability.md) — log shape + sampling
- [`docs/secrets.md`](secrets.md) § "Hygiene rules #4" — don't log credentials
