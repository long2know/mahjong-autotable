# Test architecture

> **Status.** Phase K Wave 10 — Vasquez (QA lead). Living document.
> Updated each wave as the test suite evolves. The pyramid (§4)
> is the contract; the parallelism policy (§3) is the operational
> rule that keeps the suite fast AND deterministic.

This document is the canonical reference for **how tests are
organised, scheduled, and gated** in the Mahjong-Autotable
repository. It complements:

* `docs/agent-handoff-protocol.md` — squad-level co-ordination
  (stash discipline, lock-file, concurrent-agent safety §5).
* `tests/selectors.md` — Playwright selector inventory shared
  between Hicks (frontend) and Vasquez (QA).
* `tests/ci/lane-map.json` + `tests/ci/check-cross-lane-bundling.sh`
  — lane-discipline classifier consumed by CI.

## 1. Why this document exists

Through W1–W9 the test surface grew from ~60 facts (W0 baseline)
to 1880 facts (W9 gate). At that scale the suite started
exhibiting:

* Flaky cross-test contention on the shared SQLite database
  used by the Idempotency-store contract tests (W9 retro).
* Implicit conventions about *which* lane authors which kind of
  test that only lived in agent charters.
* Coverage gaps that nobody could see at a glance because the
  inventory was scattered across `Phase_K_W*/` directories.

W10 introduces this document as the single answer to all three.

## 2. Test categories

| Category    | Runner                  | Where                                                                   | Owner    |
| ----------- | ----------------------- | ----------------------------------------------------------------------- | -------- |
| Unit        | xUnit (`dotnet test`)   | `src/backend/tests/**/!(Phase_K_W*|Regression|Collections)/`            | Bishop   |
| Contract    | xUnit (`dotnet test`)   | `src/backend/tests/**/Phase_K_W*/<Agent>/`                              | Vasquez† |
| Regression  | xUnit (`dotnet test`)   | `src/backend/tests/**/Regression/`                                      | Vasquez  |
| Smoke       | xUnit (`dotnet test`)   | `src/backend/tests/**/Phase_K_W*/*SmokeFactsTests.cs`                   | Vasquez  |
| E2E         | Playwright (Chromium)   | `src/frontend/autotable-src/tests/e2e/`                                 | Vasquez  |
| Frontend UI | Vitest (`pnpm test`)    | `src/frontend/autotable-src/src/**/*.test.ts`                           | Hicks    |
| CI shell    | bash + bats             | `tests/ci/*.sh` + `tests/ci/*.bats`                                     | Vasquez  |
| Infra       | terraform validate + helm lint | `infra/terraform/**/*.tf`, `helm/**/Chart.yaml`                  | Apone    |

† Each agent authors the contract facts that probe their own
surface area; the files live under `Vasquez/` within the per-wave
phase dir to keep the test code in the QA test lane. The
`wave_subdir_overrides` in `tests/ci/lane-map.json` re-attributes
files under `Phase_K_W*/<AgentName>/` back to `<AgentName>` for
lane-discipline purposes.

## 3. Test parallelism policy (§3)

xUnit runs all tests **in parallel by default**, across multiple
test classes per assembly. The Mahjong-Autotable suite ships
under that default because:

* The vast majority of facts are pure reflection on the API
  assembly (no global state).
* Where global state IS touched, the test sets up its own
  isolated fixture (per-test temp SQLite DB, per-test WAF host).

That works *most* of the time. The W9 retro identified a class
of tests that DOES need to opt out of parallel execution:

> **Tests that share a process-level mutable resource that the
> test fixture cannot economically isolate.**

The canonical example is the SQLite + EF Core + the
`IDbContextFactory<MahjongDbContext>` singleton. Per-test temp
DB files isolate the *data*, but the EF model cache, the
internal command interceptors, and the connection pool are
*all* process-wide. Under heavy parallelism, EF occasionally
throws `InvalidOperationException` on a test that ran fine in
isolation. That's a parallelism artefact, not a real defect.

### 3.1. The `DbSerial` collection

Tests that touch the EF stack opt out of parallelism by
declaring membership in the `DbSerial` xUnit collection:

```csharp
[Collection("DbSerial")]
public sealed class MyIdempotencyTests
{
    // ... tests run in a single-threaded queue with all other
    // [Collection("DbSerial")] classes across the assembly.
}
```

The collection definition lives in
`src/backend/tests/Mahjong.Autotable.Api.Tests/Collections/DbSerialCollection.cs`:

```csharp
[CollectionDefinition("DbSerial", DisableParallelization = true)]
public sealed class DbSerialCollection { }
```

> **W10 status.** The collection is defined and documented.
> Bishop's W11 work attributes the surviving SQLite-heavy
> contract tests (`Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`,
> the W10 `RedisIdempotencyStoreLiveTests.cs`) into the
> collection. Vasquez ships the definition + the policy doc
> in W10; the migration is Bishop's W11 deliverable.

### 3.2. When NOT to use `[Collection("DbSerial")]`

Any test that DOESN'T touch EF Core / SQLite / the WAF singleton
MUST stay outside the collection. Putting a pure reflection test
into `DbSerial` is a regression: it serialises a fact that has
no reason to be serial, slowing the suite for everyone.

### 3.3. Future collections

If a SECOND class of process-level contention emerges (e.g.
Redis container shared across tests in CI), a new collection
gets named for it (e.g. `RedisSerial`). The naming pattern is
`<Resource>Serial` so the collection name self-describes its
purpose.

## 4. Coverage pyramid (§4)

The classic Cohn pyramid, applied to this codebase:

```
                ╱─────────────╲
               ╱   E2E (~25)   ╲      ← Playwright, slow, signal
              ╱─────────────────╲
             ╱  Contract (~600)  ╲    ← Phase_K_W*/<Agent>/ + Regression
            ╱─────────────────────╲
           ╱     Unit (~1300)      ╲  ← reflection-light, fast, deterministic
          ╱─────────────────────────╲
```

Wave 10 baseline (this document's authorship):

| Tier      | Count (approx) | Lane-owner mix              |
| --------- | -------------- | --------------------------- |
| Unit      | ~1300          | Bishop ~70%, Hicks ~20%, Vasquez ~10% |
| Contract  | ~600           | Vasquez ~50% (cross-lane probes), Bishop ~30%, Hicks ~10%, Apone ~10% |
| Regression| ~80            | Vasquez 100% (Wave1Through* sweep) |
| Smoke     | ~110           | Vasquez 100% (W*SurfaceSmokeFactsTests) |
| E2E       | ~25            | Vasquez authors specs; Hicks owns the selectors                |
| CI shell  | ~30            | Vasquez (`tests/ci/*.sh` + bats wrappers)                      |

### 4.1. Why the pyramid is **inverted at the contract tier**

A naive Cohn pyramid would have unit tests at ~5× the contract
count. The Mahjong-Autotable codebase has contract tests at a
roughly 0.5× ratio to unit because:

* The cross-agent contract surface is BROAD (Bishop's audit, Hicks's
  bundle topology, Apone's CI workflows, Vasquez's lane-discipline
  classifier). Each surface needs reflection-defensive pins so a
  silent lane regression doesn't slip past the gate.
* Reflection-defensive contract tests are CHEAP (they're effectively
  unit tests on the API surface metadata) so they fit the pyramid
  *base* even though they're titled "contract".

### 4.2. Coverage gaps (W10 inventory)

The gap analysis below drives W11+ work assignments:

* **E2E coverage of the tournament bracket workflow** —
  W10 adds the canonical-no-fallback spec
  (`bracket-canonical-no-fallback.spec.ts`); W11 needs the
  end-to-end bracket creation → advancement → final happy path.
* **Contract coverage of the Janus gradual-degradation modes** —
  W10 ships 7 contract facts (`BishopW10JanusGradualDegradationTests`,
  `BishopW10JanusMountpointLifecycleTests`); the negative-path
  facts (e.g. degraded mode under network partition) are W11.
* **Unit coverage of the Dutch-Swiss pairing engine** —
  W10 ships 7 contract facts (`BishopW10DutchSwissPairingTests`)
  pinning surface area; the *algorithmic* unit facts (round-pairing
  determinism, bye distribution, max-rounds termination) are W11.
* **Frontend Vitest** — Hicks currently runs the Vitest suite as
  a separate `pnpm test` step. W11 should fold it into the
  backend `dotnet test` gate via a top-level `make test` so the
  pyramid is measured uniformly.
* **Infra contract tests** — W10 ships 15 Apone-surface facts
  (`AponeW10InfraContractTests`); the actual `terraform plan`
  + `helm lint` invocations remain shell-level. W11 should add
  contract-level pins for the prod-env helm release manifest
  parity.

### 4.3. Closed gaps (Phase K Wave 11)

The following W10 inventory gaps were closed during W11:

* **RedisIdempotencyStore end-to-end** — W10 shipped reflection-
  defensive contract pins + Testcontainers smoke. W11 adds an
  integration test that drives the store through the actual API
  surface (`POST` with `Idempotency-Key`, replay returns the same
  body, conflicting payload returns 409/422). File:
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Vasquez/RedisIdempotencyStoreIntegrationTests.cs`.
* **JanusReadinessSupervisor with a fake Janus server** —
  W10 reflection-defensive type pins flip to a real
  contract integration in W11. The fake Janus surface is a
  TestServer-hosted HTTP shim that returns the documented Janus
  `/info`/`/admin/list_handles` shapes; the supervisor's
  readiness probe is asserted against canonical responses.
  File:
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Vasquez/JanusReadinessSupervisorIntegrationTests.cs`.
* **SignalR backpressure load scenario** — W10 unit/contract
  facts on metric names + the backpressure pump configuration
  add a W11 integration test that drives a small message queue
  saturation against the in-process hub and asserts the
  backpressure metric advances. File:
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Vasquez/SignalRBackpressureIntegrationTests.cs`.

Each integration test is reflection-defensive (skips when the
surface isn't present) so the W11 gate doesn't depend on a
specific shipping order between Bishop's surfaces and Vasquez's
harness.

### 4.4. Coverage gaps (W11+ inventory)

The following items remain open and drive W12+ assignments:

* **Tournament bracket E2E happy path** — W11 ships
  `deep-link-action-routing.spec.ts` for the entry surface;
  the full bracket creation → seeding → advancement → final
  pixel-level pyramid is W12+.
* **Frontend Vitest unification** — Still tracked. `make test`
  fold-in is a multi-wave change (toolchain decision pending
  Hicks's W12 brief).
* **JWT rotation rehearsal end-to-end** — W11 ships the workflow
  shape contract; the actual rehearsal job invocation against
  a staging cluster is operator work tracked in Apone's
  W12+ inventory.
* **Multi-region prod health-check edge cases** — W11 ships
  the probe matrix; the negative-path facts (one region degraded
  → traffic shifted) are W12 contract work.

### 4.5. Anti-patterns to avoid

* **Don't write integration tests that boot the full WAF host
  for assertions reachable via reflection.** Reflection-defensive
  contract facts are O(ms); WAF-host tests are O(seconds) and
  belong in the `DbSerial` collection.
* **Don't duplicate a Playwright E2E with a Vitest unit.**
  E2E specs are signal-only — they exist to catch regressions
  that the lower tiers cannot see (real DOM, real bundling).
  Anything reachable from a pure component import goes in
  Vitest.
* **Don't author cross-lane contract tests OUTSIDE
  `Phase_K_W*/Vasquez/`.** The lane classifier accepts
  `Phase_K_W*/<AgentName>/` as the author's lane, so a
  Vasquez probe of Bishop's surface in `Phase_K_W*/Bishop/`
  would silently re-attribute. Keep cross-lane probes under
  Vasquez.

## 5. Gates

| Stage          | Gate                                          | Owner                       |
| -------------- | --------------------------------------------- | --------------------------- |
| PR build       | `dotnet test src/backend/Mahjong.Autotable.slnx` | required-for-merge       |
| PR build       | `pnpm --filter autotable-src test`               | required-for-merge       |
| PR build       | `pnpm --filter autotable-src playwright test`    | required-for-merge       |
| PR build       | `tests/ci/check-cross-lane-bundling.sh --strict` | required-for-merge       |
| Nightly        | `tests/ci/check-cross-lane-bundling.sh --repo-mode` | reporting-only           |
| Release tag    | Full pyramid + `infra/terraform plan -refresh-only` | required-for-tag       |

The wave gate (e.g. "W11 gate ≥ 2200/0/0") is the **backend
xunit suite total** measured against the previous wave's high
water mark. Skips count negatively against the gate — the
zero-skip streak (W0 → W11 = 25 waves of zero skips) is a
deliberate invariant. Tests that cannot pass yet are written
with reflection-defensive guards (`if (t is null) return;` or
`_ = result;`) so they STAY PASS while documenting the forward
expectation.

## 6. Concurrent-agent test safety

When multiple agents work on the test surface simultaneously,
the protocol is identical to the production-code protocol
documented in `docs/agent-handoff-protocol.md` §3 + §5:

* `.work/squad-git-lock` for the critical section.
* `.work/<agent>-w<N>-safe/` for backups of in-flight work.
* `shared_files` lane-map entry for files that legitimately
  span lanes (`tests/selectors.md`, `docs/agent-handoff-protocol.md`,
  the `Shims/` directory tree, and the PWA workflow pair — see
  `docs/agent-handoff-protocol.md §5.9` for the registry policy).
* Rebase-inside-flock so push races don't lose work.

The W9 retro called out that `git stash --include-untracked`
under concurrent execution wiped untracked agent dirs twice.
The W10 mitigation is documented in §5 of the handoff doc:
use `.work/<agent>-w<N>-safe/` (gitignored, off the index) for
backups, NEVER `git stash --include-untracked` for protective
checkpoints.

---

*Phase K Wave 11 — Vasquez (QA). Update every wave as the
suite evolves. Linked from `.squad/agents/vasquez/charter.md`.*
