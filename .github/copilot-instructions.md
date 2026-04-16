# Copilot instructions for Mahjong Claw

## Build, test, and lint commands

### .NET (solution + unit tests)
- Restore: `dotnet restore MahjongClaw.slnx`
- Build (Release): `dotnet build MahjongClaw.slnx -c Release`
- Run unit test suite: `dotnet test MahjongClaw.slnx -c Release --no-build`
- Run CI's rules-correctness subset: `dotnet test MahjongClaw.slnx -c Release --no-build --verbosity normal --filter "FullyQualifiedName~MeldValidatorTests|FullyQualifiedName~RulesCorrectnessVectorsTests|FullyQualifiedName~ScoreCalculatorTests|FullyQualifiedName~RulesetHandAnalyzerTests"`
- Run a single xUnit test method: `dotnet test tests/MahjongClaw.Tests/MahjongClaw.Tests.csproj -c Release --filter "FullyQualifiedName~MahjongClaw.Tests.HandAnalyzerTests.IsWinningHand_ReturnsTrueForStandardHand"`

### Playwright e2e smoke tests
- Install JS dependencies: `npm ci`
- Run full e2e suite: `npm run test:e2e`
- Run a single spec: `npm run test:e2e -- tests/e2e/lobby-game-flow.spec.js`
- Run a single named Playwright test: `npx playwright test tests/e2e/lobby-game-flow.spec.js -g "creates human vs bot game and auto-seats bot players"`
- Run end-to-end local smoke flow (applies migrations, starts API + client, then runs Playwright): `npm run test:e2e:smoke`
- Override smoke command (example single spec): `PLAYWRIGHT_E2E_COMMAND='npx playwright test tests/e2e/home.spec.js' ./scripts/run_playwright_smoke.sh`

### Lint
- No dedicated lint script is currently defined in `package.json` or CI workflows.

## MCP servers

- Copilot CLI project-level MCP config is in `.github/mcp.json`:
  - server name: `playwright`
  - launch command: `npx -y @playwright/mcp@latest`
- VS Code workspace MCP config also exists in `.vscode/mcp.json` for editor-side MCP tooling.

## High-level architecture

- The solution is split into four runtime projects plus tests:
  - `src/MahjongClaw.Domain`: tile model, turn-flow validation, scoring, ruleset-aware win/meld validation, and bot advisors.
  - `src/MahjongClaw.Persistence`: EF Core SQLite entities + migrations.
  - `src/MahjongClaw.Api`: minimal API + SignalR hub. Most game orchestration is in `Program.cs` (`/api/game/*` endpoints, server-side bot turn execution, event broadcasting).
  - `src/MahjongClaw.Client`: Blazor WASM UI, `GameApiClient` DTO/client methods, live SignalR game page, ruleset formatting helpers.
- Ruleset architecture:
  - `MahjongRuleset` currently supports `ChineseOfficial` and `Changsha258`.
  - `RulesetResolver`, `RulesetHandAnalyzer`, and `RulesetMeldValidator` are the ruleset boundary; API paths should call these, not raw `HandAnalyzer`/`MeldValidator`.
- Runtime flow:
  1. Lobby creates/joins/starts games via `/api/game`.
  2. API persists authoritative state in SQLite and broadcasts hub events per game group.
  3. Client refreshes state from `/api/game/{id}/state` after events.
  4. Bot turns are executed server-side (`/api/game/{id}/bot/turn` and bot turn chaining after start/discard), with client-side triggering when current player is a bot.
- Test layers:
  - `tests/MahjongClaw.Tests`: broad domain coverage, including ruleset resolution, Changsha 258, turn flow, and bot advisors.
  - `tests/e2e`: Playwright smoke + deep board-layout/3D presentation assertions.

## Key conventions in this repository

- **Default ruleset is Changsha 258**: when ruleset input is omitted or blank, resolver and persistence defaults use `Changsha258`.
- **Ruleset strings are intentionally fuzzy-tolerant**: `RulesetResolver` normalizes noisy text and many aliases (including mixed-language inputs and metadata stripping). Use `RulesetResolver.Resolve(...)` rather than custom parsing.
- **Turn legality is shape-based**: draw/discard/meld/win checks use `TurnFlowValidator` with `(hand tiles + meld tiles)` plus declared-kong adjustments. Preserve this invariant when changing API actions.
- **Bot identity is name-prefix based**: both API and client treat `Name.StartsWith("Bot", OrdinalIgnoreCase)` as bot seats.
- **API/client DTO contracts are mirrored by design**: records in `src/MahjongClaw.Api/Program.cs` are duplicated in `src/MahjongClaw.Client/Services/GameApiClient.cs`; update both together.
- **SignalR compatibility aliases are intentional**: API emits canonical + alias event names (`PlayerJoined`/`PlayerJoin`, `TileDrawn`/`DrawnTile`, etc.), and client subscribes to both.
- **Game state depends on ruleset-aware wall sizing**: API state includes `Ruleset` and `WallTotal`; client wall/deck rendering uses these values and should not hardcode 144.
- **Seat/dealing order is fixed**: East → South → West → North; initial deal is 14 tiles for East and 13 for others.
- **Entity state values are string-backed**:
  - game status values: `"Waiting"`, `"Active"`, `"Completed"` (client display normalization maps to `"In Progress"`/`"Complete"`),
  - tile location values: `"Wall"`, `"Hand"`, `"Discard"`, `"Meld"`.
- **Playwright tests are tightly coupled to UI semantics**: ARIA labels and key board-layout class names (for example wall/compass/seat/discard structures) are test targets; update tests with UI refactors.
