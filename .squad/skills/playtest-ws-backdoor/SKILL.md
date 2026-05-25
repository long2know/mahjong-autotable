# Skill: Playtest WS backdoor — observe / drive collections without UI

## Use when

You need to write a Playwright (or browser-side) playtest for the
autotable bundle and one of the following is true:

- A WS-collection route is wired backend-side but no frontend UI calls
  it yet (e.g. `discard` after backend ships but before
  `world.emitDiscard` lands).
- A server-only collection (`gameComplete`, `result`, ...) needs to
  surface in the UI as part of a smoke test, but you can't easily
  drive a real win from outside the engine.
- You want to verify event plumbing (e.g. `#game-complete-modal`
  renders) independently of the game-state machine.

## Pattern A — WS-direct collection push (drive a backend route)

The `Client` exposes its raw collection-update method publicly:

```js
window.game.client.update([[kind, key, value]]);
```

When the client is connected, `Collection.update` does NOT emit
locally; it ships the update to the server. The server's
`AutotableWsEndpoint.HandleUpdateAsync` dispatches by `kind`:

- `seats`     → `TryHandleSeatTakeAsync`
- `claim`     → `TryHandleClaimActionAsync`
- `pickup`    → `TryHandlePickupActionAsync`
- `match`     → `TryHandleMatchActionAsync` (e.g. `{dealCommand: 'start'}`)
- `discard`   → `TryHandleDiscardActionAsync`
- `result`    → ignored (server-only)
- default     → passthrough relay (mouse/sound/dice/things/nicks)

Backdoor example (human-led playtest discard):

```js
window.game.client.update([
  ['discard', String(window.game.client.seat), { tileId, seatIndex: window.game.client.seat }]
]);
```

This works the instant the backend route exists, even if the bundle
has no UI for it.

## Pattern B — Local synthetic update (verify a UI subscriber)

`BaseClient` extends `EventEmitter` through a private `events` field.
At runtime TS private semantics don't apply, so you can dispatch a
synthetic `update` event that every Collection's `onUpdate` will fan
out:

```js
const cli = window.game.client;
const events = cli.events ?? cli['events'];
events.emit('update', [['gameComplete', 'current', {
  isComplete: true,
  totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
  handHistory: [],
  maxHands: 4,
}]], false);   // false = don't echo back to server
```

This drives `GameUi.renderGameComplete` and flips
`#game-complete-modal.style.display` to `block` — proving the modal
renders end-to-end without needing a real Hu.

## Caveats

- Pattern A requires the connection to be open. `Client.connected`
  must be true; otherwise `update` will fall back to a local emit and
  the server route never fires.
- Pattern B does NOT round-trip the server; nothing persists. Use it
  for UI surface checks only.
- `things` collection: keys are tile-ids (numbers), slot names live on
  the value (`v.slotName`) per `AutotableProtocol.cs:24`. Don't filter
  by `key.startsWith('hand.')` — filter by
  `v.slotName?.startsWith('hand.')`.
- The `tour-overlay` intercepts clicks on `.take-seat` buttons unless
  defanged via `page.addInitScript` injecting
  `#tour-overlay { display: none !important }`.
- `#lobby-panel` reopens after navigation; close it via `#lobby-close`
  before clicking `#connect` (W23 known UX bug; both playtest specs
  use this workaround).

## When NOT to use

Don't use Pattern B to "fix" a missing backend route — if a collection
update would normally be server-pushed, dispatching it locally only
fools the UI. Pattern A is the authoritative way to test backend
routes; Pattern B is strictly a UI smoke check.

## Provenance

Extracted from `playtest-artifacts/playtest-human-led.spec.mjs`
(human-led Changsha playtest, 2026-05-25). See
`.squad/decisions/inbox/vasquez-human-led-playtest.md` for the
companion gap analysis.
