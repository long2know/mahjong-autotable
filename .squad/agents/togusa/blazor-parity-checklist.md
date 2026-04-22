# Blazor Parity Checklist

**Agent:** Togusa (sonnet-4.5)  
**Branch:** `squad/autotable-phase5-lobby`  
**Date:** 2026-04-17  
**Purpose:** Document feature parity between Blazor client and Autotable client to inform retirement decision

---

## Overview

This checklist enumerates all features present in the Blazor client (`src/MahjongClaw.Client/`) and marks their status in the Autotable client (`src/MahjongClaw.AutotableClient/`).

**Status Legend:**
- ✅ **Shipped:** Feature fully implemented and working
- ⚠️ **Partial:** Feature exists but incomplete or differs significantly
- ❌ **Missing:** Feature not implemented
- 🔵 **N/A:** Not applicable (e.g., Blazor-specific infrastructure)

---

## Core Gameplay Features

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Create game | ✓ | ✓ | ✅ | POST /api/game/ |
| Join game | ✓ | ✓ | ✅ | POST /api/game/{id}/join |
| Start game | ✓ | ✓ | ✅ | POST /api/game/{id}/start |
| Draw tile | ✓ | ✓ | ✅ | POST /api/game/{id}/draw |
| Discard tile | ✓ | ✓ | ✅ | POST /api/game/{id}/discard |
| Declare Pung meld | ✓ | ✓ | ✅ | POST /api/game/{id}/meld |
| Declare Kong meld | ✓ | ✓ | ✅ | POST /api/game/{id}/meld |
| Declare Win | ✓ | ✓ | ✅ | POST /api/game/{id}/win |
| Bot opponents | ✓ | ✓ | ✅ | Server-driven, auto-play |
| SignalR real-time | ✓ | ✓ | ✅ | Reconnect + state refetch |
| Game state sync | ✓ | ✓ | ✅ | GET /api/game/{id}/state |

---

## Lobby & Room Management

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Lobby page | ✓ | ✓ | ✅ | Phase 5 |
| Create game form | ✓ | ✓ | ✅ | Name, mode, bots, ruleset |
| Human vs Bot mode | ✓ | ✓ | ✅ | 0-3 bots configurable |
| Human vs Human mode | ✓ | ✓ | ✅ | 0 bots |
| Join by game ID | ✓ | ✓ | ✅ | UUID paste + seat selection |
| Waiting room / pre-start | ✓ | ✓ | ✅ | Shows seated players |
| Start button (host only) | ✓ | ✓ | ✅ | Host can start game |
| Share game link | ✓ | ✓ | ✅ | Copy link to clipboard |
| Open tables list | ✓ (mock) | ❌ | ❌ | Blazor shows hardcoded tables; no list endpoint exists |
| Lobby metrics (players online, tables active) | ✓ (mock) | ❌ | ❌ | Hardcoded placeholders; no real data source |
| Ruleset selector | ✓ | ⚠️ | ⚠️ | Blazor has dropdown; Autotable locked to Changsha 258 |
| Game history / past games | ❌ | ❌ | ❌ | Neither client has this |

---

## Visual Presentation

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| 2D table view | ✓ | ❌ | ❌ | Blazor uses HTML/CSS grid |
| 3D table view | ❌ | ✓ | ✅ | Three.js scene |
| Tile rendering | ✓ (emoji placeholders) | ✓ (procedural atlas) | ✅ | Autotable has better tiles |
| Player hands | ✓ | ✓ | ✅ | Face-up for local, face-down for others |
| Open melds display | ✓ | ✓ | ✅ | Both show melds |
| Discard piles | ✓ | ✓ | ✅ | Per-seat discard area |
| Wall remaining count | ✓ | ✓ | ✅ | HUD + progress bar |
| Turn indicator | ✓ | ✓ | ✅ | Name + seat + visual highlight |
| Seat labels | ✓ | ✓ | ✅ | East/South/West/North |
| Player scores | ✓ | ✓ | ✅ | Visible in state/HUD |
| Round wind display | ✓ | ✓ | ✅ | Shown in HUD/center |
| Tile animations | ❌ | ✓ | ✅ | Spawn fade-in + scale pop (Phase 4) |
| Camera modes | ❌ | ✓ | ✅ | Flat orthographic ↔ perspective (Phase 3) |

---

## HUD & UI Controls

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Draw button | ✓ | ✓ | ✅ | Phase 2 |
| Discard (click tile) | ✓ | ✓ | ✅ | Raycasting + HUD actions |
| Meld buttons (Pung/Kong) | ✓ | ✓ | ✅ | Phase 2 |
| Win button | ✓ | ✓ | ✅ | Phase 2 |
| Pass button | ✓ | ✓ | ✅ | No-op action |
| Legal actions filtering | ✓ | ✓ | ✅ | Server sends legalActions[] |
| Action buttons positioned per-seat | ✓ | ✓ | ✅ | Bottom-center for local player |
| Toast notifications | ❌ | ✓ | ✅ | Phase 4 (replaces alert()) |
| Error messages | ✓ (alerts) | ✓ (toasts) | ✅ | Autotable uses toast system |
| Game info HUD | ✓ | ✓ | ✅ | Wall, turn, your seat |

---

## End-of-Game / Settlement

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Win overlay | ✓ | ✓ | ✅ | Shows winner, hand, score (Phase 4) |
| Winning hand display | ✓ | ✓ | ✅ | Open + concealed tiles |
| Winner name + seat | ✓ | ✓ | ✅ | Phase 4 |
| Score deltas | ✓ | ⚠️ | ⚠️ | Blazor shows points breakdown; Autotable shows final score only |
| Winning patterns (fan) | ✓ | ❌ | ❌ | Server DTO doesn't include fan breakdown |
| Play again / new game button | ✓ | ❌ | ❌ | Neither has this wired |
| Persistent scoreboard / history | ❌ | ❌ | ❌ | Neither client |

---

## Settings & Configuration

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Ruleset selection (Changsha vs Chinese Official) | ✓ | ⚠️ | ⚠️ | Blazor has selector; Autotable locked to Changsha |
| Volume / audio controls | ❌ | ❌ | ❌ | Neither client (audio hooks exist in Autotable but no assets) |
| Camera speed / sensitivity | ❌ | ❌ | ❌ | Neither client |
| Tile skin / theme | ❌ | ❌ | ❌ | Neither client |

---

## Admin / Debug Tools

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Bot turn endpoint (POST /bot/turn) | ✓ | ❌ | ❌ | Blazor UI has button; Autotable does not |
| Force draw / discard (debug) | ❌ | ❌ | ❌ | Neither client |
| Game state inspector / JSON viewer | ❌ | ❌ | ❌ | Neither client |
| Console logs | ✓ | ✓ | ✅ | Both use console.log |

---

## Navigation & Pages

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Home page | ✓ | ❌ | ❌ | Blazor has welcome page; Autotable goes straight to lobby |
| Lobby page | ✓ | ✓ | ✅ | Phase 5 |
| Game page | ✓ | ✓ | ✅ | Phases 0-4 |
| Not Found (404) page | ✓ | ❌ | ❌ | Blazor has catch-all; Autotable no routing lib |
| Counter page (demo) | ✓ | ❌ | 🔵 | Demo component, irrelevant |
| Weather page (demo) | ✓ | ❌ | 🔵 | Demo component, irrelevant |

---

## Mobile / Responsive Design

| Feature | Blazor Client | Autotable Client | Status | Notes |
|---------|---------------|------------------|--------|-------|
| Mobile-friendly lobby | ✓ | ✓ | ✅ | Both use responsive CSS |
| Mobile-friendly game view | ⚠️ | ⚠️ | ⚠️ | Blazor 2D is responsive; Autotable 3D not optimized for touch |
| Touch controls (tap to discard) | ✓ | ✓ | ✅ | Raycasting works on touch |
| Landscape mode optimization | ❌ | ❌ | ❌ | Neither client |

---

## Summary

### Autotable Client Completeness (Phase 5)

**Fully Implemented:**
- ✅ Core gameplay loop (draw, discard, meld, win)
- ✅ Real-time multiplayer via SignalR
- ✅ Lobby with create/join/start flow
- ✅ Waiting room overlay
- ✅ 3D scene rendering (superior to Blazor 2D)
- ✅ Tile animations & polish
- ✅ Camera modes (flat ↔ perspective)
- ✅ Toast notification system
- ✅ HUD with game state info

**Partially Implemented:**
- ⚠️ Ruleset selection (locked to Changsha 258; Blazor has dropdown)
- ⚠️ Score breakdown (final scores only; no fan/pattern display)
- ⚠️ Mobile optimization (works but not polished)

**Missing:**
- ❌ Open tables list (no API endpoint for this; both clients show mock data)
- ❌ Lobby metrics (players online, tables active — no real data source)
- ❌ Persistent game history / scoreboard
- ❌ Admin/debug tools (bot turn button, state inspector)
- ❌ Multi-ruleset support (only Changsha 258)
- ❌ Audio assets (hooks exist, no files)
- ❌ Home page / navigation router (single-page app by design)
- ❌ Winning patterns breakdown (server DTO limitation)

---

## Retirement Recommendation

### Can the Autotable client replace the Blazor client?

**For core gameplay:** ✅ **Yes**  
The Autotable client fully supports game creation, joining, playing, and winning. It has superior visuals (3D vs 2D), animations, and camera controls that the Blazor client lacks.

**For lobby features:** ✅ **Yes**  
Phase 5 delivers a functional lobby with create/join/start flow and waiting room overlay. The "open tables list" and "lobby metrics" features are placeholders in both clients (no backend support), so no regression.

**For admin/debug tools:** ❌ **No**  
The Blazor client has a "Bot Turn" button and other debug utilities that Autotable does not. If these are needed in production, they must be added to Autotable.

**For settings/config:** ⚠️ **Partial**  
Blazor has a ruleset dropdown; Autotable is locked to Changsha 258. If multi-ruleset support is required, Autotable needs the UI control (trivial to add).

**For mobile users:** ⚠️ **Mostly**  
Both clients work on mobile, but Autotable's 3D scene is less touch-optimized than Blazor's 2D grid. If mobile is a priority, polish touch controls.

### Verdict

**Autotable client is ready to replace Blazor for MVP deployment** with the following caveats:

1. **Accept Changsha 258 only** (or add ruleset selector in future)
2. **Accept no admin UI** (or port bot turn / debug tools)
3. **Accept no game history** (neither client has this; deferred post-MVP)
4. **Mobile users may prefer Blazor 2D** until touch UX is polished

If the above constraints are acceptable, **Blazor can be retired** after Phase 5 ships. Otherwise, keep both clients and route users based on preference (e.g., `/game` for Blazor, `/autotable` for 3D client).

---

## Next Steps (Post-Phase-5)

If retiring Blazor:
1. Add ruleset selector to Autotable lobby (1 day)
2. Add bot turn button to Autotable (debug feature, 0.5 day)
3. Polish mobile touch controls (2-3 days)
4. Redirect all Blazor routes to Autotable in production
5. Archive `src/MahjongClaw.Client/` (or delete after user migration period)

If keeping both:
1. Document the differences for users
2. Add a "Switch to 3D/2D" toggle in the nav
3. Maintain feature parity going forward (double the effort)

---

*Checklist complete. Autotable client is feature-complete for MVP with minor gaps.*
