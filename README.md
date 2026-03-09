# Mahjong Claw

Multiplayer Traditional Chinese Mahjong (backend + rules engine + web UI).

## Prerequisites
- .NET SDK 10.x

## Build
```
dotnet build MahjongClaw.slnx -c Release
```

## Run API
```
dotnet run --project src/MahjongClaw.Api
```

## Run Client
```
dotnet run --project src/MahjongClaw.Client
```

## Test
```
dotnet test MahjongClaw.slnx -c Release
```

## UI Smoke Tests (Playwright)
Set the client base URL (required) and API base URL (optional, defaults to `http://localhost:5270`) and run:
```
PLAYWRIGHT_BASE_URL=http://localhost:5299
PLAYWRIGHT_API_URL=http://localhost:5270
npm install
npm run test:e2e
```

Or run the full local smoke flow (starts API + Client automatically, then executes Playwright):
```
./scripts/run_playwright_smoke.sh
# or
npm run test:e2e:smoke
```

To override the Playwright command (for example, run only a subset/spec), set `PLAYWRIGHT_E2E_COMMAND`:
```
PLAYWRIGHT_E2E_COMMAND='npx playwright test tests/e2e/smoke.spec.ts'
./scripts/run_playwright_smoke.sh
```

If dependencies are already installed (for example in CI after `npm ci`), skip reinstalling inside the smoke script:
```
PLAYWRIGHT_SKIP_NPM_INSTALL=1 ./scripts/run_playwright_smoke.sh
```
