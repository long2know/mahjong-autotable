# Bishop — Phase J Wave 1 hardening memo

**Branch:** `stlong/phase-j-wave-1-hardening`
**Primary commit:** `361d805` (`feat(bot): Phase J Wave 1 — wire MinShantenToHu into HardStrategy claim evaluator`)
**Baseline gate:** 402/0/0 (existing suite, no Vasquez additions yet).

## What I shipped (Task 1 — PRIMARY)

`HardStrategy.DecideClaimPhase` now treats `HandEvaluator.MinShantenToHu` as
the **claim acceptance gate** for non-Hu opportunities, not just a discard
tie-breaker. Behavioral contract:

1. **Hu is unconditional.** Any opportunity with `ClaimType == Hu` is
   accepted immediately. The shanten gate would also accept Hu (post-Hu
   shanten is 0 by definition) but treating it as a fast-path keeps the
   gate code free of special cases and avoids an unnecessary simulation.
2. **Non-Hu claims (Pung / ExposedKong / Chow) require a strict shanten
   drop.** For each candidate the strategy simulates the post-claim hand
   (matching tiles removed from concealed list + extra placeholder meld
   appended) and re-runs `MinShantenToHu`. Claims where post-claim
   shanten ≥ pre-claim shanten are refused — `BotAction.Pass()`.
3. **Tie-breaker among accepted non-Hu claims:** Hu > Kong > Pung > Chow.
   This matches `ChangshaClaimPriority.TierOf` ordering with an explicit
   Kong-over-Pung lift (both share tier 2 in the resolver because the
   adjudicator breaks Kong/Pung ties by CCW seat distance, but a bot's
   acceptance gate has no such constraint — when both shanten-drop, Kong
   commits more structure and is the safer choice).
4. **Chow simulation mirrors the runtime.** Bots return
   `BotAction.Claim(Chow)` with no explicit tile IDs, so
   `ChangshaGameStateMachine.RemoveChowTilesByLowestPattern` selects the
   first viable chow shape in lowest-rank-first order
   `(rank-2,rank-1) → (rank-1,rank+1) → (rank+1,rank+2)`. The gate uses
   the same walk and the same first-viable-pattern selection, so the
   shanten-drop check reflects the chow shape that will actually be
   played — not an idealised best case.

### Helpers added in `HardStrategy.cs`

All private static, all live alongside the existing
`ShantenAfterDiscardingLogical` helper Phase I Wave 4 introduced:

- `ClaimAcceptanceRank(TableClaimType)` — Hu=4, Kong=3, Pung=2, Chow=1.
- `ShantenAfterPungClaim(hand, discardLogical, discardTileId)`
- `ShantenAfterExposedKongClaim(hand, discardLogical, discardTileId)`
- `ShantenAfterChowClaim(hand, discardLogical, discardTileId)`
- `TryRemoveByLogical(tiles, logical, count)` — in-place clone-list mutate.
- `ProbeShantenWithExtraMeld(hand, concealedAfter, kind, discardTileId)` —
  builds the throwaway probe `ChangshaHandState` and calls
  `HandEvaluator.MinShantenToHu`. Only `Melds.Count` and `ConcealedTiles`
  influence the counter, so the placeholder meld is content-free apart
  from carrying the discard tile for traceability.

`HandEvaluator.cs` was left untouched — no helpers were promoted there.

### Class-level docstring

The Phase F bullet "Claims Hu/Kong/Pung greedily like Medium; claims Chow
only when the bot has fewer than 2 melds AND the chow doesn't open a
winning tile to the opponents" has been replaced with "Claims Hu
unconditionally. Pung/Kong/Chow are gated on a strict shanten drop". A new
multi-paragraph Phase J Wave 1 note explains the wiring, the
unconditional-Hu fast-path, the tie-breaker ordering, and the
chow-simulation-mirrors-runtime contract.

### Why this matters

The pre-Wave-1 `DecideClaimPhase` was a textbook example of the same
"dead code" pattern Phase I Wave 4 fixed for `SelectDiscardTile`: a
rigorous shanten counter existed but was never consulted at the claim
gate. The most visible regression in the legacy heuristic was the Phase F
fussy-chow rule (`hand.Melds.Count < 2 && CountLooseTiles(hand) <= 3`) —
this correctly refused chows when the bot was meld-heavy, but it had no
shanten-awareness and could accept a chow that broke an existing pair-
or pung-partial. With the shanten gate, the bot will only take a chow
when the resulting hand has strictly fewer required tile-swaps than the
current hand. Same logic kills the Phase F "always take Pung/Kong"
greed: a Pung that destroys a complete chow draw is now refused.

## What I deferred (Task 2 — SECONDARY)

**Task 2 (wall-exhaustion fast-path) is deferred / no-op.** The premise
of the task — "the runtime currently transitions to `WallExhausted`
only AFTER attempting a draw on an empty wall" — does not hold in the
current codebase. `ChangshaGameStateMachine.AdvanceToNextPlayer`
(line 1076–1087) already checks `state.Wall.Count == 0` and transitions
straight to `WallExhausted` before `AwaitingDiscard` is ever set:

```csharp
private static void AdvanceToNextPlayer(ChangshaGameState state, int currentSeatIndex)
{
    state.ActiveSeatIndex = (currentSeatIndex + 1) % SeatCount;
    if (state.Wall.Count == 0)
    {
        state.Phase = ChangshaPhase.WallExhausted;
    }
    else
    {
        state.Phase = ChangshaPhase.AwaitingDiscard;
    }
}
```

Both call sites of `AdvanceToNextPlayer` — `Discard` (line 451, post-
discard with no claim window opened) and `PassClaim` (line 591, every
seat passed on a discard claim) — therefore route directly to
`WallExhausted` when the wall is empty, never through `AwaitingDiscard`.
The Kong-replacement path in `ResolveClaim` (line 547–566) has its own
explicit empty-wall guard before drawing from the back.

Consequently `ChangshaGameRuntime.DriveAfterAdvanceAsync` (line 717) only
ever enters its `DrawTile`-on-`AwaitingDiscard` branch when the wall is
non-empty; the `else if (Phase == WallExhausted)` arm at line 732
catches the fast-path cases from the state machine and dispatches
`HandleWallExhaustedAsync` without a no-op `DrawTile` call.

Adding an additional `if (Wall.Count == 0)` short-circuit in
`DriveAfterAdvanceAsync` would be functionally inert (the state machine
already short-circuits) and would risk dropping the defensive
`wall-exhausted` event that `DrawTile` emits for paths that *do* land on
an empty wall (currently impossible via the public API, but worth
preserving as belt-and-braces). Per the wave brief's acceptance criterion
("if you can't make the change cleanly without risk to existing test
behaviour, SKIP THIS TASK"), I'm deferring it. No state-machine touch
required; nothing in `ChangshaStateMachine.cs` needs to change.

## Surprises

- `Meld.TileIds` is required, but `HandEvaluator.MinShantenToHu` only
  reads `Melds.Count` — so the probe meld in `ProbeShantenWithExtraMeld`
  can be content-free (just the discard tile, for traceability). Verified
  by inspection of `DecomposeStandard` (uses `groupsNeeded = 4 -
  meldsDeclared`) and `ComputeSevenPairsShanten` (early-returns
  `int.MaxValue` when `meldsDeclared > 0`).
- The `CountLooseTiles` helper from Phase F is still used in the legacy
  OnTurnStart/OnSelfDraw kong gates (line 60, 89). I deliberately did not
  refactor those to use the shanten gate — they're for *self-declared*
  kongs (concealed / added) rather than claim-acceptance, and the wave
  brief scope was specifically the claim evaluator. A future wave could
  promote those gates similarly, but not this one.

## Coordination handoff to Vasquez (Wave 1 test owner)

The shanten claim gate is **strictly stricter** than the prior heuristic
for non-Hu claims — every Pung/Kong/Chow the old strategy would have
accepted but that doesn't drop shanten is now refused. Tests that
relied on Hard taking a specific Pung/Chow regardless of hand shape
*may* need a small data tweak; if any such test surfaces in your wave,
the fix is usually to construct a fixture where the claim provably
drops shanten (e.g., bot has the matching pair + a complete pair as
head + 3 chow partials; accepting Pung locks the meld and shanten
drops by 2).

Suggested new tests (none added in this commit per branch ownership
rules):

- `HardStrategy_RefusesPungWhenShantenUnchanged` — bot has 2 of a
  rank-1 tile but no other structure; discarded rank-1 would form a
  meld but `MinShantenToHu` returns the same value either way.
- `HardStrategy_AcceptsPungWhenShantenDrops` — bot has the matching
  pair plus a pair head and 3 chow partials; claim takes shanten
  from N to N-1.
- `HardStrategy_AcceptsHuRegardlessOfShantenGate` — adversarial
  fixture where shanten check would refuse but Hu must still win.
- `HardStrategy_PrefersKongOverPungWhenBothDropShanten` — same
  rank, both opportunities surfaced, bot picks Kong.
- `HardStrategy_PrefersChowOnlyAsLastResort` — Chow + Pung both
  drop shanten; bot picks Pung per tie-breaker.

**Memo:** this file.
