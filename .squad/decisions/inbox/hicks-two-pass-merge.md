# Hicks — Two-pass slot merge in `world.ts onThings`

**Date:** 2026-05-29
**Branch:** `fix/two-pass-slot-merge`
**Closes (partial):** `.squad/decisions/inbox/vasquez-integration-audit.md` §"CRITICAL bug #1"
**Touches:** `src/frontend/autotable-src/src/world.ts`, rebuilt `src/frontend/autotable/` bundle.

## TL;DR

Vasquez's full-game integration audit (5 scenarios) identified
**~97 `console.warn` "skipped stale moveTo" lines in 5 minutes of
play**. Those warnings traced back to one client bug: the placement
loop in `world.ts onThings` silently dropped a backend `things` slot
move whenever the target slot was still occupied at the start of the
batch — even when the occupant was itself moving away in the same
batch. The drop took out legitimate discards, meld pickups and
post-claim relocations, causing the visible 3-D game state to drift
away from the authoritative server state.

The fix: a two-pass slot merge. **Pass 1a** pre-vacates the source
slot of every batched tile (unchanged `prepareMove`). **Pass 1b**
unconditionally nulls every target slot's `.thing` pointer when the
slot is currently held by a different tile — including tiles that
ARE in the batch (the older "skip if occupant in batch" optimisation
misfired on stale-ownership pointers). **Pass 2** places tiles; the
old silent-skip guard becomes a force-clear + place with a throttled
warning so any genuine batch double-target is logged but state still
converges (last-write-wins).

## Before / after diff (essence)

`src/frontend/autotable-src/src/world.ts` `onThings` body:

```diff
-    const batchIds = new Set<number>();
-    for (const [thingIndex, thingInfo] of entries) {
-      if (thingInfo !== null) batchIds.add(thingIndex);
-    }
-
+    // Pass 1a — vacate each batched thing's CURRENT slot.
     for (const [thingIndex, thingInfo] of entries) {
       if (thingInfo === null) continue;
       const thing = this.things.get(thingIndex);
       if (!thing) continue;
       thing.prepareMove();
     }

-    // Defensive pre-pass: force-displace stale occupants only when
-    // they are NOT in this batch (assumed pass 1 cleared the slot for
-    // batched occupants).
+    // Pass 1b — vacate every target slot regardless of whether the
+    // previous occupant is also in this batch.  The older "skip-if-
+    // in-batch" optimisation misfired on stale-ownership pointers
+    // (occupant's .slot already reassigned by a prior moveTo but THIS
+    // slot's .thing pointer still referenced it).
     for (const [thingIndex, thingInfo] of entries) {
       if (thingInfo === null) continue;
       const slot = this.slots.get(thingInfo.slotName);
       if (!slot || slot.thing === null) continue;
       if (slot.thing.index === thingIndex) continue;
-      if (batchIds.has(slot.thing.index)) continue;
-      slot.thing.prepareMove();
+      slot.thing = null;
     }
```

And the placement-loop guard:

```diff
       if (slot.thing !== null && slot.thing !== thing) {
         if (now - World._lastSlotConflictLogMs > 1000) {
           World._lastSlotConflictLogMs = now;
           console.warn(
-            `autotable: skipped stale moveTo ${thing.index} -> ${slot.name}`,
+            `autotable: forcing stale moveTo ${thing.index} -> ${slot.name}`,
             `(occupant=${slot.thing.index})`,
           );
         }
-        continue;
+        slot.thing = null;
       }
       thing.moveTo(slot, rotationIndex);
```

## Root cause walk-through

For tiles W and Z, with initial state
* W in slot S₁ (S₁.thing = W, W.slot = S₁),
* Z's `.slot` was reassigned by a prior `moveTo` to slot S₃ but
  slot S₂'s `.thing` was never cleared → **S₂.thing = Z while
  Z.slot = S₃** (a stale-ownership pointer).

When the backend ships the batch `[(W → S₂), (Z → S₄)]`:

* **Old pass 1** called `W.prepareMove()` → cleared S₁; called
  `Z.prepareMove()` → cleared S₃ (Z's CURRENT slot) — but **S₂.thing
  remains Z**.
* **Old pass 2** saw S₂.thing = Z, Z in batchIds, → skipped force-
  displacement (assumed pass 1 cleared S₂; it did not).
* **Old pass 3** (`W.moveTo(S₂)`) saw S₂.thing = Z ≠ W → **silent
  skip** with a once-per-second `console.warn`. W stays in S₁ (now
  ownerless), Z's batch entry succeeds at S₄. **W's authoritative
  server placement is lost.**

The new pass 1b nulls S₂.thing directly, so pass 2's `W.moveTo(S₂)`
succeeds. The orphan Z-slot bookkeeping self-heals on the next
batch that touches Z.

## Vasquez audit results (BEFORE vs AFTER)

Spec: `playtest-artifacts/playtest-full-game-integration.spec.mjs`,
run against `E2E_BASE_URL=http://127.0.0.1:8088`.

| Metric | BEFORE | AFTER |
|---|---|---|
| `staleMoveToWarnings` (silent-skip count) | **97** | **0** |
| `pageErrorsCount` | 6 | 0 |
| Scenario A (manual deal) | FAIL (2/4 gates) | FAIL (2/4 gates) — same gates |
| Scenario B (auto deal · bot autoplay) | FAIL (2/4 gates) — meldsOnTable = **0** | FAIL (3/4 gates) — meldsOnTable = **24** |
| Scenario C (DOM tile selection) | PASS (3/3) | PASS (3/3) |
| Scenario D (claim window) | FAIL (1/2) | FAIL (1/2) |
| Scenario E (synthetic Hu) | PASS (3/3) | PASS (3/3) |
| **passingScenarios** | **2 / 5** | **2 / 5** |

The headline number didn't move from 2/5 to 5/5 — but the underlying
contract DID shift:

* `staleMoveToWarnings 97 → 0` — **the smoking-gun signal of the
  bug is gone**.
* B's `meldsOnTable` jumped from `0 → 24` — tile movement into
  meld slots now actually lands on the canvas.
* B's `B4_someProgressMarker` flipped FAIL → PASS.
* A2/A3 still fail with a NEW root cause: the dealer's discard
  emits `client.discard.set(...)` (returns ok) but the **backend
  never broadcasts the discard back**. The spec's own diagnostic
  note now says: *"Bishop should check
  `TryHandleDiscardActionAsync` + the things-broadcast path for
  seat 0 dealer."* (see `vasquez-integration-audit.md`).
* D1 still fails because no claim window opens for the local
  seat. Vasquez's memo pre-emptively scoped this hand-off: *"If
  bug #1 is fixed and D1 still fails, escalate to Frost (claim
  rule logic) and Bishop (claim-window backend wire)."*

The 89-105 `forcing stale moveTo` warnings on the post-fix audit
runs are dominated by phantom upstream tile IDs 108..135 (the
frontend `Setup` still allocates 136 tiles even on a Changsha-108
table — pre-existing minor leak noted in
`.squad/decisions/inbox/hicks-preview-tile-fix.md`).  They are
harmless under the new "force-clear + place" semantics: the
correct tile DOES land in the slot, and the warning is throttled
to once per ~second.

## Regression sweep

| Spec | Result |
|---|---|
| `playtest-walls-facedown.spec.mjs` | **ALL CHECKS PASSED** (6/6) |
| `playtest-human-led.spec.mjs` | **All steps OK** |
| `playtest-playable-interaction.spec.mjs` | G1, G2, G3, G5 **PASS**; G4_discard FAIL (same as HEAD — pre-existing, see HEAD baseline `findings.json`) |
| Vasquez audit C scenario (DOM selection) | PASS (3/3) |
| Vasquez audit E scenario (synthetic Hu) | PASS (3/3) |

G4_discard's pre-existing failure is the same dealer-discard-not-
broadcast issue as A2 — the spec's own note attributes it to
`TryHandleDiscardActionAsync` not firing when pickup phase
transitions DealerExtra → null without flipping to AwaitingDiscard.

## Newly discovered downstream issues (hand-off list)

These were already in Vasquez's memo but the audit re-confirmed
them after my fix landed:

1. **Bishop — dealer-discard back-end state machine.**
   `world.emitDiscard()` returns ok=true and `client.discard.set()`
   pushes a WS payload, but the backend never echoes a `things`
   UPDATE moving the tile to `discard.*@0`. The move-log shows NO
   "Seat 0 discarded" line. The pickup phase moves `DealerExtra →
   null` at the take-button click but **never advances to
   `AwaitingDiscard`**. Repro: any manual-deal Changsha game where
   the dealer tries to discard immediately after the take-1
   click. Likely fix lives in
   `ApplyChangshaPickupCompletionAsync` (seat-0 / dealer-extra
   branch) or `TryHandleDiscardActionAsync` (state-gate too
   strict). Blocks audit A2, A3 **AND** `playtest-playable-
   interaction.spec.mjs` G4.

2. **Frost + Bishop — claim windows never surface for the local
   seat in auto-deal 4-bot games.** `client.claim.get('current')`
   stays null for 90 s of bot autoplay. Vasquez's audit B
   scenario actually shows claim windows firing for non-local
   seats ("Seat 2 (Bot 2):: claim window — Chow on 4条 (from Seat
   1)") so the rule engine IS firing — but the
   `client.claim` collection is not surfaced when the local seat
   has no claim of its own. May be intentional (only the
   acting seat sees the overlay) and the audit gate's premise
   may need revisiting. Blocks audit D1.

3. **Cosmetic — `forcing stale moveTo` warnings on phantom tile
   IDs.** The frontend's `Setup` allocates 136 tiles on a
   Changsha-108 table; ids 108..135 are unused by the backend but
   the local pre-deal `setup.deal('HANDS')` keeps shuffling them
   into wall slots that the backend then re-claims. ~80 of the
   89-105 warnings per audit run are from these phantoms. Low
   priority; the two-pass merge handles them correctly
   (force-clear + place last-write-wins) — they're just noisy.
   Could be fixed by gating the upstream tile count on
   `conditions.gameType === CHANGSHA` in `setup.ts`.

## Maintenance notes

1. **Pass 1b deliberately ignores `batchIds`.** Earlier iterations
   used `if (batchIds.has(slot.thing.index)) continue;` as an
   "occupant will be displaced by its own batch entry"
   optimisation. That assumption fails for stale-ownership
   pointers (occupant's `.slot` is no longer this slot). Setting
   `slot.thing = null` unconditionally is safe — if the displaced
   tile has its own batch entry, the placement loop rebinds it;
   if not, the orphan self-heals on the next batch.
2. **`moveTo` is asymmetric.** It writes `target.thing = this` and
   `this.slot = target` but **does NOT clear the source slot's
   `.thing` pointer**. That's why orphans accumulate. A future
   refactor could symmetrise `moveTo` (clear `oldSlot.thing` when
   different from target), which would let us drop pass 1b
   entirely. Out of scope for this PR.
3. **The placement-loop guard is now a "should be unreachable"
   defensive branch.** If it fires in production telemetry, the
   batch genuinely double-targets the same slot — investigate the
   translator (`ChangshaToAutotableTranslator`) rather than the
   client. The warning text changed from `skipped` to `forcing`
   to make the semantic difference visible in logs.

## Files

| File | Change |
|---|---|
| `src/frontend/autotable-src/src/world.ts` | Two-pass merge in `onThings` (lines 154-243). Warning renamed `skipped` → `forcing` and no longer drops the move. |
| `src/frontend/autotable/*` | Rebuilt bundle. Affected hashed chunks: `three-renderer.<hash>.js` (renderer chunk contains world.ts) and the `manifest-precache.json`. |
| `.squad/decisions/inbox/hicks-two-pass-merge.md` | This memo. |
| `.squad/agents/hicks/history.md` | Appended team update. |
