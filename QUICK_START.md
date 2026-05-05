# Quick Start - Play a Game

This guide gets you from zero to playing a complete Changsha Mahjong hand in under 2 minutes.

## Prerequisites
- .NET SDK 10.x installed
- Node.js 18+ installed

## Step 1: Start the API Server

```bash
cd /data/source/mahjong-claw
dotnet run --project src/MahjongClaw.Api
```

API will run at **http://localhost:5270**

Leave this terminal running.

## Step 2A: Play with Autotable Client (Recommended - 3D)

In a new terminal:

```bash
cd /data/source/mahjong-claw/src/MahjongClaw.AutotableClient
npm install  # first time only
npm run dev
```

Visit **http://localhost:5173/** in your browser.

- Click "Create Game" → enter your name → "Create & Enter"
- Click "Start Game" (3 bots will auto-fill empty seats)
- Play! Click tiles to discard, use action buttons to draw/meld/win

## Step 2B: Play with Blazor Client (Alternative - 2D)

In a new terminal:

```bash
cd /data/source/mahjong-claw
dotnet run --project src/MahjongClaw.Client
```

Visit **http://localhost:5299/** in your browser.

- Use the lobby to create a game
- Start and play

## Both Clients Can Run Simultaneously

You can run both the Autotable (3D) and Blazor (2D) clients at the same time:
- Autotable: http://localhost:5173/
- Blazor: http://localhost:5299/

Both connect to the same API at http://localhost:5270

## Troubleshooting

**Port already in use?**

Check what's using the port, then stop it manually.

**Database issues?**
```bash
# Nuke and rebuild database
rm -f src/MahjongClaw.Api/mahjongclaw.db*
dotnet run --project src/MahjongClaw.Api  # migrations auto-apply on startup
```

## What Next?

- **Run Tests:** `dotnet test MahjongClaw.slnx -c Release`
- **Run E2E Smoke:** `./scripts/run_playwright_smoke.sh`
- **Run Soak Check:** `SOAK_HANDS=4 ./scripts/run_soak_check.sh`
- **Full README:** See [README.md](README.md) for detailed docs
