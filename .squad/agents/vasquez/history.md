# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Vasquez as Rules Engineer.
- Rule priorities: Changsha wall/draw behavior, turn transitions, and compatibility seams for expanded Chinese rules.
- Current repository state is scaffolding-only; `RuleSet = changsha` exists, but no executable game-state machine or action arbitration yet.
- Bot readiness requires one authoritative pipeline for human and bot actions, plus seat-scoped state privacy and replayable deterministic logs.
- Changsha implementation should be frozen as a versioned profile (`changsha-v1`) with explicit ambiguity list before coding transitions.
- First implementation contract should keep draw as server-owned and discard as the only external seat action to avoid turn-race ambiguity.
- A minimal but durable state contract must include seed/algorithm, ordered wall, action sequence, and canonical state hash so deterministic replay is verifiable.
- For the initial slice, ending on live-wall exhaustion is the only supported round termination; claims/kong/settlement stay explicitly out-of-scope.
- Completed canonical Changsha rules spec at `docs/rules/changsha-spec.md` (v1.0, 2026-04-22).
  - Cross-referenced three sources: MahjongPros (primary), Reddit community overview, Baidu/Tencent QQ rules.
  - Key findings: 108 tiles (no honors/flowers), 258 pair rule on standard wins, chow IS allowed (next-seat only), two-tier scoring (small/big win), bird catching post-win mechanic, no dead wall.
  - Current engine uses 136 tiles and generic hu detection — both require critical changes.
  - Deal flow must implement dice roll → break point → batch-of-4 dealing (currently 1-at-a-time).
  - Kong replacement must draw from back of wall (currently front).
  - 11 open questions documented requiring product direction.
  - Decisions recorded to `.squad/decisions/inbox/vasquez-changsha-spec.md`.

📌 Team update (2026-05-05T17-00-21Z): Changsha spec decision merged to `.squad/decisions.md` (Active Decisions section). Backend audit at `docs/rules/changsha-backend-gap.md`, frontend plan at `docs/rules/changsha-frontend-plan.md`, test catalog with 8 contradictions identified at `docs/rules/changsha-test-catalog.md`. High-priority rule contradictions (bird count, scoring model, multi-win resolution, instant win continuation) must be resolved before implementation begins. Orchestration logs filed to `.squad/orchestration-log/`.

## V1 Spec Lock (2026-05-06)

**Task:** Lock Changsha spec to v1 scope per user decisions. Resolve all open questions and Hudson's 8 test catalog contradictions.

**Branch:** `stlong/changsha-v1`

### Work Completed

1. **Spec revision** (`docs/rules/changsha-spec.md` v1.0 → v1.1):
   - Header updated: status "Draft" → "V1 LOCKED — Implementation-ready baseline"
   - §1 Tile Set: explicit 108 count, v1 exclusions (no honors/flowers/wildcards)
   - §3 Turn Flow: chow confirmed allowed, claim priority locked (proximity rule)
   - §4 Winning: restructured to 4 v1 patterns only (standard 4+1, Seven Pairs, All Pungs, Full Flush); all other patterns deferred to v2
   - §5 Scoring: locked MahjongPros model (1/2/3/4/6/7 with configurable base unit); 10 worked payment examples added; bird-catching/kong-payments/multiple-Hu deferred to v2
   - §6 Game End: 16-hand lock (4 rounds × 4 hands); banker rotation clarified (keep seat on win, rotate CCW on loss)
   - §7 State Machine: simplified to v1 flow (no instant wins, no bird-catching, no seabed choice states)
   - §9 Open Questions: all 11 marked RESOLVED or DEFERRED-V2
   - §10 Hudson Contradictions: NEW section resolving all 8 source contradictions from test catalog
   - §11 Assumptions: trimmed to v1 scope (10 items)
   - §12 V1 Conformance Checklist: NEW — 60-item build-complete checklist for Bishop/Hudson

2. **Contradictions resolved**:
   - OQ-1 (Bird count): 1 tile standard, deferred to v2
   - OQ-2 (Multiple Hu): proximity rule locked for v1
   - OQ-3 (Red Dragon dealer): source error identified, deferred to v2
   - OQ-4 (Instant win flow): continue-no-redeal per Baidu, deferred to v2
   - OQ-5 (Kong dice): back-of-wall only in v1, dice deferred to v2
   - OQ-6 (Scoring model): MahjongPros authoritative (1/2/3/4/6/7)
   - OQ-7 (Seven Pairs 258): random-eye exemption (any pair)
   - OQ-8 (Full Beggar chow): not a contradiction (chow IS allowed), deferred to v2

3. **V1 scope locked**:
   - Tile set: 108 (suits only, no honors/flowers/wildcards)
   - Win patterns: 4 only (standard 4+1 with 258 pair, Seven Pairs, All Pungs, Full Flush)
   - Win methods: self-draw + discard claim (no robbing-kong, no instant wins, no seabed)
   - Scoring: 2-tier (Small 1/2, Big 3/4/6/7) with dealer bonus, configurable base unit
   - Claim priority: Win > Kong = Pung > Chow (proximity breaks ties)
   - Chow: ALLOWED (next-seat only)
   - Banker rotation: keep seat on win, rotate CCW on loss
   - Game length: 16 hands (4 rounds × 4 hands)
   - Deferred to v2: instant wins, draw-based Big Wins, bird-catching, kong payments, multiple simultaneous Hu, seabed choice, ready-kong dice

4. **Decision record**: `.squad/decisions/inbox/vasquez-v1-spec-lock.md`

5. **Commits**:
   - Commit 1: "docs(changsha): lock spec to v1 scope (108 tiles, no honors, 4 hand patterns)"
   - Commit 2: "docs(changsha): resolve 8 test catalog contradictions + add v1 conformance checklist"

### Key Rulings

- **MahjongPros is authoritative** for v1 (primary source)
- **Big Wins exempt from 258 pair rule** — any pair allowed (Seven Pairs, All Pungs, Full Flush)
- **Chow IS allowed** in Changsha (next-seat only) — all sources confirm
- **Proximity rule for multiple Hu** in v1 (closest counterclockwise wins); simultaneous wins deferred to v2
- **Kong replacement from back of wall** in v1 (no dice option); ready-kong dice deferred to v2
- **1 bird tile** standard (not 2); bird-catching deferred to v2
- **Banker keeps seat on win**, rotates counter-clockwise on loss (non-dealer win or draw)
- **16 hands per game** (4 rounds × 4 hands); round wind changes every 4 hands

### Ambiguities Remaining

**ZERO.** All v1 scope rules are unambiguous and implementation-ready. Conformance checklist (§12) is the build-complete contract for Bishop and Hudson.

### Files Modified

- `docs/rules/changsha-spec.md` (v1.0 → v1.1, 680 lines)
- `.squad/decisions/inbox/vasquez-v1-spec-lock.md` (new)
- `.squad/agents/vasquez/history.md` (this update)

## V1 Conformance Audit (2026-05-13)

**Task:** Spec-vs-source-vs-code conformance audit ahead of any external v1 release. Read-only.

**Result:** Changsha gameplay loop is playable end-to-end at the 3D autotable. 9 areas fully conformant, 4 partial, 0 missing, 7 ⏸ deferred-v2-by-design.

**Top findings (in priority order):**
1. **Banker rotation diverges from all three canonical sources.** All sources say "winner becomes dealer." Spec/code use a cyclic +1 CCW rotation that only retains the dealer when the dealer themselves wins. This is a deliberate v1 simplification per Stephen's lock, but it is *visible* to any player who knows real Changsha. Recommend surfacing as a configurable table option before external release.
2. **Chow `tileIds` disambiguation is accepted on the wire but discarded server-side.** `RemoveChowTiles` always picks the first valid pattern. Edge case: holding `{1,2}` and `{4,5}` when `3` is discarded — players cannot choose `{4,5}`.
3. **Missed-win rule (过胡) is in spec §3.6 but not enforced.** `ClaimAdjudicator` re-emits Hu opportunities unconditionally; passing on a Hu does not lock the seat out of winning on that logical tile until next draw.

**Spec hygiene items noted (non-blocking):**
- §2.7 (Instant Win Check) contradicts §4.3 (instant wins deferred to v2). §2.7 is legacy v1.0 text that needs to be struck or qualified.
- §5.2 base-unit configurability is locked but `ScoringService` returns raw values (1/2/3/4/6/7). Productization item for Bishop.
- §2.4 dice "count stacks" interpretation differs from S1's "count tiles" (which contradicts itself elsewhere). Recommend footnoting.

**Important learnings for the team:**
- **MahjongPros (S1) has at least two arithmetic errors:** (a) wall-build paragraph sums to 106 not 108 tiles; (b) deal paragraph describes only two batch-of-4 rounds (yielding 9 tiles) before the +1 round. The spec silently corrects both. Future readers of S1 should be aware.
- **S3 references "Red Dragon" in the dealer-determination tie-break** despite Changsha excluding Red Dragon entirely. This is a S3 source error (likely cross-pollination from another Tencent variant) — confirmed via S1/S2 consensus that no honor tiles exist.
- **The win detector correctly treats starting-hand wins as Standard small wins** rather than Tian Hu/Di Hu big wins — consistent with v1 deferral. If we later add instant-win classification, it overlays on top, not replaces.
- **Replay determinism is solid:** seed → wall shuffle is reproducible; per-hand dice = `seed + HandNumber` so each hand's break point is reproducible; append-only `EventLog` + monotonic `StateVersion` give full replay capability.

**Files Modified:**
- `.squad/decisions/inbox/vasquez-changsha-conformance-audit.md` (new — full audit, 20 areas)
- `.squad/agents/vasquez/history.md` (this update)

**No production code touched.**

### 2026-05-13: Audit fan-out — Peer verdicts
- **Bishop:** Three real conformance bugs (kong priority, per-hand seed reuse, banker rotation direction inverted)
- **Hicks:** Frontend unplayable from UI (no lobby, no tile selection, 3D is theater)
- **Hudson:** Frontend entirely unproven (zero test coverage); backend rules engine proven by 73 green tests

### 2026-05-13: Banker rotation canonical lock (Phase 3, v1.2)

**Task:** Resolve the banker-rotation ambiguity that surfaced from Bishop's and my own conformance audits. Spec/code/sources gave three different behaviors:
- Spec §6.2 text (v1.1): "dealer keeps seat on dealer-win; rotate CCW otherwise" (simplified v1 rule).
- Spec §6.2 example: `-1 mod 4` (Seat 0 → Seat 3).
- Backend `ChangshaStateMachine.cs:458,465`: `+1 mod 4` (direction-inverted vs. example).
- Canonical sources: **winner becomes dealer** (no cyclic rotation).

**Decision (locked, v1.2):** **Winner of a hand becomes the dealer for the next hand. On washout, the current dealer keeps the seat. Hand counter increments regardless.** No `+1 mod 4`, no `-1 mod 4`, no seat-cyclic logic of any kind.

**Verification:** Re-fetched MahjongPros, Baidu, Reddit via web_fetch. All three explicitly say winner-becomes-dealer. MahjongPros is the locked tiebreaker per Stephen's 2026-05-13 directive — no tiebreaker invocation actually needed because the three sources agree.

**V2 deferral noted:** Both MahjongPros and Baidu describe a finer-grained washout rule (e.g., MahjongPros: "the player that draws the last tile becomes the dealer"; Baidu: "if a player takes the bottom tile and no one wins, that player becomes the dealer"). V1 simplifies to "washout keeps the dealer seat" — unambiguous, deterministic, no new state required, matches dominant digital implementations. Captured as a v2 refinement in §6.2.

**Documentation:**
- `docs/rules/changsha-spec.md` bumped v1.1 → v1.2 with changelog.
- §6.2 rewritten with source quotes, worked example, implementation contract.
- §7.2 state-transition table corrected for both PAYMENT and WALL_EXHAUSTED transitions.
- §9 OQ-10, §11 assumption #9, §12 conformance checklist all updated.
- §5.2 base unit default clarified to 1 (raw values), with 10/100 as optional overrides — addresses the v1 conformance audit's noted gap between spec ("1 unit = 10 points") and `ScoringService` (raw 1/2/3/4/6/7).
- §3.3 Claim Priority and §3.6 Missed Win re-verified — no spec change needed (both already correctly stated; implementation gaps are Bishop's, not spec's).

**Impact handoff:**
- **Bishop:** Replace `(state.DealerSeatIndex + 1) % 4` with `state.DealerSeatIndex = winnerSeatIndex` (winner branch only); leave unchanged on washout. Increment `HandNumber` in both branches.
- **Hudson:** Add parametric test asserting dealer sequence across a 4-hand replay including a washout. Include negative assertions that `+1 mod 4` and `-1 mod 4` behaviors are gone.

**Files modified:**
- `docs/rules/changsha-spec.md` (v1.1 → v1.2)
- `.squad/decisions/inbox/vasquez-banker-rotation-lock.md` (new — full decision record with source quotes, Bishop/Hudson handoff)
- `.squad/agents/vasquez/history.md` (this entry)

**Production code:** Untouched (read-only per Phase 3 charter — Bishop owns the code fix).

**Key learning for the team:** When a spec, an implementation, and three canonical sources give three different answers to one rule, the answer is almost always "the spec was wrong and the implementation drifted from a separately-wrong spec." Re-anchor on the canonical sources, document the anchor, then push the implementation back. Don't try to retrofit a coherent story onto incoherent code/text.
