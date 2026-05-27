# Bishop — Face-down walls (synth) + ceremony state-machine acceptance

**Author:** Bishop (Backend Dev)
**Date:** 2026-05-27
**Branch / PR:** squash-merged direct to `main` at `9ca96c3`
(feature branch `fix/walls-facedown-backend-translator-and-state-machine`,
deleted on push).
**Closes:** Stephen 2026-05-27 face-down-walls directive
(`.squad/decisions/inbox/copilot-directive-2026-05-27T2127Z-face-down-walls.md`).

## Decision

The translator (`ChangshaToAutotableTranslator.BuildThingEntries`) is the
single backend point of truth for tile placement on the wire. When the
authoritative `state.Wall` is empty AND no tiles have left it yet
(no hands, no melds, no discards) AND the phase is one of the two pre-deal
phases (`Seating`, `RollingDice`), the translator synthesizes a 108-tile
face-down wall using the canonical 14/14/13/13 `AutotableSlotMap` slots.
After `BeginManualDeal` materialises `state.Wall` (with the shuffled deck
rotated to the break point) the synthetic-fallback shuts off and the
authoritative wall flows through normally.

The Changsha manual-pickup state machine already existed (Phase F —
`BreakPointMarked` → `PickupRound1..3` → `SingleTilePickup` →
`DealerExtra` → `AwaitingDiscard`, hands ending at 14/13/13/13). This
PR pins it with a 15-test acceptance contract so future translator or
state-machine work cannot silently regress the ceremony.

## Why

Stephen's playtest at `?dealMode=manual` rendered TILE FACES at game
start, plus a messy non-canonical layout, plus the per-4 pickup ceremony
was visually unauthored. Two problems compounded:

1. The pwmarcz/autotable bundle ships a local 108-Changsha-tile scene
   whose default `dealType` is `HANDS`. At connect time, before the
   user clicks Deal, the bundle animates 14 tiles to the dealer's hand
   FACE-UP. The server snapshot did not override this because in
   `Seating` and `RollingDice` the authoritative `state.Wall` is empty
   (the shuffled wall is only materialised inside `BeginManualDeal`).
2. The translator was iterating `state.Wall` directly, so 0 wall tiles
   in those phases meant 0 `things` entries emitted, meaning the
   bundle's local pre-deal animation went unauthored.

The fix is a defense-in-depth backend authority layer that complements
Hicks's frontend fix at `4d9e3ce` (bundle-side: privacy-fallback rotation
coercion restricted to `hand` slots only, and local `DealType.INITIAL`
when `?dealMode=manual`). Even if the bundle is ever swapped out, or
late-joining spectators land on a different code path, the server-
authoritative snapshot now carries the correct face-down placement.

## What shipped

### Translator change
`src/backend/src/Mahjong.Autotable.Api/Autotable/ChangshaToAutotableTranslator.cs`

- New private static `ShouldSynthesizeWall(ChangshaGameState state)`
  returning true iff:
  - `state.Wall.Count == 0`
  - `state.Phase ∈ { Seating, RollingDice }`
  - `state.DiscardPile.Count == 0`
  - all `state.Hands[i].ConcealedTiles` empty and all `Melds` empty
- In `BuildThingEntries`, the `state.Wall` loop is replaced with
  `var wallTiles = ShouldSynthesizeWall(state) ? Enumerable.Range(0, AutotableSlotMap.TotalTiles) : (IEnumerable<int>)state.Wall;`
  before iterating into canonical wall slots at `WallRotFaceDown`.
- All other emit paths (hands, discards, melds, claim window) untouched.

### Acceptance tests
`src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/ManualDealCeremonyTests.cs`

15 `[Fact, Trait("Category", "ManualDealCeremony")]` cases:

**Translator — face-down wall emission:**
- `Seating_Emits_108_FaceDown_Wall_Things_NoHandSlots_NoDiscards`
- `RollingDice_Manual_BeforeRoll_StillRenders_108_FaceDown_Walls`
- `SyntheticWall_Uses_Canonical_14_14_13_13_Slot_Layout`
- `BreakPointMarked_RendersFullFaceDownWall_AfterBeginManualDeal`

**State machine — pickup ceremony:**
- `RollDice_Transitions_RollingDice_To_BreakPointMarked_WithBreakPointSet`
- `Pickup_Round1_FirstTake_AdvancesCursor_ToNextCcwSeat`
- `Pickup_Round1_Complete_Transitions_To_PickupRound2_DealerNext`
- `Pickup_Round3_Complete_Transitions_To_SingleTilePickup`
- `Pickup_SingleTileRound_Complete_Transitions_To_DealerExtra`
- `Pickup_DealerExtra_Complete_Transitions_To_AwaitingDiscard_14_13_13_13`
- `Pickup_WrongSeat_Throws_InvalidOperationException`
- `Pickup_WrongCount_Throws_InvalidOperationException`
- `Pickup_MidCeremony_Translator_RendersWallShrinking_HandsGrowing`

**Non-regression:**
- `AutoMode_FastDealPath_LandsAtAwaitingDiscard_With_14_13_13_13_NoPickupPhases`
- `EndHand_WithEmptyWall_DoesNotSynthesizeFaceDownWall`

## Verification

- Build clean (only pre-existing xUnit2002 warnings unrelated to this PR).
- New 15-test suite green (144 ms).
- Inside flock: branch → patch → build → targeted test → commit → push →
  squash-merge to main → push main → delete feature branch. Single
  atomic pipeline via `flock -w 180 9 9>.work/squad-git-lock`.

## Coordination notes

- **Hicks's frontend half** at `4d9e3ce` is complementary, not redundant:
  it fixes the bundle-internal privacy-rotation coercion and local
  `DealType` so the bundle behaves correctly even when the server
  snapshot is delayed by RTT. The backend fix here makes the server
  snapshot itself correct from the first frame, which is required for
  relay-mode spectators and any future client that doesn't apply
  Hicks's local-DealType switch.
- **Branch-name collision warning:** the original directive used
  `fix/walls-facedown-and-pickup-state-machine`. Hicks claimed that
  name for the frontend PR mid-flight, which clobbered Bishop's first
  attempt. This redo uses a more distinct
  `fix/walls-facedown-backend-translator-and-state-machine` and
  squash-merges direct so the namespace clears immediately. Suggest
  the Scribe rules add an agent-prefix to feature-branch naming.
- **Open observation:** Drake's `PlayerStats.LastGameAt` hotfix at
  `c369c54` left behind a working-tree edit to `DatabaseBootstrapper.cs`
  in some sessions. Confirmed it is NOT part of this PR — `git status`
  inside the flock showed only the two intended files staged.

## Follow-ups

- Frost's `Changsha/Dealing/` helper (per the directive) — still
  separately on Frost's queue; this PR does not block it.
- Optional WS smoke test: spin up the backend at
  `?variant=changsha&dealMode=manual&botCount=3&gameId=changsha-default`
  and dump the first `things` snapshot; expect 108 entries, all
  `slotName` starting `wall.`, all `rotationIndex == 0`. Not run as
  part of this PR — the 15 acceptance tests pin the translator output
  shape directly.
