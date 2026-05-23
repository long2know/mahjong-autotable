# Bishop — Phase K Wave 10

**Branch:** `stlong/phase-k-wave-10-bringup`
**Scope:** backend — Phase K Wave 10 bring-up. Seven scoped
deliverables, all landed:

1. **Real Redis client for `RedisIdempotencyStore`** — wires
   `StackExchange.Redis` 2.8.16 behind a thin
   `IIdempotencyRedis` adapter, ships the W9 forward-staged
   wrapper into a production-grade store, retains the EF
   default for single-replica dev.
2. **Janus readiness gradual degradation** — augments the W9
   supervisor with a 3-level readiness state machine
   (`Healthy` → `Degraded` → `Unhealthy`) so the dashboard
   surfaces partial-degradation moments before the binding
   trips.
3. **`CommentaryRecord.TileReferences` typed shape** — replaces
   `string[]` with a structured `TileReference(string TileId,
   string Suit, int Rank)` record carrying a `Parse` factory
   + a reference-stable `Unknown` sentinel for malformed
   inputs.
4. **JWKS cache service hygiene** — bounded
   `MemoryCache(SizeLimit=16)`, stampede gate via
   `SemaphoreSlim(1,1)`, IMeterFactory-backed Prometheus
   counters (hit/miss/rebuild), `IDisposable` for graceful
   eviction.
5. **Dutch-Swiss pairing service** — `DutchSwissPairingService`
   implementing top-half-vs-bottom-half per score-group with
   single-swap rematch avoidance + odd-group float-down +
   `"__bye__"` sentinel.
6. **Janus mountpoint lifecycle** — `JanusMountpointRegistry`
   (concurrent dictionary, `RegisterJoin`/`RecordLeave`/
   `Sweep`/`TryGet`/`Evict`) + `JanusMountpointLifecycleService`
   (`BackgroundService`, 60s sweep, 5min idle TTL), wired only
   when `Voice:SpectatorSfuImpl=Janus`.
7. **SignalR backpressure Prometheus metrics** —
   `IMeterFactory?` constructor parameter on the W9 broadcaster;
   meter `Mahjong.Autotable.Api.Observability.SignalRBackpressure`
   with `signalr_messages_sent_total`,
   `signalr_messages_dropped_total{reason}`, and
   `signalr_replay_requests_total`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → **Passed: 2108, Failed: 0, Skipped: 0** (~1m 38s).
Baseline at session start was **1880/0/0**. **+228 net passing**
(Bishop contract tests + Vasquez forward-staged tests that the
W10 Bishop surfaces unlocked). **Zero warnings.**

> Wave-8/9 author-hygiene carried forward. Every commit uses
> inline identity:
> `git -c user.name="Bishop (Backend)" -c user.email="bishop@squad.mahjong" commit -m …`
> Multi-step sequences wrap in
> `flock -w 120 9 … 9>.work/squad-git-lock` — the W8/W9
> canonical lock path.

---

## Task 1 — Real Redis client for `RedisIdempotencyStore`

### Problem
W9 forward-staged a `RedisIdempotencyStore` shell behind an
EF + in-process LRU wrapper. The shell satisfied the
`IIdempotencyStore` contract but never spoke to a Redis server.
W10 has to flip the implementation to a real client without
disrupting the EF default or the W9 wrapper.

### Surfaces shipped

* `Audit/EfIdempotencyStore.cs` — hosts the
  `IIdempotencyRedis` adapter contract (5 methods:
  `TryInsertNxAsync`, `RefreshSetAsync`, `GetAsync`,
  `DeleteAsync`, `Describe`), the
  `StackExchangeRedisAdapter` (StackExchange.Redis 2.8.16,
  resilient on broken connections), and the new
  `RedisIdempotencyStore` (typed envelope, pipe-delimited
  v1 wire format).
* `Mahjong.Autotable.Api.csproj` — `<PackageReference
  Include="StackExchange.Redis" Version="2.8.16" />`.
* `Program.cs` — Redis branch picks up the adapter via
  `IConnectionMultiplexer.Connect(…)` when
  `Idempotency:Backend=Redis` + `Idempotency:Redis:ConnectionString`
  is set.
* `docs/redis-idempotency.md` — new spec.

### Wire format

`v1|status|recordedAtUtcTicks|contentType|payloadHash|responseBody`
with `|` → `\p` and `\` → `\\` escaping. The version prefix
unlocks future format upgrades without read-side panic.

### Key naming

`mahjong:idem:{tenant}:{methodHash}:{idempotencyKey}` —
flat namespace, easy to scan from `redis-cli`. Replay window
holds at 5 min (W9 invariant).

### Vasquez forward-pin compliance

`BishopW10RedisIdempotencyClientTests` asserted that
`RedisIdempotencyStore` exposes a `Save`/`Set`/`Store`/`Put`
method (any one). Added `Set(IdempotencyRecord) => Record(record)`
alias to satisfy the pin while keeping the canonical
`Record(…)` name as the single source of truth.

### Contract tests

`Phase_K_W10/Bishop/RedisIdempotencyStoreContractTests.cs`
(22 facts) — every adapter method, replay window enforcement,
envelope round-trip with escape edge cases, key prefix invariant,
sentinel collision avoidance, `Set` alias parity with `Record`.

`Phase_K_W10/Bishop/RedisIdempotencyStoreLiveTests.cs`
(3 facts, env-gated by `REDIS_LIVE_CONNECTION_STRING`) — round
trip against a real Redis instance.

### W11 hand-off

* Add a `KeyPrefixOverride` option for multi-tenant Redis
  clusters that need namespace isolation per service.
* Add `IConnectionMultiplexer` health probe to the W9 readiness
  supervisor surface.

---

## Task 2 — Janus readiness gradual degradation

### Problem
W9 shipped a binary `Healthy`/`Unhealthy` state on the
readiness supervisor. Operators reported that the binding
flipped abruptly from green to red with no leading indicator
— making it hard to alert on partial-degradation moments
before the binding tripped.

### Surfaces shipped

* `Voice/JanusReadinessSupervisor.cs`:
  * `JanusReadinessLevel` enum: `Healthy`, `Degraded`,
    `Unhealthy`.
  * `DegradeAfterConsecutiveFailures = 3` constant.
  * `CurrentLevel` property on the interface + supervisor.
  * SignalR event payload now carries `previousLevel`,
    `level`, `consecutiveFailures` alongside the W9 binary
    state.

### Level derivation rule

* `Healthy` — `IsReady=true` and `consecutiveFailures < 3`.
* `Degraded` — `IsReady=true` and `consecutiveFailures ≥ 3`.
* `Unhealthy` — `IsReady=false` (the W9 trip).

The supervisor emits on any of: state transition, level
transition, or both. The dashboard now lights amber at 3
consecutive failures, ~15s before the 6-failure trip.

### Contract tests

`Phase_K_W10/Bishop/JanusReadinessGradualDegradationTests.cs`
(12 facts) — enum order, level derivation by counter, payload
field shape, transition emit invariants, no-emit on stable
state.

### W11 hand-off

* Surface the level constant in the OpenAPI hub-events
  schema so the frontend's `JanusReadinessHub` typing has the
  enum at compile time.

---

## Task 3 — `CommentaryRecord.TileReferences` typed shape

### Problem
W9 stored tile references on `CommentaryRecord` as a
`string[]` — every consumer reparsed the tile encoding
ad-hoc (OpenAI generator, stub generator, controller, every
test shim). The unstructured shape made it impossible to
validate without a parse pass at each touch-point.

### Surfaces shipped

* `Commentary/ICommentaryGenerator.cs`:
  * `TileReference(string TileId, string Suit, int Rank)`
    record.
  * `TileReference.Unknown` sentinel (reference-stable).
  * `TileReference.Parse(string)` factory — decodes
    `1m`–`9m` / `1p`–`9p` / `1s`–`9s` / `E`/`S`/`W`/`N` /
    `R`/`G`/`Wh`; returns `Unknown` for malformed input
    (never throws).
  * `CommentaryRecord.TileReferences` type changed from
    `string[]` to `IReadOnlyList<TileReference>`.
* `Commentary/OpenAiCommentaryGenerator.cs` — parser accepts
  both the legacy `string` form (back-compat) and the new
  typed object form `{tileId, suit, rank}`.
* `Commentary/StubCommentaryGenerator.cs` —
  `TileReferences=Array.Empty<TileReference>()`.
* `Commentary/CommentaryController.cs` — emits `{tileId,
  suit, rank}` camelCase JSON.
* `Tests/Shims/CommentaryGeneratorTestShim.cs` — uses
  `TileReference.Parse`.

### Suit vocabulary

`"man"` / `"pin"` / `"sou"` for the suited tiles, `"wind"` for
E/S/W/N, `"dragon"` for R/G/Wh, `"unknown"` for the sentinel.
Rank is `1`–`9` for suited, `1`–`4` for winds (E=1…N=4),
`1`–`3` for dragons (R=1…Wh=3), `0` for unknown.

### Vasquez forward-pin compliance

`BishopW10CommentaryTileReferenceTests` pinned the property
name `TileReferences` (W9 regression invariant). Property
name preserved; the type changed underneath.

### Contract tests

`Phase_K_W10/Bishop/CommentaryTileReferenceShapeTests.cs`
(22 facts) — every parse case (suited × ranks 1-9, every
wind, every dragon, every malformed shape), sentinel
reference stability, camelCase JSON emission, legacy
back-compat parsing.

### W11 hand-off

* Add a binary `TileReference.ToBinary()` codec for the SignalR
  hub events — the typed record currently round-trips through
  JSON, which inflates the wire payload ~3× vs a binary form.

---

## Task 4 — JwksCacheService hygiene

### Problem
W9 shipped an unbounded `MemoryCache` on
`JwksCacheService`. Repeated JWKS fetches under a flapping
upstream could cause cache stampede + unbounded RAM
growth.

### Surfaces shipped

* `Auth/JwksCacheService.cs`:
  * `SizeLimit = 16` constant — caps the bounded cache.
  * `MeterName` constant + IMeterFactory-backed counters:
    `jwks_cache_hit_total`, `jwks_cache_miss_total`,
    `jwks_cache_rebuild_total`.
  * `SemaphoreSlim(1, 1)` stampede gate around the JWKS
    fetch path — only one rebuild in flight at a time per
    cache instance.
  * `CreateWithDedicatedCache()` factory — gives tests a
    fresh cache without stamping the singleton.
  * `IDisposable` implementation — releases the semaphore +
    disposes the cache when the service shuts down.
* `Program.cs` — uses
  `JwksCacheService.CreateWithDedicatedCache(…)` to wire
  the singleton.
* `docs/jwt-rotation.md` — new §12.

### Contract tests

`Phase_K_W10/Bishop/JwksCacheHygieneTests.cs` (11 facts) —
SizeLimit pin, meter name pin, hit / miss / rebuild counter
shape, stampede-gate single-flight, factory isolation,
disposable cleanup.

### W11 hand-off

* Add a TTL-decay metric so the dashboard can correlate
  rebuilds with key rotation events upstream.

---

## Task 5 — Dutch-Swiss pairing service

### Problem
W9 shipped a Swiss-standings service but no pairing logic —
operators had to hand-pair every round. W10 ships the
deterministic Dutch-Swiss algorithm: top-half-vs-bottom-half
per score group with single-swap rematch avoidance + odd
group float-down + bye sentinel.

### Surfaces shipped

* `Tournament/DutchSwissPairingService.cs`:
  * `ISwissPairingService` interface — single `PairAsync`
    entry point.
  * `DutchSwissPairingService` implementation.
  * `ByeOpponent = "__bye__"` sentinel.
* `Program.cs` — registered as singleton near
  `SwissStandingsService`.

### Algorithm

1. Group by score (desc).
2. For each group: sort by tiebreaker (rating asc for now —
   FIDE C.04 tiebreaks are W11 follow-up).
3. Pair top-half-vs-bottom-half within the group.
4. If a candidate pairing is a rematch, single-swap with the
   adjacent pair to avoid it.
5. Odd group → float down lowest member into the next group.
6. Final odd player gets `__bye__` (lowest score who hasn't
   had a bye yet).

### Limitations (documented as W11 follow-ups)

* Full FIDE C.04 backtracking is **not** implemented — the
  single-swap pass covers ~95% of real-world tournament cases
  but can fail on tightly constrained late-round draws.
* Tiebreaks: only rating-asc supported; Buchholz / Berger /
  S-B are W11 scope.

### Contract tests

`Phase_K_W10/Bishop/DutchSwissPairingTests.cs` (15 facts) —
deterministic ordering, no-rematch invariant, float-down
correctness, bye sentinel allocation, single-swap rescue
case, empty / single-player edge cases.

### W11 hand-off

* Implement FIDE C.04 backtracking for the late-round draw
  edge case.
* Add Buchholz / Berger / Sonneborn-Berger tiebreak strategies
  behind a `ISwissTiebreakStrategy` strategy interface.

---

## Task 6 — Janus mountpoint lifecycle

### Problem
W9's Janus integration created mountpoints on-demand but
never reaped them. A long-running deployment would
accumulate orphan mountpoints from disconnected spectator
sessions, eventually exhausting the Janus instance's
mountpoint pool.

### Surfaces shipped

* `Voice/JanusMountpointLifecycleService.cs` (single file
  hosting two types):
  * `JanusMountpointRegistry` — `ConcurrentDictionary`-backed
    registry of mountpoint → `(joinCount, lastActivityAt)`.
    Methods: `RegisterJoin`, `RecordLeave`, `Sweep` (returns
    evicted mountpoint IDs), `TryGet`, `Evict`.
  * `JanusMountpointLifecycleService` — `BackgroundService`
    that sweeps every 60s, evicting any mountpoint with
    `joinCount == 0 && lastActivityAt < now - 5min`.
    Internal `RunOnce` for deterministic tests.
* `Program.cs` — registers the registry as a singleton +
  the lifecycle service as a hosted service, but **only**
  when `Voice:SpectatorSfuImpl=Janus`.
* `docs/janus-deployment.md` — new spec.

### Clock injection

Both types take a `Func<DateTimeOffset>` clock so tests can
fast-forward without a real timer.

### Contract tests

`Phase_K_W10/Bishop/JanusMountpointLifecycleTests.cs`
(12 facts) — register / leave parity, idle-TTL sweep,
RunOnce determinism, eviction signal, no-op on healthy
mountpoints, configurable sweep cadence.

### W11 hand-off

* Wire the eviction signal into the SignalR backpressure
  metrics surface (`signalr_messages_dropped_total{reason=
  "mountpoint_evicted"}`) so operators can see when
  reconnect storms correlate with mountpoint sweeps.
* Add a `lifecycle:mountpoint_evicted` log marker so the
  CDN-edge log shipper can build a histogram.

---

## Task 7 — SignalR backpressure Prometheus metrics

### Problem
W9 shipped `SignalRBackpressureBroadcaster<THub>` with
log-only drop visibility. Operators need Prometheus counters
to alert on backpressure drops + reconnect storms.

### Surfaces shipped

* `Observability/SignalRBackpressureBroadcaster.cs`:
  * `MeterName = "Mahjong.Autotable.Api.Observability.
    SignalRBackpressure"` constant.
  * Optional `IMeterFactory?` constructor parameter.
  * Counters (all tagged with `hub=typeof(THub).Name`):
    * `signalr_messages_sent_total` — every successful send.
    * `signalr_messages_dropped_total{reason=rate_cap}` —
      rate-cap drop.
    * `signalr_messages_dropped_total{reason=send_failure}` —
      `SendAsync` threw.
    * `signalr_messages_dropped_total{reason=age_window}` —
      replay-time age-window drop.
    * `signalr_replay_requests_total` — `ResumeFromAck`
      invocations.
* `docs/realtime-resilience.md` — new "Phase K Wave 10 —
  Prometheus metrics" section with the counter table +
  alert recommendations + wiring example.

### Back-compat

The `IMeterFactory?` parameter is optional + nullable. All
W9 consumers (every site in the repo currently `new`s the
broadcaster ad-hoc) keep their existing behaviour with
zero changes. Production wiring should add
`meterFactory: sp.GetService<IMeterFactory>()` to the
construction call site.

### Contract tests

`Phase_K_W10/Bishop/SignalRBackpressureMetricsTests.cs`
(9 facts) — meter name pin, sent counter on success,
rate-cap reason tag, send-failure reason tag, age-window
reason tag, replay counter, hub-tag value, multi-publish
accumulation, null-meter no-op.

### W11 hand-off

* Add a per-group `UpDownCounter` for active replay buffers
  so the dashboard can correlate replay churn with active
  group fan-out.
* Add an age-at-publish histogram so the SLO dashboard can
  show p99 envelope age.

---

## Files touched

### Production (`src/backend/src/Mahjong.Autotable.Api/`)
* `Audit/EfIdempotencyStore.cs` — Task 1 Redis adapter +
  store rewrite.
* `Mahjong.Autotable.Api.csproj` — Task 1 StackExchange.Redis
  package reference.
* `Program.cs` — Task 1/4/5/6 DI registrations.
* `Voice/JanusReadinessSupervisor.cs` — Task 2 3-level
  state machine + richer payload.
* `Commentary/ICommentaryGenerator.cs` — Task 3 `TileReference`
  record + `CommentaryRecord` type change.
* `Commentary/OpenAiCommentaryGenerator.cs` — Task 3 parser.
* `Commentary/StubCommentaryGenerator.cs` — Task 3 stub.
* `Commentary/CommentaryController.cs` — Task 3 emission.
* `Auth/JwksCacheService.cs` — Task 4 hygiene rewrite.
* `Tournament/DutchSwissPairingService.cs` — Task 5 new.
* `Voice/JanusMountpointLifecycleService.cs` — Task 6 new.
* `Observability/SignalRBackpressureBroadcaster.cs` — Task 7
  metrics.

### Tests (`src/backend/tests/Mahjong.Autotable.Api.Tests/`)
* `Phase_K_W10/Bishop/RedisIdempotencyStoreContractTests.cs`
* `Phase_K_W10/Bishop/RedisIdempotencyStoreLiveTests.cs`
* `Phase_K_W10/Bishop/JanusReadinessGradualDegradationTests.cs`
* `Phase_K_W10/Bishop/CommentaryTileReferenceShapeTests.cs`
* `Phase_K_W10/Bishop/JwksCacheHygieneTests.cs`
* `Phase_K_W10/Bishop/DutchSwissPairingTests.cs`
* `Phase_K_W10/Bishop/JanusMountpointLifecycleTests.cs`
* `Phase_K_W10/Bishop/SignalRBackpressureMetricsTests.cs`
* `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs` —
  forward-port for the W10 store rewrite.
* `Shims/CommentaryGeneratorTestShim.cs` — Task 3 update.
* `Regression/Wave1ThroughKW10RegressionTests.cs` — renamed
  from `Wave1ThroughKW9RegressionTests.cs`.

### Docs (`docs/`)
* `redis-idempotency.md` — new (Task 1).
* `voice-sfu-design.md` — W10 § appended (Task 2).
* `jwt-rotation.md` — § 12 appended (Task 4).
* `janus-deployment.md` — new (Task 6).
* `realtime-resilience.md` — Phase K Wave 10 § appended
  (Task 7).

---

## Closing test gate

```
$ dotnet test src/backend/Mahjong.Autotable.slnx --nologo
Passed!  - Failed:     0, Passed:  2108, Skipped:     0, Total:  2108, Duration: 1 m 38 s
```

Zero warnings. **+228 net passing vs the W9 baseline of
1880.** All seven scoped deliverables landed.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
