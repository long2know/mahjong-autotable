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


## Upcoming: TS/Three.js Autotable Client

**2026-04-17:** Kusanagi architected new TypeScript + three.js client (branch: `squad/autotable-client`) modeled on pwmarcz/autotable. Togusa will lead implementation when Stephen green-lights Phase 0 scaffold.

**Scope:** 5-phase plan spanning app boilerplate (Phase 0, 1–2 days) through full feature parity + Blazor retirement decision (Phase 5, 5–7 days).

**Tech Stack:** Vite + three.js 0.170 + @microsoft/signalr 8.x; NO physics for MVP. Standalone dev server (localhost:5173) with API proxy; static prod build.

**Open Questions:** Tile art source, prod hosting strategy, Blazor retirement timeline.
