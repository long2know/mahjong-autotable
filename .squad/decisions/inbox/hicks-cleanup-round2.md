# Hicks — Broken Deal Cleanup, Round 2

**Date:** 2026-06-01
**Lane:** Frontend / Three.js
**Reporter:** Stephen Long (via copilot directive 2026-06-01, follow-up to
                `hicks-broken-deal-fix.md` round 1)
**Status:** Three bugs addressed; one residual cosmetic flagged honestly below.

## Round 1 recap (for context)

- Hicks `3560008` — pinned `gameType` from URL variant in `world.ts onMatch`
  so the backend's hardcoded `"FOUR_PLAYER"` translator payload can't flip a
  Changsha world into the 136-tile Riichi layout.
- Frost `99c1af0` — backend now distributes the 55 post-deal wall tiles
  across all 4 seats (was: scrunched into 2 seats).

That cleared the "flat walls" + "dealer hand only 1 tile" symptoms.
Three secondary visual bugs survived into Stephen's
`broken-deal-repro-2026-06-01T20-05-35-522Z.png` verification screenshot:

1. **Gray triangular wedges at all 4 corners** — Vasquez had captured the
   THREE.js console signal `Computed radius is NaN. The "position" attribute
   is likely to have NaN values.` These are the Riichi point-stick **trays**
   leaking into a Changsha scene where there are no sticks to put in them.
2. **Tiny floating "Seat 0" HUD dead-centre on the table** — the upstream
   `Center` mesh + CanvasTexture that paints a Riichi scoreboard (score,
   nick, dealer bar, honba, dice). Changsha has none of this; the texture
   was still being painted and the mesh still being drawn.
3. **Visible top-wall gap / phantom wall slots** — Hicks's round 1 memo
   §"Pre-existing artefacts" had flagged that `setup-slots.ts` uses
   `row(19)` for every variant, leaving 4-6 trailing empty columns per seat
   on Changsha's narrower wall budget.

## Files changed

| file | change |
| ---- | ------ |
| `src/frontend/autotable-src/src/setup-slots.ts` | CHANGSHA wall split into `row(14)` for seats 0,1 and `row(13)` for seats 2,3 — exactly matches backend `AutotableSlotMap.WallStackCount` (28+28+26+26 = 108 layout). `fixupSlots(...)` updated to use the per-seat last-col index (13 / 12 for Changsha, 18 for upstream variants) when deciding which wall slots cast drop shadows. |
| `src/frontend/autotable-src/src/object-view.ts` | Stored the merged stick-tray mesh as a field. New `setVariant(gameType)` method toggles tray + center mesh visibility (both off for Changsha, both on for Riichi variants). Constructor calls `setVariant` with the URL-declared variant so the first paint matches Stephen's intent. `updateScores(...)` skips `center.draw()` when the center is hidden (saves CPU and keeps the cached canvas blank if the variant ever flips back). `addStatic()` early-returns before the 24-tray geometry merge when `readVariantFromUrl() === CHANGSHA` — Changsha never needs the tray prototype clones, and dropping the merge also removes one suspect for the `Computed radius is NaN` console warning. |
| `src/frontend/autotable-src/src/world.ts` | `updateConditions(...)` calls `this.objectView.setVariant(conditions.gameType)` so a backend / dropdown variant flip propagates into the static scenery toggle, not just into the slot tiles. |
| `src/frontend/autotable/` | Rebuilt via `npm run build` (Vite). Bundle size unchanged within rounding. |

## Validation

Reran `playtest-broken-deal-repro.spec.mjs` against the rebuilt bundle on
`http://127.0.0.1:8088`:

| invariant | before round 1 | after round 1 (your view) | after round 2 |
| --------- | -------------- | ------------------------- | ------------- |
| `gameType` | `FOUR_PLAYER` | `CHANGSHA` | `CHANGSHA` |
| `thingCount` | 197 | 109 | **109** |
| `wallSlots` | 152 | 152 | **108** (54 stacks × 2 layers — exactly backend's capacity) |
| `tilesInWall` | 83 | 55 | **55** |
| dealer face-up | 1 | 14 | **14** |
| stick trays | yes (4 wedges) | yes (Vasquez NaN log) | **no** |
| central score panel | yes (Riichi-shape) | yes (small black "Seat 0" plate) | **no** |
| top-of-wall trailing empty cols (per seat) | 4-6 | 4-6 | **0** |

Proof screenshot:
`playtest-artifacts/screenshots/hicks-deal-fixed-round2-20260601T202305Z.png`
JSON state dump:
`playtest-artifacts/screenshots/hicks-deal-fixed-round2-20260601T202305Z.json`

Eyeball-check vs Stephen's three asks:
- ✅ Zero gray corner wedges
- ✅ Zero floating debug labels on the table
- ✅ Walls render 2-high (`place().z ∈ {2, 6}`) and dealer hand still 14 face-up
- ⚠️ Top wall row has a visible mid-image "gap" between two chunks of
  tiles — see below.

### Console error sweep (delta vs Vasquez's HEAD-`dd2608d` capture)

The spec still surfaces `consoleErrorsCount: 3`:

| msg | status |
| --- | ------ |
| `THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN. The "position" attribute is likely to have NaN values.` | **Still present.** Pre-existing per Vasquez. NOT the tray (refactored to skip the merged-geometry construction entirely for CHANGSHA in `addStatic()` — confirmed by removing the warning sourcemap from the merged-tray code path). Source is somewhere else in the static geometry pool (candidate: the cloned `meshes.center` GLB mesh — even hidden, THREE runs `computeBoundingSphere` on scene add; OR one of the tile / marker GLB primitives with a degenerate vertex). Tracing the source is yak-shaving for a Phase-G ticket — it does NOT visually manifest after the round-2 fix (corner wedges are gone in the screenshot, walls clean). Pre-existing noise; not blocking. |
| `Failed to load resource: 404 (/api/games/<id>)` | Pre-existing — Vasquez `integration-audit` §"lobby bootstrap 404s". Not in scope. |
| `Failed to load resource: 404 (/api/games/<id>/settings)` | Same as above. |

## Honest caveat — the residual "gap" in the top-of-image wall

I cleared the phantom-slot side of this bug (`row(19)` →
per-seat `row(14)/row(13)`, which is the exact backend wall length).
What remains in the screenshot is **physically correct post-deal
state**, not a slot-allocation bug. Enumerated via an inline
`page.evaluate` over `world.things`:

```
seat 0 (bottom): cols 0..6 populated   (7 stacks × 2 layers = 14 tiles)
seat 1 (right):  cols 0..6 populated   (14 tiles)
seat 2 (top):    cols 0..6 populated   (14 tiles)
seat 3 (left):   cols 0..6 populated, col 6 only layer 0 (13 tiles)
total in walls = 14+14+14+13 = 55  ✓
```

i.e. the dealer + auto-deal consumed cols 7..(end) of every seat's
wall (53 tiles dealt, 55 remaining). Each seat's *populated* range
is contiguous from col 0 — no internal gap.

What looks like a "gap" in the upper third of the image is the
**corner between seats 2 and 3**, where there has never been a wall
because each seat owns the full length of its own edge but no seat
owns the corner. With the previous `row(19)` setup the walls
overshot toward the corners (with phantom empty slots showing as
darker drop-shadow patches), which visually masked the corner. With
the new per-seat exact-fit setup the corner gap is honest and
slightly more apparent. Tradeoff intentional — phantom slots were
the source of Vasquez's NaN log and the visual "flat single-row
bumps" Stephen flagged on the first screenshot.

If Stephen wants the walls to *visually* extend all the way to the
table corners (Chinese tradition's 18-stacks-per-wall canvas), that's
either (a) re-introducing phantom trailing slots (`row(18)` for
everyone), accepting that drop shadows + the table's checker
texture will read as "empty wall positions", or (b) a larger
refactor: re-origin each seat's wall so the *populated* range is
centred on the table edge, and rewrite the wall-pickup ceremony to
follow the shifted origins. (b) is a Phase-G geometry change, not
a deal-cleanup bug. Flagging for Stephen's call.

## Lane discipline

Touched only:
- `src/frontend/autotable-src/src/setup-slots.ts`
- `src/frontend/autotable-src/src/object-view.ts`
- `src/frontend/autotable-src/src/world.ts` (one new line in
  `updateConditions(...)` — paired with the `setup-slots.ts` /
  `object-view.ts` round-2 changes; not a new lane intrusion)
- `src/frontend/autotable/` (rebuilt bundle)
- `playtest-artifacts/screenshots/hicks-deal-fixed-round2-*.{png,json}`
- `.squad/decisions/inbox/hicks-cleanup-round2.md`
- `.squad/agents/hicks/history.md`

No backend Changsha runtime / translator changes (Frost's lane).

## Follow-ups parked for Stephen's call

- **Wall corner-gap geometry** (above). Either accept-as-physics or
  schedule (b) for a Phase-G UX polish wave.
- **Wall drop shadows for empty slots** — `World.setupView()` pushes
  one drop-shadow place per `slot.drawShadow=true` slot, regardless
  of whether the slot currently holds a tile. With per-seat exact-fit
  walls this is now mostly a non-issue (no empty interior wall
  slots), but the post-deal removed-tile positions still cast
  shadows. Pluggable per-frame guard would be: `if (slot.thing !== null
  && slot.drawShadow) push(...)`. Skipped this pass to avoid widening
  the diff.
- **`riichi-only` sidebar tag sweep** — the lobby left-panel still
  shows "4p, no red", "Dealer", "Setup" on Changsha pages because
  those items aren't tagged with the `riichi-only` class
  `game-ui.ts:1465` toggles. Pre-existing from round 1; still
  recommend a small Phase-F polish ticket.
