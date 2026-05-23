# Voice SFU — Phase K Wave 6 design note

**Owner:** Bishop (Backend lead)
**Status:** Surface stubbed (Wave 6); production wiring deferred to Phase L
**Hub:** `/hubs/voice/spectator` → `SpectatorVoiceHub.JoinSpectatorVoice(tableId)`

## Why an SFU at all?

The existing per-table voice surface (`VoiceHub` on `/hubs/voice` and
`/hubs/webrtc`) is a peer-mesh: every seated player opens a direct
WebRTC `RTCPeerConnection` to every other seated player. That topology
is honest for the 4-seat Mahjong table itself — O(n²) connections
where n ≤ 4 caps at 12 connections per table — but it falls apart the
moment spectators start joining:

| Topology   | Conns per peer | Total conns at n=4 | Total conns at n=50 | Total conns at n=500 |
|------------|----------------|--------------------|---------------------|----------------------|
| Peer-mesh  | n − 1          | 12                 | 1,225               | 124,750              |
| SFU        | 1              | 4                  | 50                  | 500                  |

A 50-spectator session melts a dealer's uplink bandwidth in the
peer-mesh model (the dealer would be sending 50 separate encodes of
their audio). The SFU terminates the dealer's outbound stream once,
then re-broadcasts a single decoded copy to every spectator over a
receive-only connection.

## Wave 6 surface

`SpectatorVoiceHub` exposes one hub method today:

```csharp
public Task<SpectatorVoiceJoinResult> JoinSpectatorVoice(string tableId);
// → { ok: true, sfuEndpoint: "sfu://stub/<tableId>", peerId: "<guid>" }
// → { ok: false, reason: "voice-not-enabled" | "unauthorized" | "target-not-found" }
```

The Wave-6 implementation returns a deterministic stub endpoint
(`sfu://stub/<tableId>`) so the frontend's spectator-voice wiring can
pin a non-empty contract today. The real endpoint is provisioned by
the SFU sidecar in Phase L (see §"Phase L wiring" below).

## Sizing requirements

These targets assume Opus@32kbps mono audio (Mahjong commentary is
voice-only, no music) and the SFU running as a sidecar on the same
node as the Changsha gameplay state.

### 50 spectators per table

- **Encode CPU:** dealer-side; ≤ 5 % of a modern mobile core.
- **SFU egress:** 50 × 32 kbps = 1.6 Mbps per table.
- **SFU CPU:** ≤ 2 % of a single x86-64 core (audio-only fan-out is
  largely a memcpy; PLC + jitter buffer are the only non-trivial
  costs).
- **Memory:** ≈ 4 MB resident per active table (audio buffers + per-
  peer state).
- **Sidecar shape:** single Janus / mediasoup process colocated with
  the API pod is sufficient; no horizontal scale needed.

### 100 spectators per table

- **Encode CPU:** unchanged (dealer still encodes once).
- **SFU egress:** 100 × 32 kbps = 3.2 Mbps per table.
- **SFU CPU:** ≤ 4 % of a single core.
- **Memory:** ≈ 6 MB per active table.
- **Sidecar shape:** single sidecar still sufficient; provision the
  pod with ≥ 2 dedicated cores to leave headroom for the rest of the
  API surface.

### 500 spectators per table

- **Encode CPU:** unchanged.
- **SFU egress:** 500 × 32 kbps = 16 Mbps per table (≈ 200 Mbps for
  10 concurrent tables of this size).
- **SFU CPU:** ≈ 18 % of a single core. The bottleneck moves from
  CPU to NIC at this scale.
- **Memory:** ≈ 16 MB per active table.
- **Sidecar shape:** horizontal scale via per-table SFU pinning.
  Run a sharded fleet behind a hash-on-tableId load balancer so each
  SFU instance owns ≤ 4 high-traffic tables and bandwidth stays under
  1 Gbps per pod. Provision the cluster with at least 2 instances
  for HA.

## Provider candidates

| Provider   | Lang | License | Mahjong fit |
|------------|------|---------|-------------|
| Janus      | C    | GPLv3   | Excellent — audio-only is a first-class transport, HA via load balancer. |
| mediasoup  | C++/Node | ISC     | Excellent — Node SDK aligns with the existing stack; SFU process is C++. |
| LiveKit    | Go   | Apache 2.0 | Good — adds room semantics on top of SFU. Heavier than we need for one-way voice. |
| Cloudflare Calls | (managed) | proprietary | Good — managed SFU; the egress cost-per-spectator-minute needs modelling first. |

**Recommendation for Phase L:** Janus, audio-only plugin. Audio-only
fan-out is the cheapest path operationally; the rest of the platform
already runs on a single API pod so colocating a Janus sidecar keeps
the deployment shape stable.

## Phase L wiring

The `ILivestreamRecorder` interface seam (Wave 6 ships
`InMemoryLivestreamRecorder`) is mirrored by a future
`ISpectatorSfuCoordinator` interface that the Phase L code introduces.
The hub's `JoinSpectatorVoice` will resolve the coordinator and
return a real SFU URI in place of `sfu://stub/<tableId>`.

Out-of-scope until Phase L:

- Bandwidth metering per spectator.
- DTLS / SRTP fingerprint negotiation between Janus and the spectator
  browser (Janus handles this today, but we need to confirm the
  Cloudflare TURN HMAC credentials propagate cleanly).
- Per-table mute / kick controls (the dealer needs admin commands
  beyond the Wave-6 peer-mesh).

## Audit

Spectator-join events fold into the existing
`ReconnectAuditEntry` table behind a new audit Kind in Phase L —
the surface lands once we have a real per-spectator handshake to
record. Wave 6 deliberately does not emit an audit row for the
stub join.
