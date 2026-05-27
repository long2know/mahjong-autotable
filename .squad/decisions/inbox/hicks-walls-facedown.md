### 2026-05-27T22:45:00Z: Hicks — Face-down walls + canonical 4-wall manual-deal layout

**By:** Hicks (Frontend)

**Directive:** `.squad/decisions/inbox/copilot-directive-2026-05-27T2127Z-face-down-walls.md` (Stephen). "Tiles MUST start FACE DOWN", "(4) simple walls", "pick groups of FOUR".

**Branch:** `fix/facedown-walls-and-pickup-choreography` off main `c616407`.

---

## Root cause

`world.ts` `onThings` had an unconditional privacy fallback:

```ts
if (thingInfo.face === null && slot.rotations.length > 1) {
  rotationIndex = slot.rotations.length - 1;
}
```

The backend (`AutotableWsEndpoint.FilterEntriesForViewer` → `StripFace`) nulls
the `face` field for every foreign-seat tile to avoid leaking ids over the
wire. The frontend was then coercing the rotation to "the last entry in
`slot.rotations`" as a privacy fallback.

That convention is correct **only** for `hand` slots whose rotation list is
`[STANDING, FACE_UP, FACE_DOWN]` — last index = FACE_DOWN, hides the tile.

For other slot groups the rotation list does **not** put FACE_DOWN last
(see `setup-slots.ts`):

| Slot group | rotations[]                                              | last entry             |
| ---------- | --------------------------------------------------------- | ---------------------- |
| `wall`     | `[FACE_DOWN, FACE_UP]`                                    | **FACE_UP** ← bug      |
| `discard`  | `[FACE_UP, FACE_UP_SIDEWAYS, FACE_DOWN, FACE_DOWN_SIDEWAYS]` | FACE_DOWN_SIDEWAYS ← bug |
| `meld`     | `[FACE_UP, FACE_DOWN]`                                    | FACE_DOWN              |

So every wall tile rendered for a foreign seat got flipped face-up, exposing
the suits and breaking Stephen's "4 face-down walls" requirement. Discards
from the three other seats took a similar miscarriage.

The backend already authors the correct rotation for non-hand slots
(`ChangshaToAutotableTranslator.BuildThingEntries` sets `WallRotFaceDown=0`,
`DiscardRotFaceUp=0`, etc.) and only force-overrides the rotation when the
slot key starts with `hand.` (`StripFace` lines ~1257). So the frontend
fallback is **redundant** for non-hand slots and **wrong** for walls/discards.

---

## Fix

Two surgical edits to `src/frontend/autotable-src/src/world.ts`:

### 1. `onThings` — restrict the privacy fallback to hand slots

```ts
// Hicks 2026-05-27 — RESTRICTED to `slot.group === 'hand'` …
if (
  thingInfo.face === null &&
  slot.group === 'hand' &&
  slot.rotations.length > 1
) {
  rotationIndex = slot.rotations.length - 1;
}
```

For wall/discard/meld slots we now trust the backend-authored
`rotationIndex` (which is always `0 = FACE_DOWN` for walls).

### 2. Constructor — start in `DealType.INITIAL` when `?dealMode=manual`

Before WS connects, the bundle previously initialised the local `Setup` with
`DealType.HANDS`, which lays 13 tiles into each seat's hand and only 55 into
the wall. The very first paint (before any `onThings` arrives) therefore
showed pre-dealt hands — even if briefly. We now read `?dealMode=manual`
from the URL synchronously and override `conditions.dealType = INITIAL` so
the first paint matches the post-WS `RollingDice` snapshot (all 108 tiles
in walls, face-down).

---

## Validation

Backend running on `:8088` with isolated `Data Source=/tmp/hicks-walls.db`.

### New spec — `playtest-artifacts/playtest-walls-facedown.spec.mjs`

Loads `?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Hard&handCount=4`,
takes seat 0, calls `g.world.deal('HANDS')`, asserts on `window.game.world.things`:

```
{
  "wallCount": 106,
  "wallBackRotationCount": 106,      // every wall tile at rotation 0 (face-down)
  "wallFrontRotationCount": 0,       // zero wall tiles at FACE_UP
  "foreignHandFaceUp": 0,            // no other-seat hand tile is face-up
  "wallSlotRotationsLen": [2],       // confirms slot.rotations = [FACE_DOWN, FACE_UP]
  "wallSeats": [0,1,2,3]             // four canonical walls
}
checks: {
  wallCountAtLeast100: true,
  zeroForeignHandFaceUp: true,
  allWallBackRotation: true,
  fourSeatWalls: true,
  pickupReachedDealerHand: true     // dealer hand grew 9 → 14 over 3.5 s
}
pageErrorsCount: 0
```

Pickup choreography polling sample (`5-pickup-choreography`):

```
t=0     dealerHand= 9  allHand=30  wallCount=106
t=500   dealerHand= 9  allHand=34  wallCount=102
t=1000  dealerHand= 9  allHand=38  wallCount= 98
t=1500  dealerHand=13  allHand=46  wallCount= 90
t=2000  dealerHand=13  allHand=50  wallCount= 86
t=2500  dealerHand=14  allHand=59  wallCount= 77
t=3500  dealerHand=14  allHand=61  wallCount= 75
```

Wall drains in groups of ~4 per seat across ~3 s — exactly the
counter-clockwise 4-per-pick ceremony Bishop's `driveManualDealChain`
already drives. Visual confirmation in
`playtest-artifacts/walls-facedown/{01-lobby,02-connected-walls,03-mid-pickup,04-post-deal}.png`.

### Regression — `playtest-artifacts/playtest-v3-fresh.spec.mjs`

All steps `ok: true`, `pageErrorsCount: 0`. No spectator/autoplay
regression.

### Build

`npm run build` clean. Bundle sizes within baseline.

---

## Lane discipline

Only Hicks-owned files touched:
- `src/frontend/autotable-src/src/world.ts` (frontend lane)
- `playtest-artifacts/playtest-walls-facedown.spec.mjs` (new spec)
- `playtest-artifacts/walls-facedown/*.png` (artifacts)

Untouched: backend, Ferro CSS (claim-window/win-screen/variant-picker),
workflows.

---

## Known follow-ups (not in this PR)

1. **Backend emits `gameType="FOUR_PLAYER"`** in `BuildMatch` (line 326,
   `ChangshaToAutotableTranslator.cs`), so the bundle's `Setup.replace`
   path creates 136 tiles (ids 0..135) even though Changsha has 108. The
   extra 28 tiles end up in the local-INITIAL wall slot 14..17 of two seats.
   Visually fine (still face-down) but worth a future translator pass to
   emit a Changsha-aware `gameType` and prune the ghost tiles.
2. **`face===null` from backend during pre-deal phases** — the backend
   `state.Wall` is empty during `Seating`/`RollingDice` until
   `BeginManualDeal` materialises the shuffled wall. The bundle's local
   `DealType.INITIAL` keeps the visual integrity in the meantime. If/when
   Bishop adds a "synthesize wall placement" path in `BuildThingEntries`,
   we can stop relying on the local fallback.

---

## Files

- `src/frontend/autotable-src/src/world.ts` — `onThings` (l. ~225-235), constructor (l. ~96-115)
- `playtest-artifacts/playtest-walls-facedown.spec.mjs` — 6-step spec
- `playtest-artifacts/walls-facedown/{01-lobby,02-connected-walls,03-mid-pickup,04-post-deal}.png`

Branch: `fix/facedown-walls-and-pickup-choreography`.
