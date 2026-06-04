# Vasquez — Human-led playtest harness + backend gap findings

**Author:** Vasquez (Rules Engineer)
**Date:** 2026-05-25
**Branch:** `feat/playtest-human-led`
**Deliverable:** `playtest-artifacts/playtest-human-led.spec.mjs` + screenshots + `findings.json`

## Decision

Ship the playtest harness as observational tooling **now**. The backend
manual-deal wiring and the frontend `world.emitDiscard` wiring are
separately tracked follow-ups for Bishop and Hicks respectively. The
playtest is intentionally non-failing on missing affordances — it
documents what is and isn't wired so the next iteration can target the
real gaps directly.

## Why this playtest is needed

`playtest-v3-fresh.spec.mjs` covers spectator mode (no seated viewer).
The human-led path is materially different:

- Dealer must claim a seat before Deal fires.
- Manual deal requires `?dealMode=manual` + drives `pickup` collection
  via `world.emitRollDice` + `world.emitTakePickup`.
- Hand tiles must broadcast to the seated client (privacy-filtered).
- Bot autoplay must continue after the human's discard.

None of these are exercised by the spectator harness. The new spec
attempts the full loop and captures findings even when intermediate
steps degrade.

## Findings (verified against backend on port 8089)

### Gap 1 — `?dealMode=manual` is a no-op on first hand

- `AutotableConnection.DealMode` (string) is set from the query at
  `src/backend/.../AutotableWsEndpoint.cs:266`.
- The value is **never** propagated to `ChangshaGameState.DealMode`
  (enum), which defaults to `DealMode.Auto` at
  `src/backend/.../Changsha/ChangshaDomain.cs:417`.
- `StartGameAsync` (`ChangshaGameRuntime.cs:461-499`) inspects
  `state.DealMode == DealMode.Manual` and falls through to auto-deal.
- Manual flow only activates inside `RollDiceAsync`
  (`ChangshaGameRuntime.cs:504`) which is reached only from the
  RollingDice phase, which auto-deal skips.

**Symptom captured:** `findings.collections.pickup === 0` over the
entire session; pickup-driver loop bails on iteration 1.

**Recommended fix (Bishop):** In `CreateGameAsync` (or `StartGameAsync`
before the auto/manual branch), apply
`state.DealMode = connection.DealMode == "manual" ? DealMode.Manual : DealMode.Auto`.
Optionally accept the `dealMode` field on a `match` collection update
so the lobby can toggle it mid-session.

### Gap 2 — Hand tiles never broadcast to the human seat

- After Deal, `cli.things.size === 197` but `thingsByPrefix` is
  `{ wall: 136, marker: 1, tray: 60 }` — **zero** entries whose
  `slotName` starts with `hand.`.
- The runtime auto-deal completes (move log shows
  "Match started — dealer is Seat 0"), but
  `ChangshaToAutotableTranslator` does not emit hand-tile placements
  until `AcknowledgeDealAsync` runs for each human seat
  (`ChangshaGameRuntime.cs:582-591`).
- The frontend autotable bundle has **no wiring** to call ack from the
  autotable WS endpoint. Ack only exists on the SignalR `changsha` hub,
  which the autotable client does not subscribe to.

**Symptom captured:** `findings.postDealHandSize.handTileCount === 0`,
`thingsByPrefix: { wall: 136, marker: 1, tray: 60 }`.

**Recommended fix (Bishop):** Either
(a) make ack implicit when the first human's WS connection finishes
    initial bootstrap (broadcast hand state immediately on
    `OnConnectedAsync` once the game is dealt), or
(b) add an `ackDeal` collection route (`{kind: 'ackDeal', key: seat}`)
    that `ChangshaGameRuntime.AcknowledgeDealAsync` calls into.

Option (a) preserves backward-compat and removes a step from the
client; (b) keeps the explicit handshake model and matches the SignalR
contract.

### Gap 3 — Bot autoplay never starts

Combined effect of 1+2: with no hand state and no ack, runtime stays
pre-AwaitingDiscard. Bots never fire Pung/Chow/Hu. Move log stays at
a single "Match started" entry through the 60-second observation
window.

Fix follows automatically once Gap 1 + Gap 2 are addressed.

### Gap 4 (informational) — `discard` collection route is already wired backend-side

`AutotableWsEndpoint.TryHandleDiscardActionAsync` at
`src/backend/.../AutotableWsEndpoint.cs:711-743` already accepts a
`{kind: 'discard', key: seat, value: {tileId}}` UPDATE message and
routes to `_runtime.DiscardAsync`. The frontend bundle simply has no
caller. The playtest exercises this path as backdoor (d) — once a
real hand exists, a one-liner
`window.game.client.update([['discard', String(seat), { tileId }]])`
should fire a real discard end-to-end.

**Recommended fix (Hicks):** Add a `world.emitDiscard(tileId)` that
performs the above one-liner. That is the minimum-viable frontend
wiring for human discards in the current build.

## Synthetic-Hu sanity check (working)

The harness proves out a reusable backdoor:

```js
const events = cli.events ?? cli['events'];
events.emit('update', [['gameComplete', 'current', {
  isComplete: true,
  totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
  handHistory: [],
  maxHands: 4,
}]], false);
```

This fan-outs through the per-collection `onUpdate` handler →
`GameUi.renderGameComplete` → `#game-complete-modal.style.display` flips
to `block`. `findings.syntheticHu === { ok: true, modalVisible: true }`.

Skill extracted to `.squad/skills/playtest-ws-backdoor/SKILL.md`.

## Risk assessment

Shipping observational. No runtime, build, or test path is modified.
The spec file is parallel to `playtest-v3-fresh.spec.mjs` and uses the
same `playtest-artifacts/` directory.

## Status

- [x] Spec written, 15/15 steps green
- [x] Findings captured in `playtest-artifacts/human-led/findings.json`
- [x] Screenshots captured (final commit ships 1 representative pair)
- [x] history.md updated
- [x] skill extracted
- [ ] Backend gaps 1+2 — follow-up for Bishop
- [ ] Frontend `world.emitDiscard` — follow-up for Hicks
