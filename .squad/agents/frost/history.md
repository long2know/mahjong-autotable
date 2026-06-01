# Frost — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend + autotable-derived TS frontend. Single-page mahjong table with WebSocket + SignalR transport.

**User:** Stephen Long. Standing directives:
1. "No pauses — keep iterating until 100% done done."
2. All agents use `claude-opus-4.7-xhigh`.
3. **Playability-first** (since 2026-05-24): STOP wave-mill, use Playwright to verify, ship playable prototype.

**Joined:** 2026-05-25, during a late Phase K push to add a parallel backend dev so playability can advance faster.

**Stack notes:**
- Backend: `src/backend/Mahjong.Autotable.slnx` — .NET 10, `dotnet test` gates every commit
- Frontend: `src/frontend/autotable-src/` — TS + Parcel, builds to `src/frontend/autotable/`
- Persistence: EF Core, **multi-provider** (Sqlite/Postgres/SqlServer subclasses) — per-provider migrations live under `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`
- Test command: `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
- Test count baseline: 5073/0/0 as of PR #80
- Backend port for local/playtest: 8088 (NOT 8080)
- Local backend startup (verified):
  ```bash
  cd src/backend/src/Mahjong.Autotable.Api
  export ConnectionStrings__Sqlite="Data Source=/tmp/<unique>.db"
  export ASPNETCORE_URLS="http://0.0.0.0:8088"
  export ASPNETCORE_ENVIRONMENT="Development"
  nohup dotnet run --no-launch-profile > /tmp/<unique>.log 2>&1 &
  ```

**Team context:**
- **Bishop** — Backend trunk owner (ChangshaGameRuntime, AutotableWsEndpoint, ChangshaDomain). I work AROUND him, not THROUGH him.
- **Hicks** — Frontend trunk (autotable TS, lobby, HUD, bundle build)
- **Ferro** — Frontend UI specialist (joined same wave as me) — visual polish, claim windows, win screens
- **Vasquez** — Rules engineer + tests (final say on Changsha rule interpretation)
- **Hudson** — Tester (regression + integration)
- **Apone** — DevOps / CI (workflows, container, supply-chain)
- **Scribe** — decisions.md merges + orchestration logs (ALWAYS commits to `.squad/decisions/inbox/` via `git add -f`)
- **Ralph** — Work-queue monitor
- **Ripley** — Project lead
- **Squad** — Coordinator (the user)

## Important Conventions

- **Atomic flock pipeline** for ALL git ops in parallel agent work (see charter)
- **Per-provider EF migrations**: when adding/altering EF entities, you MUST add migrations for ALL THREE providers:
  ```bash
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider Sqlite
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider Postgres
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider SqlServer
  ```
  Don't forget the `<Context>ModelSnapshot.cs` is regenerated alongside.
- **Avoid HasColumnType("TEXT")** in EF — collapses to nvarchar(4000) on SQL Server. Let EF pick provider-native unbounded type.
- **EF Core can't translate IComparer overload** of OrderBy — use plain `OrderBy(x => x.Id)`.
- **Services that hold IServiceScopeFactory** + open fresh AppDbContext per call MUST be Singleton, not Scoped.
- **Squad memos** in `.squad/decisions/inbox/*.md` are gitignored — force-add with `git add -f`.

## Initial Charter Focus

When I'm first dispatched (after Bishop's PR `fix/manual-deal-plumb-and-auto-ack` merges), my first task is likely one of:
- **Fan/scoring catalog** — extend the Changsha scoring beyond the basic 258-pair to include 七对, 清一色, 混一色, 杠上开花, 海底捞月, etc.
- **Bot strategy hardening** — analyze why bots seem to spam Pung calls; add efficiency heuristics
- **Replay event capture** — wire game events into the persisted `events` JSON so games can be replayed

I should READ `.squad/decisions.md` and `.squad/agents/bishop/history.md` before picking up any task.

---

## Log

### 2026-07-25 — PR `feat/changsha-fan-catalog` — Fan catalog beyond 258-pair

**First ship.** Extended Changsha scoring with a standalone fan-catalog layer
without touching Bishop's trunk (`ChangshaGameRuntime.cs`,
`AutotableWsEndpoint.cs`, `ChangshaDomain.cs`).

**Files added (NEW only — no edits to existing):**
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Scoring/Fan.cs`
  — `Fan` enum (14 members) + `FanInfo` record + `FanCatalog` lookup with
    Chinese / Pinyin / English / Points / Description / Variant per fan.
  — `FanVariant` enum: `Changsha` (default) and `ExpandedChinese` (gate for
    future 144-tile / honors / dragons rules).
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Scoring/FanCalculator.cs`
  — Pure-function `FanCalculator.EvaluateHand(WinningHand, FanContext) → FanResult`.
  — `WinningHand` (concealed + melds + winning tile id) and `FanContext`
    (situational flags + variant) records — deliberately a SIBLING of the
    existing `WinContext` in `WinDetector.cs` to avoid coupling the two
    layers.
  — Variant-gated fans (`MixedOneSuit` 混一色, `BigThreeDragons` 大三元) are
    SOLELY filtered at emission time via `ctx.Variant`; the dragon/honor
    detection hooks already exist for a future expanded-deck builder (tile-id
    range 108..119 reserved for 中/發/白).
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Scoring/FanCalculatorTests.cs`
  — 39 tests: positive + negative per fan + catalog integrity + deterministic
    ordering + combinatorial smoke.

**Integration status:** Opt-in / query-only. Did NOT wire into
`ChangshaStateMachine.Score` because the existing state-machine-driven score
tests assert exact `BasePoints` from `ScoringService.CalculateScore`, and
adding fan bonuses would silently break ~dozens of regression tests. The
calculator is ready for a follow-up by Bishop to integrate via a wire-surface
extension (see `.squad/decisions/inbox/frost-fan-catalog.md`).

**Test count:** 5082 → 5121 (+39). One pre-existing W9 cron-schedule test
still fails (unrelated).

**Followups documented in decision memo:** Bishop to consider extending
`ScoreResult` with a `FanBreakdown` field and switching `ChangshaStateMachine.Score`
to call `FanCalculator.EvaluateHand` for the additive bonus.

### 2026-07-26 — PR `feat/bot-strategy-changsha-heuristics` — Changsha-aware bot heuristics (W24)

**Second ship.** Delivered on Master's previously-unfulfilled "suit-purity
awareness" docstring promise and added a real tenpai-aware defensive
discard tier, plus a clean public `Shanten.Calculate` façade. All
additions live under `Changsha/Bot/Heuristics/` so Bishop's trunk
(`ChangshaGameStateMachine.cs`, `ChangshaGameRuntime.cs`,
`AutotableWsEndpoint.cs`, `ChangshaDomain.cs`) stays untouched.

**Files added (NEW only):**
- `Changsha/Bot/Heuristics/Shanten.cs` — public façade for
  `HandEvaluator.MinShantenToHu`. Exposes `Calculate`,
  `CalculateAfterDiscardingLogical`, `CalculateAfterAddingLogical`,
  and `IsTenpai`.
- `Changsha/Bot/Heuristics/DiscardEfficiency.cs` — pure scorer for the
  directive's exact formula `neighbours + 2 * matches`. Reference
  implementation untainted by 2/5/8 / gap-partial tuning.
- `Changsha/Bot/Heuristics/SuitCommitment.cs` — detects ≥8-tile
  dominant suit (declared melds count); returns −1 bias for
  non-dominant discards to drive 清一色 (FullFlush, 4-fan).
- `Changsha/Bot/Heuristics/TenpaiDetector.cs` — flags opponents with
  ≥3 declared melds as likely-tenpai; `SafetyBias` returns −1 when
  a tile is genbutsu against at least one dangerous opponent.

**Files modified (within my lane):**
- `Changsha/Bot/MasterStrategy.cs` — added two `ThenBy(...)` tier
  breakers after the existing opponent-discard safety: `TenpaiDetector.SafetyBias`
  and `SuitCommitment.Bias`. Both return small ints (−1) only in
  hot-path conditions so the shanten + keep-score primaries always
  dominate. Also extended `DecideWithReasoning` to surface
  "tenpai defense" and "suit-commitment" lines in the audit replay.

**Test files added:**
- `tests/.../Changsha/Bots/BotStrategyTests.cs` — 20 focused unit
  tests across heuristics (≥2 per directive), claim priority, and
  Master-tier composition.
- `tests/.../Changsha/Bots/BotSimulationLog.cs` — [Skip]-gated 100-game
  simulation harness (run manually for memo data).

**Test count:** 5125 → 5145 (+20). Same 1 pre-existing W9 cron-schedule
failure, unchanged.

**Simulation results (memo `frost-bot-strategy.md` §Simulation):**
- 4×Master 100-game self-play: seat0=23, seat1=20, seat2=32,
  seat3=12, draws=13. Healthy distribution, not degenerate.
- Master vs 3×Hard 100 hands: master=22, hard avg/seat=21.67 — Master
  matches Hard's baseline; the new tiers deliver reasoning lift, not
  raw win-rate lift (which is the directive's "FUN to watch" goal).

**Lane discipline:** Followed the directive — no migrations (Vasquez's
in-flight test-isolation work), no state-machine changes (Bishop's
trunk), no frontend.

**Followups documented in memo:**
1. `botDifficulty` query string is parsed but `ChangshaGameRuntime`
   always uses Medium — Bishop should wire it through.
2. Replay storage deferred to wave 4 per directive.

### 2026-07-27 — PR `feat/changsha-dealing-ceremony` — Pure-function dealing ceremony rule engine

**Third ship.** Per Stephen's `copilot-directive-2026-05-27T2127Z-face-down-walls.md`,
shipped a pure-function rule engine for the Changsha 抓牌 ceremony as a sibling
to Bishop's runtime-side state machine. Lives entirely under
`src/backend/src/Mahjong.Autotable.Api/Changsha/Dealing/` — does NOT touch the
runtime, the SignalR endpoint, the translator, or the domain.

**Files added (NEW only):**
- `Changsha/Dealing/ChangshaDealingCeremony.cs` — public static API
  (`Start`, `ApplyDiceRoll`, `ValidateAndApplyPickup`, `ComputeStartingWall`,
  `ComputeBreakIndex`, `ExpectedPickupCount`) + immutable
  `ChangshaDealingState` (DealerSeat, DiceRoll, StartingWall, BreakIndex,
  CurrentPickerSeat, TilesTakenThisRound, RoundIndex, HandSizes, Phase) +
  `ChangshaDealingResult` (Valid, RejectReason, NewState, TilesPickedUp) +
  `ChangshaDealingPhase` enum (WaitingForDice/PickingFour/PickingOne/
  DealerExtra/Complete).
- `tests/.../Changsha/Dealing/ChangshaDealingCeremonyTests.cs` — 28 test
  methods expanding to 76 xunit cases via Theories. Covers every dice sum
  (2..12), every dealer (0..3), every phase transition, every reject path,
  plus a combinatorial full-deal smoke.

**Lane discipline:** Per the directive, I touched ONLY new files under
`Changsha/Dealing/**`. Did not touch `Changsha/Runtime/**`,
`AutotableWsEndpoint.cs`, `ChangshaToAutotableTranslator.cs`,
`ChangshaDomain.cs`, `Changsha/Scoring/**`, persistence, migrations, or
frontend.

**Workspace hazard during this ship:** another agent ran branch switches
that clobbered my untracked working files mid-flight. Recovered by writing
content into `.work/` (which survives branch switches better than
working-tree files) and doing all git ops under flock. Committed
immediately after writing files to make subsequent clobbers harmless.

**Integration contract:** documented in `.squad/decisions/inbox/frost-changsha-dealing-ceremony.md`.
Bishop's runtime must call: `Start(dealer)` at game start (manual mode),
`ApplyDiceRoll(state, dice)` on dice action, and
`ValidateAndApplyPickup(state, seat, count)` on each pickup. Runtime owns
tile-id assignment from wall slots; the ceremony only computes turn order
+ counts + phase.

**Test count:** baseline 5145 + 76 new = 5221 expected; actual 5223 (includes
2 from other intervening merges), 5220 pass / 1 pre-existing W9 cron fail / 2
skipped.

**Commits:**
- Feature branch: `feat/changsha-dealing-ceremony` → `15fa72d`
- Squash-merged to main as `85b8ed6`

📌 Team update (2026-05-27T22:00:00Z): Wave 4 — Dealing ceremony rebuild. Shipped pure-function Changsha dealing ceremony rule engine at Changsha/Dealing/ChangshaDealingCeremony.cs. Public API: Start(dealerSeat) → WaitingForDice; ApplyDiceRoll(state, dice[]) → PickingFour; ValidateAndApplyPickup(state, seat, count) → validation result or new state. Invariants: pure-function transducer (no mutation), programmer errors throw (ArgumentException/InvalidOperationException), runtime violations surface as ChangshaDealingResult { Valid=false, RejectReason="…" }. Turn order: CCW from dealer (dealer + i) % 4. Final hands: 14 (dealer) / 13 (others). Tests: 28 methods, 76 cases under xunit Theories covering Start, ApplyDiceRoll, ComputeStartingWall (15 cases), ComputeBreakIndex (11 cases), ValidateAndApplyPickup, full deal sequence, phase transitions (17 pickups), purity assertions, all ✅. Sibling to Bishop's runtime state machine (intentional clean-room reimplementation as canonical rule spec). Suggested follow-up (Bishop's lane): Consolidate runtime to call this engine, store returned ChangshaDealingState on game state. Full suite 5219 tests pass.

---

## Wave-K — Scoring End-to-End Audit (2026-05-29)

**Audit scope:** End-to-end Hu → `FanCalculator` → `ScoreResult` → `PlayerStats`
pipeline + bot-strength sanity check. Report at
`.squad/decisions/inbox/frost-scoring-audit.md`.

**Findings**

- Fan catalog: 14 fans defined, 12 reachable in pure Changsha (2 correctly
  variant-gated to `ExpandedChinese`). All 12 reachable fans have unit-test
  coverage; the variant-gated pair has gating tests.
- Hu → score → persistence pipeline is intact. New
  `HuToScoreToPersistenceTests` drives the real `ChangshaGameStateMachine`
  through self-draw Hu and discard-Hu and asserts the resulting `PlayerStats`
  row lands in SQLite with the expected `GamesWon` / `LastGameAt` / `TotalScore`
  / `LongestWinStreak`.
- Bot-strength simulation: 4×Master produces 20 Hu / 30 hands, 4×Easy
  produces 18 Hu / 30 hands, Master@seat0-vs-3×Easy produces a clean +9 win
  delta for the Master seat. The strategy abstraction is observable.

**New tests (all passing)**

- `Changsha/Acceptance/HuToScoreToPersistenceTests.cs` — 3 tests
  - `SevenPairsSelfDrawHu_ScoresAndPersistsWinnerStats` — BasePoints=30,
    persists row.
  - `DiscardHu_ScoresAndPersistsWinnerStats` — 7-pair shape completing on
    dealer's discard; persists row with `SelfDraw` fan absent.
  - `RepeatedHu_AccumulatesWinsAndStreak` — 3 self-draws → streak=3.
- `Changsha/Bots/BotStrengthSimulationTests.cs` — 3 tests, permanent
  unskipped sibling of `BotSimulationLog.cs`.

**Pitfall captured for future Frost / Bishop work**

`PlayerProfileService.RecordGameCompletedAsync` skips IDs starting with
`bot-` (PlayerProfileService.cs:256). `ChangshaGameStateMachine.CreateGame`
seeds bot seats with exactly that prefix. Any test asserting persistence
**must** overwrite seat `PlayerId` to a non-bot ID before invoking the
persistence hook, or the test will pass-by-vacuum (no row written and no
assertion that catches it).

**Lane discipline**

Touched only test files under `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/`
and two squad-state files (`.squad/decisions/inbox/frost-scoring-audit.md`,
this file). No production code changed.

**Regression**

61 / 61 pass on
`~FanCalculator|~FanCatalogIntegration|~HuToScoreToPersistence|~BotStrengthSimulation|~BotContextualHu|~ScoringService`.

**Suggested follow-ups (handed to next Frost wave)**

1. Variant switcher to enable `FanVariant.ExpandedChinese` + 144-tile deck.
2. End-to-end persistence test for robbing-kong Hu (currently only the fan
   detection is covered in isolation).
3. Persistence-side stacked-fan test (HeavenlyHand + FullFlush) confirming
   `HighestSingleGameScore` increments by the correct stacked total.

📌 Team update (2026-05-29T00:00:00Z): Wave K — Scoring end-to-end audit. Verified Hu → FanCalculator → ScoreResult → PlayerStats pipeline is intact. New tests: HuToScoreToPersistenceTests (3, self-draw + discard + streak, real SQLite persistence) and BotStrengthSimulationTests (3, permanent 30-hand bot-strength simulation). Audit memo at .squad/decisions/inbox/frost-scoring-audit.md with fan coverage matrix (12/12 reachable fans tested, 2 variant-gated for future ExpandedChinese deck), per-seat bot simulation numbers (4×Master: 20 Hu / 30 hands; Master@seat0 banks +9 wins vs Easy peers), and persistence-side gaps for follow-up. **Pitfall for all squad members:** PlayerProfileService.cs:256 skips PlayerIds starting with "bot-" — persistence tests must overwrite the seed IDs the state machine plants or assertions pass-by-vacuum.

📌 Team update (2026-05-29T19:30:00Z): Wave L — Claim window doesn't open for local seat. Investigated Vasquez integration-audit gate D1 ("claim overlay never appears in a 4-Hard-bot game"). Found **two** layered bugs + one audit-premise issue. **BUG #1:** `ChangshaToAutotableTranslator` always emitted `deadlineUnixMs: 0` — both Ferro overlay and side-panel compute `remaining = deadline - Date.now()`, so 0 made them auto-pass / auto-hide instantly. Fix: added `OpenedAtUnixMs` to `ChangshaClaimWindow`, set it on every claim-window open (`Discard` + `DeclareAddedKong`), plumbed `ClaimWindowTimeoutMs` from `IOptions<ChangshaRuntimeOptions>` through `AutotableConnectionManager` into both `Translate` call sites. **BUG #2:** `EncodeClaimWindow` wrote `CollectionEntry.Key` as an `int`, which the JSON converter serialised as a bare number. The frontend `Collection<K,V>` is a `Map`, where `Map.get(0) !== Map.get("0")`, and every consumer (`game-ui.ts.sendClaim`, overlay, side-panel) keys by `String(selfSeat)`. Net effect: overlay's listener never matched any wire entry and `activeClaim` stayed `null`. Fix: stringified the seat key on encode (`EncodeClaimWindow` + `EncodeClaimWindowClosed`). **Audit premise:** Vasquez's D scenario used `botCount=4`, which fills every seat with a bot and makes the viewer a spectator — spectators are by-design never claim-eligible. Hand-off to Vasquez to update D to `botCount=3` + explicit takeSeat. Defensive frontend guards (`deadline <= 0` → "no client countdown" instead of "expired now") added in both Ferro overlay (`ui/claim-window-overlay.ts`) and side-panel (`game-ui.ts`). 4 new translator tests + updated key-shape test (50/50 pass). End-to-end verified via `playtest-artifacts/frost-claim-window-verify.spec.mjs` — observed `claim` entry with non-zero deadline, overlay class `ferro-claim-overlay-visible`, `display: grid`. Memo: `.squad/decisions/inbox/frost-claim-window.md`. **Pitfall for all squad members:** when you wire a new Collection consumer, never rely on numeric-vs-string Map keys matching — coerce both to a single type. `client.claim.set(String(seat), …)` (local writes) and a backend-side `int` key would silently create two separate map entries.

📌 Team update (2026-05-29T20:00:00Z): Wave M — Backend deal-emit verdict for Stephen's "dealing seems very whacky" screenshot. **VERDICT: backend mixed — one real bug found + fixed; remaining visual symptoms are frontend (Hicks's lane).** Evidence: live WS capture against the running backend with Stephen's exact URL params (`dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&seat=0`) — hand counts correct (14/13/13/13), discards correctly empty, but wall counts were **28 / 27 / 0 / 0** (all 55 post-deal wall tiles packed into seats 0+1, seats 2+3 walls physically empty). **Root cause:** `AutotableSlotMap.EnumerateWallSlotsInOrder` was seat-major, so packing the 55-tile remainder col-major within seat 0 first stuffed it before reaching seats 2/3. **Fix:** flipped to col-major-across-seats — for each col yield every seat × 2 layers, so 55 tiles now distribute ~13-14 per seat, all 2-high stacks. Pre-deal synthesized 108-tile wall is unchanged (every slot still filled). **Tests:** +3 regression tests (`WallTiles_DistributedAcrossAllFourSeats`, `WallTiles_StackedTwoHighAtEverySeat`, `NoPhantomDiscards_BeforeAnyDiscardEvent`) pin the contract going forward. Full suite: 5263/5267 pass; 2 failures (`MultiGameRoutingTests.LateJoin_…`, `VasquezW9SelfLaneTests.NightlyCron…`) are **pre-existing on baseline** (verified by `git stash` baseline run), unrelated to deal-emit. Other Stephen symptoms (only 1 hand tile visible, corner wedges, floating labels) confirmed via WS dump to NOT be backend bugs — handed off to Hicks. Memo: `.squad/decisions/inbox/frost-backend-deal-emit-verdict.md`. **Pitfall for all squad members:** translator slot enumeration order is load-bearing whenever the authoritative collection is smaller than the slot capacity — seat-major orders silently strand entire seats once the source list is partial. Default to col-major-across-seats when downstream rendering must remain visually balanced.

## Team updates

📌 **2026-06-01** — Broken-deal response: Backend fix — column-major wall enumeration (was seat-major, packed all 55 tiles into seats 0+1) — commit `99c1af0`.

📌 **2026-06-01T13:41Z** — Wave N continued — `wall.13.0@2` fence-post final diagnosis & regression tests (commit `165166d`). Stephen reported a page error after my `99c1af0` (backend col-major enum) collided with Hicks's `b4c82ec` (frontend per-seat `row(13)`/`row(14)` setup-slots layout). Task brief proposed "option 1: backend caps per-seat". **Diagnosis: backend was already capping correctly** — `EnumerateWallSlotsInOrder` skips `col >= WallStackCount(seat)` and `WallSlot` throws on out-of-range col. No backend code path can emit `wall.13.0@2`. Real root cause: **frontend `src/frontend/autotable-src/src/setup-deal.ts` DEALS.CHANGSHA table** (Hicks's lane) still uses `['wall.1.0', 2, 26]` for seats 2/3, which walks `slotNames[2..27]` = `wall.1.0 .. wall.13.1`. With Hicks's per-seat split, slots `wall.13.{0,1}@{2,3}` don't exist → `setup.ts:256` throws `slot not found: wall.13.0@2`. Pitfall captured: Playwright `pageerror` parses thrown strings into `name`+`message` at the first `:` — `err.message` is the LAST template-literal interpolation, not the whole thrown string. Empirically verified via a 10-line repro. Backend-side regression (this commit): 5 new tests + 1 helper in `AutotableTranslatorTests.cs` pin both pre-deal (synthesized) and post-deal (authoritative) wall paths against the over-limit slot patterns the task brief called out; iterator-direct test `EnumerateWallSlotsInOrder_NeverYields_OverLimitTuples` pins the iterator independently. All 35 translator tests pass. Lane discipline kept — did NOT touch frontend. Hand-off memo `.squad/decisions/inbox/frost-wall-fence-post-fix.md` contains the 6-line fix for Hicks's setup-deal.ts and the bundle-rebuild + playtest verification recipe. Hicks subsequently applied the patch in round 3 (commit `ff096ff`) and validated across 3 playtests: **ZERO page errors end-to-end. Game is visually + functionally playable.** My regression tests now guarantee this fence-post can't slip back in via backend.

