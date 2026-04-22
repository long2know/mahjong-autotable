# Mahjong Claw - Autotable Client (Phase 1)

TypeScript + three.js 3D client for Mahjong Claw — state-driven rendering with procedural tile atlas.

## Prerequisites

1. **Start the .NET API server first:**
   ```bash
   # From repo root
   dotnet run --project src/MahjongClaw.Api
   ```
   API will run at http://localhost:5270

2. **Create a game** using existing Blazor client or via REST:
   - Option A: Visit http://localhost:5270 → use Blazor client to create game
   - Option B: `curl -X POST http://localhost:5270/api/game -H "Content-Type: application/json" -d '{"hostName":"Alice","mode":"HumanVsBot","botSeatCount":3}'`
   - Note the returned game `id` from the response

## Setup

```bash
npm install
```

## Development

Start the Vite dev server:

```bash
npm run dev
```

Visit http://localhost:5173?game=<GAME_ID>&player=<PLAYER_ID> where:
- `<GAME_ID>`: The GUID of the game you created (e.g., `123e4567-e89b-12d3-a456-426614174000`)
- `<PLAYER_ID>`: Your player GUID (optional; omit to spectate)

### Example

```
http://localhost:5173?game=123e4567-e89b-12d3-a456-426614174000&player=987fcdeb-51a2-43f8-9b1d-789012345678
```

Without a `game` query param, the client will display an empty table.

## Build

Typecheck and build for production:

```bash
npm run build
```

Output is written to `dist/`.

## Preview Production Build

```bash
npm run preview
```

## Environment Variables

- `VITE_API_BASE`: API base URL (default: `http://localhost:5270`)

## Architecture

- `index.html` - Entry point with HUD overlay
- `src/main.ts` - App bootstrap + HUD updates
- `src/scene.ts` - State-driven three.js scene rebuilding
- `src/state.ts` - Client state cache with event emitter
- `src/net.ts` - SignalR + REST state fetching
- `src/tileAtlas.ts` - Procedural tile texture atlas generator (27 Changsha faces + back)
- `src/tile.ts` - Tile mesh factory with atlas UV mapping
- `src/types.ts` - DTO type definitions

## Phase 1 Features

- **Full GameState rendering** from REST `/api/game/{id}/state`
- **Real-time updates** via SignalR (refetches state on each event)
- **Procedural tile atlas**: 27 Changsha tile faces (bamboo/character/dot × 1-9) + tile back
- **State-driven scene**:
  - Wall pile with remaining count label
  - Each seat's concealed hand (face-up for viewer, face-down for others)
  - Open melds (face-up, 4th tile stacked for kongs)
  - Discard piles (6-column grid per seat)
  - Current-turn indicator (yellow ring)
- **HUD overlay**: Wall count, current turn player, your seat
- **Spectator mode**: Works without `player` param (all hands face-down)

## Phase 1 Out of Scope

- No click/hover/drag interaction (Phase 2)
- No perspective camera toggle (Phase 3)
- No tile animations (Phase 4)
- No lobby/create-game UI (Phase 5)

## Testing a Game

1. Start API: `dotnet run --project src/MahjongClaw.Api`
2. Create game via Blazor or REST (note game ID + player ID)
3. Start Autotable: `npm run dev`
4. Visit with query params: `http://localhost:5173?game=<id>&player=<id>`
5. Play turns via Blazor client or REST; Autotable updates in real-time
