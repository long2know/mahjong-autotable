# Autotable Client Plan

> **Lead:** Kusanagi (Architect)  
> **Branch:** `squad/autotable-client`  
> **Date:** 2026-04-17  
> **Reference:** pwmarcz/autotable (MIT) — https://github.com/pwmarcz/autotable

---

## 1. Architecture

### 1.1 Sub-Project Layout

```
src/MahjongClaw.AutotableClient/
├── index.html                 # Entry point (Vite dev server)
├── package.json
├── tsconfig.json
├── vite.config.ts
├── public/
│   └── assets/
│       └── tiles/             # Tile textures (34 faces + back)
└── src/
    ├── main.ts                # App bootstrap
    ├── scene/
    │   ├── SceneManager.ts    # Three.js scene setup, camera, lighting
    │   ├── TileMesh.ts        # Single-tile box geometry + materials
    │   ├── WallRenderer.ts    # 4-wall layout
    │   ├── HandRenderer.ts    # Player hand + melds
    │   ├── DiscardRenderer.ts # Per-seat discard area
    │   └── TableRenderer.ts   # Table surface + seat markers
    ├── state/
    │   ├── GameState.ts       # Client state model (mirrors server GameStateResponse)
    │   ├── StateStore.ts      # Reactive store (Zustand or vanilla signals)
    │   └── Reconciler.ts      # Server state → scene reconciliation
    ├── net/
    │   ├── ApiClient.ts       # REST calls (create, join, draw, discard, meld, win)
    │   ├── SignalRClient.ts   # SignalR connection, event subscriptions
    │   └── types.ts           # DTOs matching server contracts
    ├── input/
    │   ├── RaycastPicker.ts   # Three.js raycasting for tile selection
    │   └── ActionDispatcher.ts# Map clicks → API calls
    ├── ui/
    │   ├── HudOverlay.ts      # HTML overlay for actions (discard, meld, win)
    │   ├── InfoPanel.ts       # Scores, turn indicator, round wind
    │   └── CameraControls.ts  # Toggle flat ↔ perspective
    └── utils/
        └── TileTextures.ts    # Texture atlas loader
```

### 1.2 Toolchain

| Tool | Version | Notes |
|------|---------|-------|
| **Vite** | ^6.x | Dev server + prod build; fast HMR |
| **TypeScript** | ^5.x | Strict mode, `noUncheckedIndexedAccess` |
| **three.js** | ^0.170 | Scene, camera, geometries, materials |
| **cannon-es** | *(optional)* | Physics — see §3.7 |
| **@microsoft/signalr** | ^8.x | SignalR client for realtime |
| **pnpm** | — | *(repo uses npm; stick with npm)* |

**What NOT to copy from autotable:**
- Autotable uses custom WebSocket + JSON-RPC; we use SignalR.
- Autotable has client-side shuffling; we're server-authoritative.
- Autotable's multi-layer SCSS/PostCSS setup; we only need minimal CSS for HUD.

### 1.3 Serving Strategy

**Recommendation: Standalone static dev server (option a).**

| Mode | Setup |
|------|-------|
| **Dev** | Vite at `localhost:5173`; proxies `/api` and `/hubs` to ASP.NET at `localhost:5270`. |
| **Production** | `vite build` outputs to `dist/`; deploy to static CDN or copy into `wwwroot/autotable/` for same-origin hosting. |

**Why not StaticFiles mount (option b)?**
- Tight coupling slows iteration; Vite HMR is faster standalone.
- Separate deploy allows CDN-edge caching for assets.
- Same-origin auth cookies still work via proxy in dev; in prod, same domain or CORS.

### 1.4 CORS / Auth / SignalR During Dev

API (`Program.cs`) already allows any origin via `AddCors()`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});
```

SignalR requires:
- `withCredentials: false` (no cookies for now; game uses player GUID as identity).
- No change needed on server — CORS policy is permissive.

**Vite proxy config:**

```ts
// vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/api': 'http://localhost:5270',
      '/hubs': {
        target: 'http://localhost:5270',
        ws: true,
      },
    },
  },
});
```

---

## 2. API Contract Audit

### 2.1 REST Endpoints

| Endpoint | Method | Purpose | Request DTO | Response DTO |
|----------|--------|---------|-------------|--------------|
| `/api/game` | POST | Create game | `CreateGameRequest` (HostName, Mode, BotSeatCount, Ruleset) | `CreateGameResponse` (Id, Status, Round, CreatedAt, HostPlayerId, Ruleset) |
| `/api/game/{id}/join` | POST | Join game | `JoinGameRequest` (Name, Seat) | `JoinGameResponse` (PlayerId, GameId, Name, Seat, Score) |
| `/api/game/{id}/start` | POST | Start game | — | `StartGameResponse` (GameId, Status, DealerId, CurrentPlayerId) |
| `/api/game/{id}/state` | GET | Get full state | query `viewerPlayerId` | `GameStateResponse` (see §2.2) |
| `/api/game/{id}/draw` | POST | Draw tile | `DrawTileRequest` (PlayerId) | `DrawTileResponse` (TileId, Suit, Rank, Wind?, Dragon?) |
| `/api/game/{id}/discard` | POST | Discard tile | `DiscardTileRequest` (PlayerId, TileId) | `DiscardTileResponse` (DiscardId, TileId, Sequence) |
| `/api/game/{id}/meld` | POST | Declare meld | `MeldRequest` (PlayerId, Type, TileIds[]) | `MeldResponse` (MeldId, Type, TileIds) |
| `/api/game/{id}/win` | POST | Declare win | `WinRequest` (PlayerId, IsSelfDraw, DiscarderPlayerId?, BirdRank?, BirdRanks?, ...) | `WinResponse` (GameId, Status, PlayerId, ChangshaWinTypes?, IsBigWin?, ScoreDeltas?, NextDealerId?, ...) |
| `/api/game/{id}/bot/turn` | POST | Force bot turn | — | `BotTurnResponse` (DrawnTileId?, DiscardId?, DiscardTileId?, NextPlayerId?, Won) |

### 2.2 GameStateResponse Shape

```ts
interface GameStateResponse {
  id: string;             // Guid
  status: string;         // "Waiting" | "Active" | "Completed"
  currentPlayerId?: string;
  dealerId?: string;
  round: number;
  roundWind?: string;     // "East" | "South" | "West" | "North"
  ruleset: string;
  wallTotal: number;      // 108 for Changsha, 144 for ChineseOfficial
  createdAt: string;      // ISO 8601
  players: PlayerSummary[];
  tilesInPlay: number;
  stateRevision: number;
  melds: MeldSummary[];
  discards: DiscardSummary[];
  viewerHand: TileSummary[];
  turnPhase: string;      // "Draw" | "Discard" | "NotStarted" | ...
  legalActions: string[]; // ["Draw", "Discard", "Meld", "Win"]
  lastDiscardTileId?: string;
  lastDiscardPlayerId?: string;
}
```

### 2.3 SignalR Hub & Events

**Hub:** `/hubs/game` (`GameHub`)

**Client → Server Methods:**
- `JoinGame(gameId: string)` — add connection to SignalR group
- `LeaveGame(gameId: string)` — remove from group

**Server → Client Events:**

| Event | Payload | Aliases |
|-------|---------|---------|
| `PlayerJoined` | `JoinGameResponse` | `PlayerJoin` |
| `GameStarted` | `StartGameResponse` | — |
| `TileDrawn` | `DrawTileResponse` | `DrawnTile` |
| `TileDiscarded` | `DiscardTileResponse` | `DiscardedTile` |
| `MeldDeclared` | `MeldResponse` | `Meld` |
| `GameWon` | `WinResponse` | `WinDeclared` |
| `KongRobOpportunity` | `{ TileId, Suit, Rank, Wind?, Dragon?, KongPlayerId, EligibleOpponents[] }` | — |

### 2.4 Contract Friction Points

| Issue | Impact | Mitigation |
|-------|--------|------------|
| **camelCase** | Server uses PascalCase in C# records; System.Text.Json default serializes to camelCase — ✅ OK for JS. | None needed. |
| **Guid as string** | All IDs are Guid; JSON sends as string — standard. | TypeScript uses `string`. |
| **Tile representation** | `Suit: string` + `Rank: int` + optional `Wind`/`Dragon` — straightforward. | Map to local `TileId` numeric for texture lookup. |
| **TurnPhase enum as string** | "Draw", "Discard", etc. | Union type in TS. |
| **BirdRank vs BirdRanks** | Backward compat; both exist. | Client sends `BirdRanks: [r1, r2]` for Changsha. |
| **Missing claim endpoint** | No `/claim` yet (M1 deferred). | Human players can still call `/meld` or `/win` directly after another's discard — no claim window. |

**Contract-tidy PR?** Not required. Current DTOs are JS-friendly. No breaking changes.

---

## 3. Scene Model

### 3.1 Coordinate System

- **Y-up**, right-handed (Three.js default).
- **XZ plane** = table surface.
- Origin (0, 0, 0) = table center.

### 3.2 Table Dimensions

| Element | Size (units) | Notes |
|---------|--------------|-------|
| Table surface | 40 × 40 | Green felt; slight chamfer |
| Tile | 0.8 × 1.2 × 0.6 (W × H × D) | Standard mahjong tile proportions |
| Wall segment (17 tiles × 2 rows) | ~13.6 × 1.2 | Stacked |

### 3.3 Seat Positions

Counter-clockwise from player POV (matches server `East → South → West → North`):

| Seat | Position | Facing |
|------|----------|--------|
| **East** (bottom) | (0, 0, +15) | -Z |
| **South** (right) | (+15, 0, 0) | -X |
| **West** (top) | (0, 0, -15) | +Z |
| **North** (left) | (-15, 0, 0) | +X |

Human player defaults to East; camera looks down at their hand.

### 3.4 Tile Mesh Strategy

**Recommended:** Single `BoxGeometry` with 6 materials array.

```ts
const geometry = new THREE.BoxGeometry(0.8, 1.2, 0.6);
const materials = [
  sideWhite,  // +X
  sideWhite,  // -X
  topMat,     // +Y (face texture from atlas)
  backMat,    // -Y (bamboo back)
  sideWhite,  // +Z
  sideWhite,  // -Z
];
```

**Texture atlas:** 34 distinct tile faces (9×3 suits + 7 honors if Chinese Official; 27 for Changsha) packed into 512×512 or 1024×1024 atlas. UV mapping selects region.

### 3.5 Walls

Changsha 258 = **108 tiles** = 4 walls × 27 tiles (13-14 each; adjust for asymmetry). Actual layout:
- Each wall: ~13-14 tiles × 2 rows (stacked).
- Dead wall: last ~14 tiles reserved for kong replacements.

**Visual approach:**
- Render from server's `wallTotal - tilesInPlay` to show remaining wall size.
- Wall tiles are face-down (show back texture).
- As tiles are drawn, decrement visually from draw end.

**Dead wall:** Visually separate last 14 tiles (slight gap or color tint). Kong replacement draws from dead wall end.

### 3.6 Hand / Meld / Discard Rendering

| Element | Layout |
|---------|--------|
| **Concealed hand** | 13+ tiles in front of player, face-up for owner only (server returns `viewerHand` with labels). Other players see face-down. |
| **Drawn tile** | 14th tile held slightly right, separated from rest. |
| **Open melds** | Right of hand, face-up, grouped. Kongs show 4 tiles (concealed kongs show back faces on outer two). |
| **Discards** | 6-column grid in front of player, in sequence order. Tiles face-up. |

### 3.7 Camera Presets

1. **Flat top-down (orthographic):** 
   - Position: (0, 50, 0), looking at (0, 0, 0).
   - Orthographic frustum sized to fit table.
   - Good for quick overview.

2. **Perspective 3D:**
   - Position: (0, 25, 30), looking at (0, 0, 0).
   - FOV ~50°.
   - Can orbit via OrbitControls (optional).

**Transition:** Smooth tween (0.5s) between presets via GSAP or Three.js animation.

### 3.8 Physics: MVP Scope

**Recommendation: NO physics for MVP.**

Autotable uses physics for freeform tile manipulation (dragging, shuffling). Our server-authoritative model:
- State is discrete; tiles move between known locations.
- Animations are deterministic tweens (tile slides from wall → hand).
- No collision detection needed.

**Phase 4+:** If we want "tile tumble" on claim animations, add cannon-es. Not MVP.

---

## 4. Networking / State

### 4.1 SignalR Client Lifecycle

```ts
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/game')
  .withAutomaticReconnect()
  .build();

await connection.start();
await connection.invoke('JoinGame', gameId);

connection.on('PlayerJoined', handler);
connection.on('TileDrawn', handler);
// ... etc
```

**Reconnection:** SignalR's built-in retry; on reconnect, call `GET /api/game/{id}/state` to resync.

### 4.2 State Sync Model

**Recommended: Event-based deltas + periodic state fetch.**

- On each SignalR event (`TileDrawn`, `TileDiscarded`, etc.), update local state incrementally.
- `StateStore` holds:
  - `players[]`, `melds[]`, `discards[]`, `viewerHand[]`, `wallRemaining`, `currentPlayerId`, `turnPhase`, etc.
- `Reconciler` maps state changes → scene updates (add/remove meshes, animate moves).
- On reconnect or state drift, fetch full `GameStateResponse` and rebuild.

### 4.3 User Actions

| Action | Trigger | API Call | Post-action |
|--------|---------|----------|-------------|
| **Draw** | Click "Draw" button or click wall | `POST /draw` | Wait for `TileDrawn` event; animate tile to hand |
| **Discard** | Click tile in hand | `POST /discard` with TileId | Wait for `TileDiscarded` event; animate to discard area |
| **Meld** | Click action button after selecting tiles | `POST /meld` with TileIds | Wait for `MeldDeclared` event |
| **Win** | Click "Hu" button when legal | `POST /win` | Wait for `GameWon` event; show overlay |

### 4.4 Bot Seats

Server auto-plays bots; client receives `TileDrawn` → `TileDiscarded` (or `GameWon`) events. Client just renders.

---

## 5. Phase Plan

### Phase 0 — Scaffolding (PR #1)

**Scope:**
- Vite + TS + three.js "hello tile" scene.
- Connect to SignalR, join game, log events.
- Render static wall of 108 tiles (face-down boxes).
- No interaction.

**Done criteria:**
- `npm run dev` starts at localhost:5173.
- Scene shows 108 tiles arranged in 4-wall formation.
- Console logs `PlayerJoined`, `GameStarted` from a bot game.
- Build passes: `npm run build`.

**Agent load:** Togusa (lead TS/three.js), minimal Batou involvement.  
**Estimate:** 1-2 days.

---

### Phase 1 — State-Driven Rendering

**Scope:**
- Fetch `GameStateResponse` on load.
- Render all elements: hands (face-down for opponents), melds, discards, walls.
- Current-seat indicator (glow or marker).
- Read-only spectator mode — can watch bot-only game.

**Done criteria:**
- Full game state visualized.
- Real-time updates via SignalR reflect in scene.
- Wall count decrements visually on draw.

**Agent load:** Togusa (primary), Saito (tests).  
**Estimate:** 3-4 days.

---

### Phase 2 — Interaction

**Scope:**
- Draw: click button or click wall end.
- Discard: click tile in hand.
- Meld: select tiles, click Pung/Kong button.
- Win: click Hu when legal.
- HUD overlay for action buttons.

**Done criteria:**
- Human player can complete a full game vs bots.
- All legal actions work.

**Agent load:** Togusa (primary), Batou (if any API gaps).  
**Estimate:** 3-5 days.

---

### Phase 3 — View Modes

**Scope:**
- Toggle flat ↔ perspective camera.
- Smooth animated transition.
- Keyboard shortcut (e.g., `V`).

**Done criteria:**
- Both views work.
- Transition is smooth (no pop).

**Agent load:** Togusa.  
**Estimate:** 1 day.

---

### Phase 4 — Polish

**Scope:**
- Tile animations: slide into discard, slide to hand on draw.
- Kong replacement: tile flies from dead wall.
- Claim animation: discarded tile slides to claimant.
- Bird highlight: flash winning tile(s) at settlement.
- Win overlay: show win types, scores.
- Settlement display: score deltas.

**Done criteria:**
- Animations smooth at 60 fps.
- Win screen shows all settlement info.

**Agent load:** Togusa (animations), Saito (e2e tests).  
**Estimate:** 3-4 days.

---

### Phase 5 — Parity & Retirement

**Scope:**
- Lobby: game list, create/join UI.
- Scoreboard: persistent view.
- Ruleset selector (Changsha vs Chinese Official).
- Full feature parity with Blazor client.
- Evaluate Blazor retirement.

**Done criteria:**
- All Blazor features replicated.
- Decision: retire Blazor or keep dual clients.

**Agent load:** Togusa + Batou + Saito.  
**Estimate:** 5-7 days.

---

## 6. Risks

### 6.1 License

pwmarcz/autotable is **MIT-licensed**. We can:
- Reference architecture freely.
- Lift code with attribution.

**Recommendation:** Clean-room implementation based on architecture reading. Avoid direct code copy to prevent inadvertent style/logic coupling.

### 6.2 Tile Textures

Autotable tile art is custom; license unclear for assets. **Do NOT assume reuse.**

**Options:**
1. **Source public-domain tiles:** OpenGameArt, Kenney, or other CC0 mahjong sets.
2. **Generate programmatically:** SVG → PNG for suit tiles (numbers + symbols).
3. **Commission:** If visual quality is critical.

**Phase 0 fallback:** Solid-color placeholder tiles with text labels.

### 6.3 WebGL Performance

- 108 tiles + table + HUD = ~150 draw calls. Manageable.
- Use `InstancedMesh` for wall tiles (same geometry, different transforms).
- Test on low-end (integrated GPU, mobile browser).

### 6.4 Blazor Client

**Keep Blazor alive** during autotable development. It's our fallback and existing user-facing UI. Retire only after Phase 5 parity is proven.

### 6.5 Missing Server Features

- **Claim arbitration (M1):** No claim window; players must act fast. Acceptable for MVP.
- **Multi-winner (M6):** Not implemented; client shows single winner.

---

## 7. Recommended First Deliverable (Phase 0 PR)

**PR Title:** `feat(AutotableClient): scaffold Vite + three.js project`

**Contents:**
- `src/MahjongClaw.AutotableClient/` with full directory structure.
- `package.json` with dependencies (vite, typescript, three, @microsoft/signalr).
- `vite.config.ts` with API proxy.
- `tsconfig.json` (strict).
- `index.html` + `src/main.ts` — renders 108-tile wall.
- `src/net/SignalRClient.ts` — connects, logs events.
- Placeholder tile mesh (solid color boxes).

**Root `package.json` update:**
```json
"scripts": {
  "dev:autotable": "npm --prefix src/MahjongClaw.AutotableClient run dev",
  "build:autotable": "npm --prefix src/MahjongClaw.AutotableClient run build"
}
```

**Acceptance:**
- `npm run dev:autotable` → localhost:5173 shows wall.
- `npm run build:autotable` → green.
- Can connect to running .NET API and log SignalR events.

---

## Appendix A: Tile ID Mapping (Changsha 258)

| Suit | Ranks | Count | IDs (for texture atlas) |
|------|-------|-------|-------------------------|
| Characters | 1-9 | 36 | 0-8 (face index) |
| Dots | 1-9 | 36 | 9-17 |
| Bamboos | 1-9 | 36 | 18-26 |

**Total:** 27 unique faces × 4 copies = 108 tiles.

---

## Appendix B: Awaiting Saito Recon

*Note: Saito's autotable architecture recon (`.squad/research/autotable-recon.md`) was not available at plan time. Once delivered, incorporate insights on:*
- Autotable's scene graph organization.
- Animation patterns for tile movement.
- Any reusable shader or material techniques.

---

*Plan authored by Kusanagi. Next step: Stephen review + green-light for Phase 0 PR.*
