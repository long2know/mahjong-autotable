# Bracket persistence shape

Phase K Wave 12 — Bishop.

## Why

The W6–W10 tournament-bracket surfaces (single-elim, double-elim,
round-robin, Swiss, FIDE C04 Swiss) generated their pairings as
in-memory tuples. The format pleased the contract tests but lost the
state on a process restart and didn't survive a multi-replica
deployment. Wave 12 lands a durable, idempotent bracket store so:

1. Brackets survive a pod restart / Argo Rollouts rollover.
2. Replaying a game-complete event (e.g. via the audit trail) is a
   no-op — the natural key `(TournamentId, RoundNumber, MatchSlot)` is
   unique and the upsert path overwrites rather than duplicates.
3. The W13 operator dashboard can read the bracket state directly
   without round-tripping through `TournamentService`.

## Schema

| Column         | Type         | Notes                                  |
| -------------- | ------------ | -------------------------------------- |
| `Id`           | Guid PK      | Surrogate; natural key is the unique   |
| `TournamentId` | Guid         | Owning tournament                      |
| `RoundNumber`  | int          | 1-indexed                              |
| `MatchSlot`    | int          | 0-indexed slot within the round        |
| `SeedA`        | string       | Seed/playerId on side A                |
| `SeedB`        | string       | Seed/playerId on side B                |
| `WinnerSeed`   | string?      | Set by `RecordResultAsync`             |
| `Status`       | string       | `pending` / `active` / `completed` / `forfeit` / `bye` |
| `CompletedAt`  | DateTime?    | Set by `RecordResultAsync`             |

**Unique index**: `(TournamentId, RoundNumber, MatchSlot)`.

## Configuration

| Key                            | Default      | Notes                          |
| ------------------------------ | ------------ | ------------------------------ |
| `Tournament:BracketStoreImpl`  | `InMemory`   | `"Ef"` for production          |

## Idempotency contract

Both `UpsertAsync` and `RecordResultAsync` are idempotent on the
natural key:

* `UpsertAsync` overwrites `SeedA`, `SeedB`, `WinnerSeed`, `Status`,
  `CompletedAt` on the existing row when one is found. Re-applying the
  same payload is a no-op.
* `RecordResultAsync` is a strict update — it returns `null` when no
  row exists for `(TournamentId, RoundNumber, MatchSlot)`, otherwise
  stamps `WinnerSeed` + `Status` + `CompletedAt`. Replaying the same
  result yields the same row.

This pins the contract that replay-game-complete events can flow
through the store safely without bookkeeping at the caller.

## W12 vs W13

Wave 12 ships the **seam** — the entity, both implementations, the
migration, the contract tests, and the DI wire-up. `TournamentService`
does NOT yet call into the store on every `AdvanceMatchAsync` /
`ForfeitMatchAsync`; that hook lands in W13 once a strict
`MatchSlot` derivation is agreed (today's `TournamentMatch` entity
carries `Round` but no slot column; the W13 work adds slot
backfill + the call-site).

To opt in early, a wrapper in `Mahjong.Autotable.Api.Tournament` can
shadow `TournamentService.AdvanceMatchAsync` and call
`IBracketStore.RecordResultAsync` in parallel; both store
implementations are safe under contention.

## Migration

Table `BracketRecords` ships in W12 migration
`Phase_K_W12_Replays_Brackets_SignalRSeq` across Sqlite, Postgres,
SqlServer.

## Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Bishop/BracketStorePersistenceFacts.cs`:

* Both `InMemoryBracketStore` and `EfBracketStore` satisfy `IBracketStore`.
* `BracketStorageOptions.BracketStoreImpl` defaults to `"InMemory"`.
* Upsert → Get round-trips a row (both stores).
* Upsert is idempotent on the natural key (both stores).
* `RecordResultAsync` stamps the winner + completion time (both stores).
* `RecordResultAsync` replayed twice is idempotent (no row duplication).
* `ListAsync` orders rows by `(RoundNumber, MatchSlot)`.

## §4 — Tournament service integration (Phase K Wave 13)

Wave 13 wires `TournamentService` into the store seam introduced in
W12. The integration lives entirely behind an optional ctor
parameter — the store stays an opt-in dependency so the existing test
matrix continues to compile against a service constructed without it.

### §4.1 — Slot derivation without a schema change

`TournamentMatch` carries `Round` but no `MatchSlot` column. Rather
than land a fourth migration, the slot index is tracked **locally**
inside the bracket-emitting paths:

* `StartAsync` builds the first-round pairings sequentially and
  emits `BracketRecord` rows by walking the pairing list with a
  parallel slot counter (0-indexed).
* `AdvanceMatchAsync` / `ForfeitMatchAsync` re-derive the completed
  match's slot by matching `(SeedA, SeedB)` against the existing
  bracket rows in the same round. The match goes in as
  `Status = "completed"` (advance) or `"forfeit"`.
* The new next-round pairings emitted by `MaybeAdvanceRoundAsync` are
  appended to the slot tail — the helper looks up the current
  max-slot for the next round and increments from there. A bye
  carries the canonical seed literal `__bye__` exported from
  `TournamentService.BracketByeSeed`.

### §4.2 — Bye semantics

A first-round bye seats one playerId against `__bye__`; the
matching `BracketRecord` is emitted with
`Status = "bye"` and a populated `CompletedAt` so the row participates
in the bracket-shape API at parity with a played match.

### §4.3 — Idempotency

The W13 call-sites rely on the W12 upsert contract — replaying a
`StartAsync` (e.g. on a process restart) re-emits the same first-round
rows with the same `(TournamentId, RoundNumber, MatchSlot)` natural
key; the upsert overwrites in place rather than duplicating. The
result-stamping path (`RecordResultAsync`) is likewise replayable.

### §4.4 — Configuration

No new configuration in W13. The store implementation is selected
via the existing `Tournament:BracketStoreImpl` key documented above.
The ctor injection is wired in `Program.cs`; when the key is
`"None"` (or the IoC container resolves nothing) the service
operates exactly as in W12 — bracket rows are not emitted.

### §4.5 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/BracketStoreIntegrationTests.cs`:

* `StartAsync` upserts a `BracketRecord` per first-round pairing.
* First-round byes carry `Status = "bye"` + `SeedB = "__bye__"`.
* `AdvanceMatchAsync` stamps the corresponding row with
  `Status = "completed"`, `WinnerSeed`, and `CompletedAt`.
* `ForfeitMatchAsync` stamps the row with `Status = "forfeit"`.
* New next-round pairings emitted by an advance/forfeit are
  upserted into the store at the correct slot tail.
* Constructing `TournamentService` without a store leaves the
  service functional (no NRE on advance/forfeit).
