# Session Log — Architectural Pivot (Phase A shipped)

**Date:** 2026-05-13
**Session:** Architectural pivot + Phase A execution
**Duration:** Early afternoon → late evening (UTC)
**Requestor:** Stephen Long
**Outcome:** Phase A merged to main @ 55d8dfb

---

## The Pivot

Stephen rejected the then-current architecture (separate React SPA at `/changsha` + iframe-embedded autotable bundle + Strategy C postMessage bridge). Core insight: *"autotable IS the framework — implement Changsha rules INSIDE autotable, not bolted on."* This was the original day-1 architectural intent we'd misread.

The corrected direction: vendor pwmarcz/autotable as an in-tree fork, modify its TypeScript directly for 108-tile Changsha rules, have the .NET backend speak autotable's native `NEW/JOIN/JOINED/UPDATE` WS protocol, delete the React SPA and all bridge machinery.

---

## Team Work (Parallel Streams)

| Agent | Phase | Output | Time |
|-------|-------|--------|------|
| **Vasquez** | Rules audit | Rules divergence manifest (14 axes, 9 open Q's) | 2026-05-13T22:50Z (592s) |
| **Hicks** | Frontend inventory | Autotable TS modification survey (3 vendoring paths, Parcel vs Vite) | 2026-05-13T23:10Z (631s) |
| **Bishop** | Backend inventory | Component salvage map (~2,500 LOC pure logic survives, ~2,400 LOC legacy dies) | 2026-05-13T23:20Z (483s) |
| **Ripley** | Plan synthesis | 5-phase pivot plan (A–E), 16 numbered defaults, 3 MVP fast-cuts | 2026-05-13T23:05Z (two sync calls) |
| **Stephen** | Acceptance | All 16 defaults + MVP fast-cuts (a) + (b) + (c) approved for Phase A | 2026-05-13T23:20Z (batch decision) |
| **Bishop + Hicks** | Phase A execution | Backend purge + frontend vendor; tests green, Parcel build green | 2026-05-13T23:30Z–23:50Z (parallel) |

---

## Phase A Outcome

✅ **Code delta:**
- Deleted: Tables/* (~2,400 LOC src + ~1,120 LOC tests), 8 /api/tables/* REST endpoints, 2 EF entities, React SPA (~7,094 LOC), bridge receiver (~154 LOC)
- Vendored: pwmarcz/autotable @ 8b81d92 → `src/frontend/autotable-src/` (~34,000 LOC upstream TS)
- Wired: .vscode F5 compound launch (backend + Parcel watch), config.json bumped to claude-opus-4.7-xhigh

✅ **Test suite:** 188 passing, 0 failing, 7 skipped (Coordinator de-flaked pre-existing Tong-5 injection race)

✅ **Build:** Parcel production bundle green

✅ **Merged to main:** Commit 55d8dfb (de-flaking was pre-Phase-A main HEAD)

---

## Next: Phase B

Hicks concurrently implementing on `stlong/phase-b-changsha-scene` (branch target for Phase B work, where Scribe merges this pivot inbox + future drops).

Phase B deliverable: Changsha-shaped 108-tile wall (14/14/13/13), no honors, no riichi sticks, no dora slot, dealing runs autotable's local setup (no backend yet).

---

## Decision Archive

8-file pivot inbox (3 directives + 3 inventories + 1 plan + acceptance) merged to canonical `.squad/decisions.md`.
