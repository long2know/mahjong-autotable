# Copilot instructions — mahjong-autotable

Changsha-style 4-player Mahjong: a **server-authoritative .NET 10 backend** plus a
**3D WebGL/Three.js frontend** (a hard fork of `pwmarcz/autotable`), shipped as a
**single Docker image**. One .NET process serves both the WebSocket game endpoint
and the static frontend bundle.

## Layout

- `src/backend/src/Mahjong.Autotable.Api/` — .NET 10 minimal API, Changsha rules
  engine, WS endpoint, static hosting. Solution: `src/backend/Mahjong.Autotable.slnx`.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/` — xUnit suite (~5,000+ tests).
- `src/frontend/autotable-src/` — TypeScript + Three.js source (Vite-built).
- `src/frontend/autotable/` — **the built bundle the backend serves** (regenerated
  by `npm run build`; do not hand-edit).
- `mobile/` — Capacitor wrapper. `docs/` — deep references. `.squad/` — multi-agent workflow state.

## Build / run / test / lint

Requires .NET 10 SDK and Node 20+.

**Backend** (from repo root):
```bash
dotnet run --project src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj   # serves http://localhost:5114/autotable/
dotnet build src/backend/Mahjong.Autotable.slnx -c Release
dotnet test  src/backend/Mahjong.Autotable.slnx
# Single test / class (xUnit filter):
dotnet test src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj \
  --filter "FullyQualifiedName~ChangshaStateMachineTests.Discard_RotatesTurn"
```

**Frontend** (from `src/frontend/autotable-src/`):
```bash
npm install
npm run build                 # vite build -> src/frontend/autotable/
npm run watch                 # rebuild on save (vite build --watch)
ESLINT_USE_FLAT_CONFIG=false npx eslint src        # ESLint 9 needs this flag — config is legacy .eslintrc.json
npx tsc --noEmit --strict --target es6 --module esnext \
  --moduleResolution bundler --types vite/client \
  --lib DOM,DOM.Iterable,es6,es2017 src/*.ts        # strict type-check (CI parity)
```

**E2E (Playwright)** targets the *running backend serving the built bundle*, not a
dev server. Build the frontend, start the backend, then:
```bash
npm run e2e:install                                       # one-time chromium install
E2E_BASE_URL=http://127.0.0.1:8080/autotable/ npm run e2e -- <spec>
```

**Pre-commit** (CI runs `pre-commit run --all-files`): includes signer-identity and
cross-file invariant guards (`always_run`), and `check-added-large-files --maxkb=512`
— compress newly added screenshots/binaries under 512 KB.

**Docker**:
```bash
docker compose up -d --build                              # http://localhost:8080/autotable/
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build   # Postgres overlay
```

## Architecture (the big picture)

- **Two-mode WebSocket switch** at `/autotable/ws` (`Autotable/AutotableWsEndpoint.cs`)
  is the central design decision. Per connection, the `?variant=` query param selects:
  - `?variant=changsha` (default) → **ChangshaRuntime**: server is authoritative over
    wall, deal, claims, scoring, and bots.
  - any other variant (`four_player`/`three_player`/`bamboo`/`minefield`) → **Relay**:
    server forwards client UPDATEs verbatim (upstream autotable parity), no rules engine.
- **Changsha engine** (`Changsha/`): `ChangshaStateMachine.cs` is pure functions over
  `ChangshaGameState`; `Changsha/Runtime/ChangshaGameRuntime.cs` is the async wrapper
  that holds a per-game `SemaphoreSlim` lock, schedules bots, persists snapshots, and
  fires `StateChanged` → `ChangshaToAutotableTranslator` → connection manager → clients.
- **Manual deal ceremony**: a 6-state sub-machine inside `ChangshaPhase`
  (BreakPointMarked → PickupRound1..3 → SingleTilePickup → DealerExtra) models the
  dice-roll/wall-break/batch-of-4 pickup flow. `DealMode.Manual` (WS default) drives it;
  `DealMode.Auto` deals atomically. Both converge to identical post-deal state.
- **Bots** (`Changsha/Bot/`): `ChangshaBotEngine.Resolve("easy"|"medium"|"hard")` returns
  a stateless `IChangshaBotStrategy`. **Medium is the default**; unknown difficulty falls
  back to Medium.
- **Persistence**: EF Core provider switch via `Persistence:Provider`
  (`Sqlite` default / `Postgres` / `SqlServer`). Snapshots write after every mutation
  with optimistic concurrency (`StateVersion`); writes are fire-and-forget off the WS
  hot path.
- **`/health`** returns DB connectivity, EF migration count, active game count, OAuth
  status, build SHA, and uptime — wired into the Docker `HEALTHCHECK`.

See `docs/architecture.md` for the full module breakdown (note: it predates the
Parcel→Vite swap; trust `package.json`/`docs/frontend-build-tooling.md` for tooling).

## Key conventions & gotchas

- **Frontend edits don't take effect until rebuilt.** The backend serves
  `src/frontend/autotable/` (the dist dir). After editing `autotable-src/`, run
  `npm run build` (or `npm run watch`). Bundle is `[name].[hash:8].[ext]` from Vite.
- **Lobby config lives in the URL.** `lobby.ts` writes `variant`/`dealMode`/`botCount`/
  `botDifficulty` to the query string and `location.replace`s; the URL is the source of
  truth, read at WS handshake.
- **Production fails fast without a JWT key.** With `ASPNETCORE_ENVIRONMENT=Production`
  the API refuses to boot unless `Authentication__JwtSigningKeys__0` (base64, ~48 bytes)
  is set; reuse the same key across restarts or all prior JWTs are invalidated. Keys bind
  from both `Auth:JwtSigningKeys` and `Authentication:JwtSigningKeys`.
- **Postgres connection-string key is `ConnectionStrings:PostgreSql`** (that exact
  casing, not `Postgres`); pair with `Persistence__Provider=Postgres`.
- **Backend tests** are organized into `Phase_K_W*` wave folders plus feature folders.
  xUnit 2.9.3; `xunit.runner.json` disables cross-collection parallelism for the
  throwaway-Postgres-DB isolation pattern. xUnit 2.9.3 has **no runtime/dynamic skip**
  (`Assert.Skip` is unsupported) — use `[Fact(Skip=...)]`.
- **Worktree caveat**: ~81 `Phase_K_W16/17/18` contract tests locate the repo root via
  `Directory.Exists(".git")` and fail inside a git *worktree* (where `.git` is a file).
  These are environmental, not regressions; they pass in a normal CI clone.
- **Smoke scripts** under `tests/smoke/*.sh` must stay executable (git mode `100755`) —
  `docker-smoke.yml` execs them directly, so `100644` fails CI with "Permission denied".

## Multi-agent (Squad) workflow

This repo uses the **Squad** framework (`.squad/`). When picking up an issue
autonomously, read `.squad/copilot-instructions.md` first. Highlights:
- Branch naming: `squad/{issue-number}-{kebab-case-slug}`; PRs reference `Closes #N`.
- Scope ownership: backend production code, backend tests, frontend, and architecture
  docs are owned by distinct agents — respect the lane you are working in.
- Durable decisions log is `.squad/decisions.md` (`.squad/decisions/inbox/` is gitignored
  transient state folded in during ceremonies).
