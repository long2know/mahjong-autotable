# Hicks — preview/claim tile divergence fix

**Date:** 2026-05-29
**Owner:** Hicks (Frontend Dev)
**Branch:** `fix/preview-tile-discardable` → merged to `main`
**Tracks:** `bishop-dealerextra-fix.md` §"Known follow-up — preview/claim tile divergence (Hicks)" (lines 112–128)

## Summary

The front-end optimistically lays down a local `setup.deal('HANDS')` in
`joinMatch` that places tiles in `hand.0..12@N` plus a 14th preview
tile in `hand.extra@N`. The backend then progressively overwrites
`hand.0..13@N` during the pickup ceremony. After Bishop's W23 fix
landed, the dealer-extra take animation reliably reached
`AwaitingDiscard`, but the spec's M7 direct-API fallback failed
G4_discard because it picked a "phantom" hand tile whose `tileId` the
runtime didn't recognize.

This decision memo captures the two phantom shapes, the surgical
front-end fix, and the validation result.

## Root cause (two shapes of the same problem)

Both shapes share the same trigger: the local `setup.deal('HANDS')`
runs BEFORE any backend `things` push lands, so the hand slots are
pre-populated with local-only tile-ids that the runtime has no
knowledge of.

### (a) Orphan in `hand.X@N`

When the backend pushes its own tile into a `hand.X@N` slot already
occupied by a local-deal tile, `World.onThings` calls the displaced
Thing's `prepareMove()` to free `slot.thing` (see `thing.ts:54`):

```ts
prepareMove(): void {
  if (this.slot !== null) {
    this.slot.thing = null;   // slot forgets the orphan
    // BUG: this.slot is NOT cleared — orphan still points back
  }
}
```

The orphan keeps `this.slot` pointing at the now-foreign slot. Any
downstream filter like `t.slot.group === 'hand' && t.slot.seat === self`
still passes for the orphan, even though `slot.thing` is now the
backend tile.

### (b) Pure preview in `hand.extra@N`

`AutotableSlotMap.HandSlot` (backend) only emits `hand.0..13@N`; the
`hand.extra@N` slot is a frontend-only invention used to *visually*
park the dealer's 14th tile before the take button is wired up. The
backend never writes here, so the local preview tile sits unmolested
and authoritative-looking (`slot.thing === self`), yet its `tileId`
remains in the runtime-side wall.

### Why M7 picks the phantom

The phantoms are local-only Things — they never receive a backend
`things` update, so their `claimedBy` retains the constructor default
of `null`. Backend-pushed tiles get `claimedBy: undefined` (the
`JsonSerializerDefaults.WhenWritingNull` option in
`AutotableJson.Options` strips the explicit `null`). The M7 fallback
in `playtest-playable-interaction.spec.mjs:627-666` picks the first
hand tile with `claimedBy === null` — which matches phantoms *and only
phantoms*.

## Fix

Two surgical edits to `src/frontend/autotable-src/src/world.ts`:

1. **`emitDiscard(tileOrId)`** — before validating the target slot,
   detect orphans (`tile.slot.thing !== null && tile.slot.thing !== tile`)
   and remap to the slot's authoritative occupant when the occupant
   is in our hand. Then reject any target whose slot name starts with
   `hand.extra@`. This way:
   - A click on (or M7 pick of) an orphan in `hand.X@N` gets routed
     to the backend's tile in that slot — discard succeeds.
   - A click on the `hand.extra@N` preview returns `false`; the UI /
     caller picks a different tile. The dealer's actual 14th tile
     lives in `hand.13@N` post-take and is selectable normally.
2. **`hasExtraHandTile()`** — the click-to-discard gate previously
   counted *every* tile reporting `slot.group === 'hand'` for our
   seat, which the phantoms inflated. Tightened to count only
   backend-authoritative tiles (`slot.thing === thing` AND
   `!slot.name.startsWith('hand.extra@')`). This stops the gate from
   firing prematurely (e.g., during the pickup ceremony when 13 real
   tiles + 1 phantom would have triggered click-to-discard).

The deeper bug — `Thing.prepareMove` never clearing `this.slot` on
the displaced Thing — is upstream code (autotable v1.x heritage) and
fixing it risks animation glitches across every slot transition.
The slot-thing-based remap is the surgical, symptom-level fix that
the lane discipline asks for.

## Validation

```
$ E2E_BASE_URL=http://127.0.0.1:8088 \
    node playtest-artifacts/playtest-playable-interaction.spec.mjs
...
=== GATE SUMMARY ===
  G1_setup: PASS
  G2_takeButton: PASS
  G3_selectUI: PASS
  G4_discard: PASS
  G5_autoDealFaceUp: PASS
pageErrors=0 consoleErrors=6 networkFails=4
ALL GATES PASSED
```

The G4 PASS payload confirms the remap kicked in:
- `M7-direct-api-discard` picked `targetId=118` (a phantom — id outside
  the Changsha 0..107 range), `emitDiscard` returned `true`, the hand
  count dropped 17→16, and the discard pile grew 0→2 (the two
  remapped-but-also-bot discards from this round). The runtime
  accepted the discard because the actual tileId emitted on the wire
  was the slot's authoritative backend tile, not the phantom's id.

## Known limits / follow-up

- Phantom tile ids reach 108..135 even though Changsha has
  `tileLimit=108`. The frontend's `setup` allocates 136 upstream-style
  tiles somewhere before settling on Changsha conditions. Not the
  blocker for G4 (the remap handles any phantom id) but worth a
  future cleanup memo so the local deal never creates tiles outside
  the active variant's id range. Tagged as low-priority.
- The two-Thing-per-slot orphan render visually overlaps for one
  frame between a backend push and the next animation tick. Not
  user-visible in practice (the take animation hides it), but a
  proper fix in `Thing.prepareMove` (clear `this.slot` too) would
  eliminate the overlap entirely. Defer until a Wave-5 polish pass.

## Files changed

- `src/frontend/autotable-src/src/world.ts` — `emitDiscard()` and
  `hasExtraHandTile()` updates with inline rationale comments.
- `src/frontend/autotable/*` — rebuilt Parcel bundle (hashed
  filenames; backend serves these as static assets).
- `src/frontend/autotable-src/dist-size.json` — bundle metadata
  refreshed by the build.

## Co-signers

- Bishop's `bishop-dealerextra-fix.md` §"Known follow-up" (commissioned
  the work).
- Vasquez's `playtest-playable-interaction.spec.mjs` (the acceptance
  oracle — untouched by this change).
