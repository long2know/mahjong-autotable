# Session: 3D Renderer Spike (2026-05-13T17-40-17Z)

**Agent:** Hicks (Frontend Dev) | **Topic:** 3D Renderer Integration Scoping | **Duration:** 521s

## Summary

Read-only scoping spike on wiring autotable 3D perspective view (three.js mesh-rendered scene) to live Changsha game state. Audited upstream autotable codebase, analyzed five integration strategies, and recommended **Strategy C: Fake autotable WS server** as the MVP path (Complexity L, ~900 LOC, 3–5 days for Phase 5a).

**Key Finding:** The current "3D bridge" in Phase 2 is theater — the upstream bundle exposes zero listeners for changsha-bridge CustomEvents and no JavaScript surface beyond `window.__THREE__`. Canvas effect is limited to opacity flip on a sprite.

**Recommended Path:** Collocate a WebSocket endpoint in the .NET backend speaking upstream's NEW/JOIN/JOINED/UPDATE protocol verbatim. Translate authoritative ChangshaGameState into upstream's seven collections (match, seats, things, nicks, mouse, sound, dice). Bundle connects unchanged — byte-identical.

## Deliverables

- `docs/rules/changsha-3d-renderer-plan.md` (829 lines, 41KB) — full analysis with 5 strategies evaluated, complexity estimates, top 3 risks, Phase 5a/5b/5c scope breakdown
- `.squad/decisions/inbox/hicks-3d-renderer-spike.md` — decision summary (TL;DR + recommended strategy + complexity + open questions)
- `.squad/agents/hicks/history.md` (+74 lines) — task entry + strategy summary + open questions filed

## Key Links

- **Full Spike:** `docs/rules/changsha-3d-renderer-plan.md`
- **Decision Summary:** `.squad/decisions/inbox/hicks-3d-renderer-spike.md`
- **Hicks History Entry:** `.squad/agents/hicks/history.md` (last 74 lines)
- **Orchestration Log:** `.squad/orchestration-log/2026-05-13T17-40-17Z-hicks.md`

## Status

Spike complete. Awaiting Stephen's approval to proceed with Phase 5a (walls + dealt hands visible in 3D).
