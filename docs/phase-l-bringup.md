# Phase L bring-up surface

Phase K Wave 14 — Bishop. Pre-work doc landed during W14 so the
team has a stable target for Phase L sizing + sequencing. Phase K
ships in W15; Phase L bring-up begins immediately after the
release tag.

## §1 — Mission

Phase K rebuilt the realtime + persistence backbone (durable
SignalR sequence store, JWT staged rotation, replay-by-id,
spectator handoff audit, commentary LLM cost ledger,
bracket store, Redis OAuth introspection rate limiter,
always-on retention sweeps, EKS bring-up, helm canary,
visual-regression fleet, branch-protection). Phase L lifts the
product from "tournament-ready backend" to "tournament-grade
end-to-end experience" with four pillars:

1. **Tournament-grade play surface** — rated play, ELO
   leaderboards, formal Swiss bracket UI, season rollover
   automation, anti-cheat audits.
2. **Real-time spectator improvements** — WebRTC voice + chat,
   spectator picture-in-picture, multi-camera mountpoint
   switching, mountpoint authority handoff.
3. **Mobile native apps** — iOS / Android wraps over the
   existing renderer, push notifications via APNs / FCM, offline
   replay download.
4. **AI commentary tuning** — model A/B testing harness, latency
   tracking, per-game cost attribution, prompt-template registry.

## §2 — Scope per pillar

### §2.1 — Tournament-grade play

**Surface areas:**

* `src/backend/src/Mahjong.Autotable.Api/Tournament/` — extend
  `PlayerRatingService` to publish ELO deltas per match, persist
  rating history for chart rendering.
* `src/backend/src/Mahjong.Autotable.Api/Leaderboard/` — new
  durable leaderboard table keyed on
  `(season, scope = global|tournament-id, playerId)` with
  precomputed `(rating, gamesPlayed, lastChangedAt)` columns.
  EF migration `Phase_L_W1_Leaderboard`.
* `src/frontend/autotable-src/src/tournament/` — Swiss bracket
  pairing UI matching the FIDE C04 round-robin shape. New
  `<BracketSwiss />` component reading from
  `GET /api/tournaments/{id}/brackets` (W14 endpoint).
* `src/backend/.../SeasonRolloverService` — extend the existing
  W10 scaffold so the rollover stamps a snapshot row in the new
  leaderboard table at month boundaries; surfaces a
  `Tournament:SeasonRolloverCron` knob (default `0 0 1 * *`).
* `src/backend/.../Audit/AntiCheatPipeline.cs` — pattern-match
  improbable claim cadences; emit `audit.suspicious.claim_burst`
  rows. Initial heuristic only; Phase M wires ML.

**Success criteria:**

* Rated game completion stamps an ELO delta row within 200 ms p99.
* Leaderboard query (`GET /api/leaderboard?season=current`) p99 ≤ 80 ms over a 100k-row table.
* Swiss bracket UI renders the W14 query endpoint with zero
  Hicks-side workarounds.

### §2.2 — Real-time spectator improvements

**Surface areas:**

* `src/backend/.../Voice/` — extend `VoiceHub` with chat-channel
  shape, durable chat persistence (`Phase_L_W2_ChatHistory`
  migration). Per-table chat rate limit shares the existing
  `Voice:RateLimiter` token bucket.
* `src/backend/.../Spectator/SpectatorLivestream*` — multi-
  mountpoint authority handoff: when an authoritative mountpoint
  goes silent, the next spectator with the highest weight is
  promoted. The W12 spectator handoff JWT scope claim already
  carries the necessary game-pin.
* `src/frontend/autotable-src/src/spectator/PiP.vue` — new
  picture-in-picture overlay component, Hicks-owned. Reads
  the W14 `signalr_seq_replay_from_ack_total` metric to surface
  reconnect health to the operator HUD.

**Success criteria:**

* Voice + chat round-trip latency p99 ≤ 300 ms over the same
  link as the existing WebRTC media path.
* Mountpoint authority handoff completes within 5 s of the
  authoritative source falling silent.
* Spectator chat persists across reconnects (durable, replay-on-
  resume).

### §2.3 — Mobile native apps

**Surface areas:**

* `mobile/ios/` — Swift wrap over the renderer, native push
  registration, native auth keychain integration. Reuses the
  W12 spectator handoff JWT for the native session flip.
* `mobile/android/` — Kotlin wrap, equivalent surface, FCM
  push.
* `src/backend/.../Push/PushNotificationService.cs` — new
  service that publishes game-completed / your-turn / spectator-
  invited push envelopes via APNs + FCM through a single
  abstraction.
* `src/backend/.../Replays/` — extend the W14 listing endpoint
  with a `?format=offline-bundle` query that returns a zip-
  bundled replay payload + asset list for native offline replay.

**Success criteria:**

* iOS + Android apps clear App Store / Play Store review on
  first submission (compliance prework verified during Phase K
  W15).
* Push notification fan-out p99 ≤ 2 s from runtime emit to
  device.
* Offline replay download replays the full 16-hand game with no
  network access after the bundle lands.

### §2.4 — AI commentary tuning

**Surface areas:**

* `src/backend/.../Commentary/CommentaryAbTestHarness.cs` —
  routes a configurable fraction of LLM calls through the
  alternate model so the team can A/B-test prompt + model
  variants. Toggle: `Commentary:AbTest:Enabled` (default false).
* `src/backend/.../Commentary/CommentaryLatencyHistogram.cs` —
  Prometheus histogram `commentary_llm_latency_seconds`
  (buckets: 0.25, 0.5, 1, 2, 4, 8, 15). Sampled by the
  `OpenAiCommentaryGenerator` request path.
* `src/backend/.../Commentary/PromptTemplateRegistry.cs` —
  durable registry keyed on `(template_name, version)`. The
  active template per game-phase is selected by the
  controller; admins flip versions via
  `POST /api/commentary/templates/{name}/{version}/activate`.
* Extend the W14 `GET /api/commentary/cost/summary` endpoint with
  a `byModel` breakdown that actually distinguishes between the
  configured + A/B variant models (W14 ships the shape with a
  single entry).

**Success criteria:**

* A/B harness routes ≤ 0.5% of production traffic without
  observable user-facing latency regression.
* Latency histogram surfaces p50 / p99 broken down by model in
  the Grafana commentary board.
* Prompt template flips land without a pod restart.

## §3 — Expected wave count + sequencing

Phase L is sized for **8 waves** (L1 – L8) plus a wrap wave (L9).
Rough sequencing:

| Wave | Pillar       | Theme                                         |
| ---- | ------------ | --------------------------------------------- |
| L1   | Tournament   | Leaderboard schema + ELO publishing           |
| L2   | Spectator    | Voice + chat surface bring-up                 |
| L3   | Mobile       | iOS shell + APNs wiring                       |
| L4   | AI           | A/B harness + latency histogram               |
| L5   | Tournament   | Swiss bracket UI + season rollover automation |
| L6   | Spectator    | Mountpoint authority handoff + PiP overlay    |
| L7   | Mobile       | Android shell + FCM + offline replay bundle   |
| L8   | AI           | Prompt template registry + per-model cost     |
| L9   | Wrap         | Documentation, release tag, retros            |

Cross-cutting work (observability, deployment, security) ride
along with each wave under the existing Apone / Vasquez / Hicks
lanes; Bishop owns the backend deliverables across all 8 + the
L9 wrap.

## §4 — Open questions for sign-off

* **ELO algorithm choice** — Glicko-2 vs. classical ELO with
  uncertainty? Vasquez to weigh in during Phase K W15
  retro.
* **Chat moderation policy** — server-side keyword filter, ML
  classifier (Sightengine), or both? Hicks + Apone to scope
  the UX + the cost.
* **Mobile distribution channels** — TestFlight only for L3, or
  open beta? Stephen to call.
* **Commentary template versioning** — semver vs. monotonic int?
  Bishop leans monotonic int so the activate endpoint is a
  single PK lookup, but the prompt-engineering team has asked
  for semver.

## §5 — Cross-references

* `docs/bracket-shape.md §5` — the W14 bracket query endpoint
  L5 builds on.
* `docs/realtime-resilience.md §8` — the W14 SignalR metrics
  L2 + L6 dashboards consume.
* `docs/commentary-llm.md §6` — the W14 cost summary
  endpoint L8 widens.
* `docs/replay-by-id.md §3` — the W14 replay listing endpoint
  L7 extends with `?format=offline-bundle`.
* `docs/spectator-handoff.md §4` — the W14 audit query endpoint
  L6 consumes for the multi-mountpoint authority HUD.
* `docs/jwt-rotation.md §14` — the W14 overlap-window
  enforcement L1 + L3 inherit verbatim (mobile auth flips reuse
  the active key only).
