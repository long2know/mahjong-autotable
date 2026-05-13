# Bishop — Phase 5a Backend (Strategy C Autotable WS Endpoint)

**By:** Bishop (Backend Dev)
**Date:** 2026-05-14
**Branch:** `stlong/changsha-3d-phase5a`
**Refs:** `docs/rules/changsha-3d-renderer-plan.md` §5 (Strategy C, ~900 LOC),
spike inbox at `.squad/decisions/inbox/copilot-directive-2026-05-13-3d-phase5a-defaults.md`,
Hicks's frontend wiring at `.squad/decisions/inbox/hicks-phase5a-frontend.md`

## TL;DR

Backend now exposes a fake upstream `pwmarcz/autotable` WS server at
`/autotable/ws` that speaks `NEW`/`JOIN`/`JOINED`/`UPDATE` verbatim. Hicks's
unchanged `autotable.9519e86d.js` bundle connects and renders authoritative
Changsha state in 3D — walls, hands (own seat face-up, others face-down),
discards, and melds (concealed kongs face-down). The byte-identical bundle is
not touched; Strategy C succeeds.

Build clean. 203 → 226 backend tests pass (+23 new). 0 failures, 7 v2-skipped.

## Files added (all under `src/backend/src/Mahjong.Autotable.Api/Autotable/`)

| File | LOC | Purpose |
|---|---:|---|
| `AutotableProtocol.cs` | ~140 | Envelope records (`AutotableInboundMessage`, `JoinedMessage`, `UpdateMessage`), `CollectionEntry` with custom `CollectionEntryJsonConverter` serializing `[kind, key, value]` JSON tuples, shared `AutotableJson.Options` (camelCase + ignore null) |
| `AutotableSlotMap.cs` | ~130 | `WallSlot/HandSlot/DiscardSlot/MeldSlot` builders, `UpstreamTypeIndex(tileId) = tileId / 4`, `WallStackCount(seat)` returning 14/14/13/13 per Default #6, `EnumerateWallSlotsInOrder()` yielding 108 (seat, col, layer) tuples |
| `ChangshaToAutotableTranslator.cs` | ~260 | Pure `Translate(state?, viewerSeat?, viewerPlayerId?) → IReadOnlyList<CollectionEntry>` emitting match + 4 seats + 4 nicks + 1 dice + 108 things. Forces `fives='000'`. Viewer-seat hand → FACE_UP (rot 1); other hands → FACE_DOWN (rot 2); concealed kong → FACE_DOWN (rot 2). Null state → always-available pattern (match-entry only). |
| `AutotableWsEndpoint.cs` | ~280 | `MapAutotableWs` extension wiring `/autotable/ws`; `AutotableConnectionManager` singleton subscribes to `IChangshaGameRuntime.StateChanged` and broadcasts a full snapshot per connection on every state mutation. Handles NEW (random gameId + empty snapshot), JOIN (resolve via runtime), UPDATE (discard with Debug log — Phase 5b will translate to hub commands). |

## Tests added (under `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/`)

All carry `[Trait("Category", "Phase5a")]`.

| File | Tests | Coverage |
|---|---:|---|
| `AutotableTranslatorTests.cs` | 19 | typeIndex mapping, 14/14/13/13 wall split totaling 108, JOINED snapshot counts (108 things + 4 seats + 4 nicks + 1 match + 1 dice with 2-element array), slot-name uniqueness, hand size 13/14 per seat, 55 wall things post-deal, discard slot movement, pung 3-entry meld, concealed kong 4-entry FACE_DOWN meld, null-state always-available, `fives='000'` forced, viewer-seat face-up vs face-down |
| `AutotableWsEndpointTests.cs` | 4 | Unknown gameId → JOINED + match-only UPDATE; known gameId → JOINED + full UPDATE; state mutation triggers second UPDATE; synthetic bundle UPDATE discarded without crashing |

**Total delta:** +23 tests (203 → 226 passing).

## Files modified

| File | Change |
|---|---|
| `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs` | Added `event Action<string>? StateChanged` to interface + class. Fired inside `PersistSnapshotAsync` *before* the DB write, *unconditionally* (independent of `PersistSnapshots` flag, since tests run with persistence off). Handler exceptions caught + logged so a misbehaving WS broadcast can never break game state. |
| `src/backend/src/Mahjong.Autotable.Api/Program.cs` | `using Mahjong.Autotable.Api.Autotable;`, `AddSingleton<AutotableConnectionManager>()`, `app.UseWebSockets()` immediately after `UseCors`, force-resolve manager + `app.MapAutotableWs()` before `app.Run()` |

**Untouched (per Strategy C constraints):** `src/frontend/autotable/**`
(including `autotable.9519e86d.js` and every bundled asset), every other
backend file outside `Autotable/` + the one runtime line above.

## Endpoint contract (canonical)

```
ws://host/autotable/ws[?seat={0..3}][&gameId={id}]
```

- Path verified against upstream `client-ui.ts:getUrl()`:
  `path.substring(1, path.lastIndexOf('/')+1) + 'ws'` resolves
  `/autotable/` → `autotable/ws` exactly.
- Optional `seat` query string controls viewer-seat face-up/face-down logic
  during translation.
- Optional `gameId` query string is consumed when the bundle later sends
  `{type:"JOIN", gameId}`. The endpoint responds with
  `{type:"JOINED", gameId, playerId, isFirst:false}` followed by
  `{type:"UPDATE", entries:[...], full:true}`.

## Always-available pattern

A JOIN against an unknown `gameId` does **not** error — it returns a JOINED
plus an UPDATE containing only the `match` entry. This is what makes
Hicks's "iframe mounts before backend has created a Changsha game" path
work: the bundle renders an empty table until the first state change broadcasts.

## `fives='000'` keystone (Strategy C 1:1 mapping)

The bundle's `Setup.tileIndex(i, conditions)` defaults to `floor(i/4)`, but
patches the mapping (e.g. i=16→34 = red-5-wan) whenever `fives !== '000'`.
Forcing `match[0].conditions.fives = '000'` triggers
`World.onMatch → setup.replace()` which rebuilds 136 tiles with clean
`i/4` typeIndices. Result: Changsha tileId N → bundle thing-index N with
no translation table on either side. This is *the* reason Strategy C is
viable; the entire `UpstreamTypeIndex` helper is just `tileId / 4`.

## Known limitations / Phase 5b+5c carry-overs

1. **28 wind/dragon things visible** — The bundle's local Setup creates 136
   things regardless of server state. We emit only 108 (Changsha's tile
   set), leaving 28 wind/dragon things parked at their initial bundle wall
   positions. Visual artifact for Phase 5b cleanup; cannot delete via WS
   protocol (no "shrink thing array" operation in upstream).
2. **Full snapshot per state change** (not incremental diffs) — Wire size
   is ~50–80 KB per UPDATE for a deal-batch. Phase 5c optimization.
3. **Bundle-initiated UPDATEs discarded** with a Debug log. Phase 5b will
   translate bundle drag-and-drop events to Changsha hub commands (discard,
   claim, pass).
4. **One game per backend instance** — Per Default #8. The
   `AutotableConnectionManager` does not multiplex; the single
   `IChangshaGameRuntime` instance is the single game.
5. **`isFirst: false` always** — Phase 5a hard-codes this so the bundle
   never echoes `sendOnConnect` collections back at us. Multi-instance work
   in Phase 5b+ will re-evaluate whether the host of a fresh table needs
   `isFirst: true`.

## Deviations from spike text

- **Wall split:** Followed the task's explicit Default #6 lock (seats 0,1
  get 14 stacks; 2,3 get 13). Spike doc text had a 0,2/1,3 split. The
  task brief is the operating contract; the spike is reference.
- **No source-gen JSON contexts.** The Phase 5a task brief suggested
  "source-gen preferred (matches codebase)", but the actual codebase
  convention is reflection-based `JsonSerializer.Serialize(obj, options)`
  across every Changsha + Tables + Persistence file. Followed the actual
  convention.

## Build/test commands

```bash
dotnet build src/backend/Mahjong.Autotable.slnx --nologo
dotnet test  src/backend/Mahjong.Autotable.slnx --nologo --no-build
dotnet test  src/backend/Mahjong.Autotable.slnx --nologo --filter "Category=Phase5a"
```

## Handoff

- **Hicks:** No frontend changes needed. The bundle wired via
  `?gameId=X&seat=Y` will now receive live `UPDATE` broadcasts when a
  Changsha game state mutates.
- **Hudson:** Phase 5a test coverage is in place (+23 backend tests);
  Phase 5b will need ws-roundtrip tests for bundle→hub command translation
  once that lands.
- **Phase 5b owners:** Inbound `UPDATE` translation lives in
  `AutotableWsEndpoint.HandleInboundMessage`; replace the Debug-log line
  with a translator that resolves bundle slot moves (e.g. hand→discard) to
  `IChangshaGameRuntime` commands (Discard, ClaimResolve).
