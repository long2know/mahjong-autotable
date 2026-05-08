# Mahjong Autotable — Modern React Frontend

React 19 + Fluent UI 9 + Vite shell hosting the modernized mahjong UI,
including the **Changsha v1** game surface at `/changsha`.

## Run

From this directory (`src/frontend/modern`):

```bash
npm install
npm run dev      # starts Vite on http://localhost:5173
npm run build    # tsc -b && vite build  (produces dist/)
npm run preview  # preview a production build
```

The dev server proxies these paths to the backend on `localhost:5114`:

| Path         | Target                  | Notes                               |
|--------------|-------------------------|-------------------------------------|
| `/api`       | `http://localhost:5114` | REST endpoints                       |
| `/autotable` | `http://localhost:5114` | Bundled autotable Parcel app         |
| `/hubs`      | `http://localhost:5114` | SignalR hubs (`ws: true` for WS)     |

Backend run (in another terminal): `dotnet run --project src/backend/src/Mahjong.Autotable.Api`.

## Routes

- `/`          — legacy table UI (App.tsx, REST-based)
- `/changsha`  — Changsha v1 game surface (Phase 2 SignalR + iframe bridge)

## Changsha architecture

```
                ┌────────────────────────────────┐
                │   ChangshaTablePage (React)    │
                │   - HUD, hand panel, modals    │
                │   - SignalR via useChangshaGame│
                │                                │
                │   ┌──────────────────────────┐ │
                │   │  AutotableViewport       │ │
                │   │  <iframe src=/autotable/>│ │
                │   └──────────────────────────┘ │
                └────────────────────────────────┘
                          │ postMessage
                          ▼
                ┌────────────────────────────────┐
                │  autotable iframe (vendored)   │
                │  + changsha-bridge-receiver.js │
                └────────────────────────────────┘
                          │
                          ▼
                ┌────────────────────────────────┐
                │  Backend SignalR hub           │
                │  /hubs/changsha (Bishop)       │
                └────────────────────────────────┘
```

### Key modules (`src/changsha/`)

| File                       | Role                                                         |
|----------------------------|--------------------------------------------------------------|
| `types.ts`                 | TypeScript types reconciled with `changsha-signalr-contract.md`. |
| `tileUtils.ts`             | Tile id → suit/rank/label helpers.                           |
| `signalrClient.ts`         | Strongly-typed `HubConnection` factory + invoke wrappers.    |
| `changshaReducer.ts`       | `useReducer` action handlers, one per server event.          |
| `useLiveChangshaGame.ts`   | Live SignalR hook: connection, reducer, action callbacks.    |
| `useChangshaMockGame.ts`   | Offline mock state hook for UI-only work.                    |
| `useChangshaGame.ts`       | Picks live or mock at mount (env + localStorage override).   |
| `autotableBridge.ts`       | postMessage bridge (parent → child); `diffAndSend` helper.   |
| `components/`              | Fluent UI 9 components (HUD, dice modal, hand, claim, fan).  |
| `components/TileFace.tsx`  | SVG tile face renderer (27 tiles, face-down, claim glow).    |

### Mock vs live mode

`useChangshaGame` selects the implementation based on:

1. `localStorage.getItem('changsha.useMock')` — `'1'` forces mock,
   `'0'` forces live, `null` falls back to:
2. `import.meta.env.DEV` — dev defaults to mock, prod defaults to live.

The Mode toggle button at the top of `/changsha` flips this preference
and reloads the page (a reload is required so the unused hook can be
cleanly torn down).

### Bridge protocol

Documented in `docs/rules/changsha-autotable-bridge.md`. Phase 2 is
parent-→-child only; Phase 3 will add canvas-event upstream.

## Smoke test

With backend running:

1. `npm run dev`
2. Open http://localhost:5173/changsha
3. Click "Mode: Mock state" to switch to **Live server** (forces live).
4. The connection banner should briefly show "Connecting…" then clear.
5. The autotable iframe should load and show the Changsha bridge overlay.
6. Real `DiceRolled` events from the hub render in the dice modal; real
   `TilesDealt` events appear in the hand panel via `<TileFace>` SVG.
