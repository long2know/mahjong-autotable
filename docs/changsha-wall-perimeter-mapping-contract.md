# Changsha wall perimeter mapping contract (Bishop → Hicks)

**Scope:** Ripley §12.1 (RC-4 re-upgraded) — the wall must render as a **single physical
perimeter arc** that depletes contiguously from the dice break point. Seat-major *ordinal*
contiguity in `AutotableSlotMap.WallOrdinalToSlot` does **not** guarantee physical *corner*
contiguity because `setup-slots.ts` rotates each seat's wall. This is a **cross-lane
co-design**: Bishop owns the authoritative ordinal→slot order (below); Hicks owns
`setup-slots.ts` geometry so the composed world positions are corner-contiguous. **The
Changsha engine draw order is NOT changed.**

## 1. Authoritative ordinal → slot mapping (backend, frozen)

`AutotableSlotMap` (unchanged by this work) defines the render ring:

- **Capacities (14/14/13/13 split):** seat 0 = 28, seat 1 = 28, seat 2 = 26, seat 3 = 26
  tiles (`WallTileCapacity(seat) = WallStackCount(seat) * 2`; stacks = 14 for seats 0,1 and
  13 for seats 2,3). Sum = 108.
- **`WallOrdinalToSlot(ordinal)` is seat-major, column-ascending, layer-minor:**
  walk seat 0's whole wall (ordinals 0..27), then seat 1 (28..55), seat 2 (56..81),
  seat 3 (82..107). Within a seat, `col = localOrdinal / 2`, `layer = localOrdinal % 2`.
  So ordinals `2c, 2c+1` are the two stacked layers of column `c` (same x,y; different z),
  and advancing by 2 ordinals advances one column (one tile pitch in x within a seat).
- **`WallDealerOriginOrdinal(dealer)`** = cumulative capacity of lower-indexed seats
  (0 / 28 / 56 / 82 for dealer 0/1/2/3) — the ring origin of that seat's physical wall.

### Draw anchor (already implemented in the translator + BE-6 pickup)

For the remaining wall, tile `i` (front-relative) renders at

```
ordinal(i) = WallDealerOriginOrdinal(dealer) + BreakPoint.TileIndex + frontDrawn + i   (mod 108)
frontDrawn = 108 - state.Wall.Count - state.WallBackDrawn
```

Front draws consume the near (break) end; kong replacements (`WallBackDrawn`) consume the
far end; both leave one contiguous middle arc. **BE-6** exposes the exact endpoint slots for
the manual pickup ceremony (`pickup.nextTileSlots`, `[0]` = the exposed-end tile) using this
identical anchor, so the pickup endpoint and the rendered wall agree.

## 2. Required physical world order (frontend contract — Hicks)

Render `wall.{col}.{layer}@{seat}` so that **increasing render-ring ordinal is a single
counter-clockwise perimeter walk with each consecutive column exactly one tile pitch apart in
WORLD space, including across every corner**:

| Ordinals | Seat | Physical edge | Column direction (increasing col) | Corner seam into next seat |
|---|---|---|---|---|
| 0..27  | 0 | bottom | left → right (+X) | seat0 col13 meets seat1 col0 (bottom-right corner) |
| 28..55 | 1 | right  | bottom → top (rotate 90° CCW of +X) | seat1 col13 meets seat2 col0 (top-right corner) |
| 56..81 | 2 | top    | right → left (rotate 180°) | seat2 col12 meets seat3 col0 (top-left corner) |
| 82..107| 3 | left   | top → bottom (rotate 270°) | seat3 col12 meets seat0 col0 (bottom-left corner) |

Grounding from the current bundle (`setup-slots.ts`): wall START origin `(30, 20, 0)`;
`row(14|13)` lays columns along **+X**; `stack(2)` lays layers along **+Z**; `seats()` applies
`SEAT_ROTATIONS[seat]` = `seat · 90°` about **+Z** around table centre `(WORLD_SIZE/2, WORLD_SIZE/2)`
= `(87, 87)`.

### The corner-seam invariant (the actual fix)

For "one tile pitch across every corner" the **last column of seat N** must sit exactly one
tile pitch (in world space) from the **first column of seat N+1** at the shared corner. Because
seats 0,1 have 14 columns and seats 2,3 have 13, the four walls have **different lengths**;
centring each wall on its edge (the upstream default) leaves the observed ~52 px corner gaps and
the "seat3 full / seat1 empty scatter" that reads as broken. Hicks must set each seat wall's
origin/offset so the columns form a continuous ring (equal inter-column pitch across the seam),
NOT four independently-centred walls. The layer pair (`layer 0`/`layer 1`) is co-located in x,y
and stacked in z — the polyline advances one pitch **per column**, not per layer.

## 3. Acceptance (Hudson G4 — world-coordinate polyline, supersedes count proxies)

Derive each wall tile's WORLD position from the rendered slot geometry, order tiles by
authoritative draw ordinal, and assert consecutive **columns** are exactly one tile pitch apart
(tight tolerance) forming a single perimeter polyline **including all four corners** — no corner
gaps, no seat1-empty/seat3-full scatter. Parameterise over dealer × dice-sum. Per-seat counts or
slot names are INVALID proxies (`{0:10,1:0,2:12,3:26}` passes a naive per-seat check yet fails
physically).

## 4. What is frozen vs open

- **Frozen (Bishop):** `AutotableSlotMap.WallOrdinalToSlot` order + capacities + the draw
  anchor above; `pickup.nextTileSlots` (BE-6) uses the same anchor. Engine draw order unchanged.
- **Open (Hicks):** `setup-slots.ts` per-seat wall origin/offset so the composed world order
  satisfies §2/§3. If Hicks prefers to keep the current geometry and instead re-order the
  backend seat/column mapping, that is a joint change — coordinate before either side lands, and
  keep `pickup.nextTileSlots` + the wall emission using one shared mapping.
