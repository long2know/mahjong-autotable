# Mahjong Claw — Project Plan

## 1) Objectives
- Implement Traditional Chinese Mahjong (official wall construction, turn flow, melds, win detection, scoring) for 4 players.
- Ship a .NET 10 backend with EF Core (SQLite) + real‑time SignalR updates.
- Deliver a Web UI (Blazor WASM preferred) with responsive layout and game visuals.
- Provide automated tests, CI, and documentation.

## 2) Architecture Overview
**Backend (.NET 10 / ASP.NET Core Minimal API)**
- API endpoints for game lifecycle + player actions.
- JWT auth for players.
- SignalR hub for pushing game state.
- Rules engine encapsulated in domain services.

**Persistence (EF Core + SQLite)**
- Entities: Game, Player, Tile, Wall, Hand, DiscardPile, Meld.
- Migrations tracked in repo.

**Front‑end (Blazor WASM)**
- Real‑time updates via SignalR.
- Visuals for hand/discard/wall/melds.
- Mobile‑friendly responsive layout (future MAUI reuse).

## 3) Milestones & Phases
### Phase 0 — Repo & Foundations
- [ ] Initialize .NET 10 solution structure
- [ ] Add base documentation & conventions
- [ ] CI pipeline scaffold (GitHub Actions)

### Phase 1 — Core Domain & Rules Engine
- [ ] Tile generation (144 tiles) + shuffle
- [ ] Wall build (4 walls, 18 stacks x 2 tiles)
- [ ] Break wall + deal logic (East 14, others 13)
- [ ] Turn order, draw/discard
- [ ] Meld detection (chow/pung/kong)
- [ ] Win detection + baseline scoring
- [ ] Unit tests for rule engine

### Phase 2 — Backend API + Persistence
- [ ] EF Core models + SQLite migrations
- [ ] Game lifecycle endpoints: create/join/start
- [ ] Action endpoints: draw/discard/meld/win
- [ ] Game state serialization
- [ ] SignalR hub + events
- [ ] Integration tests for API

### Phase 3 — Front‑End MVP
- [ ] Blazor WASM project
- [ ] Lobby + join/start UI
- [ ] Game board (hand/discards/wall/melds)
- [ ] Action UI (draw/discard/meld)
- [ ] SignalR subscription + state rendering
- [ ] Responsive layout pass

### Phase 4 — Quality & Release
- [ ] Expanded test coverage
- [ ] Performance pass (state diffing, payload sizes)
- [ ] Documentation (API + rules)
- [ ] Deployment notes & Docker (optional)

## 4) Data Model (Draft)
- **Game**: Id, Status, CurrentPlayerId, DealerId (East), WallId, Round, CreatedAt
- **Player**: Id, Name, Seat (East/South/West/North), HandId, Score
- **Tile**: Id, Suit, Rank, IsFlower, IsSeason, OwnerType (Wall/Hand/Discard/Meld)
- **Wall**: Id, Tiles (ordered)
- **Hand**: Id, PlayerId, Tiles
- **DiscardPile**: Id, GameId, Tiles
- **Meld**: Id, GameId, PlayerId, Type (Chow/Pung/Kong), Tiles

## 5) API Endpoints (Initial)
- POST `/api/game` — create game
- POST `/api/game/{id}/join`
- POST `/api/game/{id}/start`
- POST `/api/game/{id}/draw`
- POST `/api/game/{id}/discard`
- POST `/api/game/{id}/meld`
- POST `/api/game/{id}/win`
- GET `/api/game/{id}/state`

## 6) SignalR Events
- `GameStateUpdated`
- `PlayerJoined`
- `GameStarted`
- `TileDrawn`
- `TileDiscarded`
- `MeldDeclared`
- `GameEnded`

## 7) Risks / Open Questions
- Exact scoring rules (Chinese Official) need confirmation + reference spec.
- State model for hidden info (hands) across clients.
- Security model for player auth (JWT flow design).
- Determinism in wall break + dice handling.

## 8) Immediate Next Steps
1. Scaffold .NET 10 solution + projects (Api, Domain, Persistence, Tests, Client).
2. Add EF Core models + initial migration.
3. Implement rules engine core (tiles, wall, deal, draw/discard).
4. Stand up minimal API endpoints + SignalR hub.

## 9) Acceptance Criteria (Phase 1 MVP)
- 4 players can create/join/start a game.
- Server enforces valid turns and tile actions.
- Wall/draw/discard/meld flows work end‑to‑end.
- Win detection triggers game end with scoring summary.
- Clients receive real‑time updates.
