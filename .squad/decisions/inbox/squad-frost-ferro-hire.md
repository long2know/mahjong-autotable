# Squad expansion — Frost + Ferro hired (2026-05-25)

**Requested by:** Stephen Long
**Effective:** 2026-05-25T22:15Z
**Reason:** Playability sprint is dragging because Bishop is the sole backend dev and Hicks is the sole frontend dev. Both have been doing solo work for multiple waves. Stephen explicitly approved hiring +1 UI engineer and +1 backend engineer to parallelize.

## New roster slots

| Name | Role | Charter | Pairs With | Status |
|------|------|---------|------------|--------|
| **Frost** | Backend Dev (parallel) | `.squad/agents/frost/charter.md` | Bishop | Active |
| **Ferro** | Frontend / UI Engineer (parallel) | `.squad/agents/ferro/charter.md` | Hicks | Active |

Both from the Aliens universe (consistent with existing roster: Ripley/Bishop/Hicks/Vasquez/Hudson/Apone).

## Lane assignments

To avoid stepping on Bishop's and Hicks's trunks:

### Frost (backend, parallel to Bishop)
**OWNS (new files / sub-features only):**
- Changsha rule **edge cases** (自摸 bonuses, 抢杠, 流局, 包牌, etc.)
- **Fan/scoring** catalog beyond basic 258-pair (七对, 清一色, 混一色, 杠上开花, 海底捞月, ...)
- **Bot strategy** heuristics — efficient-tile-selection, claim-priority tuning
- **Replay storage** — persisted event JSON for game playback
- **Tournament infra polish** — rating/seeding flow iteration

**MUST NOT TOUCH WITHOUT BISHOP COORDINATION:**
- `Changsha/Runtime/ChangshaGameRuntime.cs`
- `Autotable/AutotableWsEndpoint.cs`
- `Changsha/ChangshaDomain.cs`

### Ferro (frontend, parallel to Hicks)
**OWNS (new files / sub-features only):**
- **Visual polish** — overlay sizing, theming, layout
- **Claim window UX** — countdown timers, hover/disabled states
- **Win-screen animations** — rolling score counters, fan list reveals
- **Mobile responsive** — 375px / 768px viewports
- **Fluent UI 9 trial** — incremental React migration for lobby + score panel
- **Lobby UX iteration** — variant/dealMode/botCount/botDifficulty pickers

**MUST NOT TOUCH WITHOUT HICKS COORDINATION:**
- `src/frontend/autotable-src/src/world.ts`
- `src/frontend/autotable-src/src/setup.ts`
- `src/frontend/autotable-src/src/setup-deal.ts`
- `src/frontend/autotable-src/src/mouse-tracker.ts`
- `src/frontend/autotable-src/src/game-ui.ts`
- `src/frontend/autotable-src/src/lobby.ts`
- `src/frontend/autotable-src/src/index.html`

Both should prefer **adding new modules** (e.g., `src/ui/claim-window.ts`, `Changsha/Scoring/FanCalculator.cs`) and wiring them via DI / imports rather than editing trunk files in-place.

## Model directive

Both Frost and Ferro use `claude-opus-4.7-xhigh` (per standing directive).

## First-wave queue (deferred until after Bishop's `fix/manual-deal-plumb-and-auto-ack` PR merges)

- **Frost:** wire 自摸 self-draw bonus + 杠上开花 win-on-kong-replacement detection + tests
- **Ferro:** fix the lobby overlay sizing (Stephen-flagged regression vs. original autotable) + add a variant switcher to the lobby

## Onboarding notes

- Both have full charters + history.md skeletons committed in this PR
- Both know about the atomic flock pipeline, the per-provider EF migration convention, and the squad memo `git add -f` convention
- Both should READ `.squad/decisions.md` AND the relevant trunk-owner's `history.md` before picking up any task
