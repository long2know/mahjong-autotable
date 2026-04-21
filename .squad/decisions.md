# Squad Decisions

## Active Decisions

### 2026-04-16: Behavior-changing work requires tests
**By:** Stephen Long (via Squad setup)
**What:** Any gameplay/rules/API behavior change must include test additions or updates in the same workstream.
**Why:** Prevent regressions and keep confidence high as rules and realtime flows evolve.

### 2026-04-16: API/DTO changes require client and docs synchronization
**By:** Stephen Long (via Squad setup)
**What:** Any API contract or DTO update must include corresponding client update and docs update before completion.
**Why:** Keep server/client compatibility intact and avoid stale integration docs.

### 2026-04-16: Non-obvious decisions must be captured in Squad memory
**By:** Stephen Long (via Squad setup)
**What:** Team members record non-obvious technical/process decisions in `.squad/decisions/inbox/*.md` for Scribe merge.
**Why:** Preserve rationale for future sessions and reduce repeated re-discovery.

### 2026-04-17: Changsha Rules Parity Plan
**By:** Kusanagi (Lead Architect)  
**Date:** 2026-04-17  
**Branch:** `squad/changsha-full-rules-parity`

**Context:**  
Stephen requested completion of all Changsha rules parity gaps in a single PR. The audit identified 14 items (D1-D6 divergences, M1-M8 missing features) plus a broken e2e test. After reviewing code locations, dependencies, and effort estimates, applying the principle: **depth over breadth — a smaller set done correctly beats 14 half-done.**

**Decision:**

**IN (10 items):**
- D1: Two-Bird Settlement — additive DTO field with backward compat
- D3: Round-Wind Rotation — small entity extension, no migration pain
- D4: San Tong Win Type — evaluator-only, no API change
- D5: LuxurySevenPairs ×2 — settlement calc tweak
- D6: FullyReliant Detection — meld data exists, compute at `/win`
- M3: Kong Subtypes — string type field, no schema change; replacement draw essential
- M4: Robbing-Kong (partial) — detection + flag; interruptive claim flow deferred
- M5: Last-Tile Server Detection — wall count check, trivial
- BONUS: E2E Fix — 2-line test fix, unblocks CI

**DEFERRED (5 items):**
- D2: Multi-Winner Dealer — depends on M6 winner list representation
- M1: Claim Arbitration — large scope (endpoint + state + UI), dedicated PR
- M2: Chow Prior-Player — enforcement only useful with M1
- M6: Multi-Winner Proper — significant DTO + settlement restructure
- M7: Missed-Win Lock — requires M1 claim window for pass semantics
- M8: Settlement Ledger — DB schema migration, additive not blocking

**Execution:** Parallel phases (domain + API + QA) → integration → validation (full suite + e2e).

**Acceptance Criteria:**
- All IN items implemented with tests
- `dotnet test` green (2297+ tests)
- Playwright e2e 10/10
- No breaking API changes (backward compat for single BirdRank)

### 2026-04-17: Changsha Parity API Wiring
**By:** Batou (API + Realtime Engineer)  
**Date:** 2026-04-17  
**Branch:** `squad/changsha-full-rules-parity`

**Summary:**  
Completed API/persistence work for D1, D3, D6 (partial), M3, M4 (partial), M5. All endpoints, DTOs, database migrations, and SignalR events implemented. Build green; 2324/2327 tests pass (3 failures are Ishikawa's pending domain work).

**Key Decisions:**

1. **D1 Two-Bird Backward Compat:**  
   - Existing `int? BirdRank` still accepted; converts to single-element list internally
   - Validation: returns 400 if both `BirdRank` and `BirdRanks` provided
   - Rationale: Gradual client migration path

2. **D3 Round Wind Storage:**  
   - New `GameEntity` fields: `RoundWind: string?` (default "East") + `DealerRotationCount: int`
   - Advancement logic: E→S→W→N every 4 dealer rotations
   - EF migration: `AddRoundWindAndRotationCount`

3. **M3 Kong Replacement Draw:**  
   - Auto-draw from wall after any kong (concealed/exposed/promoted)
   - Handles wall exhaustion: skip draw if no tiles left
   - Broadcasts `TileDrawn` event to client

4. **M4 Robbing-Kong Broadcast:**  
   - New SignalR event: `KongRobOpportunity` with eligible opponent list
   - Emitted after promoted kong; full claim arbitration deferred to M1
   - Rationale: Detect + broadcast now; interruptive flow in Phase 2

5. **M5 Last-Tile Server Authority:**  
   - Server override: checks `wallRemaining == 0` at `/win` call
   - Sets `IsLastTileDraw`/`IsLastTileDiscard` server-authoritative
   - Prevents client spoofing

**Database Migrations:**
- `AddRoundWindAndRotationCount`: RoundWind + DealerRotationCount
- `AddMeldKind`: Kind field to MeldEntity

**Integration Checklist (for Ishikawa):**
- Replace `TODO(Ishikawa)` stubs with real calls to domain helpers
- Wire `ChangshaWinTypeEvaluator` results to settlement calculator
- Integrate `ChangshaFullyReliantDetector` in `/win` meld analysis
- Use `ChangshaRobbingKongDetector.CanRobPromotedKong()` in `/meld` promotion path

**Risk Notes:**
1. Two-bird backward compat requires coordinated client migration
2. Kong replacement draw silently skips if wall exhausted (UI notification recommended)
3. Robbing-kong broadcast currently non-authoritative (full claim flow deferred to M1)

### 2026-04-17: Changsha Domain Rules Additions
**By:** Ishikawa (Domain Rules Engineer)  
**Date:** 2026-04-17  
**Branch:** `squad/changsha-full-rules-parity`

**Summary:**  
Completed domain-layer rules work for Changsha parity PR. All changes additive and backward-compatible. No API contracts modified (Batou owns integration).

**Additions:**

1. **D4: SanTong (Three Same) Win Type**
   - Detection: 3 triplets of same rank across all 3 suits at initial deal
   - Classification: big-win type
   - Implementation: `ChangshaWinTypes.cs` enum + `ChangshaWinTypeEvaluator.cs` detection
   - Tests: 3 new tests, all passing

2. **D5: LuxurySevenPairs ×2 Multiplier**
   - Applied to dealer loser's payment only (not all losers)
   - Behavior: doubles dealer payment after bird adjustments
   - Stacks multiplicatively with bird doublings (×2 LuxurySevenPairs + ×2 bird = ×4 total)
   - Integration: pass `WinTypes: IReadOnlyList<ChangshaWinType>?` to settlement calculator
   - Tests: 2 new tests

3. **D6: FullyReliant (Quan Qiu Ren) Detection Helper**
   - API: `IsFullyReliant(IReadOnlyList<MeldInfo> melds, TileSource winningTileSource) → bool`
   - Returns true iff all melds + final tile came from opponent discards
   - Pure domain helper (no side effects)
   - Integration: track meld source in API; call helper at `/win`
   - Tests: 5 new tests

4. **M3: Kong Subtypes (Concealed/Exposed/Promoted)**
   - Enum: `KongKind` (Concealed, Exposed, Promoted)
   - Parser: `KongKindParser` with Parse/TryParse/ToMeldTypeString API
   - String values: "ConcealedKong", "ExposedKong", "PromotedKong"
   - No schema migration (Type field already string)
   - Tests: 3 tests

5. **M4: Robbing-Kong Detection Helper**
   - API: `CanRobPromotedKong(IReadOnlyList<Tile> opponentHand, Tile kongTile, int existingKongCount) → bool`
   - Validates if opponent can win by adding kong tile to hand
   - Uses `Changsha258Ruleset.TryGetWinningHand()` for validation
   - Integration: check all opponents in `/meld` promoted kong path
   - Tests: 4 new tests

**Build & Tests:**
- All domain changes compiled successfully
- 17 new tests added (all passing)
- 2326/2326 total tests passing
- No breaking changes to existing tests

**Integration Checklist (for Batou):**
- [ ] D5: Add `WinTypes` parameter to `ChangshaSettlementRequest`; pass evaluation results
- [ ] D6: Track meld source; call `IsFullyReliant` in `/win`; set `ChangshaWinContext.IsFullyReliant`
- [ ] M3: Store kong kind in `MeldEntity.Type`; implement replacement draw
- [ ] M4: Check opponents with `CanRobPromotedKong` before allowing promotion

**Implementation Notes:**
- SanTong positioned in enum after SixSixStraight with sort order 175
- LuxurySevenPairs multiplier semantics differ from initial spec; confirmed via tests
- All work is domain-layer only; Batou owns API integration
- FullyReliant and RobbingKong helpers are pure functions with no side effects

### 2026-04-17: Autotable Client Direction
**By:** Kusanagi (Lead Architect)  
**Date:** 2026-04-17  
**Branch:** `squad/autotable-client`  
**Status:** Proposed (awaiting Stephen review)

**Decision:** Build new TypeScript + three.js client (Vite) to replace/complement Blazor WASM.

**Key Choices:**
- **Tech Stack:** Vite + TypeScript 5.x + three.js 0.170 + @microsoft/signalr 8.x
- **NO cannon-es for MVP** — server-authoritative state; animations are deterministic tweens, not physics
- **Sub-Project Layout:** Modular `src/scene/`, `src/state/`, `src/net/`, `src/input/`, `src/ui/`
- **Serving:** Standalone Vite at localhost:5173 with API proxy; static prod build
- **Blazor Policy:** Untouched during autotable build; retire only after Phase 5 parity proven

**Phase 0 Scope (1–2 days, Togusa lead):**
- Vite + TS + three.js boilerplate
- 108-tile wall mesh (placeholder colors)
- SignalR connect + event subscription
- `npm run dev` → localhost:5173, `npm run build` → green

**Open Questions:**
1. Tile art source: public-domain set vs commissioned?
2. Prod hosting: CDN vs same-origin wwwroot/?
3. Blazor retirement timeline: post-Phase 5 validation?

**Alternatives Rejected:**
- Blazor + three.js interop (tight coupling, JS friction)
- React/Vue wrapper (unnecessary overhead for single 3D scene)
- Physics-first approach (overkill for server-authoritative state)

## Governance

- Keep decisions concise and implementation-facing.
- Prefer append-only entries; avoid rewriting prior decision intent.
- Route unresolved trade-offs to Kusanagi for lead review.
