# September 9, 2026 — authored inbox source archive (renderer/runtime history)

Runtime-owned preservation by Scribe. Original August 11 texts follow each source label; their historical red/green claims remain tied to the original targets. Dietrich's later August attribution resolves Drake's preceding hand-2-stall diagnosis; neither proves September 9 browser acceptance. Current outcomes are in `sessions/2026-09-09-dietrich-renderer.md` and `sessions/2026-09-09-current-focus.md`. Historical write/routing instructions below do not reauthorize any locked-out author.

## Source: decisions/inbox/dietrich-hidden-park-slot-root-fix.md

# Dietrich — renderer invariant: off-table hidden park slot (root fix)

**By:** Dietrich (Frontend Renderer Engineer) — 2026-08-11
**Scope:** `src/frontend/autotable-src/src/{setup,slot,world}.ts` + 2 NEW renderer tests. Unstaged. Generated dist NOT touched.

## Root cause (reproduced, not inferred)

`World.ensureHiddenBackPool()` created the SC-2 park slot `hiddenpool@0` **lazily on first
hidden-back activation** and registered it into `Setup.slots` — the very map that
`Setup.addSlots()` **clears** on every conditions rebuild. `Setup.replace()` records each
Thing's slot NAME before the clear and re-resolves it after, so a rebuild that happened
while Things were parked threw:

```
trying to move thing to slot hiddenpool@0, but it doesn't exist        (setup.ts:123)
ws.onmessage -> BaseClient.onMessage -> Collection.onUpdate
             -> World.onMatch -> World.updateConditions -> Setup.replace
```

Trigger in real play: the backend legitimately flips broadcast match conditions
`auto/HANDS -> manual/INITIAL` when it applies the manual deal mode
(`AutotableWsEndpoint.TryServerStartOnSeatFillAsync -> ApplyDealModeAsync`, surfaced by
`ChangshaToAutotableTranslator.BuildMatch`), which lands AFTER the first `things` snapshot
built the pool. `match` is the FIRST-constructed Collection, so the throw escapes the whole
client dispatch for that message and every later collection in it (seats/nicks/things/turn)
is silently dropped.

Post-throw scene (measured on :18087, fresh gameId): park slot destroyed
(`slots 325 -> 324`), **217/217 Things pointing at a dead slot generation**, 109 asymmetric
`slot.thing` pointers, `Setup.conditions` never committed.

Cascade (real stack captured from the shipped bundle, not assumed):

```
TypeError: Cannot read properties of undefined (reading 'x')
    at L.copy            (three-renderer.78479d44.js)   <- THREE Vector3.copy
    at as.prepareObjects (three-renderer.e9d0398a.js)   <- MouseUi.prepareObjects:269
```

`World.toSelect()` offered NON-RENDERED parked Things as raycast targets; their
`Thing.place()` is undefined (a Thing parked while carrying a discard/hand rotation indexes
past the park slot's single `places` entry), so `{...undefined, id}` reached
`obj.position.copy(select.position)` every frame. Seated viewers only — which is why hc1
(seated human) saw 941-5808 of them and a spectator run saw none.

## Fix (invariant, not symptom)

1. **`Setup` owns the park slot** (`Setup.HIDDEN_PARK`, `hiddenParkSlot`): one stable
   instance created at construction and re-registered by every `addSlots()` rebuild, so it
   exists before anything can target it and `thing.slot` identity survives rebuilds. Kept
   out of `slotNames` (never a deal range).
2. **`Setup.replace()` treats the park slot as multi-tenant** — parked Things keep their
   off-table home instead of going through `moveTo`'s single-occupant contract.
3. **`Slot.offTable`** marks the park slot; `World.findSlot` skips off-table slots so it can
   never become a drag-drop target (relay drag/drop unchanged).
4. **`World.parkThing()` normalises `rotationIndex` to 0** so a parked Thing always has a
   place its slot can express.
5. **`World.toSelect()` skips `thing.hidden`** — a Thing that `updateViewThings` refuses to
   render must not be hoverable/selectable/raycastable.

No try/catch, no silent skip, no dummy slot, no raycast filter that hides corrupted objects.

## Evidence (immutable :18087 = HEAD dist, vs :18191 = HEAD + this fix only)

Same driven flow, fresh gameIds:

| | pre-fix | post-fix |
|---|---|---|
| root pageerrors / run | 1 | 0 |
| `MouseUi.prepareObjects()` | `...reading 'x'` | no error |
| `toSelect()` entries w/o position | 3 (108 hidden offered) | 0 (0 hidden offered) |
| park slot after rebuild | destroyed | preserved |
| orphaned slot refs / asymmetry | 217 / 109 | 0 / 0 |
| undefined places during a full bot hand | 51-68 | 0 |
| things / parked / real tiles | 217 / 108 / 108 | 217 / 108 / 108 |

Relay preserved: FOUR_PLAYER 197 things + 645 slots, THREE_PLAYER, BAMBOO, and back to
CHANGSHA — all rebuild clean (0 orphan / 0 asym / 0 undefined places, no page errors).

## Tests added

- `tests/e2e/renderer-hidden-park-slot.spec.ts` (4 tests) — 4/4 FAIL on the pre-fix bundle,
  4/4 PASS post-fix (chromium + mobile-chrome).
- `tests/node/hidden-park-slot.invariants.test.mjs` (4 tests) — browser-free ordering
  backstop with a non-vacuous BUGGY variant.
- Existing `tests/node/sc2-slot-owner.invariants.test.mjs` 8/8 and
  `changsha-mode-policy.contract.spec.ts` 52/52 still green; strict `tsc` + eslint clean.
  `view-mode-toggle.spec.ts` "Riichi centre hidden" fails identically on the untouched
  :18087 bundle — pre-existing, not a regression.

## Handoff notes (other lanes)

- The uncaught renderer throw also **swallowed the `seats`/`nicks` entries in the same WS
  message**, which is one reason a human could not sit down on a fresh table. Ferro's
  concurrent `client-ui.ts`/`game-ui.ts` seat-handoff work (unstaged, different files) is
  the complementary half; my fix removes the dropped-dispatch cause.
- Backend note (Bishop/Frost lane, no action taken): `BuildMatch` reports `dealMode` from
  live runtime state, so binding a game legitimately flips conditions mid-session. That is
  a valid server behaviour — the renderer simply must survive it.

---

## Follow-up validation (2026-08-11, integrated scratch) — P0 4-hand stall CLOSED

Isolated integrated bundle = current worktree source (my renderer fix + Hicks/Ferro seat
handoff), built in `.dietrich/run/scratch`, served by `mahjong-autotable:uat-rev2-release`
with the built dir bind-mounted (:18192). Repo `src/frontend/autotable/` dist, staging and
`playtest-artifacts/` untouched; gate artifacts written inside the scratch tree.

Attribution control: a second bundle built from the SAME tree with ONLY my three renderer
files reverted to HEAD (`setup.ts`, `slot.ts`, `world.ts` — Ferro's seat fix retained),
served on :18193.

| corrected gate (real UI) | minus-Dietrich (:18193) | integrated (:18192) |
|---|---|---|
| P0 4-hand | FAIL — `real play made no progress for 53s (discards=15 claims=8 handEnds=1 dealers=[0,2])` | **PASS** ×2 — handEnds 4, dealers [0,2,3], 49 discards / 27 claims, GameComplete + modal, zeroSum 0 |
| hc1 (handCount=1) | FAIL — clean-error gate, 904 pageErrors | **PASS** — GameComplete, 0 pageErrors |
| pageErrors 4-hand | 5,796 (1 hiddenpool + 5,795 `.x`) | **0** |
| max no-progress gap (monitor) | game never left hand 2 in 300 s | **2.85–2.90 s** |

The minus-Dietrich failure string matches Drake's P0 artifact byte-for-byte
(`54s (discards=15 claims=8 handEnds=1 dealers=[0,2])`), so the stall is the renderer root
exception, not a backend or harness defect.

Hand-transition renderer invariants (long-lived client, 2 monitored 4-hand runs, sampled
continuously + at every boundary): park slot present, slots 325, things 217, real tiles 108,
parked 108, hidden 108, orphan slot refs 0, on-table asymmetry 0, undefined places 0,
raycast targets 109 with 0 position-less / 0 hidden offered, `MouseUi.prepareObjects()`
never throws. The long-lived client saw its 14th tile in EVERY hand (hand 2 at 51.4 s /
52.4 s, hand 3, hand 4) — the exact state Drake found missing pre-fix.

Mechanism of Drake's "long-lived client doesn't see the hand-2 14th tile": after the throw
the client keeps 109 Things bound to the destroyed slot generation, so the hand-accounting
readers (`slot.thing === thing`) mis-count the local hand while the server state is healthy;
a fresh browser rebuilds from a clean snapshot and reads 14. Same root, no separate defect.

**Conclusion: no remaining frontend state defect for this stall — the existing fix fully
closes it. No new source edits were made during this validation.**

## Source: decisions/inbox/drake-realplay-gate-stall-is-client-side.md

# Drake — real-play gate hand-2 "stall" is CLIENT-SIDE, not a backend defect

**Date:** 2026-08-11 · **Owner:** Drake (Backend Runtime Engineer) · **Against:** HEAD `349dbd67`

## Verdict

**NO backend runtime / scheduler / rules-engine defect. No product code changed.**
The server was correctly parked in `AwaitingDiscard` waiting for the seat-0 human's
discard. The browser driver never performed it.

## Reproduction

Built HEAD `349dbd67` Release in a throwaway clone, served the committed bundle on
`:18187`, ran the corrected gate spec **unmodified**. Reproduced the exact signature:

```
real play made no progress for 53s (discards=15 claims=8 handEnds=1 dealers=[0,2])
```

## Authoritative terminal state (persisted snapshot of the wedged game)

```
phase=9 (AwaitingDiscard)   hand=2   dealer=2 (BOT)   active=0 (HUMAN)
pickupSeatIndex=null  pickupRoundIndex=0  turnNumber=6  stateVersion=211
wall=52  claimWindow=null  discardPile=3
hands=[seat0:14, seat1:10+1 meld, seat2:13, seat3:10+1 meld]
seat0 tiles = [79,5,17,15,83,70,56,60,31,6,73,50,2,101]
```

Hand 2's ceremony **completed**: bot dealer auto-rolled, all bots took their batches,
the human took 0→4→8→12→13, three bot discards + two Chow claims followed, and the
human drew its 14th tile. Server log: **0** `Bot pickup failed` / `Bot dealer
dice-roll failed` / `Bot turn failed`. Nothing was scheduled-but-unfired.

## Wire evidence (watcher attached live during the failing run)

```
[62468] pickup[current] = {phase:DealerExtra, seatIndex:2, count:1, …}
[63007] pickup[current] = null      <- explicit tombstone, emitted correctly
[63007] turn[current]   = {activeSeat:2, phase:AwaitingDiscard, awaitingDiscard:true}
…
[65529] turn[current]   = {activeSeat:0, phase:AwaitingDiscard, awaitingDiscard:true}
```

## Client evidence (fresh page, SAME wedged game, same `mahjong_pid`)

```
seat=0  pickupCurrent=null  isMyPickupTurn=false  hasExtraHandTile=TRUE
turnCurrent={activeSeat:0, phase:AwaitingDiscard, awaitingDiscard:true}
14 owned hand.*@0 slots (hand.0@0 … hand.13@0)
```

A **freshly loaded** client on the identical server state renders the correct,
actionable discard affordance. The long-lived stalled session did not — so the defect
is in accumulated client world-state across the hand-1 → hand-2 transition.

## Exact required action (what the server was waiting for)

`["discard", 0, {tileId}]` for any of seat 0's 14 tiles. Supplying it un-wedged the
table immediately: `turnNumber 6→10`, `stateVersion 211→219`, `discardPile 3→7`.

**Handoff → Hicks (frontend):** `world.hasExtraHandTile()` (and therefore the gate's
`hasExtraHandTile` branch) returns false in the long-lived session while a fresh client
on the same state returns true. Suspect the per-hand reset of `world.things` slot
ownership across the `EndHand → RotateBanker → new-hand ceremony` transition, not the
wire contract — `pickup['current']=null` and the `turn` cue are both delivered correctly.

## Hudson vs Vasquez conflict — RESOLVED (Vasquez correct)

Human non-dealer (seat 1) + BOT dealer (seat 0), hand 1, through the real
`/autotable/ws` auto-seat-fill / auto-start path with **no** human roll: the bot dealer
auto-rolls and the ceremony reaches `AwaitingDiscard hands=[14,13,13,13]`. Deterministic
on both a fast and a production-shaped slow timing profile. Hudson's reported
human-seat1/bot-dealer stall **does not reproduce at HEAD**.

## Regressions landed (test-only)

- `Changsha/Acceptance/HumanSeatMultiHandManualProgressionTests.cs` — 4 tests:
  dealer rotation after a Hu onto a BOT seat re-arms the ceremony; full 4-hand
  human + 3 Hard bots to `GameComplete` with a no-progress stall detector;
  **Auto mode preserved** (never enters a pickup phase); **reconnect preserved**
  (no bot-ification, no auto-play of the human, ceremony resumes).
- `Changsha/Acceptance/WsDrivenManualProgressionRegressionTests.cs` — 4 tests:
  the same scenarios driven over the REAL `/autotable/ws` endpoint using the bundle's
  own wire verbs, fast + slow profiles.

## Validation

New 8/8 green ×4 consecutive runs (worktree) and ×3 (clean clone);
`Changsha.Acceptance|StateMachine|BankerRotation|TurnFlow|Dealing|GameCompletionLifecycle|Reconnect|Tests.Autotable`
→ **927/927**; full suite in the clean clone → **5808 passed / 2 skipped / 0 failed**;
`dotnet build src/backend/Mahjong.Autotable.slnx -c Release` → 0 errors.

## Lane discipline

Zero product-code edits. `Autotable/AutotableWsEndpoint.cs` was mid-edit (and briefly
non-compiling) under Wierzbowski; I built and tested from a throwaway clone at
`349dbd67` rather than touch it. Frontend, E2E harness, workflows, visual PNGs and the
generated bundle untouched.

## Harness gotcha worth reusing

`IChangshaGameRuntime.TryGetSnapshot` is deliberately lock-free, so a poller can observe
a mutation **before** `StateChanged` fires (both under the instance lock, snapshot
second). Any "assert the state at transition X" test must subscribe to `StateChanged`
**and** bounded-wait for the latch, otherwise it flakes ~1-in-3.
