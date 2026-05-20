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

📌 Team update (2026-05-13T10-00-00Z): Phase 3 wave complete. Bishop shipped 5 backend fixes + 203 passing tests; Hicks shipped lobby + claim UX + SignalR fixes; Hudson shipped vitest infra + 47 frontend tests. Banker rotation v1.2 now canonical (winner-becomes-dealer; washout keeps seat). All four streams merged to main in PR #25 (SHA a03feda). See `.squad/orchestration-log/` and `.squad/decisions.md` Phase 3 section for full details.

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

📌 Team update (2026-05-13T17-40-17Z): 3D Renderer spike complete — Hicks identified a canonical wall-split open question (14/14/13/13 vs 14/13/14/13 symmetric) for Stephen's product decision. Q6 from the spike may need Vasquez's rules ruling if Stephen chooses the asymmetric option.

## Rules Diff Manifest — Riichi vs Changsha (2026-05-13T23:00Z directive)

**Task:** Produce the authoritative rules diff that Hicks will follow when modifying autotable's vendored TS source per Stephen's binding pivot (autotable IS Changsha, not Changsha-bolted-on-top).

**Deliverable:** `.squad/decisions/inbox/vasquez-rules-diff-manifest.md` (~57 KB). 14 divergence axes; 13 v1 concepts to ADD + 12 v2-deferred; 31 concepts to REMOVE; 9 open questions tagged for Stephen.

### Learnings — rule clarifications locked down

- **Chow restriction is identical between Riichi and Changsha** ("only the next player in turn order"). The Riichi-convention phrasing ("from the player on your left") and the Changsha-spec phrasing ("immediately counterclockwise from the discarder") describe the same seat. Not a divergence — both restrict chow to the player who would naturally draw next if no one claims. A future engineer reading the spec from a Riichi background should not assume Changsha's chow rule is more permissive; it isn't.
- **Pao (sekinin barai) is structurally absent in Changsha**, but Changsha's standard discard-claim payment model (`点炮` → discarder pays alone for **every** win, not just yakuman) functionally encodes the same "discarder bears full cost" semantics as Riichi pao — just universally, not as a narrow yakuman-only rule. There is no separate pao tracking needed.
- **Furiten does NOT translate to Changsha's 过胡 rule.** Furiten is a *standing* tenpai restriction tied to your own discard pile (locked from ron on any winning tile you've ever discarded). 过胡 (过水) is a *per-tile transient* lockout triggered by *passing on a Hu opportunity*, lasting only until your next draw. Different triggers, different scope, different durations. Implementations must not collapse them into one mechanism.
- **Riichi-only mechanics that "feel like" they'd carry over but do not:** ippatsu, double riichi, ura-dora, kan-dora, tenpai-at-draw payments (流局聴牌払い), honba counters, Nagashi Mangan, kyuushu kyuuhai abortive draws, four-kans / four-winds / four-riichi / triple-ron abortive draws. None map to a Changsha concept. All must be removed cleanly rather than translated.
- **Riichi's separate Ron and Tsumo claim buttons collapse to a single Hu/胡 in Changsha.** The win method (self-draw vs discard) is inferred from context (active player + recency of draw/discard), not selected by the user. This is a UI-shape divergence with consequence: any Riichi-shaped "tsumo button shown to the active player only after their draw, ron button shown to non-active players in the claim window" affordance pattern must be redesigned for Changsha.
- **No yaku gate in Changsha.** A complete 4-melds-plus-258-pair hand is a valid Hu regardless of "value." Riichi's mandatory `≥1 han yaku to win` rule has no analog. The Hu validator must NOT reject hands for lacking yaku — it should only check structural validity (4+1 + 258 pair, or any of the Big Win patterns).
- **The autotable upstream TS source is rules-agnostic at the table-layout level** (it just moves tiles into slots per a deal pattern). The Riichi-shaped concepts are in: tile counts (136), deal patterns (`DEALS.FOUR_PLAYER.HANDS` with 12 dice-conditional placement entries), stick groups (riichi 1000-point stick, denomination sticks), slot layouts (`fr("riichi")` per variant), `WINDS` dealType, `Conditions.fives` and `Conditions.points`, and the `GameType` variant enum. These are the surgical strike points for Hicks's codemod — not "rules code" because there isn't any, but rather "Riichi-shaped *artifacts* of the table-setup system."
- **The `GameType` enum's other variants (THREE_PLAYER / BAMBOO / MINEFIELD) are Japanese-mahjong variants** out of scope per the binding directive. Cleanest path is to delete them entirely from the Changsha-native fork; second-cleanest is to keep `FOUR_PLAYER` and rename it to `CHANGSHA` while deleting the others.
- **Wall split is asymmetric (14/14/13/13)** for Changsha because 108 ÷ 4 = 27 tiles per player wall = 13.5 stacks. Per the Phase 5a Default #6 lock, two players get 14-stack walls and two get 13-stack walls. The codemod's wall-builder must encode this asymmetry; the canonical Riichi 17-stacks-per-player is the wrong default.

**Files modified:**
- `.squad/decisions/inbox/vasquez-rules-diff-manifest.md` (new — full diff spec, 14 axes)
- `.squad/agents/vasquez/history.md` (this entry)

**Production code:** Untouched (this is a specification artifact for the codemod, not the codemod itself).

## Architectural Pivot — Phase A SHIPPED (2026-05-13)

**Branch:** stlong/autotable-vendored-pivot (merged to main @ 55d8dfb)
**Timestamp:** 2026-05-13T22:50Z
**Contribution:** Produced authoritative Changsha vs Riichi rules divergence manifest (14 axes, 13 ADD, 31 REMOVE, 9 open Q's), binding specification for Hicks's TS modifications and Ripley's 5-phase pivot plan. Key findings: 108-tile set (no honors), 14/14/13/13 asymmetric wall (no dead wall), claim grammar (Pung/Chow-next-only/Kong/Hu), Small/Big Win scoring, 过胡 per-tile lockout.

## Phase D-tests — Acceptance Test Suite SHIPPED (2026-05-19)

**Branch:** `stlong/phase-b-changsha-scene`
**Timestamp:** 2026-05-19T15:50Z
**Contribution:** 10 acceptance test files (1 fixture + 9 test classes, 1,242
LoC, 44 methods → 66 invocations) defining the executable contract for "fully
playable Changsha" across all 8 rule axes (§1.5 deal, §1.6 chow restriction,
§1.7 claim priority, §1.8 pung→kong, §1.10 258-pair, §1.11 Big Wins, §1.13
banker rotation, §3.6 missed-win lockout) plus an end-to-end synthesis suite.
After running: **62 passed / 0 failed / 4 skipped** — the Changsha rule engine
is **closer to playable than expected**; the 4 skips document the precise
remaining Phase D-backend gaps (诈胡 penalty payment, per-draw 过胡 decay,
13-Orphans Big Win, autotable WS-relay test). Drove `ChangshaGameStateMachine`
directly (pure functional commands) — no live HTTP, no SignalR coupling. All
files compile clean on their own; Bishop's concurrent Autotable WIP currently
doesn't build in isolation but is unrelated to my scope.

**Files added:**
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/AcceptanceFixture.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/DealAndDicePhaseTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/ClaimPriorityTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/PungPromotionToKongTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/ChowFromLeftNeighborTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/HuValidation258Tests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/HuValidationBigWinsTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/BankerRotationTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/MissedWinPenaltyTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/EndToEndPlayableTests.cs`
- `.squad/decisions/inbox/vasquez-phase-d-test-coverage.md` (decision drop)
- `.squad/agents/vasquez/history.md` (this entry)

**Production code:** Untouched. Disjoint from Bishop's Phase D-backend wiring
and Hicks's Phase D-frontend scene scope.

**Key findings flagged to Stephen:**
1. Engine surface is more complete than the brief implied — only 2 small rule
   refinements (诈胡 penalty, per-draw 过胡 decay) remain in the rules layer.
2. The "fully playable" gate is mostly a wiring problem now (autotable WS
   pipe ↔ `IChangshaGameRuntime`), not a rules problem.
3. 13-Orphans is the only un-implemented Big Win; deferred as optional v2.

---

## Phase F — Manual pickup + variant switch + bot tiers acceptance suite (2026-06-04)

**Branch:** `stlong/phase-f-changsha-realism` off `d461726` (Wave 3 Changsha runtime).
**Role this wave:** rule auditor + acceptance test author. Disjoint from Bishop's
production work (already partly shipped locally as untracked Bot/ folder and
ChangshaDomain/StateMachine modifications) and from Hicks's frontend work.

### What I shipped

**1. Rule audit document** — `.squad/decisions/inbox/vasquez-phase-f-rule-audit.md`
   - 12 rule axes covered (dice geometry, break-point semantics, pickup order,
     phase sequence, tile counts, Hu mid-pickup, replacement-tile interaction,
     dealer rotation, missed-win lockout, bot-tier behaviour, variant scoping,
     re-entry/disconnect).
   - Cited sources: MahjongPros (English primary), Baidu (English translation),
     `docs/rules/changsha-spec.md` v1.2, Ripley's Phase F design doc.
   - GAPS FOUND / DEFAULTS LOCKED tables make Stephen-needed product calls
     explicit. Recommended Bishop's design choices verbatim where the design
     already encoded the right answer.
   - Key locks: 2d6 sum 2-12; break counts STACKS (not tiles) from the right
     end of the chosen wall; CCW pickup order from dealer; tile counts
     4+4+4+1+1=14 for dealer / 4+4+4+1=13 for others; no Hu mid-pickup;
     pickup uses wall-front only (no kong-replacement intersection); dealer
     rotation unchanged from Wave 3.

**2. Three failing acceptance test files** — all under
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/`:

   - `ManualPickupAcceptanceTests.cs` (14 facts/theories, ~38 expanded cases):
     dice 2d6 range/determinism, break-point math (Theory × 4 seeds), wall
     containment, BreakPointMarked phase transition, first-round dealer 4-tile
     pickup, CCW pickup order, out-of-turn rejection, wrong-count rejection,
     all three rounds yielding 12 tiles each, single-tile round → 13 each,
     dealer-extra → 14/13/13/13 + AwaitingDiscard, Hu-mid-pickup rejection,
     auto-deal mode regression (must skip pickup), per-viewer privacy mask
     (own hand rotation 1, others 2), and translator pickup-collection emit
     + tombstone semantics.

   - `VariantSwitchAcceptanceTests.cs` (9 facts/theories, ~17 expanded cases):
     variant=changsha binds runtime, variant=four_player does NOT, relay-mode
     bundle forwarding regression, no claim/result/pickup collections in relay
     mode (Theory × 4 variants), default variant = changsha, AutotableConnection
     property surface (Variant/DealMode/BotCount/BotDifficulty/RuntimeMode),
     AutotableRuntimeMode enum members, mixed-mode rejection/lockout, URL
     parameter parsing (Theory × 4 inline queries).

   - `BotEngineAcceptanceTests.cs` (11 facts/theories, ~28 expanded cases):
     Easy = discard-highest-rank + never-Chow + always-Hu; Medium =
     shanten-minimizing discard + claims-Hu-when-offered + respects
     ClaimAdjudicator's Chow-from-next-only filter; Hard = legal-discard
     smoke + missed-win lockout respect; ChangshaBotEngine.Resolve
     case-insensitive (Theory × 7), null/unknown → MediumStrategy; OnPickupCue
     hook smoke (Theory × 3 difficulties); BotPickupDelayMs default 500;
     4-bot full hand completes within 200 steps (Theory × 3 seeds).

### Test posture — reflection-pending pattern

All three files reference Phase-F-pending symbols (`DealMode`, new
`ChangshaPhase` values, `BeginManualDeal`, `TakeTilesFromWall`,
`PickupSeatIndex`, `ChangshaCollectionKinds.Pickup`, `AutotableRuntimeMode`,
`AutotableConnection.Variant`, etc.) via `Assembly.GetType` + `MethodInfo`
reflection. This means **the test assembly always compiles** even when Bishop's
production code lags. Tests fail RED with descriptive
`"Phase F backend not yet shipped — missing X. Bishop owns…"` messages until
Bishop ships each symbol. Critical because Vasquez/Hicks/Bishop share one
branch — if my tests didn't compile, Bishop's CI would be blocked.

### Result: 60 expanded tests, 31 red / 29 green

The 29 currently-green tests are the regression set (dice, break-point math,
relay-mode forwarding, bot-vs-bot harness termination, ChangshaBotEngine
resolver — Bishop has shipped the Bot/ folder locally as untracked). The 31
red tests pin the work Bishop still owes (DealMode toggle, new pickup phases,
TakeTilesFromWall, AutotableConnection Variant/DealMode/RuntimeMode props,
AutotableRuntimeMode enum, BotPickupDelayMs option, etc.).

### Files I did NOT touch (forbidden surface)

- `src/backend/src/**` — all production code untouched (Bishop's surface).
- `src/frontend/**` — Hicks's surface.
- `AcceptanceFixture.cs` — chose to colocate reflection helpers inline in
  each new test file rather than extend the shared fixture; keeps Bishop's
  parallel work uncoupled.
- `.csproj` files — left Bishop's pending `Microsoft.AspNetCore.Hosting`
  global-using untouched; my tests carry an explicit `using` instead.

### Open assumption (low risk)

Ripley's design names the property `state.DealMode`. If Bishop chooses
`state.Mode` or `state.Conditions.DealMode` instead, my reflection lookups
fail with a descriptive message telling him what name to use. Bishop will
either rename to match or my test will get a one-line fix.

---

## Phase G — Bot pickup scheduler + privacy mask acceptance tests (2026-06-11)

**Branch:** `stlong/phase-g-bot-scheduler-lobby` off `main` @ `1e9134a`
**Role this wave:** acceptance test author. Disjoint from Bishop's production
work (bot pickup tick scheduler + privacy filter slot-parse cleanup; both
already shipped on this branch's tip) and from Hicks's frontend work.

### What I shipped

**Two new acceptance test files** — `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/`:

- **`BotPickupSchedulerAcceptanceTests.cs`** (6 facts, 31 assertions): all
  pickup phases bot-driven when seat is bot / chain halts on human seat /
  cursor advances CCW through `BreakPointMarked` → `PickupRound1/2/3` →
  `SingleTilePickup` → `DealerExtra` → `AwaitingDiscard`; `BotPickupDelayMs`
  knob respected (lower bound 13×200×0.5ms / upper bound 13×200×3ms); auto-deal
  mode does NOT trigger the scheduler (regression gate); pending pickup tasks
  cancel cleanly on game teardown (reflection on `_games` + `LifecycleCts`
  cooperative cancel).

- **`PrivacyMaskAcceptanceTests.cs`** (5 facts, 29 assertions): non-`@` slots
  pass through; foreign-seat hand slots masked; own-seat hand slots preserved;
  spectator (`viewerSeat == null`) sees all hands masked; malformed slots
  (multi-`@`, trailing `@`, non-numeric suffix) gracefully passed through
  via `LastIndexOf('@')` + soft-parse semantics — no exceptions, count
  preserved.

**Total: 11 facts, 60 assertions.**

### Contract locks

1. **Bot pickup tick scheduler** — `BotPickupDelayMs` (default 500ms, configurable
   via `IOptions<ChangshaRuntimeOptions>`). The scheduler MUST gate on
   `state.Seats[pickupSeat].IsBot` AND `IsPickupPhase(state.Phase)` AND
   `state.PickupSeatIndex == pickupSeat` under the instance lock before firing,
   so that human seats stall the chain and cancelled lifecycle CTSes drop the
   task silently.

2. **Privacy filter slot-parse** — `FilterEntriesForViewer` uses the
   `LastIndexOf('@')` boundary; legacy `IndexOf('.') ... IndexOf('@')` slice
   parse (which extracted the `handIdx` instead of the seat) is gone.
   Non-`@`, non-`hand.` slots pass through unchanged.

### Test posture — reflection where useful

- `BotPickupSchedulerAcceptanceTests` builds a real `ChangshaGameRuntime` via
  `WebApplicationFactory<Program>` (per-test inline harness so `BotPickupDelayMs`
  is per-test configurable — see line 36 `RuntimeHarness` class). State is read
  via the runtime's `TryGetSnapshot` public API; pickup actions drive through
  `RollDiceAsync` + `TakeTilesFromWallAsync` public methods. Reflection is only
  used for the teardown test (no public `RemoveGame` API yet).

- `PrivacyMaskAcceptanceTests` reaches the private static
  `FilterEntriesForViewer` via reflection. The lookup probes three candidate
  hosts (`AutotableConnectionManager`, `AutotableWsEndpoint`, `PrivacyFilter`)
  so a future refactor into a dedicated `PrivacyFilter` class won't break the
  suite.

### Result: 11/11 GREEN at commit time (no expected red)

Both contracts were ALREADY shipped on the branch tip — Bishop landed the
scheduler (`ChangshaGameRuntime.RunBotPickupAsync` line ~831 + `ScheduleBotIfNeededAsync`
line ~798) and the privacy filter cleanup (`AutotableConnectionManager.FilterEntriesForViewer`
lines 739–795 with `LastIndexOf('@')` parse) before I wrote the tests. The tests
therefore serve as a **regression lock**: any drift in either contract will
turn one or more of these 11 tests red.

### Verification

- `dotnet build src/backend/Mahjong.Autotable.slnx --nologo` → **0/0**.
- `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build --filter "FullyQualifiedName~BotPickupScheduler|FullyQualifiedName~PrivacyMask"` →
  **12/0/0** stable across 3 consecutive runs.
- Full suite: **330/0/9** stable across 3 consecutive runs (no flakes, no
  regressions). The 9 skipped are pre-existing; the 330 passed includes my
  11 new + 319 existing.

### Files I did NOT touch (forbidden surface)

- ANY production code under `src/backend/src/**` (Bishop's surface).
- ANY frontend file (Hicks's surface).
- `AcceptanceFixture.cs` or any existing acceptance test file (additive only).
- `.csproj` files.

### One stability bump

Initial test 3 (`Bot_Pickup_Continues_Through_All_Three_Rounds`) flaked under
full-suite concurrent load — root cause was a race between phase entering
`AwaitingDiscard` and the dealer bot's first auto-discard (BotTurnDelayMs=1
in my test harness). Fix: set `BotTurnDelayMs = 30_000` in test 3's harness so
the post-deal snapshot reads stable hand counts before any discard fires. Test
re-ran clean across 3+ full-suite runs.

### Open assumption (low risk)

Test 6's reflection-based teardown reaches into `_games` (the runtime's
private `ConcurrentDictionary<string, ChangshaGameInstance>`). If Bishop
renames the field or refactors to an alternate registry, test 6 turns red
with the descriptive "Bishop should expose a public RemoveGame/DisposeGame
API" message and Bishop adds a one-line public teardown method. That's an
upgrade path, not a regression.
