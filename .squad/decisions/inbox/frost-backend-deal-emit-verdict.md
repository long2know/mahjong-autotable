# Frost — Backend Deal Emit Verdict (2026-05-29)

**Trigger:** Stephen Long surfaced "is the game working? The dealing seems very
whacky." Screenshot showed: (1) flat single-row wall strips at four corners,
(2) only ONE face-up tile in front of Seat 0, (3) gray triangular wedge
artifacts at four corners with white tile strips, (4) tiny floating Bot 1/2/3
+ Seat 0 label box dead-center.

**VERDICT: backend mixed — one real backend bug found and fixed; the other
visual symptoms are frontend (Hicks's lane).**

## Evidence — live WS capture (Stephen's URL params)

Probe: `node .work/frost-ws-probe.cjs` against the running backend on
`ws://127.0.0.1:8088/autotable/ws` with `?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&seat=0`,
then driving the same sequence the autotable bundle issues (`NEW` →
`UPDATE seats[0]={seat:0}` → `UPDATE match[0]={dealer:0,…}`).

**Post-deal snapshot (BEFORE fix):**

| Aspect | Observed | Expected | Verdict |
| --- | --- | --- | --- |
| Hand tiles per seat | 14 / 13 / 13 / 13 | 14 dealer + 13 ×3 | ✅ correct |
| Wall tile counts per seat | **28 / 27 / 0 / 0** | ~14 across all four | ❌ **BUG** |
| Wall layers (both 0 and 1?) | layer0=28, layer1=27 (only seats 0,1) | both layers all seats | ❌ tied to bug |
| Discard tiles before any discard | 0 | 0 | ✅ correct |
| Snapshot kinds shipped | match, seats, nicks, things, dice | same | ✅ no debug-label kinds |
| `seats` / `nicks` keys | bot-1/2/3 + viewer id | same | ✅ correct |

## Root cause

`ChangshaToAutotableTranslator.BuildThingEntries` packs the 55-tile post-deal
`state.Wall` into wall slots in the order yielded by
`AutotableSlotMap.EnumerateWallSlotsInOrder`. The enumerator was **seat-major,
col-secondary, layer-tertiary**:

```
seat 0 cols 0..13 (28 slots) → seat 1 cols 0..13 (28) → seat 2 cols 0..12 (26) → seat 3 cols 0..12 (26)
```

55 tiles fills the first 55 of those 108 slots ⇒ seats 0/1 walls are stuffed,
**seats 2/3 walls are physically empty**. The frontend renderer has nothing
to draw at the two opposite corners.

## Fix (this commit)

Reordered `AutotableSlotMap.EnumerateWallSlotsInOrder` to be **col-major
across seats, layer-tertiary**: for each `col ∈ 0..13`, yield (seat, col,
layer) for every seat that still has a stack at that col, both layers.

55 tiles now distribute as:
- cols 0..5 (6 × 4 seats × 2 layers = 48 tiles) — every seat 6 stacks full
- col 6 — seats 0/1 (4) + seat 2/3 partial (3) = 7 more tiles
- ~13-14 tiles per seat, all 2-high

Every seat keeps a visible 2-high wall silhouette after deal. The 108-tile
synthesized wall (Stephen's face-down-walls pre-deal directive) still fills
every slot, so the pre-deal visual is unchanged.

## Regression tests added (`AutotableTranslatorTests.cs`)

1. `Snapshot_AfterStartGame_WallTiles_DistributedAcrossAllFourSeats` — every
   seat has > 0 wall tiles, max−min ≤ 2 across the four seats.
2. `Snapshot_AfterStartGame_WallTiles_StackedTwoHighAtEverySeat` — for every
   seat both layer 0 and layer 1 are non-empty.
3. `Snapshot_AfterStartGame_NoPhantomDiscards_BeforeAnyDiscardEvent` — no
   `discard.X.Y@N` slots leak into the snapshot before the first discard.

All three pin the bug + the contract going forward. Pre-existing translator
tests (counts: 108 things / 53 hands / 55 walls, viewer-seat rotation, claim
deadline plumbing, etc.) all still pass.

## Test results

- `dotnet test --filter "FullyQualifiedName~AutotableTranslator|FullyQualifiedName~InitialDeal|FullyQualifiedName~ChangshaDealingCeremony"`
  → 110 passed, 0 failed (was 107 before; +3 new regression tests).
- Full suite: 5263 / 5267 passed, 2 skipped (bot simulations), 2 failed.
  Both failures are **pre-existing on baseline** (confirmed by `git stash` →
  re-run): `MultiGameRoutingTests.LateJoin_ToExistingGameId_…` and
  `VasquezW9SelfLaneTests.NightlyCronWorkflow_HasSchedule_AndRepoMode` —
  neither touches the translator nor the dealing ceremony.

## What's NOT a backend bug (hand-off to Hicks)

The other three Stephen symptoms remain frontend / scene-rendering issues:

1. **"Only ONE face-up tile in front of Seat 0"** — backend ships 14 hand
   entries for seat 0 with `rotationIndex: 1` (FACE_UP for the viewer seat).
   The WS dump confirms every slot from `hand.0@0` through `hand.13@0` is
   present in the latest snapshot. If only one is rendered, the bundle is
   dropping / hiding tiles client-side.
2. **"Gray triangular wedges + white tile strips at corners"** — no
   `discard.*` entries are shipped pre-discard; whatever the wedges are,
   they're not from a phantom discard pile. Likely scene-shell geometry.
3. **"Floating Bot 1/2/3 + Seat 0 label box dead-center"** — backend ships
   one `nicks` entry per seat with the player/bot id; layout positioning is
   purely frontend.

The wall-distribution fix in this memo will at minimum make all four wall
corners hold tiles again, which may also clear some of the corner artifacts
(symptom #3) if those wedges were the frontend's fallback for empty wall
groups.

## Files touched

- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableSlotMap.cs` —
  reordered `EnumerateWallSlotsInOrder` + expanded XML doc.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableTranslatorTests.cs` —
  3 new regression tests.
- `.squad/decisions/inbox/frost-backend-deal-emit-verdict.md` (this memo).
- `.squad/agents/frost/history.md` (update appended).

No frontend / Players / Auth / Data / WS endpoint plumbing touched.
