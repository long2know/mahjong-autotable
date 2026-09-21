# Scribe Health Report — 2026-06-19T16-50-00Z (full regression sweep wrap)

**State backend:** `FSStorageProvider` (verified via `squad_state_health` before any mutation).

## Decisions archival — HARD GATE (Tier 1, 30-day)
- **decisions.md: 1,550,871 B → 1,398,558 B** (net −152,313 B).
  - Tier-1 trim removed the foundational + pre-Phase-H era (2026-04-20 → 2026-05-20; lines 3–1841: Active Decisions, Changsha v1 audits/governance/spec-lock, MahjongPros source, opus-4.7 directive, 3D spike, Ripley pivot plan, Phases B–G) → −160,378 B; live log now begins at **Phase H (2026-05-21)**.
  - Inbox merge added the new §2026-06-19 section → +8,065 B.
- **decisions-archive.md: 20,471 B → 181,626 B** (+161,155 B, dated provenance header added).
- ⚠️ **Note:** Per task instruction #1 only the **Tier-1 (30-day)** archival was applied (matching the prior 2026-06-15 run's practice). decisions.md remains **> 50 KB**, so the charter's **Tier-2 (7-day)** archival is available if the Coordinator wants a leaner live log (would retain only the 2026-06-15 + 2026-06-19 sections).

## Decision inbox
- **5 → 0.** Merged Bishop D1, Hicks D3, Frost D5 + earlier Frost #106 and Bishop #107/#108; D4 F5-drift fix + 2 P2 follow-ups captured. All 5 processed entries deleted.

## History summarization — HARD GATE (≥ 15,360 B)
8 histories exceeded the gate → full prior content preserved verbatim to a new per-agent `history-archive.md`; `history.md` rewritten to stable context block + archived-entry digest (+ 2026-06-19 entry for the 5 wave agents). **All now under 15,360 B:**

| Agent | history.md before → after | history-archive.md |
|---|---|---|
| apone | 321,015 → 5,570 | 321,159 |
| bishop | 256,399 → 15,087 | 256,544 |
| hicks | 278,259 → 11,931 | 278,403 |
| vasquez | 256,478 → 8,463 | 256,624 |
| frost | 30,870 → 5,665 | 31,014 |
| drake | 19,846 → 2,471 | 19,990 |
| ripley | 38,543 → 1,694 | 38,688 |
| scribe | 557,340 → 3,884 | 557,485 |

Under threshold, untouched: ferro (12,064 B), hudson (12,892 B), ralph (502 B).

## Other artifacts
- Orchestration logs: **6** written (`2026-06-19T16-50-00Z-{bishop,vasquez,hicks,apone,frost,scribe}.md`).
- Session log: `log/2026-06-19T16-50-00Z-regression-sweep.md`.
- Cross-agent history: 2026-06-19 learnings appended to bishop, vasquez, hicks, apone, frost.
- `STATE-OF-GAME.md` (tracked repo file): 30,255 → 33,404 B; §2026-06-19 appended → **reported to Coordinator for git commit**.

## Git / ownership
- Mutable `.squad/` state left **uncommitted** (runtime-owned); no `git add`/`commit`/`reset` on squad state.
- Bulk archival/summarization performed via filesystem on the FSStorageProvider-backed store (state-tool payload limits); state tools used for all merges, deletes, appends, and writes; results verified via `squad_state_*`.
- No scratch/temp files left behind.
