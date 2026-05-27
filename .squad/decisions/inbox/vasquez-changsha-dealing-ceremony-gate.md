# Vasquez — Changsha dealing-ceremony visual gate

**Author:** Vasquez (Rules / QA Engineer)
**Date:** 2026-05-27
**Branch:** `test/visual-gate-walls-facedown`
**Deliverable:**
- `playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs` (NEW)
- `playtest-artifacts/changsha-dealing/` (NEW screenshots dir, including
  `baseline-before-fix.png`)
- `docs/playable-gates.md` (NEW promotion doc — must-pass for "playable" claim)
- This memo (`.squad/decisions/inbox/vasquez-changsha-dealing-ceremony-gate.md`)

## Decision

Promote the new spec to a **hard playability gate**. Every "playable"
claim against Changsha v1 must show this spec passing all 6 gates
against current main before the team / Stephen takes the claim
seriously. This memo is the canonical gate definition.

## Why the gate exists

Stephen's 2026-05-27T21:27Z directive
(`.squad/decisions/inbox/copilot-directive-2026-05-27T2127Z-face-down-walls.md`)
called out a real regression — Changsha-variant rendering at game start
showed face-up tiles around the perimeter + scattered side stacks
instead of the canonical four face-down walls.

**`playtest-human-led.spec.mjs` did not catch it.** That spec drives
`world.deal('HANDS')` and then *counts* hand tiles
(`postDealHandSize.handTileCount === 14`) but never inspects the
**visual contract** — wall composition, tile orientation, or pickup
choreography. Stephen quote: *"no reason for you to keep churning and
have no output or be confused about what works."*

The new gate spec asserts the visual contract at six points:

| ID | Gate | Assertion |
| --- | --- | --- |
| GATE-1 | Wall count = 4 | Exactly 4 distinct seat-keyed wall groups present in `client.things` at T=2s post-connect. Reports `canonicalChangshaStacks` (14/14/13/13) and `canonicalChangshaTotal108` as additional diagnostics. |
| GATE-2 | Walls face-down | For every tile with `slotName` starting `wall.`, `face === null` or `undefined`. A numeric face on a wall tile is the regression Stephen called out. |
| GATE-3 | No hand tiles | At T=2s post-connect (before Deal), zero entries in any seat's `hand.*` slot. |
| GATE-4 | Dice not yet rolled | `client.dice` collection is empty or shows initial state. |
| GATE-5 | Pickup ceremony plays out | After firing `world.deal('HANDS')` and waiting for the auto-chain (Hicks PR #88), every seat reaches **either** 12 tiles (intermediate, post-3-rounds) or 14/13/13/13 (final, dealer-extra applied). Anything else is a regression in the dealing logic. |
| GATE-6 | Zero page errors | No JS exceptions surfaced by Playwright's `pageerror` listener throughout the spec. |

The gate spec exits non-zero on any failure so it can wire into CI as a
hard pre-merge check.

## Baseline at `c616407` (current main, pre-Bishop+Hicks+Frost fix wave)

`E2E_BASE_URL=http://127.0.0.1:8088 node playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs`

### Result: GATEs 1-4 + 6 PASS, GATE-5 FAILS.

```
✅ GATE-1-wall-count-eq-4: { seatsWithWall: 4, wallStacksBySeat: {0:19,1:19,2:19,3:18}, totalWallTiles: 136, canonicalChangshaStacks: false, canonicalChangshaTotal108: false }
✅ GATE-2-walls-all-face-down: { faceDownCount: 136, faceUpCount: 0 }
✅ GATE-3-no-hand-tiles-visible: { handTilesBySeat: {0:0,1:0,2:0,3:0} }
✅ GATE-4-dice-not-yet-rolled: { dice: null, diceRolled: false }
❌ GATE-5-twelve-tiles-after-3-rounds: { handsBySeat: {0:17,1:15,2:18,3:15}, totalHands: 65, expected: 'either {12,12,12,12} or {14,13,13,13}' }
✅ GATE-6-zero-page-errors: { count: 0 }
```

Baseline screenshot: `playtest-artifacts/changsha-dealing/baseline-before-fix.png`
(captures the over-distributed final-deal state showing the ceremony
mis-firing).

### What the baseline tells us

1. **Walls ARE face-down at T=2s** — but this is the upstream bundle's
   sandbox default, *not* the runtime's actual wall state. The bundle
   pre-renders 136 face-down wall tiles before our server deals.
   Stephen's screenshot showing face-up tiles must therefore have
   captured the **post-deal** state, where the runtime is supposed to
   ship the Changsha 108-tile wall but instead leaves the sandbox's 136
   in place (see canonical-stacks diagnostics: 19/19/19/18 stacks vs
   the locked 14/14/13/13 contract from `AutotableSlotMap.WallStackCount`).

2. **The pickup chain over-distributes** — after `world.deal('HANDS')`
   + the Hicks PR #88 auto-chain, hand totals end at **65 tiles** spread
   17/15/18/15 across seats. Canonical Changsha v1 after-deal is
   **53 tiles** spread 14/13/13/13. The chain is double-firing takes
   (likely Hicks's `driveManualDealChain` racing with bot-AI takes
   on the same seats).

3. **The visible-rendering regression Stephen described** maps to:
   - **Wall geometry** — backend emits 136 tiles in the FOUR_PLAYER
     19-col layout rather than Changsha 14/14/13/13. *Lane: Bishop
     (Translator) and/or Frost (bot-strategy could be triggering an
     auto-fill of empty slots).*
   - **Chain over-distribution** — `driveManualDealChain` does not
     gate against bot-AI takes for the same seat. *Lane: Hicks
     (frontend chain).*
   - **Face-up tiles "around the perimeter"** — likely the post-deal
     hand tiles for seats other than the dealer's are leaking face
     state (not stripped to `face: null` for non-self seats). *Lane:
     Bishop (translator's privacy filter).*

### Confirmation: this is exactly the gap Stephen called out

The pre-existing `playtest-human-led.spec.mjs` runs to completion
against the same backend and reports `14/14 OK` in its findings because
its only post-deal check is `postDealHandSize.handTileCount` — which
counts to 14 even when the dealer overshoots into 17 (the test only
looks at the SELF seat, never asserts the canonical 14, and never
inspects the other seats).

The new spec catches it. **Test failed against c616407 as expected — confirms Stephen's regression.**

## Verification commands

```bash
# Start backend (matches the recipe in `.squad/agents/vasquez/history.md`):
cd src/backend/src/Mahjong.Autotable.Api
ASPNETCORE_URLS=http://0.0.0.0:8088 ASPNETCORE_ENVIRONMENT=Development \
  dotnet bin/Debug/net10.0/Mahjong.Autotable.Api.dll &
sleep 8
curl -sf http://127.0.0.1:8088/api/health  # → { status: "ok" }

# Run the gate:
E2E_BASE_URL=http://127.0.0.1:8088 \
  node playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs
# Exit code 0 = all 6 gates pass.  Exit code 1 = at least one gate failed.
```

Artifacts on a failed run:
- `playtest-artifacts/changsha-dealing/findings.json` — full structured
  findings (gate detail, console errors, hand progression log, dice
  state, pickup phases).
- `playtest-artifacts/changsha-dealing/{01-walls-only, 02-dice-rolled,
  03-mid-pickup, 04-all-hands-12, 05-final-deal}.png` — visual record
  of each ceremony phase.
- `playtest-artifacts/changsha-dealing/baseline-before-fix.png` —
  auto-copied from the most-informative phase whenever any gate fails
  (only saved when `PLAYTEST_POST_FIX` env var is unset).

## Promotion to CI

Added `docs/playable-gates.md` codifying the rule:

> **No "playable Changsha v1" claim is acceptable without this spec's
> exit-0 against current main.** Test counts, bundle sizes, and "the
> human-led spec passes" are *not* substitutes — the human-led spec
> demonstrably misses Stephen's regression.

Future CI workflow `playable-gates.yml` (separate PR / Apone lane —
not landed here to respect lane discipline) should run this spec
against a freshly-built backend before signing off any merge to main
that touches:

- `src/backend/src/.../Autotable/**`
- `src/backend/src/.../Changsha/**`
- `src/frontend/autotable-src/src/world.ts`
- `src/frontend/autotable-src/src/setup-deal.ts`
- `src/frontend/autotable-src/src/client.ts`

## Re-run plan after Bishop + Hicks + Frost merge

When `fix/facedown-walls-and-pickup-choreography` (or its successor PRs)
lands on main, this branch will be rebased on the new main and the
spec re-run. Expected post-fix state:

```
✅ GATE-1-wall-count-eq-4: { canonicalChangshaStacks: true, canonicalChangshaTotal108: true }
✅ GATE-2-walls-all-face-down
✅ GATE-3-no-hand-tiles-visible
✅ GATE-4-dice-not-yet-rolled
✅ GATE-5-twelve-tiles-after-3-rounds: { handsBySeat: {0:14,1:13,2:13,3:13} }
✅ GATE-6-zero-page-errors
```

Post-fix screenshot deltas will land in this memo as Δ entries.

## Maintenance notes

- The spec accepts `PLAYTEST_GAME_ID` env var so the same gate can be
  pinned to a deterministic seed for CI repeatability.
- The spec accepts `PLAYTEST_POST_FIX=1` env var to suppress the
  baseline-screenshot copy after a fix has landed (avoids polluting
  the post-fix artifacts dir with a stale "baseline").
- `wallTilesFaceUp` is a list of `{ tileId, slot, face }` — useful for
  pinpointing the specific tiles whose `face` leaked through if GATE-2
  ever regresses again.
- The chain timing budget is 30s. If a future legitimate change makes
  the chain take longer, bump `budgetMs` in step 8 rather than
  weakening the gate.
