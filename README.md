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
Set the base URL to a running client instance and run:
```
PLAYWRIGHT_BASE_URL=http://localhost:5002
npm install
npm run test:e2e
```
