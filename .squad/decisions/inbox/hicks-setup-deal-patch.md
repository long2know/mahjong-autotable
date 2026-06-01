# Hicks — setup-deal.ts fence-post patch (round 3)

**Date:** 2026-06-01
**Author:** Hicks (frontend/Three.js/autotable specialist)
**Trigger:** Frost's diagnostic memo (`165166d`,
`.squad/decisions/inbox/frost-wall-fence-post-fix.md`) and Stephen's
round-3 task — `wall.13.0@2` page error persisting after my round 2
(`b4c82ec`) per-variant wall-sizing patch in `setup-slots.ts`.

## TL;DR

Round 2 shrank seats 2/3 walls to `row(13)` (26 slots) in
`setup-slots.ts` + `fixupSlots()`, but the SIBLING table
`setup-deal.ts` still walked from `wall.1.0` (slotNames index 2) for
26 entries on seats 2/3 → ran off the end at `wall.13.0@2`.

Applied Frost's 6-line patch verbatim. Verified across 3 playtests.

## The patch

`src/frontend/autotable-src/src/setup-deal.ts` — `DEALS.CHANGSHA`
ranges, three blocks (INITIAL, HANDS[1], UNSHUFFLED) each had 2 lines
flipped from `wall.1.0` → `wall.0.0` for seats 2 and 3:

| Block | Before | After |
|---|---|---|
| `INITIAL` seat 2 | `['wall.1.0', 2, 26]` | `['wall.0.0', 2, 26]` |
| `INITIAL` seat 3 | `['wall.1.0', 3, 26]` | `['wall.0.0', 3, 26]` |
| `HANDS[1]` seat 2 | `['wall.1.0', 2, 13]` | `['wall.0.0', 2, 13]` |
| `HANDS[1]` seat 3 | `['wall.1.0', 3, 13]` | `['wall.0.0', 3, 13]` |
| `UNSHUFFLED` seat 2 | `['wall.1.0', 2, 26]` | `['wall.0.0', 2, 26]` |
| `UNSHUFFLED` seat 3 | `['wall.1.0', 3, 26]` | `['wall.0.0', 3, 26]` |

Seats 0/1 (`['wall.1.0', 0/1, 28]` and `[14]`/`[15]`) unchanged — they
still have the full 14-stack rows where `wall.1.0` start is safe.

The vestigial `wall.1.0` start was inherited from upstream's uniform
`row(19)` layout (seats 2,3 had 38 slots each, so index 2 + 26 = 28
< 38 was harmless). With round-2's per-seat `row(13)` for seats 2,3,
the row now has 26 slots and `dealPart()` MUST fill from index 0.

## Rebuild + verify

```bash
( cd src/frontend/autotable-src && npm run build )
# → exited 0, manifest-precache wrote 14 assets, dist-size wave K22 recorded
grep -oE '"wall\.[01]\.0",[0-3],26' src/frontend/autotable/three-renderer.e788248e.js
#   "wall.0.0",2,26   ← present (was wall.1.0)
#   "wall.0.0",3,26   ← present (was wall.1.0)
#   "wall.0.0",2,26
#   "wall.0.0",3,26
#   "wall.1.0",1,26   ← seat 1 unchanged (different variant block)
```

Bundle picked up the change cleanly.

## Playtest results (all on freshly restarted backend, `/tmp/mat-hicks-r3.db`)

```
walls-facedown.spec.mjs     → pageErrorsCount: 0  ✅
human-led.spec.mjs          → pageErrorsCount: 0  ✅
broken-deal-repro.spec.mjs  → pageErrorsCount: 0  ✅
```

The previously failing `walls-facedown` `pageErrors: ["wall.13.0@2"]`
is GONE. All other invariants on walls-facedown pass (zero foreign
hands face-up, all wall backs rotated, four-seat walls present, pickup
reached dealer hand, local seat hand face-up). One unrelated
measurement check `wallCountAtLeast100` still flags (wallCount=88
mid-pickup, which is a measurement-timing artefact in the spec, not
the fence-post bug) — out of scope for this patch.

## Visual proof

`playtest-artifacts/screenshots/hicks-final-clean-2026-06-01T20-52-57Z.png`
— walls-facedown post-deal frame, all wall tiles rendered correctly
on all four seats with the new 14/14/13/13 per-seat geometry.

## Lane discipline

Touched ONLY:
- `src/frontend/autotable-src/src/setup-deal.ts` (6 lines)
- `src/frontend/autotable/` (rebuilt bundle, machine-generated)
- `playtest-artifacts/screenshots/hicks-final-clean-*.png` (proof)
- `.squad/decisions/inbox/hicks-setup-deal-patch.md` (this memo)
- `.squad/agents/hicks/history.md` (entry)

No backend code touched. Frost's regression tests in
`AutotableTranslatorTests` (`165166d`) continue to guard the backend
side of the contract.
