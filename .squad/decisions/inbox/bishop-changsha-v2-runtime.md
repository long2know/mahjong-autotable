# Bishop — Changsha v1 Phase 2 Runtime Architecture

**Date:** 2026-05-06
**Author:** Bishop (Backend Dev)
**Branch:** `stlong/changsha-v1-phase2`

## Decision 1 — Runtime hosting model

**Choice:** Singleton `IChangshaGameRuntime` holding a `ConcurrentDictionary<string, ChangshaGameInstance>`
of in-memory game instances; each instance owns its own `SemaphoreSlim` for command
serialization. Persistence is via short-lived `IServiceScopeFactory` scopes that resolve
`AppDbContext` per snapshot.

**Rejected alternatives:**

- *Per-game scope* — would force the hub to resolve a fresh DbContext on every command, but it
  buys nothing because state lives in memory; the DB is just a snapshot store. A scoped runtime
  also can't outlive a hub call, breaking the claim-window timer fire-and-forget pattern.
- *Database-of-record runtime* — i.e., reload state from JSON on every command. Too slow and races
  on lifecycle transitions. We want sub-millisecond hub turnaround for bot-driven games.

## Decision 2 — Bot timing

| Trigger                        | Delay  | Rationale                                                  |
| ------------------------------ | ------ | ---------------------------------------------------------- |
| Bot turn (after `TurnStarted`) | 350 ms | Lets clients animate the prior tile-drawn before discard.  |
| Bot claim response             | 250 ms | Faster than human reflex but long enough for UI to render. |
| Claim window total timeout     | 5 s    | Per the hub contract; clients show a 5-second indicator.   |

Timings are constants in `ChangshaRuntimeOptions` so tests can override to 0ms for E2E speed.

## Decision 3 — Claim window resolution

A claim window opens when `Discard` produces opportunities. The runtime:

1. Emits `ClaimWindowOpen` to all clients.
2. Schedules bot responses (each bot eligible to claim issues `Claim` or `Pass` after `BotClaimDelay`).
3. Starts a 5-second cancellation token; on expiration, all unresponded eligible seats auto-pass.
4. Once every eligible seat has responded *or* the timer fires, the runtime selects the highest-priority
   claim (`hu > kong = pung > chow`, ties broken by counter-clockwise distance from the discarder)
   and applies it through `ChangshaGameStateMachine.ResolveClaim` / `PassClaim`.

## Decision 4 — `FullState` snapshot strategy

Reconnection sends a single `FullState` event whose payload mirrors the union of the most recent
public events (game phase, dealer, round, all seats with public meld/discard data) plus the
reconnecting seat's *private* concealed tiles. Subsequent events are sent normally.

We do **not** replay the event log on reconnect; the materialized state is sufficient for v1.
Hudson's replay tests cover state-machine determinism separately.

## Decision 5 — Wire-event shape

The hub emits exactly the events specified in `docs/rules/changsha-signalr-contract.md`. The
internal `ChangshaEvent` log (used by the state machine for replay/integrity) remains for
persistence; the runtime translates between the two on every transition.

## Decision 6 — No EF migration; reuse existing Changsha tables

`ChangshaGames` and `ChangshaGameEvents` were created in Phase 1. The runtime persists JSON
snapshots after every state transition and appends events to the event log table.
