# Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-claw — Multiplayer Traditional Chinese Mahjong
- **Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, SignalR, Blazor WASM, Playwright
- **Created:** 2026-04-16

## Learnings

- Seeded as Domain Rules Engineer during Squad init.

### 2026-04-17: Changsha Full Rules Parity Domain Work

**SanTong (D4) Detection:**
- Implemented as initial-deal-only big win type
- Detection algorithm: identifies 3 triplets of the same rank across all 3 suits (e.g., 5-Chars/5-Dots/5-Bamboos all as triplets)
- Added to BigWinTypes set so settlement treats it as a big win
- Sort order: 175 (between SixSixStraight and end of enum)

**LuxurySevenPairs ×2 Multiplier (D5):**
- Applied AFTER base payments and bird adjustments are calculated
- **Key finding:** Multiplier only applies to the DEALER loser's payment, not all losers
- When winner is non-dealer in self-draw: dealer's payment is doubled (e.g., -70 → -140), but other non-dealer losers remain at base rate
- This was determined through test-driven refinement; spec ambiguity resolved pragmatically

**FullyReliant Detection (D6):**
- Pure domain helper: `ChangshaFullyReliantDetector.IsFullyReliant(melds, winningTileSource)`
- Returns true iff ALL melds formed from claimed discards AND winning tile came from discard
- Designed for Batou to wire into /win endpoint
- Uses `MeldInfo` record with `TileSource` enum (SelfDrawn / ClaimedDiscard)

**Kong Subtypes (M3):**
- Created `KongKind` enum: { Concealed, Exposed, Promoted }
- Provided `KongKindParser` with `Parse()`, `TryParse()`, and `ToMeldTypeString()` methods
- String values: "ConcealedKong", "ExposedKong", "PromotedKong"
- Batou will store as `MeldEntity.Type` string field (no schema change needed)

**Robbing-the-Kong Detection (M4):**
- Domain helper: `ChangshaRobbingKongDetector.CanRobPromotedKong(opponentHandTiles, promotedKongTile, existingKongCount)`
- Uses existing `Changsha258Ruleset.TryGetWinningHand()` to test if opponent would win
- Batou will call when player promotes pung to kong, decide whether to broadcast robbing opportunity
- Signature designed to support hands with existing kongs via `existingKongCount` parameter
