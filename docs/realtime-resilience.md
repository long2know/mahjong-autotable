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
