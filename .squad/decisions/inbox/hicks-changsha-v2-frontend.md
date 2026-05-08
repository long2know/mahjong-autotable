# Hicks · Changsha v1 Phase 2 — Frontend wiring

**Date:** 2026-05-08
**By:** Hicks (Frontend Dev)
**Branch:** `stlong/changsha-v1-phase2`

## Context

Phase 1 shipped UI chrome (HUD, dice modal, hand panel, claim modal,
fan panel) driven by a mock state hook. Phase 2 needed three things:
live SignalR wiring, an autotable 3D viewport bridge, and real tile
faces.

## Decisions

### 1. SignalR client architecture — reducer pattern

`useReducer` over Bishop's `ChangshaGameState` with one action per
server event from `changsha-signalr-contract.md`. The hook
(`useLiveChangshaGame`) owns the `HubConnection` lifecycle (start/stop,
onreconnecting/onreconnected/onclose) and exposes typed action
callbacks (`rollDice`, `discard`, `claim`, `declareKong`, `declareWin`,
`pass`, `reconnect`).

**Why:** Keeps event handling pure, testable, and trivially auditable
against the contract. One reducer file maps 1:1 to the contract event
table — when Bishop adds events, we add a single `case`.

**Reconnect strategy:** per contract §6, reconnection re-sends
`JoinTable` to trigger event-log replay. The hook calls
`invoke.reconnectGame` (alias for `JoinTable`) on
`HubConnection.onreconnected` with the cached `gameId`.

### 2. Mock vs live mode toggle

`useChangshaGame` is a thin shim picking `useLiveChangshaGame` or
`useChangshaMockGame` once at mount based on:

1. `localStorage('changsha.useMock')` — explicit user override
2. `import.meta.env.DEV` — dev defaults to mock, prod defaults to live

The page-level Mode toggle button writes the override and reloads.
Reload-on-toggle avoids the rules-of-hooks problem of switching between
two structurally different hook trees and prevents dangling SignalR
connections.

### 3. Bridge protocol direction — one-way for Phase 2

The autotable bundle is embedded as an iframe at `/autotable/`. The
bridge (`autotableBridge.ts` + `changsha-bridge-receiver.js`) is **one-way
parent → child** for Phase 2:

- Parent diffs `ChangshaGameState` snapshots and posts
  `phase`/`dice`/`breakPoint`/`tilesDealt`/`tileDiscarded`/`claimMade`
  messages.
- Receiver maintains a small mirror scene state, displays a debug
  overlay, and dispatches `CustomEvent`s on the iframe window for
  autotable scene code to react to (Phase 3 wiring).
- Receiver posts `ready` upstream once on init so the parent flushes
  its queued outbound messages.

**Why:** Demonstrates state propagation end-to-end (sufficient to prove
the channel) without coupling Phase 2 delivery to autotable mesh-event
plumbing, which is a deep three.js/raycaster integration. Phase 3 plan
documented in `docs/rules/changsha-autotable-bridge.md`.

### 4. Tile rendering — SVG over atlas decoding

Built `TileFace.tsx` as a pure SVG component:

- Wan: red Chinese rank numeral + 萬 glyph
- Tong: blue dot pip patterns 1–9
- Tiao: green bamboo sticks 2–9 plus a stylized "bird" for rank 1
- Face-down: gray back with dashed inner panel
- Highlighted variant `.changsha-tile-claim` with animated glow (used
  for the active claim window's tile)

**Why:** Atlas decoding requires reverse-engineering the bundled
autotable Parcel JS to extract texture coordinates per tile, which is
fragile against future autotable updates. SVG gives crisp scaling at
any size, no asset dependency, and an obvious extension path for
future suit additions (winds/dragons in honor-tile variants).

## Files added

- `src/frontend/modern/src/changsha/signalrClient.ts`
- `src/frontend/modern/src/changsha/changshaReducer.ts`
- `src/frontend/modern/src/changsha/useLiveChangshaGame.ts`
- `src/frontend/modern/src/changsha/useChangshaMockGame.ts` (renamed)
- `src/frontend/modern/src/changsha/useChangshaGame.ts` (rewritten)
- `src/frontend/modern/src/changsha/autotableBridge.ts`
- `src/frontend/modern/src/changsha/components/TileFace.tsx`
- `src/frontend/autotable/changsha-bridge-receiver.js`
- `docs/rules/changsha-autotable-bridge.md`
- `src/frontend/modern/README.md`

## Files modified

- `src/frontend/modern/vite.config.ts` — `/hubs` ws proxy
- `src/frontend/modern/src/changsha/components/PlayerHandPanel.tsx` —
  TileFace adoption
- `src/frontend/modern/src/styles.css` — `.changsha-tile*` classes
- `src/frontend/modern/src/pages/ChangshaTablePage.tsx` — iframe embed,
  connection banner, mode toggle, error toast
- `src/frontend/autotable/index.html` — single-line script tag for
  receiver

## Phase 3 deferrals

- Autotable canvas tile-click → `Discard` / `Claim` upstream wiring
- Atlas-based mesh tile rendering inside the iframe (currently the
  receiver only mutates a debug overlay; tiles still drawn by SVG in
  the parent hand panel)
- Tighten `postMessage(msg, '*')` to specific origin
- Code-split the 560 KB bundle (warning from Vite)
