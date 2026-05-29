# Bishop — `HandResultEntry.score` wire-shape fix

**Author:** Bishop (Backend Dev)
**Date:** 2026-05-29
**Branch / PR:** `fix/result-payload-shape`
**Hand-off origin:** Stephen's directive via Copilot — "Fan out and perform an
audit with real integration testing"; concrete trigger was Vasquez's
2026-05-29 integration-audit `findings.json` capturing 6
`TypeError: ... is not iterable` exceptions in 35 s of scenario-B bot
autoplay (`renderResult` in `scene-effects.0e…js`).

## Root-cause finding

The frontend `HandResultEntry.score` is typed as `Array<ScoreDelta>`
(`src/frontend/autotable-src/src/types.ts:199-211`) where
`ScoreDelta = { seat: number, delta: number }`. The result-modal renderer
(`game-ui.ts:998`) does:

```ts
const ordered = [...(result.score ?? [])].sort((a, b) => a.seat - b.seat);
```

The backend was emitting `HandResultEntry.Score` as
`Dictionary<int, int>` (`Autotable/AutotableProtocol.cs:132` pre-fix), which
`System.Text.Json` serializes as a JSON OBJECT
(`{"0":100,"1":-50,"2":-25,"3":-25}`), not a JSON array. Spreading a JS
object throws `TypeError: (intermediate value) is not iterable`, which
took out the whole win-screen render and downstream `recordHandResult` /
banner update path. The `?? []` only catches `null` / `undefined`, not
"a non-iterable value" — defense-in-depth on the client wasn't going to
save us.

Tellingly, the in-source docblock for the `result` collection
(`AutotableProtocol.cs:63` pre-fix) ALREADY described the contract
correctly as `score: { seat: points }[]` (note the `[]` suffix). The
C# type drifted away from the documented wire contract at some point
during the multi-wave evolution of `HandResultEntry`; nothing in the
test suite pinned the JSON shape, so the drift was silent.

This was choice **B** from the briefing's possible root causes ("the
HandResultEntry C# DTO has wrong property types"), not A/C/D.

## Fix

`AutotableProtocol.cs`:
- Introduced `ScoreDeltaEntry` (`{ seat: int, delta: int }`, camelCase
  JSON names) with an XML doc explicitly calling out the
  spread-must-be-iterable contract and the historical breakage.
- Changed `HandResultEntry.Score` from `Dictionary<int, int>` to
  `List<ScoreDeltaEntry>` (default `[]` so partial / pre-scoring emits
  serialize as `"score": []`, not `null` or `{}`).
- Strengthened the `Hand` field's docblock with the same iteration
  contract.
- Updated the top-of-file `ChangshaCollectionKinds` docblock for the
  `result` collection to spell out `{ seat: int, delta: int }[]`
  explicitly and append a one-liner about the iterability requirement.

`ChangshaToAutotableTranslator.cs`:
- `BuildHandResult` now projects `state.CumulativeScores` to a
  seat-ordered `List<ScoreDeltaEntry>`. `OrderBy(seat)` gives
  deterministic snapshots so connect-time full-syncs replay byte-identically
  (the frontend's `recordHandResult` dedupe uses
  `JSON.stringify(last) === JSON.stringify(result)`).

`HandResultPayloadShapeTests.cs` (new):
- 8 xUnit tests under `Category=PayloadShape`:
  1. `DefaultHandResultEntry_Score_SerializesAsEmptyJsonArray`
  2. `DefaultHandResultEntry_Hand_SerializesAsEmptyJsonArray`
  3. `PopulatedScore_SerializesAsArrayOfSeatDeltaObjects`
  4. `PopulatedHand_SerializesAsArrayOfNumbers`
  5. `RoundTrip_ScoreAndHand_AreIterableLikeFrontendExpects` (mirrors
     the exact `[...result.score]` + `for (const tile of result.hand)`
     semantic from `game-ui.ts:renderResult`)
  6. `BuildHandResult_FromFreshState_EmitsScoreArrayAndEmptyHand`
  7. `BuildHandResult_WithEmptyCumulativeScores_EmitsEmptyScoreArray`
  8. `EncodeHandResult_AsCollectionEntry_PreservesArrayShape` (asserts
     the array shape survives the `[kind, key, value]` triple
     `CollectionEntryJsonConverter` path)

All eight assert `JsonValueKind.Array` via `JsonDocument.Parse` round-trip
through `AutotableJson.Options` — the same serializer options used on the
WebSocket wire. This is the schema-shape regression the briefing called
for; future drift between the C# DTO and the frontend `ScoreDelta`
interface trips a fast, targeted-filter-friendly failure.

## Schema-shape test result

```
$ dotnet test --no-build --filter \
    "FullyQualifiedName~ChangshaTo|FullyQualifiedName~HandResultPayloadShape|FullyQualifiedName~AutotableTranslator|FullyQualifiedName~FanCatalogIntegration|FullyQualifiedName~WinResultSurface"

Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34, Duration: 156 ms
```

All 34 targeted tests pass — the new 8 alongside 26 existing
translator / Hu-context / fan-catalog cases, confirming no regression
to the surrounding result-payload code paths.

## Post-fix audit (Vasquez integration spec)

Restarted backend on port 8088 with the fix; re-ran
`playtest-full-game-integration.spec.mjs`:

| Metric                                  | BEFORE | AFTER |
|-----------------------------------------|--------|-------|
| `B3_noPageErrors.pageErrorsDelta`       | **6**  | **0** |
| `renderResult / "is not iterable" hits` | 6      | **0** |
| `E_winDetection` scenario               | (modal never opened) | **PASS** (3/3 gates) |

`E_winDetection` flipped from failing to fully passing — the win modal
now opens, shows totals, and dismisses cleanly. This is a downstream
win from the same fix: `recordHandResult` no longer crashes mid-flight,
so the `result` collection update propagates through to the modal show
side effect.

Other scenario failures (A2/A3 dealer-discard round-trip, B2 log-count
parity, C2 raycaster click hit-testing, D1 claim-window never fires)
are **outside this PR's lane** — they map to Hicks's `world.ts`
two-pass slot merge (running in parallel) and bot-claim-strategy
tuning, not the result payload schema.

## Notes for future passes

1. **The frontend types directory is the source of truth for wire
   shapes.** Anytime a backend DTO ships fields the frontend will
   spread, iterate, or destructure, add a `PayloadShape`-trait test
   that round-trips through `AutotableJson.Options` and asserts
   `JsonValueKind.Array` / `Object` explicitly. The C# type system
   alone doesn't catch `Dictionary` vs. `List<EntryDto>` confusion
   because both compile fine; the test layer is the only line of
   defense.

2. **`?? []` is NOT a guard against shape mismatch on the frontend.**
   It only handles `null` / `undefined`. Any wire field whose
   semantics include "iterable" must be array-shaped on the wire —
   period. Hicks (frontend) may want to add a defensive
   `Array.isArray(result.score)` coerce-or-warn at the bundle entry
   point as belt-and-braces, but the source-of-truth fix lives here.

3. **The C# `Dictionary<int, int>` → JSON object pitfall is the same
   shape-class of bug as the May 2026 `ternary long/double → double`
   converter issue** (`bishop-dealerextra-fix.md`). Both stem from
   the JSON serializer faithfully encoding a C# type whose runtime
   shape doesn't match what the frontend statically expects.
   Recommend a one-time sweep on remaining `Dictionary<int, *>`
   fields in `AutotableProtocol.cs` to confirm none are similarly
   typed for fields the frontend will iterate.

**Decision memo:** this file.
**Squash commit on main:** see git log post-merge.
