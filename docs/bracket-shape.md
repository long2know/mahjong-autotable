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
