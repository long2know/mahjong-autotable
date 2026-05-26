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

### Iter 1 — 2026-05-23 — Claim-window countdown + Win-screen polish

**PR:** `feat/ferro-claim-window-and-win-screen` (this branch).
**Decision memo:** `.squad/decisions/inbox/ferro-claim-window-and-win-screen.md`

Picked up tasks 2 + 3 from the initial charter (claim-window countdown
overlay + win-screen polish) because Hicks's PR #82 had already shipped
the gameplay loop (lobby auto-close + click-to-discard + wall anim).
Postponed the lobby-overlay-sizing polish (task 1) to a follow-up iter so
Stephen could see the more visible UX polish first.

**What I built:**

- `src/ui/claim-window-overlay.ts` + `.css` — fixed bottom-bar that
  subscribes to `client.claim` Collection, renders 44px-tall Pung/Chow/
  Kong/Hu chips with keyboard shortcuts (P/C/K/H/Esc), aria-live timer,
  progress bar that fills as the window expires, auto-pass at 0.
- `src/ui/win-screen-polish.ts` + `.css` — wraps `#game-complete-modal`
  with a MutationObserver, replaces the static "Total Δ" numbers with
  1.2s rolling rAF counters (ease-out cubic, 80ms stagger), inserts a
  "番种 Fans Scored" card grid between totals and recap.
- `src/ui/ferro-bootstrap.ts` — listens for `mahjong:three-renderer-ready`
  + polling fallback; attaches both classes idempotently.
- 1-line dynamic import in `src/index.ts` (NOT on Hicks's forbidden list).

**Trunk discipline:** Did NOT touch any of: `world.ts`, `setup.ts`,
`setup-deal.ts`, `mouse-tracker.ts`, `game-ui.ts`, `lobby.ts`,
`index.html`.  The win-screen polish wraps the existing modal markup via
MutationObserver — no edits to the modal DOM that `game-ui.ts` produces.

**Bundle impact:** ferro-bootstrap chunk = 14.34 kB raw / 4.89 kB gzipped.
No new runtime deps.

**Trunk bugs discovered (logged for Hicks, NOT fixed here):**

1. `game-ui.ts:refreshClaimButtons` — TypeError on `.available.includes()`
   when `client.claim.set({action:'pass', type:null})` echoes locally.
   Mitigation: my overlay uses `isClaimEntry()` guard — Hicks should
   add the same in `refreshClaimButtons`.
2. `game-ui.ts:998` — `[...result.score].sort(...)` crashes when
   `result.score` is undefined/null.  Easy fix: `[...(result.score ?? [])]`.

**Playtest evidence:** 6 screenshots in `playtest-artifacts/ferro-iter1/`
covering desktop + mobile (375px) for both overlays.  Spectator playtest
regression: pageErrors=0, ≥30 move-log entries ✓.

**Lessons learned:**

- The Collection.set echo behavior depends on `client.connected()` — in
  the disconnected branch it emits locally immediately, in the connected
  branch it pends + sends.  Synthetic specs MUST close claim windows via
  `events.emit('update', [['claim','0',null]], false)` tombstones, NOT
  by pressing Escape (which triggers the trunk bug above).
- Bootstrap 4 modal-dialog-centered loses its show state when the
  viewport resizes from desktop → mobile; force-show via
  `$('#game-complete-modal').modal('show')` is necessary. Also remember
  to hide BOTH `#settings-drawer` AND `#settings-drawer-v2` — the v2
  variant overlays the modal on 375px viewports.
- `MutationObserver` on a Bootstrap modal needs a `subtree: true` watcher
  on the modal-body, not just the modal — game-ui re-renders the table
  in place.

**Charter follow-ups still open:**

- Lobby overlay sizing parity (task 1)
- Variant switcher (task 5)
- Mobile canvas/HUD beyond what's already verified (task 4)
