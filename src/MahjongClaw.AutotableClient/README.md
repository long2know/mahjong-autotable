# Mahjong Claw - Autotable Client (Phase 4)

TypeScript + three.js 3D client for Mahjong Claw — state-driven rendering with animations, feedback, and settlement overlay.

## Prerequisites

1. **Start the .NET API server first:**
   ```bash
   # From repo root
   dotnet run --project src/MahjongClaw.Api
   ```
   API will run at http://localhost:5270

2. **No additional setup needed** — Phase 5 lobby lets you create/join games directly in the Autotable client (see below)

## Setup

```bash
npm install
```

## Development

Start the Vite dev server:

```bash
npm run dev
```

Visit http://localhost:5173/

**Phase 5 Lobby (shipped):**
- No `?game=` param → lobby appears
- Click "Create Game" → enter your name → "Create & Enter"
- Click "Start Game" → 3 bots auto-fill → play!

**Direct game link (optional):**
If you have a game ID, you can jump straight to it:
```
http://localhost:5173?game=<GAME_ID>&player=<PLAYER_ID>
```

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
- `src/main.ts` - App bootstrap, HUD updates, toast/overlay/audio integration
- `src/scene.ts` - State-driven three.js scene rebuilding with animations
- `src/state.ts` - Client state cache with event emitter
- `src/net.ts` - SignalR + REST state fetching with audio hooks
- `src/tileAtlas.ts` - Procedural tile texture atlas generator (27 Changsha faces + back)
- `src/tile.ts` - Tile mesh factory with atlas UV mapping
- `src/camera.ts` - Dual camera controller (flat/perspective) with smooth transitions
- `src/input.ts` - Raycaster for click-to-discard interaction
- `src/hud.ts` - Action buttons (Draw, Chow, Pung, Kong, Win, Pass) + View toggle
- `src/actions.ts` - REST API wrappers (draw, discard, meld, win)
- `src/animation.ts` - Generic tween manager with easing functions
- `src/toast.ts` - Toast notification system
- `src/overlay.ts` - Game end overlay with winner display
- `src/audio.ts` - Audio hook system (no-op, ready for Phase 5)
- `src/types.ts` - DTO type definitions

## Features (Phases 1-4)

### Phase 1: State-Driven Rendering
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

### Phase 2: Interaction
- **Click-to-discard**: Raycast tile selection in your hand
- **Action buttons**: Draw, Chow, Pung, Kong, Win (self-draw), Pass
- **Claim window**: 5-second offer after opponent discard
- **Client-side validation**: Meld legality checks, automatic tile selection
- **Error handling**: ActionResult pattern with user feedback
- **Input locking**: Prevents concurrent API calls

### Phase 3: View Modes
- **Dual cameras**: Flat (orthographic) and Perspective (3D angled)
- **Smooth transitions**: 300ms animated camera swap with easeInOutCubic
- **Per-seat perspective**: Camera rotates to put local player at bottom
- **Toggle mechanisms**: 
  - Keyboard: Press `p` key
  - HUD button: "View: [mode]" button (top-right)
- **Raycaster compatibility**: Click-to-discard works in both views

### Phase 4: Polish
- **Tile animations**:
  - Spawn animation: 250ms fade-in + Y-drop on tile placement
  - Meld scale pop: 200ms scale animation (0.8→1) when melds form
- **Turn indicator pulse**: Opacity oscillates 0.6-0.8 for visual feedback
- **Toast notifications**: Bottom-center, 3s auto-dismiss, replaces alert()
  - "Drew a tile", "Tile discarded", "[Meld] declared", "Win declared!"
  - Error messages with visual consistency
- **Game end overlay**: Full-screen with winner info, hand display, close button
- **Audio hooks**: No-op system ready for Phase 5 audio assets
  - Wired for 'draw', 'discard', 'meld', 'win' events

## Out of Scope (Phase 5 Complete)

- ✅ Lobby/create-game UI — SHIPPED in Phase 5
- No actual audio files (hooks only)
- No mobile touch controls polish
- No settings panel
- No spectator chat

## Testing a Game

1. Start API: `dotnet run --project src/MahjongClaw.Api` (from repo root)
2. Start Autotable: `npm run dev` (from this directory)
3. Visit http://localhost:5173/
4. Use lobby to create game → start → play
5. Click tiles to discard, use Draw/Meld/Win buttons
