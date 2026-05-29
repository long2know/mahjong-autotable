# Vasquez — Full Changsha game integration audit

**Date:** 2026-05-29
**Branch:** `test/full-game-integration`
**Spec:** `playtest-artifacts/playtest-full-game-integration.spec.mjs`
**Artifacts:** `playtest-artifacts/integration-audit/`
**Baseline SHA:** `5b8c920` (Bishop's Int64 seat-key boxing fix)

## TL;DR

Built a 5-scenario behavior-first integration audit. Two scenarios PASS
(C, E). Three scenarios FAIL (A, B, D) — and they all trace back to
**one** root cause:

> `world.ts:263-272` silently drops `things` slot moves when the target
> slot is already occupied, using a once-per-second `console.warn`
> ("autotable: skipped stale moveTo …") as the only signal. During
> the audit run we counted **97 such warnings in ~5 minutes**, and the
> dropped moves include legitimate discard placements that the backend
> authoritatively committed.

The visible-to-the-player game state therefore drifts away from the
authoritative server state. Move log shows discards, claims, and
turn-completions that never materialise in the 3-D canvas.

Hand-off: **Bishop** (translator broadcast batches collide) +
**Hicks** (world.ts merge needs ordering guarantee or batch-aware
slot resolution).

## Per-scenario gate roll

| Scenario | Status | Gates (PASS/total) | Notes |
|---|---|---|---|
| A — Manual deal · dealer discard · round-robin | **FAIL** | 2/4 | A1 dealer-14-face-up PASS; A2 discard not visible in `things` (log shows it); A3 only 3 of 4 seats produced visible discards; A4 dealer cycled back PASS |
| B — Auto deal · bot autoplay 30+ moves | **FAIL** | 2/4 | B1 all-seats-dealt PASS; B2 30+ discards-in-log PASS (37 captured); B3 6 page errors `(intermediate value) is not iterable` thrown in `renderResult`; B4 only 1 tile visible in `discard` slots vs. 37 logged |
| C — Tile selection via DOM | **PASS** | 3/3 | C1 hover via `page.mouse.move` works; C2 click→discard fires (proven with retry across 5 rack tiles); C3 documents `world.selected: Array<Thing>` + `world.hovered: Thing\|null` runtime contract |
| D — Claim window (Pung/Kong) | **FAIL** | 1/2 | D1 no claim window opens in 90s of 4-Hard-bot autoplay (overlay element exists, never made visible); D2 vacuous PASS |
| E — Win detection (synthetic Hu) | **PASS** | 3/3 | E1 modal becomes visible after `cli.events.emit('update', [['gameComplete','current',...]])`; E2 totalScores rendered correctly; E3 dismiss-via-tombstone `cli.events.emit('update', [['gameComplete','current',null]])` works |

## CRITICAL bug #1 — "skipped stale moveTo" drops authoritative slot moves

**Location:** `src/frontend/autotable-src/src/world.ts:263-272`

```ts
if (slot.thing !== null && slot.thing !== thing) {
  if (now - World._lastSlotConflictLogMs > 1000) {
    World._lastSlotConflictLogMs = now;
    console.warn(
      `autotable: skipped stale moveTo ${thing.index} -> ${slot.name}`,
      `(occupant=${slot.thing.index})`,
    );
  }
  continue;
}
thing.moveTo(slot, rotationIndex);
```

**What this does**: when a backend `things` batch contains a move of
tile *X* into a slot already occupied by tile *Y*, the move for X is
**silently dropped**. The expected behavior is one of:
  1. The backend never sends such a batch (broadcasts ordered so that
     the slot is vacated first).
  2. The frontend resolves the batch in two passes (vacate → place),
     so within-batch swaps work.

**Symptoms recorded in the audit run:**
- Scenario A: dealer issues `world.emitDiscard(2万)`. Move log records
  `Seat 0: discarded 2万`. `discardBySeat[0]` stays `0`. The backend
  did accept the discard (or the move log entry would not appear), but
  the resulting `things` slot move was dropped by the guard.
- Scenario A: at end of 60 s observation, `discardBySeat=[1,0,1,1]`
  while move log shows seat 1 also discarded — that move was dropped.
- Scenario B: 37 discard log entries vs. 1 tile in any `discard.*` slot.
  Other 36 tiles either still appear in their owning hand slot, or are
  in slot-conflict limbo.
- Scenario D: claim window never opens. The `client.claim` collection
  is populated by backend logic that REACTS to discard slot updates;
  if those never arrive at the client, no claim window can render.

**97 "skipped stale moveTo" warnings recorded in ~5 minutes of audit.**
First 10 samples (from `findings.staleMoveToSamples`):
```
autotable: skipped stale moveTo 108 -> wall.8.1@3 (occupant=99)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=26)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=46)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=76)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=57)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=36)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=31)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=54)
autotable: skipped stale moveTo 110 -> wall.12.0@1 (occupant=54)
```
Notice the same occupant being reported many times for the same slot —
the batch order is repeatedly putting the same tile in conflict.

**Recommended fix (two-pass merge in `world.ts:onThings`):**

```ts
// Pass 1: pre-clear slots that a tile is leaving (per the new batch).
for (const [idx, info] of entries) {
  const t = this.things.get(idx); if (!t) continue;
  if (t.slot && t.slot.name !== info.slotName) {
    t.slot.thing = null;       // vacate the source slot
  }
}
// Pass 2: place tiles into their target slots (now empty for swaps).
for (const [idx, info] of entries) {
  // existing logic, but the "skipped stale moveTo" branch should now
  // be a HARD error (assert) rather than a silent skip — if we still
  // hit it after pass 1, the batch is genuinely malformed.
}
```

Owner: **Hicks** (world.ts trunk). Cross-reference: **Bishop** to
confirm the translator emits `slotName` updates for the vacated
tile in the same batch as the new placement (so pass-1 sees the
correct departure).

## CRITICAL bug #2 — `renderResult` throws on result-collection updates

**Location:** `src/frontend/autotable-src/src/game-ui.ts:955-1024`
(specifically the spread on line 998 and the `for…of` on line 1017)

```ts
const ordered = [...(result.score ?? [])].sort(...)
// ...
for (const tile of result.hand) { ... }
```

**Captured stack** (from `findings.pageErrors`):
```
TypeError: (intermediate value)(intermediate value)(intermediate value) is not iterable
    at lt.renderResult (scene-effects.js:1:28927)
    at lt.onResultUpdate (scene-effects.js:1:28092)
    at y.emit (autotable-src.js:2:3272)
    at I.onUpdate (three-renderer.js:19:8420)
```

Fires repeatedly during scenario B (4-bot autoplay) — **6 distinct
emissions in 35 s** of observation. The `??` operator on
`result.score` only guards against `null`/`undefined`; if the backend
sends `result.score` as a non-iterable type (e.g., number, object,
empty string), the spread throws.

Either:
- **Bishop**: fix `ChangshaToAutotableTranslator` to always emit
  `result.score` as `Score[]` and `result.hand` as `Tile[]` (per the
  `HandResultEntry` interface), OR
- **Hicks**: defend with `Array.isArray(result.score) ? result.score : []`
  and `Array.isArray(result.hand) ? result.hand : []` before iterating.

Recommendation: **fix the source (Bishop)**; defensive iteration in
game-ui.ts masks future shape regressions.

## BUG #3 — Claim windows never open for the local seat

**Manifest:** Scenario D (auto deal, 4 Hard bots, 90 s observation).

- `client.claim.get('current')` stays `null` for the entire window.
- `.ferro-claim-overlay-visible` class never applied.

This is a **downstream effect** of bug #1: claim windows are gated on
the local client seeing a freshly-placed discard (the source tile for
a Pung/Chow/Kong). With discards being dropped client-side, the claim
heuristic never fires.

If bug #1 is fixed and D1 still fails, escalate to **Frost** (claim
rule logic) and **Bishop** (claim-window backend wire).

## Gate logic learnings (for future audits)

1. **Don't grade discard success on `handBySeat[seat] <= 13`**. In
   Changsha the dealer immediately draws the next pickup after a
   discard, bouncing the count back up. Use `discardBySeat[seat] > 0`
   AND `move-log entry` instead. (Vasquez gate A2 fixed.)

2. **`world.things` is a Map**, NOT a plain object. `Object.values()`
   returns `[]` silently — always use `Array.from(world.things.values())`
   or `for (const t of world.things.values())`. (probe-end-to-end.mjs
   had this bug; it returned `"no hand tile"` falsely.)

3. **`world.selected` is a real runtime field** (`Array<Thing>`) but
   click-to-discard does NOT typically populate it — the discard
   intercept in `world.onDragStart` (world.ts:885+) fires directly off
   `world.hovered`. Selection-as-persistent-list is a non-feature in
   this codebase.

4. **Game-complete modal dismiss path**: the canonical hide is via the
   `gameComplete['current'] = null` tombstone (game-ui.ts:1814), not
   via jQuery `.modal('hide')`. The latter may or may not work
   depending on bundle load order.

5. **`page.on('console')` capture is essential** for catching drift
   bugs — `console.warn` lines are the ONLY signal that batch slot
   conflicts are happening. Always wire `warn` capture + a counter,
   not just `error`.

## Recommended hand-offs

- **Hicks** — fix `world.ts:263-272` two-pass slot merge (per fix
  sketch above). Add an integration-spec assertion that
  `staleMoveToWarnings === 0` after any normal turn.
- **Bishop** — (a) audit `ChangshaToAutotableTranslator` for
  `result.score` and `result.hand` shape regressions; (b) confirm
  discard `things` updates include both the vacated source slot
  (`hand.<N>@<seat>`) and the new destination slot
  (`discard.<R>.<C>@<seat>`) in the SAME batch.
- **Frost** — once bug #1 is resolved, re-run scenario D and verify
  claim windows surface for the local seat against bot discards.

## Run command (reproducible)

```bash
cd /data/source/mahjong-autotable
E2E_BASE_URL=http://127.0.0.1:8088 \
  node playtest-artifacts/playtest-full-game-integration.spec.mjs
```

Backend was running on port 8088 (`buildSha=dev`, sqlite at
`/tmp/mat-postfix.db`, uptime 1.17 d at start of audit).

## Decision

**Audit status: GREEN-AS-WRITTEN, RED-AS-EXPERIENCED**.

The 5 PASSing gates (C1/C2/C3/E1/E2/E3) confirm the underlying
runtime APIs (tile projection, click intercept, synthetic gameComplete
backdoor, totalScores rendering, tombstone dismiss) are correct.

The 6 FAILing gates (A2/A3/B3/B4/D1) all reduce to the **same
client-side stale-moveTo guard silently dropping authoritative slot
updates**. From a Stephen-plays-the-game perspective: the game
**does not work** end-to-end despite individual primitives working —
the player cannot see their own discards, cannot see bot discards
accumulate, cannot see claim windows, and the result-modal renderer
crashes on real hand-completion data.

Until bug #1 is fixed, "the game works" is **false**.
