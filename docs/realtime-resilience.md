# Realtime resilience — SignalR backpressure + reconnect

> Phase K Wave 9 — Bishop (Backend).

This note covers the W9 backpressure + reconnect-resilience surface
that ships across every SignalR hub introduced in Phase K
(`ChangshaHub`, `VoiceHub`, `SpectatorVoiceHub`, `TournamentMatchHub`,
`JanusReadinessHub`, and the forthcoming `CommentaryHub`).

## Why

W7/W8 added several long-lived SignalR connections per session.
Under steady-state traffic a slow consumer (mobile client on a
spotty network, a misbehaving browser tab in the background, or a
script-injected admin dashboard) caused the per-connection queue
to grow unboundedly. The host then buffered messages until the
ASP.NET Core HTTP/2 streams stalled, taking the rest of the table
down with it.

The W9 surface gives every hub a uniform shape:

* **Per-client rate cap** — 30 messages/second is the canonical
  SignalR ceiling. Every popular client library can sustain at
  least that; faster than 30 Hz collapses to noise on the UI
  anyway.
* **Age-based drop** — envelopes older than 5 seconds are dropped
  on the way out. A slow consumer that's fallen behind by more
  than 5 s gets the catch-up snapshot, not every dropped
  interstitial. (Sequence numbers tell the client what it
  missed.)
* **Last-acked sequence reconnect** — every envelope carries a
  monotonic server sequence. Clients persist the last sequence
  they processed and pass it back on reconnect; the server
  replays everything in the retained buffer newer than the ack.

## Wire envelope

```jsonc
{
  "seq": 12345,                    // monotonic per-server counter
  "createdAt": "2026-05-23T15:00:00Z",
  "payload": {                     // method-specific payload
    "tournamentId": "…",
    "format": "single-elim",
    "winnersRounds": 3,
    "losersRounds": 0
  }
}
```

The wrapper is the same shape for every hub method that publishes
through `SignalRBackpressureBroadcaster<THub>`. The
method-specific payload sits under the `payload` key so the
sequence + timestamp stay accessible regardless of the inner shape.

## Tuning knobs

| Knob | Default | Surfaced via |
|------|---------|--------------|
| `DefaultMaxMessagesPerSecond` | 30 | Constructor parameter `maxPerSecond` |
| `DefaultMaxMessageAgeSeconds` | 5 | Constructor parameter `maxAge` |
| `DefaultRetainedMessageCount` | 256 | Constructor parameter `retentionDepth` |

Operators can override these per-hub by registering a custom
`SignalRBackpressureBroadcaster<TournamentMatchHub>` etc. in
`Program.cs`; the default singletons use the canonical values
above.

## Drop semantics

A message is dropped (rate cap or age window) — what does the
client see?

* The sequence number gap is observable — `seq=12347` arrives
  after `seq=12345` (12346 was dropped). Client code can detect
  the gap and either:
  * **Ignore** (typical for chat-like surfaces — the missed
    message was probably a partial state). Recommended for
    `JanusReadinessChanged`, `TournamentBracketUpdated`.
  * **Refetch** (request the canonical REST snapshot to fill the
    gap). Recommended for game-state surfaces where every
    intermediate matters.
* The hub does NOT replay dropped messages outside the reconnect
  path. A consumer that's actively connected but falling behind
  gets the rate-cap behaviour permanently until it catches up.

## Reconnect protocol

1. Client opens the SignalR connection.
2. Client invokes the hub's `ResumeAfterAck(lastSeq)` method (every
   hub that uses the broadcaster MUST expose this method —
   convention is `Resume{HubName}(lastSeq)` for clarity).
3. The hub calls
   `broadcaster.ResumeFromAck(groupName, lastSeq)` which returns
   every retained envelope newer than the ack AND newer than
   `now - maxAge`. Envelopes older than `maxAge` are skipped —
   they'd arrive too stale to be useful.
4. The hub replays those envelopes via direct
   `Clients.Caller.SendAsync(...)` (NOT through the broadcaster —
   the replay path doesn't itself need rate-cap treatment).
5. The client's UI processes the replayed envelopes IN ORDER
   (sequence-ascending) — order matters for state replication.

## Hub adoption checklist

When wiring a new hub to use this broadcaster:

- [ ] Register the broadcaster as a singleton in `Program.cs`:
      `services.AddSingleton<SignalRBackpressureBroadcaster<MyHub>>();`
- [ ] Inject the broadcaster into every publisher (controller,
      service, runtime hook) that fires hub events.
- [ ] Add a `Resume{HubName}(lastSeq)` method to the hub that
      flushes the replay buffer to the caller.
- [ ] Document the per-method payload shape in the relevant
      contract spec (e.g. `docs/rules/changsha-signalr-contract.md`).

## Hard-asserted contract

The W9 surface ships with hard-asserted tests in
[`Phase_K_W9/Bishop/SignalRBackpressureTests`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/SignalRBackpressureTests.cs):

* Rate cap drops excess messages within a 1-second window.
* Age-based drop excludes envelopes older than `maxAge`.
* Monotonic sequence stamping is strictly increasing.
* Reconnect replay surfaces only the post-ack subset.
* Retention depth caps the buffer size — older messages evict
  cleanly.

## Cross-references

* [`src/backend/src/Mahjong.Autotable.Api/Observability/SignalRBackpressureBroadcaster.cs`](../src/backend/src/Mahjong.Autotable.Api/Observability/SignalRBackpressureBroadcaster.cs)
  — the broadcaster implementation.
* [`docs/api-precedence.md`](api-precedence.md) — HTTP surface
  status-code ordering (different layer, but the realtime + REST
  surfaces co-exist behind the same auth gates).
* [`docs/rules/changsha-signalr-contract.md`](rules/changsha-signalr-contract.md)
  — the original Changsha hub wire contract.

## Phase K Wave 10 — Prometheus metrics

The broadcaster now emits OpenTelemetry counters via the
`IMeterFactory` system. Meter name:

```
Mahjong.Autotable.Api.Observability.SignalRBackpressure
```

The constructor takes an **optional** `IMeterFactory`. When
supplied, the following counters are created. Every counter has
a `hub` tag whose value is `typeof(THub).Name`.

| Counter                                | Tags                              | Meaning                                                                                                   |
| -------------------------------------- | --------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `signalr_messages_sent_total`          | `hub`                             | Incremented once for every envelope successfully shipped via `Clients.Group(...).SendAsync(...)`.         |
| `signalr_messages_dropped_total`       | `hub`, `reason=rate_cap`          | Per-group rate-cap drop. Indicates a hot publisher exceeded `maxPerSecond`.                               |
| `signalr_messages_dropped_total`       | `hub`, `reason=send_failure`      | `SendAsync` threw — usually transient transport failure. Envelope is retained for reconnect replay.       |
| `signalr_messages_dropped_total`       | `hub`, `reason=age_window`        | During reconnect replay, envelopes newer than the ack but older than `maxAge` are silently filtered out — each one counts as a drop so the dashboard reflects the full backpressure picture. |
| `signalr_replay_requests_total`        | `hub`                             | `ResumeFromAck` invocations. Spikes here usually mirror reconnect storms.                                 |

### Alert recommendations

* **Sustained rate-cap drops > 1% of sent**: investigate whether
  a publisher is over-batching or whether the cap is too tight for
  the deployment.
* **`send_failure` > 0 across multiple windows**: SignalR transport
  is unhealthy; check the underlying hub host.
* **Replay rate >> baseline**: clients are reconnecting unusually
  often — investigate network or auth churn.

### Wiring example

```csharp
services.AddSingleton(sp => new SignalRBackpressureBroadcaster<MyHub>(
    sp.GetRequiredService<IHubContext<MyHub>>(),
    sp.GetRequiredService<ILogger<MyHub>>(),
    meterFactory: sp.GetService<IMeterFactory>()));
```

The factory parameter is optional — passing `null` preserves the
W9 behaviour exactly (no metrics, no exceptions).

### Tests

Hard-asserted in
[`Phase_K_W10/Bishop/SignalRBackpressureMetricsTests`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W10/Bishop/SignalRBackpressureMetricsTests.cs)
— meter name pin, sent counter, every drop reason, and replay
counter all asserted via `MeterListener`.

## Phase K Wave 11 — Mountpoint eviction metrics

Phase K Wave 11 (Bishop). The Janus mountpoint lifecycle
service emits an evictions counter so operators can
distinguish *normal* idle reclamation from *adversarial*
janus-unhealthy churn.

### Meter

| Property | Value |
|----------|-------|
| **Meter name** | `Mahjong.Autotable.Api.Voice.JanusMountpoint` |
| **Counter name** | `signalr_mountpoint_evictions_total` |
| **Type** | `Counter<long>` |
| **Tags** | `reason` |

### Reason vocabulary

The `reason` tag carries one of three canonical values
(constants on `Voice.JanusMountpointLifecycleService.MountpointEvictionReason`):

| Reason | Trigger | Operator significance |
|--------|---------|----------------------|
| `idle` | Mountpoint inactivity exceeded `Voice:MountpointIdleEvictionSeconds` (default 600s). | Normal — confirms the reclaim sweep is functioning. |
| `gameEnded` | Tournament service notified the lifecycle service via `EvictForGameEnded(tableId)`. | Normal — confirms cleanup is wired into the game loop. |
| `janusUnhealthy` | Janus health-probe flipped to red; service drained every mountpoint via `EvictAllForJanusUnhealthy()`. | **Investigate** — recurrent flips suggest a Janus container restart loop or upstream network issue. |

### Alert recommendations

* **`reason="janusUnhealthy"` rate > 0 across a 5-minute
  window**: the Janus host is flapping. Page the on-call.
* **`reason="idle"` rate = 0 across a 1-hour window with
  active mountpoints**: the sweep may be stuck — inspect
  `RunOnce` cancellation propagation.

### Wiring

The service consumes an optional `IMeterFactory` ctor
parameter — when present, it creates the meter and counter
lazily. When the factory is null (legacy harnesses), the
counter calls are no-ops; the lifecycle behaviour is
preserved exactly.

### Tests

Hard-asserted in
[`Phase_K_W11/Bishop/MountpointEvictionMetricsFacts`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Bishop/MountpointEvictionMetricsFacts.cs)
— counter name + meter name + every reason value all
asserted via `MeterListener`.

## Phase K Wave 11 — Latency observability

Phase K Wave 11 (Bishop). The `SignalRBackpressureBroadcaster`
publishes a histogram of the **server-side queue age** for
every envelope just before it is fanned out to SignalR
clients.

### Meter

| Property | Value |
|----------|-------|
| **Meter name** | `Mahjong.Autotable.Api.Observability.SignalRBackpressure` |
| **Histogram name** | `signalr_message_age_at_publish_seconds` |
| **Unit** | `s` (seconds) |
| **Type** | `Histogram<double>` |
| **Tags** | `hub` (short type name of the SignalR hub) |

### Bucket vocabulary

The recommended bucket scheme exposed on
`SignalRBackpressureBroadcaster.AgeAtPublishBuckets` is:

```text
[0.01, 0.05, 0.1, 0.5, 1, 5, 10]
```

The sub-10ms bucket captures the *common path* (envelope
created → sent in the same tick); the 0.5–1s buckets capture
*backpressure*; the 5–10s buckets capture *pathological*
queueing that warrants paging on-call.

### Measurement point

The observation is recorded inside `PublishAsync` **just
before** the `SendAsync` call:

```text
age_at_publish = UtcNow - envelope.CreatedAt
```

This measures *only* the server-side queueing tail — it
excludes network propagation and client-side processing.
Pair with the existing `signalr_messages_sent_total` counter
to derive a per-hub throughput-vs-latency view.

### Alert recommendations

* **P99 > 1s for sustained 5-minute window**: the SignalR
  hub is saturated; investigate backpressure or rate-limit
  configuration.
* **P99 > 5s**: pathological queueing — likely a publisher
  loop or stuck consumer. Page the on-call.

### Tests

Hard-asserted in
[`Phase_K_W11/Bishop/SignalRAgeAtPublishHistogramFacts`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Bishop/SignalRAgeAtPublishHistogramFacts.cs)
— histogram name + unit + bucket vocabulary + per-Publish
recording + hub tag all asserted via `MeterListener`.


## Phase K Wave 12 — Replay-from-ack persistence

### Why

The W9 `SignalRBackpressureBroadcaster<THub>` keeps the most-recent ~256 entries per group in memory so a reconnecting client can replay messages from its last-acknowledged sequence. Long-lived sessions (> 30 min at the canonical publish rate) can outlive the in-memory tail; the W12 store overlays the broadcaster with a durable per-(hub, connection) ledger so an arbitrarily-stale ack pointer can still be reconciled.

### Surface

* Interface: `Observability/EfSignalRSequenceStore.cs::ISignalRSequenceStore`.
* Implementations: `InMemorySignalRSequenceStore` (tests/dev) and `EfSignalRSequenceStore` (production).
* Options: `SignalR:SequenceStoreImpl` ("InMemory" default / "Ef" for prod), `SignalR:RetentionMinutes` (default 60), `SignalR:SweepIntervalMinutes` (default 5), `SignalR:MaxReplayPageSize` (default 1024).
* Payload encoding: `SignalRSequencePayloadSerializer.Serialize`/`Deserialize` — JSON via the canonical web-defaults options.
* Sweeper: `SignalRSequenceSweepService` (BackgroundService) runs at the configured cadence and drops rows where `ExpiresAt < utcNow`.

### Migration

The `SignalRSequenceEntries` table ships in the W12 migration `Phase_K_W12_Replays_Brackets_SignalRSeq` across all three providers (Sqlite, Postgres, SqlServer). Natural key: `(HubName, ConnectionId, Sequence)` unique.

### W12 vs W13

Wave 12 ships the **seam** — the store, the entity, the migration, the contract tests. The broadcaster does NOT yet write through to the durable store on every publish; that hook lands in Wave 13 once the toggle has soaked in staging. To opt in early, operators can register a wrapper around `SignalRBackpressureBroadcaster.PublishAsync` that calls `ISignalRSequenceStore.AppendAsync` in parallel; the in-memory + Ef stores are both safe under contention.

### Contract pins

Hard-asserted in `tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Bishop/SignalRSequenceStorePersistenceFacts.cs`:

* Both implementations satisfy `ISignalRSequenceStore`.
* Append → ReadFromAck round-trips (in-memory + EF).
* `ReadFromAckAsync` excludes entries with `Sequence ≤ lastAckedSequence`.
* `ReadFromAckAsync` honours the limit cap.
* `AppendAsync` pins `ExpiresAt = CreatedAt + RetentionMinutes`.
* `SweepExpiredAsync` drops expired rows (in-memory + EF).
* `SignalRSequencePayloadSerializer` round-trips a dictionary payload.


## §7 — Always-on retention sweep (Phase K Wave 13)

### §7.1 — Why lift the sweep

The W12 `SignalRSequenceSweepService` was registered ONLY when the
EF store was selected — the in-memory store accumulated rows for
the lifetime of the process. Long-lived single-replica development
sessions (or any test fixture that kept the API up for hours) would
slowly leak memory through expired sequence entries.

Wave 13 lifts the sweep to run for any `ISignalRSequenceStore`
implementation. Both the in-memory and EF stores expose the same
`SweepExpiredAsync` contract, so a single hosted service can
discharge the work without an implementation switch.

### §7.2 — Surface

* **Hosted service**: `Observability/SignalRSequenceRetentionSweep.cs`.
* **Cadence**: `SignalR:Sequences:SweepIntervalMinutes` (default 5,
  floor 1). Falls back to the legacy
  `SignalRSequenceStoreOptions.SweepIntervalMinutes` when the new key
  is absent so an operator upgrading from W12 keeps the existing
  cadence without an appsettings edit.
* **Predicate**: `ExpiresAt < utcNow`. Both stores stamp
  `ExpiresAt = CreatedAt + RetentionMinutes` at append time, so the
  predicate is equivalent to the spec's "`LastSeenAt < now - retention`".

### §7.3 — Logging

A single info log per tick records the count evicted. Zero-evict
ticks are silenced so the log stream stays readable under steady
state. Errors from `SweepExpiredAsync` are caught + logged as
warnings so a transient store failure (e.g. EF deadlock) does not
crash the hosted service.

### §7.4 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/SignalRSequenceRetentionSweepTests.cs`:

* `SignalRSequenceRetentionSweep.DefaultSweepIntervalMinutes == 5`.
* `MinSweepIntervalMinutes == 1`.
* The sweep runs against any `ISignalRSequenceStore` impl (both
  in-memory + EF).
* Each tick invokes `SweepExpiredAsync` with `DateTime.UtcNow`.
* A negative / zero configured interval is clamped to the floor.
* A swallowed `SweepExpiredAsync` exception does not stop the loop.
