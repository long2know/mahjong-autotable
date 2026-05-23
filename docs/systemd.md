# systemd deployment

Phase J Wave 6 (Apone, DevOps). The canonical "boot starts the
container, `systemctl restart` redeploys it" wiring. Pairs with the
sample unit at
[`infra/systemd/mahjong-autotable.service.example`](../infra/systemd/mahjong-autotable.service.example).

`docker run --restart unless-stopped` already auto-restarts a container
on Docker daemon restart, so this unit is only required when you want
**systemd** (not Docker) to own the lifecycle — typically because:

- Other host services depend on the container being up (`After=`
  ordering).
- You want a single `systemctl status` view of "the mahjong app",
  including its config file under `/etc/default/`.
- You're driving redeploys from a tool that calls
  `systemctl restart` (rather than `docker compose up`).

If none of those apply, stick with `docker run --restart unless-stopped`
from [`docs/deployment.md`](deployment.md) and skip this page.

## Install

```bash
# 1. Copy the sample unit into place.
sudo install -m 0644 \
    infra/systemd/mahjong-autotable.service.example \
    /etc/systemd/system/mahjong-autotable.service

# 2. (Optional) populate the EnvironmentFile so the service knows the
#    image tag + public CORS origin without you editing the unit.
sudo tee /etc/default/mahjong-autotable > /dev/null <<'EOF'
IMAGE=ghcr.io/long2know/mahjong-autotable:latest
BUILD_SHA=
Cors__AllowedOrigins__0=https://mahjong.example.com
EOF
sudo chmod 0640 /etc/default/mahjong-autotable

# 3. Tell systemd to pick up the new unit, then enable + start it.
sudo systemctl daemon-reload
sudo systemctl enable --now mahjong-autotable
```

Verify:

```bash
systemctl status mahjong-autotable
journalctl -u mahjong-autotable -f
curl -fsS http://localhost:8080/health | jq
```

## What the unit does

| Directive                  | Purpose                                                                 |
| -------------------------- | ----------------------------------------------------------------------- |
| `After=docker.service`     | Wait for the Docker daemon to be up before launching.                  |
| `Type=simple`              | `docker run` (without `-d`) is the long-running foreground process.    |
| `Restart=on-failure`       | systemd restarts the container if it exits non-zero. Clean stops do not trigger a restart. |
| `RestartSec=5s`            | Brief debounce so a flapping crash loop doesn't pound the daemon.      |
| `LimitNOFILE=65536`        | Plenty of file descriptors for many simultaneous SignalR / WS clients. |
| `EnvironmentFile=-/etc/default/mahjong-autotable` | Optional override surface. The `-` makes it tolerant of a missing file. |
| `ExecStartPre=docker pull` | Pulls the current image before each start — handy for `latest`.        |
| `ExecStart=docker run --rm` | The container is named so subsequent `systemctl restart`s find and remove it cleanly. Logs land in the journal because Docker's `json-file` driver writes to a file the journal also watches. |
| `--log-opt max-size=10m`   | Wires Docker's built-in rotation (see [`docs/log-rotation.md`](log-rotation.md)). |
| `NoNewPrivileges=true`     | Hardening; container can't gain extra capabilities.                    |
| `ProtectSystem=full`       | Read-only `/usr`, `/boot`, `/etc` from the unit's perspective.         |

## Redeploy workflow

```bash
# Pull a new tag — the unit's ExecStartPre will pick it up automatically
# on restart.
sudo $EDITOR /etc/default/mahjong-autotable    # bump IMAGE=
sudo systemctl restart mahjong-autotable

# Or pin a specific sha-* tag (see docs/ci.md for the published tag scheme):
sudo sed -i 's|IMAGE=.*|IMAGE=ghcr.io/long2know/mahjong-autotable:sha-7c3e5a1|' \
    /etc/default/mahjong-autotable
sudo systemctl restart mahjong-autotable
```

The `--rm` + named container approach keeps the host's `docker ps -a`
clean: the unit pulls, runs, and when it stops cleans up after itself.

## Uninstall

```bash
sudo systemctl disable --now mahjong-autotable
sudo rm /etc/systemd/system/mahjong-autotable.service
sudo rm -f /etc/default/mahjong-autotable
sudo systemctl daemon-reload
# Data volume mahjong-data is preserved — remove with `docker volume rm`.
```

## Troubleshooting

| Symptom                                                | Likely cause                                                                 | Fix |
| ------------------------------------------------------ | ---------------------------------------------------------------------------- | --- |
| `systemctl start` hangs for ~120s and times out         | First-time image pull is slow / hit ghcr.io rate limit.                      | Pre-pull manually: `sudo docker pull $IMAGE`. |
| `Error response from daemon: Conflict. The container name "/mahjong-autotable" is already in use` | Previous `docker run` left the container in `Exited` state. | The `ExecStartPre=-/usr/bin/docker rm -f mahjong-autotable` line should clean this; if it doesn't, run `sudo docker rm -f mahjong-autotable` once and retry. |
| `Cors__AllowedOrigins__0=` is empty inside the container | EnvironmentFile not picked up (typo? wrong path?).                          | `systemctl cat mahjong-autotable` shows the loaded contents; `systemctl show mahjong-autotable -p Environment` lists what systemd resolved. |
| `LimitNOFILE` ignored                                   | systemd doesn't propagate `LimitNOFILE` into the container — it's a host-side cap. | Set `--ulimit nofile=65536:65536` on the `docker run` line as well. |

## Related docs

- [`docs/deployment.md`](deployment.md) — base Docker deployment + env vars
- [`docs/reverse-proxy.md`](reverse-proxy.md) — TLS + WebSocket upgrade fronting
- [`docs/log-rotation.md`](log-rotation.md) — log driver opts the sample unit uses
- [`docs/secrets.md`](secrets.md) — where to put real CORS / DB connection-string values
