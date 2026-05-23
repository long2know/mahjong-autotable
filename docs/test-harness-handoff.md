# Test harness hand-off — Phase K Wave 4

**Author:** Vasquez (QA)
**Audience:** Hudson (test-infra owner)
**Date:** Phase K Wave 4 bring-up

This note documents a flake observed during the Wave-4 bring-up and
the workaround Vasquez recommends so the suite stays at the
**1232/0/0** gate (target ≥1230) without intermittent
`ObjectDisposedException` noise during high-parallel xunit runs.

---

## Symptom

Under high xunit parallelism (default `maxParallelThreads = cpu count`
on the bring-up boxes used for Wave 4 — 8+ logical cores) the
cross-wave regression class
`Mahjong.Autotable.Api.Tests.Regression.Wave1ThroughKW4RegressionTests`
intermittently surfaces an
`ObjectDisposedException: Cannot access a disposed object. Object
name: 'IServiceProvider'.` during its
`InitializeAsync` phase (the call that materialises a fresh
`WebApplicationFactory<Program>` + temp Sqlite DB).

The flake never reproduces under serial / 2-thread runs. It also
never reproduces when the file is executed in isolation. The class
itself is conformant to xunit's `IAsyncLifetime` contract.

## Root-cause hypothesis

The regression class shares the same `WebApplicationFactory<Program>`
construction pattern as the rest of `Phase_K_W*` — each class spins
its own factory + `_factory.Server` warm-up in `InitializeAsync`.
At very high parallelism the factory-startup graph races against
the test-host's `IServiceProvider` lifecycle when another fixture
class is mid-teardown:

1. Class A's `DisposeAsync` schedules root-`IServiceProvider`
   disposal.
2. Class B's `InitializeAsync` enters `factory.Server` warm-up.
3. Class B's factory inherits a host whose root `IServiceProvider`
   was just disposed by Class A's dispose chain (the test host's
   default disposal pool is process-scoped, not factory-scoped).

The race window is narrow enough that 2-thread parallelism never
reproduces, but 8+ threads land on it ~1-in-30 runs.

## Recommended workaround (low-risk, ship for Wave 5)

Pin `maxParallelThreads = 2` for this assembly via a runner config:

```jsonc
// src/backend/tests/Mahjong.Autotable.Api.Tests/xunit.runner.json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 2
}
```

That alone removes the flake on Vasquez's repro box. The
`parallelizeTestCollections: true` line keeps wave-scoped collections
running concurrently so the wall-clock cost is < 10 s vs the
single-threaded baseline.

The same config also dampens the Wave-3
`Wave1ThroughKW3RegressionTests` race that I called out in
`docs/test-harness-handoff.md`'s predecessor note (if shipped).

## Suggested longer-term fix (Wave 6+)

Move the regression class's `WebApplicationFactory<Program>` onto a
shared `CollectionFixture` so the host lifecycle is owned by a
single xunit collection, not constructed-and-torn-down per
test-class. Sketch:

```csharp
[CollectionDefinition("regression-host")]
public class RegressionHostCollection : ICollectionFixture<RegressionHostFixture> { }

public sealed class RegressionHostFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string TempDb { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        TempDb = Path.Combine(dataDir, $"mahjong-reg-{Guid.NewGuid():N}.db");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={TempDb}");
        });
        _ = Factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        try { if (File.Exists(TempDb)) File.Delete(TempDb); } catch { }
        return Task.CompletedTask;
    }
}

[Collection("regression-host")]
public class Wave1ThroughKW4RegressionTests
{
    private readonly RegressionHostFixture _host;
    public Wave1ThroughKW4RegressionTests(RegressionHostFixture host) => _host = host;
    // …existing tests, swap `_factory` → `_host.Factory`
}
```

That refactor moves the warm-up cost out of every `InitializeAsync`
invocation (≈ 1.4 s saved per class) and eliminates the disposal race
by construction.

## Validation

After landing the workaround locally:

```bash
$ dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build
Passed!  - Failed: 0, Passed: 1232, Skipped: 0, Total: 1232
```

10 consecutive runs on the bring-up box: 10/10 green, 0 flakes.

## Out-of-scope concerns

- The `server/game.test.ts` file under `src/frontend/autotable-src/`
  raises `ReferenceError: describe is not defined` when the root
  `npx playwright test --list` glob picks it up. This is **NOT** the
  Wave-4 playwright config's fault — the e2e config at
  `src/frontend/autotable-src/tests/e2e/playwright.config.ts` is
  correctly scoped to `testDir: '.'` and only inspects e2e specs
  when invoked from that directory. Pre-existing issue; flagged here
  only so Hudson doesn't waste a cycle chasing it.

---

Filed by Vasquez during Phase K Wave 4 bring-up. Hand-off ready —
all Wave-4 specs already follow the reflection-defensive pattern,
so the workaround above is strictly a flake mitigation.

---

## Phase K Wave 5 — addendum (Vasquez)

The Wave-5 bring-up brief asked Hudson to introduce a
`CollectionFixture` for the regression class so the cross-wave
file (`Wave1ThroughKW5RegressionTests`) returns to running under
the default xUnit collection-per-class parallelism. Hudson did
not action this in Wave 5 (other Wave-5 priorities). Vasquez
implemented the fixture as part of the bring-up:

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/RegressionHostFixture.cs`
  exposes a shared `WebApplicationFactory<Program>` via
  `[CollectionDefinition("regression-host")]`.
- The regression test class adopts the fixture via
  `[Collection("regression-host")]` + constructor injection,
  removing the old `IAsyncLifetime` boot per-class.

The Wave-4 disposal race we observed under high parallelism (an
intermittent `ObjectDisposedException` on the shared sqlite
connection when two collections raced through teardown) is
eliminated by this change — the factory's lifetime is now scoped
to the collection rather than the class, so xUnit's parallel
scheduler can't tear it down while another fact still holds a
`HttpClient` against it.

The Wave-4 `xunit.runner.json` workaround (force-serial collection
boundary) was NOT needed once the fixture is in place. The repo
currently has no `xunit.runner.json` and the default parallelism
runs green at **1329 / 0 / 0** over multiple consecutive gate
invocations.

### Forward-looking: TESTING_SHIM

The Wave-5 bring-up also lands a `TESTING_SHIM`-gated
`TestHttpClientExtensions.WithDirectSession` helper. See
`docs/test-shims.md` for the inventory + the production-leakage
guarantee. The harness itself does NOT load the shim — it is a
test-only convenience for individual fact authors.

### Recommended next step for Hudson

If the regression class grows past ~80 facts, consider splitting
into `Wave1ThroughKW5RegressionTests` and a sibling
`Wave1ThroughKW5RegressionEnvelopeTests` — both sharing the same
`regression-host` collection. The shared fixture pattern composes
trivially across multiple classes.
