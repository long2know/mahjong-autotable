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

## Phase H Wave 2 Scribe Sweep — V2 Rules (2026-05-22)

**Timestamp:** 2026-05-22T20:00Z
**Branch:** `stlong/phase-h-wave-2-v2-rules` (cut from main @ `8ec6cfa`)
**Contribution:** Merged 4-file Phase H Wave 2 inbox + 1 newly-written coordinator wiring memo into canonical `.squad/decisions.md` as a single `## Phase H Wave 2 — V2 Rules` section with five subsections (V2 rules / V2 tests / Frontend polish / Wiring fix / Gate result + parking lot). Wrote 1 new coordinator memo documenting the `AllPatterns` carrier-field pattern for detector→state→scoring boundaries (commit `ba622e4`). Merged 16 new tests + 6 unskips across 5 test files, delivering 17 net new passes vs Wave 1 baseline (340 → 357). Captured 7 Phase I polish parking lot ideas + open questions re: NineTerminals semantics, RobbingKong bonus, pure-NineTerminals rarity, concealed-kong inertness.

**Four-agent fan-out + coordinator fix:**

1. **Bishop** (`bishop-phase-h-wave-2.md`, commits `a6e876d` → `9784604` → `de6f721` → `16b7b39`): NineTerminals pattern + AllPatterns list (detector→scoring carrier) + RobbingKong claim window + runtime wiring. Four production-code commits, 0 test changes. Contracts locked: `WinPattern.NineTerminals`, `WinDetectionResult.AllPatterns`, `ScoringService.CalculateScore(…, int)` 4-arg overload, `WinResult.IsRobbedKong`, `ChangshaClaimWindow.IsKongRobbing`, `ChangshaGameRuntime` added-kong window dispatch. Coordination note: Vasquez WIP test file had build error (duplicate method names) mid-Wave-2; Bishop stashed + restored for Vasquez to fix. Open question: NineTerminals structural-validity (Vasquez's binding test = rank-bounds-only; Ripley spec = valid structure; Bishop adopted binding-test precedent, awaits counter-confirmation).

2. **Hicks** (`hicks-phase-h-wave-2.md`, commit `257faa5`): Frontend UI enrichment — stacked-pattern chips (color-coded: purple/brown/blue/gold), RobbingKong badge (red-on-glow), NineTerminals friendly label (九幺). Defensive wire contract (all-optional fields) ships green before Bishop backend wires the fields. Bundle hash rollover: JS `c97ea9e9.js` → `74e239e6.js`, CSS `96cb3b60.css` → `674133df.css`. Wave 1 hashes pruned. Deferred: move-log sidebar (no current turn-history UI exists; separate Phase I scope). Captured 7 polish ideas for Phase I: move-log, score-multiplier breakdown, 九幺 3D highlight, RobbingKong audio, pattern tooltips, self-draw badge, handCount progress pill.

3. **Vasquez** (`vasquez-phase-h-wave-2.md`, commits `adf3ca8` → `c9e9b29` → `046fc8e`): 16 new tests + 6 unskips (22 total facts) across `WinPatternTests` (3 unskip), `HuValidationBigWinsTests` (1 new), `EdgeCaseTests` (2 unskip), `RobbingKongAcceptanceTests` (5 new), `StackedBigWinScoringTests` (6 new). Reflection-defensive symbol probes enable standalone compilation; Hu-only scenarios isolate test state from dealer RNG. Coverage: NineTerminals rank-bounds, AllPatterns population, kong-robbing window, claim adjudication, stacked-multiplier contract (1x/2x/3x clamp, small-win immunity). One RED during Wave 2: `MultipleBigWinPatterns_ScoresStack_DeferredToV2` pinned Score() wiring gap (see Coordinator entry). Methodology: acceptance + edge facts from multiple angles catch wiring boundaries.

4. **Coordinator (Ripley, acting via Hicks/Vasquez authority)** — New memo `coordinator-phase-h-wave-2-wiring.md`, commit `ba622e4`: Discovered via Vasquez's test RED that `ChangshaGameStateMachine.Score()` called 3-arg `CalculateScore` instead of 4-arg. Root cause: detector populates `WinDetectionResult.AllPatterns`, but state machine never threaded it to scoring. Solution: add `WinResult.AllPatterns` carrier field (mirrors detector), copy at Hu time, read in `Score()`. Bonus: wire WebSocket emissions to emit `AllPatterns` + `IsRobbedKong` for Hicks's UI. Design pattern captured for future multi-phase architectures: detector→state→scoring boundaries require explicit carriers (not re-detector runs). Result: Vasquez's RED test greens, full suite reaches **357/0/1**.

**Gate result:** Phase H Wave 1 baseline **340/0/7** → Phase H Wave 2 **357 passed / 0 failed / 1 skipped** (+17 net passes; 7 skips→1 skip; only `AutotableWsRelayTests.Update_IsIsolated_PerGameId` remains, unrelated WS isolation deferred).

**Inbox files:** All 5 memos present and read (4 agent + 1 coordinator wiring new-write); not deleted (`.squad/decisions/inbox/` is gitignored — local-only primary sources per standing instruction).

**Notable:** Coordinator memo introduced the "detector→state→scoring carrier" pattern—a reusable design for multi-phase rule engines where detectors enrich contracts but consumers run downstream. Vasquez's reflection-defensive tests (symbol probes with named-contract exceptions) enabled seamless agent coordination across test→production boundaries, catching wiring gaps early. Phase I parking lot captured 7 UI polish ideas + 4 open questions for Wave 3 (NineTerminals semantics, RobbingKong bonus, pure-NineTerminals rarity, concealed-kong inertness).

**Branch ready for PR against `main`.** All four agents' Wave 2 work + coordinator fix captured in canonical decisions.md.

## Phase I Wave 1 Scribe Sweep — Special-context wins + UX polish (2026-05-22)

**Timestamp:** 2026-05-22T21:15Z  
**Branch:** `stlong/phase-i-wave-1-special-wins-ux` (cut from main @ same base as Wave 2)  
**Contribution:** Merged 4-file Phase I Wave 1 inbox + 1 newly-written coordinator translator-gap memo into canonical `.squad/decisions.md` as a single `## Phase I Wave 1 — Special-context wins + UX polish` section with seven subsections. Wrote 1 new coordinator memo (`coordinator-phase-i-wave-1-translator-gap.md`, commit `85c5328`) documenting the SignalR ↔ bundle WS path divergence and the multi-phase carrier pattern applied to `HandResultEntry`. Merged 17 new tests across Vasquez's two suites (9 acceptance + 8 unit), delivering +17 net passes vs Phase H Wave 2 baseline (357 → 374).

**Four-agent orchestration:**

1. **Bishop** (`bishop-phase-i-wave-1.md`, commits `afd59b9` → `7509685` → `9e0439c` → `419ba7a` + test coordination `0117a30`): Five contextual Big Win patterns (天和/地和/海底捞月/河底捞鱼/杠上开花) as new enum values in `WinPattern`. `WinContext` sealed record (5 bool flags) optional on detector. `LastDrawWasKongReplacement` state-flag lifecycle across game-state mutations. State-machine wiring at two Hu-detection sites (`DeclareSelfDrawWin`, `ResolveHuClaim`). WS wire emission for new patterns (camelCase). Cross-lane test coordination: test `HuValidation258` scenario is now canonical EarthlyHand fixture; Bishop owned the assertion update (`0117a30`) due to his rule change causing the drift.

2. **Vasquez** (`vasquez-phase-i-wave-1.md`, commits `b6a512e` → `cd95b5b`): Two test suites — 9-fact acceptance (`SpecialContextWinsTests` driving state machine end-to-end) + 3 facts + 1 Theory ×5 unit layer (`WinPatternTests` pinning contracts). Reflection-defensive symbol probes decouple test compilation from Bishop's commit order. Methodology note: direct state-machine drive (not Runtime) mirrors Wave 2 precedent (`RobbingKongAcceptanceTests`). Coverage: all 5 contextual patterns fire correctly under gating conditions; mutual exclusivity verified; negative cases prevent regressions.

3. **Hicks** (`hicks-phase-i-wave-1.md`, commit `f91c95e`): Score-multiplier breakdown modal + streaming move-log sidebar. Result-modal reads optional `scoreResult` + `allPatterns[]` from wire; move-log subscribes to existing game-state collections. **Critical discovery:** Phase H Wave 2 chip strip was dead code — `PATTERN_LABELS` keyed by PascalCase but `WinPatternToWire` emits camelCase. Phase I Wave 1 fixes via `normalizePatternKey()` + camelCase-first lookup. **Build-command correction:** Wave 2 doc wrongly said `parcel build src/index.ts src/index.html`; `src/index.html` doesn't exist. Corrected to `parcel build index.html …` (TS entry discovered via `<script>` tag; emits single hashed JS). Bundle hashes: JS `74e239e6.js` → `4ce16ecc.js`, CSS `674133df.css` → `8ade01c3.css`. Wave 2 hashes pruned.

4. **Coordinator (acting)** — New memo `coordinator-phase-i-wave-1-translator-gap.md`, commit `85c5328`: Bishop's Phase H Wave 2 detector emits `winResult.allPatterns` + `isRobbedKong` on SignalR path; Hicks's Phase I Wave 1 UI ready to render both. But `ChangshaToAutotableTranslator.BuildHandResult` (bundle WS path) never copied those nested objects — two paths diverged. **Fix:** Extend `HandResultEntry` to carry `WinResult?` + `ScoreResult?`; `BuildHandResult` populates both. **Pattern captured:** Multi-phase architectures need explicit carriers at every boundary (detector → state → scorer → translator) — don't re-run detectors downstream. This pattern emerged from Phase H Wave 2's `AllPatterns` carrier; now applied to translator-layer bridging.

**Gate result:** Phase H Wave 2 baseline **357/0/1** → Phase I Wave 1 final **374 passed / 0 failed / 1 skipped** (+17 net passes from Vasquez's 12 acceptance + 8 unit tests; 1 test reclassified but still passes).

**Inbox files:** All 5 memos present and read (4 agent + 1 coordinator translator-gap new-write); not deleted (`.squad/decisions/inbox/` is gitignored — local-only primary sources per standing instruction).

**Notable:** 
- **Regression prevention:** Hicks's discovery that Phase H Wave 2 chip strip was dead code (PascalCase ↔ camelCase mismatch) surfaced a hidden wire-enum risk. Added to decision memo as regression-prevention rule: always test PascalCase ↔ camelCase keys when enums cross language boundaries (C# → TypeScript). Recommend Phase J adds translator contract test.
- **Build command fix:** Hicks's correction to the parcel build command (removing non-existent `src/index.html`) is a critical invariant for future frontend rebuilds — easy to forget, easy to produce duplicate artifacts. Surfaced prominently in decisions.md build-invariants section.
- **Translator divergence pattern:** Coordinator memo generalized the "carrier at boundaries" pattern from Phase H Wave 2 (`AllPatterns` through state-machine) to Phase I Wave 1 (detector/scorer outputs through translator to WS wire). Multi-phase detectors should document all carriers upfront to prevent future gaps.

**Branch ready for PR against `main`.** All four agents' Phase I Wave 1 work + coordinator translator fix captured in canonical decisions.md.

## Phase I Wave 2 Scribe Sweep — Persistence hydration + bot contextual coverage + UI polish (2026-05-22)

**Timestamp:** 2026-05-22T23:15Z
**Branch:** `stlong/phase-i-wave-2-hydration-bot-ctx` (all commits pushed; ready for PR)
**Contribution:** Merged 3-file Phase I Wave 2 inbox into canonical `.squad/decisions.md` as a single `## Phase I Wave 2 — Persistence hydration + bot contextual coverage + UI polish` section covering Bishop's hydration + Hicks's UI polish + Vasquez's two test suites (Phase A bot-pipeline + Phase B hydration round-trip). Documented **observation-race WinObserver pattern** (StateChanged subscription) for future post-win tests + **modal z-index trap** (1060>1050) + **AppDbContext spelling correction** (not `MahjongDbContext`). Wave 2 result: **383/0/1 tests** (was 374/0/1 at Phase I Wave 1 → +9 net passes). Bundle rolled: JS `4ce16ecc.js` → `e6653bd3.js`, CSS `8ade01c3.css` → `60fe83d8.css`. All inbox files remain in place per standing instruction (`.squad/decisions/inbox/` is gitignored — local-only primary sources).

**Notable:**
- **Observation-race pattern:** The `WinObserver` IDisposable pattern (subscribe to `StateChanged` fires in `PersistSnapshotAsync` *before* next hand starts) is a reusable solution for tests needing post-win state inspection. Pinned for future test framework improvements.
- **CSS rule-of-thumb:** Modal tooltips need 10-point z-index buffer above Bootstrap modal base (1050). Hicks's discovery that `.pattern-tooltip` requires `z-index: 1060` + `position: relative` on parent is a design constraint worth surfacing on future tooltip work.
- **DbContext spelling:** Real name is `AppDbContext`, not `MahjongDbContext` — upstream directive had the typo. Future agents should pin this to avoid repeating it.
- **Bot WinContext:** Phase A tests confirm bot doesn't need explicit passthrough; context derives inside state machine (correct separation). No bug surfaced.

**Branch ready for PR against `main`.** All three agents' Phase I Wave 2 work captured in canonical decisions.md.

## Phase I Wave 3 Scribe Sweep — Multi-game vertical slice + zero skips (2026-05-23)

**Timestamp:** 2026-05-23 (date TBD)  
**Branch:** `stlong/phase-i-wave-3-multigame-bot-strength` (all commits pushed)  
**Contribution:** Merged 3-file Phase I Wave 3 inbox into canonical `.squad/decisions.md` as a single `## Phase I Wave 3 — Multi-game vertical slice + zero skips` section covering Bishop's multi-game routing + Hicks's lobby Game ID UI + Vasquez's ten tests (9 new + 1 unskip). Documented **validation rules verbatim** (length 64, control chars, case-sensitive, fallback chain) + **Parcel `<input type="text">` stripping gotcha** + **per-game routing source priority** + **cross-lane assertion-flip protocol** + **whitespace-only quirk** for future waves. Phase I Wave 3 result: **393 / 0 / 0 tests** (was 383/0/1 at Phase I Wave 2 → +10 net passes + **first zero-skip wave this session**). Bundle rolled: JS `e6653bd3.js` → `49eb3789.js`, CSS `60fe83d8.css` → `af973ea2.css`; Bootstrap hash unchanged. All inbox files remain in place per standing instruction (`.squad/decisions/inbox/` is gitignored — local-only primary sources).

**Four-agent coordination notes:**

1. **Bishop** (multi-game routing + hydration filter): Removed `DefaultGameId` coercion at two sites; established validated fallback chain (JOIN.gameId → ?gameId query → DefaultGameId). Closed Wave 2 open Q on `WallExhausted` hydration (now excluded). **Key invariant pinned:** `AutotableConnectionManager` lives at bottom of `AutotableWsEndpoint.cs`, not separate file (prevents phantom-file searches in future waves).

2. **Hicks** (lobby Game ID input): New row above Connect/Disconnect block with 64-char pattern validation + URL persistence via `history.replaceState`. Connected-state hides input, shows `Game: <id>` display. **Build-time gotcha discovered:** Parcel strips `type="text"` defaults from `<input>` — CSS selector anchoring must use ID/scoped class, not attribute selector. This is a critical invariant for future frontend rebuilds (easy to miss, easy to break input styling post-build).

3. **Vasquez** (ten tests, zero skips): Unskipped `Update_IsIsolated_PerGameId`; wrote 9 new tests across MultiGameRoutingTests.cs + HydrationOnStartupTests.cs. **Protocol pinned:** When production rules change, test owner flips assertions in same wave (not future wave) — captures post-fix reality, not pre-fix bug. **Whitespace-only quirk:** `TryNormalizeGameId(null)` returns `true` with `normalized = null`, enabling clean fallback-chain resolution at JOIN time.

**Notable observations:**
- **Protocol-design subsection:** Validated fallback chain (JOIN.gameId → query → default) + source priority + control-char/length rejection + case-sensitivity (Ordinal) all documented verbatim as contracts for future multi-region routing or rule-tightening waves.
- **Cross-agent coordination:** Bishop closes Wave 2 open Q (WallExhausted filter); Hicks discovers Parcel build-time behavior (type stripping); Vasquez flips cross-lane test assertion (protocol shift). All three findings surfaced in decisions.md for team reuse.
- **Zero-skips milestone:** First wave in this session with no skipped tests (357 → 374 → 383 all had 1 skip; now 393 with 0). Milestone achievement worth noting for velocity metrics.

**Open questions for Phase I Wave 4:** `_bindingLock` per-game profiling, bot shanter estimator improvements, Hard-tier bot WinContext audit, Game ID hot-seat swap UI (Disconnect → edit → Connect today, Move button deferred).

**Branch ready for PR against `main`.** All three agents' Phase I Wave 3 work + Scribe sweep captured in canonical decisions.md.


## Phase I Wave 4 Scribe Sweep — Proper shanten + spectator + strength tests (2026-05-24)

**Timestamp:** 2026-05-24 (date TBD)
**Branch:** `stlong/phase-i-wave-4-bot-strength-spectator` (all commits pushed; ready for PR)
**Contribution:** Merged 4-file Phase I Wave 4 inbox into canonical `.squad/decisions.md` as a single `## Phase I Wave 4 — Proper shanten + spectator + strength tests` section covering Bishop's rigorous shanten counter + spectator seat backend, Hicks's lobby Spectate UI, Vasquez's 9 tests (3 bot strength + 6 spectator validation), and Coordinator's dead-code resolution (shanten tie-breaker wiring in HardStrategy). Documented **monotonicity property** (loose-tile discard invariant), **benchmark verification** (six hand shapes, < 1 ms each), **Attempt 1 pathology** (seed 40595 4000-step timeout with shanten-primary ordering), **Attempt 2 resolution** (shanten as tie-breaker, minimal change). Phase I Wave 4 result: **402 / 0 / 0 tests** (was 393/0/0 at Phase I Wave 3 → +9 net passes, zero-skip streak 2). Bundle rolled: JS `49eb3789.js` → `c93fbb44.js`, CSS `af973ea2.css` → `3f21032c.css`; Bootstrap hash unchanged. All inbox files remain in place per standing instruction (`.squad/decisions/inbox/` is gitignored — local-only primary sources).

**Four-agent coordination notes:**

1. **Bishop** (shanten counter + spectator backend): Replaced `MinShantenToHu` with rigorous backtracking (Standard path) + SevenPairs formula (two paths, return min). Monotonicity property ensures loose-tile discard never increases shanten. Smoke bench confirmed correctness on six hand shapes (< 1 ms per call). Added `?seat=-1` spectator surface, widened botCount cap to 4 for spectators only, auto-deal one-shot on NEW/JOIN when seat=-1 AND botCount=4. **Key invariant pinned:** ParseSeat range `>= -1 and <= 3` (was `>= 0 and <= 3`); spectator → ViewerSeat `null` (existing privacy filter falls through). Initial bench caught formula bug (extraneous `+1-pair` term) before commit — caught via monotonicity verification table.

2. **Hicks** (lobby Spectate UI): New `Seat` fieldset (Auto / 0..3 / Spectate) above Bot difficulty. Spectate selection unlocks 4-bot slot (disabled for non-spectators) + pre-selects 4 on flip from non-spectator. Green "Spectating" pill on connected state. URL `?seat=-1` persisted via `history.replaceState`. Spectator mode hides Take-seat / Leave-seat / Claim buttons / Deal (server auto-deals) / Pickup HUD; bot banner stays (names seating). **Critical build invariant reaffirmed:** Parcel strips `type="text"` from `<input>` — anchor CSS on ID/scoped class, not attribute selector (surfaced in Wave 3, applies to new `.spectator-pill`).

3. **Vasquez** (9 tests, no production edits): 3 `BotStrengthTests.cs` (Hard beats Medium ≥ 0.9×, Medium beats Easy ≥ 0.9×, Hard no-draw regression) + 6 `SpectatorModeTests.cs` (connects without seat, receives full snapshot, no turn prompts, 4-bot auto-deal within 3s, 3-bot no auto-deal, seat=0 botCount=4 defensive assertion). Audit flagged dead code: Bishop's rigorous counter delivered but never consumed by HardStrategy. Fixed by Coordinator.

4. **Coordinator** (dead-code resolution): Vasquez audit surfaced `MinShantenToHu` was orphaned. Attempt 1 (shanten as primary key) broke at seed 40595 with 4000-step timeout — pathological claim-chain loop (state-machine edge case, not bot bug; deferred to Phase J). Attempt 2 shipped: `OrderBy(ComputeDiscardScore).ThenBy(shantenByLogical)` — keep-score primary (Phase F baseline), shanten tie-breaker (minimum change). Result 402/0/0. Rationale: Changsha's Big Win mix favors defensive/contextual plays; keep-score was already stronger than shanten-greedy. Promoting shanten demands re-tuning every Hard heuristic; tie-breaker exercises counter in production without disturbing baseline.

**Notable observations:**
- **Shanten-as-tie-breaker pattern:** When a rigorous estimator (counter, evaluator, heuristic) is delivered but not yet integrated, shipping it as a tie-breaker (not primary sort key) is the minimal, lowest-risk wiring that exercises it in production. Allows future phases to promote it to primary after understanding its interaction surface more deeply.
- **Benchmark-caught bug:** Initial formula error (`+1-pair` term) inflated shanten by 1 for any decomposition without a pair. Monotonicity verification table (discard → shanten delta) caught it immediately; removing the term restored canonical values across all six test cases + kept gate at 402/0/0. Recommends running perf + property-based benches on backtracking algorithms during initial implementation.
- **Dead-code audit pattern:** Phase I Wave 4 Vasquez audit (`MinShantenToHu` never called) triggered Coordinator memo and resolution in same wave. When audit surfaces unused production code, surface it in the same wave's decisions.md + resolution memo, don't defer. Enables immediate re-integration or cleanup decision.
- **Zero-skip streak milestone:** Wave 4 maintains zero-skips (357 → 374 → 383 → 393 → 402). Consecutive zero-skip waves indicate test suite stability + low flake risk.

**Phase J Wave 1 backlog:** Diagnose seed 40595 4000-step pathology (trace harness needed), promote shanten to primary key (demands re-tuning keep-score weights), wire shanten into OnDiscardOpportunity (claim evaluation), hot-seat swap UI ("Move" button), NineTerminals strict-vs-loose semantics (pending Stephen's call).

**Branch ready for PR against `main`.** All four agents' Phase I Wave 4 work + Scribe sweep captured in canonical decisions.md.

## Phase J Wave 1 Scribe Sweep — Shanten claim gate + hot-seat swap (2026-05-25)

**Timestamp:** 2026-05-25 (final sweep date)  
**Branch:** `stlong/phase-j-wave-1-hardening` (all commits pushed; ready for PR)  
**Contribution:** Merged 3-file Phase J Wave 1 inbox into canonical `.squad/decisions.md` as a single `## Phase J Wave 1 — Shanten claim gate + hot-seat swap + spectator camera lock` section covering Bishop's shanten-aware claim gate + wall-exhaustion fast-path deferral, Hicks's Move button + spectator camera lock, and Vasquez's 7 new test facts (4 claim evaluator + 3 hot-seat swap). Documented **Kong-over-Pung dead-code reality** (mathematically unreachable via realistic adjudicator output; reframed Fact 4 to Pung-vs-Chow instead), **Task 2 deferral rationale** (state machine already short-circuits on empty wall; adding runtime check would be inert), **autotable disconnect seat-release gap** (SignalR Hub only; Bundle UI works around by disabling current seat). Phase J Wave 1 result: **409 / 0 / 0 tests** (was 402/0/0 at Phase I Wave 4 → +7 net passes, zero-skip streak 3). Bundle rolled: JS `c93fbb44.js` → `214d524e.js`, CSS `3f21032c.css` → `884bb475.css`; Bootstrap hash unchanged. All inbox files remain in place per standing instruction (`.squad/decisions/inbox/` is gitignored — local-only primary sources).

**Three-agent coordination notes:**

1. **Bishop** (shanten claim gate + Task 2 deferral): Wired `MinShantenToHu` into `DecideClaimPhase` as strict-shanten-drop gate for non-Hu opportunities. Contract: Hu unconditional (fast-path); non-Hu only if post-claim shanten < pre-claim. Tie-breaker rank Hu > Kong > Pung > Chow. Helpers ShantenAfterPungClaim / ShantenAfterExposedKongClaim / ShantenAfterChowClaim simulate post-claim hand; ClaimAcceptanceRank encodes ordering. Chow simulation mirrors `RemoveChowTilesByLowestPattern` first-viable-pattern selection (lowest-rank-first) so gate decision matches runtime play. Resolves Phase I Wave 4 Vasquez audit (dead code). Deferred Task 2 (wall-exhaustion fast-path) with clear reasoning: `ChangshaGameStateMachine.AdvanceToNextPlayer:1076-1087` already checks `Wall.Count == 0` before setting AwaitingDiscard; both call sites (Discard / PassClaim) route to WallExhausted on empty wall. Adding duplicate check in runtime driver would be functionally inert + risk dropping wall-exhausted event broadcast. Per wave brief, SKIP.

2. **Hicks** (Move button + camera lock): New sidebar row with Move button + seat picker (Seat 0..3 + Spectate). Visibility: `connected() && match.get(0) === null` (disappears post-Deal). Picker per-option disabled state: current seat + occupied seats. Soft reconnect: `history.replaceState` rewrite `?seat=`, clear local seats entry (avoid stale reapply), `client.disconnect()`. Auto-reconnect picks up new seat via existing `buildWsUrl()`; body class + spectator pill sync automatically. Spectator camera lock: one-liner in `world.ts` initializing `seat` field from `readSpectatorFromUrl()` instead of hard-coded 0, eliminating seat-0 first-person flash. `main-view.ts` untouched (existing `fromTop` branch handles null seat already). No orbit-controls to disable. Files: `index.html` / `game-ui.ts` / `style.css` / `world.ts` + bundle roll.

3. **Vasquez** (7 new tests, zero skips): Two suites — `ClaimEvaluatorTests.cs` (4 facts pinning Bishop's gate) + `HotSeatSwapTests.cs` (3 facts on swap surface). All facts reflection-defensive (symbol probes stay compile-stable across refactors). Claim facts: refuse-on-shanten-rise (SevenPairs-candidate hand) / accept-on-shanten-drop (chow-partial-rich) / unconditional-Hu / tie-breaker-rank (reframed Pung-vs-Chow per Kong dead-code discovery). Hot-seat facts: player↔player binding swap preserves runtimeGameId / player→spectator "does-not-claim-seat" (not "frees" — disconnect doesn't call HandleDisconnectAsync) / spectator→player binds-seat. All 7 run unconditionally (no Skip).

**Notable observations:**
- **Kong-over-Pung dead-code discovery:** Shanten counter treats any concealed 3-of-a-kind as complete pung. Kong from discard moves that "group" to declared meld (zero net gain); Pung removes 2 (leaves dangler, usually worse shanten). So both Kong and Pung cannot strictly drop shanten simultaneously on same tile. Bishop's `ClaimAcceptanceRank` lift (Kong=3 > Pung=2) is defensible defence-in-depth (matches runtime's CCW preference) but mathematically unreachable. Vasquez discovered this during Fact 4 construction, reframed to Pung-vs-Chow (both drop shanten, rank decides). Pattern: realistic-adjudicator-output verification early in test-first design catches theoretical-but-unreachable code paths.
- **Autotable WS disconnect gap:** Only SignalR Hub path calls `HandleDisconnectAsync` to release runtime seats. Autotable path leaves seats occupied. Bundle UI workaround: disable current seat in picker. Recommendation: Phase J Wave 2 brief if seat-release becomes priority (larger refactor impact).
- **Task 2 deferral discipline:** Wave brief criterion ("if you can't make the change cleanly without risk to existing test behaviour, SKIP THIS TASK") applied. State-machine already has guard; runtime guard would be inert. Rather than add cosmetic code + risk event broadcast change, explicitly document deferral + move to backlog. Clear reasoning in decisions.md prevents future "why wasn't this done?" questions.
- **Zero-skip streak (3 waves):** I-W3, I-W4, J-W1 all 0 skips (357 → 374 → 383 → 393 → 402 → 409). Consecutive streak indicates stable test suite + low regression risk across bot logic + UI + protocol changes.

**Open questions for Phase J Wave 2:** Autotable WS seat-release (gap documented), HardStrategy.OnTurnStart Win-context audit, i18n AllPatterns ordering, NineTerminals semantics, per-game _bindingLock profiling, Seed 40595 4000-step pathology (from Phase I Wave 4 backlog).

**Branch ready for PR against `main`.** All three agents' Phase J Wave 1 work + Scribe sweep captured in canonical decisions.md.
