> **⚠️ SUPERSEDED — see `.squad/decisions/inbox/ripley-pivot-plan.md`.**
> The architecture described below was abandoned in the pivot to autotable-vendored Changsha-native. Kept for archaeology only; will be hard-deleted in Phase E.
---

# Changsha Mahjong — Frontend UX Plan

**Author:** Hicks (Frontend Dev)
**Date:** 2026-04-23
**Status:** Draft for team review

---

## 1. Architecture Recommendation

**Recommended: Option B — Backend-authoritative state with autotable as 3D viewport, Changsha chrome via React Fluent UI.**

### Justification

The autotable bundle (`autotable.9519e86d.js`) is a Parcel-hashed, minified blob we cannot and should not modify. Upstream autotable's deal flow is entirely client-side: when a user long-presses the Deal button, `GameUi.setupDealButton()` reads the deal-type/game-type selectors, calls `World.deal()`, which calls `Setup.deal(seat)` — all locally. It shuffles tiles, rolls dice via `Math.random()`, computes a break point from the roll, places tiles into wall/hand slots, and broadcasts the resulting `things` + `match` + `dice` collections over WebSocket to peers. **No server authority exists in upstream.**

Our backend already provides the correct authoritative model: `TableStateEngine.CreateInitialState()` creates a seeded, shuffled wall and deals 13 tiles per seat (14 for dealer) via `POST /api/tables`. The React modern frontend already consumes this via REST and renders a playable loop. The 3D autotable visuals are what the user values — we keep those.

**Option B** gives us:
- **Backend authority** for all Changsha rules (dice roll, break-point, batch-draw order, scoring) — no client-side game logic to maintain.
- **Autotable 3D viewport** preserved for visual rendering of wall, tiles, and table via its existing WebSocket protocol (things/slots/match/dice collections).
- **React Fluent UI panels** for all Changsha-specific chrome that autotable was never designed for: dice modal, banker badge, wind/round indicator, fan calculation panel, scoring summary.

**Why not Option A (overlay only)?** The autotable bundle's Deal button triggers the entire client-side deal. We'd have to intercept and suppress it, inject dice state, and hack slot assignments — all without modifying the bundle. The interception surface is fragile.

**Why not Option C (React renders everything)?** Rebuilding the 3D table in React (Three.js/R3F) is a large effort with no user value — the autotable renderer already works. The user explicitly values the autotable look.

---

## 2. Deal Flow Storyboard

### Current state (what's wrong)

The backend (`TableStateEngine.CreateInitialState`) deals all 136 tiles immediately: shuffles via Fisher-Yates with a seed, then deals 13 tiles per seat + 1 extra to dealer in a simple loop. There is **no dice roll, no break-point computation, no 4-tile-batch draw sequence.** The wall array in `TableGameState` has no positional mapping to the physical 4-sided wall.

The autotable bundle's upstream deal (in `setup.ts` → `setup-deal.ts`) *does* implement dice + break-point + wall slot placement for a Riichi-style deal — but it runs entirely client-side and uses upstream's own slot coordinate system. We need the Changsha equivalent, driven by our backend.

### Changsha deal flow — step by step

**(Pending Vasquez's rules spec for precise Changsha dice semantics. The following assumes standard Changsha conventions.)**

| Step | UX | Source of truth | Animation | Open questions |
|------|----|----|-----|------|
| **1. Players seated, banker shown** | Banker badge (東) shown on East seat. Round/wind indicator displayed. | Backend: `dealState.bankerSeatIndex`, `dealState.prevalentWind` | React badge component | Vasquez: banker rotation rules between hands? |
| **2. "Roll Dice" prompt** | For multiplayer: banker sees a "Roll Dice" button. For solo-vs-bots: auto-roll with brief delay. | Backend: new `POST /api/tables/{id}/deal/roll-dice` endpoint returns `{dice: [d1, d2], breakWallIndex, breakTileOffset}` | React modal or inline prompt | Vasquez: single roll or double roll (some Changsha variants use two rolls)? |
| **3. Dice animation** | Dice values displayed on the autotable center canvas. The bundle already renders dice via `Center.drawDice()` when it receives a `dice` collection update with `state: 'rolled'`. We can trigger this through the WS bridge. | Backend provides values; autotable renders via bridge | Autotable's existing `drawDie()` sprite animation (dice.auto.391822b5.png spritesheet) — 2D overlay on center mesh | Confirm: can we inject dice collection updates through the bridge without a full deal cycle? |
| **4. Break point highlighted** | The wall segment where drawing begins is visually marked. In autotable, wall tiles sit in slots `wall.0.0` through `wall.17.0` × 4 seats. The break point is computed from dice sum + banker position. | Backend computes break index from dice + banker seat | React overlay indicator (arrow/highlight on wall edge) or autotable slot manipulation via bridge | Vasquez: exact Changsha break-point formula (dice sum counted counter-clockwise from banker's right wall end)? |
| **5. 4-tile-batch draw** | Tiles drawn in batches of 4, counter-clockwise from break point, repeated 3 rounds (each player gets 12), then 1 tile each for 13, then banker draws 14th. | Backend: new phased deal endpoint or enriched `CreateInitialState` that returns draw-batch sequence | Autotable: tiles move from wall slots to hand slots via `things` collection updates through bridge. Each batch is a separate animation frame (200-400ms delay between batches). | Vasquez: exact batch order — is it always 4-4-4-1 or does Changsha vary? Bishop: backend must produce ordered draw events, not just final hand state. |
| **6. Hands populated, banker's turn** | All players have tiles. Banker has 14, others have 13. Phase transitions to `AwaitingDiscard` for banker. | Backend: `state.activeSeat = bankerSeatIndex`, `phase = AwaitingDiscard` | React hand panel updates; autotable hand slots populated via bridge | None — existing flow handles this once hands are set. |
| **7. Banker discards first** | Standard discard flow via existing `POST /api/tables/{id}/actions/discard` | Backend (existing) | Existing React UI + autotable slot movement | None — already implemented. |
| **8. Normal turn flow with claim windows** | Draw-discard cycle with pung/kong/chow/hu claim windows. | Backend (existing claim resolution) | Existing React claim panel + autotable tile movement | None — claim resolution is implemented. |
| **9. Win → fan calculation panel** | Winner declared. Fan breakdown slides in showing hand composition, fan types, and point values. | Backend: new `WinScoring` object in `TableWinState` with fan details | React Fluent UI side panel with fan breakdown table | Vasquez: complete fan table for Changsha v1 (which fans are in scope?). Bishop: `TableWinState` needs scoring fields. |

---

## 3. Component Inventory

### React Fluent UI Components (new)

| Component | Purpose | Data inputs | Mount point |
|-----------|---------|-------------|-------------|
| `DiceRollModal` | Prompts banker to roll; shows animated result | `bankerSeatIndex`, `diceValues`, `isAutoRoll` | Modal overlay on main app |
| `BankerBadge` | Shows 東/南/西/北 wind marker on active banker seat | `bankerSeatIndex`, `prevalentWind` | Inside each `seat-zone` header |
| `RoundWindIndicator` | Displays current round wind and hand number | `prevalentWind`, `handNumber`, `roundNumber` | Table center metrics grid |
| `WallBreakIndicator` | Highlights break-point on wall (visual cue) | `breakWallSide`, `breakTileOffset` | Overlay positioned relative to autotable canvas or in React center panel |
| `BatchDrawProgress` | Shows dealing progress during 4-tile-batch animation | `currentBatch`, `totalBatches`, `drawingForSeat` | Inline status below table center |
| `FanScorePanel` | Displays fan composition and point calculation on win | `winState`, `fanBreakdown[]`, `pointsAwarded` | Slide-in card below table or modal |
| `GameScoreBoard` | Running score across multiple hands | `seatScores[]`, `handHistory[]` | Collapsible panel in sidebar or below table |

### Autotable Bridge Component

| Component | Purpose | Data inputs | Mount point |
|-----------|---------|-------------|-------------|
| `AutotableBridge` | Translates backend state to autotable WS protocol messages; manages colocated WS server or direct collection injection | `tableState`, `diceValues`, `slotAssignments` | Invisible — runs as a service alongside autotable iframe/embed |

---

## 4. Bundle Interception Strategy

Since we chose **Option B**, we need the autotable bundle to render tiles in positions dictated by our backend, not by its own client-side deal logic.

### Recommended: Colocated WebSocket bridge server

**Approach:** Run a lightweight WebSocket server (in the .NET backend or as a small Node sidecar) that speaks upstream autotable's protocol (`protocol.ts`: `NEW` → `JOINED`, then `UPDATE` messages with `[kind, key, value]` entry tuples). The autotable bundle connects to this bridge instead of upstream's Python/Node server.

**How it works:**
1. The autotable `index.html` loads at `/autotable/`. Its `ClientUi.getUrl()` computes a WS URL from the page location (e.g., `ws://host/autotable/ws`).
2. Our backend serves a WS endpoint at `/autotable/ws` that implements the upstream protocol:
   - On `NEW`/`JOIN`: respond with `JOINED` message.
   - Translate backend `TableGameState` into autotable `things` collection entries (tile index → slot name + rotation).
   - Push `match` collection with dealer/honba/conditions.
   - Push `dice` collection with roll values and `state: 'rolled'`.
3. When backend state changes (deal, discard, claim), the bridge pushes `UPDATE` messages with the new `things` entries to move tiles visually.

**Why this approach:**
- **No bundle modification.** The bundle thinks it's talking to its normal server.
- **Full control.** We map Changsha state to autotable slot coordinates server-side.
- **Dice rendering for free.** Pushing `dice` collection with `state: 'rolled'` triggers autotable's existing `Center.drawDice()`.
- **Incremental.** We can start with a wall-only bridge (no interactive tile dragging) and add seat claims later.

**Upstream protocol summary** (from `server/protocol.ts`):
```typescript
type Entry = [string, string | number, any | null];  // [kind, key, value]
type Message =
  | { type: 'NEW' }
  | { type: 'JOIN', gameId: string }
  | { type: 'JOINED', gameId: string, playerId: string, isFirst: boolean }
  | { type: 'UPDATE', entries: Entry[], full: boolean };
```

**Key collections to bridge:**
- `things`: `[thingIndex, { slotName, rotationIndex, claimedBy, heldRotation, shiftSlotName }]` — positions every tile, stick, marker.
- `match`: `[0, { dealer, honba, conditions }]` — game setup and dealer indicator.
- `dice`: `[0, { dice: [d1, d2], state: 'rolled' | 'ignore' }]` — dice display.
- `seats`: `[playerId, { seat: number | null }]` — seat assignments.

**Slot naming convention** (from `setup-slots.ts` and `setup-deal.ts`):
- Wall slots: `wall.{column}.{layer}@{seat}` (e.g., `wall.5.0@2`)
- Hand slots: `hand.{position}@{seat}` (e.g., `hand.0@0` through `hand.12@0` for 13 tiles)
- Discard slots: `discard.{position}@{seat}`

**Alternative considered — DOM event injection:**
Inject a script in `index.html` that patches `World.deal()` or intercepts the Deal button. Rejected because: (a) the bundle is minified and method names are mangled, (b) fragile across bundle rebuilds, (c) doesn't give us control over individual tile animations during the batch-draw sequence.

---

## 5. Modern Frontend Modernization Roadmap

Priority order (lowest risk, highest user value first):

### Phase 1: Dice + Banker + Round Indicator (1-2 days)
- `BankerBadge` on seat zones (trivial — data already in backend, just needs new field)
- `RoundWindIndicator` in center metrics
- `DiceRollModal` with static values from backend (auto-roll for solo)
- **Prerequisite:** Backend adds `bankerSeatIndex`, `prevalentWind`, `diceRoll` to `TableGameState`

### Phase 2: WS Bridge — Static Table Render (3-5 days)
- `AutotableBridge` WS server endpoint in .NET backend
- Translate `TableGameState` wall/hand/discard arrays to autotable slot assignments
- Autotable renders the correct post-deal state from backend
- Deal button in autotable sidebar suppressed (or bridge ignores client-initiated deals)

### Phase 3: Animated Batch Draw (2-3 days)
- Backend returns draw-batch sequence (not just final state)
- Bridge pushes batched `things` updates with timing delays
- `BatchDrawProgress` component shows draw progress
- `WallBreakIndicator` highlights starting position

### Phase 4: Fan Scoring Panel (3-5 days)
- Backend implements Changsha fan detection (depends on Vasquez spec)
- `FanScorePanel` React component with fan breakdown
- `GameScoreBoard` for multi-hand tracking
- **Prerequisite:** Vasquez delivers complete fan table; Bishop implements scoring engine

### Phase 5: Full Bridge Interactivity (5+ days)
- Bridge translates player tile drags back to backend discard actions
- Bridge handles claim visualization (tile highlighting on pung/kong)
- Multiplayer seat management through bridge

---

## 6. Open Questions

### For Vasquez (Rules)
1. **Dice mechanics:** Single roll or double roll for break-point? Exact formula for counting from banker's wall?
2. **Batch draw order:** Is it strictly 4-4-4-1 (three rounds of 4, then 1 each, then banker's extra)? Any Changsha-specific variation?
3. **Banker rotation:** Does banker rotate on every hand, or only when non-banker wins? What about draws?
4. **Fan table scope for v1:** Which fans are included? Need the complete list to build `FanScorePanel`.
5. **Flower tiles:** Does Changsha v1 include flower/season tiles, or just the 136 standard tiles?

### For Bishop (Backend)
1. **New state fields needed:** `TableGameState` needs `bankerSeatIndex`, `prevalentWind` (East/South/West/North), `diceRoll: [int, int]`, and `breakPointIndex`.
2. **Deal sequencing:** Current `CreateInitialState` produces final state atomically. For animated batch-draw, the bridge needs either (a) a separate batch-draw event sequence in `ActionLog`, or (b) a deterministic client-side reconstruction from seed + dice + break-point. Recommend (a) for correctness.
3. **WS endpoint:** Is adding a WebSocket endpoint at `/autotable/ws` in the .NET backend feasible alongside the existing REST API? Or should this be a separate sidecar?
4. **Scoring model:** `TableWinState` needs `fanBreakdown` and `pointsAwarded` fields for Phase 4.
5. **Slot coordinate mapping:** We need a mapping function from `(wallTileIndex, seatIndex)` → autotable slot name. This is non-trivial (4 wall sides × 17-18 columns × 2 layers). Should this live in the bridge or as a shared utility?

### Cross-team
- **When can we suppress autotable's Deal button?** Until the bridge is active, the autotable Deal button triggers a client-side Riichi deal that conflicts with backend state. Simplest fix: add `style="display:none"` to the Deal button in `index.html` (this file is ours to edit, unlike the JS bundle).
- **Iframe vs. embedded?** Currently autotable is served at `/autotable/` as a separate page. For Option B, we may want to embed it in an iframe within the React app for tighter layout control. This affects how the bridge connects.
