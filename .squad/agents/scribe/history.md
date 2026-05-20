# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Scribe as Session Logger and Decision Merger.
- Scribe maintains `.squad/decisions.md`, `.squad/decisions/inbox/`, `.squad/log/`, and `.squad/orchestration-log/`.

## Architectural Pivot — Phase A Scribe Sweep (2026-05-13)

**Timestamp:** 2026-05-13T23:55Z
**Branch:** stlong/phase-b-changsha-scene (where Phase B work is in progress)
**Contribution:** Merged 8-file pivot inbox into canonical `.squad/decisions.md`: 3 user directives (architectural pivot intent + binding binding plan + F5 constraint), 3 parallel inventories (Bishop/Hicks/Vasquez), Ripley's 5-phase pivot plan, and Stephen's acceptance directive (all 16 defaults + MVP fast-cuts). Wrote 4 orchestration log entries (Bishop/Hicks/Vasquez/Ripley, 2026-05-13T22:50Z – 23:20Z). Wrote 1 session log documenting pivot narrative and Phase A outcome (188/0/7 tests, Parcel green, merged to main @ 55d8dfb). Appended "Phase A SHIPPED" cross-agent history entries (Bishop, Hicks, Vasquez, Ripley). Deleted inbox files after merge.

## Phase F Scribe Sweep — Wave 4 RECONCILED & SHIPPED (2026-05-19)

**Timestamp:** 2026-05-19T18:00Z (post-reconciliation, post-stale-bundle-prune)
**Branch:** stlong/phase-f-changsha-realism @ b64efb8 (cut from stlong/phase-b-changsha-scene)
**Contribution:** Merged 5-file Phase F inbox into canonical `.squad/decisions.md` as a single `## Phase F — Changsha Realism (manual pickup, variant switch, 3-tier bot)` section covering Stephen's directive + Ripley's §1–§5 architecture (variant switching, manual-pickup state machine, deal-mode toggle, bot fill modes, 3-tier bot engine) + Vasquez's 12-axis rule audit (locked defaults table + falsifiable Easy/Medium/Hard tier specs) + Hicks's frontend delta + Bishop's backend delta + reconciliation pass + deferred follow-ups + the §8.1 Test 1 smoke recipe. Wrote 5 orchestration log entries (Ripley sync ~16 min; Vasquez/Hicks/Bishop parallel 25/23/38 min; Coordinator reconcile 4 min). Inbox files left in place per Stephen's instruction (`.squad/decisions/inbox/` is gitignored — local-only, primary sources). Wave 4 result: **319/0/9 tests** (was 318/1/9 before reconciliation), bundle `autotable-src.6d5fae4c.js`. Committed Ripley's uncommitted history.md edits (Phase F design entry) as part of this sweep. Branch ready for PR against `main`; recommend merging Phase B first if not yet on main.
