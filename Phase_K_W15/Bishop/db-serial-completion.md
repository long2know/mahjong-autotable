# Phase K Wave 15 — Bishop — DbSerial completion memo

> Closes the W14 Vasquez DbSerial migration memo
> (`Phase_K_W14/Vasquez/db-serial-migration-completion.md`).

## Background

The W9 commentary-usage / idempotency tests touch a real
EF `DbContext` backed by an on-disk SQLite database. xUnit's
default test-class-level parallelism races multiple writers
through the same SQLite file, surfacing intermittent
`SaveChanges` failures.

W10 (Vasquez) introduced the canonical xUnit collection
`"DbSerial"` (definition at
`tests/Mahjong.Autotable.Api.Tests/Collections/DbSerialCollection.cs`,
`DisableParallelization = true`) and W11–W13 progressively
migrated DB-touching test classes to opt in via
`[Collection("DbSerial")]`.

The W14 Vasquez memo
(`Phase_K_W14/Vasquez/db-serial-migration-completion.md`)
identified the two remaining W9 Bishop test files that had not
yet been migrated:

1. `tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`
2. `tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`

The memo instructed Bishop W15 to close the migration.

## What this wave delivered

Both files now carry `[Collection("DbSerial")]`. The xUnit
collection literal matches the canonical definition at
`tests/.../Collections/DbSerialCollection.cs`.

Each file's XML doc-comment received a short W15 paragraph
explaining the closure. No test-body or fixture logic changed —
the contract surface remains identical to W9.

## Reflection-asserted invariant

The closure is now hard-asserted at the test layer via
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/DbSerialCompletionTests.cs`:

* `EfCommentaryUsageMeterTests` carries the `"DbSerial"`
  collection.
* `IdempotencyStoreContractTests` carries the `"DbSerial"`
  collection.
* Both types live under the `Phase_K_W9.Bishop` namespace.
* The two attribute values point at the same canonical literal
  (`"DbSerial"`).

A future maintainer who accidentally drops the attribute
trips the reflection invariant — no more silent flake regression.

## Flake-reduction note for Vasquez W15

Per `docs/test-architecture.md §3.4 "DbSerial migration final
completion"`, the canonical post-merge step is for Vasquez to
run the full backend gate 3–5 times after this commit lands and
confirm a zero-flake streak across the runs. The pin lives in
the Vasquez W15 memo
(`Phase_K_W15/Vasquez/vasquez-phase-k-wave-15.md` §1).

## Closeout citation

* Files modified:
  * `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`
  * `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`
* Tests added:
  * `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/DbSerialCompletionTests.cs`
    (5 reflection-asserted facts).
