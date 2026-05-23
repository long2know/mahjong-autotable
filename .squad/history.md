# Project Context

- **Owner:** {user name}
- **Project:** {project description}
- **Stack:** {languages, frameworks, tools}
- **Created:** {timestamp}

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase J Wave 7 (Hicks, 2026-05-XX) — replay viewer wiring + a11y + settings drawer + profile page

- Replay viewer rewired to `GET /api/games/{gameId}/replay` via new
  `replay-launcher.ts` with 404 fallback to in-memory playback.
- New app-wide tabbed settings drawer (`settings-drawer.ts`) persisting
  to `localStorage["mahjong.settings.v1"]`, mirrors sound + perspective
  toggles to existing Wave-2 keys.
- New player profile page (`profile-page.ts`) with stats grid + recent
  games (with replay links) + editable display name / avatar colour.
  Read-only mode opens for any leaderboard row via custom event.
- Replay footer gained prev/next-hand buttons, playback-speed dropdown
  (0.5×–4×), and an aria-live event counter.
- Accessibility sweep: `aria-*` coverage on every Wave-7 surface +
  Wave-6 touchups; new `tests/e2e/a11y.spec.ts` (axe-core, 5 specs)
  asserting zero serious/critical violations across lobby, leaderboard,
  settings drawer, profile page, replay viewer.
- Bundle hashes: `autotable-src.85bbb8ca.js` + `autotable-src.a7cd8ea4.css`
  (main).  Pruned stale `2391eb20.js` + `094cde3a.css`.
- Gates: TS strict ✅ · parcel ✅ · `playwright --list` ✅ (24 tests).
  Full e2e requires Apone's container; out-of-band locally.


### Phase J Wave 7 (Apone, 2026-05-24) — production hardening: multi-DB provider, k8s, backup, non-root container

- **Multi-provider EF Core.** Three subclasses (`SqliteAppDbContext`,
  `PostgresAppDbContext`, `SqlServerAppDbContext`) wrap a now-abstract
  `AppDbContext` base; runtime registers the active one and aliases
  `AppDbContext` to it so existing `GetRequiredService<AppDbContext>()`
  call sites stay unchanged. Selector: `Persistence__Provider`.
  Connection strings throw at DI-resolve time when missing (lazy).
- **Migrations are per-provider** under
  `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`. Each
  `dotnet ef migrations add` targets the matching subclass via
  `--context` + `--output-dir`. Sqlite keeps EnsureCreated + legacy
  CREATE-IF-NOT-EXISTS for back-compat; Postgres/SqlServer use
  `MigrateAsync` at startup.
- **`HasColumnType("TEXT")` is provider-portability poison** —
  works on SQLite, accepted by Postgres, but on SQL Server it
  collapses to a 4000-char column. Removed from `StateJson` /
  `EventsJson`; let EF Core pick provider-native unbounded type.
- **K8s sticky sessions are mandatory for WS / SignalR.**
  nginx-ingress cookie affinity (`affinity: cookie`, `mahjong_aff`).
  Without it a WS upgrade lands on pod A and frames hit pod B.
- **Pod Security Standard `restricted`** is achievable with the
  existing Dockerfile: `runAsNonRoot: true`, `runAsUser: 1000`,
  `readOnlyRootFilesystem: true` (writes go to PVC + emptyDir tmp).
- **Non-root Dockerfile gotcha:** newer `aspnet:10.0` ships with
  GID 1000 pre-occupied. Guard `groupadd/useradd` with `getent` so
  the build is idempotent across base-image revisions.
- **Postgres CI service container** is cheap (~6s spin-up). Matrix
  the test suite across Sqlite + Postgres; SQL Server's official
  image is too heavy for hosted runners (>5min unpack).
- **`Assert.NotEqual(string, string, ignoreCase: true)`** is gone
  from xunit 2.9.3. Use
  `Assert.False(string.Equals(..., StringComparison.OrdinalIgnoreCase))`.
- Gates: `dotnet build -c Release` ✅ · `dotnet test` 526/527 ✅
  (1 pre-existing replay-ordering failure in Bishop's endpoint,
  out of scope) · `tests/smoke/docker-build-smoke.sh` ✅ /health
  green under UID 1000.
