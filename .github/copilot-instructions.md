# Copilot instructions for Mahjong Claw

## Build, test, and lint commands

### .NET (solution + unit tests)
- Restore: `dotnet restore MahjongClaw.slnx`
- Build (Release): `dotnet build MahjongClaw.slnx -c Release`
- Run unit test suite: `dotnet test MahjongClaw.slnx -c Release --no-build`
- Run a single xUnit test: `dotnet test tests/MahjongClaw.Tests/MahjongClaw.Tests.csproj -c Release --filter "FullyQualifiedName~MahjongClaw.Tests.HandAnalyzerTests.IsWinningHand_ReturnsTrueForStandardHand"`

### Playwright e2e smoke tests
- Install JS dependencies: `npm ci`
- Run full e2e suite: `npm run test:e2e`
- Run a single spec: `npm run test:e2e -- tests/e2e/lobby-game-flow.spec.js`
- Run end-to-end local smoke flow (applies migrations, starts API + client, then runs Playwright): `npm run test:e2e:smoke`
- Override smoke command (example single spec): `PLAYWRIGHT_E2E_COMMAND='npx playwright test tests/e2e/home.spec.js' ./scripts/run_playwright_smoke.sh`

### MCP servers
- Copilot CLI project-level MCP config lives in `.github/mcp.json`:
  - server name: `playwright`
  - launch command: `npx -y @playwright/mcp@latest`
- VS Code workspace MCP config also exists in `.vscode/mcp.json` for editor-side MCP tooling.

### Lint
- No dedicated lint script is currently defined in `package.json` or CI workflows.

## High-level architecture

- The solution is split into four runtime projects plus tests:
  - `src/MahjongClaw.Domain`: core Mahjong model/rules (`Tile`, wall/dealing, meld validation, hand analysis, score/fan evaluation).
  - `src/MahjongClaw.Persistence`: EF Core SQLite model (`GameEntity`, `PlayerEntity`, `TileEntity`, `MeldEntity`, `DiscardEntity`) and migrations.
  - `src/MahjongClaw.Api`: minimal API + SignalR hub. API endpoints under `/api/game/*` persist game state and broadcast real-time events.
  - `src/MahjongClaw.Client`: Blazor WebAssembly UI, with `Services/GameApiClient.cs` for HTTP calls and `Pages/Game.razor` for live game state + SignalR updates.
- Runtime flow:
  1. Lobby creates/joins/starts games via `/api/game`.
  2. API mutates SQLite-backed state with EF Core.
  3. API emits hub events on `/hubs/game` (grouped by `gameId`).
  4. Client listens to hub events, then refreshes authoritative state from `/api/game/{id}/state`.
- Test layers:
  - `tests/MahjongClaw.Tests`: rules-engine correctness and pattern-priority tests (xUnit + FluentAssertions).
  - `tests/e2e`: Playwright smoke scenarios for home/lobby/game flows.

## Key conventions in this repository

- **Seat and dealing order is fixed**: East → South → West → North; East receives 14 starting tiles, others 13. This order is used across Domain dealing logic, API start/deal logic, and client seat rendering.
- **Game and tile state are string-backed in persistence/API**:
  - game status values: `"Waiting"`, `"Active"`, `"Completed"` (client normalizes for display, e.g. `"Active"` → `"In Progress"`).
  - tile location values: `"Wall"`, `"Hand"`, `"Discard"`, `"Meld"` (and `DeadWall` is reserved in comments).
- **SignalR event compatibility aliases are intentional**: API broadcasts canonical names and legacy aliases (`PlayerJoined`/`PlayerJoin`, `TileDrawn`/`DrawnTile`, etc.), and the client subscribes to both.
- **API/client DTO shapes are duplicated by design**: request/response records are declared in `src/MahjongClaw.Api/Program.cs` and mirrored in `src/MahjongClaw.Client/Services/GameApiClient.cs`; keep them synchronized when changing contracts.
- **Game page live mode is query-parameter driven**: `/game?gameId=<guid>&playerId=<guid>` enables API actions and SignalR wiring; without `gameId`, `Game.razor` falls back to mock state.
- **Tile domain invariants are strict and relied on by tests**:
  - suited tiles use ranks 1-9,
  - winds/dragons use rank `0` plus required enum metadata,
  - flowers/seasons are bonus tiles and rejected by winning-hand analysis.
- **Playwright selectors depend on semantic UI labels/roles**: tests target ARIA labels like `"Mahjong wall preview"` and `"Center discard lane"`; keep those stable unless tests are updated together.
- **Phase scope emphasis (from `AGENT.md`)**: Phase 1 prioritizes Chinese rules engine + minimal API + SignalR + Blazor UI + CI/e2e smoke coverage.
