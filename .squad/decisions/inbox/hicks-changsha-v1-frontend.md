# Decision: Changsha v1 Frontend — Phase 1 Component Inventory

**By:** Hicks (Frontend Dev)
**Date:** 2026-05-05
**Branch:** `stlong/changsha-v1`

## Summary

Phase 1 delivers the Changsha Fluent UI chrome as React components with mock data, establishing the visual framework that Phase 2 will wire to live SignalR.

## Component Inventory (Phase 1 — this wave)

| Component | File | Purpose |
|-----------|------|---------|
| `types.ts` | `changsha/types.ts` | TypeScript interfaces reconciled with Bishop's SignalR contract |
| `tileUtils.ts` | `changsha/tileUtils.ts` | Tile Unicode glyphs, labels, id derivation from contract |
| `useChangshaGame` | `changsha/useChangshaGame.ts` | Mock state hook with demo actions for all phases |
| `DiceRollModal` | `changsha/components/DiceRollModal.tsx` | Dice roll dialog with animation, break-point display |
| `BankerBadge` | `changsha/components/BankerBadge.tsx` | Banker indicator with wind label |
| `RoundWindIndicator` | `changsha/components/RoundWindIndicator.tsx` | Round/hand progress display |
| `ChangshaHud` | `changsha/components/ChangshaHud.tsx` | Top bar: banker + round + 4-player scores |
| `FanBreakdownPanel` | `changsha/components/FanBreakdownPanel.tsx` | Win pattern + payment breakdown table |
| `PlayerHandPanel` | `changsha/components/PlayerHandPanel.tsx` | Concealed tiles with discard buttons + melds |
| `ClaimPromptModal` | `changsha/components/ClaimPromptModal.tsx` | Claim options with 5s countdown |
| `ChangshaTablePage` | `pages/ChangshaTablePage.tsx` | Full page layout at `/changsha` route |

## Phase 1 / Phase 2 Split

### Phase 1 (this wave) ✅
- All UI components render from mock `ChangshaGameState`
- Demo controls panel cycles through all game phases
- Types reconciled with Bishop's SignalR contract
- `/changsha` route wired in `main.tsx`
- Build passes (`tsc -b && vite build`)

### Phase 2 (next wave) — deferred
- Replace `useChangshaGame` mock with real SignalR client (`@microsoft/signalr`)
- Wire to `/hubs/changsha` hub endpoint
- Embed autotable iframe/canvas in the placeholder div
- Real dice break-point projection onto the 3D wall
- Real tile rendering from GLB models (replace Unicode glyphs)
- WebSocket bridge for autotable upstream protocol
- Multiplayer seat management

## Key decisions
- **No react-router added:** Simple pathname check in `main.tsx` for `/changsha` route. Phase 2 may add react-router if more routes needed.
- **No Vitest added:** Test framework not configured in project. Skipped per scope guidance.
- **Tile IDs are numeric (0-107):** Reconciled from straw-man string IDs to match Bishop's contract exactly.
- **Phase names from contract:** Using `rollingDice`, `awaitingDiscard`, `awaitingClaim` etc. per SignalR contract.
