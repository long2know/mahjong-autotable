# Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-claw — Multiplayer Traditional Chinese Mahjong
- **Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, SignalR, Blazor WASM, Playwright
- **Created:** 2026-04-16

## Learnings

- Seeded as QA Automation Engineer during Squad init.

### 2026-04-17: Host Player Seat Collision in Lobby→Game E2E Flow
**Symptom:** Playwright e2e test `lobby-game-flow.spec.js` failed with 409 conflict when the seat-fill loop tried to join the host player's already-occupied seat.
**Root Cause:** The server auto-creates a host player when a game is created (e.g., at seat "East"). The test then iterates through all 4 seats calling `/join` for each, but the first call to the host's seat collides with the existing player.
**Fix:** Query `/api/game/:gameId/state` once to learn which seats are already occupied, then only attempt to `/join` for empty seats. Build a `Set` of occupied seats and skip them in the loop.
**Resilience:** The fix does not hardcode "East" — it dynamically adapts to whichever seat the server assigns the host, making it robust to future host-seating logic changes.
**Secondary Fix:** Added `exact: true` to "Draw" button selector to avoid strict-mode ambiguity with "Discard Last Draw" button.
**Outcome:** All 10 e2e smoke tests now pass (10/10 ✅).
