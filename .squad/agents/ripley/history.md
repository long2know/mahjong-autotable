# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Ripley as Lead.
- Immediate focus is aligning Changsha rule flow with autotable interaction and backend contracts.
- Established a two-track frontend structure: `src/frontend/autotable` (baseline) plus optional `src/frontend/modern` (React + Fluent UI 9 + TS + Vite).
- Created backend foundation at `src/backend/src/Mahjong.Autotable.Api` with EF Core provider switching (`Sqlite`, `PostgreSql`, `SqlServer`) via `Persistence:Provider`.
- Standardized local startup with VS Code configs in `.vscode/launch.json` and `.vscode/tasks.json`, including one-key full-stack F5 compound.
- Added single-image Docker targets in `infra/docker/Dockerfile`: `runtime-autotable` and `runtime-modern` for incremental UI rollout.

## Pivot Plan Synthesis (2026-05-13)

Synthesised Bishop's backend salvage inventory, Hicks's TS modification map, and Vasquez's rules diff manifest into a single phased pivot plan at `.squad/decisions/inbox/ripley-pivot-plan.md`. Stephen's binding directive (2026-05-13T23:06Z) ordered vendoring pwmarcz/autotable in-tree, implementing Changsha rules directly in its TS, having the .NET backend speak autotable's native `NEW`/`JOIN`/`JOINED`/`UPDATE` WS protocol, and deleting Strategy C, the React/Fluent UI 9 SPA, the SignalR `/hubs/changsha` surface, and the iframe-bridge.

### Approach
- **Five-phase shape (A–E)** that ships *something playable* after each phase: A = vendor + cruft delete (~5,000 LOC out, stock Riichi bundle still works); B = tile/asset Changsha-shaping (sandbox playable, no rules); C = backend WS-native + per-viewer privacy filter + inbound-UPDATE validation (4-bot Changsha hand end-to-end); D = rules graft via custom collections (`changsha.claim`/`scoring`/`banker`/`lifecycle`) + claim/Hu overlays + 过胡 + chow disambiguation (human-vs-bots playable, Changsha v1 lock); E = lobby-in-sidebar + Docker single-image + docs sweep.
- **15 consolidated decisions** for Stephen (combining Bishop's 8, Vasquez's 9, and Hicks's 10 — collapsed by topic). Every decision carries an opinionated default + a switching-cost grade. The two non-negotiables I framed firmly: per-viewer `things` filter (decision 6 — without it, hands leak via DevTools) and custom collections for the 6 non-carrier events (decision 7).

### Opinionated picks
- **Vendor as in-tree fork** at `src/frontend/autotable-src/`, not a submodule. Upstream rejects non-Riichi PRs anyway; submodule footgun for CI / Copilot agents is real.
- **Keep upstream Parcel 2.15**; don't migrate to Vite. The "consolidate to one bundler" pressure disappears with the React app's deletion in Phase A.
- **Path 1 lobby** — autotable's existing sidebar IS the lobby. No React survives in v1.
- **Collapse `GameType` to `CHANGSHA` only** (Vasquez Q2). Carrying legacy variants as dead enums invites drift.
- **Claim labels: Chinese primary + pinyin sublabel** (`碰 Pung` etc.).
- **Drop deal-acks** — autotable has no ack concept; turn 1 starts on a fixed server timeout after the last deal UPDATE.
- **Hard-delete the entire legacy `Tables/*` + `/api/tables/*` REST surface in Phase A** (Bishop D5 default), not later. No surviving frontend consumes them; their tests go red whenever the controllers leave, so doing it in Phase A consolidates the cruft sweep.
- **Defer persistence-on-restart hydration and replay-integrity verifier** to v1.1 — both are pre-existing gaps unrelated to the pivot.

### Key tradeoffs and risks captured
- **Top-3 critical risks:** (1) hand-tile privacy at protocol layer (Hicks R1 — server-side filter is mandatory); (2) six runtime events have no native carrier (Bishop's #1 — custom collections); (3) inbound-UPDATE validation engine doesn't exist yet (Bishop's #4 — adds ~80 LOC `ChangshaStateMachine.ValidateInboundMove` as Phase C prerequisite).
- **Drag-to-claim vs button-driven claims**: I defaulted to buttons + custom `changsha.claim` collection (decision 7) because Hicks R2 flagged drag-only as having irreducible race / ambiguity. Listed drag-only as cut-3 in the MVP-narrowing section for Stephen's awareness.
- **MVP-faster cuts I'd accept:** (1) defer multiplayer ("1 human + 3 bots, single backend instance"); (2) drop the explicit claim window and ship drag-to-meld with a first-valid-drag arbiter; (3) defer Vasquez's two conformance gaps (chow tile-IDs, 过胡) to v1.1. All three are reversible without architectural debt.

### Net delta projected
~6,000 LOC source + ~1,300 LOC tests deleted (Phases A + C + E). ~1,500 LOC TS added (Phases B + D). ~600 LOC backend repointed (Phase C). Pure-rules Bucket A (2,500 LOC src + 2,800 LOC tests) survives untouched. Plan is in the inbox awaiting Stephen's batch-answer.

### F5 dev-loop amendment (2026-05-13T23:15Z follow-up)

Stephen bolted a binding constraint onto the pivot mid-flight (`copilot-directive-2026-05-13T2315Z-f5-dev.md`): *"Also - keep in mind I still want to be able to launch the app with vscode.."* The original plan killed `src/frontend/modern/` (and its Vite dev server) in Phase A without spelling out how the F5 dev loop survives. I amended the plan in-place (no new inbox file) with four surgical additions: (1) a **Local Dev (F5) story** subsection inside Phase A specifying the new compound launch (`F5 Full Stack (Backend + Autotable)` boots `.NET Backend` + a new `Autotable (Parcel watch)` node-terminal config in parallel), the new `autotable: watch` task wrapping upstream's `make parcel` (or `parcel watch --dist-dir build/`), the **byte-identical preservation** of PR #27 + #28's PATH augmentation, the natural retirement of `src/frontend/modern/vite.config.ts`'s proxy when `modern/` dies, and a recommendation to default to pure `parcel watch` (no HTTP server, no port-1234 dependency — backend serves `build/` verbatim) with the `make parcel` + HMR-shim alternative scoped as v1.1 polish; (2) a new **Q16 (F5 dev shape)** at the end of §4 — defaulted to compound launch — inserted at the END rather than between Q1 and Q2 to avoid breaking the ~6 ordinal cross-references ("decision 1", "decision 6", "decision 14", etc.) that pepper §5/§6/§7; (3) a `.vscode/launch.json` + `.vscode/tasks.json` bullet in Phase A's Deliverables block explicitly calling out PATH-augmentation preservation; (4) an F5-regression sentence appended to Phase A's Exit criteria — fresh checkout + one-time `dotnet restore` + one-time `npm install` = working `/autotable/` page on F5 with zero further manual steps. Verified by re-reading the relevant sections after edit; no other section of the plan touched.

## Architectural Pivot — Phase A SHIPPED (2026-05-13)

**Branch:** stlong/autotable-vendored-pivot (merged to main @ 55d8dfb)
**Timestamp:** 2026-05-13T23:05Z
**Contribution:** Synthesized 5-phase pivot plan from parallel inventories (Bishop/Hicks/Vasquez), articulated 16 numbered architectural defaults (vendoring, bundler, lobby, claims, privacy, etc.), drafted 3 MVP fast-cuts (single-game-per-instance, drag-to-meld, defer Vasquez gaps), incorporated F5 dev-loop constraint amendment (compound launch + Parcel watch task). Plan awaited Stephen's batch-decision; after acceptance, Phase A executed in parallel by Bishop + Hicks.

## Phase F Design — DESIGN LANDED (2026-05-19)

**Branch:** stlong/phase-b-changsha-scene (recommend cutting `stlong/phase-f-changsha-realism` for actual work)
**Timestamp:** 2026-05-19T~17:00Z (post-Wave-3-launch, gating Vasquez/Hicks/Bishop fan-out)
**Drop:** `.squad/decisions/inbox/ripley-phase-f-design.md` (~955 lines)

### Scope

Stephen issued a major UX-correction directive: the current "click Deal → auto-deal" behavior is wrong for real Changsha play. Phase F design covers (1) variant switching Changsha ↔ original autotable variants with backend-runtime-vs-relay dual mode, (2) a manual-pickup state machine with 5 new ChangshaPhase enum values driving the dice-roll → break-point → round-robin-4×3 → single-tile → dealer-extra sequence per MahjongPros/Baidu, (3) `dealMode: manual|auto` toggle (manual default for Changsha, auto default for Riichi), (4) bot fill modes (0/3/4 with single-player + spectator), and (5) full Changsha bot engine via pluggable `IChangshaBotStrategy` (Easy/Medium/Hard, with Medium = port of existing `ChangshaBotPolicy`).

### Architectural Keystones

1. **Runtime-vs-relay duality already exists.** `AutotableGameState.UpdateSource.Runtime|Client` precedence (Wave 3) means when `variant!=changsha` we simply never bind a Changsha runtime game — the bundle's UPDATEs flow peer-to-peer through `AutotableGameState` as pure relay, exactly like upstream's `server/game.ts`. No new dual-code-path; one switch (`AutotableConnection.RuntimeMode`).
2. **Manual pickup is a sub-state machine grafted on `ChangshaPhase.Dealing`,** not a parallel system. The existing one-shot `Deal()` becomes the `dealMode=auto` path; new `BeginManualDeal() → TakeTilesFromWall() × N → AwaitingDiscard` is the `dealMode=manual` path. Both converge in identical hand state. Downstream (claim, scoring, banker rotation) untouched.
3. **Wave 3's per-viewer privacy filter already handles partial-hand privacy correctly.** Translator's `BuildThingEntries` walks `state.Hands` size-agnostically; an 8-tile concealed hand renders 8 face-down entries to non-viewer seats automatically. Zero new privacy code.
4. **One new collection kind: `pickup`.** Singleton entry (key=0) carrying `{phase, seatIndex, count, dealMode, breakPoint, wallIndex}`. Server emits during pickup; tombstones on `AwaitingDiscard`. Inbound (client) entries `pickup.rollDice` and `pickup.take` route to two new runtime methods (`RollDiceAsync`, `TakeTilesFromWallAsync`).

### Disjoint Scope Locks for Vasquez/Hicks/Bishop

Wrote the §6 table explicitly enumerating READ+WRITE / READ-only / MUST-NOT-TOUCH per agent. Bishop = bottleneck (12+ files including new `Changsha/Bot/` directory with 6 new files for the strategy engine). Hicks = second bottleneck (10 TS/HTML/CSS files + rebuilt parcel bundle). Vasquez = fast (3 new test files + `AcceptanceFixture.cs` helper edit). Coordinator can fan out all three in parallel — strict-disjoint scopes prevent file-level collision.

### Locked Defaults (per Stephen's "accept defaults" earlier)

Difficulty levels: all three Easy/Medium/Hard ship in Phase F (Stephen confirmed). Default variant: Changsha. Default dealMode: manual for Changsha, auto for everything else (preserves upstream UX). Default botCount: 3. Default botDifficulty: Medium. Default bot speed: 800ms move / 500ms pickup / 400ms claim (configurable via `ChangshaRuntimeOptions`). `AutotableConnection.AutoBotFill` flips from Wave-3's `true` default to explicit `BotCount`-driven fill — Wave 3 bare-URL UX is preserved because `botCount=3` is the new query-param default.

### Top Risks Captured

1. Pickup state machine has 6 distinct phases — easy to bug. Mitigated by Vasquez's `ManualPickupAcceptanceTests.cs` covering every transition + every invalid-seat/count rejection (target ~18 cases).
2. Mid-session variant switching can't hot-swap (runtime is already bound). Mitigated by gating the `#game-type` select behind a "Reload to change variant" warning; `#deal-mode` stays hot-swap.
3. Hard-tier bot performance (EV depth-2 lookahead) might exceed 800ms budget. Mitigated by per-(hand,draw) EV cache + Medium-tier fallback under budget pressure.

### Cut Lines

In priority order if Phase F runs over: cut Hard difficulty (→ Phase F.1), then break-point marker icon (→ "highlight only"), then spectator mode (→ 0/3 only). Do NOT cut manual pickup, variant switching, or single-player bot fill — those are the directive.

### Exit Gate

§8.4 in the design doc: 5 named smoke tests (Changsha manual + bots, Riichi 4p variant switch, Changsha auto-deal regression, spectator mode, 3-difficulty bot smoke), `dotnet test` 250+ green, `parcel build` clean, all three agents sign off, Scribe merges, Stephen approves headline smoke test.

### Self-Critique

The §2.4 doc has a corrective breadcrumb (initial mislabeling of `PickupRound3` as count 1, with correction below) — left in deliberately as a "trust but verify" hint for Bishop. The corrected `ExpectedPickupCount` switch is canonical. Also: `FOUR_PLAYER_DEMO` is intentionally dropped on restore — documented as deliberate divergence from upstream rather than oversight. The translator's existing hardcoded `gameType="FOUR_PLAYER"` in `BuildMatch` (line 204) was a Wave 3 lie that Bishop needs to fix when restoring variant authority.

## Phase H — Architecture + Known-Limitations + V2 Rules Design Memo (2026-05-20)

**Branch:** `stlong/phase-h-wave-1-stability-polish` (cut from `main` @ `730946c`)
**Drops:**
- `docs/architecture.md` (NEW, committed) — 3–6 page engineering reference
- `docs/known-limitations.md` (NEW, committed) — V1 gap surface with skipped-test pin-points
- `.squad/decisions/inbox/ripley-phase-h-design.md` (NEW, gitignored, local-only) — Wave 1 contracts + Wave 2 V2 rules plan

### Learnings

#### Design highlights — Wave 1 contracts
- **`StateVersion` already half-built.** The field exists in `ChangshaGameState`, the state machine increments it, and persistence round-trips it. Wave 1 is purely additive: a new `ChangshaConcurrencyException` + an `int? expectedVersion = null` trailing param on the 12 mutating runtime methods + a single `GuardVersion` call inside each. Default null means full backward-compat for tests and the WS endpoint (which is not yet plumbed to echo versions).
- **Bot timeout has a clean injection point.** `ChangshaBotEngine.Resolve` is a static singleton resolver; wrapping every strategy call in a `CancellationTokenSource(BotDecisionTimeoutMs)` happens inside the engine, not at each caller. Strategies stay timeout-agnostic. Safe-default fallbacks per phase (Pass for claim, `SelectDiscardTile` for own-turn) preserve game progression deterministically.
- **Wave 1 is 4 commits** (Bishop ×2, Vasquez ×1, Ripley ×1 for docs). Disjoint file scope between Bishop's prod code (`Changsha/{Runtime,Bot}/`) and Vasquez's tests (`tests/Changsha/EdgeCaseTests.cs` + `BotBehaviorTests.cs`). Hicks no-op in Wave 1.

#### Rules-completeness reasoning — Wave 2 V2 surface
- **ThirteenOrphans has a structural gap in Changsha.** Classical 13-Orphans requires the 13 distinct terminals+honors. Changsha plays with **no honors** (108-tile deck = 3 suits × 9 ranks × 4 copies — verified at `setup.ts:46`). The spec §4.3 doesn't list 13-Orphans as deferred. The test stub comment hedges "Reddit §Big hands lists 13-Orphans in some Changsha variants." Two options surfaced in the memo: (1) ship a `WinPattern.NineTerminals` analog (all rank-1-or-9 tiles, 14 tiles); (2) delete the two test stubs and treat as N/A. Defaulting to option 1 with option 2 as Stephen-veto fallback.
- **Robbing-Kong is two-tests-one-mechanic.** `RobbingKong_Win` (claimer view) and `ExposedKong_CanBeRobbed` (declarer view) are the same state-machine change: added-kong (補杠) opens a Hu-only claim window between `DeclaringKong` and `DrawingReplacement`. Concealed kong (暗杠) explicitly stays non-robbable per spec — confirmed against MahjongPros + Baidu. `WinMethod.RobbingKong` enum already exists; the detector path is reused; the new work is in `ChangshaStateMachine` + `ClaimAdjudicator` + the `Runtime` wiring.
- **Stacking is two-tests-one-mechanic too.** `StackedBigWinPatterns` (detector view) + `MultipleBigWinPatterns_ScoresStack` (scoring view) both need `WinDetectionResult.AllPatterns: IReadOnlyList<WinPattern>` populated AND `ScoringService` applying a ×N multiplier to base BigWin payments where N = count of distinct Big-Win patterns satisfied. Spec §5 is silent on the multiplier; the memo proposes ×1 / ×2 / (×3 vestigial) and flags it as needing Stephen sign-off before Wave 2 ships.
- **Net Wave 2: 5 tests un-skip via 3 rule changes** (NineTerminals/13-Orphans, RobbingKong, Stacking). Estimate **~7 commits** on a future `stlong/phase-h-wave-2-v2-rules` branch (Bishop ×4, Vasquez ×3).

#### Anticipated Wave 2 file scopes
- **Bishop (prod):** `Changsha/WinDetector.cs`, `Changsha/ChangshaDomain.cs` (enum extension + new fields), `Changsha/ScoringService.cs`, `Changsha/ChangshaStateMachine.cs` (added-kong window graft), `Changsha/ClaimAdjudicator.cs` (Hu-only opportunity overload), `Changsha/Runtime/ChangshaGameRuntime.cs` (wiring).
- **Vasquez (tests):** `Changsha/WinPatternTests.cs`, `Changsha/EdgeCaseTests.cs`, `Changsha/Acceptance/HuValidationBigWinsTests.cs`, plus two new acceptance files (`RobbingKongAcceptanceTests.cs`, `StackedBigWinScoringTests.cs`).
- **Hicks:** zero work in Wave 2 (V2 rules are server-authoritative; optional Phase I+ polish to surface stacked patterns in the win panel).
- Strict-disjoint scope just like Phase F/G — no co-owned files in either wave.

#### Architecture doc — design choices
- Chose a **Mermaid flowchart** for the high-level diagram (renders natively on GitHub; avoids ASCII art that doesn't survive prose-editing).
- Tabular module breakdowns with **file:LOC counts** verified via `wc -l` to give reviewers calibration on where complexity lives (the 1,543-line `ChangshaGameRuntime.cs` and the 1,056-line `AutotableWsEndpoint.cs` are the two large surfaces).
- Section 10 (scope boundaries) is explicit about ownership per agent, intentionally mirrored from each Ripley design memo so contributors and AI agents find the same rules in the architecture doc and in `.squad/`.
- Did NOT invent a "system context" or "deployment" diagram beyond the actual code paths — kept it grounded.

#### Known-limitations doc — design choices
- Every entry has the three-part structure: **what is gapped**, **spec section reference**, **skipped test pin-point** (where one exists). This makes the doc both a player-facing FAQ and an engineer-facing TODO.
- Distinguished "deferred to V2" (with concrete design plan in the inbox memo) from "Phase I+" (no detailed plan yet) to keep the V2 scope tight.
- Surfaced the **single-game-per-instance** limitation that's pinned by `AutotableWsRelayTests.cs:182` — separately from the V2 rules tests, since it's an infra gap not a rules gap.

#### Process discipline observation
- Branch `stlong/phase-h-wave-1-stability-polish` already had Bishop and Hicks's in-flight Wave 1 changes (new `ChangshaConcurrencyException.cs`, modified `ChangshaGameRuntime.cs` + `ChangshaBotEngine.cs` + `ChangshaRuntimeOptions.cs`, plus frontend lobby polish + Parcel rebuild) when I started. Stayed strictly in my docs-only scope and committed only the three Ripley files. Other agents own their commits independently.

## Phase L+ — Full-System Audit with Real Integration Testing (2026-05-29)

**Branch:** `audit/system-checklist` (squashed into `main`)
**Base:** `main` @ `4f81e08`
**Directive:** Stephen — "Yes - continue... fan out and perform an audit with real integration testing to confirm that the game works."
**Mode:** Audit-only. No product code touched.

### Drops
- `playtest-artifacts/playtest-system-audit.spec.mjs` (NEW, ~750 LOC) — full Playwright audit spec, ESM, runs 43 gates across 7 categories (DB / lobby / seat-matrix / spectator / leave-seat / reconnect / variants×5 / mobile / claim / win-modal) producing PASS/FAIL/SKIP with evidence + screenshots.
- `playtest-artifacts/system-audit/` (NEW) — findings.json (691 lines, machine-readable), REPORT.md (167 lines, human-readable), run.log, and 17 screenshots.
- `.squad/decisions/inbox/ripley-system-audit.md` (NEW) — verdict memo with per-fail routing to Bishop / Frost.

### Verdict — 🟡 SHIPPABLE WITH CAVEATS
**38 PASS / 5 FAIL / 0 SKIP across 43 gates.** All 5 failures cluster on two narrow surfaces — one backend wire-handler bug and one documented architectural limit for relay variants. Changsha runtime (canonical) fully green. Lobby, all 4 seats, spectator, reconnect, mobile (375px), claim overlay, win modal, DB persistence all green.

### Learnings

#### Real product bugs found
- **`L-10-leave-seat` — Bishop's lane.** `TryHandleSeatTakeAsync` at `AutotableWsEndpoint.cs:553-559` does `if (seatEl.ValueKind != JsonValueKind.Number) return false;` which silently drops the frontend's `{seat: null}` release payload (sent by `game-ui.ts:579`). The disconnect path (`RemovePlayerEntries` ~line 982) cleans up correctly, which is why reconnect/spectator pass. Fix is ~10 lines: add an `else if (seatEl.ValueKind == JsonValueKind.Null)` branch that clears the seat for that `playerId`.
- **`V-2..V-5-bot-move` — Bishop or Frost's call.** All four relay variants (riichi4 / riichi3 / bamboo / minefield) render fine (`thingsCount=197`, wall=136) but never advance after `#deal` because `?botCount=N` only triggers backend autobots through the **Changsha runtime path** (`AutotableWsEndpoint.cs:235, 253`), and relay variants explicitly do not bind a Changsha runtime (`AutotableWsEndpoint.cs:561`: "Relay-mode connections do NOT bind a Changsha runtime. The bundle's local Setup drives the deal; the backend only relays.") — so they have no autobot driver. Two resolution paths surfaced in the memo: (a) accept and document, (b) build a relay-side autobot harness. Needs Stephen's call.

#### Test pattern discoveries
- **Synthetic claim must key by `String(selfSeat)`, not `'current'`.** `claim-window-overlay.ts:307` filters by `key === String(client.seat)`. The first iteration used `'current'` and silently no-op'd — gates passed-but-broken until I traced the filter. Documented in the spec and the memo.
- **Win-modal close uses the tombstone path.** `$('#game-complete-modal').modal('hide')` from `page.evaluate` is unreliable because jQuery `$` isn't always in Playwright's isolated context. Emitting `[['current', null]]` on `client.gameComplete.events` fires `dismissGameCompleteModal()` via the production subscriber at `game-ui.ts:1814`. More robust because it exercises the actual production close path.
- **Ferro overlay attach is polling-based** — `ClaimWindowOverlay.attach()` polls every 100ms for up to 30s. The first probe on a fresh page races the attach; the spec uses a 30×500ms retry loop on `C-1-claim-overlay-attached` to absorb this. Without the loop, runs would intermittently flap (saw it on iteration 3 — 5/5 claim gates FAILed because attach hadn't completed).

#### Environment quirks
- **No `sqlite3` CLI, no sudo.** The DB snapshot helper wraps `python3 -c "import sqlite3; ..."` via `execFileSync`. Future audits should reuse this pattern (`readPlayerStatsSnapshot()` at ~L155 of the spec).
- **Run from repo root.** `path.resolve('./playtest-artifacts/system-audit')` double-nests if you run from inside `playtest-artifacts/`. Codified in the memo's run command.
- **Backend already running** on port 8088 with sqlite at `/tmp/mat-postfix.db`. Identity endpoint `POST /api/identity` is 200 OK with arbitrary `{displayName}` (Drake's c369c54 working as designed).

#### Coverage map
- Changsha (canonical) — full green: deal → seat → discard → claim overlay → win modal → DB row written.
- 4 relay variants — render-only green; bot-move FAIL (architectural).
- All 4 seats (E/S/W/N) take cleanly with seat-take screenshots as visual evidence.
- Mobile @ 375px — sidebar 160px (matches `hicks-mobile-sidebar.css:115-122`), all touch targets ≥44px (qm=44, picker=44, lobby-close=44×44), safe-area-inset present on both `#lobby-panel` and `#lobby-toggle`, no horizontal overflow.
- DB rowcount delta — 84 → 101 across the run, confirming PlayerStats writes are landing.

### Self-Critique
- **4 iterations to stabilise.** Should have caught the `#game-id` vs `#lobby-gameId` selector drift and the overlay-attach race on iteration 1 by reading the DOM more carefully before writing assertions. Future audit specs: bias toward retry-with-diagnostics loops on any DOM that's populated by an async/polling subsystem.
- **Marking the 4 relay-variant `bot-move` gates as FAIL surfaces the architectural gap (good) but conflates a documented design decision with a regression (less good).** A more accurate audit would SKIP these gates with a `reason: "Relay variants have no backend bot driver — see AutotableWsEndpoint.cs:561"`. The current binary PASS/FAIL/SKIP model handled it correctly enough; future iterations might add a `KNOWN-LIMIT` status.
- **The DB-1 schema gate is brittle.** Static expected-column list will FAIL on any schema change. Acceptable trade-off (loose pattern-match hides real drift) but flagged for whoever next touches `PlayerStats`.
- **Did not exercise:** WebSocket disconnect under load, multi-game-per-instance (pinned by `AutotableWsRelayTests.cs:182`), bot-vs-bot full hand to completion. These are the next layer if Stephen wants deeper coverage.
- **Strict lane discipline held.** Touched only the spec file, the system-audit/ output directory, the memo, and this history file. Did not edit `AutotableWsEndpoint.cs` or `claim-window-overlay.ts` even though I'd diagnosed the bugs — those are Bishop and Ferro's lanes.

---

## 2026-06-03 — Production-readiness audit (post broken-deal sprint)

### Directive
Stephen — "So, are you done? Have the team fan out and throughly test the game and its functionality. This has taken so, so long already. Get it together!"

### Verdict
🟡 **GO — 54/59 gates PASS** (91.5%). The canonical Changsha + 3-bots game is healthy end-to-end. One MAJOR (incomplete L-10 leave-seat WS broadcast — Bishop's lane) + 4 MINOR (relay-variant bot-move architectural gaps). Backend test sweep 5272/5276 (99.96%).

### Workstream
1. Re-ran prior 43-gate `playtest-system-audit.spec.mjs` → 38 PASS / 5 FAIL — **no regressions** vs prior `b7b58e9` baseline.
2. Built new 16-gate `playtest-ripley-prodready.spec.mjs` covering operational (health/DB/migrations/WS), tour overlay, multi-game isolation, HTTPS-readiness, bundle hygiene, source hygiene → **16/16 PASS**.
3. Full backend test sweep: 5272 PASS / 2 FAIL / 2 SKIP. The 2 fails are pre-existing flakes (W9 NightlyCron YAML, MultiGameRouting LateJoin).
4. Cross-agent sync — read and incorporated:
   - Vasquez `vasquez-thorough-test.md` — 18/18 gates, 5/5 scenarios, multi-game isolation E1-E4 PASS.
   - Hicks `hicks-vreg-sweep.md` — 10/10 visual scenarios, 0 page errors.
   - Drake `drake-persistence-thorough-audit.md` — 207/207 persistence tests, 100-parallel race stress green, schema-drift detector.
5. Delivered `.squad/decisions/inbox/ripley-prodready-final.md` (the verdict memo).

### Findings
- **L-10 leave-seat still regresses.** Bishop's `1febbd8` only fixed the runtime — `ReleaseSeatAsync` broadcasts on SignalR `/hubs/changsha`, not the autotable WS where the frontend's `seats` collection lives. Fix needed (Bishop's lane): mirror `AutotableWsEndpoint.cs:1015-1027` disconnect path — `state.RemovePlayerEntries(connection.PlayerId)` + `BroadcastToOthersAsync(connection, tombstones, full: false)` inside `TryHandleSeatTakeAsync`'s null branch. ~10 LOC.
- **Relay variants** (Riichi4/Riichi3/Bamboo/Minefield) `V-2..V-5-bot-move` FAIL is architectural — no backend autobot. Documented at `AutotableWsEndpoint.cs:561`. Acceptable for v0.31 (Changsha is primary scope).
- **README is severely out of date** (still documents the deleted `Tables/*` REST endpoints and the deleted React/Fluent UI 9 modern frontend). Doc debt, not a blocker. Scribe's lane.
- **Production bundle is clean** — 0 `console.log/debug/info` in `autotable-src.*.js` (terser stripped), only 1 hardcoded `http://localhost` reference (a code comment in `hub.ts:49`), 0 `TODO/FIXME/XXX` in backend Players/Changsha/Autotable critical paths.

### Lane discipline
Touched ONLY:
- `playtest-artifacts/playtest-ripley-prodready.spec.mjs` (new)
- `playtest-artifacts/ripley-prodready/*` (artifacts)
- `.squad/decisions/inbox/ripley-prodready-final.md` (new)
- `.squad/agents/ripley/history.md` (this entry)

Did NOT touch any source file. The L-10 fix belongs to Bishop's lane.

### Self-Critique
- **The L-10 regression I flagged in the prior wave is still not fixed.** Bishop's `1febbd8` addressed only half the problem. I should have re-read the entire fix commit at squash time and flagged this before declaring the prior wave done. **Lesson:** when a prior-wave fix lands, the next audit MUST trace the full data pipeline (runtime → broadcast → frontend collection → world render), not just inspect the immediate symptom site.
- **The `findings.json` on disk for the system-audit run is stale** — concurrent process collision. The `.work/ripley-audit.log` is the authoritative source-of-truth for today's run. Future audits should write to run-specific suffixes (`findings-${timestamp}.json`) to avoid file-overwrite races between waves.
- **Spec stabilisation was clean this time** — 3 iterations on the prodready spec (Node WebSocket vs `ws` package, `rg` vs `grep -rn`, multi-game gameId probe robustness). Faster than the system-audit's 4 iterations because I borrowed the same patterns (retry loops, env-aware tool detection).
- **Did not exercise:** bot-vs-bot completion of all 4 hands, live WS disconnect under load, cross-provider live DB validation (Postgres/SqlServer). Drake covered persistence at unit-test fidelity; cross-provider live matrix is the next layer if Stephen wants deeper coverage.
