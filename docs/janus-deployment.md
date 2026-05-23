# Janus deployment — operator runbook

**Owner:** Bishop (Backend lead) — Phase K Wave 10.

Janus is the audio-only SFU we use for spectator voice fan-out (see
[`voice-sfu-design.md`](voice-sfu-design.md) for the design
rationale). This document is the production runbook: how
mountpoints are provisioned, how the API tracks their lifecycle,
and how operators interact with the sidecar.

## 1. Topology

The Janus instance runs as a sidecar to the API pod (one per
replica). Each table that opens a spectator-voice session is
mapped to a deterministic 6-digit mountpoint id computed by
`JanusSpectatorVoiceHub.ComputeMountpointId(tableId)`. The mapping
is deterministic so a concurrent registration on a second pod
targets the same Janus mountpoint — no central coordinator is
required.

## 2. Mountpoint lifecycle (Phase K Wave 10)

The API process owns a `JanusMountpointRegistry` (in-memory) +
`JanusMountpointLifecycleService` (background hosted service). The
registry is touch-based: every spectator join refreshes the
entry's `LastSeenAtUtc`; every leave decrements the
`ActiveSpectators` counter without evicting. The lifecycle service
sweeps the registry every 60 seconds (`DefaultSweepInterval`) and
evicts entries that satisfy **both** conditions:

* `ActiveSpectators == 0`, AND
* `(UtcNow - LastSeenAtUtc) >= DefaultIdleTtl` (5 minutes by
  default).

This produces a safe-by-default behaviour: a flapping spectator
who reconnects within 5 minutes finds the same mountpoint id; a
table that goes idle for longer than the TTL has its mountpoint
torn down (operators get one log line per eviction at INFO).

### Operator overrides

The lifecycle service accepts a custom `sweepInterval` + `idleTtl`
via its constructor — useful for tests and for staging
deployments that want faster eviction. Production binds the
defaults via `Program.cs`.

### Force-eviction

`JanusMountpointRegistry.Evict(tableId)` removes an entry
regardless of TTL. This is the right surface for an admin command
that needs to teardown a misbehaving table immediately.

## 3. Wiring

```csharp
builder.Services.AddSingleton<JanusMountpointRegistry>();
builder.Services.AddSingleton<JanusMountpointLifecycleService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<JanusMountpointLifecycleService>());
```

Both registrations are gated on `Voice:SpectatorSfuImpl=Janus` —
the in-memory stub does not need the registry.

## 4. Observability

* `JanusMountpointLifecycleService` logs one INFO line per
  eviction (`tableId`, `mountpointId`, `age` in seconds).
* Sweep errors log at WARNING and are non-fatal — the next tick
  retries.
* The registry's `Count` + `Entries` collection are available
  through DI for any future admin endpoint that wants to render
  the live table → mountpoint mapping.

## 5. Cross-references

* [`voice-sfu-design.md`](voice-sfu-design.md) — design rationale,
  sizing, Janus selection.
* [`realtime-resilience.md`](realtime-resilience.md) — SignalR
  backpressure surface adjacent to the spectator voice path.
* `src/backend/src/Mahjong.Autotable.Api/Voice/JanusMountpointLifecycleService.cs`
  — implementation.
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W10/Bishop/JanusMountpointLifecycleTests.cs`
  — contract suite.
