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
```
