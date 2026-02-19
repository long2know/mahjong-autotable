# AGENT.md

## Synopsis
Build a multiplayer Traditional Chinese Mahjong game with a .NET 10 backend (minimal APIs / ASP.NET Core), EF Core + SQLite persistence, and a Web front‑end (Blazor WebAssembly preferred). The system must implement official Chinese wall construction, turn flow, melds, win detection, scoring, and real‑time updates via SignalR. Mobile is a later phase (MAUI).

## Scope (Phase 1)
- **Rules & mechanics**: Tile generation/shuffling, wall build (4 walls, 18 stacks x 2 tiles), break wall, deal (East 14, others 13), turn order, draw/discard, melds (chow/pung/kong), win detection, scoring baseline.
- **Backend**: .NET 10 minimal API, JWT auth, EF Core models + migrations (SQLite), API endpoints for game lifecycle/actions, SignalR hub for state updates.
- **Front‑end**: Blazor WASM (or SPA if needed), live game state view (hands, discards, wall, melds), responsive layout, basic interactions.
- **Testing**: Unit tests for rules engine, integration tests for API, CI via GitHub Actions.
- **Docs**: API + rules documentation, setup/run instructions.

## Out of Scope (Phase 1)
- Advanced AI opponents/bots.
- Matchmaking/ranked play.
- Custom variant rulesets (Hong Kong/Riichi/Taiwanese).
- Mobile app build (planned Phase 2).

## Deliverables
- Backend API + EF Core models/migrations
- Rules engine
- Web UI + SignalR updates
- Tests + CI
- Documentation
