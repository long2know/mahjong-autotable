# Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-claw — Multiplayer Traditional Chinese Mahjong
- **Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, SignalR, Blazor WASM, Playwright
- **Created:** 2026-04-16

## Learnings

- Seeded as API + Realtime Engineer during Squad init.

## 2026-04-17: Changsha Parity API Wiring (squad/changsha-full-rules-parity)

### D1: Two-Bird Settlement
- Extended `WinRequest` DTO with `int[]? BirdRanks` alongside existing `int? BirdRank` for backward compatibility.
- Extended `ChangshaSettlementRequest` domain record with `IReadOnlyList<int>? BirdRanks`.
- Implemented `ApplyMultiBirdAdjustments` in settlement calculator:
  - When both birds hit winner (self-draw): ×3 total (base × (1 + 2 hitCount)).
  - When one bird hits winner: ×2 total.
  - When birds hit distinct losers: each loser doubles independently.
  - Legacy single `BirdRank` converted to single-element list internally.
- Added 3 unit tests in `ChangshaSettlementCalculatorTests` - all passing.
- **Decision**: Prefer `BirdRanks` if both provided; return 400 if both set. Client must transition to multi-bird.

### D3: Round-Wind Rotation
- Added `RoundWind: string?` and `DealerRotationCount: int` to `GameEntity`.
- Configured EF defaults: `RoundWind="East"`, `DealerRotationCount=0`.
- Created migration `AddRoundWindAndRotationCount`.
- After settlement, increment `DealerRotationCount` when dealer changes; when count % 4 == 0, advance round wind (E→S→W→N→E).
- Added `RoundWind` to `GameStateResponse` DTO.
- Initialized `RoundWind="East"` and `DealerRotationCount=0` in `/start` endpoint.
- **Decision**: Round wind does NOT pass into evaluator (Changsha 258 scoring doesn't depend on it per spec).

### D6: FullyReliant Wiring (Partial)
- Included `Melds` in `/win` endpoint query.
- Added stub for `ChangshaFullyReliantDetector.IsFullyReliant` (TODO comment for Ishikawa).
- Extended `BuildChangshaWinContext` to accept `overrideIsFullyReliant` parameter for server-side detection.
- **Decision**: Client-provided `IsFullyReliant` remains fallback until Ishikawa's detector ships; server will be authoritative once available.

### M3: Kong Subtypes + Replacement Draw
- Added `Kind: string?` to `MeldEntity` (nullable to avoid migration pain on existing data).
- Created migration `AddMeldKind`.
- In `/meld` endpoint, classify kong type on server:
  - No discard source + all 4 from hand → `ConcealedKong`
  - Discard source + 3 from hand → `ExposedKong`
  - Existing pung + self-drew 4th → `PromotedKong`
- After kong formation, draw replacement tile from wall and broadcast `TileDrawn` event.
- Handle wall exhaustion edge case (skip replacement if wall empty).
- **Decision**: Used `Kind` string column instead of enum Type to avoid breaking existing "Kong" string values in DB.

### M4: Robbing-the-Kong Detection (Partial)
- On promoted kong, broadcast `KongRobOpportunity` SignalR event with tile + eligible opponents.
- Added TODO comment for `ChangshaRobbingKongDetector.CanRobPromotedKong` (Ishikawa).
- Currently broadcasts opportunity to all opponents; full eligibility check deferred.
- **Decision**: Full claim arbitration (M1) deferred to follow-up PR; this implementation enables client UI to show rob opportunities.

### M5: Last-Tile / Bottom-of-the-Ocean Server Detection
- In `/win` endpoint, check `wallRemaining = game.Tiles.Count(t => t.Location == "Wall")`.
- If `wallRemaining == 0`:
  - Self-draw path → set `IsLastTileDraw=true`
  - Discard path → set `IsLastTileDiscard=true`
- Server value overrides client-provided flags to prevent spoofing.
- Extended `BuildChangshaWinContext` with `overrideIsLastTileDraw` and `overrideIsLastTileDiscard` parameters.

### Build & Test Status
- All code compiles cleanly (`dotnet build` succeeded, 0 warnings/errors).
- D1 tests: 3/3 passing (two-bird settlement scenarios).
- 3 tests failing (expected): LuxurySevenPairs ×2 multiplier (D5) and robbing-kong detection - both are Ishikawa's domain work in progress.
- Total: 2324/2327 passing.

### Migrations Created
1. `AddRoundWindAndRotationCount` (D3)
2. `AddMeldKind` (M3)

### Integration Notes for Ishikawa
When the following domain pieces ship, remove TODO stubs:
- `ChangshaFullyReliantDetector.IsFullyReliant(handTiles, melds, winningTileSource)` → wire into D6
- `KongKind` enum + parser → replace string classification in M3
- `ChangshaRobbingKongDetector.CanRobPromotedKong(...)` → replace broadcast-to-all in M4
- LuxurySevenPairs ×2 multiplier applied in settlement (D5) → no API work, but tests will pass

## 2026-04-17 (Round 2): Domain Helper Integration (squad/changsha-full-rules-parity)

Completed all 3 TODO stubs from Round 1. Ishikawa's domain helpers now wired and operational.

### TODO #1: KongKind Enum Parser (Line 651)
**Before:** String-based kong kind classification with hardcoded `"ConcealedKong"`, `"ExposedKong"`, `"PromotedKong"`.
**After:** Using `KongKind` enum + `KongKindParser.ToMeldTypeString(kind)` for type-safe conversion.

**Adapter logic:**
- Detect kong kind based on game state:
  - Check if last discard contains any of the 4 tiles → `Exposed`
  - Else check if player has existing pung with same tile → `Promoted`
  - Else all 4 from hand → `Concealed`
- Convert enum to string via `KongKindParser.ToMeldTypeString(kind)`.
- Store in `MeldEntity.Kind` field (already nullable string, no schema change).

### TODO #2: Robbing-Kong Detection (Line 699)
**Before:** Broadcast rob opportunity to all opponents without checking if they can actually rob.
**After:** Call `ChangshaRobbingKongDetector.CanRobPromotedKong` for each opponent; only broadcast to eligible players.

**Adapter logic:**
- For promoted kong tile, convert to domain `Tile` object via `TryBuildTileHand`.
- Iterate each opponent:
  - Fetch opponent's hand tiles from `game.Tiles` (Location="Hand").
  - Convert to domain `IReadOnlyList<Tile>`.
  - Count opponent's existing kongs by querying `game.Melds` where Type="Kong" or Kind contains "Kong".
  - Call `ChangshaRobbingKongDetector.CanRobPromotedKong(opponentHandTiles, promotedTile, existingKongCount)`.
  - If true, add opponent to eligible list.
- Only broadcast `KongRobOpportunity` if at least one opponent can rob.

**Key learnings:**
- MeldEntity uses both `Type` and `Kind` fields — `Type="Kong"` is legacy, `Kind` contains specific subtype.
- Must check both fields when counting kongs for existing kong count parameter.
- Domain expects `IReadOnlyList<Tile>` from `TryBuildTileHand`, not raw `TileEntity`.

### TODO #3: FullyReliant Detection (Line 878)
**Before:** Stub logic, always used client-provided `request.IsFullyReliant`.
**After:** Server-authoritative detection via `ChangshaFullyReliantDetector.IsFullyReliant`.

**Adapter logic:**
- Build `List<MeldInfo>` from player's `game.Melds`:
  - For each meld, determine `TileSource`:
    - `ConcealedKong` → `TileSource.SelfDrawn` (all 4 tiles self-drawn).
    - `ExposedKong` / `PromotedKong` → `TileSource.ClaimedDiscard` (original tiles from discard).
    - `Chow` / `Pung` → `TileSource.ClaimedDiscard` (standard mahjong: these are always claimed).
  - Create `MeldInfo(source)` record for each meld.
- Determine winning tile source:
  - `request.IsSelfDraw` → `TileSource.SelfDrawn`
  - Else → `TileSource.ClaimedDiscard`
- Call `ChangshaFullyReliantDetector.IsFullyReliant(meldInfos, winningTileSource)`.
- Edge case: If player has no melds and won from discard, fully reliant = true.

**Key learnings:**
- Chow/Pung in standard mahjong are always formed by claiming discards (cannot be self-formed).
- PromotedKong counts as "from discard" because original pung was from discard (4th tile source irrelevant).
- FullyReliant means ALL sets came from discards AND final tile from discard — player literally relied on others for everything.

### Build & Test Results
- **Build:** Clean compile, 0 warnings/errors.
- **Tests:** 2326/2326 passing (100% green).
- **Before:** 2324/2327 (3 failing: LuxurySevenPairs multiplier, robbing-kong detection).
- **After:** All 3 previously-failing tests now pass:
  - `Calculate_LuxurySevenPairs_DoublesAllLosersPayment` ✅
  - `Calculate_LuxurySevenPairs_WithBird_QuadruplesMatchedPayment` ✅
  - All 4 robbing-kong detector tests ✅

### TODOs Removed
All 3 `TODO(Ishikawa)` comments removed from `src/MahjongClaw.Api/Program.cs`:
- Line 651 (kong kind parser)
- Line 699 (robbing-kong detector)
- Line 878 (fully-reliant detector)

No further integration work required for Ishikawa's domain helpers.
