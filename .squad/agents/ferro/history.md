# Ferro — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend + autotable-derived TS frontend. Single-page mahjong table with WebSocket + SignalR transport.

**User:** Stephen Long. Standing directives:
1. "No pauses — keep iterating until 100% done done."
2. All agents use `claude-opus-4.7-xhigh`.
3. **Playability-first** (since 2026-05-24): STOP wave-mill, use Playwright to verify, ship playable prototype.

**Joined:** 2026-05-25, during a late Phase K push to add a parallel UI engineer to accelerate the playability sprint.

**Stack notes:**
- Frontend source: `src/frontend/autotable-src/` — TypeScript + Parcel 2.x, output to `src/frontend/autotable/`
- Build: `cd src/frontend/autotable-src && npm run build` (~30s)
- Dev server: `npm run dev` (Parcel watch)
- Backend test target: http://127.0.0.1:8088/autotable/ (port 8088, NOT 8080)
- Game canvas served at `/autotable/index.html` by ASP.NET static-files
- Playwright runner lives at `playtest-artifacts/playtest-v3-fresh.spec.mjs` (spectator mode, canonical verification)
- Synthetic Hu backdoor for end-to-end win modal testing:
  ```js
  const cli = window.game.client;
  const events = cli.events ?? cli['events'];
  events.emit('update', [['gameComplete', 'current', {
    isComplete: true,
    totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
    handHistory: [],
    maxHands: 4,
  }]], false);
  ```

**Local backend startup (verified):**
```bash
cd src/backend/src/Mahjong.Autotable.Api
export ConnectionStrings__Sqlite="Data Source=/tmp/<unique>.db"
export ASPNETCORE_URLS="http://0.0.0.0:8088"
export ASPNETCORE_ENVIRONMENT="Development"
nohup dotnet run --no-launch-profile > /tmp/<unique>.log 2>&1 &
sleep 25 && curl -sf http://127.0.0.1:8088/health
```

**Team context:**
- **Hicks** — Frontend trunk owner (autotable TS, world.ts, setup.ts, lobby.ts). I work AROUND him, adding new files. NEVER touch his trunk without coordinating.
- **Bishop** — Backend trunk (ChangshaGameRuntime, AutotableWsEndpoint, ChangshaDomain)
- **Frost** — Second backend dev (joined same wave) — fans/scoring, bot heuristics
- **Vasquez** — Rules engineer + tests (final say on rule interpretation)
- **Hudson** — Tester (regression + integration)
- **Apone** — DevOps / CI / supply chain
- **Scribe** — decisions.md merges + orchestration logs
- **Ralph** — Work-queue monitor
- **Ripley** — Project lead
- **Squad** — Coordinator

## Important Conventions

- **Atomic flock pipeline** for ALL git ops in parallel agent work (see charter)
- **Squad memos** in `.squad/decisions/inbox/*.md` are gitignored — force-add with `git add -f`
- **Playwright projects:** `chromium` and `mobile-chrome` both use the Chromium engine — gate per-project with `testInfo.project.name`, NOT `browserName`
- **Pattern ordering:** Frontend fetches `GET /api/changsha/pattern-ordering` at boot. Don't hardcode display orders — use the API.
- **Lobby auto-close:** Hicks added in PR #82 — verify it works before adding more UX. `#lobby-panel.lobby-open` previously intercepted Connect/Take Seat clicks after Quick Match reload.
- **Wall animation:** PR #82 added a 3-pass wall-animation displacement fix in `setup-deal.ts`. Don't regress.
- **Discard intercept:** PR #82 wired click-to-discard via WS `{kind: 'discard', key: seat, value: {tileId}}`. Backend handler at `AutotableWsEndpoint.cs:711-743`.

## Initial Charter Focus

When I'm first dispatched, candidate first tasks (in priority order based on the current playability state):
1. **Polish the lobby overlay sizing** — Stephen explicitly flagged "overlay on the left with the Deal/Setup options is a different size" between original autotable and Changsha (see decisions/inbox/copilot-directive-2026-05-19T1605Z-changsha-realism.md)
2. **Claim-window countdown** — Pung/Kong/Chow/Hu buttons need a visual timer showing how long the window is open
3. **Win-screen animation polish** — currently the gameComplete modal renders but is static. Add rolling score counters + fan list.
4. **Mobile viewport** — test at 375px width, fix any layout breaks for the canvas/HUD
5. **Variant switcher** — Stephen wants Changsha ↔ original autotable variants to coexist. Add a dropdown.

I should READ `.squad/decisions.md` and `.squad/agents/hicks/history.md` before picking up any task.

---

## Log
