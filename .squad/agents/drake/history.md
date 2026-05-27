# Drake — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend +
autotable-derived TS frontend (Parcel-bundled). Single-page mahjong
table with WS + SignalR transport, in-memory game runtime, EF Core
SQLite (dev) / Postgres (prod) / SqlServer (prod) persistence.

**User:** Stephen Long. Standing directives: (1) "No pauses — keep
iterating until 100% done done." (2) All agents use
`claude-opus-4.7-xhigh`.

**Joined:** 2026-05-27, as a backend hotfix engineer. Stephen brought
me in to handle a stray runtime exception while Bishop, Frost, and
Hicks were deep in the Changsha dealing-ceremony rework.

**Charter status:** None yet. Stephen has not committed to keeping me
on the roster — first task is a one-off, and a charter only gets
written if I stick around past it.

**Team context I should know (snapshot at join):**

- Bishop owns backend `Changsha/Runtime/**` + `Autotable/**`.
  Currently on `fix/walls-facedown-and-pickup-state-machine`.
- Frost owns `Changsha/Dealing/**` (new module), `Changsha/Bot/**`,
  `Changsha/Scoring/**`. Currently on `feat/changsha-dealing-ceremony`.
- Hicks owns the frontend.
- Vasquez owns the test infrastructure (`TestInfrastructure/**` —
  e.g. `PostgresTestDatabaseLifetime.cs`) and runs playtests.
- Apone owns DevOps / CI / Docker / observability.
- Scribe handles decisions.md merges and orchestration logs.
- Ripley / Ralph / Ferro / Hudson — other specialists already on the
  roster, dormant at the time I joined.

**Lane rules learned the first day:**

- Don't touch other agents' active branches even adjacently.
- The squad's flock pipeline lives at `.work/squad-git-lock`. Always
  branch from `origin/main`, never from another agent's branch.
- Memos go in `.squad/decisions/inbox/<agent>-<short-handle>.md` and
  are gitignored — force-add with `git add -f`.
- Agent history files live under `.squad/agents/<agent>/history.md`
  and ARE tracked (no `-f` needed in principle, but the brief
  asked for `-f` belt-and-braces so I followed instructions).

## First task — PlayerStats.LastGameAt nullable hotfix (2026-05-27)

**Commit authored:** _TBD — recorded after squash-merge_

**Symptom:** Runtime `SqliteException 19 — NOT NULL constraint failed:
PlayerStats.LastGameAt` on `POST /api/identity` against a dev SQLite
file that pre-dated Phase J Wave 5.

**Root cause:** `Data/DatabaseBootstrapper.cs:301` declared the
SQLite-only defensive bootstrap CREATE TABLE for `PlayerStats` with
`LastGameAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'`. The EF
model (`Players/PlayerStats.cs:18` → `public DateTime? LastGameAt`)
and every EF migration + model snapshot across SQLite / Postgres /
SqlServer say nullable. The hand-rolled bootstrap was the only thing
shadowing the model — and it only ran on dev SQLite files that pre-
date the migration set.

**Fix:** Surgical single-file change to
`Data/DatabaseBootstrapper.cs`:
1. Corrected the CREATE: `"LastGameAt" TEXT NULL`.
2. Added a defensive remediation pass: PRAGMA-introspect the
   `notnull` flag on `LastGameAt`; if `1`, rebuild the table with
   the SQLite-recommended pattern and remap the sentinel default
   back to `NULL`.

No EF migration changes (they were already correct). No model snapshot
changes (already correct). No entity-config changes (no fluent
`.IsRequired()` was set).

**Verified:**
- `dotnet build` 0 errors.
- `dotnet test … --filter PlayerStats|PlayerProfile|DatabaseBootstrap`
  — 11/11 pass.
- Full suite — 5219 pass; 2 flaky `Autotable/MultiGameRoutingTests`
  (Bishop's lane) re-passed 8/8 in isolation, flake attributable to
  a concurrent test runner from another agent racing on the shared
  test DB.
- Fresh-DB runtime smoke: `/health` + `POST /api/identity` 200 OK.
- Broken-DB remediation: hand-seeded the pre-fix schema, booted,
  confirmed table rebuilt + data preserved + sentinel default mapped
  back to NULL.

**Memo:** `.squad/decisions/inbox/drake-playerstats-lastgameat-fix.md`

**Lane discipline observed:** Did not touch any of
`Changsha/Runtime/**`, `Changsha/Dealing/**`, `Changsha/Bot/**`,
`Changsha/Scoring/**`, `Autotable/**`, frontend, workflows, or
`TestInfrastructure/**`. The only file changed in product code was
`src/backend/src/Mahjong.Autotable.Api/Data/DatabaseBootstrapper.cs`.

**Pattern locked in:** The defensive `EnsureSqlite…TablesAsync`
bootstrappers in `DatabaseBootstrapper.cs` are effectively a
hand-rolled migration chain for the SQLite provider. Any future
schema change covered by one of those helpers MUST update both the
canonical EF migration AND the hand-rolled SQL in lockstep, or this
exact class of bug recurs.
