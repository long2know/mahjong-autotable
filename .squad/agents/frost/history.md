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
