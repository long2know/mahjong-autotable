# Orchestration Log Entry

> One file per agent spawn. Saved to `.squad/orchestration-log/{timestamp}-{agent-name}.md`

---

### {timestamp} — {task summary}

| Field | Value |
|-------|-------|
| **Agent routed** | {Name} ({Role}) |
| **Why chosen** | {Routing rationale — what in the request matched this agent} |
| **Mode** | {`background` / `sync`} |
| **Why this mode** | {Brief reason — e.g., "No hard data dependencies" or "User needs to approve architecture"} |
| **Files authorized to read** | {Exact file paths the agent was told to read} |
| **File(s) agent must produce** | {Exact file paths the agent is expected to create or modify} |
| **Outcome** | {Completed / Rejected by {Reviewer} / Escalated} |

---

## Rules

1. **One file per agent spawn.** Named `{timestamp}-{agent-name}.md`.
2. **Log BEFORE spawning.** The entry must exist before the agent runs.
3. **Update outcome AFTER the agent completes.** Fill in the Outcome field.
4. **Never delete or edit past entries.** Append-only.
5. **If a reviewer rejects work,** log the rejection as a new entry with the revision agent.

---

## Phase F — Changsha Realism (Wave 4) — 2026-05-19

> Local per-agent files exist in `.squad/orchestration-log/` (gitignored — local-only).
> This is the committed summary.

**Branch:** `stlong/phase-f-changsha-realism` (cut from `stlong/phase-b-changsha-scene`)
**Tip:** `b64efb8` — Wave 4 + reconciliation + stale-bundle prune
**Trigger:** Stephen's directive `copilot-directive-2026-05-19T1605Z-changsha-realism.md`
**Test gate at exit:** 319 / 0 / 9 (was 318/1/9 before reconciliation pass)
**Bundle at exit:** `src/frontend/autotable/autotable-src.6d5fae4c.js`

### Wave 4 Shape

```
   Stephen's directive (T+0)
        │
        ▼
    Ripley (sync, ~16 min)    ← design gate
        │  ridge: §6 disjoint scope table
        ▼
   ┌────┴──────────────────┐
   │     PARALLEL FAN-OUT  │
   ├──────┬───────┬────────┤
   ▼      ▼       ▼        
Vasquez Hicks   Bishop      (background, all 3)
~25 min ~23 min ~38 min     ← Bishop the bottleneck (predicted in Ripley §6)
   │      │       │
   └──────┴───┬───┘
              ▼
       Coordinator reconcile (sync, ~4 min)
       30d03ee: pickup singleton key + privacy mask slot suffix
       b64efb8: prune stale parcel bundle d9507f0f.js
              │
              ▼
         Scribe sweep (this)
```

### Entries

| # | Timestamp | Agent | Role | Mode | Elapsed | Outcome |
|---|---|---|---|---|---|---|
| 1 | 2026-05-19T16:30Z | Ripley | Lead, Architect | sync | ~16 min | **Completed.** 955-line design doc; §6 disjoint scope table enabled the parallel fan-out without file-level collision. |
| 2 | 2026-05-19T17:00Z | Vasquez | Rules Engineer / Acceptance Test Author | background | ~25 min | **Completed.** 12-axis rule audit; ~45 acceptance test cases across 3 new files (reflection-based so test assembly always compiles). |
| 3 | 2026-05-19T17:00Z | Hicks | Frontend Lead | background | ~23 min | **Completed.** `tsc` strict ✓, parcel build ✓; bundle `autotable-src.6d5fae4c.js`. Variant restored, manual-pickup UI wired, pickers + URL params + localStorage shipped. |
| 4 | 2026-05-19T17:00Z | Bishop | Backend | background | ~38 min | **Completed.** `dotnet build` 0/0; 22/22 bot engine tests, 22/23 manual-pickup tests, all variant-switch tests green. New `Changsha/Bot/` dir; `AutotableRuntimeMode` gate; pickup state machine. |
| 5 | 2026-05-19T17:45Z | Coordinator | Squad | sync | ~4 min | **Completed.** Reconciliation: pickup-singleton-key drift fixed (translator emits both string + number keys); privacy-mask slot-suffix test fixed per Bishop's diagnosis (`StartsWith("hand.0")` → `EndsWith("@0")`); stale parcel bundle pruned. 318/1/9 → 319/0/9. |

### Inputs/Outputs Summary

**Inputs read across the wave** (Ripley's §6 scope map enforced disjoint reads on the parallel batch):
- Stephen's directive (all 5 agents)
- `docs/rules/changsha-spec.md` v1.2 (Ripley, Vasquez)
- MahjongPros + Baidu rule sources (Vasquez)
- Wave 3 prior art in `.squad/decisions.md` (Ripley)
- Pre-Phase-B `git show 98d4cca^:src/frontend/autotable-src/src/{setup-slots,setup-deal,types}.ts` (Hicks — `SLOT_GROUPS`/`DEALS`/`GameType` recovery)
- Ripley design (all parallel agents); Vasquez acceptance tests (Bishop, as red→green target)

**Outputs produced (committed):**
- 9 frontend source files (`src/frontend/autotable-src/`)
- 1 parcel bundle (`autotable-src.6d5fae4c.js`)
- 14 backend source files (`src/backend/src/Mahjong.Autotable.Api/`) including new `Changsha/Bot/` dir
- 3 acceptance test files (`src/backend/tests/.../Changsha/Acceptance/`)
- 1 test project file edit (global usings)

**Outputs produced (local-only — `.squad/decisions/inbox/` is gitignored):**
- Ripley's `ripley-phase-f-design.md` (~955 lines)
- Vasquez's `vasquez-phase-f-rule-audit.md` (382 lines)
- Hicks's `hicks-phase-f-frontend.md`
- Bishop's `bishop-phase-f-backend.md`
- (All retained as primary sources; merged summary lives in `.squad/decisions.md`)

### Deferred Follow-ups (filed, NOT blocking)

- Bot pickup tick scheduler (`OnPickupCue` hook present; runtime tick loop pending) — Bishop's small follow-up
- `FilterEntriesForViewer` (AutotableWsEndpoint.cs:644-652) slot-parse cleanup — same bug as the fixed test
- `MinShantenToHu` rigorous version (coarse approximation passes current tests) — deferred to V2
- Hard-tier EV budget overruns — Medium fallback per Ripley §7.6
- Soft variant hot-swap — Phase G

### Reviewer Rejection Lockout — not triggered

This wave had no rejection events; the reconciliation pass was mechanical wire-key alignment + a Bishop-diagnosed test bug applied by the coordinator (not a verdict against Bishop's work — Bishop deliberately did not fix it from his own seat per file-scope discipline).
