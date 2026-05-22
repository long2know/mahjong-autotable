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

## Phase H Wave 1 Scribe Sweep — Stability + Polish (2026-05-21)

**Timestamp:** 2026-05-21T17:30Z
**Branch:** stlong/phase-h-wave-1-stability-polish (cut from main @ 730946c)
**Contribution:** Merged 4-file Phase H Wave 1 inbox into canonical `.squad/decisions.md` as a single `## Phase H — Stability + Polish (2026-05-21)` section with four subsections:

1. **Hicks — Lobby polish + Docker cleanup** (`hicks-phase-h-wave-1.md`): Captured the **critical `--public-url .` parcel build flag** as a prominent build invariant (without it, asset URLs are absolute and 404 under `/autotable/`). Four lobby additions (seed override, hand-count selector, save-defaults checkbox, About link). Dockerfile cleaned of dangling `src/frontend/modern/` references. Bundle hashes rolled: JS `33f97fad.js` → `c97ea9e9.js`, CSS `7934372e.css` → `96cb3b60.css`.

2. **Ripley — Architecture + V2 design** (`ripley-phase-h-design.md`): Documented two shipped files (`docs/architecture.md`, `docs/known-limitations.md`) locking Phase H structure. Cross-referenced Phase H Wave 2 design memo (local inbox, not committed): three rule implementations planned — NineTerminals (Changsha-adapted 9-Terminals hand pattern), RobbingKong (抢杠胡 added-kong claim window), and big-win stacking via `AllPatterns` list on `WinDetectionResult`.

3. **Bishop — StateVersion + bot timeout + CORS** (`bishop-phase-h-wave-1.md`): Recorded three backend production tasks. (1) `StateVersion` optimistic concurrency: new `ChangshaConcurrencyException` type, eight `IChangshaGameRuntime` mutations gained trailing `int? expectedVersion = null`, guard runs inside lock before state-machine call. (2) `BotDecisionTimeoutMs` option (default 2000ms) + `DecideActionWithTimeoutAsync` helper (Task.Run + Task.WhenAny pattern, logs Warning on timeout, returns safe default). (3) CORS shrunk from 4 origins → 2 (removed deleted Vite dev server ports). Gate baseline preserved: **330/0/9 tests**. Flagged one Vasquez test bug for follow-up: `Bot_Decision_Within_Timeout_ProceedsNormally` computed `expectedNatural` BEFORE `StartGameAsync` (empty hand) — should compute AFTER deal.

4. **Vasquez — Tests** (`vasquez-phase-h-wave-1.md`): Unskipped two Phase G marker skips (`StateVersion_OptimisticConcurrency_DeferredToV2`, `Bot_TimeoutFallback_DeferredV2`) + 8 net new tests (10 added, 2 skips removed). `BotBehaviorTests.cs`: 4 tests (1 unskip + 3 edges) covering timeout fallback, safe-default discard, claim-window pass, and timeout race. `EdgeCaseTests.cs`: 6 tests (1 unskip + 5 edges) covering StateVersion 0-start, null bypass, fresh success + increment, stale exception, exception message, and bot invocation no-increment. Reflection-backed symbol resolution allows test assembly to compile standalone. Verified: Bishop's bug flag appears resolved in her final commit (no mention of it in Vasquez's test manifest).

**Gate result:** `dotnet test` → **340 passed / 0 failed / 7 skipped of 347 total** (was 330/0/9 before; 2 V2-deferred tests unskipped, 8 net new passing edges). TS strict exit 0. Bundle rolled: JS `33f97fad.js` → `c97ea9e9.js`, CSS `7934372e.css` → `96cb3b60.css`.

**Inbox files:** All 4 memos present and read; not deleted (`.squad/decisions/inbox/` is gitignored — local-only primary sources per standing instruction).

**Notable:** Hicks's memo highlights a critical build-time invariant (`--public-url .`) that is easy to forget on future Parcel rebuilds; surfaced prominently in decisions.md for team reference. Bishop's memo documents contract details thread through the Wave 1 commit chain: `0a2499d` (Bishop production), `9377ab1` (Vasquez tests, rides on Bishop), with Bishop flagging a test logic bug in Vasquez's code (though tests ship green, suggesting Vasquez may have iterated and fixed before push — verified via test manifest).

**Branch ready for PR against `main`.** All four agents' Wave 1 work captured in canonical decisions.md.
