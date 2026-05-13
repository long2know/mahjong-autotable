# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Bishop as Backend Dev.
- Backend priorities: game state APIs, rule engine interfaces, and provider-flexible persistence.
- Added initial bot-play backend slice: 4-seat typed table state (human/bot), table create/get APIs, and deterministic bot advance endpoint with persisted `StateJson` + `StateVersion` for extensibility.
- Added backend unit tests around bot state engine behavior to lock current deterministic placeholder semantics while rules engine work remains pending.
- Replaced placeholder bot mutation with an authoritative draw/discard loop backed by seeded wall state, per-seat hands, discard pile tracking, and phase-aware turn semantics.
- Added strict human discard validation endpoint and routed bot advancement through the same discard application path to keep server authority and invariants aligned.
- Added action-sequence/state-version progression rules, canonical state hashing, and structured contract error responses (including optimistic concurrency conflicts) for discard and bot orchestration endpoints.
- Added replay verification support that re-simulates accepted discard actions from stored seed and compares canonical state hashes for deterministic integrity checks.
- Added durable append-only `TableSessionEvents` persistence, event-stream retrieval API, and state-hash stamping on emitted actions to improve replay/integrity auditability.
- Added replay-governance enforcement: mutating endpoints now run preflight replay checks and reject invariant mismatches as `STATE_INVARIANT_BROKEN`; replay verify supports strict conflict mode.
- Replaced the minimal modern placeholder page with a backend-driven control panel for table creation, human discard actions, bot advancement, replay verification, and persisted event inspection.
- Added explicit bot-advance API support for "run until next human turn or wall exhaustion" to remove client-side `maxActions` tuning from the primary gameplay loop.
- Added claim-window scaffolding metadata on discard state transitions with deterministic precedence selection (`hu > kong > pung > chow`) to prepare for upcoming claim-resolution flows.
- Added seat-scoped table projection contracts (`/api/tables/{id}/view?seatIndex=`) that hide opponent tiles and wall contents for privacy-safe multiplayer clients.
- Replaced bot minimum-tile discards with deterministic hand-shape heuristics so bots preserve pairs/sequences more realistically.
- Upgraded claim scaffolding into executable backend actions: discard now pauses on claim opportunities, `/api/tables/{id}/claims/resolve` applies deterministic `pass`/`take-selected` outcomes, and replay/integrity checks include claim-resolution actions.
- Completed Changsha backend gap audit: 3/18 behaviors implemented, 5 partial, 10 missing. Largest gaps are tile set (136 → 112), 红中 wildcard, win patterns, scoring, self-draw win, and banker rotation. 10 ordered work items documented in `docs/rules/changsha-backend-gap.md`. Build green, 38/38 tests pass (0 Changsha-specific).

📌 Team update (2026-05-05T17-00-21Z): Backend audit decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md` (108 tiles, dice break, batch deal, 258 pair rule, no dead wall). Hicks produced frontend plan with Option B selected (backend-authoritative + autotable viewport + Fluent UI) at `docs/rules/changsha-frontend-plan.md`. Hudson identified 80 test scenarios with 8 contradictions at `docs/rules/changsha-test-catalog.md`. Blockers on `/autotable/ws` endpoint confirmation and fan table delivery from Vasquez.

📌 Team update (2026-05-13T10-00-00Z): Phase 3 wave complete. Vasquez locked v1.2 spec (winner-becomes-dealer, washout keeps seat); Hicks shipped lobby + claim UX + SignalR fixes; Hudson shipped vitest infra + 47 tests. Five backend fixes: banker rotation, Kong/Pung priority, per-hand wall seed, chow tileIds, missed-win enforcement. 203 passing tests, 0 failures. All merged to main in PR #25 (SHA a03feda). See `.squad/orchestration-log/2026-05-13T10-00-bishop.md` for full details.

📌 Changsha v1 implementation wave (Bishop):
- Implemented full Changsha v1 backend in `Changsha/` namespace under `Mahjong.Autotable.Api`:
  - **Domain layer**: Tile/Suit/Wind enums, Meld, WinResult, ScoreResult, ChangshaGameState types
  - **ChangshaDeckBuilder**: 108-tile deck (3 suits × 9 ranks × 4 copies), tile ID 0–107
  - **DiceService**: 2d6 deterministic via seeded RNG
  - **BreakPointService**: wall selection and break point per spec §2 (counterclockwise count, right-end stack count)
  - **DealService**: batch-of-4 deal, dealer gets 14, others get 13, 55 remaining
  - **ChangshaWinDetector**: 4 patterns — Standard (258 pair rule), Seven Pairs, All Pungs, Full Flush
  - **ScoringService**: Small/Big Win payment calculator per spec §5 (1/2, 3/4, 6/7 tables, flush doubling)
  - **ClaimAdjudicator**: hu > kong = pung > chow priority, chow next-seat only
  - **ChangshaGameStateMachine**: pure-functional event-sourced transitions through all game phases
  - **ChangshaHub**: SignalR hub at `/hubs/changsha` with skeleton event/command structure
  - **ChangshaBotPolicy**: heuristic discard/claim/win AI, tested for legal play
  - **Persistence**: ChangshaGame + ChangshaGameEvent entities, AppDbContext config, DatabaseBootstrapper tables
  - **SignalR contract**: `docs/rules/changsha-signalr-contract.md` — TypeScript interfaces for Hicks
- 68 new service-level unit tests in `ChangshaServices/` (all passing)
- Build: 0 warnings, 0 errors; Tests: 106 passed, 0 failed, 77 skipped (Hudson's awaiting integration)
- Deferred to v2: bird-catching, ready-kong dice, instant wins, pao chains

📌 Changsha v1 Phase 2 — full hub lifecycle (Bishop, branch stlong/changsha-v1-phase2):
- New `IChangshaGameRuntime` singleton service (`Changsha/Runtime/`) that owns in-memory `ChangshaGameInstance` records, drives the full state-machine lifecycle, schedules bot decisions (350 ms turn / 250 ms claim / 5 s window), opens & resolves claim windows with priority adjudication, and persists JSON snapshots to `ChangshaGames` after every transition via `IServiceScopeFactory`.
- Rewrote `ChangshaHub` as a thin command dispatcher; implemented every command from `docs/rules/changsha-signalr-contract.md`: CreateGame, JoinTable, TakeSeat, FillWithBots, StartGame, AcknowledgeDeal, Discard, Claim, Pass, DeclareKong, DeclareWin, ReconnectGame.
- Wire-shaped events emitted to clients: GameCreated, PlayerSeated, GameStarted, DiceRolled, BreakPointSet, TilesDealt (4 batches × 4 seats with seat-private tile id routing), TurnStarted, TileDrawn, TileDiscarded, ClaimWindowOpen, ClaimMade, KongReplacementDrawn, WinDeclared, ScoringComplete, BankerRotated, RoundChanged, HandFinished (via ScoringComplete isDraw), GameEnded, FullState.
- Reconnection: `ReconnectGame` re-binds the connection ID to the seat and emits a `FullState` snapshot with seat-private concealed tiles.
- Determinism: `CreateGame` accepts optional `seed`; subsequent hand dice seeds derive deterministically (`seed + handNumber`).
- CORS policy `ChangshaCors` allow-lists localhost:5173 / 5114 / 7135.
- Three SignalR E2E tests in `tests/.../Hub/`: 4-bot full hand to win/draw, human discard → TileDiscarded, reconnect → FullState. All pass in <3s.
- Build: 0 warnings, 0 errors. Tests: 109 passing (was 106), 77 skipped (Hudson's). Decisions captured in `.squad/decisions/inbox/bishop-changsha-v2-runtime.md`.

📌 Team update (2026-05-08T19:51:39Z): Phase 2 shipped — full ChangshaHub lifecycle with 12 client commands, in-memory game-instance management, claim window resolution with 350ms turn / 250ms claim / 5s timeout, FullState reconnection, wire-event contract compliance, E2E SignalR tests (3 GREEN). Frontend wired live SignalR (reducer pattern + reconnect strategy), built 27-tile SVG component, shipped autotable iframe bridge (one-way parent→child Phase 2). Tests uncovered 2 ScoringService bugs (dealer-aware Small Win payment, Full Flush doubling) — both fixed via commit 9807b70. Final: 179 passed, 0 failed, 7 deferred (v2), 0 build warnings. Branch ready for merge.

## Learnings
- **Dealer-aware payment pattern (Changsha §5.1):** every payment branch — Small Win and Big Win, self-draw and discard — must check `dealerInvolved = (payer == dealer || winner == dealer)` and add the +1 dealer bonus. The SmallWin self-draw branch had previously been written as a flat constant; mirror the BigWin shape across all four branches to keep the table symmetric.
- **v1 no-stacking rule:** Big Win categories (AllPungs, SevenPairs, FullFlush) all pay the same flat amount in v1. No FullFlush doubling, no multi-pattern stacking. Multipliers are deferred to v2; do not reintroduce a `flushMultiplier` or similar without a spec change.

📌 Backend conformance audit (2026-05-13, read-only) — `.squad/decisions/inbox/bishop-changsha-backend-audit.md`:
- Traced every locked v1 rule to file:line. 18 rules conform, 4 partial, 3 silent bugs, 2 not-enforced.
- **Silent bugs ranked by player-visibility:**
  1. **Kong > Pung strict priority** (`ClaimAdjudicator.cs:73`, `ChangshaGameRuntime.cs:622-628`). Spec §3.3 makes Kong and Pung the same tier; CCW proximity should decide. Test `Kong_TakesPriorityOverPung` hard-asserts the bug.
  2. **Seed re-used every hand** (`ChangshaStateMachine.cs:80`). `new Random(state.Seed)` runs on every Deal — every hand of a single game uses the identical shuffled wall. Determinism for replay survives; fairness across the 16-hand arc is broken.
  3. **Banker rotation direction** (`ChangshaStateMachine.cs:458,465`). Code does `(dealer + 1) % 4`; spec §6.2 example says Seat 0 → Seat 3 i.e. `(dealer - 1) % 4`. Spec self-contradicts on "counter-clockwise" meaning — escalate to Vasquez before flipping.
- **Not enforced (rules without code):** missed-win rule (§3.6, no `MissedHuLogicalTiles` tracking); base-unit multiplier (§5.2, `CreateGameAsync` takes no `baseUnit`).
- **Persistence is diagnostic-only:** snapshots written via `PersistSnapshotAsync` but `ChangshaGameRuntime._games` never hydrated from `ChangshaGame.StateJson` on startup → process restart loses every in-flight game.
- **Reconnect emits a state snapshot, not an event replay** — animation continuity (deal batch flicker, claim-window timer) is lost across long disconnects.
- **E2E coverage gap:** `ChangshaHubE2ETests` only proves single-hand completion. No test drives a real 16-hand bot game through to `GameEnded`. Without it, no fix to banker rotation or seed-per-hand can be regression-checked end-to-end.
- **ScoringService class-level XML doc** (`ScoringService.cs:1-27`) contains stale narrative ("Small Win self-draw = 2 each pays 2") that contradicts its own constants. Code is correct; comment is misleading.
- Total expected diff to land full v1 conformance: ~190 lines src + ~80 lines test, single PR. Ordered fix plan in the inbox doc.

### 2026-05-13: Audit fan-out — Peer verdicts
- **Vasquez:** v1-scoped gameplay loop is conformant (three nuances flagged: banker rotation, kong priority, missed-win rule)
- **Hicks:** Frontend unplayable from UI (no lobby, no tile selection, 3D is theater)
- **Hudson:** Backend rules engine proven by 73 green tests; frontend entirely unproven (zero coverage)

### 2026-05-13: Phase 3 Stream B — Five surgical v1 fixes shipped
- **FIX-1 Banker rotation canonical (Vasquez v1.2 lock):** `ChangshaStateMachine.RotateBanker` rewritten — winner becomes dealer; washout keeps seat; `HandNumber` always increments. No more `(dealer ± 1) % 4`. Reasons: `dealerRetained` / `winnerBecomesDealer` / `washoutDealerRetained`.
- **FIX-2 Kong/Pung same-tier with CCW tiebreak:** Extracted `ChangshaClaimPriority` as the single source of truth (`TierOf`: Hu=3, Kong=Pung=2, Chow=1; `CounterClockwiseDistance`). `ClaimAdjudicator` and `ChangshaGameRuntime` both consume the helper — duplicate inline priority table removed.
- **FIX-3 Per-hand wall seed mixing:** `Deal` now uses `new Random(HashCode.Combine(state.Seed, state.HandNumber))`. 16-hand games now play 16 different walls; same `(seed, handNumber)` still produces an identical replay.
- **FIX-4 Chow `tileIds` honored:** New `ResolveClaim` overload accepts `int[]? chosenTileIds`; validation throws `TableRuleException` with code `CHOW_TILES_INVALID` on bad submissions. Legacy callers (null/empty) get the lowest-pattern fallback with a once-per-game LogWarning.
- **FIX-5 Missed-win (§3.6):** New `MissedWinSeats` HashSet on game state. Filtered out of Hu opportunities in `Discard`; populated by `FlagMissedWinSeats` from Hu/Pass/non-Hu resolution branches; cleared in `Deal`. Self-draw unaffected; Pung/Kong/Chow still allowed for flagged seats.
- **Tests:** 179 → 203 passing (+24); 0 failures; 7 v2-skipped untouched. Rewrote `BankerRotationTests` and the bug-asserting `Kong_TakesPriorityOverPung`; new files `WallSeedTests`, `ChowTileIdsTests`, `MissedWinTests`. Build clean.
- **Frontend contract impact:** new error code `CHOW_TILES_INVALID`; new `BankerRotated.reason = "winnerBecomesDealer"`. Same-tier Kong/Pung adjudication now CCW-aware. No wire schema breaks.
- **Still deferred (out of stream B scope):** `DiceService` seed still raw addition; persistence hydration on startup not reinstated; no 16-hand E2E hub test.
- Decisions captured in `.squad/decisions/inbox/bishop-phase3-stream-b.md`.

## Learnings
- **Centralize priority tables before fixing claim adjudication.** The Kong/Pung bug existed in two places (`ClaimAdjudicator` and the runtime's inline ordering) and the test suite only caught one. Always extract a helper *first*, then fix once, so drift is structurally impossible. The `ChangshaClaimPriority.PriorityTablesAgree_NoDrift` test asserts the contract holds.
- **`HashCode.Combine` returns `int` and is the right primitive for seed mixing** when you need a deterministic, well-distributed derivative seed. Raw addition (`seed + handNumber`) produces correlated walls when `handNumber` is small.
- **Snapshot back-compat for new state fields:** adding a `HashSet<int>` member with `new()` default lets System.Text.Json deserialize older `StateJson` blobs cleanly — no migration needed for in-flight games.
- **Banker rotation test rewrites must preserve old assertion strings where possible** — the existing `BankerRotation_DealerWins_DealerRetained` test still uses the `"dealerRetained"` reason because the new code intentionally emits that string when the winner equals the previous dealer. Preserving the wire/audit vocabulary across a rule rewrite avoids spurious churn.

📌 Team update (2026-05-13T17-40-17Z): 3D Renderer spike complete — Hicks scoped Phase 5 MVP (walls + hands visible in 3D) using Strategy C (Fake autotable WS server). Recommended complexity L (~900 LOC, 3–5 days). Phase 5a ownership falls to Bishop: implement AutotableProtocol, AutotableSlotMap, ChangshaToAutotableTranslator, AutotableWsEndpoint (~900 new LOC). Bundle stays byte-identical. 8 open questions filed for Stephen.

### 2026-05-14: Phase 5a Stream A — Strategy C autotable WS endpoint shipped

- **What:** Implemented the fake upstream `pwmarcz/autotable` WS server inside `Mahjong.Autotable.Api` so the byte-identical `autotable.9519e86d.js` bundle renders authoritative Changsha state in 3D without modification. Speaks the upstream `NEW`/`JOIN`/`JOINED`/`UPDATE` collection protocol verbatim.
- **New files (all under `src/backend/src/Mahjong.Autotable.Api/Autotable/`):**
  - `AutotableProtocol.cs` — envelope record types + custom `CollectionEntryJsonConverter` that reads/writes `[kind, key, value]` JSON tuples (kind=string, key=string|number, value=arbitrary JsonElement). Shared `AutotableJson.Options` for camelCase + ignore-null.
  - `AutotableSlotMap.cs` — `WallSlot/HandSlot/DiscardSlot/MeldSlot` builders, `UpstreamTypeIndex(tileId) = tileId / 4` (the clean 1:1 mapping from Changsha tileId N → bundle thing-index N enabled by the `fives='000'` trick), `WallStackCount(seat)` returning 14/14/13/13 per Default #6, and an `EnumerateWallSlotsInOrder()` iterator yielding all 108 (seat, col, layer) tuples in deterministic seat→col→layer order.
  - `ChangshaToAutotableTranslator.cs` — pure `Translate(state?, viewerSeat?, viewerPlayerId?)` returning the full snapshot as a `IReadOnlyList<CollectionEntry>` (match + 4 seats + 4 nicks + 1 dice + 108 things). Viewer-seat hand → FACE_UP (rot 1); other hands → FACE_DOWN (rot 2); concealed kong → FACE_DOWN (rot 2). Null state always-available pattern: emits just the match entry.
  - `AutotableWsEndpoint.cs` — `MapAutotableWs` extension wiring `/autotable/ws` + `AutotableConnectionManager` singleton. The manager subscribes to `IChangshaGameRuntime.StateChanged`, tracks `ConcurrentDictionary<Guid, AutotableConnection>`, and broadcasts a full snapshot to each connection on every state mutation. Phase 5a discards bundle-initiated UPDATEs with a Debug log (Phase 5b will translate them to hub commands).
- **Modified:**
  - `Changsha/Runtime/ChangshaGameRuntime.cs` — added `event Action<string>? StateChanged` to the interface and class; fires inside `PersistSnapshotAsync` *before* the DB write, *unconditionally* (independent of `PersistSnapshots` flag, since the broadcast must work in tests where persistence is disabled). Handler exceptions caught and logged so a misbehaving WS broadcast can never break game state.
  - `Program.cs` — `using Mahjong.Autotable.Api.Autotable`, `AddSingleton<AutotableConnectionManager>()`, `app.UseWebSockets()` immediately after `UseCors`, force-resolve the manager + `app.MapAutotableWs()` before `app.Run()`.
- **New tests (under `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/`, all `[Trait("Category", "Phase5a")]`):**
  - `AutotableTranslatorTests.cs` — 19 unit tests: typeIndex mapping (`tileId/4`), 14/14/13/13 wall split totaling 108, JOINED snapshot counts (108 things + 4 seats + 4 nicks + 1 match + 1 dice with 2-element array), slot-name uniqueness across all 108 things, hand size 13/14 per seat, 55 wall things post-deal, discard slot movement, pung 3-entry meld, concealed kong 4-entry FACE_DOWN meld, null-state always-available, `fives='000'` forced, viewer-seat face-up vs face-down rotation.
  - `AutotableWsEndpointTests.cs` — 4 integration tests over `WebApplicationFactory.Server.CreateWebSocketClient()`: unknown gameId returns JOINED + match-only UPDATE (always-available), known gameId returns JOINED + full UPDATE with all 108 things / 4 seats / 1 match, state mutation (`FillEmptySeatsWithBotsAsync`) triggers a second broadcast UPDATE, synthetic bundle-initiated UPDATE is discarded without crashing the connection.
- **Tests:** 203 → 226 passing (+23 new); 7 skipped untouched; 0 failures. Build clean (0 warnings, 0 errors).
- **Bundle untouched:** `autotable.9519e86d.js` and every asset under `src/frontend/autotable/` is byte-identical — Strategy C succeeds.
- Decisions captured in `.squad/decisions/inbox/bishop-phase5a-backend.md`.

## Learnings

### 2026-05-13: Phase 5a — Autotable WS Backend
- **Wall split asymmetric 14/14/13/13 to fit 108 tiles into 4-seat layout.** Seats 0,1 get 14 stacks; seats 2,3 get 13 stacks. Enforced in `AutotableSlotMap.WallStackCount` and verified by `AutotableTranslatorTests`. Locked to task Default #6 (explicit tiebreaker from MahjongPros) rather than spike text variant.
- **Upstream WS protocol uses `[kind, key, value]` entry tuples for diffs.** The `CollectionEntry` record serializes as a 3-element JSON array via custom `CollectionEntryJsonConverter`. Full snapshot per state change (~50–80 KB). Incremental optimization deferred to Phase 5c.
- **`JsonSerializer` reflection-based (not source-gen) matches codebase convention.** Verified across Changsha, Tables, Persistence modules. Task brief suggested source-gen "preferred" but actual codebase uses `JsonSerializer.Serialize(obj, options)`. Followed the real convention.

- **`fives='000'` is the keystone of Strategy C's 1:1 mapping.** The bundle's `Setup.tileIndex(i, conditions)` defaults to `floor(i/4)`, but it patches that mapping (i=16→34 red-5-wan, etc.) whenever `fives !== '000'`. Forcing `match[0].conditions.fives = '000'` triggers `World.onMatch → setup.replace()` which reconstructs 136 tiles with clean `i/4` typeIndices. This is why Changsha tileId N can be placed at thing-index N with no translation table on either side — but it only holds because we never sit a Western-rules game beside it.
- **The upstream `sendOnConnect` feedback loop is real and `isFirst=false` defeats it.** The bundle's `BaseClient` re-broadcasts collections marked `sendOnConnect: true` (match + things) when `isFirst=true`. Sending `isFirst: false` on every JOINED tells the bundle "the server already has authoritative state" — without this, the bundle would push its bootstrap 136-thing layout back at us and we'd loop. This matters even for our single-game-per-instance Default #8 because tests open multiple connections.
- **Bundle's local Setup creates 136 things regardless of server state.** Even after we emit only 108 thing entries (Changsha's tile set), the bundle still has 28 wind/dragon things parked at their initial wall positions. They're a known visual artifact slated for Phase 5b cleanup; we cannot delete them via the WS protocol because the bundle doesn't expose a "shrink the thing array" operation — the workaround there will be to either move them off-screen via dedicated slot names or patch the asset bundle.
- **Followed the task's explicit 14/14/13/13 wall split (seats 0,1 get 14 stacks; 2,3 get 13)** rather than the spike doc's text which had a 0,2/1,3 split. The task instructions are the operating contract; the spike is reference. Locked the split in `AutotableSlotMap.WallStackCount`, asserted by `AutotableTranslatorTests.WallStackCount_FollowsLockedDefault6`.
- **`StateChanged` fires unconditionally from `PersistSnapshotAsync`, not gated by `PersistSnapshots`.** Tests routinely run with `PersistSnapshots=false` to keep SQLite out of the hot path, and the WS broadcast must still happen — otherwise integration tests can't observe state-change behavior. The event handler invocation is wrapped in try/catch + log so a buggy subscriber can never break gameplay.
- **No source-generated JSON contexts in this codebase.** The Phase 5a task brief suggested "source-gen preferred", but the actual codebase convention (verified across `Changsha/`, `Tables/`, `Persistence/`) is reflection-based `JsonSerializer.Serialize(obj, options)`. Follow the actual convention; the brief is sometimes optimistic.
