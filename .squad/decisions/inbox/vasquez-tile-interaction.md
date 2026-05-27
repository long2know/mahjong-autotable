# Vasquez memo — Tile interaction discovery (W4 follow-up)

**Date:** 2026-05-28
**Agent:** Vasquez (Tester / Quality)
**Branch:** `test/playable-interaction-gate`
**Spec:** `playtest-artifacts/playtest-playable-interaction.spec.mjs`
**Artifacts:** `playtest-artifacts/playable/`

## Context

Wave 4 landed four fixes that visually restored the Changsha dealing
ceremony — face-down walls, per-4 pickup, seat-0 hand face-up, and
the dealing-ceremony rule engine. Stephen's open question remained
unanswered: **"I can't even select a tile from my tiles."**

This memo files the discovery results of a behaviour-first playtest
spec that drives a full manual-deal Changsha game and an auto-deal
game, attempts UI-level tile selection at canvas-pixel coordinates,
and observes whether a discard round-trips through the backend.

## Gate matrix

| Gate | Description | Status |
| --- | --- | --- |
| G1 `setup` | Manual mode loads + seat 0 claim + `world.deal('HANDS')` lands; `#pickup-take-btn` visible | **PASS** |
| G2 `takeButton` | Clicking `#pickup-take-btn` increments dealer hand (13→14) and sets `hasExtraHandTile()===true` | **PASS** |
| G3 `selectUI` | Playwright `mouse.move(x,y)` at a projected canvas pixel sets `world.hovered` to the targeted tile id | **PASS** |
| G4 `discard` | A press/release on a hand tile (or direct `emitDiscard`) causes the tile to land in a `discard.*@N` slot AND the move-log records the discard | **FAIL** |
| G5 `autoDealFaceUp` | `?dealMode=auto`: seat-0's own concealed hand renders face-up (rotationIndex===1) after deal | **PASS** |

Three independent runs (cold backend cache, fresh game ids) reproduced
the same gate pattern.

## G4 failure — root cause sketch

Frontend emission path is correct end-to-end:

1. `world.onDragStart()` (world.ts:885) recognises a hand tile owned
   by `this.seat` while `hasExtraHandTile() === true` and invokes
   `world.emitDiscard(tile)` (world.ts:896).
2. `emitDiscard` validates `slot.group === 'hand' && slot.seat ===
   this.seat`, sets `this.client.discard.set(this.seat, { tileId })`
   (world.ts:406), and returns `true`.
3. Direct API call from the spec verified `emitDiscard` returns
   `true` for the chosen tile id.

What does NOT happen:

- The backend (`AutotableWsEndpoint.TryHandleDiscardActionAsync`)
  never echoes a `things` UPDATE moving the tile to `discard.*@0`.
- `world.things` count for `slot.group === 'discard'` stays at 0
  for 3+ seconds after emission.
- The move-log captures `Seat 0: picking 1 tile (dealerextra)` but
  no subsequent `Seat 0: discarded …` entry.
- `client.pickup.get('current').phase` transitions
  **DealerExtra → null** at the take-button click, then never
  advances. Expected: DealerExtra → AwaitingDiscard (or `inPlay`).
- The dealer is stranded with 14 (or more) tiles and no valid
  command the backend will accept.

## Owner: **Bishop** (backend)

The fix lives in the Changsha state machine's DealerExtra-completion
hook. Most likely `ApplyChangshaPickupCompletionAsync` (or whichever
runtime hook completes a 1-tile dealerExtra pickup) is failing to
transition to `AwaitingDiscard` and consequently
`TryHandleDiscardActionAsync` rejects on phase guard.

Suggested investigation order:

1. Add a backend log line at the moment `pickup.phase=DealerExtra`
   completes — confirm whether the state machine reaches
   `AwaitingDiscard`.
2. If it does, confirm `TryHandleDiscardActionAsync` accepts the
   incoming `discard` payload from seat 0 in that phase.
3. If it does not, fix the pickup-completion transition.
4. Add a backend test that round-trips a manual-deal game from
   `DealerExtra` → discard → `inPlay` for the dealer.

## Repro

```bash
# Backend already running on 8088 with /tmp/mat-final.db.
cd /data/source/mahjong-autotable
E2E_BASE_URL=http://127.0.0.1:8088 \
  node playtest-artifacts/playtest-playable-interaction.spec.mjs
```

Outputs gate summary on stdout, full diagnostic in
`playtest-artifacts/playable/findings.json`, and screenshots
`01..05-*.png`.

## Side-finding (informational; out-of-scope here)

After the take-button click, the per-frame snapshot occasionally
shows own-hand tiles carrying `thing.claimedBy === undefined`
(rather than `null`). `world.toSelect()` filters strictly on
`=== null` (world.ts:1185), which silently excludes those tiles
from the raycaster. The spec polls past the transient
`undefined` state and the bug self-resolves before the discard
attempt — but a settle race in `world.update` (world.ts:276
`thing.claimedBy = thingInfo.claimedBy`) is suspect. Hicks may
want to coerce the assignment to `?? null` for defensive parity.

## What works (positive findings)

- G3 PASS confirms canvas-pixel raycasting is functional. The
  projection in the spec computes
  `worldPos.project(camera).toNDC().toCanvasOffset()` from
  `world.toSelect()[i].position` and Playwright's
  `page.mouse.move(clientX, clientY)` reliably sets
  `world.hovered`.
- G5 PASS confirms Hicks's seat-self rotation override applies
  uniformly to auto and manual deal modes — dealer sees own hand
  face-up in BOTH paths.
- G2 PASS confirms the `#pickup-take-btn` HUD wiring is correct
  end-to-end.

## Recommended Stephen-facing summary

> The "Take 1" button works, the dealer's hand grows to 14, and
> mouse-clicks ARE landing on tiles. The block is that after the
> dealer takes the +1, the backend doesn't open the discard window —
> so the discard command goes out and gets silently dropped. The
> dealer is stuck. Bishop owns the next fix in
> `ApplyChangshaPickupCompletionAsync` to advance the state machine
> from DealerExtra to AwaitingDiscard.
