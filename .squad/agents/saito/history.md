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

### 2026-04-17: Autotable Reconnaissance — Modeling a Client After pwmarcz/autotable

**Task:** Read-only research into pwmarcz/autotable (Riichi Mahjong tabletop sim) to inform squad's new client architecture.

**Key Findings:**
- **License:** MIT code + mixed CC/OFL assets. ⚠️ Tile textures are CC BY-NC-SA (non-commercial); avoid reusing.
- **Stack:** TypeScript + three.js ^0.179 + Parcel 2 + WebSocket (ws library). No physics engine.
- **Networking:** Central WebSocket server, key-value state collections, 10Hz mouse sync with interpolation. Fully deterministic, state-driven. No peer-to-peer.
- **Tile Rendering:** Per-tile 3D Blender models (.glb) with texture mapping; InstancedMesh for optimization. Quaternion-based rotation, no atlas.
- **Physics:** Zero physics. Tiles are placed in slots; no tumble, bounce, or collision. Raycasting for pick; dragging constrained to horizontal plane.
- **View Toggle:** Orthographic (default) vs. Perspective; press 'p' or UI checkbox. Smooth 100ms animation between modes.
- **Slot-based Model:** Named slots (hand, wall, discard, payment) with allowed rotations and push relationships. Elegant avoids collision bugs.
- **Interpolation:** Mouse waypoints sent 10/sec with timestamps; client smoothly interpolates → 100ms latency, unnoticed in non-real-time game.

**Patterns to Adopt:**
1. Slot-based tile placement (no physics needed).
2. State-driven sync: state updates → client-side animation (not physics simulation).
3. Key-value server collections for game state.
4. InstancedMesh rendering for scale.
5. Raycasting + constrained dragging (no complex collision).

**Pitfalls to Avoid:**
1. Non-commercial asset licensing (commission original art instead).
2. Makefile asset pipeline (brittle in CI; consider embedded generation or Git LFS).
3. Orthographic-only projection (players struggle with far tiles; design UI/camera better).
4. No persistence (add light DB for replay/spectating).
5. Minimal UI/accessibility (plan colorblind variants, font scaling early).

**Deliverable:** Report at `.squad/research/autotable-recon.md` (599 words, all 9 sections covered).
