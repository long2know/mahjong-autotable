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

### 2026-05-05: Changsha Mahjong Definitive Audit

**Task:** Comprehensive rules-engine audit to determine 100% completion status of Changsha implementation.

**Method:**
- Inventoried all domain code: ruleset, evaluators, detectors, settlement calculator
- Cross-referenced against authoritative sources (Baike, MahjongPros, Reddit)
- Verified API integration for each domain feature
- Ran full test suite (2,413 tests) and Changsha subset (1,288 tests)
- Mapped backlog items (D1-D6, M1-M8) to actual code

**Findings:**

**Overall verdict:** 🟡 95% complete — 1 critical integration bug, 5 large features deferred by design

**Core implementation status:**
- **Tile set:** ✅ 108 tiles (3 suits × 9 ranks × 4), correct validation
- **Hand validation:** ✅ 2-5-8 pairs, big-win bypass, 7-pairs, kongs, all patterns
- **Win types:** ✅ All 18 canonical types implemented and tested (Heavenly, Earthly, AllPungs, PureSuit, SevenPairs, LuxurySevenPairs, LastTile, RobbingKong, FullyReliant, SanTong, etc.)
- **Settlement:** ✅ Base payments, bird logic, two-bird support, dealer continuation, round-wind rotation
- **Kong subtypes:** ✅ Concealed/Exposed/Promoted classification + replacement draw
- **Server authority:** ✅ Last-tile detection, fully-reliant tracking, robbing-kong broadcast

**Critical gap found:**
- **D5 Integration Bug:** `WinTypes` parameter exists in domain but is NOT passed from API to settlement calculator. LuxurySevenPairs ×2 multiplier will never trigger in production despite domain logic being correct and tested.
- **Location:** `/win` endpoint L1016-1025 (player path) and L1921-1927 (bot path)
- **Fix:** One-line change × 2 paths: add `WinTypes: changshaEvaluation.WinTypes` to `ChangshaSettlementRequest` constructor
- **Impact:** Rare but incorrect payouts for LuxurySevenPairs hands (dealer loser should pay 2× but pays 1×)

**Deferred features (by design per 2026-04-17 decision):**
- M1: Claim arbitration (no `/claim` endpoint) — large scope
- M2: Chow from prior-player only — pairs with M1
- M6: Multi-winner list representation — DTO restructure
- M7: Missed-win lock — needs M1 pass semantics
- M8: Settlement ledger persistence — DB migration

**Test coverage:**
- 1,288 / 1,288 Changsha tests passing ✅
- 2,413 / 2,413 total tests passing ✅
- Domain coverage: comprehensive (all win types, patterns, settlement scenarios)
- API coverage: good (integration paths tested); missing: D5 WinTypes parameter test

**Recommendations:**
1. **Immediate (P0):** Fix D5 integration bug (trivial 1-line change)
2. **Phase 2:** Implement M1 claim arbitration (unblocks M2, M7)
3. **Phase 2:** Implement M6 multi-winner representation (unblocks D2)
4. **Optional:** M8 settlement ledger (non-blocking)

**Key insight:** The domain layer is production-ready and correct. The API layer has one integration bug (D5) and five intentionally deferred large features. The implementation is fully playable for single-winner scenarios; D5 bug affects payout fairness for rare hands but doesn't block gameplay.

**Deliverables:**
- Full audit report: `.squad/decisions/inbox/ishikawa-changsha-deep-audit.md`
- Updated backlog: `.squad/backlog-changsha-parity.md` (marked D1/D3/D4/D6/M3/M4/M5 as shipped, D5 as partial, M1/M2/M6/M7/M8 as deferred)
- Test evidence: all tests green, 1,288 Changsha-specific tests covering domain logic
