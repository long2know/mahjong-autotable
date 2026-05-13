> **⚠️ SUPERSEDED — see `.squad/decisions/inbox/ripley-pivot-plan.md`.**
> The architecture described below was abandoned in the pivot to autotable-vendored Changsha-native. Kept for archaeology only; will be hard-deleted in Phase E.
---

# Changsha ↔ Autotable Bridge Protocol

> Status: **v1 (Phase 2)** · Owner: Hicks (Frontend Dev)
> Scope: postMessage protocol between the Changsha React app (parent
> window) and the bundled autotable Parcel app (iframe child).

## Why a bridge?

The bundled autotable client at `/autotable/` is a self-contained Parcel
app with its own DOM, canvas, three.js scene graph, and WebSocket
protocol. We do **not** modify it. Instead, the Changsha React UI embeds
it via `<iframe src="/autotable/" />` and communicates via
`window.postMessage`.

## Phase 2 scope

- **Parent → Child only.** Changsha state changes drive the iframe scene.
  The receiver shows an overlay summarising current state and dispatches
  custom DOM events (`changsha-bridge:*`) inside the iframe.
- **Child → Parent stubbed.** The receiver posts a single `ready` message
  on init so the parent can flush its outbound queue. Bidirectional
  canvas events (tile clicks → discard / claim) is **Phase 3**.

## Wire format

All messages are plain JSON-serialisable objects with a discriminating
`proto` field:

```ts
{ proto: 'changsha-bridge/1', type: '...', /* payload */ }
```

The version suffix (`/1`) lets us evolve the protocol without breaking
older receivers loaded by browser cache.

## Parent → Child messages

| Type            | Payload                                                                 | Behaviour |
|-----------------|-------------------------------------------------------------------------|-----------|
| `hello`         | `{ gameId: string }`                                                    | Resets receiver and binds it to a new game id. |
| `reset`         | `{}`                                                                    | Clears receiver scene state. Used between games or on remount. |
| `phase`         | `{ phase: GamePhase }`                                                  | Notifies receiver of the current `ChangshaGameState.phase`. The receiver MAY use this to switch camera/perspective. |
| `dice`          | `{ die1: number; die2: number; sum: number }`                           | A dice roll just resolved. Receiver may animate / show dice. Phase 2 just toggles the bundled `#dice-img` opacity. |
| `breakPoint`    | `{ wallIndex, stackIndex, tileIndex }`                                  | Wall break point computed from dice. Receiver may highlight that wall position. |
| `tilesDealt`    | `{ seatIndex, tileIds: number[], tileCount, isComplete }`               | A batch of tiles was dealt to a seat. `tileIds` is empty for non-self seats. |
| `tileDiscarded` | `{ seatIndex, tileId }`                                                 | A tile was discarded; receiver moves a mesh to the discard pile area. |
| `claimMade`     | `{ seatIndex, tileIds: number[], meldType: string }`                    | A claim was awarded; receiver renders an open meld for that seat. |

## Child → Parent messages

| Type        | Payload                                                  | Phase |
|-------------|----------------------------------------------------------|-------|
| `ready`     | `{}`                                                     | 2 — emitted once after the receiver finishes init. |
| `tileClick` | `{ tileId: number; seatIndex: number }`                  | 3 — user clicked a tile mesh. To be wired to `Discard` / `Claim` hub commands. |
| `tileDrop`  | `{ tileId: number; target: 'discard' \| 'meld' \| 'wall' }` | 3 — drag-and-drop result on the canvas. |

## Ordering & idempotency

- Parent messages are queued until the receiver posts `ready`. Order is
  preserved.
- The parent diffs successive `ChangshaGameState` snapshots
  (`diffAndSend`) so re-renders that don't change observable state
  produce no traffic.
- The receiver MUST tolerate duplicate `phase` / `hello` messages.

## Files

- Parent client: `src/frontend/modern/src/changsha/autotableBridge.ts`
- Receiver: `src/frontend/autotable/changsha-bridge-receiver.js`
  (loaded via `<script src="changsha-bridge-receiver.js" defer>` in
  `src/frontend/autotable/index.html`).
- Embedding component: `<AutotableViewport>` in
  `src/frontend/modern/src/pages/ChangshaTablePage.tsx`.

## Phase 3 plan

1. Receiver listens to autotable canvas events (raycaster on tile click,
   drag-end on tile mesh) and posts `tileClick` / `tileDrop` upstream.
2. Parent maps inbound `tileClick` → `actions.discard(tileId)` when the
   active phase is `awaitingDiscard` and the user owns the seat.
3. Parent maps inbound `tileDrop` with `target: 'meld'` to `claim` for
   pung/kong, with chow disambiguation via a small popover.
4. Add `pong/kong/chow/hu` button overlays inside the iframe, posted
   from the parent during `awaitingClaim` windows.
5. Reconcile origin checking: tighten `postMessage(msg, '*')` to a
   specific origin once the deployment story is settled.
