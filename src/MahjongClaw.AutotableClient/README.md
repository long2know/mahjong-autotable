# Mahjong Claw - Autotable Client (Phase 0)

TypeScript + three.js 3D client for Mahjong Claw.

## Setup

```bash
npm install
```

## Development

Start the Vite dev server:

```bash
npm run dev
```

Open http://localhost:5173 in your browser. The scene will display a 136-tile wall arrangement in top-down orthographic view.

SignalR will attempt to connect to the game hub at `http://localhost:5270/hubs/game`. If the API is not running, the connection will fail gracefully and log an error to the console.

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

- `VITE_HUB_URL`: SignalR hub URL (default: `http://localhost:5270/hubs/game`)

## Architecture

- `index.html` - Entry point
- `src/main.ts` - App bootstrap
- `src/scene.ts` - Three.js scene, camera, renderer, 136-tile wall rendering
- `src/net.ts` - SignalR connection and event subscriptions
- `src/types.ts` - DTO type definitions

## Phase 0 Scope

- Static 136-tile wall visualization (4 walls × 17 tiles × 2 rows)
- Orthographic top-down camera
- SignalR connection with event logging (non-blocking if API is offline)
- Placeholder colored box tiles (no textures)

Next phases will add state-driven rendering, interaction, and animations.
