# Changsha wall perimeter mapping contract

**Scope:** The wall renders as a **single physical perimeter arc** that depletes from
the dice break point. This document records the existing final F1/F2 and SC4v4 contract;
it does not authorize a new wall layout or change the Changsha engine's draw order.
The rules engine and backend slot map share one fixed frame. The frontend realizes
that frame in world coordinates: ordinal contiguity alone does not prove physical
continuity across corners.

## 1. Authoritative ordinal → slot mapping (backend, frozen)

`BreakPointService` and `AutotableSlotMap` share this **seat-absolute** frame. The
wall sizes do not rotate with the dealer:

| Absolute seat | Stacks | Tile capacity | Render ordinals | Dealer origin |
|---|---|---|---|---|
| 0 | 14 | 28 | 0..27 | 0 |
| 1 | 14 | 28 | 28..55 | 28 |
| 2 | 13 | 26 | 56..81 | 56 |
| 3 | 13 | 26 | 82..107 | 82 |

The ring contains 54 stacks and 108 tiles. `WallDealerOriginOrdinal(dealer)` is the
cumulative capacity of lower-indexed seats: `[0, 28, 56, 82]`.

`WallOrdinalToSlot(ordinal)` reduces the ordinal modulo 108 and walks seat-major,
column-ascending order. Within the owning seat:

```text
col = localOrdinal / 2              (integer division)
layer = 1 - (localOrdinal % 2)
```

Even offsets address **layer 1 (top)**; odd offsets address **layer 0 (bottom)**.
Both layers share a column's x,y footprint and differ in z. Front draws remove the
top before the bottom. A bottom frontier is reachable only after its same-column
top is absent; reachability is **an empty up-link**, not simply `layer == 1`.

### Dice break and remaining-wall anchor

For dealer seats 0..3 and dice sums 2..12:

```text
breakWall = (dealer + diceSum - 1) % 4
col = StacksPerSeat[breakWall] - diceSum
breakTileIndex = tilesBeforeWall(dealer → breakWall) + 2 * col
```

`tilesBeforeWall` sums the fixed capacities counterclockwise from the dealer's wall,
excluding the break wall. `breakTileIndex` is `BreakPoint.TileIndex`: an index measured
from the dealer's wall, not a tile ID or a seat-0 render ordinal. The counted stack's
top is the first draw tile. The composed break ordinal is even, so the initial
frontier is always layer 1. There are **44** dealer/dice-sum cases, not 52.

For each remaining tile `state.Wall[i]`, where `i` is front-relative:

```text
dealerOrigin = WallDealerOriginOrdinal(dealer)
frontDrawn = 108 - state.Wall.Count - state.WallBackDrawn
anchor = (dealerOrigin + BreakPoint.TileIndex + frontDrawn) % 108
ordinal(i) = (anchor + i) % 108
```

Front draws consume the near end; kong replacements increment `WallBackDrawn` and
consume the far end. Both leave one contiguous middle arc. Each undrawn tile keeps
its physical slot rather than being repacked across seats. Before a dealt wall
exists, the translator can synthesize the full 108-slot face-down ring; that
display does not grant pickup authority.

### Manual pickup designation (SC4v4)

The translator computes the remaining wall's slots once and shares that result
between wall `things` and the pickup entry:

- **`pickup.targetSlots` contains exactly ONE public slot name**: the reachable
  frontier occupied by `state.Wall[0]`. It is the single actionable trigger even
  when the pickup count is four.
- A real press on that frontier sends a `take` payload of **`{seatIndex, count}`**.
  The server selects the first `count` wall tiles; the client does not select tile
  IDs or send a batch of target slots.
- **`batchPreviewSlots` is inert**, display-only information for the batch. Neither
  another preview tile nor a covered bottom tile becomes an actionable target.
- The client affordance fails closed for missing, empty, multiple, wrong or
  unreachable targets. The server validates the pickup actor and count; Auto mode
  and post-ceremony wall input stay inert. Human pickup requires an actual press,
  not an automatic take.
- **`targetHandles` is not a pickup field.** Opaque per-viewer identities belong to
  hidden `things`, not the public pickup designation. Do not restore `nextTileSlots`
  or raw tile IDs in the pickup signal.
- Leaving the ceremony emits **`pickup['current'] = null`**. A new manual hand
  repeats the ceremony with its current dealer; stale pickup cues cannot survive
  the prior hand or make the ordinary draw wall interactive.

## 2. Required physical world order (frontend contract, frozen)

Render `wall.{col}.{layer}@{seat}` so increasing ring ordinal follows one
counterclockwise perimeter. Consecutive columns on a straight side are one tile
pitch apart. **Corners are allowed: the existing corner-seam bound is at most
1.6 × tile pitch, not exactly one pitch.**

| Ordinals | Seat | Physical edge | Column direction (increasing col) | Corner seam into next seat |
|---|---|---|---|---|
| 0..27  | 0 | bottom | left → right (+X) | seat0 col13 meets seat1 col0 (bottom-right corner) |
| 28..55 | 1 | right  | bottom → top (rotate 90° CCW of +X) | seat1 col13 meets seat2 col0 (top-right corner) |
| 56..81 | 2 | top    | right → left (rotate 180°) | seat2 col12 meets seat3 col0 (top-left corner) |
| 82..107 | 3 | left   | top → bottom (rotate 270°) | seat3 col12 meets seat0 col0 (bottom-left corner) |

Measure world positions from the real slot geometry, not an assumed legacy wall
origin or screen-space distances. The layer pair is co-located in x,y and stacked
in z, so the perimeter polyline advances **per column**, not per layer.

Changsha wall slots now use the existing zero `direction` vector to make their
`origin.x/y` the rendered tile centre. This is an explicit reference-point
correction: the previous corner-origin coordinates were not mesh centres and
could pass a short-origin-seam check while the real tile bodies intersected.
Other slot groups and every relay variant retain their existing anchoring.

With pitch `p = 6`, tile depth `d = 9`, and mean wall length `n = 13.5` stacks,
the centre start is derived from `run = (n - 1) * p = 75` and
`contact = (p + d) / 2 = 7.5`:
`((174 - run + contact) / 2, (174 - run - contact) / 2) = (53.25, 45.75)`.
The four seat rotations produce corner vectors with longitudinal component
`+/-3` and transverse component `7.5`. Both origin and actual mesh-centre
seams are therefore `sqrt(3^2 + 7.5^2) = 8.077747211`, below the unchanged
`9.6` bound. The contact offset separates adjacent perpendicular wall bodies
instead of intersecting them. The original model is not shrunk.

### Corner-seam invariant

The last column of seat N joins the first column of seat `(N+1)%4` at a shared
corner. The static geometry contract bounds their world x,y distance by
`1.6 * PITCH` (`PITCH = 6`, bound `9.6` world units). Four straight sides must form
one non-degenerate counterclockwise ring without overlapping hands or discard
trays. The unequal 14/14/13/13 wall lengths do not justify four detached walls,
but they also do not require identical corner and straight-side distances.
Actual same-tier tile bodies must not penetrate one another. The real GLB has
nominal dimensions `6 x 9 x 4`; its stored floating-point thickness differs
from `4` by less than `0.000001`. Geometry checks use `0.00001` world units
only to distinguish contact/round-off from penetration, not to relax `9.6`.

## 3. Acceptance and existing oracles

These checks complement one another; co-emission alone cannot detect a shared
mapping error, and a slot-name or per-seat-count check cannot prove world geometry.

| Contract | Existing evidence |
|---|---|
| F1 fixed frame and break anchor | `SlotMapWallGoldenTests`: all 44 rules-owned literal anchor triples, 108-slot bijection and frame consistency. |
| F2 front reachability | `SlotMapWallGoldenTests`: every front-depletion position and all 17 ceremony pickup designations for each dealer/dice-sum case, including reachable bottoms at `frontDrawn` 49 and 51. |
| SC3 independent emission | `FrostSc3ReachableWall0LiteralTests`: dealer 0, dice sum 5, seed 42, break index 18; 17 literal frontier slots beginning with `wall.9.1@0`. Consumed tops are absent at 49/51; covered bottoms remain present but are not targets at 48/50. |
| SC4v4 designation and lifecycle | `BishopUatPickupTargetSlotsTests`: single co-derived target, inert batch preview, no pickup IDs/handles, explicit tombstone. `changsha-manual-pickup-endpoint-only.spec.ts` supplies real-pointer and fail-closed interaction coverage. |
| Physical perimeter | `wall-corner-ring.contract.spec.ts`: real slot geometry, four seams ≤1.6× pitch, counterclockwise winding and non-overlap. `changsha-wall-worldcoord-polyline.spec.ts`: live rendered remaining-wall continuity, with corners allowed. Exact-one-pitch corner checks are diagnostic only. |
| Actual tile bodies / added stack | `tests/node/changsha-physical-layout.test.mjs` loads the original GLB through Three's GLTFLoader, executes production slot/ThingGroup transforms, and checks actual body separation, both corner reference points, all 16 added-Kong positions, all 108 bootstrap wall bodies, and unchanged relay/ordinary-meld geometry. No browser/image acceptance is implied by this CPU test. |

The browser sweep does not replace the backend's full 44-case dealer/dice coverage.
Likewise, a passing current oracle is not evidence that a historical mutation run
or reviewer signature was recovered; run-specific proof and provenance stay separate.

## 4. Change boundaries

The fixed frame, top-first front mapping, break/depletion anchor, single-slot pickup
designation and current world geometry are frozen together. Changes require joint
rules/backend/frontend review; do not reorder the backend to satisfy superseded
prose or treat geometry as an open implementation task. This contract does not
change non-Changsha relay variants or enable deferred preset/house-rule behavior.

## Related reference

- [Canonical rules §2.4.1](rules/changsha-spec.md#241-canonical-wall-index-mapping-pinned).
