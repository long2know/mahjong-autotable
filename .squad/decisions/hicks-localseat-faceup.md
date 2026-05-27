# Hicks decision memo — local seat sees own hand face-up

**Date:** 2026-05-28
**Author:** Hicks (Frontend)
**Status:** Shipped (client-side workaround), with backend follow-up requested
**Scope:** `src/frontend/autotable-src/src/world.ts`, `playtest-artifacts/playtest-walls-facedown.spec.mjs`
**Companion commits:** 4d9e3ce (face-down walls), 9ca96c3 (Bishop translator synth)

## What shipped

After the manual-mode pickup ceremony, the local player (seat 0 = dealer)
now sees the FACES of their own 13 concealed hand tiles. Foreign-seat
hands stay face-down (privacy preserved). Walls stay face-down (no
regression to 4d9e3ce). Stephen can now play.

`world.ts onThings` was extended with a local-seat exception that
**forces** `rotationIndex = 1` (FACE_UP) for any hand-group slot whose
`slot.seat === this.seat` (and `this.seat !== null`). The original
face-down fallback (face===null + hand → coerce to face-down rotation)
still runs for foreign-seat hands and is unchanged for non-hand slots.

## Root cause — backend ViewerSeat is sticky-null (Bishop, please)

Diagnosis under CDP-tap of the WS frames:

```
{"slotName":"hand.8@0","rotationIndex":2,"face":null,"hasFace":true}
```

The backend is shipping **rotationIndex=2 and face=null** for the
dealer's own hand. That's the `StripFace(forceHandFaceDown=true)` path in
`AutotableWsEndpoint.FilterEntriesForViewer`. It only takes that path
when `viewerSeat.HasValue && slotSeat == viewerSeat.Value` is false.

Reading `AutotableConnection` (lines 1478–1557):

```cs
public sealed class AutotableConnection
{
    ...
    public int? ViewerSeat { get; }              // ← GET-only, no setter
    ...
    public AutotableConnection(WebSocket socket, string? gameId, int? viewerSeat)
    {
        ViewerSeat = viewerSeat;
    }
}
```

`ViewerSeat` is set ONCE at WS-upgrade time from the `?seat=` query
string. The bundle always opens the WS without a `seat=` param (the user
hasn't picked a seat yet at handshake time), so `ViewerSeat` starts at
`null`. When the user clicks "Take Seat" later, `TryHandleSeatTakeAsync`
routes the seat-take to the runtime, but **never** updates
`connection.ViewerSeat`. It stays null forever.

Consequence: for **every** post-take-seat snapshot the privacy filter
runs with `viewerSeat=null`, the `slotSeat == viewerSeat.Value` short-
circuit is false for every entry, and the dealer's own hand goes through
`StripFace(forceHandFaceDown=true)` — face stripped, rotationIndex forced
to 2 (FACE_DOWN).

The frontend has no way to "tell" the backend its seat after the fact,
so the only way to fix this in the bundle alone is to override
rotationIndex client-side for the local seat. That's what shipped.

## Requested backend follow-up (Bishop's lane)

1. Make `ViewerSeat` settable (drop `{ get; }` → add `internal set;`
   or convert to a field with a setter).
2. In `TryHandleSeatTakeAsync` after `_runtime.TakeSeatAsync` succeeds,
   set `connection.ViewerSeat = seatIndex` so subsequent snapshots
   correctly identify the dealer's own hand.
3. Add a regression test in
   `tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsEndpointTests.cs`
   (or similar) that asserts the post-take-seat snapshot includes the
   dealer's `hand.X@0` entries with `face != null` and `rotationIndex=1`
   for a viewer that took seat 0.
4. Once the backend ships, I can remove the client-side override in
   `world.ts` (it'll degrade gracefully — backend ships rotationIndex=1
   which my override also lands at).

Tagged: `bishop`, `backend-followup`, `frontend-workaround-active`.

## Why this is safe today

- Foreign hands: `slot.seat !== this.seat` ⇒ `isLocalSeatHand=false` ⇒
  original face-down fallback still runs. **No regression to 4d9e3ce.**
- Walls: `slot.group === 'wall' !== 'hand'` ⇒ `isLocalSeatHand=false`
  AND original fallback's `slot.group === 'hand'` check is false ⇒
  walls trust the backend's rotationIndex=0 (FACE_DOWN). **No
  regression to 4d9e3ce.**
- Discards / melds: `slot.group ∈ {discard, meld}` ⇒ both branches skip
  ⇒ trust the backend.
- Spectator viewer (`this.seat === null`): `isLocalSeatHand=false`
  guard ⇒ all hands face-down (correct).
- Hand-slot rotations are authored `[STANDING, FACE_UP, FACE_DOWN]`
  identically for `'hand'`, `'hand.3p'`, and `'hand.extra'` (see
  `setup-slots.ts:106,117,132`) — index 1 is always FACE_UP across
  every Changsha hand variant. The hard-coded `1` is correct.

## Validation

`E2E_BASE_URL=http://127.0.0.1:8088 node playtest-artifacts/playtest-walls-facedown.spec.mjs`
— all checks pass:

```
{
  "wallCountAtLeast100": true,         //  114 walls
  "zeroForeignHandFaceUp": true,       //  bots' hands hidden
  "allWallBackRotation": true,         //  no face-up walls
  "fourSeatWalls": true,
  "pickupReachedDealerHand": true,     //  ceremony drove tiles in
  "localSeatHandFaceUp": true          //  ← NEW: 13/13 dealer tiles at rotationIndex=1
}
ALL CHECKS PASSED
```

Spec extended with a new `localSeatHandFaceUp` gate that asserts
`>=13` dealer hand tiles have `rotationIndex === 1` (FACE_UP) in the
post-deal snapshot. Probed: `localSeatRotIdx: [1,1,1,1,1,1,1,1,1,1,1,1,1]`.

Final screenshot: `playtest-artifacts/walls-facedown/04-post-deal.png` —
seat 0 (bottom of screen) now shows tile faces (suited mahjong tiles)
instead of yellow backs.

## Lane discipline

Touched only:
- `src/frontend/autotable-src/src/world.ts` (the fix)
- `src/frontend/autotable/*` (rebuilt bundle, hashed)
- `playtest-artifacts/playtest-walls-facedown.spec.mjs` (extended)
- `.squad/decisions/inbox/hicks-localseat-faceup.md` (this memo)
- `.squad/agents/hicks/history.md` (appended)

Did NOT touch: backend C#, Frost's dealing branch, Bishop's runtime
branch, Drake's persistence.
