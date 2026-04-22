# Mahjong Autotable (Changsha-first)

Initial project structure for a Changsha-first Mahjong implementation based on `pwmarcz/autotable` behavior and flow.

## Structure

```text
src/
  backend/
    Mahjong.Autotable.slnx
    src/Mahjong.Autotable.Api/      # .NET 10 API + static hosting
  frontend/
    autotable/                      # baseline/autotable-first assets
    modern/                         # optional React + Fluent UI 9 + TS + Vite shell
infra/
  docker/
    Dockerfile                      # single-image targets for Linux deploy
.vscode/
  launch.json                       # F5 launch/compound configs
  tasks.json                        # backend/frontend tasks
```

## Backend foundation

- .NET 10 minimal API at `src/backend/src/Mahjong.Autotable.Api`.
- EF Core wired with provider switch:
  - `Sqlite` (default)
  - `PostgreSql`
  - `SqlServer`
- `TableSession` persists authoritative table state snapshots (`StateJson`) with optimistic version increments (`StateVersion`).
- Draw/discard loop slice endpoints:
  - `POST /api/tables` creates a 4-seat table and deterministically deals from a seeded wall.
    - Request: `{ "ruleSet": "changsha", "botSeatIndexes": [1,2,3], "seed": 12345 }` (`seed` optional; server-generated when omitted).
  - `GET /api/tables/{id}` returns persisted table state (including `stateVersion`, `actionSequence`, `phase`, `wall`, `hands`, `discardPile`, `metadata.seed`, `metadata.algorithmId`, and `integrity.stateHash`).
  - `POST /api/tables/{id}/actions/discard` submits a human discard through server validation.
    - Request: `{ "seatIndex": 0, "tileId": 87, "expectedStateVersion": 3 }` (`expectedStateVersion` optional optimistic concurrency token).
    - Rejections return structured contract payloads (`code`, `message`, `stateVersion`, `actionSequence`, `correlationId`).
    - Error codes now include `ROUND_NOT_ACTIVE`, `INVALID_PHASE`, `NOT_ACTIVE_SEAT`, `SEAT_NOT_FOUND`, `TILE_NOT_IN_HAND`, `CONCURRENCY_CONFLICT`, and `STATE_INVARIANT_BROKEN`.
    - State now includes `claimWindow` scaffolding metadata (opportunities + selected winner by precedence policy `hu > kong > pung > chow`) to support upcoming claim-resolution phases.
  - `POST /api/tables/{id}/bots/advance` advances bot seats through the same discard validation pipeline used by humans until a halt condition (`HumanTurn`, `MaxActionsReached`, `WallExhausted`).
    - Request: `{ "advanceUntilHumanTurnOrWallExhausted": true }` (default) to safely run until the next human decision point without client-side action budgeting.
    - Optional capped mode: `{ "advanceUntilHumanTurnOrWallExhausted": false, "maxActions": 8 }`.
  - `GET /api/tables/{id}/events` returns the append-only persisted action stream (`sequence`, action payload, `stateVersion`, `stateHash`, and timestamps).
    - Query options: `afterSequence` (exclusive lower bound) and `limit` (max 500, default 200).
  - `POST /api/tables/{id}/replay/verify` replays accepted discard actions from seed and returns integrity comparison metadata (`integrityMatch`, `expectedStateHash`, `replayedStateHash`).
    - Optional query: `strict=true` returns `409` with `STATE_INVARIANT_BROKEN` when replay integrity does not match.
  - State-mutating endpoints (`/actions/discard`, `/bots/advance`) now enforce replay integrity before applying new actions and reject mismatched snapshots with `STATE_INVARIANT_BROKEN`.

Key config (`appsettings.json`):

- `Persistence:Provider`
- `ConnectionStrings:Sqlite`
- `ConnectionStrings:PostgreSql`
- `ConnectionStrings:SqlServer`

## Local development

### VS Code F5

- **Full stack (backend + modern frontend):** select `F5 Full Stack (Backend + Modern Frontend)`.
- **Autotable baseline only:** select `Backend + Autotable Baseline`.
- Full stack F5 runs `npm install && npm run dev` for the modern frontend terminal session.
- The modern frontend now provides a playable tabletop loop with graphical tile rendering (4-seat layout, clickable human hand, bot auto-progression, center discard visualization, and strict replay verification under Advanced tools).

### CLI

Backend:

```bash
dotnet run --project src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj
```

Modern frontend (optional):

```bash
cd src/frontend/modern
npm install
npm run dev
```

## Docker (single image)

Build autotable-first runtime image:

```bash
docker build -f infra/docker/Dockerfile --target runtime-autotable -t mahjong-autotable:autotable .
```

Build modern-overlay runtime image:

```bash
docker build -f infra/docker/Dockerfile --target runtime-modern -t mahjong-autotable:modern .
```

Run:

```bash
docker run --rm -p 8080:8080 -v $(pwd)/data:/app/data mahjong-autotable:autotable
```

## Notes

- Frontend modernization is optional and incremental; no forced rewrite in this scaffold.
- Claim windows, kong variants, win validation, and scoring/settlement are still deferred to upcoming Changsha phases.
