# Frost — Wall Fence-Post Fix

**Date:** 2026-06-01
**Author:** Frost (backend specialist)
**Trigger:** Stephen's `wall.13.0@2` page-error after commits `99c1af0` (mine)
+ `b4c82ec` (Hicks). Playtest spec
`playtest-artifacts/playtest-walls-facedown.spec.mjs` fails with
`pageErrors: ["wall.13.0@2"]`.

## TL;DR

**Backend is healthy.** The `wall.13.0@2` page error originates from the
**frontend** local pre-WS render path
(`src/frontend/autotable-src/src/setup-deal.ts`), NOT from anything the
backend ships over the wire. Adding 4 backend regression tests
(`AutotableTranslatorTests`) to pin the per-seat cap contract and
guarantee the backend can never become the source of a fence-post regression.
**Recommend Hicks update `setup-deal.ts` to match his new per-seat wall
geometry** (canonical fix below). The backend fix the task brief proposed
(option 1: "backend caps per-seat") is **already shipped in `99c1af0`** —
`EnumerateWallSlotsInOrder` skips `col >= WallStackCount(seat)` and
`WallSlot(seat, col, layer)` validates `col < WallStackCount(seat)` and
throws on violation.

## Investigation

### Trace 1 — backend slot emit

`AutotableSlotMap.cs:124-140` (mine, `99c1af0`):

```csharp
public static IEnumerable<(int Seat, int Col, int Layer)> EnumerateWallSlotsInOrder()
{
    const int MaxStacks = 14;
    for (var col = 0; col < MaxStacks; col++)
    {
        for (var seat = 0; seat < 4; seat++)
        {
            if (col >= WallStackCount(seat)) continue;   // ← per-seat cap
            for (var layer = 0; layer < 2; layer++)
                yield return (seat, col, layer);
        }
    }
}
```

For `col = 13`, `seat = 2`, `WallStackCount(2) = 13`, so the `continue`
fires and the tuple `(2, 13, layer)` is **never yielded**. The single
consumer of this iterator is `ChangshaToAutotableTranslator.cs:449-464`
which feeds the yielded tuples into `AutotableSlotMap.WallSlot(seat, col,
layer)` — itself a defensive throw on `col >= WallStackCount(seat)`. **No
backend code path can produce `wall.13.0@2`.**

Confirmed via 6 backend tests (5 new + 1 expanded — pin the contract for
both pre-deal synthesized and post-deal authoritative paths, across 5
seeds). All pass on this commit.

### Trace 2 — where `wall.13.0@2` actually comes from

The page error captured by `playtest-walls-facedown.spec.mjs` is
`err.message = "wall.13.0@2"`. Playwright's `pageerror` handler parses a
thrown string as `Error(name, message)` where `name = "${prefix-before-:}"`
and `message = "${after-:}"`. Empirically verified with a minimal repro:

```js
page.on('pageerror', err => console.log(err.name, '|', err.message));
// throw `slot not found: wall.13.0@2`
//   → name='slot not found' | message='wall.13.0@2'
```

So the actual thrown text is one of:
- `throw \`slot not found: ${slotName}\`` — `setup.ts:249` or `setup.ts:256`
- `throw \`Unknown slot: ${slotName}\`` — `setup.ts:303`

Walking `setup.ts` and `setup-deal.ts`:

1. The autotable bundle's `World` constructor reads `?dealMode=manual`
   from the URL and overrides `conditions.dealType = DealType.INITIAL`
   (`world.ts:110-113`), then calls `setup.setup(this.conditions)` which
   ends with `this.deal(0)` (`setup.ts:31`).

2. `setup.deal(0)` walks `DEALS.CHANGSHA.INITIAL`
   (`setup-deal.ts:31-41`):

   ```ts
   INITIAL: [{
     ranges: [
       ['wall.1.0', 0, 28],
       ['wall.1.0', 1, 28],
       ['wall.1.0', 2, 26],   // ← BUG: starts at slotNames[2]=wall.1.0
       ['wall.1.0', 3, 26],   //   walks 26 entries → wall.13.0/wall.13.1
     ],
   }],
   ```

3. `dealPart()` at `setup.ts:242-265` walks `for (let i = idx; i < idx +
   n; i++) { targetSlotName = slotNames[i] + '@' + effectiveSeat; }`.
   For seat 2 (`effectiveSeat = (2 + 0) % 4 = 2`), starting at
   `idx = slotNames.indexOf('wall.1.0') = 2`, walking 26 steps walks
   indices `2..27` of `slotNames`. Since Group A's per-seat wall stems
   (`wall.0.0 .. wall.13.1`, 28 entries) get inserted into the union'd
   `slotNames` before Group B's (which only contribute `wall.0.0 .. wall.12.1`),
   `slotNames[26] = wall.13.0` and `slotNames[27] = wall.13.1` are present
   as stems. But `this.slots.get('wall.13.0@2')` returns **undefined**
   because Hicks's per-seat layout only allocates `wall.{0..12}@{2,3}` for
   seats 2 and 3. → throws `slot not found: wall.13.0@2`.

### Trace 3 — why the existing tests caught nothing

The frontend bundle's `setup.deal()` runs **once at first paint** before
the WebSocket delivers the authoritative snapshot. Backend tests cover
the wire protocol exclusively; the bundle's local pre-WS render is out
of scope for backend xUnit. Vasquez's existing playtest specs are the
only artefact that exercises this code path, and `walls-facedown.spec.mjs`
captured it (pageError fired, the assertion harness flagged it).

## Why option 1 (backend caps) does not solve this

Re-reading the task brief: "**Backend caps:** Backend
`EnumerateWallSlotsInOrder` returns slot keys constrained by per-seat
column count for the current variant." This describes the **status quo
shipped in `99c1af0`** — and the page error persists. The backend is
already capping; the frontend's local `setup-deal.ts` table is the
unfixed half of Hicks's per-seat wall split (round 2 of `b4c82ec`
updated `setup-slots.ts` and `fixupSlots()` but missed the sibling
`setup-deal.ts`).

## Recommended fix — frontend (Hicks's lane)

Update `src/frontend/autotable-src/src/setup-deal.ts` `DEALS.CHANGSHA`
ranges so seats 2 and 3 start at `wall.0.0` (slotNames index 0) instead
of `wall.1.0` (index 2):

```diff
 CHANGSHA: {
   INITIAL: [{
     ranges: [
       ['wall.1.0', 0, 28],
       ['wall.1.0', 1, 28],
-      ['wall.1.0', 2, 26],
-      ['wall.1.0', 3, 26],
+      ['wall.0.0', 2, 26],
+      ['wall.0.0', 3, 26],
     ],
   }],
   HANDS: [
     { … hand.0/hand.extra unchanged … },
     {
       ranges: [
         ['wall.1.0', 0, 14],
         ['wall.1.0', 1, 15],
-        ['wall.1.0', 2, 13],
-        ['wall.1.0', 3, 13],
+        ['wall.0.0', 2, 13],
+        ['wall.0.0', 3, 13],
       ],
     },
   ],
   UNSHUFFLED: [{
     ranges: [
       ['wall.1.0', 0, 28],
       ['wall.1.0', 1, 28],
-      ['wall.1.0', 2, 26],
-      ['wall.1.0', 3, 26],
+      ['wall.0.0', 2, 26],
+      ['wall.0.0', 3, 26],
     ],
   }],
 },
```

The `wall.1.0` start was a vestige of the old uniform `row(19)` layout
(where seats 2,3 had 38 slots each so starting at index 2 was harmless).
With Hicks's per-seat `row(13)` for seats 2,3, the row only has 26 slots
and we need to fill from index 0.

Bundle rebuild required (`cd src/frontend/autotable-src && npm run build`)
plus a fresh playtest run.

## Backend regression test (this commit)

`src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableTranslatorTests.cs`
gains 4 new tests under
`Snapshot_…NeverEmits_OverLimitWallSlots` (+ the matching iterator-direct
test `EnumerateWallSlotsInOrder_NeverYields_OverLimitTuples`):

- `Snapshot_PreDeal_NeverEmits_OverLimitWallSlots` (theory, 2 cases —
  Seating + RollingDice) — pins the synthesized 108-tile wall path.
- `Snapshot_PostDeal_NeverEmits_OverLimitWallSlots` — pins the
  authoritative 55-tile post-deal path.
- `Snapshot_NeverEmits_OverLimitWallSlots_AcrossSeeds` (theory, 5 cases)
  — pins the same invariant under multiple shuffles.
- `EnumerateWallSlotsInOrder_NeverYields_OverLimitTuples` — pins the
  iterator directly so a future refactor can't silently shift the bug.

The assertion uses TWO regexes that match the EXACT patterns the task
brief called out:
- `^wall\.(?:1[3-9]|[2-9]\d|\d{3,})\.[01]@[23]$` — seats 2/3 cols ≥13
- `^wall\.(?:1[4-9]|[2-9]\d|\d{3,})\.[01]@[01]$` — seats 0/1 cols ≥14

Plus a stronger structural cross-check via
`AutotableSlotMap.WallStackCount(seat)`.

Filter:
```bash
dotnet test src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj \
  --no-restore -c Debug --nologo --verbosity minimal \
  --filter "FullyQualifiedName~AutotableTranslator|FullyQualifiedName~AutotableSlotMap"
# → Passed!  - Failed: 0, Passed: 35, Skipped: 0
```

All pass on `99c1af0` (no production code change needed in this PR).
The tests will fail RED if someone reverts the per-seat cap in
`EnumerateWallSlotsInOrder` OR weakens `WallSlot`'s range guard.

## Pitfall captured

**Playwright `pageerror` parses thrown strings into name+message at the
first `:`.** A `throw \`slot not found: wall.13.0@2\`` surfaces in test
findings as `err.message = "wall.13.0@2"` (NOT the full thrown string).
When triaging fence-post bugs, the captured `message` is the LAST
template-literal interpolation, not the whole thrown string. Cross-check
the thrown text with `err.name`. The verification repro is:

```js
page.on('pageerror', err => console.log(err.name, '|', err.message));
// throw `slot not found: wall.13.0@2`
//   → 'slot not found' | 'wall.13.0@2'
```

This affects any squad member triaging similar `${some-slot-name}` page
errors in the future.

## Lane discipline

Touched ONLY:
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableTranslatorTests.cs`
  (+5 tests, 1 helper method, ~100 LOC)
- `.squad/decisions/inbox/frost-wall-fence-post-fix.md` (this memo)
- `.squad/agents/frost/history.md` (entry)

Per the task brief's explicit lane discipline, did NOT touch the
frontend. The diagnosis ↑ shows the actual fix lives in
`src/frontend/autotable-src/src/setup-deal.ts` (Hicks's lane).

## Hand-off to Hicks

The 6-line frontend patch above resolves the page error. Suggested
playtest verification command (matches the task brief verbatim):

```bash
cd /data/source/mahjong-autotable
# Rebuild bundle after setup-deal.ts edit:
( cd src/frontend/autotable-src && npm run build )
# Restart backend on fresh DB (existing instance on PID via lsof -ti :8088):
PID=$(lsof -ti :8088); [ -n "$PID" ] && kill $PID; sleep 4
rm -f /tmp/mat-frost-fence.db*
cd src/backend/src/Mahjong.Autotable.Api
ConnectionStrings__Sqlite="Data Source=/tmp/mat-frost-fence.db" \
  ASPNETCORE_URLS="http://0.0.0.0:8088" \
  ASPNETCORE_ENVIRONMENT="Development" \
  nohup dotnet run --no-launch-profile > /tmp/mat-frost-fence.log 2>&1 &
sleep 60 && curl -sf http://127.0.0.1:8088/health
cd /data/source/mahjong-autotable
E2E_BASE_URL=http://127.0.0.1:8088 timeout 90 node playtest-artifacts/playtest-walls-facedown.spec.mjs 2>&1 | tail -20
```

Expected after the frontend patch: `pageErrors: []`,
`walls-facedown.spec.mjs` reports 6/6 invariants pass.
