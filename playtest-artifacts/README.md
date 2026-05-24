# Playtest artifacts

This directory holds short, hand-runnable Playwright-based playtests + proof screenshots
for the Changsha mahjong gameplay.

## v3 — full lobby → connect → take seat → deal walkthrough

`playtest-v3-fresh.spec.mjs` is a standalone Node script that drives Chromium through
the canonical user flow:

1. Load `/autotable/`
2. Dismiss the tour overlay
3. Click **Quick Match** (in the lobby panel)
4. Close the lobby panel (so it stops intercepting main-UI clicks)
5. Click **Connect** (SignalR + WS)
6. Click **Take Seat** (auto-fills 3 bots in remaining seats)
7. Click **Deal** (server deals 14/13/13/13 and emits `Match started`)
8. Observe bot activity for 30 s

### Run it

The backend must be reachable at `E2E_BASE_URL` (default `http://127.0.0.1:8088`):

```bash
# Start the backend (fresh sqlite DB recommended for a clean lobby):
cd src/backend/src/Mahjong.Autotable.Api
export ConnectionStrings__Sqlite="Data Source=/tmp/playtest-mahjong.db"
export ASPNETCORE_URLS="http://0.0.0.0:8088"
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --no-launch-profile &
sleep 25
curl -sf http://127.0.0.1:8088/health
```

In a second terminal:

```bash
cd src/frontend/autotable-src
npm install                                  # one-time, brings playwright
cd ../../..
E2E_BASE_URL=http://127.0.0.1:8088 \
  node --experimental-vm-modules playtest-artifacts/playtest-v3-fresh.spec.mjs
```

Screenshots land in `playtest-artifacts/v3/` and a JSON summary in
`playtest-artifacts/v3/findings.json`.

The included `v3/06-observed.png` is a proof screenshot of the rendered
Changsha game — face-up tile walls, 14-tile dealer hand, 3 seated bots,
score panel at `25000` per seat, and the "Match started — dealer is Seat 0"
move-log entry.

### Known limitation

As of `4062626` (PR #73 merged), bots are seated but do **not** take their
turns autonomously — `phase-g-bot-scheduler` is the follow-on work that
wires bot ticks to the discard / claim / Hu pipeline.
