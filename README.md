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
- `AppDbContext` and `TableSession` are placeholder persistence objects only.

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
- Rule logic implementation is intentionally deferred; this commit only establishes architecture and delivery plumbing.
