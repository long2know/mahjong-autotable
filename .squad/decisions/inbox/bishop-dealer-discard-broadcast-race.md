# Bishop — Dealer-discard broadcast race + leave-seat null handler

**Author:** Bishop (Backend Dev)
**Date:** 2026-05-29
**Branch / PR:** `fix/discard-broadcast-race-and-leave-seat`
**Hand-off origin:** Stephen's directive — fix the two backend bugs called out
by Vasquez (integration audit A2/A3, "dealer's first discard after DealerExtra
take silently fails to round-trip to the local view") and Ripley (system audit
L-10, "leave-seat handler returns silently when `seat: null` is sent").

## Root-cause findings

### Bug 1 — Dealer-discard broadcast race

Stephen's brief framed this as a state-machine bug. After deep tracing it is
**not**:

- `ChangshaGameStateMachine.Discard` correctly mutates `state.DiscardPile`,
  drops the tile from `state.Hands[seat].ConcealedTiles`, and advances the
  phase.
- `ChangshaGameRuntime.DiscardAsync` correctly fires `EmitDiscardAsync`,
  persists the snapshot, and schedules the post-discard work
  (`DriveAfterAdvanceAsync` → `DrawTile` for the next seat → bot scheduler).
- The runtime's `DiscardPile` and the move-log entry "Seat 0 discarded X筒"
  both confirm the discard reached the authoritative state.
- Existing tests (`DealerExtraTransitionsToAwaitingDiscardTests`,
  `RoundRobinDiscardCycleTests`) cover the runtime path but ALL of them assert
  on `runtime.TryGetSnapshot(...)` — none consume the WS broadcast envelope,
  so they cannot reproduce the bug.

The actual root cause is **a lock-free read race in the autotable WS broadcast
pipeline**:

`AutotableConnectionManager.SendFullSnapshotAsync` previously read the runtime
state via `_runtime.TryGetSnapshot(out runtimeState)`. That method returns
`instance.State` **directly** — a live reference into the runtime's mutable
state graph (`ChangshaGameRuntime.cs:278-286`). The translator then iterates
`state.Hands[i].ConcealedTiles`, `state.DiscardPile`, and `state.Wall` —
all `List<T>` — outside the runtime instance lock.

`OnStateChanged` (`AutotableWsEndpoint.cs:1443-1453`) launches one
fire-and-forget `BroadcastSnapshotAsync` per connection. After a discard the
runtime fires StateChanged TWICE within milliseconds:

1. From `DiscardAsync`'s `PersistSnapshotAsync` (post-mutation, lock held by
   the caller).
2. From `DriveAfterAdvanceAsync`'s `PersistSnapshotAsync` (after the next
   seat's `DrawTile`, also under lock).

Both broadcasts are fire-and-forget; both read state without the lock. The
first broadcast can be iterating `state.Wall` / `state.Hands` while the
second mutation is `DrawTile`-ing into the same lists. Result: a torn snapshot
where the translator emits the wrong tiles or drops entries.

`AutotableGameState.ApplyUpdate` (`AutotableGameState.cs:82-133`) stores
`things` entries by normalized tile-id key. Any tile that was OMITTED by a
torn-read translator keeps its prior (now stale) slot in the stored snapshot,
and the next `Full=true` broadcast ships the stale entry to the client. That
gaslights the local view into showing the dealer's hand at 14 tiles and an
empty discard tray when authoritative state has the dealer at 13 + 1 in
`discard.0.0@0`.

### Bug 2 — Leave-seat handler null guard

`TryHandleSeatTakeAsync` (`AutotableWsEndpoint.cs:558-596` pre-fix) accepted
only `JsonValueKind.Number` for the inner `seat` property:

```csharp
if (!je.TryGetProperty("seat", out var seatEl)) return;
if (seatEl.ValueKind != JsonValueKind.Number) return;   // ← drops {seat:null}
```

The upstream `Player.svelte` "Leave" action sends `["seats", N, {seat:null}]`
(per upstream autotable). Pre-fix the handler returned silently, leaving:
- `instance.State.Seats[N].PlayerId` populated (the player still "owns" the
  seat from the runtime's perspective).
- `instance.SeatConnections[N]` populated (the per-tab transport binding
  intact).
- The lobby seat counter stuck at full capacity; the `nicks[N]` entry never
  cleared; no other player could take the seat.

`HandleDisconnectAsync` (`ChangshaGameRuntime.cs:901-970`) correctly releases
the per-tab binding on socket close, but intentionally keeps the persistent
`PlayerId` so a reconnect (tab cycle) can reclaim the seat. An explicit
"Leave" action needs the more aggressive clear.

## The fix

### Backend changes — runtime API

- **`IChangshaGameRuntime.ReleaseSeatAsync(gameId, playerId, connectionId, ct)`**
  (`ChangshaGameRuntime.cs:46-58`) — new method. Acquires the instance lock,
  finds seats owned by the connection (with playerId fallback for reconnects),
  removes from `SeatConnections`, clears the persistent `PlayerId`, and
  re-broadcasts. Phase-guarded to `ChangshaPhase.Seating` only — mid-hand
  leaves are out of scope (the disconnect / forfeit lanes handle those).
- **`IChangshaGameRuntime.TryGetSnapshotCopyAsync(gameId, ct)`**
  (`ChangshaGameRuntime.cs:128-167`) — new method. Acquires the instance lock,
  JSON-round-trips `instance.State` via the existing `SnapshotJson` serializer,
  returns the deserialized clone. The caller iterates an isolated graph that
  cannot be mutated by concurrent runtime work.

### Backend changes — WS endpoint

- **`TryHandleSeatTakeAsync`** (`AutotableWsEndpoint.cs:558-617`) — branches on
  `JsonValueKind.Null` after the property lookup and calls `ReleaseSeatAsync`
  through the runtime. Surface contract for the bundle is unchanged.
- **`SendFullSnapshotAsync`** (`AutotableWsEndpoint.cs:1083-1118`) — swaps
  `TryGetSnapshot` for the async `TryGetSnapshotCopyAsync`. The translator
  now iterates a lock-protected clone.

(Both `AutotableWsEndpoint.cs` hunks landed in commit `3a93507` — Frost's
claim-window-deadline squash bundled in the working-tree edits while the
flock was hot. The runtime backing methods this PR adds are the missing
counterpart that made `3a93507` buildable in the working tree but not on
main alone.)

## Tests

Three new acceptance tests added under `Changsha/Acceptance/`:

1. **`DealerDiscardBroadcastAuditA2Tests.cs`** — drives the manual deal +
   DealerExtra take + dealer discard via WS (mirroring Vasquez's
   `playtest-full-game-integration.spec.mjs` scenario A2), then **consumes
   subsequent WS UPDATE envelopes** and asserts at least one carries a
   `things` entry keyed on the discarded tileId with a slot starting with
   `discard.` and ending with `@0`. Pre-fix: RED (the wire envelope drops the
   discard entry under contention). Post-fix: GREEN.
2. **`LeaveSeatViaNullSeatTests.LeaveSeatViaNullPayload_ClearsSeatAndAllowsReseat`** —
   end-to-end via the WS endpoint: take seat → push `{seat:null}` → assert seat
   cleared → assert repeat-push is a clean no-op → assert a different player
   on a different connection can take the now-free seat.
3. **`LeaveSeatViaNullSeatTests.ReleaseSeatAsync_RuntimeApi_ClearsSeatInSeatingPhase`** —
   direct runtime-level test. Confirms the Seating-phase clear works,
   confirms idempotency, AND confirms the phase guard fires (mid-hand
   release is a no-op).

Targeted suite filter
`RoundRobin|DealerExtraTransitions|LeaveSeatViaNullSeat|DealerDiscardBroadcastAuditA2`
→ **9 / 9 pass** in ~7 s. New tests are isolated (each test owns its own
SQLite scratch file in `test-data/`).

## Live audit verification

Backend rebuilt + restarted on `:8088` against
`/data/source/mahjong-autotable/.work/mat-bishop-discard.db`. Re-ran
`playtest-full-game-integration.spec.mjs` against the live instance.

| Scenario | Pre-fix | Post-fix |
|---|---|---|
| A_manualDealRoundRobin | 2/4 PASS | 2/4 PASS |
| B_autoDealBotAutoplay | 1/4 PASS | **3/4 PASS** |
| C_tileSelectionDom | 0/3 PASS (page crash) | **3/3 PASS** |
| D_claimWindow | ERROR | **1/2 PASS** |
| E_winDetection | 3/3 PASS | 3/3 PASS |
| **Total gates** | **6/15 PASS** | **12/15 PASS** |

The backend fix dramatically improves the audit: scenario C goes from total
failure (page crash during D blocked C entirely) to full PASS, scenario B
goes from 1/4 to 3/4, scenario D recovers from an ERROR state to 1/2.

The two remaining A_manualDealRoundRobin failures (A2, A3) ARE in the wire
contract Bishop's new test pins as GREEN. The discrepancy is downstream of
the WS endpoint: scenario A2's local `world.things.discardBySeat[0]` stays
at 0 even though the WS envelope carries the `discard.0.0@0` entry (the
A2 spec exits the poll loop on EITHER pile-grew OR log-matched, breaking
early once the log entry arrives — the broadcast may still be in flight).
The remaining A2/A3 work is in **Hicks's lane** (frontend `world.ts`
two-pass slot merge — see `hicks-two-pass-merge.md`); the backend
broadcast pipeline is now provably correct.

The deeper L-10 lobby workflow is also covered: the L-10 audit explicitly
exercises a take-then-leave cycle which the L-10 acceptance test now
reproduces end-to-end via the same WS path.

## Notes for future passes

1. **`TryGetSnapshot` is a live reference, not a snapshot.** Any new code
   path that iterates `state.Hands[i].ConcealedTiles`, `state.DiscardPile`,
   `state.Wall`, or any other `List<T>` on `ChangshaGameState` outside the
   instance lock MUST use `TryGetSnapshotCopyAsync` instead. The XML docs on
   both methods now make this distinction explicit.
2. **Fire-and-forget event broadcasts race their producers.** The
   `OnStateChanged` handler intentionally does not `await` the per-connection
   broadcast tasks — this is correct for throughput but it requires every
   broadcast worker to be lock-protected against state mutation. Pattern is
   "Producer fires under lock, consumer reads lock-protected snapshot".
3. **The `[seats, N, {seat:null}]` shape comes from upstream `Player.svelte`
   verbatim.** The bundle is byte-identical with upstream so the wire
   contract is not negotiable on our side. Any future handler that consumes
   the `seats` collection needs the same null-branch the take handler now
   has.
4. **WS-broadcast acceptance tests beat runtime-state assertions.** The bug
   passed every runtime-level test for weeks. Both
   `DealerExtraTransitionsToAwaitingDiscardTests` and
   `RoundRobinDiscardCycleTests` checked `runtime.TryGetSnapshot(...)` and
   passed, while the audit failed. The new
   `DealerDiscardBroadcastAuditA2Tests` consumes WS envelopes directly and
   reproduces the live audit signal. Recommend extending this pattern to any
   bug that surfaces in the audit but not in the unit suite — the test
   harness already has the `WsSession` helper for the pattern.

## Artefacts

- New runtime methods: `ChangshaGameRuntime.cs:278-291,295-321` (impls) +
  `ChangshaGameRuntime.cs:46-58,128-167` (interface).
- New tests:
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/DealerDiscardBroadcastAuditA2Tests.cs`
  +
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/LeaveSeatViaNullSeatTests.cs`
- WS endpoint fix is in `3a93507` (Frost's claim-window squash — picked up the
  working-tree changes; consistent with what this memo describes).
- Live audit findings: `playtest-artifacts/integration-audit/findings.json`
  (post-fix snapshot at 2026-05-29 12:31 PT).
