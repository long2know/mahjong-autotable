# Changsha Backend Gap Report

> Bishop audit — produced alongside Vasquez's canonical Changsha spec (`docs/rules/changsha-spec.md`).
> Baseline: build ✅ 0 warnings, 38/38 tests pass.

---

## A. Current State Inventory

| Project | Path | Role | Changsha Relevance |
|---|---|---|---|
| **Mahjong.Autotable.Api** | `src/backend/src/Mahjong.Autotable.Api/` | Monolith API — game engine, persistence, HTTP endpoints, static-file hosting | **Primary target.** All Changsha logic lives here today. |
| **Mahjong.Autotable.Api.Tests** | `src/backend/tests/Mahjong.Autotable.Api.Tests/` | xUnit integration + unit tests | Must grow with Changsha-specific test cases. |
| **Mahjong.Autotable.slnx** | `src/backend/Mahjong.Autotable.slnx` | Solution file binding both projects | No change needed. |

> **Note:** There is no separate `*.Rules.*` or `*.Game.*` project. All domain logic is co-located in the `Tables/` folder of the API project.

---

## B. Domain Model Audit

### Existing Types

| Type | File | Current Responsibility | Changsha Alignment | Notes |
|---|---|---|---|---|
| `TableGameState` | `Tables/TableGameState.cs` | Root aggregate — wall, hands, seats, melds, discards, action log, claim window, win state | **Partial.** Covers generic draw/discard/claim loop but lacks Changsha-specific fields (banker, dice, round/wind, scoring). | Missing: `BankerSeatIndex`, `DiceRollResult`, `RoundWind`, `ScoreSheet`. |
| `TableSeatState` | `Tables/TableGameState.cs` | Seat identity (index, human/bot, player ID) | **OK** for seat assignment. | Missing: seat wind assignment. |
| `TableSeatHandState` | `Tables/TableGameState.cs` | Concealed tiles per seat | **OK.** | — |
| `TableSeatMeldState` / `TableMeldState` | `Tables/TableGameState.cs` | Exposed melds (pung/kong/chow) per seat | **Partial.** Tracks melds but no concealed-kong distinction. | Changsha needs concealed-kong vs exposed-kong differentiation for scoring. |
| `TableDiscard` | `Tables/TableGameState.cs` | Single discard record | **OK.** | — |
| `TableClaimOpportunity` / `TableClaimWindowState` | `Tables/TableGameState.cs` | Claim precedence (hu > kong > pung > chow) | **Partial.** Includes chow which Changsha forbids. No self-draw hu path. | Chow must be disabled for Changsha; self-draw win needs its own path. |
| `TableClaimType` | `Tables/TableGameState.cs` | Enum: Hu, Kong, Pung, Chow | **Partial.** Chow is invalid in Changsha. | Keep the enum for future rule sets but exclude chow from Changsha claim generation. |
| `TableWinState` | `Tables/TableGameState.cs` | Records winning seat, tile, source | **Partial.** No pattern classification or scoring info. | Needs: `WinPattern`, `FanCount`, `IsSelfDraw`. |
| `TableAction` | `Tables/TableGameState.cs` | Append-only action log entry | **OK.** | — |
| `BotAdvanceResult` / `BotAdvanceStopReason` | `Tables/TableGameState.cs` | Bot loop stop reasons | **OK.** | — |
| `TableStateEngine` | `Tables/TableStateEngine.cs` | Core game loop: create, discard, draw, claim, bot advance, replay | **Partial.** Generic Chinese mahjong loop. No Changsha-specific tile set, dice, banker, scoring, or win-pattern analysis. | Largest gap area — see Section C. |
| `TableStateHasher` | `Tables/TableStateHasher.cs` | SHA-256 canonical state hash | **OK.** Will need updates when new state fields are added. | — |
| `TableStateSerializer` | `Tables/TableStateSerializer.cs` | JSON serialization for `TableGameState` | **OK.** System.Text.Json with enum converters. | — |
| `TableRuleException` | `Tables/TableRuleException.cs` | Structured rule-violation error | **OK.** | — |
| `TableActionErrorCodes` | `Tables/TableActionErrorCodes.cs` | Error code constants | **OK.** May need new codes for Changsha violations. | — |
| `TableSessionEventStore` | `Tables/TableSessionEventStore.cs` | Append-only event persistence + retrieval | **OK.** | — |
| `TableContracts` | `Tables/TableContracts.cs` | API request/response DTOs, seat-view projection | **Partial.** No scoring DTOs, no round-progression DTOs. | — |
| `TableSession` | `Data/Entities/TableSession.cs` | EF Core entity — game session row | **OK.** `RuleSet` defaults to `"changsha"`. | — |
| `TableSessionEvent` | `Data/Entities/TableSessionEvent.cs` | EF Core entity — event row | **OK.** | — |
| `AppDbContext` | `Data/AppDbContext.cs` | EF Core context with two tables | **OK.** | — |
| `DatabaseBootstrapper` | `Data/DatabaseBootstrapper.cs` | SQLite schema bootstrapper (non-migration) | **OK** but fragile for schema evolution. | Consider adding EF migrations for Changsha schema changes. |
| `PersistenceProvider` / `PersistenceOptions` / `ServiceCollectionExtensions` | `Persistence/*.cs` | Multi-provider DB config (SQLite/PostgreSQL/SQL Server) | **OK.** | — |

### Missing Types (needed for Changsha)

| Type | Purpose | Priority |
|---|---|---|
| `ChangshaTileSet` | Changsha tile-set definition: 3 suits × 9 ranks × 4 copies + 4 红中 = 112 tiles (no winds, no other dragons, no flowers) | **Critical** |
| `DiceRoll` | Two-dice roll result determining wall break point | High |
| `BankerState` | Tracks current banker (East), rotation rules | High |
| `RoundProgression` | East/South/West/North round tracking, continuations | Medium |
| `WinPattern` / `ChangshaWinDetector` | Pattern classifier: standard, 七对子, 碰碰胡, 清一色, 全色, 将将胡, etc. | **Critical** |
| `HongZhongWildcard` | 红中 wildcard substitution logic for win detection | **Critical** |
| `ScoringCalculator` | Fan-based scoring with multipliers per pattern | High |
| `ScoreSheet` / `GameScoreState` | Running score across rounds | Medium |
| `SelfDrawWin` | Self-draw (自摸) win path — currently no codepath for tsumo | High |
| `WallBreakState` | Wall segments after dice-based break point | Medium |

---

## C. Behavior Audit

| Changsha Behavior | Status | File Reference(s) | Notes |
|---|---|---|---|
| **Tile set construction** (suits + 红中 only, no winds/dragons/flowers) | ❌ **MISSING** | `TableStateEngine.cs:1292-1304` | Current wall uses 136 tiles (standard Chinese set: 3 suits + winds + dragons). Changsha needs 112 tiles (3×9×4 + 4 红中). `TotalTiles = 136` is hardcoded. |
| **Wall building** (4 walls of stacks) | ⚠️ **PARTIAL** | `TableStateEngine.cs:1292-1304` | Wall is a flat shuffled list. No physical 4-wall / stack structure. Sufficient for gameplay but won't support visual wall display or break-point semantics. |
| **Dice roll for break point** | ❌ **MISSING** | — | No dice roll concept exists. Wall is drawn from the end sequentially. |
| **Initial deal in batches of 4** | ⚠️ **PARTIAL** | `TableStateEngine.cs:70-78` | Deal loop deals 1 tile at a time per seat for 13 rounds. The result is correct (13 tiles each + 14 for seat 0) but not in authentic 4-tile batches. |
| **Banker (East) starting with 14 tiles** | ⚠️ **PARTIAL** | `TableStateEngine.cs:78` | Seat 0 always gets the 14th tile. But there is no banker concept — it is always seat 0, never rotates. |
| **Draw / discard turn** | ✅ **IMPLEMENTED** | `TableStateEngine.cs:537-633` | Full draw-from-wall → discard → next-seat loop with validation. |
| **Pung** | ✅ **IMPLEMENTED** | `TableStateEngine.cs:696-706, 1085` | Pung claim detection and meld execution work correctly. |
| **Kong** | ✅ **IMPLEMENTED** | `TableStateEngine.cs:689-695, 1086` | Kong claim with supplemental draw works. Missing: concealed kong (暗杠) and promoted kong (加杠) paths. |
| **Chow (吃)** — should be **disabled** in Changsha | ⚠️ **WRONG** | `TableStateEngine.cs:708-716, 746-763` | Chow is fully implemented and active. **Changsha forbids chow.** Must be gated by rule-set config. |
| **Self-draw win (自摸)** | ❌ **MISSING** | — | No codepath checks for win after drawing a tile. Only claim-based hu exists. |
| **Claim win (点炮/放炮)** | ⚠️ **PARTIAL** | `TableStateEngine.cs:766-791, 998-1037` | Hu candidate detection exists (`IsHuCandidate`). Win resolution via claim-take-selected works. But the win detector is generic (standard 4×3+2) — no Changsha-specific patterns. |
| **Hand patterns: 七对子 (seven pairs)** | ❌ **MISSING** | — | `IsWinningHand` only checks 4×(3-meld)+pair. Seven pairs is a separate pattern. |
| **Hand patterns: 碰碰胡 (all triplets)** | ❌ **MISSING** | — | Not classified. Currently any valid 4×3+2 hand wins; no pattern tagging. |
| **Hand patterns: 清一色 (one suit)** | ❌ **MISSING** | — | No suit-purity check. |
| **Hand patterns: 全色 (full color / 字一色)** | ❌ **MISSING** | — | No classification. |
| **Hand patterns: 将将胡 (all pairs + pair-only)** | ❌ **MISSING** | — | Not considered. |
| **红中 wildcard substitution** | ❌ **MISSING** | — | 红中 (tile logical index 31 in standard set) is treated as a regular tile. No wildcard/joker substitution in win detection. |
| **Scoring (fan multipliers)** | ❌ **MISSING** | — | No scoring system exists. `TableWinState` records the winner but no fan count or point values. |
| **Banker rotation** | ❌ **MISSING** | — | No banker concept. Seat 0 is always "first" and never rotates. |
| **Round (East/South/West/North) progression** | ❌ **MISSING** | — | Single-hand sessions only. No multi-round game tracking. |
| **Game persistence (survive reconnection)** | ✅ **IMPLEMENTED** | `Data/Entities/TableSession.cs`, `Program.cs` | Full `StateJson` persistence per session. Replay verification via seed + action log. |

**Summary: 3 IMPLEMENTED, 5 PARTIAL, 10 MISSING out of 18 behaviors.**

---

## D. API/Transport Audit

### Current Endpoints

| Method | Path | Purpose | Changsha-Ready? |
|---|---|---|---|
| `GET` | `/api/health` | Health check | ✅ |
| `GET` | `/api/system/persistence` | Persistence provider info | ✅ |
| `POST` | `/api/tables` | Create new table session | ⚠️ Accepts `ruleSet` param but engine ignores it |
| `GET` | `/api/tables/{id}` | Get full table state | ✅ |
| `GET` | `/api/tables/{id}/view?seatIndex=` | Seat-scoped projection (hidden opponent tiles) | ✅ |
| `GET` | `/api/tables/{id}/events` | Event stream retrieval | ✅ |
| `POST` | `/api/tables/{id}/bots/advance` | Advance bot turns | ⚠️ No Changsha rule gating |
| `POST` | `/api/tables/{id}/actions/discard` | Human discard action | ✅ |
| `POST` | `/api/tables/{id}/claims/resolve` | Resolve claim window (pass/take-selected) | ⚠️ Must exclude chow for Changsha |
| `POST` | `/api/tables/{id}/replay/verify` | Replay integrity verification | ✅ |
| `POST` | `/api/tables/{id}/next-hand` | Create next hand from current table | ⚠️ No banker rotation logic |

### Missing Endpoints/Messages

| Endpoint | Purpose | Priority |
|---|---|---|
| `POST /api/tables/{id}/actions/self-draw-win` | Declare tsumo (自摸) win after drawing | **Critical** |
| `GET /api/tables/{id}/score` | Get current scores / fan breakdown | High |
| `POST /api/tables/{id}/actions/concealed-kong` | Declare concealed kong (暗杠) | Medium |
| `POST /api/tables/{id}/actions/promoted-kong` | Promote pung to kong (加杠) | Medium |
| SignalR hub for real-time state push | Currently HTTP-only, no real-time channel | Medium (future) |

---

## E. Bot Audit

**Location:** Bot logic is embedded in `TableStateEngine.cs` — there is no separate bot class.

**Current behavior:**
- `AdvanceBots()` and `AdvanceBotsUntilHumanTurnOrWallExhausted()` drive the bot loop.
- Bot tile selection: `SelectBotDiscardTile()` (line 1319) uses a heuristic scoring system (`ComputeBotTileKeepScore`, line 1336):
  - +6 per duplicate of same logical tile (preserves pairs/triplets)
  - +3 for adjacent tiles in same suit (preserves sequence potential)
  - +1 for tiles 2-away in same suit
  - Honors (logical ≥ 27) get no adjacency bonus
  - Lowest-scored tile is discarded
- Bot claim resolution: auto-resolves `take-selected` when the selected claim belongs to a bot seat.
- **No win declaration by bots** — bots never call self-draw win (tsumo) because the path doesn't exist.

**What needs to change for Changsha:**
1. Bot must check for self-draw win before discarding
2. Chow claim opportunities must be suppressed
3. Heuristic should value 红中 tiles as wildcards (high keep score)
4. Bot should be able to declare concealed kong
5. Consider scoring-aware discard strategy (e.g., pursue 清一色)

---

## F. Persistence Audit

**Is game state durable?** ✅ Yes.
- `TableSession.StateJson` stores the full `TableGameState` as JSON.
- `TableSession.StateVersion` tracks optimistic concurrency.
- `TableSessionEvent` provides an append-only event log per session.
- Replay verification (`ReplayFromSeed` + `VerifyReplayIntegrity`) deterministically re-simulates from seed + action log and compares state hashes.

**Are replays loggable?** ✅ Yes.
- Every action (discard, draw, claim-resolve) is appended to `ActionLog` in state and persisted to `TableSessionEvents` table.
- Events include sequence number, action type, seat index, turn number, tile ID, detail, state version, and state hash.

**Gaps:**
- No multi-round game entity — each hand is a separate `TableSession`. `next-hand` creates a new session rather than continuing a game.
- No score persistence across hands.
- Schema evolution uses manual `ALTER TABLE` bootstrapper (`DatabaseBootstrapper.cs`) rather than EF Core migrations — works for SQLite but may be fragile for production schema changes.

---

## G. Recommended Implementation Plan

| # | Title | Target Files | Risk | Spec Dependency |
|---|---|---|---|---|
| 1 | **Changsha tile set (112 tiles)** | New: `Tables/ChangshaTileSet.cs`; Modify: `TableStateEngine.cs` (`CreateShuffledWall`, `TotalTiles`, `IsChowCandidate`, `IsHuCandidate`) | **Med** — breaks replay integrity for existing sessions using 136-tile wall | Vasquez: tile composition |
| 2 | **Disable chow for Changsha** | `TableStateEngine.cs` (`GetClaimOpportunities`) — gate chow on rule set; `TableGameState.cs` — add `RuleSet` to state | **Low** | Vasquez: chow prohibition confirmed |
| 3 | **红中 wildcard in win detection** | New: `Tables/HongZhongWildcard.cs`; Modify: `IsHuCandidate`, `IsWinningHand` | **High** — wildcard substitution is combinatorially complex | Vasquez: wildcard rules |
| 4 | **Self-draw win (自摸) path** | `TableStateEngine.cs` — add `CheckSelfDrawWin` after `DrawForActiveSeat`; new API endpoint; `TableWinState` — add `IsSelfDraw` | **Med** | Vasquez: self-draw rules |
| 5 | **Changsha win patterns** (七对子, 碰碰胡, 清一色, etc.) | New: `Tables/ChangshaWinDetector.cs` with pattern classifier | **High** — many patterns, each with edge cases | Vasquez: full pattern catalog |
| 6 | **Scoring system (fan multipliers)** | New: `Tables/ChangshaScoringCalculator.cs`; Modify: `TableWinState`, `TableContracts.cs` | **Med** | Vasquez: scoring table |
| 7 | **Banker + dice roll + round progression** | Modify: `TableGameState.cs` (add `BankerSeatIndex`, `DiceRoll`, `RoundWind`); `TableStateEngine.cs` (`CreateInitialState`, next-hand flow) | **Med** | Vasquez: banker rotation rules |
| 8 | **Concealed kong (暗杠) + promoted kong (加杠)** | `TableStateEngine.cs` — new action paths; `TableMeldState` — add `IsConcealed` flag; new API endpoints | **Low** | Vasquez: kong variants |
| 9 | **Multi-round game entity + score persistence** | New: `Data/Entities/Game.cs`; Modify: `AppDbContext.cs`, `Program.cs` | **Low** | None — infrastructure |
| 10 | **Bot Changsha awareness** | `TableStateEngine.cs` (`SelectBotDiscardTile`, `AdvanceBots`) — self-draw win check, 红中 value, no-chow | **Med** — depends on items 1-5 | Items 1-5 |

**Suggested merge order:** 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10

---

## H. Build / Test Baseline

```
$ dotnet build src/backend/Mahjong.Autotable.slnx
Build succeeded.    0 Warning(s)    0 Error(s)

$ dotnet test src/backend/Mahjong.Autotable.slnx
Test Run Successful.
Total tests: 38
     Passed: 38
 Total time: 1.30 Seconds
```

### Test Coverage by Area

| Area | Test Count | Files |
|---|---|---|
| TableStateEngine (core loop) | 28 | `TableStateEngineTests.cs` |
| Claim resolution API (integration) | 4 | `ClaimResolutionApiTests.cs` |
| Seat-view projection | 2 | `TableSeatViewProjectionTests.cs` |
| Event store persistence | 2 | `TableSessionEventStoreTests.cs` |
| **Changsha-specific rules** | **0** | — |
| Scoring | 0 | — |
| Win patterns | 0 | — |
| Banker / dice / round | 0 | — |

---

*Generated by Bishop (Backend Dev) — audit wave for Changsha implementation planning.*
