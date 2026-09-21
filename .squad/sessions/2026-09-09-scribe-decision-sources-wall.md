# September 9, 2026 — authored inbox source archive (wall/reviewer provenance)

Runtime-owned preservation by Scribe. These are HISTORICAL source records, not September 9 execution or blanket author clearance. Original authored text follows each source label. Current rules evidence is `sessions/2026-09-09-vasquez-rules.md`; exact current lockouts are `sessions/2026-09-09-reviewer-lockouts.md`. The 44-case frame and top-first contract remain active; the historical combined SC3 physical mutation/signature record is still incomplete. Referencing a historical path below grants no permission to access it.

## Source: decisions/inbox/Frost-SC3-literal-oracle-verified-test-pending.md


---
## 2026-08-07T16:03 — GREEN independently re-read on the LANDED fixture; physical F2-flip RED routed
FrostSc3ReachableWall0LiteralTests.cs now exists (integration tree, mtime 14:56, 7944 bytes). I
re-read the GREEN side ON THE FILE (not a summary) and it matches the sketch + Vasquez's own read:
- Deal-pin L105 `Assert.Equal(18, state.BreakPoint!.Value.TileIndex)` after ManualDealAt(0,5,seed42).
- Anti-circular first assert L122 `Assert.Equal(expected, target0)` — RHS literal (dict L34 `[0]="wall.9.1@0"`), LHS real `Translate(state, viewerSeat:picker)` emission (L116-117).
- Explicit split: L127-128 `NotEqual`+`Assert.DoesNotContain(drawnTop, wallSlots)` (consumed top absent); L134-135 `NotEqual`+`Assert.Contains(occludedBottom, wallSlots)` (occluded bottom present). NOT collapsed to bare predicate.
- Non-vacuity: fdAsserted appended post-assert L138; L150 `Assert.Equal(Keys, fdAsserted)`; L151 `count==17`; L152-153 `Contains(49)/Contains(51)`; L154 `Phase==AwaitingDiscard`.
Vasquez independently verified the same on its own read and EMITTED the SC-3 (a) SIGN (2026-08-07).

REMAINING for the COMBINED (a)+(b) sign = Vasquez criterion #1: a PHYSICAL F2-flip mutation-RED on
THIS fixture (not the vacuous feature-absent @200cad4 base-RED). Exact mutation (integration tree):
AutotableSlotMap.cs L169 `return (seat, o / 2, 1 - (o % 2));` -> `return (seat, o / 2, (o % 2));`
(flip ONLY the layer; keep col o/2). Expected RED, first failure at fd0 L122:
Assert.Equal Expected "wall.9.1@0" Actual "wall.9.0@0". Then revert; git diff clean; re-run GREEN.
DISCIPLINE: I am read-only AND cannot write /tmp (the fixture + AutotableSlotMap live only in
/tmp/mahjong-autotable-uat-integration) -> I physically cannot run the mutation. Routed the physical
RED to hudson-2 (the fixture's author; re-creating the pre-fix stock bug IS its RED-baseline charter).
On hudson-2's verbatim RED log (fd0 Assert.Equal wall.9.1@0 vs wall.9.0@0) + revert-clean + GREEN-
after-revert, I verify read-only and Vasquez's combined (a)+(b) sign is automatic per pre-auth.

## Source: decisions/inbox/bishop-F1-frame-declaration-PA-PB.md

# F1 canonical frame declaration — P-A + P-B (Bishop → Vasquez)

**Purpose:** formally declare the two frame primitives Vasquez needs to build her
**independent** dice-anchor oracle, and reconcile the frozen slotmap evidence tree
(`/data/source/mahjong-autotable-uat-backend-slotmap`) against my canonical worktree
(`/tmp/mahjong-autotable-uat-backend`, base `200cad4`). Read-only extraction; the frozen
tree's `AutotableSlotMap`/golden were **not** merged wholesale.

## P-A — stacks per ABSOLUTE seat (the 14/14/13/13 frame)

| Seat (absolute) | Stacks | Tiles |
|---|---|---|
| 0 | 14 | 28 |
| 1 | 14 | 28 |
| 2 | 13 | 26 |
| 3 | 13 | 26 |

- Total = 54 stacks / **108 tiles**.
- **Seat-absolute, NOT dealer-relative.** Which walls are 14 vs 13 is fixed by seat index,
  never rotated by the dealer. (The superseded engine frame was dealer-relative
  `[14,13,14,13]` — dealer/opposite always 14 — which drifted the anchor a stack for many
  dealer×dice combinations.)
- Grounded in the frontend bundle geometry: seats 2/3 only define 13 wall columns.
- Single source: `AutotableSlotMap.WallStackCount(seat)` (`0,1→14; 2,3→13`) ≡
  `BreakPointService.StacksPerSeat = [14,14,13,13]`. One frame, two files, no divergence.

## P-B — column indexing + draw direction + layer

1. **Seat-major perimeter walk.** Render ordinals: `0..27`=seat0, `28..55`=seat1,
   `56..81`=seat2, `82..107`=seat3. `WallDealerOriginOrdinal(dealer)` = Σ capacities of
   lower-indexed seats = `0 / 28 / 56 / 82` for dealer `0/1/2/3`.
2. **Columns ascending within a seat (col-major):** `col = seatLocalOffset / 2`,
   range `[0, WallStackCount(seat)-1]`. Advancing 2 ordinals = +1 column (one tile pitch).
3. **Layer = TOP-first (F2):** `layer = 1 - (seatLocalOffset % 2)`. Even offset → **layer 1
   (exposed top)**; odd → layer 0 (occluded bottom). Depletion is top-before-bottom.
4. **Break-point rule (§2, `BreakPointService`):**
   - Wall selection counterclockwise from dealer: `wallIndex = (dealer + (diceSum-1) % 4) % 4`
     (1=dealer, 2=right, 3=opposite, 4=left).
   - Count `diceSum` stacks from the **RIGHT** end of the selected wall:
     `col = WallStackCount(wallIndex) - diceSum`.
   - `TileIndex = tilesBeforeWall(dealer→wallIndex, seat-absolute sizes) + col*2`
     (flat index measured from the dealer's wall, counterclockwise).
   - Render composition: `WallOrdinalToSlot(WallDealerOriginOrdinal(dealer) + TileIndex)`
     resolves to `(seat = wallIndex, col = WallStackCount(seat) - diceSum, layer = 1)`.
5. **Break ordinal is provably EVEN** (all seat capacities even ⇒ `tilesBeforeWall` even;
   `col*2` even) ⇒ the frontier is always **layer 1 (reachable top)**. This is what makes
   F2 (top-first) and F1 (anchor) compose without an occluded front.

## Case count: **44**, not 52

The parameterization is `dealer 0..3 × diceSum 2..12` = 4 × 11 = **44** cases.
The frozen tree's golden comment says "52 reachable" — that is an arithmetic error (its own
`DealerDiceCases` generator yields 44, identical to canonical). **Please build the oracle
over 44 rows.** If your "52-case" framing counts a different axis (e.g. die-pair outcomes,
or sums 2..14), flag it and we reconcile before sign-off.

## Independent oracle for your confirmation (44 rows)

Derived purely from P-A/P-B above (`B=(dealer+sum-1)%4; col=Stacks[B]-sum; layer=1`) — NOT
read back from the production mapping. Please confirm each `(seat,col,layer)` against your
own §2 derivation, or return diffs:

```
dealer0: (0,2)->(1,12,1) (0,3)->(2,10,1) (0,4)->(3,9,1) (0,5)->(0,9,1) (0,6)->(1,8,1) (0,7)->(2,6,1) (0,8)->(3,5,1) (0,9)->(0,5,1) (0,10)->(1,4,1) (0,11)->(2,2,1) (0,12)->(3,1,1)
dealer1: (1,2)->(2,11,1) (1,3)->(3,10,1) (1,4)->(0,10,1) (1,5)->(1,9,1) (1,6)->(2,7,1) (1,7)->(3,6,1) (1,8)->(0,6,1) (1,9)->(1,5,1) (1,10)->(2,3,1) (1,11)->(3,2,1) (1,12)->(0,2,1)
dealer2: (2,2)->(3,11,1) (2,3)->(0,11,1) (2,4)->(1,10,1) (2,5)->(2,8,1) (2,6)->(3,7,1) (2,7)->(0,7,1) (2,8)->(1,6,1) (2,9)->(2,4,1) (2,10)->(3,3,1) (2,11)->(0,3,1) (2,12)->(1,2,1)
dealer3: (3,2)->(0,12,1) (3,3)->(1,11,1) (3,4)->(2,9,1) (3,5)->(3,8,1) (3,6)->(0,8,1) (3,7)->(1,7,1) (3,8)->(2,5,1) (3,9)->(3,4,1) (3,10)->(0,4,1) (3,11)->(1,3,1) (3,12)->(2,1,1)
```

## Frozen-tree reconciliation (what I took vs left)

- **BreakPointService F1 proposal:** byte-identical to my canonical — already integrated. ✓
- **AutotableSlotMap F2:** byte-identical to my canonical (top-first `1-(o%2)`) — **no conflict
  remains**; the helper top-first primitive you approved is what canonical carries. Not re-merged.
- **SlotMapWallGoldenTests:** **NOT merged wholesale.** Frozen version leaves
  `ExpectedAnchorOracle` deliberately EMPTY (correct: "must not fabricate a self-satisfying
  oracle") and mislabels the count "52". My canonical golden is the superset: same F2
  reachability invariants + the **populated 44-row oracle** bound via
  `BreakAnchor_MatchesRulesOwnedOracle_WhenProvided` (a dealer-rotating frame fails ≥16 rows —
  the gate is non-vacuous).

## Verification

Canonical `SlotMapWallGoldenTests`: **223 passed / 0 failed** (F1 anchor + F2 reachability +
108-slot bijection + one-frame consistency). Full suite 5096/0/2.

**Ask:** confirm P-A + P-B and the 44-row oracle (or send diffs / the 44-vs-52 reconciliation)
so the F1 binding is to YOUR independent oracle and your combined SC-3 (F1 anchor + F2
production golden) can close.

## Source: decisions/inbox/ripley-wall2-paste-lane-gap-proxy-stale.md

# Ripley — Wall #2 paste: real gap is LANE-only (a)+(b); (c) proxy is STALE/gone; (d) done. Integration is correct.

**When:** 2026-08-07T16:15 PT · **Author:** ripley-2 (read-only) · **Trigger:** Frost's routing nudge — "route the paste to Bishop-1; ping me when the merged worktree exists." Verified read-only across all backend worktrees. Frost's nudge is VALID for the owning lane but the merged assembly (integration) is already correct.

## Worktree topology (verified via `git worktree list`)
- `/tmp/mahjong-autotable-uat-integration` — MERGED assembly = :18084 build source. HEAD=200cad4 + uncommitted edits.
- `/data/source/mahjong-autotable-uat-backend-slotmap` — OWNING lane for slotmap code + the new golden. HEAD=200cad4.
- `/tmp/mahjong-autotable-uat-backend` — older backend base (has SC4Final pickup test, pre-v4).
- `/data/source/mahjong-autotable-uat-backend-sc2` — privacy/SC-2 lane. `/tmp/mahjong-autotable-uat-backend-helpers` — Frost's bot-harness lane.

## Per-item state (a)-(d): the real gap is LANE-only, (a)+(b)
| Item | Slotmap LANE | Integration (merged) | Verdict |
|---|---|---|---|
| (a) 44-triple paste | **EMPTY** — `ExpectedAnchorOracle = new Dictionary<…>();` @ lane `:359` | POPULATED @ `:387-393` + `Assert.Equal(44):400` | **REAL gap in lane** |
| (b) docstring | `:28` "All **52** reachable…" (wrong; 4×11=44) | `:28` "All **44** reachable…" | **REAL gap in lane** |
| (c) self-ref proxy delete | (file `BishopUatBackendContractsTests.cs` ABSENT in lane) | real SC-4 contracts, **no proxy** anywhere | **STALE ref — already gone/N-A** |
| (d) slotmap code merge | `AutotableSlotMap 1-(o%2)` + `BreakPointService [14,14,13,13]` ✓ | same ✓ | **DONE** |

- (c): swept BOTH base + integration test trees for `Assert.True(true)` / self-referential / vacuous-anchor / tautological patterns → NONE. The only `WallOrdinalToSlot(BreakOrdinal…)` uses are the legitimate NON-vacuous golden assertions (`:49/:227/:379` etc.). Frost's `:144-148 proxy` points at what is now the real SC-4 pickup contract (`SC4Final_…` in base, evolved to `SC4v4_…` + new `R1E3_…tombstone` in integration). Stale line-ref from Frost's pre-merge checkout.
- The slotmap-lane golden is STRUCTURALLY integration's golden minus the paste (lane 389 lines vs integration 424; sole population diff = the empty-vs-filled dictionary).

## DURABILITY RISK (why the lane backfill still matters)
Integration's paste is an INTEGRATOR-APPLIED edit; the OWNING slotmap lane still has the empty oracle + "52" docstring. If the integrator re-assembles integration's golden FROM the lane, integration REGRESSES (loses the 44 paste → vacuous test; reintroduces "52"). So (a)+(b) must land in the owning lane for durability, and the integrator must not overwrite integration's golden from the stale lane meanwhile.

## Exact verbatim paste block (integration `:387-393`, == Vasquez-signed 44/44)
```
        ExpectedAnchorOracle = new Dictionary<(int Dealer, int DiceSum), (int Seat, int Col, int Layer)>
        {
            [(0,2)]=(1,12,1), [(0,3)]=(2,10,1), [(0,4)]=(3,9,1), [(0,5)]=(0,9,1), [(0,6)]=(1,8,1), [(0,7)]=(2,6,1), [(0,8)]=(3,5,1), [(0,9)]=(0,5,1), [(0,10)]=(1,4,1), [(0,11)]=(2,2,1), [(0,12)]=(3,1,1),
            [(1,2)]=(2,11,1), [(1,3)]=(3,10,1), [(1,4)]=(0,10,1), [(1,5)]=(1,9,1), [(1,6)]=(2,7,1), [(1,7)]=(3,6,1), [(1,8)]=(0,6,1), [(1,9)]=(1,5,1), [(1,10)]=(2,3,1), [(1,11)]=(3,2,1), [(1,12)]=(0,2,1),
            [(2,2)]=(3,11,1), [(2,3)]=(0,11,1), [(2,4)]=(1,10,1), [(2,5)]=(2,8,1), [(2,6)]=(3,7,1), [(2,7)]=(0,7,1), [(2,8)]=(1,6,1), [(2,9)]=(2,4,1), [(2,10)]=(3,3,1), [(2,11)]=(0,3,1), [(2,12)]=(1,2,1),
            [(3,2)]=(0,12,1), [(3,3)]=(1,11,1), [(3,4)]=(2,9,1), [(3,5)]=(3,8,1), [(3,6)]=(0,8,1), [(3,7)]=(1,7,1), [(3,8)]=(2,5,1), [(3,9)]=(3,4,1), [(3,10)]=(0,4,1), [(3,11)]=(1,3,1), [(3,12)]=(2,1,1),
        };
```

## Routing
- **Bishop-1** (author): backfill the slotmap-lane golden — (a) replace the empty `new Dictionary<…>();` at `:359` with the block above; (b) fix `:28` "52"→"44". (c) is stale/no-op (confirm, don't hunt); (d) already correct in-lane. This makes the owning source == integration and durable.
- **Frost-1** (gate): merged worktree = integration; it satisfies your 4 ping conditions (slotmap files ✓ + oracle paste ✓ + proxy-gone ✓ + #4 ✓). Run RV-2 there now, OR against the lane after Bishop's backfill. (c) is a stale line-ref (no proxy exists); (d) done.
- **Integrator (ripley-3/apone-6):** protect integration's golden paste — do NOT rebuild integration's golden from the empty-oracle slotmap lane until Bishop backfills, else you revert (a)+(b).
- G19 (raw HMAC in ChangshaPrivacyProjector) remains Frost's separate BLOCKING gate — tracked elsewhere, unaffected.
