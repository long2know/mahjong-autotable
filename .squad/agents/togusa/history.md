# Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-claw — Multiplayer Traditional Chinese Mahjong
- **Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, SignalR, Blazor WASM, Playwright
- **Created:** 2026-04-16

## Learnings

- Seeded as Blazor UI Engineer during Squad init.

### Phase 0 AutotableClient Scaffold (2026-04-17)

**Completed:** Full Phase 0 scaffold of TypeScript + three.js client in `src/MahjongClaw.AutotableClient/`.

**Toolchain:**
- Vite 6.0.11 (dev server on port 5173; proxy `/api` and `/hubs` to localhost:5270)
- TypeScript 5.7.3 (strict mode, noUncheckedIndexedAccess)
- three.js 0.170.0 + @types/three
- @microsoft/signalr 8.0.7

**SignalR Hub & Events Discovered:**
- Hub URL: `/hubs/game` (mapped in Program.cs line 28)
- Events: `PlayerJoined`, `GameStarted`, `TileDrawn`, `TileDiscarded`, `MeldDeclared`, `GameWon` + legacy aliases (`PlayerJoin`, `DrawnTile`, `DiscardedTile`, `Meld`, `WinDeclared`)
- Connection is non-blocking if API offline (graceful failure + console error)

**Scene Setup:**
- 136 tiles in 4-wall formation (17 tiles × 2 rows × 4 walls)
- Changsha 258 uses 108 tiles; extra 28 tiles for standard mahjong layout visualization (matches Kusanagi's plan §3.2)
- Orthographic top-down camera at (0, 50, 0), frustum size 50
- Placeholder tiles: dark gray back (0x2a2a2a), lighter gray edges (0x4a4a4a)
- Table surface: green felt plane (0x1a5a3a), 40×40 units on XZ plane
- Tile dimensions: 0.8W × 1.2H × 0.6D (standard mahjong proportions)
- Wall offset: ±15 units from center

**Gotchas:**
1. **Vite environment typing:** Needed `src/vite-env.d.ts` with `/// <reference types="vite/client" />` to avoid TS2339 error on `import.meta.env`. TypeScript doesn't know about Vite globals by default.
2. **.gitignore coverage:** Root `.gitignore` already covered `node_modules/` (line 57) but missing `dist/` — added at line 58.
3. **SignalR bundle size:** Single-chunk build is 524 KB minified (132 KB gzip). SignalR client is heavy; consider lazy-loading or dynamic import in future phases if size becomes issue.
4. **Dev server port collision:** Vite auto-increments to 5174 if 5173 occupied. Non-blocking.

**Validation Results:**
- ✅ `npm install` in sub-project: 40 packages, 0 vulnerabilities
- ✅ `npm run build` in sub-project: TypeScript clean, Vite build clean
- ✅ `npm run dev` in sub-project: server starts on port 5173/5174
- ✅ Root `dotnet build`: 0 errors, 0 warnings
- ✅ Root `dotnet test`: 2336/2336 passed (all green)
- ✅ Root `npm run build:autotable`: delegates correctly, builds clean

**Deferred to Future Phases:**
- Tile textures (Phase 1+): Currently placeholder boxes; atlas + UV mapping needed
- State-driven rendering (Phase 1): Wall count, hands, melds, discards
- Interaction (Phase 2): Raycasting, HUD, API calls
- Animations (Phase 4): Tile movement tweens

**Non-obvious Defaults Applied (from Stephen's brief):**
- Tile art: Placeholder colored boxes (no textures)
- Prod hosting: Plan for same-origin `wwwroot/autotable/` mount, but Phase 0 uses standalone Vite dev server
- Blazor client: Untouched (zero changes to `src/MahjongClaw.Client/`)


### Phase 1 AutotableClient State-Driven Rendering (2026-04-17)

**Completed:** Full Phase 1 implementation — state-driven rendering with procedural tile atlas.

**GameState REST Endpoint:**
- Path: `GET /api/game/{id}/state?viewerPlayerId={playerId}`
- Returns: `GameStateResponse` with camelCase JSON serialization (System.Text.Json default)
- Shape: `{ id, status, currentPlayerId, dealerId, round, roundWind, ruleset, wallTotal, createdAt, players[], tilesInPlay, stateRevision, melds[], discards[], viewerHand[], turnPhase, legalActions[], lastDiscardTileId, lastDiscardPlayerId }`
- `players[]` includes: `{ id, name, seat, score, handCount }`
- `melds[]` includes: `{ id, playerId, type, tiles[] }` where tiles have `{ id, label }`
- `discards[]` includes: `{ id, playerId, tileId, sequence, tileLabel }`
- `viewerHand[]` only populated if `viewerPlayerId` query param matches a player in the game

**SignalR Event-to-State Mapping:**
- Phase 1 approach: refetch full state snapshot after each SignalR event (simple & correct)
- Alternative granular patching deferred to future optimization
- Connection auto-reconnects and refetches state on reconnection

**Tile Atlas Coord Strategy:**
- Procedural generation: 27 tile faces (3 suits × 9 ranks) + 1 back texture
- Canvas layout: 9 columns × 4 rows (1152 × 768 pixels)
- TileId format: `"bamboo-1"` through `"dot-9"` + `"back"`
- UV mapping: normalized 0-1 coords stored in Map<TileId, UVRect>
- Face rendering: rank number at top, suit symbol in middle (bars for bamboo, circles for dots, 萬 for character), suit label at bottom
- Back rendering: diagonal hatch pattern with center diamond on dark green background
- All generation synchronous at startup, no external assets, no licensing issues

**Tile Label Parsing:**
- Server sends labels like "🀐 1 Bamboo", "🀇 2 Character", "🀙 3 Dot"
- Regex extraction: `/(\d+)\s+(Bamboo|Character|Dot)/i`
- Maps to TileId format for atlas lookup
- Fallback to 'back' if parse fails

**Seat Layout & Rendering:**
- 4 seats counter-clockwise: East (bottom), South (right), West (top), North (left)
- Each seat rendered with rotation & translation relative to table center
- Concealed hand: horizontal row of tiles 5 units in front of seat
- Local player (matching viewerPlayerId) shows face-up tiles; others face-down
- Open melds: positioned to right of hand, 3-4 tiles each, slight rotation, 4th tile stacked for kongs
- Discards: 6-column grid in middle area adjacent to seat, ordered by sequence
- Turn indicator: yellow ring positioned at current player's seat

**Wall Pile Representation:**
- Single stack of up to 10 face-down tiles in corner position (-12, -12)
- Canvas label showing "Wall: N" where N = wallTotal - tilesInPlay
- Simplified from 4-segment wall approach (easier to implement, visually clear)

**HUD Implementation:**
- Top-right overlay with black background (70% opacity)
- Three sections: "Wall: N", "Turn: {player} ({seat})", "You: {player} ({seat})"
- Updates on every state change via clientState subscription
- Shows "Spectator" if no playerId provided

**TypeScript Strict Mode Fixes:**
- Array indexing: always check `!= null` before accessing elements
- Regex matches: validate capture groups exist before accessing
- Object removal: check child exists before THREE.js remove
- All strict + noUncheckedIndexedAccess checks passing

**Build Results:**
- ✅ `npm run build`: TypeScript clean, Vite build produces 534 KB bundle (136 KB gzip)
- ✅ `npm run dev`: starts on port 5173/5174
- ✅ Root `dotnet build`: 0 errors, 0 warnings
- ✅ Root `dotnet test`: 2341/2341 passed (5 more tests than baseline; expected from parallel work)

**Config & README:**
- Environment: `VITE_API_BASE` (default `http://localhost:5270`)
- Query params: `?game=<id>&player=<id>` (player optional for spectator mode)
- README updated with prerequisites, testing workflow, feature list, and out-of-scope items

**Phase 1 TODOs / Deferred:**
- Dead wall visualization (mentioned in plan but not critical; current single pile is sufficient)
- Optimized scene diffing (full rebuild on state change is acceptable for Phase 1)
- Tile face texture quality (procedural is functional but basic; polish deferred)

**Key Architectural Decisions:**
- State-driven rebuild: scene fully reconstructs on every state change (simple, correct, performant enough for turn-based game)
- Refetch-on-event: rather than granular SignalR delta application, we refetch full state (reliable, avoids sync bugs)
- Procedural atlas: no external assets, ship with confidence, license-clean


## Upcoming: TS/Three.js Autotable Client

**2026-04-17:** Kusanagi architected new TypeScript + three.js client (branch: `squad/autotable-client`) modeled on pwmarcz/autotable. Togusa will lead implementation when Stephen green-lights Phase 0 scaffold.

**Scope:** 5-phase plan spanning app boilerplate (Phase 0, 1–2 days) through full feature parity + Blazor retirement decision (Phase 5, 5–7 days).

**Tech Stack:** Vite + three.js 0.170 + @microsoft/signalr 8.x; NO physics for MVP. Standalone dev server (localhost:5173) with API proxy; static prod build.

**Open Questions:** Tile art source, prod hosting strategy, Blazor retirement timeline.
